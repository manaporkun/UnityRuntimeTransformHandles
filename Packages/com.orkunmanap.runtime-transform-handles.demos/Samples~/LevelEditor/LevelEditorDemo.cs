using System;
using System.Collections.Generic;
using System.Linq;
using TransformHandles;
using TransformHandles.Utils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using InputWrapper = TransformHandles.Utils.InputWrapper;

namespace TransformHandles.Samples.LevelEditor
{
    /// <summary>A small, resettable prop editor using only the public Core API.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class LevelEditorDemo : MonoBehaviour
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
        private readonly Dictionary<Transform, PosRotScale> _dragStart = new Dictionary<Transform, PosRotScale>();
        private readonly List<Material> _materials = new List<Material>();
        private readonly Dictionary<Renderer, Material> _originalMaterials = new Dictionary<Renderer, Material>();
        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        private TransformHandleManager _manager, _ownedManager;
        private TransformHandleSettings _previousSettings;
        private Camera _previousCamera;
        private bool _previousUiBlock, _ownsManager, _initialized, _tearingDown, _dragging, _rebuildPending;
        private Handle _handle;
        private HandleType _mode = HandleType.Position;
        private Space _space = Space.World;
        private bool _snap;
        private const int MinimumHandlePixels = 64, MaximumHandlePixels = 512;
        private int _handleSizePixels = 192;
        private int _lastSizeEditFrame = -1;
        private SliderInt _sizeSlider;
        private TextField _sizeInput;
        private bool _sizeEditing;
        private int _lastStartFrame = -1, _pressFrame = -1;
        private bool _pressBlocked, _pressAdditive, _releasePending, _pressCancelled;
        private Vector3 _pressPosition, _releasePosition;
        private bool _releaseBlocked;
        private GameObject _uiObject, _ownedEventSystem;
        private UIDocument _document;
        private PanelSettings _runtimePanelSettings;
        private VisualElement _uiRoot, _workshop, _helpCard, _helpBackdrop;
        private Label _status, _selectionLabel, _selectionHint, _snapLabel, _inspectorStatus;
        private Button _moveButton, _rotateButton, _resetButton, _helpToggleButton, _helpCloseButton, _scaleButton, _spaceButton, _snapButton;
        private bool _helpPanelOpen, _closeHelpPanelPending;
        private string _uiState;
        private string _notice = "Adjust the handle without changing the object.";
        private float _yaw = 25f, _pitch = 42f, _distance = 18f;
        private Vector3 _cameraFocus = new Vector3(0, 0.5f, 0);
        private int _navigationButton = -1;
        private bool _navigationStarted;
        private Vector3 _navigationOrigin, _navigationPrevious;
        private int _pickMask;
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern float TH_LevelEditorReadScroll();
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void TH_LevelEditorResetScroll();
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void TH_LevelEditorReleaseScroll();
#endif

        public int SelectionCount => _selection.Count;
        public Handle ActiveHandle => _handle;
        public bool IsDragging => _dragging;

        private void Awake()
        {
#if ENABLE_INPUT_SYSTEM && !TH_INPUTSYSTEM
            Debug.LogError("Level Editor requires Input System 1.1.1 or later when the new input backend is enabled.", this);
            enabled = false;
            return;
#endif
            if (uiLayout == null || uiPanelSettings == null || sceneCamera == null || targetMaterial == null || interactionSettings == null ||
                interactionSettings.EnableShortcuts || targets == null || targets.Length == 0 ||
                targets.Any(t => t == null || t.parent != null || t.GetComponent<Collider>() == null || !ValidScale(t.localScale)) ||
                targets.Distinct().Count() != targets.Length)
            {
                Debug.LogError("Level Editor requires its camera, material, shortcuts-disabled settings and distinct collider-bearing root targets with scale in [0.25, 4].", this);
                enabled = false;
                return;
            }
            var layer = LayerMask.NameToLayer("TransformHandle");
            if (layer < 0)
            {
                Debug.LogError("Run Tools > Transform Handles > Setup Layer before playing the Level Editor sample.", this);
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
                var renderer = target.GetComponent<Renderer>();
                if (renderer != null) SetMaterial(renderer, colors[i % colors.Length], false);
            }
            if (floorRenderer != null) SetMaterial(floorRenderer, new Color(.87f,.89f,.91f), true);
            BuildUi();
            _initialized = true;
            UpdateCamera();
        }

