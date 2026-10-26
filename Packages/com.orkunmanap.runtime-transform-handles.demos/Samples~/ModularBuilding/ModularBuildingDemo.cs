using System;
using System.Collections.Generic;
using System.Linq;
using TransformHandles;
using TransformHandles.Utils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using InputWrapper = TransformHandles.Utils.InputWrapper;

namespace TransformHandles.Samples.ModularBuilding
{
    /// <summary>A grid-aligned modular building workshop using only the public Core API.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class ModularBuildingDemo : MonoBehaviour
    {
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private Transform[] targets;
        [SerializeField] private Renderer floorRenderer;
        [SerializeField] private Material targetMaterial;
        [SerializeField] private TransformHandleSettings interactionSettings;
        [SerializeField] private VisualTreeAsset uiLayout;
        [SerializeField] private PanelSettings uiPanelSettings;

        private readonly List<Transform> _selection = new List<Transform>();
        private readonly Dictionary<Transform, PosRotScale> _initial = new Dictionary<Transform, PosRotScale>();
        private readonly List<Material> _materials = new List<Material>();
        private readonly Dictionary<Renderer, Material> _originalMaterials = new Dictionary<Renderer, Material>();
        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        private TransformHandleManager _manager, _ownedManager;
        private TransformHandleSettings _previousSettings;
        private Camera _previousCamera;
        private bool _previousUiBlock, _ownsManager, _initialized, _tearingDown, _dragging;
        private Handle _handle;
        private HandleType _mode = HandleType.Position;
        private const int MinimumHandlePixels = 64, MaximumHandlePixels = 512;
        private int _handleSizePixels = 192;
        private int _lastSizeEditFrame = -1;
        private SliderInt _sizeSlider;
        private TextField _sizeInput;
        private bool _sizeEditing;
        private int _lastStartFrame = -1, _pressFrame = -1;
        private bool _pressBlocked, _releasePending, _pressCancelled;
        private Vector3 _pressPosition, _releasePosition;
        private bool _releaseBlocked;
        private GameObject _uiObject, _ownedEventSystem;
        private UIDocument _document;
        private PanelSettings _runtimePanelSettings;
        private VisualElement _uiRoot, _workshop, _helpCard, _helpBackdrop;
        private Label _status, _selectionLabel, _selectionHint;
        private Button _moveButton, _rotateButton, _resetButton, _helpToggleButton, _helpCloseButton;
        private bool _helpPanelOpen, _closeHelpPanelPending;
        private string _uiState;
        private string _notice = "Select a wall or corner and arrange your layout.";
        private float _yaw = 25f, _pitch = 42f, _distance = 18f;
        private Vector3 _cameraFocus = new Vector3(0, 0.5f, 0);
        private int _navigationButton = -1;
        private bool _navigationStarted;
        private Vector3 _navigationOrigin, _navigationPrevious;
        private int _pickMask;
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern float TH_ModularBuildingReadScroll();
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void TH_ModularBuildingResetScroll();
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void TH_ModularBuildingReleaseScroll();
#endif

        public int SelectionCount => _selection.Count;
        public Handle ActiveHandle => _handle;
        public bool IsDragging => _dragging;

        private void Awake()
        {
#if ENABLE_INPUT_SYSTEM && !TH_INPUTSYSTEM
            Debug.LogError("Modular Building requires Input System 1.1.1 or later when the new input backend is enabled.", this);
            enabled = false;
            return;
#endif
            if (sceneCamera == null || targetMaterial == null || interactionSettings == null ||
                interactionSettings.EnableShortcuts || targets == null || targets.Length == 0 ||
                targets.Any(t => !ValidModule(t)) ||
                targets.Distinct().Count() != targets.Length)
            {
                Debug.LogError("Modular Building requires its camera, material, shortcuts-disabled settings and distinct unparented modules on y=0 with unit scale, integer X/Z, quarter-turn yaw and child colliders/renderers.", this);
                enabled = false;
                return;
            }
            if (uiLayout == null || uiPanelSettings == null)
            {
                Debug.LogError("Modular Building requires its UI Toolkit layout and panel settings. Reimport the complete sample before playing.", this);
                enabled = false;
                return;
            }
            var layer = LayerMask.NameToLayer("TransformHandle");
            if (layer < 0)
            {
                Debug.LogError("Run Tools > Transform Handles > Setup Layer before playing the Modular Building sample.", this);
                enabled = false;
                return;
            }
            _pickMask = ~(1 << layer);
            var colors = new[] { new Color(.65f,.76f,.71f), new Color(.80f,.73f,.59f),
                new Color(.62f,.72f,.84f), new Color(.76f,.65f,.69f),
                new Color(.72f,.77f,.61f), new Color(.71f,.67f,.80f) };
            for (var i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                _initial.Add(target, Capture(target));
                foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
                    SetMaterial(renderer, colors[i % colors.Length], false);
            }
            if (floorRenderer != null) SetMaterial(floorRenderer, new Color(.87f,.89f,.91f), true);
            BuildUi();
            _initialized = true;
            UpdateCamera();
        }

        private void OnEnable()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            TH_ModularBuildingResetScroll();
#endif
            if (!_initialized) return;
            _ownsManager = !Resources.FindObjectsOfTypeAll<TransformHandleManager>().Any(m => m.gameObject.scene.IsValid() && m != _ownedManager);
            _manager = TransformHandleManager.Instance;
            if (_ownsManager) _ownedManager = _manager;
            _previousCamera = _manager.MainCamera;
            _previousSettings = _manager.Settings;
            _previousUiBlock = _manager.BlockWhenPointerOverUI;
            _manager.MainCamera = sceneCamera;
            _manager.Settings = interactionSettings;
            _manager.BlockWhenPointerOverUI = true;
            _uiObject.SetActive(true);
            BindUi();
            if (_ownedEventSystem != null) _ownedEventSystem.SetActive(true);
            _pressFrame = -1;
            _lastStartFrame = -1;
            _releasePending = false;
            RefreshUi();
        }

