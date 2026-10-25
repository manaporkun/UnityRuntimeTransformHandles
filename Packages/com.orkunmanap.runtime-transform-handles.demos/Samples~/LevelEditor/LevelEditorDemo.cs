using System;
using System.Collections.Generic;
using System.Linq;
using TransformHandles;
using TransformHandles.Utils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
        [SerializeField] private Sprite panelSprite;
        [SerializeField] private Sprite moveIcon, rotateIcon, scaleIcon;

        private readonly List<Transform> _selection = new List<Transform>();
        private readonly Dictionary<Transform, PosRotScale> _initial = new Dictionary<Transform, PosRotScale>();
        private readonly Dictionary<Transform, PosRotScale> _dragStart = new Dictionary<Transform, PosRotScale>();
        private readonly List<Material> _materials = new List<Material>();
        private readonly Dictionary<Renderer, Material> _originalMaterials = new Dictionary<Renderer, Material>();
        private readonly List<Button> _buttons = new List<Button>();
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
        private Slider _sizeSlider;
        private InputField _sizeInput;
        private int _lastStartFrame = -1, _pressFrame = -1;
        private bool _pressBlocked, _pressAdditive, _releasePending, _pressCancelled;
        private Vector3 _pressPosition, _releasePosition;
        private bool _releaseBlocked;
        private GameObject _canvas, _ownedEventSystem;
        private Text _status, _selectionLabel, _modeLabel, _snapLabel;
        private Button _moveButton, _rotateButton, _scaleButton, _spaceButton, _snapButton, _resetButton;
        private Font _font;
        private RectTransform _toolbar, _toolRow, _selectionCard, _contextBar;
        private Button _sizeToggleButton, _sizeCloseButton, _sizeBackdrop;
        private Text _projectLabel;
        private bool _sizePanelOpen, _closeSizePanelPending;
        private bool _narrowLayout;
        private string _uiState;
        private static readonly Color PrimaryText = new Color(.15f, .16f, .18f);
        private static readonly Color MutedText = new Color(.39f, .41f, .45f);
        private static readonly Color Accent = new Color(.08f, .40f, .82f);
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
            if (sceneCamera == null || targetMaterial == null || interactionSettings == null ||
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
            _canvas.SetActive(true);
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
                BeginSelectionPress(Time.frameCount, mouse, PointerOverUi() || editingSize,
                    InputWrapper.GetKey(KeyCode.LeftShift) || InputWrapper.GetKey(KeyCode.RightShift));
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
            _sizePanelOpen = _closeSizePanelPending = false;
            // Scene unloading may destroy the separately owned canvas before this callback.
            if (_selectionCard != null) _selectionCard.gameObject.SetActive(false);
            if (_sizeBackdrop != null) _sizeBackdrop.gameObject.SetActive(false);
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
            TH_LevelEditorReleaseScroll();
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
        private static bool ValidScale(Vector3 s) => Valid(s.x) && Valid(s.y) && Valid(s.z);
        private static bool Valid(float v) => !float.IsNaN(v) && !float.IsInfinity(v) && v >= .25f && v <= 4f;

        private bool PointerOverUi()
        {
            var system = EventSystem.current;
            if (system == null) return false;
            // Query the current position instead of EventSystem's potentially previous-frame hover cache.
            _uiHits.Clear();
            system.RaycastAll(new PointerEventData(system) { position = InputWrapper.PointerPosition }, _uiHits);
            return _uiHits.Any(hit => hit.module is GraphicRaycaster || hit.module is UnityEngine.UIElements.PanelRaycaster);
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
                _ownedEventSystem = new GameObject("Level Editor Event System", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && TH_INPUTSYSTEM
                _ownedEventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
#else
                _ownedEventSystem.AddComponent<StandaloneInputModule>();
#endif
            }
            _canvas = new GameObject("Level Editor Controls", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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
            Label(_toolbar, "Scene editor", 24, 14, PrimaryText, 22);
            _projectLabel = Label(_toolbar, "Untitled scene", 184, 17, MutedText, 18);
            _resetButton = AddButton(_toolbar, "Reset scene", 0, 0, 136, ResetLayout);
            AnchorHeaderButton(_resetButton, 16);

            _toolRow = Panel("Transform tools", new Vector2(0, 1), new Vector2(22, -102), new Vector2(168, 184));
            AddPanelOutline(_toolRow);
            _moveButton = AddButton(_toolRow, "Move", 6, 8, 156, () => SetMode(HandleType.Position), moveIcon);
            _rotateButton = AddButton(_toolRow, "Rotate", 6, 64, 156, () => SetMode(HandleType.Rotation), rotateIcon);
            _scaleButton = AddButton(_toolRow, "Scale", 6, 120, 156, () => SetMode(HandleType.Scale), scaleIcon);

            _contextBar = Panel("Selection context", new Vector2(.5f, 0), new Vector2(0, 140), new Vector2(600, 72));
            AddPanelOutline(_contextBar);
            _selectionLabel = Label(_contextBar, "No selection", 18, 10, PrimaryText, 20, 52);
            _selectionLabel.alignment = TextAnchor.MiddleLeft;
            _selectionLabel.supportRichText = false;
            _selectionLabel.rectTransform.sizeDelta = new Vector2(210, 52);
            UiImage(_contextBar, "Selection separator", new Color(.90f, .91f, .93f), new Vector2(236, -14), new Vector2(1, 44));
            _modeLabel = Label(_contextBar, "Move", 250, 20, MutedText, 20, 32);
            _modeLabel.rectTransform.sizeDelta = new Vector2(84, 32);
            _modeLabel.alignment = TextAnchor.MiddleLeft;
            _spaceButton = AddButton(_contextBar, "World", 334, 8, 110, ToggleSpace);
            _snapButton = AddButton(_contextBar, "Snap off", 444, 8, 140, ToggleSnapping);

            _status = FooterLabel(_notice, 80, 56, 20);
            _snapLabel = FooterLabel("", 46, 30, 20);
            var help = FooterLabel("Left-drag   Orbit     Right-drag   Pan     Scroll   Zoom     Shift-click   Add to selection", 10, 32, 18);
            help.name = "Shortcuts";

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

        private Text FooterLabel(string text, float bottom, float height, int size)
        {
            var label = Label(_canvas.transform, text, 0, 0, MutedText, size, height);
            var rect = label.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(.5f, 0);
            rect.anchoredPosition = new Vector2(0, bottom);
            rect.sizeDelta = new Vector2(-64, height);
            label.alignment = TextAnchor.MiddleCenter;
            return label;
        }

        private void ToggleSizePanel()
        {
            if (_dragging) return;
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
            if (!_closeSizePanelPending) return;
            _closeSizePanelPending = _sizePanelOpen = false;
            _selectionCard.gameObject.SetActive(false);
            _sizeBackdrop.gameObject.SetActive(false);
        }

        private void HandleEscape(int frame)
        {
            if (SuppressShortcuts(frame)) return;
            if (_sizePanelOpen) RequestCloseSizePanel();
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
            var group = _selection.Count > 1;
            var type = _handle != null ? _handle.Type : _mode;
            var space = _handle != null ? _handle.Space : _space;
            var selectionName = _selection.Count == 1 ? _selection[0].name : "";
            var state = $"{_selection.Count}|{selectionName}|{type}|{space}|{_snap}|{_dragging}|{_handle != null}|{_notice}|{_handleSizePixels}|{_sizePanelOpen}";
            if (_uiState == state) return;
            _uiState = state;
            SetSelectionLabel(_selection.Count == 0 ? "No selection" : group ? _selection.Count + " props selected" : selectionName);
            _contextBar.gameObject.SetActive(_selection.Count > 0);
            _modeLabel.text = (type == HandleType.Position ? "Move" : type == HandleType.Rotation ? "Rotate" : "Scale");
            foreach (var button in _buttons) button.interactable = !_dragging && _handle != null;
            _spaceButton.interactable &= type != HandleType.Scale;
            var localPosition = type == HandleType.Position && space == Space.Self;
            _snapButton.interactable &= !localPosition;
            _resetButton.interactable = !_dragging;
            _sizeToggleButton.interactable = !_dragging;
            _sizeCloseButton.interactable = true;
            _sizeToggleButton.GetComponentInChildren<Text>().text = "Handle size · " + _handleSizePixels + " px";
            var snapActive = _snap && !localPosition;
            _spaceButton.GetComponentInChildren<Text>().text = group && type == HandleType.Scale ? "Uniform" : space == Space.World ? "World" : "Local";
            _snapButton.GetComponentInChildren<Text>().text = snapActive ?
                (type == HandleType.Position ? "Snap 0.5" : type == HandleType.Rotation ? "Snap 15 deg" : "Snap 0.1") : "Snap off";
            StyleButton(_moveButton, type == HandleType.Position);
            StyleButton(_rotateButton, type == HandleType.Rotation);
            StyleButton(_scaleButton, type == HandleType.Scale);
            StyleButton(_snapButton, snapActive);
            StyleButton(_spaceButton, false);
            StyleButton(_resetButton, false);
            StyleButton(_sizeToggleButton, _sizePanelOpen);
            StyleButton(_sizeCloseButton, false);
            _snapLabel.text = localPosition ? "Position grid is available in World space." :
                type == HandleType.Position ? (group ? "0.5-unit pivot grid; prop offsets stay intact." : "0.5-unit World grid") :
                type == HandleType.Rotation ? "15-degree steps from drag start" : (group ? "0.1 factor steps; object scales stay within 0.25 to 4" : "0.1 scale drag steps; range 0.25 to 4");
            _snapLabel.gameObject.SetActive(localPosition || snapActive);
            _snapLabel.supportRichText = false;
            _status.text = _notice;
            if (group && type != HandleType.Scale && space == Space.Self)
            {
                var referenceName = _selection[_selection.Count - 1].name.Replace('\n', ' ').Replace('\r', ' ');
                if (referenceName.Length > 24) referenceName = referenceName.Substring(0, 24) + "…";
                _snapLabel.text = (snapActive ? _snapLabel.text + " · " : "") + "Local axes: " + referenceName;
                _snapLabel.gameObject.SetActive(true);
            }
            else if (group && type == HandleType.Scale)
            {
                _snapLabel.text = snapActive ? "Center scale · 0.1 factor steps" : "Drag the center to scale sizes and spacing together.";
                _snapLabel.gameObject.SetActive(true);
            }
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
