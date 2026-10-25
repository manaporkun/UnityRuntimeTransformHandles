using System;
using System.Collections.Generic;
using System.Linq;
using TransformHandles;
using TransformHandles.Utils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
        [SerializeField] private Sprite panelSprite;
        [SerializeField] private Sprite moveIcon, rotateIcon;

        private readonly List<Transform> _selection = new List<Transform>();
        private readonly Dictionary<Transform, PosRotScale> _initial = new Dictionary<Transform, PosRotScale>();
        private readonly List<Material> _materials = new List<Material>();
        private readonly Dictionary<Renderer, Material> _originalMaterials = new Dictionary<Renderer, Material>();
        private readonly List<Button> _buttons = new List<Button>();
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
        private Slider _sizeSlider;
        private InputField _sizeInput;
        private int _lastStartFrame = -1, _pressFrame = -1;
        private bool _pressBlocked, _releasePending, _pressCancelled;
        private Vector3 _pressPosition, _releasePosition;
        private bool _releaseBlocked;
        private GameObject _canvas, _ownedEventSystem;
        private Text _status, _selectionLabel, _modeLabel, _constraintLabel;
        private Button _moveButton, _rotateButton, _resetButton;
        private Font _font;
        private RectTransform _toolbar, _toolRow, _selectionCard, _contextBar;
        private Button _sizeToggleButton, _sizeCloseButton, _sizeBackdrop;
        private Text _projectLabel;
        private RectTransform _helpCard, _contextSeparator;
        private Button _helpToggleButton, _helpCloseButton, _helpBackdrop;
        private bool _helpPanelOpen, _closeHelpPanelPending;
        private bool _sizePanelOpen, _closeSizePanelPending;
        private bool _narrowLayout;
        private string _uiState;
        private static readonly Color PrimaryText = new Color(.15f, .16f, .18f);
        private static readonly Color MutedText = new Color(.39f, .41f, .45f);
        private static readonly Color Accent = new Color(.08f, .40f, .82f);
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
#if UNITY_2022_2_OR_NEWER
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
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
            _canvas.SetActive(true);
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
                BeginSelectionPress(Time.frameCount, mouse, PointerOverUi() || editingSize);
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
            _sizePanelOpen = _closeSizePanelPending = false;
            _helpPanelOpen = _closeHelpPanelPending = false;
            _helpCard.gameObject.SetActive(false);
            _helpBackdrop.gameObject.SetActive(false);
            _selectionCard.gameObject.SetActive(false);
            _sizeBackdrop.gameObject.SetActive(false);
            if (_canvas != null) _canvas.SetActive(false);
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
            if (_canvas != null) Destroy(_canvas);
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
            // Query the current position instead of EventSystem's potentially previous-frame hover cache.
            _uiHits.Clear();
            system.RaycastAll(new PointerEventData(system) { position = InputWrapper.PointerPosition }, _uiHits);
            return _uiHits.Any(hit => hit.module is GraphicRaycaster);
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
            var canvas = _canvas != null ? _canvas.GetComponent<Canvas>() : null;
            return Vector3.Distance(a, b) / (canvas != null ? Mathf.Max(.001f, canvas.scaleFactor) : 1f);
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
            _canvas = new GameObject("Modular Building Controls", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = _canvas.GetComponent<CanvasScaler>();
            // Scale the complete interface, including type and hit targets, with the viewport.
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = .5f;
            _toolbar = Panel("Panel", new Vector2(0, 1), Vector2.zero, new Vector2(0, 56));
            _toolbar.anchorMax = Vector2.one;
            _toolbar.GetComponent<Image>().sprite = null;
            var border = UiImage(_toolbar, "Bottom separator", new Color(.89f, .90f, .92f), Vector2.zero, Vector2.zero);
            border.rectTransform.anchorMin = Vector2.zero;
            border.rectTransform.anchorMax = new Vector2(1, 0);
            border.rectTransform.pivot = new Vector2(.5f, 0);
            border.rectTransform.sizeDelta = new Vector2(0, 1);
            Label(_toolbar, "Modular workshop", 24, 14, PrimaryText, 22);
            _projectLabel = Label(_toolbar, "Wall study", 250, 17, MutedText, 18);
            _resetButton = AddButton(_toolbar, "Reset scene", 0, 0, 136, ResetLayout);
            AnchorHeaderButton(_resetButton, 16);

            _toolRow = Panel("Transform tools", new Vector2(0, 1), new Vector2(22, -102), new Vector2(168, 128));
            AddPanelOutline(_toolRow);
            _moveButton = AddButton(_toolRow, "Move", 6, 8, 156, () => SetMode(HandleType.Position), moveIcon);
            _rotateButton = AddButton(_toolRow, "Rotate", 6, 64, 156, () => SetMode(HandleType.Rotation), rotateIcon);

            _contextBar = Panel("Selection context", new Vector2(.5f, 0), new Vector2(0, 24), new Vector2(600, 72));
            AddPanelOutline(_contextBar);
            _selectionLabel = Label(_contextBar, "No selection", 18, 10, PrimaryText, 20, 52);
            _selectionLabel.alignment = TextAnchor.MiddleLeft;
            _selectionLabel.supportRichText = false;
            _selectionLabel.rectTransform.sizeDelta = new Vector2(210, 52);
            _contextSeparator = UiImage(_contextBar, "Selection separator", new Color(.90f, .91f, .93f), new Vector2(236, -14), new Vector2(1, 44)).rectTransform;
            _modeLabel = Label(_contextBar, "Move", 250, 20, MutedText, 20, 32);
            _modeLabel.rectTransform.sizeDelta = new Vector2(84, 32);
            _modeLabel.alignment = TextAnchor.MiddleLeft;
            _constraintLabel = Label(_contextBar, "1 unit grid · 90° turns", 340, 20, MutedText, 20, 32);
            _constraintLabel.rectTransform.sizeDelta = new Vector2(246, 32);

            BuildHelpUi();

            // This backdrop remains active until LateUpdate on dismissal, after Core input.
            var backdrop = Panel("Size panel backdrop", Vector2.zero, Vector2.zero, Vector2.zero);
            backdrop.anchorMax = Vector2.one;
            backdrop.GetComponent<Image>().color = Color.clear;
            _sizeBackdrop = backdrop.gameObject.AddComponent<Button>();
            _sizeBackdrop.transition = Selectable.Transition.None;
            _sizeBackdrop.onClick.AddListener(RequestCloseSizePanel);
            _sizeToggleButton = AddButton(_canvas.transform, "Handle size · 192 px", 0, 0, 244, ToggleSizePanel);
            _buttons.Remove(_sizeToggleButton);
            AnchorHeaderButton(_sizeToggleButton, 154);
            _selectionCard = Panel("Handle size", Vector2.one, new Vector2(-24, -80), new Vector2(300, 258));
            AddPanelOutline(_selectionCard);
            Label(_selectionCard, "Handle size", 22, 18, PrimaryText, 22);
            _sizeCloseButton = AddButton(_selectionCard, "×", 248, 0, 44, RequestCloseSizePanel);
            _buttons.Remove(_sizeCloseButton);
            BuildHandleSizeUi();
            var note = Label(_selectionCard, "Choose a comfortable size for your display.", 22, 200, MutedText, 18, 52);
            note.rectTransform.sizeDelta = new Vector2(256, 52);
            _selectionCard.gameObject.SetActive(false);
            _sizeBackdrop.gameObject.SetActive(false);
            UpdateUiLayout();
        }

        private static void AnchorHeaderButton(Button button, float right)
        {
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-right, 0);
        }

        private static void AddPanelOutline(RectTransform panel)
        {
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.84f, .86f, .89f, .6f);
            outline.effectDistance = new Vector2(1, -1);
        }

        private void BuildHelpUi()
        {
            var launcher = Panel("Help launcher", new Vector2(1, 0), new Vector2(-24, 24), new Vector2(112, 48));
            AddPanelOutline(launcher);
            _helpToggleButton = AddButton(launcher, "?  Help", 0, 0, 112, ToggleHelpPanel);
            _buttons.Remove(_helpToggleButton);
            ((RectTransform)_helpToggleButton.transform).sizeDelta = new Vector2(112, 48);
            _helpToggleButton.GetComponentInChildren<Text>().rectTransform.sizeDelta = new Vector2(112, 48);

            var backdrop = Panel("Help backdrop", Vector2.zero, Vector2.zero, Vector2.zero);
            backdrop.anchorMax = Vector2.one;
            backdrop.GetComponent<Image>().color = Color.clear;
            _helpBackdrop = backdrop.gameObject.AddComponent<Button>();
            _helpBackdrop.transition = Selectable.Transition.None;
            _helpBackdrop.onClick.AddListener(RequestCloseHelpPanel);
            _helpCard = Panel("Workshop help", new Vector2(1, 0), new Vector2(-24, 108), new Vector2(360, 344));
            AddPanelOutline(_helpCard);
            Label(_helpCard, "Workshop controls", 22, 18, PrimaryText, 22);
            _helpCloseButton = AddButton(_helpCard, "×", 310, 0, 44, RequestCloseHelpPanel);
            _buttons.Remove(_helpCloseButton);
            HelpRow("Move / Rotate", "W / E", 64);
            HelpRow("Orbit", "Left-drag", 96);
            HelpRow("Pan", "Right-drag", 128);
            HelpRow("Zoom", "Scroll", 160);
            UiImage(_helpCard, "Help separator", new Color(.90f, .91f, .93f), new Vector2(22, -198), new Vector2(316, 1));
            _status = Label(_helpCard, _notice, 22, 212, PrimaryText, 20, 48);
            _status.supportRichText = false;
            _status.rectTransform.sizeDelta = new Vector2(316, 48);
            var note = Label(_helpCard, "Modules can overlap. Reset restores the layout.", 22, 274, MutedText, 20, 48);
            note.rectTransform.sizeDelta = new Vector2(316, 48);
            _helpCard.gameObject.SetActive(false);
            _helpBackdrop.gameObject.SetActive(false);
        }

        private void HelpRow(string action, string gesture, float top)
        {
            var name = Label(_helpCard, action, 22, top, PrimaryText, 20, 28);
            name.rectTransform.sizeDelta = new Vector2(166, 28);
            var shortcut = Label(_helpCard, gesture, 194, top, MutedText, 20, 28);
            shortcut.rectTransform.sizeDelta = new Vector2(144, 28);
            shortcut.alignment = TextAnchor.UpperRight;
        }

        private void ToggleHelpPanel()
        {
            if (_dragging) return;
            // A dismissal click only closes the current popup; never open another in the same press.
            if (_sizePanelOpen) { RequestCloseSizePanel(); return; }
            if (_helpPanelOpen) { RequestCloseHelpPanel(); return; }
            CancelNavigation();
            _pressBlocked = _pressCancelled = true;
            _helpPanelOpen = true;
            _closeHelpPanelPending = false;
            _helpBackdrop.gameObject.SetActive(true);
            _helpCard.gameObject.SetActive(true);
            RefreshUi();
        }

        private void RequestCloseHelpPanel()
        {
            if (!_helpPanelOpen) return;
            _pressBlocked = _pressCancelled = true;
            _releaseBlocked = true;
            CancelNavigation();
            _closeHelpPanelPending = true;
        }

        private void ToggleSizePanel()
        {
            if (_dragging) return;
            if (_helpPanelOpen) { RequestCloseHelpPanel(); return; }
            if (_sizePanelOpen) { RequestCloseSizePanel(); return; }
            CancelNavigation();
            _pressBlocked = _pressCancelled = true;
            _sizePanelOpen = true;
            _closeSizePanelPending = false;
            _sizeBackdrop.gameObject.SetActive(true);
            _selectionCard.gameObject.SetActive(true);
            RefreshUi();
        }

        private void RequestCloseSizePanel()
        {
            if (!_sizePanelOpen) return;
            // Commit an outside/Close click, but let InputField handle Escape cancellation first.
            if (_sizeInput.isFocused) _sizeInput.DeactivateInputField();
            _pressBlocked = _pressCancelled = true;
            _releaseBlocked = true;
            CancelNavigation();
            _closeSizePanelPending = true;
        }

        private void ApplyDeferredPanelClose()
        {
            if (_closeSizePanelPending)
            {
                _closeSizePanelPending = _sizePanelOpen = false;
                _selectionCard.gameObject.SetActive(false);
                _sizeBackdrop.gameObject.SetActive(false);
            }
            if (_closeHelpPanelPending)
            {
                _closeHelpPanelPending = _helpPanelOpen = false;
                _helpCard.gameObject.SetActive(false);
                _helpBackdrop.gameObject.SetActive(false);
            }
        }

        private void HandleEscape(int frame)
        {
            if (SuppressShortcuts(frame)) return;
            if (_sizePanelOpen) RequestCloseSizePanel();
            else if (_helpPanelOpen) RequestCloseHelpPanel();
            else ClearSelection();
        }

        private RectTransform Panel(string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_canvas.transform, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.sprite = panelSprite; image.type = Image.Type.Sliced;
            image.color = new Color(1f, 1f, 1f, .98f);
            return rect;
        }

        private void UpdateUiLayout()
        {
            var narrow = ((RectTransform)_canvas.transform).rect.width < 1190;
            if (_uiState != null && narrow == _narrowLayout) return;
            _narrowLayout = narrow;
            _projectLabel.gameObject.SetActive(!narrow);
            _toolRow.anchoredPosition = new Vector2(narrow ? 16 : 22, -102);
            _selectionCard.anchoredPosition = new Vector2(narrow ? -16 : -24, -80);
        }

        private Image UiImage(Transform parent, string name, Color color, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color; image.sprite = panelSprite; image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            return image;
        }

        private void BuildHandleSizeUi()
        {
            var label = Label(_selectionCard, "Size", 22, 70, PrimaryText, 20, 44);
            label.alignment = TextAnchor.MiddleLeft;
            label.rectTransform.sizeDelta = new Vector2(150, 44);
            var fieldImage = UiImage(_selectionCard, "Handle size input", Color.white, new Vector2(180, -70), new Vector2(98, 44));
            fieldImage.raycastTarget = true;
            var border = fieldImage.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(.81f, .83f, .87f); border.effectDistance = new Vector2(1, -1);
            _sizeInput = fieldImage.gameObject.AddComponent<InputField>();
            _sizeInput.targetGraphic = fieldImage;
            _sizeInput.contentType = InputField.ContentType.IntegerNumber;
            _sizeInput.characterLimit = 10;
            var value = Label(fieldImage.transform, "192", 6, 0, PrimaryText, 20, 44);
            value.rectTransform.sizeDelta = new Vector2(60, 44); value.alignment = TextAnchor.MiddleRight;
            _sizeInput.textComponent = value;
            var unit = Label(fieldImage.transform, "px", 71, 0, MutedText, 16, 44);
            unit.rectTransform.sizeDelta = new Vector2(24, 44); unit.alignment = TextAnchor.MiddleLeft;
            _sizeInput.SetTextWithoutNotify(_handleSizePixels.ToString());
            _sizeInput.onEndEdit.AddListener(CommitHandleSize);

            var sliderImage = UiImage(_selectionCard, "Handle size slider", Color.clear, new Vector2(22, -126), new Vector2(256, 44));
            sliderImage.raycastTarget = true;
            _sizeSlider = sliderImage.gameObject.AddComponent<Slider>();
            _sizeSlider.minValue = MinimumHandlePixels; _sizeSlider.maxValue = MaximumHandlePixels;
            _sizeSlider.wholeNumbers = true;
            UiImage(sliderImage.transform, "Track", new Color(.86f, .87f, .90f), new Vector2(9, -20), new Vector2(238, 4));
            var fillArea = UiImage(sliderImage.transform, "Fill area", Color.clear, new Vector2(9, -20), new Vector2(238, 4));
            var fill = UiImage(fillArea.transform, "Fill", Accent, Vector2.zero, Vector2.zero);
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            var handleArea = UiImage(sliderImage.transform, "Handle area", Color.clear, new Vector2(9, -13), new Vector2(238, 18));
            var thumb = UiImage(handleArea.transform, "Thumb", Accent, Vector2.zero, new Vector2(18, 0));
            thumb.rectTransform.anchorMin = thumb.rectTransform.anchorMax = new Vector2(0, .5f);
            thumb.rectTransform.pivot = new Vector2(.5f, .5f);
            _sizeSlider.fillRect = fill.rectTransform; _sizeSlider.handleRect = thumb.rectTransform;
            _sizeSlider.targetGraphic = thumb;
            _sizeSlider.SetValueWithoutNotify(_handleSizePixels);
            _sizeSlider.onValueChanged.AddListener(SetHandleSize);
            Label(_selectionCard, "64", 22, 174, MutedText, 14, 20);
            var maximum = Label(_selectionCard, "512", 228, 174, MutedText, 14, 20);
            maximum.rectTransform.sizeDelta = new Vector2(50, 20); maximum.alignment = TextAnchor.UpperRight;
        }

        private bool SuppressShortcuts(int frame) =>
            (_sizeInput != null && _sizeInput.isFocused) || _lastSizeEditFrame == frame;

        private void CommitHandleSize(string text)
        {
            _lastSizeEditFrame = Time.frameCount;
            if (_sizeInput != null && !_sizeInput.wasCanceled && int.TryParse(text, out var value))
                SetHandleSize(value);
            if (_sizeInput != null) _sizeInput.SetTextWithoutNotify(_handleSizePixels.ToString());
        }

        private void SetHandleSize(float value)
        {
            if (!CanChange() || float.IsNaN(value) || float.IsInfinity(value))
            {
                _sizeSlider.SetValueWithoutNotify(_handleSizePixels);
                return;
            }
            _handleSizePixels = Mathf.RoundToInt(Mathf.Clamp(value, MinimumHandlePixels, MaximumHandlePixels));
            if (_handle != null)
            {
                _handle.AutoScale = true;
                _handle.AutoScaleSizeInPixels = _handleSizePixels;
            }
            _sizeSlider.SetValueWithoutNotify(_handleSizePixels);
            if (!_sizeInput.isFocused) _sizeInput.SetTextWithoutNotify(_handleSizePixels.ToString());
        }

        private Text Label(Transform parent, string text, float x, float y, Color color, int size, float height = 32)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>(); label.text = text; label.font = _font;
            label.fontSize = size; label.color = color; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            var rect = label.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(252, height);
            return label;
        }

        private Button AddButton(Transform parent, string text, float x, float y, float width, UnityEngine.Events.UnityAction action, Sprite icon = null)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, 56);
            var image = go.GetComponent<Image>(); image.sprite = panelSprite; image.type = Image.Type.Sliced;
            image.color = Color.white;
            var button = go.GetComponent<Button>(); button.onClick.AddListener(action); _buttons.Add(button);
            var label = Label(go.transform, text, 0, 0, PrimaryText, 20, 56);
            label.alignment = TextAnchor.MiddleCenter; label.rectTransform.sizeDelta = new Vector2(width, 56);
            if (icon != null)
            {
                var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(go.transform, false);
                var iconImage = iconObject.GetComponent<Image>(); iconImage.sprite = icon; iconImage.raycastTarget = false;
                var iconRect = iconImage.rectTransform; iconRect.anchorMin = iconRect.anchorMax = new Vector2(0, .5f);
                iconRect.anchoredPosition = new Vector2(24, 0); iconRect.sizeDelta = new Vector2(22, 22);
                label.rectTransform.anchoredPosition = new Vector2(42, 0); label.rectTransform.sizeDelta = new Vector2(width - 48, 56);
            }
            StyleButton(button, false);
            return button;
        }

        private void SetSelectionLabel(string name)
        {
            name = name.Replace('\n', ' ').Replace('\r', ' ');
            var length = Mathf.Min(name.Length, 64);
            _selectionLabel.text = length < name.Length ? name.Substring(0, length) + "…" : name;
            while (length > 0 && _selectionLabel.preferredHeight > _selectionLabel.rectTransform.rect.height + .1f)
            {
                length--;
                _selectionLabel.text = name.Substring(0, length) + "…";
            }
        }

        private void RefreshUi()
        {
            if (_canvas == null) return;
            UpdateUiLayout();
            _sizeSlider.interactable = !_dragging;
            _sizeInput.interactable = !_dragging;
            var selectionName = _selection.Count == 1 && _selection[0] != null ? _selection[0].name : "";
            var state = $"{selectionName}|{_mode}|{_dragging}|{_handle != null}|{_notice}|{_handleSizePixels}|{_sizePanelOpen}|{_helpPanelOpen}";
            if (_uiState == state) return;
            _uiState = state;
            var selected = _selection.Count > 0;
            _contextBar.sizeDelta = selected ? new Vector2(600, 72) : new Vector2(340, 56);
            _selectionLabel.rectTransform.sizeDelta = selected ? new Vector2(210, 52) : new Vector2(304, 36);
            _selectionLabel.alignment = selected ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            SetSelectionLabel(selected ? selectionName : "Select a wall or corner");
            _modeLabel.gameObject.SetActive(selected);
            _constraintLabel.gameObject.SetActive(selected);
            _contextSeparator.gameObject.SetActive(selected);
            _modeLabel.text = _mode == HandleType.Position ? "Move" : "Rotate";
            foreach (var button in _buttons) button.interactable = !_dragging && _handle != null;
            _resetButton.interactable = !_dragging;
            _sizeToggleButton.interactable = !_dragging;
            _sizeCloseButton.interactable = true;
            _helpToggleButton.interactable = !_dragging;
            _helpCloseButton.interactable = true;
            _sizeToggleButton.GetComponentInChildren<Text>().text = "Handle size · " + _handleSizePixels + " px";
            StyleButton(_moveButton, _mode == HandleType.Position);
            StyleButton(_rotateButton, _mode == HandleType.Rotation);
            StyleButton(_resetButton, false);
            StyleButton(_sizeToggleButton, _sizePanelOpen);
            StyleButton(_sizeCloseButton, false);
            StyleButton(_helpToggleButton, _helpPanelOpen);
            StyleButton(_helpCloseButton, false);
            _status.text = _notice;
        }
        private static void StyleButton(Button button, bool active)
        {
            var colors = button.colors;
            colors.normalColor = active ? new Color(.92f, .95f, .99f) : new Color(1, 1, 1, 0);
            colors.highlightedColor = new Color(.90f, .93f, .98f);
            colors.pressedColor = new Color(.82f, .88f, .97f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(1, 1, 1, 0);
            colors.fadeDuration = .08f;
            button.colors = colors;
            var color = button.interactable ? (active ? Accent : MutedText) : new Color(.64f, .65f, .68f);
            button.GetComponentInChildren<Text>().color = color;
            var icon = button.transform.Find("Icon");
            if (icon != null) icon.GetComponent<Image>().color = color;
        }

    }
}