        private void Update()
        {
            if (!_initialized || _manager == null) return;
            if (_selection.Any(t => t == null))
            {
                RebuildHandle();
            }
            Vector3 mouse = InputWrapper.MousePosition;
            var editingSize = SuppressShortcuts(Time.frameCount);
            if (InputWrapper.GetMouseButtonDown(0))
            {
                var overUi = PointerOverUi();
                if (_sizeEditing && !overUi) FinishSizeEdit(false);
                BeginSelectionPress(Time.frameCount, mouse, overUi || editingSize);
            }
            if (InputWrapper.GetMouseButtonUp(0))
            {
                _releasePending = true;
                _releasePosition = mouse;
                _releaseBlocked = PointerOverUi();
            }
            if (InputWrapper.GetKeyDown(KeyCode.Escape)) HandleEscape(Time.frameCount);
            if (!_dragging)
            {
                if (!editingSize && InputWrapper.GetKeyDown(KeyCode.W)) SetMode(HandleType.Position);
                if (!editingSize && InputWrapper.GetKeyDown(KeyCode.E)) SetMode(HandleType.Rotation);
            }
        }

        private void LateUpdate()
        {
            if (!_initialized || _manager == null) return;
            // All manager Updates have completed. Our earlier LateUpdate moves the camera
            // before Core synchronizes handle visuals, without relying on Update ordering.
            ProcessNavigation(InputWrapper.MousePosition,
                InputWrapper.GetMouseButtonDown(0), InputWrapper.GetMouseButtonDown(1), InputWrapper.GetMouseButtonDown(2),
                InputWrapper.GetMouseButton(0), InputWrapper.GetMouseButton(1), InputWrapper.GetMouseButton(2),
                PointerOverUi(), SuppressShortcuts(Time.frameCount), ReadScroll(), Time.frameCount);
            UpdateCamera();
            if (_releasePending)
            {
                _releasePending = false;
                if (CanSelectOnRelease(_releasePosition, _releaseBlocked))
                {
                    var ray = sceneCamera.ScreenPointToRay(_releasePosition);
                    Transform picked = null;
                    if (Physics.Raycast(ray, out var hit, 1000, _pickMask))
                        picked = ResolveModule(hit.transform);
                    if (picked != null) SelectTarget(picked);
                    else ClearSelection();
                }
                _pressFrame = -1;
            }
            ApplyDeferredPanelClose();
            RefreshUi();
        }

