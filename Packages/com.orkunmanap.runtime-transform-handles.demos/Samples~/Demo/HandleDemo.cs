using System.Collections.Generic;
using System.Linq;
using TransformHandles;
using TransformHandles.Utils;
using UnityEngine;
#if TH_UGUI
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using InputWrapper = TransformHandles.Utils.InputWrapper;
#endif

/// <summary>
/// Self-contained showcase that exercises the whole public surface of the package:
/// every <see cref="HandleType"/>, <see cref="HandleAxes"/> mask, <see cref="Space"/>,
/// snapping mode, scale/auto-scale, multi-target grouping, a runtime
/// <see cref="TransformHandleSettings"/>, and all interaction events.
///
/// Drop this on an empty GameObject and press Play — it spawns its own targets, ensures a
/// camera, and draws the sample UI Toolkit layout. Assign its local layout and panel settings. The handle gizmos
/// still need the "TransformHandle" physics layer (Tools > Transform Handles > Setup Layer).
/// </summary>
[DefaultExecutionOrder(-100)]
public class HandleDemo : MonoBehaviour
{
    [Header("Spawned targets")]
    [SerializeField] private int targetCount = 6;
    [SerializeField] private float spacing = 2.5f;
    [SerializeField] private VisualTreeAsset uiLayout;
    [SerializeField] private PanelSettings uiPanelSettings;

    private static readonly Color[] Palette =
    {
        new Color(0.90f, 0.30f, 0.30f), new Color(0.30f, 0.80f, 0.40f),
        new Color(0.30f, 0.55f, 0.95f), new Color(0.95f, 0.80f, 0.25f),
        new Color(0.75f, 0.40f, 0.90f), new Color(0.35f, 0.85f, 0.85f),
    };

    private Camera _camera;
    private TransformHandleManager _manager;
    private TransformHandleSettings _settings;
    private TransformHandleManager _ownedManager;
    private Camera _previousCamera;
    private TransformHandleSettings _previousSettings;
    private bool _previousUiBlock, _navigationPress;
    private readonly List<Material> _ownedMaterials = new List<Material>();

    private readonly List<Transform> _targets = new List<Transform>();
    private readonly Dictionary<Transform, Handle> _targetToHandle = new Dictionary<Transform, Handle>();
    private Handle _activeHandle;
    private bool _interacting;

    // Mirrors of the active handle's state so the HUD can drive ChangeAxes/ChangeHandleType/etc.
    private HandleType _type = HandleType.Position;
    private Space _space = Space.Self;
    private HandleAxes _axes = HandleAxes.XYZ;
    private SnappingType _snapType = SnappingType.Relative;
    private float _posSnap;
    private float _rotSnap;
    private float _scaleSnap;
    private float _scaleMultiplier = 1f;
    private bool _autoScale = true;
    private bool _useSettings;

    private readonly List<string> _log = new List<string>();
    private float _camYaw, _camPitch = 15f, _camDist = 12f;
    private readonly Dictionary<Transform, PosRotScale> _initial = new Dictionary<Transform, PosRotScale>();
    private GameObject _uiObject, _ownedEventSystem;
    private UIDocument _document;
    private PanelSettings _runtimePanelSettings;
    private VisualElement _uiRoot, _workshop, _helpBackdrop, _testRegion;
    private Label _selectionLabel, _selectionHint, _status, _eventLog;
    private Button _moveButton, _rotateButton, _scaleButton, _spaceButton, _resetButton, _helpToggleButton, _helpCloseButton, _groupButton, _clearButton;
    private SliderInt _sizeSlider;
    private TextField _sizeInput;
    private Slider _positionSlider, _rotationSlider, _scaleSlider, _multiplierSlider;
    private Toggle _autoToggle, _settingsToggle, _showTestToggle, _blockTestToggle;
    private DropdownField _typeField, _axesField, _snapField;
    private int _handleSizePixels = 192, _lastSizeEditFrame = -1, _releaseFrame = -1;
    private bool _initialized, _sizeEditing, _helpPanelOpen, _closeHelpPanelPending, _blockTest = true, _showTest, _suspendedManager;

    // Raycast mask that excludes the handle-gizmo layer so picking ignores gizmo colliders.
    private int _pickMask = ~0;