        private void OnEnable()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            TH_LevelEditorResetScroll();
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
            _releasePending = _rebuildPending = false;
            RefreshUi();
        }

        private void Update()
        {
            if (!_initialized || _manager == null) return;
            if (_selection.Any(t => t == null))
            {
                RebuildHandle();
            }
            if (_rebuildPending)
            {
                _rebuildPending = false;
                RebuildHandle();
            }
            Vector3 mouse = InputWrapper.MousePosition;
            var editingSize = SuppressShortcuts(Time.frameCount);
            if (InputWrapper.GetMouseButtonDown(0))
            {
                var overUi = PointerOverUi();
                if (editingSize && !overUi) FinishSizeEdit(false);
                BeginSelectionPress(Time.frameCount, mouse, overUi || editingSize,
                    InputWrapper.GetKey(KeyCode.LeftShift) || InputWrapper.GetKey(KeyCode.RightShift));
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
                if (!editingSize && InputWrapper.GetKeyDown(KeyCode.R)) SetMode(HandleType.Scale);
                if (!editingSize && InputWrapper.GetKeyDown(KeyCode.X)) ToggleSpace();
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
                    if (Physics.Raycast(ray, out var hit, 1000, _pickMask) && _initial.ContainsKey(hit.transform))
                        picked = hit.transform;
                    if (picked != null) SelectTarget(picked, _pressAdditive);
                    else if (!_pressAdditive) ClearSelection();
                }
                _pressFrame = -1;
            }
            ApplyDeferredPanelClose();
            RefreshUi();
        }

        // Frame stamps survive manager-first Update order and drag-end callbacks in the release frame.
        private void BeginSelectionPress(int frame, Vector3 position, bool overUi, bool additive)
        {
            _pressFrame = frame;
            _pressPosition = position;
            _pressBlocked = overUi;
            _pressAdditive = additive;
            _pressCancelled = false;
        }

        private bool CanSelectOnRelease(Vector3 position, bool overUi)
        {
            return _pressFrame >= 0 && !_pressCancelled && !_pressBlocked && !overUi && !_dragging &&
                _lastStartFrame < _pressFrame && LogicalPointerDistance(position, _pressPosition) < 6f;
        }

        public void SelectTarget(Transform target, bool additive)
        {
            if (!_initialized || !isActiveAndEnabled || _dragging || target == null || !_initial.ContainsKey(target)) return;
            if (!additive) _selection.Clear();
            if (additive && _selection.Contains(target)) _selection.Remove(target);
            else if (!_selection.Contains(target)) _selection.Add(target);
            RebuildHandle();
            _notice = _selection.Count > 1 ? "Groups rotate and scale around their shared center." : "Adjust the handle without changing the object.";
        }

        public void ClearSelection()
        {
            _pressCancelled = true;
            _rebuildPending = false;
            RemoveOwnedHandle();
            _selection.Clear();
            _notice = "Selection cleared. Your layout is kept.";
        }

        public void SetMode(HandleType mode)
        {
            if (!CanChange() || (mode != HandleType.Position && mode != HandleType.Rotation && mode != HandleType.Scale)) return;
            _mode = mode;
            if (_handle != null)
            {
                TransformHandleManager.ChangeHandleType(_handle, mode);
                _manager.ChangeHandleSpace(_handle, _space);
                ApplySnapping();
            }
        }

        public void ToggleSpace()
        {
            if (!CanChange() || _mode == HandleType.Scale) return;
            _space = _space == Space.World ? Space.Self : Space.World;
            if (_handle != null) _manager.ChangeHandleSpace(_handle, _space);
            ApplySnapping();
        }

        public void ToggleSnapping()
        {
            if (!CanChange() || (_mode == HandleType.Position && _space == Space.Self)) return;
            _snap = !_snap;
            ApplySnapping();
        }

        public void ResetLayout()
        {
            if (!_initialized || !isActiveAndEnabled || _dragging) return;
            ClearSelection();
            foreach (var pair in _initial) if (pair.Key != null) Restore(pair.Key, pair.Value);
            _mode = HandleType.Position; _space = Space.World; _snap = false;
            _notice = "Scene reset. Handle size preference is kept.";
        }

        private bool CanChange() => _initialized && isActiveAndEnabled && !_dragging && _manager != null;

        private void RebuildHandle()
        {
            RemoveOwnedHandle();
            _selection.RemoveAll(t => t == null);
            if (_selection.Count == 0 || _manager == null || !isActiveAndEnabled) return;
            _handle = _manager.CreateHandleFromList(new List<Transform>(_selection));
            if (_handle == null) return;
            _handle.LocalSpaceReference = _selection.Count > 1 ? _selection[_selection.Count - 1] : null;
            _handle.UniformGroupScale = _selection.Count > 1;
            _handle.AutoScale = true;
            _handle.AutoScaleSizeInPixels = _handleSizePixels;
            _handle.OnInteractionStartEvent += OnDragStart;
            _handle.OnInteractionEvent += OnDrag;
            _handle.OnInteractionEndEvent += OnDragEnd;
            TransformHandleManager.ChangeHandleType(_handle, _mode);
            _manager.ChangeHandleSpace(_handle, _space);
            ApplySnapping();
        }

        private void ApplySnapping()
        {
            if (_handle == null) return;
            var localPosition = _handle.Type == HandleType.Position && _handle.Space == Space.Self;
            _handle.SnappingType = _handle.Type == HandleType.Position && !localPosition ? SnappingType.Absolute : SnappingType.Relative;
            _handle.PositionSnap = _snap && !localPosition ? Vector3.one * .5f : Vector3.zero;
            _handle.RotationSnap = _snap ? 15f : 0;
            _handle.ScaleSnap = _snap ? Vector3.one * .1f : Vector3.zero;
        }

        private void OnDragStart(Handle handle)
        {
            if (handle != _handle || _tearingDown) return;
            _lastStartFrame = Time.frameCount;
            CancelNavigation();
            if (_selection.Any(target => target == null || !ValidScale(target.localScale)))
            {
                ClearSelection();
                _notice = "A prop has an unsupported scale. Reset course to recover.";
                return;
            }
            _dragging = true;
            _dragStart.Clear();
            foreach (var target in _selection) if (target != null) _dragStart[target] = Capture(target);
        }

        private void OnDrag(Handle handle)
        {
            if (handle != _handle || _tearingDown || handle.Type != HandleType.Scale) return;
            var validFactor = true;
            if (_selection.Count > 1 && handle.Pivot != null)
            {
                var factor = handle.Pivot.localScale;
                validFactor = ValidPositive(factor.x) && ValidPositive(factor.y) && ValidPositive(factor.z) &&
                    Mathf.Approximately(factor.x, factor.y) && Mathf.Approximately(factor.x, factor.z);
            }
            if (validFactor && _selection.All(t => t != null && ValidScale(t.localScale))) return;
            // Remove first, because it synchronously ends Core interaction. Restore only after callbacks.
            RemoveOwnedHandle();
            foreach (var pair in _dragStart) if (pair.Key != null) Restore(pair.Key, pair.Value);
            _notice = "Scale must stay between 0.25 and 4. This drag was cancelled.";
            _rebuildPending = true;
        }

        private static bool ValidPositive(float value) => value > 0 && !float.IsInfinity(value) && !float.IsNaN(value);

        private void OnDragEnd(Handle handle)
        {
            if (_tearingDown || handle != _handle) return;
            OnDrag(handle);
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
                    handle.OnInteractionEvent -= OnDrag;
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
            TH_LevelEditorReleaseScroll();
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
            TH_LevelEditorReleaseScroll();
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
        private static bool ValidScale(Vector3 s) => Valid(s.x) && Valid(s.y) && Valid(s.z);
        private static bool Valid(float v) => !float.IsNaN(v) && !float.IsInfinity(v) && v >= .25f && v <= 4f;

        private bool PointerOverUi()
        {
            var system = EventSystem.current;
            if (system == null) return false;
            if (system.currentInputModule == null) return true;
            // Query the current position instead of EventSystem's potentially previous-frame hover cache.
            _uiHits.Clear();
            system.RaycastAll(new PointerEventData(system) { position = InputWrapper.PointerPosition }, _uiHits);
            return _uiHits.Any(hit => hit.module is UnityEngine.UI.GraphicRaycaster || hit.module is UnityEngine.UIElements.PanelRaycaster);
        }

        private void SetMaterial(Renderer renderer, Color color, bool grid)
        {
            _originalMaterials[renderer] = renderer.sharedMaterial;
            var material = new Material(targetMaterial) { color = color, name = "Level Editor " + renderer.name };
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
            TH_LevelEditorResetScroll();
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
            return TH_LevelEditorReadScroll();
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
                _ownedEventSystem = new GameObject("Prop Workshop Event System", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && TH_INPUTSYSTEM
                _ownedEventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
#else
                _ownedEventSystem.AddComponent<StandaloneInputModule>();
#endif
            }
            _uiObject = new GameObject("Prop Workshop Controls");
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
            _snapLabel = _uiRoot.Q<Label>("snap-description");
            _inspectorStatus = _uiRoot.Q<Label>("inspector-status");
            _snapLabel.enableRichText = _inspectorStatus.enableRichText = false;
            _scaleButton = _uiRoot.Q<Button>("scale");
            _spaceButton = _uiRoot.Q<Button>("space");
            _snapButton = _uiRoot.Q<Button>("snap");
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
            _scaleButton.clicked += Scale;
            _spaceButton.clicked += ToggleSpace;
            _snapButton.clicked += ToggleSnapping;
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
            _scaleButton.clicked -= Scale;
            _spaceButton.clicked -= ToggleSpace;
            _snapButton.clicked -= ToggleSnapping;
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
        private void Scale() => SetMode(HandleType.Scale);
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
            if (!InputWrapper.GetKey(KeyCode.W) && !InputWrapper.GetKey(KeyCode.E) && !InputWrapper.GetKey(KeyCode.R) && !InputWrapper.GetKey(KeyCode.X)) return;
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
            var group = _selection.Count > 1;
            var type = _handle != null ? _handle.Type : _mode;
            var space = _handle != null ? _handle.Space : _space;
            var selectionName = _selection.Count == 1 && _selection[0] != null ? _selection[0].name : "";
            var state = $"{_selection.Count}|{selectionName}|{type}|{space}|{_snap}|{_dragging}|{_handle != null}|{_notice}|{_helpPanelOpen}";
            if (_uiState == state) return;
            _uiState = state;
            _selectionLabel.text = _selection.Count == 0 ? "Select a prop" : group ? _selection.Count + " props selected" : selectionName.Replace('\n', ' ').Replace('\r', ' ');
            _selectionLabel.tooltip = selectionName;
            _selectionHint.text = _selection.Count == 0 ? "Click a prop. Shift-click adds to selection." : group ? "Transform the selection together." : "Move, rotate or scale this prop.";
            var canTransform = !_dragging && _handle != null;
            _moveButton.SetEnabled(canTransform); _rotateButton.SetEnabled(canTransform); _scaleButton.SetEnabled(canTransform);
            _spaceButton.SetEnabled(canTransform && type != HandleType.Scale);
            var localPosition = type == HandleType.Position && space == Space.Self;
            _snapButton.SetEnabled(canTransform && !localPosition);
            _resetButton.SetEnabled(!_dragging); _helpToggleButton.SetEnabled(!_dragging);
            _sizeInput.SetEnabled(!_dragging); _sizeSlider.SetEnabled(!_dragging);
            var snapActive = _snap && !localPosition;
            _spaceButton.text = group && type == HandleType.Scale ? "Uniform" : space == Space.World ? "World" : "Local";
            _snapButton.text = snapActive ? (type == HandleType.Position ? "Snap 0.5" : type == HandleType.Rotation ? "Snap 15°" : "Snap 0.1") : "Snap off";
            _moveButton.EnableInClassList("selected", _handle != null && type == HandleType.Position);
            _rotateButton.EnableInClassList("selected", _handle != null && type == HandleType.Rotation);
            _scaleButton.EnableInClassList("selected", _handle != null && type == HandleType.Scale);
            _snapButton.EnableInClassList("selected", snapActive);
            _helpToggleButton.EnableInClassList("selected", _helpPanelOpen);
            _snapLabel.text = localPosition ? "Position grid is available in World space." :
                type == HandleType.Position ? (group ? "0.5-unit pivot grid; prop offsets stay intact." : "0.5-unit World grid") :
                type == HandleType.Rotation ? "15° steps from drag start." : "0.1 scale steps; range 0.25 to 4.";
            _snapLabel.style.display = localPosition || snapActive ? DisplayStyle.Flex : DisplayStyle.None;
            if (group && type != HandleType.Scale && space == Space.Self)
            {
                var referenceName = _selection[_selection.Count - 1].name.Replace('\n', ' ').Replace('\r', ' ');
                if (referenceName.Length > 24) referenceName = referenceName.Substring(0, 24) + "…";
                _snapLabel.text = (snapActive ? _snapLabel.text + " · " : "") + "Local axes: " + referenceName;
                _snapLabel.style.display = DisplayStyle.Flex;
            }
            else if (group && type == HandleType.Scale)
            {
                _snapLabel.text = snapActive ? "Center scale · 0.1 factor steps" : "Drag the center to scale sizes and spacing together.";
                _snapLabel.style.display = DisplayStyle.Flex;
            }
            _status.text = _inspectorStatus.text = _notice;
        }
    }
}