        // Frame stamps survive manager-first Update order and drag-end callbacks in the release frame.
        private void BeginSelectionPress(int frame, Vector3 position, bool overUi)
        {
            _pressFrame = frame;
            _pressPosition = position;
            _pressBlocked = overUi;
            _pressCancelled = false;
        }

        private bool CanSelectOnRelease(Vector3 position, bool overUi)
        {
            return _pressFrame >= 0 && !_pressCancelled && !_pressBlocked && !overUi && !_dragging &&
                _lastStartFrame < _pressFrame && LogicalPointerDistance(position, _pressPosition) < 6f;
        }

        public void SelectTarget(Transform target)
        {
            if (!_initialized || !isActiveAndEnabled || _dragging) return;
            target = ResolveModule(target);
            if (target == null) return;
            _selection.Clear();
            _selection.Add(target);
            RebuildHandle();
            _notice = "Drag the handle to place this module.";
        }

        private Transform ResolveModule(Transform hit)
        {
            while (hit != null)
            {
                if (_initial.ContainsKey(hit)) return hit;
                hit = hit.parent;
            }
            return null;
        }

        public void ClearSelection()
        {
            _pressCancelled = true;
            RemoveOwnedHandle();
            _selection.Clear();
            _notice = "Selection cleared. Your layout is kept.";
        }

        public void SetMode(HandleType mode)
        {
            if (!CanChange() || (mode != HandleType.Position && mode != HandleType.Rotation)) return;
            _mode = mode;
            ConfigureHandle();
        }

        public void ResetLayout()
        {
            if (!_initialized || !isActiveAndEnabled || _dragging) return;
            ClearSelection();
            foreach (var pair in _initial) if (pair.Key != null) Restore(pair.Key, pair.Value);
            _mode = HandleType.Position;
            CancelNavigation();
            _yaw = 25f; _pitch = 42f; _distance = 18f;
            _cameraFocus = new Vector3(0, .5f, 0);
            UpdateCamera();
            _notice = "Scene reset. Handle size preference is kept.";
        }

        private bool CanChange() => _initialized && isActiveAndEnabled && !_dragging && _manager != null;

        private void RebuildHandle()
        {
            RemoveOwnedHandle();
            _selection.RemoveAll(t => t == null);
            if (_selection.Count == 0 || _manager == null || !isActiveAndEnabled) return;
            _handle = _manager.CreateHandle(_selection[0]);
            if (_handle == null) return;
            _handle.AutoScale = true;
            _handle.AutoScaleSizeInPixels = _handleSizePixels;
            _handle.OnInteractionStartEvent += OnDragStart;
            _handle.OnInteractionEndEvent += OnDragEnd;
            ConfigureHandle();
        }

        private void ConfigureHandle()
        {
            if (_handle == null) return;
            TransformHandleManager.ChangeHandleType(_handle, _mode);
            _handle.Axes = _mode == HandleType.Position ? HandleAxes.XZ : HandleAxes.Y;
            _manager.ChangeHandleSpace(_handle, Space.World);
            _handle.SnappingType = _mode == HandleType.Position ? SnappingType.Absolute : SnappingType.Relative;
            _handle.PositionSnap = new Vector3(1, 0, 1);
            _handle.RotationSnap = 90f;
        }