    private void Awake()
    {
        _camera = Camera.main;
        if (_camera == null)
        {
            var go = new GameObject("Demo Camera") { tag = "MainCamera" };
            _camera = go.AddComponent<Camera>();
        }

        // Build a runtime settings asset (no .asset file needed) to demo Settings injection.
        _settings = TransformHandleSettings.CreateDefault();

        var handleLayer = LayerMask.NameToLayer("TransformHandle");
        _pickMask = handleLayer >= 0 ? ~(1 << handleLayer) : ~0;
        if (uiLayout == null || uiPanelSettings == null)
        {
            Debug.LogError("Handle Showcase requires its local UI Toolkit layout and panel settings.", this);
            enabled = false;
            return;
        }
        BuildUi();
        _initialized = true;
    }

    private void Start()
    {
        // Keep the showcase usable beside either Built-in or URP workspaces. A local
        // referenced shader also survives player stripping without pipeline dependencies.
        var targetMaterial = Resources.Load<Material>("HandleDemoTarget");
        for (var i = 0; i < targetCount; i++)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"Target_{i}";
            cube.AddComponent<DemoTarget>();
            cube.transform.position = new Vector3((i - (targetCount - 1) * 0.5f) * spacing, 0.5f, 0f);
            var renderer = cube.GetComponent<Renderer>();
            var material = targetMaterial != null ? new Material(targetMaterial) : renderer.material;
            renderer.sharedMaterial = material;
            _ownedMaterials.Add(material);
            material.color = Palette[i % Palette.Length];
            _targets.Add(cube.transform);
            _initial[cube.transform] = new PosRotScale { Position = cube.transform.position, Rotation = cube.transform.rotation, Scale = cube.transform.localScale };
        }

        // FindAnyObjectByType lands in 2021.3.18 / 2022.2; the package's declared minimum is
        // 2021.3.0f1, so fall back to FindObjectOfType to keep the sample compiling there.
#if UNITY_2023_1_OR_NEWER
        if (!Object.FindAnyObjectByType<Light>())
#else
        if (!Object.FindObjectOfType<Light>())
#endif
        {
            var lightGo = new GameObject("Demo Light");
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        UpdateCamera();
    }

    private void OnEnable()
    {
        if (!_initialized) return;
        bool ownsManager = !Resources.FindObjectsOfTypeAll<TransformHandleManager>().Any(m => m.gameObject.scene.IsValid() && m != _ownedManager);
        _manager = TransformHandleManager.Instance;
        if (ownsManager) _ownedManager = _manager;
        _previousCamera = _manager.MainCamera;
        _previousSettings = _manager.Settings;
        _previousUiBlock = _manager.BlockWhenPointerOverUI;
        _manager.MainCamera = _camera;
        _manager.BlockWhenPointerOverUI = true;
        _uiObject.SetActive(true);
        BindUi();
        if (_ownedEventSystem != null) _ownedEventSystem.SetActive(true);
        RefreshUi();
    }