        private void OnDragStart(Handle handle)
        {
            if (handle != _handle || _tearingDown) return;
            _lastStartFrame = Time.frameCount;
            CancelNavigation();
            _dragging = true;
        }

        private void OnDragEnd(Handle handle)
        {
            if (_tearingDown || handle != _handle) return;
            _dragging = false;
        }

        private void RemoveOwnedHandle()
        {
            if (_tearingDown) return;
            _tearingDown = true;
            var handle = _handle;
            _handle = null;
            try
            {
                if (handle != null)
                {
                    handle.OnInteractionStartEvent -= OnDragStart;
                    handle.OnInteractionEndEvent -= OnDragEnd;
                    try { if (_manager != null) _manager.RemoveHandle(handle); }
                    finally { if (handle != null) Destroy(handle.gameObject); }
                }
            }
            finally { _dragging = false; _tearingDown = false; }
        }

        private void OnDisable()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            TH_ModularBuildingReleaseScroll();
#endif
            CancelNavigation();
            _pressCancelled = true;
            _releasePending = false;
            if (!_initialized) return;
            ClearSelection();
            _helpPanelOpen = _closeHelpPanelPending = false;
            UnbindUi();
            if (_uiObject != null) _uiObject.SetActive(false);
            if (_ownedEventSystem != null) _ownedEventSystem.SetActive(false);
            if (_manager != null)
            {
                _manager.MainCamera = _previousCamera;
                _manager.Settings = _previousSettings;
                _manager.BlockWhenPointerOverUI = _previousUiBlock;
            }
            _manager = null;
        }

        private void OnDestroy()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            TH_ModularBuildingReleaseScroll();
#endif
            if (_ownedManager != null) Destroy(_ownedManager.gameObject);
            foreach (var pair in _originalMaterials)
                if (pair.Key != null && _materials.Contains(pair.Key.sharedMaterial)) pair.Key.sharedMaterial = pair.Value;
            foreach (var material in _materials) if (material != null) Destroy(material);
            if (_uiObject != null) Destroy(_uiObject);
            if (_runtimePanelSettings != null) Destroy(_runtimePanelSettings);
            if (_ownedEventSystem != null) Destroy(_ownedEventSystem);
        }

        private static PosRotScale Capture(Transform t) => new PosRotScale { Position = t.position, Rotation = t.rotation, Scale = t.localScale };
        private static void Restore(Transform t, PosRotScale state) { t.SetPositionAndRotation(state.Position, state.Rotation); t.localScale = state.Scale; }
        private static bool ValidModule(Transform t)
        {
            if (t == null || t.parent != null || (t.localScale - Vector3.one).sqrMagnitude > .000001f) return false;
            var p = t.position;
            if (!Finite(p.x) || !Finite(p.y) || !Finite(p.z) || Mathf.Abs(p.y) > .0001f ||
                Mathf.Abs(p.x - Mathf.Round(p.x)) > .0001f || Mathf.Abs(p.z - Mathf.Round(p.z)) > .0001f) return false;
            var yaw = t.eulerAngles.y;
            return Quaternion.Angle(t.rotation, Quaternion.Euler(0, Mathf.Round(yaw / 90f) * 90f, 0)) < .01f &&
                t.GetComponentsInChildren<Collider>().Any(c => c.enabled && !c.isTrigger) &&
                t.GetComponentsInChildren<Renderer>().Any(r => r.enabled);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private bool PointerOverUi()
        {
            var system = EventSystem.current;
            if (system == null) return false;
            // PanelRaycaster needs the active input module after an EventSystem re-enable.
            if (system.currentInputModule == null) return true;
            // Query the current position instead of EventSystem's potentially previous-frame hover cache.
            _uiHits.Clear();
            system.RaycastAll(new PointerEventData(system) { position = InputWrapper.PointerPosition }, _uiHits);
            return _uiHits.Any(hit => hit.module is UnityEngine.UI.GraphicRaycaster || hit.module is PanelRaycaster);
        }

        private void SetMaterial(Renderer renderer, Color color, bool grid)
        {
            _originalMaterials[renderer] = renderer.sharedMaterial;
            var material = new Material(targetMaterial) { color = color, name = "Modular Building " + renderer.name };
            material.SetFloat("_Grid", grid ? 1 : 0);
            _materials.Add(material);
            renderer.sharedMaterial = material;
        }

        private void CancelNavigation()
        {
            _navigationButton = -1;
            _navigationStarted = false;
        }

        private void OnApplicationFocus(bool focused)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            TH_ModularBuildingResetScroll();
#endif
            if (focused) return;
            CancelNavigation();
            _pressCancelled = true;
            _pressFrame = -1;
            _releasePending = false;
        }

        private void ProcessNavigation(Vector3 position, bool leftDown, bool rightDown, bool middleDown,
            bool leftHeld, bool rightHeld, bool middleHeld, bool overUi, bool editing, float scroll, int frame)
        {
            // A handle press owns the whole gesture, even if its callbacks ended it this frame.
            if (_dragging || _lastStartFrame == frame)
            {
                CancelNavigation();
                return;
            }
            var held = _navigationButton == 0 ? leftHeld : _navigationButton == 1 ? rightHeld : middleHeld;
            if (_navigationButton >= 0)
            {
                // A button chord cannot move the camera and an object together.
                if (leftDown || rightDown || middleDown || editing)
                {
                    _pressBlocked = true;
                    CancelNavigation();
                    return;
                }
                AdvanceNavigation(position, overUi);
                if (!held) CancelNavigation();
            }
            else if (!overUi && !editing && (leftDown || rightDown || middleDown))
            {
                var button = leftDown ? 0 : rightDown ? 1 : 2;
                var heldCount = (leftHeld ? 1 : 0) + (rightHeld ? 1 : 0) + (middleHeld ? 1 : 0);
                if (heldCount > 1) { _pressBlocked = true; return; }
                if (button == 0 && (_pressBlocked || _pressCancelled || _lastStartFrame >= _pressFrame)) return;
                _navigationButton = button;
                _navigationOrigin = _navigationPrevious = position;
                _navigationStarted = false;
                if (button != 0) _pressBlocked = true;
            }
            if (!overUi && !editing && !leftHeld && !rightHeld && !middleHeld)
                ApplyZoom(scroll);
        }

        private float LogicalPointerDistance(Vector3 a, Vector3 b)
        {
            var width = _uiRoot != null ? _uiRoot.resolvedStyle.width : Screen.width;
            var scale = width > 0 && !float.IsNaN(width) ? Screen.width / width : 1f;
            return Vector3.Distance(a, b) / Mathf.Max(.001f, scale);
        }

        private void AdvanceNavigation(Vector3 position, bool overUi)
        {
            if (!_navigationStarted && LogicalPointerDistance(position, _navigationOrigin) >= 6f)
            {
                _navigationStarted = true;
                _pressCancelled = true;
            }
            var delta = position - _navigationPrevious;
            _navigationPrevious = position;
            if (!_navigationStarted || overUi) return;
            var height = Mathf.Max(1f, sceneCamera.pixelHeight);
            if (_navigationButton == 1)
            {
                var unitsPerPixel = 2f * _distance * Mathf.Tan(sceneCamera.fieldOfView * .5f * Mathf.Deg2Rad) / height;
                _cameraFocus -= (sceneCamera.transform.right * delta.x + sceneCamera.transform.up * delta.y) * unitsPerPixel;
            }
            else
            {
                _yaw += delta.x / height * 180f;
                _pitch = Mathf.Clamp(_pitch - delta.y / height * 180f, 15f, 75f);
            }
        }

        private void ApplyZoom(float scroll)
        {
            if (float.IsNaN(scroll) || float.IsInfinity(scroll)) return;
            _distance = Mathf.Clamp(_distance * Mathf.Exp(-Mathf.Clamp(scroll * .08f, -.15f, .15f)), 9f, 28f);
        }

        private static float ReadScroll()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return TH_ModularBuildingReadScroll();