    private void OnDisable()
    {
        if (!_initialized) return;
        ReleaseInput();
        ClearOwnedHandles();
        UnbindUi();
        _helpPanelOpen = _closeHelpPanelPending = false;
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

    private Vector2 PanelPointer()
    {
        var pointer = InputWrapper.PointerPosition;
        return RuntimePanelUtils.ScreenToPanel(_uiRoot.panel, new Vector2(pointer.x, Screen.height - pointer.y));
    }

    private bool OverTestRegion() => _uiRoot != null && _uiRoot.panel != null && _showTest && _testRegion.worldBound.Contains(PanelPointer()) && !_helpPanelOpen;

    private bool PointerOverUi()
    {
        if (_uiRoot == null || _uiRoot.panel == null) return false;
        var hit = _uiRoot.panel.Pick(PanelPointer());
        if (hit == null || hit == _uiRoot || hit == _workshop) return false;
        return !OverTestRegion() || _blockTest;
    }

    private void Update()
    {
        if (!_initialized || _manager == null || _uiRoot == null) return;
        bool overUi = PointerOverUi();
        bool down = InputWrapper.GetMouseButtonDown(0) || InputWrapper.GetMouseButtonDown(1) || InputWrapper.GetMouseButtonDown(2);
        bool held = InputWrapper.GetMouseButton(0) || InputWrapper.GetMouseButton(1) || InputWrapper.GetMouseButton(2);
        if (overUi && down) { _navigationPress = true; _releaseFrame = -1; }
        if (_navigationPress && !held)
        {
            if (_releaseFrame < 0) _releaseFrame = Time.frameCount;
            else if (Time.frameCount > _releaseFrame) _navigationPress = false;
        }
        var editing = IsEditingText() || _lastSizeEditFrame == Time.frameCount;
        bool suppress = _navigationPress || editing || _helpPanelOpen;
        if (suppress && _manager.enabled) { _manager.enabled = false; _suspendedManager = true; }
        else if (!suppress && _suspendedManager) ReleaseInput();
        // Only the explicitly marked test region may bypass UI occlusion.
        _manager.BlockWhenPointerOverUI = !OverTestRegion() || _blockTest;
        if (_helpPanelOpen && InputWrapper.GetKeyDown(KeyCode.Escape) && !editing) RequestCloseHelpPanel();
        if (suppress || overUi) return;
        HandleSelectionInput();
        OrbitCamera();
    }

    private void LateUpdate()
    {
        if (!_initialized || _manager == null) return;
        if (_closeHelpPanelPending)
        {
            _helpPanelOpen = _closeHelpPanelPending = false;
            _helpBackdrop.AddToClassList("hidden");
        }
        RefreshUi();
    }

    private void ReleaseInput()
    {
        if (_suspendedManager && _manager != null) _manager.enabled = true;
        _suspendedManager = false;
        _navigationPress = false;
        _releaseFrame = -1;
    }

    // ----- Selection -------------------------------------------------------------------------

    private void HandleSelectionInput()
    {
        if (_interacting) return; // don't pick targets mid-drag
        // Respect the same toggle the handle manager uses, so flipping it off in the HUD lets
        // both picking and handle drags pass through the panel (matching the on-screen hint).
        if (_manager.BlockWhenPointerOverUI && PointerOverUi()) return;

        // Left click: select (Shift adds to the current handle's group).
        if (InputWrapper.GetMouseButtonDown(0) && TryPickTarget(out var picked))
        {
            var additive = InputWrapper.GetKey(KeyCode.LeftShift) || InputWrapper.GetKey(KeyCode.RightShift);
            if (additive && _activeHandle != null) AddToActive(picked);
            else SelectSingle(picked);
        }

        // Right click: drop a target from its handle.
        if (InputWrapper.GetMouseButtonDown(1) && TryPickTarget(out var removed)) Drop(removed);
    }

    private bool TryPickTarget(out Transform target)
    {
        target = null;
        var ray = _camera.ScreenPointToRay(InputWrapper.MousePosition);
        if (!Physics.Raycast(ray, out var hit, 1000f, _pickMask)) return false;
        if (!hit.transform.GetComponent<DemoTarget>()) return false;
        target = hit.transform;
        return true;
    }

    private void SelectSingle(Transform target)
    {
        if (_targetToHandle.ContainsKey(target)) { _activeHandle = _targetToHandle[target]; return; }

        var handle = _manager.CreateHandle(target);
        if (handle == null) return;
        Subscribe(handle);
        _targetToHandle[target] = handle;
        _activeHandle = handle;
        ApplyAllToActive();
        Log($"CreateHandle({target.name})");
    }

    private void AddToActive(Transform target)
    {
        if (_targetToHandle.ContainsKey(target)) return;
        if (_manager.AddTarget(target, _activeHandle))
        {
            _targetToHandle[target] = _activeHandle;
            Log($"AddTarget({target.name})");
        }
        else Log($"AddTarget({target.name}) rejected (duplicate / parent-child)");
    }

    private void Drop(Transform target)
    {
        if (!_targetToHandle.TryGetValue(target, out var handle)) return;
        _manager.RemoveTarget(target, handle);
        _targetToHandle.Remove(target);
        if (_activeHandle == handle && _targetToHandle.All(kv => kv.Value != handle)) _activeHandle = null;
        Log($"RemoveTarget({target.name})");
    }

    private void GroupAll()
    {
        DestroyAll();
        var handle = _manager.CreateHandleFromList(_targets.Where(target => target != null).ToList());
        if (handle == null) return;
        Subscribe(handle);
        foreach (var t in _targets.Where(target => target != null)) _targetToHandle[t] = handle;
        _activeHandle = handle;
        ApplyAllToActive();
        Log($"CreateHandleFromList({_targets.Count} targets)");
    }

    private void DestroyAll()
    {
        ClearOwnedHandles();
        Log("Clear owned handles");
    }

    // ----- Apply HUD state to the active handle ----------------------------------------------

    private void ApplyAllToActive()
    {
        if (_activeHandle == null) return;
        if (_useSettings) _activeHandle.ApplySettings(_settings);
        TransformHandleManager.ChangeHandleType(_activeHandle, _type);
        _manager.ChangeHandleSpace(_activeHandle, _space);
        _activeHandle.Axes = _axes;
        ApplySnapping();
        _activeHandle.AutoScale = _autoScale;
        _activeHandle.AutoScaleSizeInPixels = _handleSizePixels;
        _activeHandle.SetScale(_scaleMultiplier);
    }

    private void ApplySnapping()
    {
        if (_activeHandle == null) return;
        _activeHandle.SnappingType = _snapType;
        _activeHandle.PositionSnap = Vector3.one * _posSnap;
        _activeHandle.RotationSnap = _rotSnap;
        _activeHandle.ScaleSnap = Vector3.one * _scaleSnap;
    }

    // ----- Events ----------------------------------------------------------------------------

    private void Subscribe(Handle handle)
    {
        handle.OnInteractionStartEvent += OnStart;
        handle.OnInteractionEvent += OnStay;
        handle.OnInteractionEndEvent += OnEnd;
        handle.OnHandleDestroyedEvent += OnDestroyed;
    }

    private void OnStart(Handle h) { _interacting = true; Log($"InteractionStart: {h.name}"); }
    private void OnStay(Handle h) { /* fires every drag frame */ }
    private void OnEnd(Handle h) { _interacting = false; Log($"InteractionEnd: {h.name}"); }

    private void OnDestroyed(Handle h)
    {
        h.OnInteractionStartEvent -= OnStart;
        h.OnInteractionEvent -= OnStay;
        h.OnInteractionEndEvent -= OnEnd;
        h.OnHandleDestroyedEvent -= OnDestroyed;
        Log($"HandleDestroyed: {h.name}");
    }

    // ----- Camera ----------------------------------------------------------------------------

    private void OrbitCamera()
    {
        if (_interacting) return;
        if (InputWrapper.GetMouseButton(2))
        {
            _camYaw += InputWrapper.GetAxis("Mouse X") * 3f;
            _camPitch = Mathf.Clamp(_camPitch - InputWrapper.GetAxis("Mouse Y") * 3f, -80f, 80f);
        }
        _camDist = Mathf.Clamp(_camDist - InputWrapper.GetAxis("Mouse ScrollWheel") * 10f, 3f, 40f);
        UpdateCamera();

    }

    private void UpdateCamera()
    {
        var rot = Quaternion.Euler(_camPitch, _camYaw, 0f);
        _camera.transform.position = rot * new Vector3(0f, 0f, -_camDist) + Vector3.up * 0.5f;
        _camera.transform.LookAt(Vector3.up * 0.5f);
    }

    private void Log(string msg)
    {
        _log.Add(msg);
        if (_log.Count > 50) _log.RemoveAt(0);

    }

    private void ClearOwnedHandles()
    {
        foreach (var handle in _targetToHandle.Values.Distinct().ToArray())
        {
            if (handle == null) continue;
            handle.OnInteractionStartEvent -= OnStart;
            handle.OnInteractionEvent -= OnStay;
            handle.OnInteractionEndEvent -= OnEnd;
            handle.OnHandleDestroyedEvent -= OnDestroyed;
            if (_manager != null) _manager.RemoveHandle(handle);
            if (handle != null) Destroy(handle.gameObject);
        }
        _targetToHandle.Clear(); _activeHandle = null; _interacting = false;
    }

    public void ResetLayout()
    {
        if (_interacting) return;
        ClearOwnedHandles();
        foreach (var pair in _initial)
        {
            if (pair.Key == null) continue;
            pair.Key.SetPositionAndRotation(pair.Value.Position, pair.Value.Rotation);
            pair.Key.localScale = pair.Value.Scale;
        }
        _type = HandleType.Position; _axes = HandleAxes.XYZ; _space = Space.Self;
        _snapType = SnappingType.Relative; _posSnap = _rotSnap = _scaleSnap = 0;
        _scaleMultiplier = 1; _autoScale = true; _useSettings = false;
        _showTest = false; _blockTest = true;
        _camYaw = 0; _camPitch = 15; _camDist = 12; UpdateCamera();
        _log.Clear();
        RefreshUi();
    }

    private void OnDestroy()
    {
        if (_ownedManager != null) Destroy(_ownedManager.gameObject);
        if (_settings != null) Destroy(_settings);
        foreach (var material in _ownedMaterials) if (material != null) Destroy(material);
        foreach (var target in _targets) if (target != null) Destroy(target.gameObject);
        if (_uiObject != null) Destroy(_uiObject);
        if (_runtimePanelSettings != null) Destroy(_runtimePanelSettings);
        if (_ownedEventSystem != null) Destroy(_ownedEventSystem);
    }

    private void BuildUi()
    {
        if (EventSystem.current == null)
        {
            _ownedEventSystem = new GameObject("Showcase Event System", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && TH_INPUTSYSTEM
            _ownedEventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
#else
            _ownedEventSystem.AddComponent<StandaloneInputModule>();
#endif
        }
        _uiObject = new GameObject("Showcase Controls");
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
        _workshop = _uiRoot.Q("workshop");
        _selectionLabel = _uiRoot.Q<Label>("selection-name");
        _selectionHint = _uiRoot.Q<Label>("selection-hint");
        _status = _uiRoot.Q<Label>("inspector-status");
        _eventLog = _uiRoot.Q<Label>("event-log");
        _selectionLabel.enableRichText = _status.enableRichText = _eventLog.enableRichText = false;
        _helpBackdrop = _uiRoot.Q("help-backdrop");
        _testRegion = _uiRoot.Q("test-region");
        _moveButton = _uiRoot.Q<Button>("move"); _rotateButton = _uiRoot.Q<Button>("rotate"); _scaleButton = _uiRoot.Q<Button>("scale");
        _spaceButton = _uiRoot.Q<Button>("space"); _resetButton = _uiRoot.Q<Button>("reset");
        _helpToggleButton = _uiRoot.Q<Button>("help-toggle"); _helpCloseButton = _uiRoot.Q<Button>("help-close");
        _groupButton = _uiRoot.Q<Button>("group-all"); _clearButton = _uiRoot.Q<Button>("destroy-all");
        _moveButton.clicked += Move; _rotateButton.clicked += Rotate; _scaleButton.clicked += Scale;
        _spaceButton.clicked += ToggleSpace; _resetButton.clicked += ResetLayout;
        _helpToggleButton.clicked += ToggleHelpPanel; _helpCloseButton.clicked += RequestCloseHelpPanel;
        _groupButton.clicked += GroupAll; _clearButton.clicked += DestroyAll;
        _sizeSlider = _uiRoot.Q<SliderInt>("size-slider"); _sizeInput = _uiRoot.Q<TextField>("size-input");
        _sizeSlider.RegisterValueChangedCallback(OnSizeChanged);
        _sizeInput.RegisterCallback<FocusInEvent>(OnSizeFocusIn);
        _sizeInput.RegisterCallback<FocusOutEvent>(OnSizeFocusOut);
        _sizeInput.RegisterCallback<KeyDownEvent>(OnSizeKeyDown, TrickleDown.TrickleDown);
        _positionSlider = _uiRoot.Q<Slider>("position-snap"); _rotationSlider = _uiRoot.Q<Slider>("rotation-snap");
        _scaleSlider = _uiRoot.Q<Slider>("scale-snap"); _multiplierSlider = _uiRoot.Q<Slider>("scale-multiplier");
        foreach (var slider in new[] { _positionSlider, _rotationSlider, _scaleSlider, _multiplierSlider }) slider.RegisterValueChangedCallback(OnAdvancedSliderChanged);
        _autoToggle = _uiRoot.Q<Toggle>("auto-scale"); _settingsToggle = _uiRoot.Q<Toggle>("runtime-settings");
        _showTestToggle = _uiRoot.Q<Toggle>("show-test"); _blockTestToggle = _uiRoot.Q<Toggle>("block-test");
        foreach (var toggle in new[] { _autoToggle, _settingsToggle, _showTestToggle, _blockTestToggle }) toggle.RegisterValueChangedCallback(OnToggleChanged);
        var enums = _uiRoot.Q("enum-fields"); enums.Clear();
        _typeField = new DropdownField("Handle type", System.Enum.GetNames(typeof(HandleType)).ToList(), (int)_type);
        _axesField = new DropdownField("Axes", System.Enum.GetNames(typeof(HandleAxes)).ToList(), System.Array.IndexOf(System.Enum.GetNames(typeof(HandleAxes)), _axes.ToString()));
        _snapField = new DropdownField("Snap mode", System.Enum.GetNames(typeof(SnappingType)).ToList(), (int)_snapType);
        foreach (var field in new[] { _typeField, _axesField, _snapField })
        {
            field.AddToClassList("advanced-field"); enums.Add(field); field.RegisterValueChangedCallback(OnEnumChanged);
        }
        _uiRoot.RegisterCallback<PointerDownEvent>(OnUiPointerDown, TrickleDown.TrickleDown);
        _uiRoot.RegisterCallback<NavigationMoveEvent>(OnUiNavigation, TrickleDown.TrickleDown);
        _uiRoot.RegisterCallback<FocusOutEvent>(OnUiFocusOut);
        _uiRoot.RegisterCallback<GeometryChangedEvent>(OnUiGeometryChanged);
        _helpBackdrop.RegisterCallback<PointerDownEvent>(OnBackdropPressed);
        UpdateUiLayout();
    }

    private void UnbindUi()
    {
        if (_uiRoot == null) return;
        _moveButton.clicked -= Move; _rotateButton.clicked -= Rotate; _scaleButton.clicked -= Scale;
        _spaceButton.clicked -= ToggleSpace; _resetButton.clicked -= ResetLayout;
        _helpToggleButton.clicked -= ToggleHelpPanel; _helpCloseButton.clicked -= RequestCloseHelpPanel;
        _groupButton.clicked -= GroupAll; _clearButton.clicked -= DestroyAll;
        _sizeSlider.UnregisterValueChangedCallback(OnSizeChanged);
        _sizeInput.UnregisterCallback<FocusInEvent>(OnSizeFocusIn);
        _sizeInput.UnregisterCallback<FocusOutEvent>(OnSizeFocusOut);
        _sizeInput.UnregisterCallback<KeyDownEvent>(OnSizeKeyDown, TrickleDown.TrickleDown);
        foreach (var slider in new[] { _positionSlider, _rotationSlider, _scaleSlider, _multiplierSlider }) slider.UnregisterValueChangedCallback(OnAdvancedSliderChanged);
        foreach (var toggle in new[] { _autoToggle, _settingsToggle, _showTestToggle, _blockTestToggle }) toggle.UnregisterValueChangedCallback(OnToggleChanged);
        foreach (var field in new[] { _typeField, _axesField, _snapField }) field.UnregisterValueChangedCallback(OnEnumChanged);
        _uiRoot.UnregisterCallback<PointerDownEvent>(OnUiPointerDown, TrickleDown.TrickleDown);
        _uiRoot.UnregisterCallback<NavigationMoveEvent>(OnUiNavigation, TrickleDown.TrickleDown);
        _uiRoot.UnregisterCallback<FocusOutEvent>(OnUiFocusOut);
        _uiRoot.UnregisterCallback<GeometryChangedEvent>(OnUiGeometryChanged);
        _helpBackdrop.UnregisterCallback<PointerDownEvent>(OnBackdropPressed);
        _sizeEditing = false;
        _uiRoot = null;
    }

    private void Move() => SetMode(HandleType.Position);
    private void Rotate() => SetMode(HandleType.Rotation);
    private void Scale() => SetMode(HandleType.Scale);
    private void SetMode(HandleType mode) { if (_interacting) return; _type = mode; ApplyAllToActive(); }
    private void ToggleSpace() { if (_interacting) return; _space = _space == Space.World ? Space.Self : Space.World; ApplyAllToActive(); }
    private void OnEnumChanged(ChangeEvent<string> evt)
    {
        if (_interacting) return;
        if (evt.target == _typeField) _type = (HandleType)System.Enum.Parse(typeof(HandleType), evt.newValue);
        else if (evt.target == _axesField) _axes = (HandleAxes)System.Enum.Parse(typeof(HandleAxes), evt.newValue);
        else _snapType = (SnappingType)System.Enum.Parse(typeof(SnappingType), evt.newValue);
        ApplyAllToActive();
    }
    private void OnAdvancedSliderChanged(ChangeEvent<float> evt)
    {
        if (_interacting) return;
        if (float.IsNaN(evt.newValue) || float.IsInfinity(evt.newValue)) { RefreshUi(); return; }
        if (evt.target == _positionSlider) _posSnap = Mathf.Clamp(evt.newValue, 0, 5);
        else if (evt.target == _rotationSlider) _rotSnap = Mathf.Clamp(evt.newValue, 0, 90);
        else if (evt.target == _scaleSlider) _scaleSnap = Mathf.Clamp01(evt.newValue);
        else _scaleMultiplier = Mathf.Clamp(evt.newValue, .1f, 10);
        ApplyAllToActive();
    }
    private void OnToggleChanged(ChangeEvent<bool> evt)
    {
        if (evt.target == _showTestToggle) _showTest = evt.newValue;
        else if (evt.target == _blockTestToggle) _blockTest = evt.newValue;
        else if (evt.target == _autoToggle) { _autoScale = evt.newValue; ApplyAllToActive(); }
        else
        {
            _useSettings = evt.newValue;
            if (_useSettings) _scaleMultiplier = _settings.HandleScale;
            ApplyAllToActive();
        }
        RefreshUi();
    }
    private void OnSizeChanged(ChangeEvent<int> evt) => SetHandleSize(evt.newValue);
    private void SetHandleSize(int value)
    {
        if (_interacting) return;
        _handleSizePixels = Mathf.Clamp(value, 64, 512);
        foreach (var handle in _targetToHandle.Values.Distinct())
            if (handle != null) handle.AutoScaleSizeInPixels = _handleSizePixels;
        _sizeSlider.SetValueWithoutNotify(_handleSizePixels);
        if (!_sizeEditing) _sizeInput.SetValueWithoutNotify(_handleSizePixels.ToString());
    }
    private void OnSizeFocusIn(FocusInEvent evt) { _sizeEditing = true; _sizeInput.parent.AddToClassList("editing"); }
    private void OnSizeFocusOut(FocusOutEvent evt) { if (_sizeEditing) FinishSizeEdit(false); }
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
        _sizeEditing = false; _lastSizeEditFrame = Time.frameCount;
        if (!cancel && int.TryParse(_sizeInput.value, out var value)) SetHandleSize(value);
        _sizeInput.SetValueWithoutNotify(_handleSizePixels.ToString());
        _sizeInput.parent.RemoveFromClassList("editing"); _sizeInput.Blur();
    }
    private static bool TextEditor(VisualElement element)
    {
        for (var current = element; current != null; current = current.parent)
            if (current is TextField || current.ClassListContains("unity-base-text-field")) return true;
        return false;
    }
    private bool IsEditingText() => _sizeEditing || TextEditor(_uiRoot?.focusController?.focusedElement as VisualElement);
    private void OnUiFocusOut(FocusOutEvent evt) { if (TextEditor(evt.target as VisualElement)) _lastSizeEditFrame = Time.frameCount; }
    private void OnUiPointerDown(PointerDownEvent evt)
    {
        var target = evt.target as VisualElement;
        if (!_blockTest && target != null && (target == _testRegion || _testRegion.Contains(target))) return;
        _navigationPress = true; _releaseFrame = -1;
        if (_sizeEditing && target != _sizeInput && !_sizeInput.Contains(target)) FinishSizeEdit(false);
    }
    private void OnUiNavigation(NavigationMoveEvent evt)
    {
        if (!InputWrapper.GetKey(KeyCode.W) && !InputWrapper.GetKey(KeyCode.E) && !InputWrapper.GetKey(KeyCode.R) &&
            !InputWrapper.GetKey(KeyCode.A) && !InputWrapper.GetKey(KeyCode.X) && !InputWrapper.GetKey(KeyCode.Z)) return;
#if !UNITY_6000_0_OR_NEWER
        evt.PreventDefault();
#endif
        evt.StopPropagation();
    }
    private void OnUiGeometryChanged(GeometryChangedEvent evt) => UpdateUiLayout();
    private void UpdateUiLayout() { _workshop.EnableInClassList("compact", _uiRoot.resolvedStyle.width < 1190); }
    private void ToggleHelpPanel()
    {
        if (_interacting) return;
        if (_helpPanelOpen) { RequestCloseHelpPanel(); return; }
        _helpPanelOpen = true; _closeHelpPanelPending = false;
        _helpBackdrop.RemoveFromClassList("hidden");
    }
    private void OnBackdropPressed(PointerDownEvent evt) { if (evt.target == _helpBackdrop) RequestCloseHelpPanel(); }
    private void RequestCloseHelpPanel() { if (_helpPanelOpen) { _closeHelpPanelPending = true; _navigationPress = true; _releaseFrame = -1; } }

    private void RefreshUi()
    {
        if (_uiRoot == null) return;
        if (_activeHandle != null)
        {
            _type = _activeHandle.Type; _space = _activeHandle.Space; _axes = _activeHandle.Axes;
        }
        _selectionLabel.text = _targetToHandle.Count == 0 ? "Select a target" : _targetToHandle.Count + " targets selected";
        _selectionHint.text = _activeHandle == null ? "Click a cube to create its handle." : "Shift-click adds to the active handle.";
        _status.text = "Targets: " + _targets.Count(target => target != null) + " · Active tool: " + _type;
        _eventLog.text = _log.Count == 0 ? "Interactions will appear here." : string.Join("\n", _log);
        foreach (var button in new[] { _moveButton, _rotateButton, _scaleButton, _spaceButton, _resetButton, _helpToggleButton, _groupButton, _clearButton }) button.SetEnabled(!_interacting);
        _moveButton.EnableInClassList("selected", _type == HandleType.Position);
        _rotateButton.EnableInClassList("selected", _type == HandleType.Rotation);
        _scaleButton.EnableInClassList("selected", _type == HandleType.Scale);
        _spaceButton.text = _space == Space.World ? "World" : "Local";
        _sizeSlider.SetEnabled(!_interacting && _autoScale); _sizeInput.SetEnabled(!_interacting && _autoScale);
        _sizeSlider.SetValueWithoutNotify(_handleSizePixels);
        if (!_sizeEditing) _sizeInput.SetValueWithoutNotify(_handleSizePixels.ToString());
        _autoToggle.SetValueWithoutNotify(_autoScale); _settingsToggle.SetValueWithoutNotify(_useSettings);
        _showTestToggle.SetValueWithoutNotify(_showTest); _blockTestToggle.SetValueWithoutNotify(_blockTest);
        foreach (var field in new[] { _typeField, _axesField, _snapField }) field.SetEnabled(!_interacting);
        _typeField.SetValueWithoutNotify(_type.ToString()); _axesField.SetValueWithoutNotify(_axes.ToString()); _snapField.SetValueWithoutNotify(_snapType.ToString());
        foreach (var slider in new[] { _positionSlider, _rotationSlider, _scaleSlider, _multiplierSlider }) slider.SetEnabled(!_interacting);
        if (!IsEditingText())
        {
            _positionSlider.SetValueWithoutNotify(_posSnap); _rotationSlider.SetValueWithoutNotify(_rotSnap);
            _scaleSlider.SetValueWithoutNotify(_scaleSnap); _multiplierSlider.SetValueWithoutNotify(_scaleMultiplier);
        }
        _positionSlider.label = "Position snap · " + _posSnap.ToString("0.##");
        _rotationSlider.label = "Rotation snap · " + _rotSnap.ToString("0.#") + "°";
        _scaleSlider.label = "Scale snap · " + _scaleSnap.ToString("0.##");
        _multiplierSlider.label = "Scale multiplier · " + _scaleMultiplier.ToString("0.##");
        _testRegion.EnableInClassList("hidden", !_showTest);
        _uiRoot.Q<Label>("test-region-hint").text = _blockTest ? "This region blocks scene input." : "Scene input passes through this region.";
    }

}

/// <summary>Marker so selection only includes spawned targets.</summary>
public class DemoTarget : MonoBehaviour { }