#elif ENABLE_INPUT_SYSTEM && TH_INPUTSYSTEM
            var raw = UnityEngine.InputSystem.Mouse.current?.scroll.y.ReadValue() ?? 0f;
#if TH_SCROLL_NORMALIZATION
            var native = UnityEngine.InputSystem.InputSystem.settings.scrollDeltaBehavior ==
                UnityEngine.InputSystem.InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange;
#else
            var native = true;
#endif
            var windows = Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer;
            return native && windows ? raw / 120f : raw;
#else
            return Input.mouseScrollDelta.y;
#endif
        }

        private void UpdateCamera()
        {
            var rotation = Quaternion.Euler(_pitch, _yaw, 0);
            sceneCamera.transform.SetPositionAndRotation(_cameraFocus - rotation * Vector3.forward * _distance, rotation);
        }

        private void BuildUi()
        {
            if (EventSystem.current == null)
            {
                _ownedEventSystem = new GameObject("Modular Building Event System", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && TH_INPUTSYSTEM
                _ownedEventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
#else
                _ownedEventSystem.AddComponent<StandaloneInputModule>();
#endif
            }
            _uiObject = new GameObject("Modular Building Controls");
            _uiObject.SetActive(false);
            _document = _uiObject.AddComponent<UIDocument>();
            _runtimePanelSettings = Instantiate(uiPanelSettings);
            _document.panelSettings = _runtimePanelSettings;
            _document.visualTreeAsset = uiLayout;
        }

        private void BindUi()
        {
            _uiRoot = _document.rootVisualElement;
            _uiRoot.pickingMode = PickingMode.Ignore;
            _workshop = _uiRoot.Q<VisualElement>("workshop");
            _selectionLabel = _uiRoot.Q<Label>("selection-name");
            _selectionHint = _uiRoot.Q<Label>("selection-hint");
            _status = _uiRoot.Q<Label>("status");
            _selectionLabel.enableRichText = _status.enableRichText = false;
            _moveButton = _uiRoot.Q<Button>("move");
            _rotateButton = _uiRoot.Q<Button>("rotate");
            _resetButton = _uiRoot.Q<Button>("reset");
            _helpToggleButton = _uiRoot.Q<Button>("help-toggle");
            _helpCloseButton = _uiRoot.Q<Button>("help-close");
            _helpBackdrop = _uiRoot.Q<VisualElement>("help-backdrop");
            _helpCard = _uiRoot.Q<VisualElement>("help-card");
            _sizeSlider = _uiRoot.Q<SliderInt>("size-slider");
            _sizeInput = _uiRoot.Q<TextField>("size-input");
            _sizeInput.SetValueWithoutNotify(_handleSizePixels.ToString());
            _sizeSlider.SetValueWithoutNotify(_handleSizePixels);
            _moveButton.clicked += Move;
            _rotateButton.clicked += Rotate;
            _resetButton.clicked += ResetLayout;
            _helpToggleButton.clicked += ToggleHelpPanel;
            _helpCloseButton.clicked += RequestCloseHelpPanel;
            _sizeSlider.RegisterValueChangedCallback(OnSizeSliderChanged);
            _sizeInput.RegisterCallback<FocusInEvent>(OnSizeFocusIn);
            _sizeInput.RegisterCallback<FocusOutEvent>(OnSizeFocusOut);
            _sizeInput.RegisterCallback<KeyDownEvent>(OnSizeKeyDown, TrickleDown.TrickleDown);
            _uiRoot.RegisterCallback<PointerDownEvent>(OnUiPointerDown, TrickleDown.TrickleDown);
            _uiRoot.RegisterCallback<NavigationMoveEvent>(OnUiNavigation, TrickleDown.TrickleDown);
            _uiRoot.RegisterCallback<GeometryChangedEvent>(OnUiGeometryChanged);
            _helpBackdrop.RegisterCallback<PointerDownEvent>(OnHelpBackdropPressed);
            _uiState = null;
            UpdateUiLayout();
        }

        private void UnbindUi()
        {
            if (_uiRoot == null) return;
            _moveButton.clicked -= Move;
            _rotateButton.clicked -= Rotate;
            _resetButton.clicked -= ResetLayout;
            _helpToggleButton.clicked -= ToggleHelpPanel;
            _helpCloseButton.clicked -= RequestCloseHelpPanel;
            _sizeSlider.UnregisterValueChangedCallback(OnSizeSliderChanged);
            _sizeInput.UnregisterCallback<FocusInEvent>(OnSizeFocusIn);
            _sizeInput.UnregisterCallback<FocusOutEvent>(OnSizeFocusOut);
            _sizeInput.UnregisterCallback<KeyDownEvent>(OnSizeKeyDown, TrickleDown.TrickleDown);
            _uiRoot.UnregisterCallback<PointerDownEvent>(OnUiPointerDown, TrickleDown.TrickleDown);
            _uiRoot.UnregisterCallback<NavigationMoveEvent>(OnUiNavigation, TrickleDown.TrickleDown);
            _uiRoot.UnregisterCallback<GeometryChangedEvent>(OnUiGeometryChanged);
            _helpBackdrop.UnregisterCallback<PointerDownEvent>(OnHelpBackdropPressed);
            _sizeEditing = false;
            _uiRoot = null;
        }

        private void Move() => SetMode(HandleType.Position);
        private void Rotate() => SetMode(HandleType.Rotation);
        private void OnSizeSliderChanged(ChangeEvent<int> evt) => SetHandleSize(evt.newValue);
        private void OnUiGeometryChanged(GeometryChangedEvent evt) => UpdateUiLayout();

        private void UpdateUiLayout()
        {
            if (_uiRoot == null) return;
            _workshop.EnableInClassList("compact", _uiRoot.resolvedStyle.width < 1190);
        }

        private void OnUiPointerDown(PointerDownEvent evt)
        {
            _pressBlocked = _pressCancelled = true;
            CancelNavigation();
            if (_sizeEditing && evt.target is VisualElement target && target != _sizeInput && !_sizeInput.Contains(target))
                FinishSizeEdit(false);
        }

        private void OnUiNavigation(NavigationMoveEvent evt)
        {
            // W/E are workshop tools, not navigation through focused controls. Tab and arrows remain available.
            if (!InputWrapper.GetKey(KeyCode.W) && !InputWrapper.GetKey(KeyCode.E)) return;
#if !UNITY_6000_0_OR_NEWER
            evt.PreventDefault();
#endif
            evt.StopPropagation();
        }

        private void OnSizeFocusIn(FocusInEvent evt)
        {
            _sizeEditing = true;
            CancelNavigation();
            _sizeInput.parent.AddToClassList("editing");
        }

        private void OnSizeFocusOut(FocusOutEvent evt)
        {
            if (_sizeEditing) FinishSizeEdit(false);
        }

        private void OnSizeKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape && evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
            FinishSizeEdit(evt.keyCode == KeyCode.Escape);
#if !UNITY_6000_0_OR_NEWER
            evt.PreventDefault();
#endif
            evt.StopPropagation();
        }

        private void FinishSizeEdit(bool cancel)
        {
            if (!_sizeEditing) return;
            _sizeEditing = false;
            _lastSizeEditFrame = Time.frameCount;
            if (!cancel) CommitHandleSize(_sizeInput.value);
            _sizeInput.SetValueWithoutNotify(_handleSizePixels.ToString());
            _sizeInput.parent.RemoveFromClassList("editing");
            _sizeInput.Blur();
        }

        private bool SuppressShortcuts(int frame) => _sizeEditing || _lastSizeEditFrame == frame;

        private void CommitHandleSize(string text)
        {
            _lastSizeEditFrame = Time.frameCount;
            if (int.TryParse(text, out var value)) SetHandleSize(value);
            _sizeInput.SetValueWithoutNotify(_handleSizePixels.ToString());
        }

        private void SetHandleSize(float value)
        {
            if (!CanChange() || float.IsNaN(value) || float.IsInfinity(value))
            {
                _sizeSlider.SetValueWithoutNotify(_handleSizePixels);
                return;
            }
            _handleSizePixels = Mathf.Clamp(Mathf.RoundToInt(value), MinimumHandlePixels, MaximumHandlePixels);
            if (_handle != null)
            {
                _handle.AutoScale = true;
                _handle.AutoScaleSizeInPixels = _handleSizePixels;
            }
            _sizeSlider.SetValueWithoutNotify(_handleSizePixels);
            if (!_sizeEditing) _sizeInput.SetValueWithoutNotify(_handleSizePixels.ToString());
        }

        private void ToggleHelpPanel()
        {
            if (_dragging) return;
            if (_helpPanelOpen) { RequestCloseHelpPanel(); return; }
            CancelNavigation();
            _pressBlocked = _pressCancelled = true;
            _helpPanelOpen = true;
            _closeHelpPanelPending = false;
            _helpBackdrop.RemoveFromClassList("hidden");
            RefreshUi();
        }

        private void OnHelpBackdropPressed(PointerDownEvent evt)
        {
            if (evt.target == _helpBackdrop) RequestCloseHelpPanel();
        }

        private void RequestCloseHelpPanel()
        {
            if (!_helpPanelOpen) return;
            _pressBlocked = _pressCancelled = _releaseBlocked = true;
            CancelNavigation();
            _closeHelpPanelPending = true;
        }

        private void ApplyDeferredPanelClose()
        {
            if (!_closeHelpPanelPending) return;
            _closeHelpPanelPending = _helpPanelOpen = false;
            _helpBackdrop.AddToClassList("hidden");
        }

        private void HandleEscape(int frame)
        {
            if (SuppressShortcuts(frame)) return;
            if (_helpPanelOpen) RequestCloseHelpPanel();
            else ClearSelection();
        }

        private void RefreshUi()
        {
            if (_uiRoot == null) return;
            var selectionName = _selection.Count == 1 && _selection[0] != null ? _selection[0].name : "";
            var state = $"{selectionName}|{_mode}|{_dragging}|{_handle != null}|{_notice}|{_helpPanelOpen}";
            if (state == _uiState) return;
            _uiState = state;
            var selected = _selection.Count > 0;
            _selectionLabel.text = selected ? selectionName.Replace('\n', ' ').Replace('\r', ' ') : "Select a module";
            _selectionLabel.tooltip = selected ? selectionName : "";
            _selectionHint.text = !selected ? "Click a wall or corner in the scene." :
                _mode == HandleType.Position ? "Move on the ground plane." : "Turn in 90° steps.";
            _moveButton.SetEnabled(!_dragging && _handle != null);
            _rotateButton.SetEnabled(!_dragging && _handle != null);
            _resetButton.SetEnabled(!_dragging);
            _helpToggleButton.SetEnabled(!_dragging);
            _sizeInput.SetEnabled(!_dragging);
            _sizeSlider.SetEnabled(!_dragging);
            _moveButton.EnableInClassList("selected", selected && _mode == HandleType.Position);
            _rotateButton.EnableInClassList("selected", selected && _mode == HandleType.Rotation);
            _helpToggleButton.EnableInClassList("selected", _helpPanelOpen);
            _status.text = _notice;
        }
    }
}
