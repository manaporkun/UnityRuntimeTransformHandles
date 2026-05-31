using System.Collections.Generic;
using System.Linq;
using TransformHandles;
using UnityEngine;

/// <summary>
/// Self-contained showcase that exercises the whole public surface of the package:
/// every <see cref="HandleType"/>, <see cref="HandleAxes"/> mask, <see cref="Space"/>,
/// snapping mode, scale/auto-scale, multi-target grouping, a runtime
/// <see cref="TransformHandleSettings"/>, and all interaction events.
///
/// Drop this on an empty GameObject and press Play — it spawns its own targets, ensures a
/// camera, and draws an IMGUI control panel. No scene wiring required. The handle gizmos
/// still need the "TransformHandle" physics layer (Tools > Transform Handles > Setup Layer).
/// </summary>
public class HandleDemo : MonoBehaviour
{
    [Header("Spawned targets")]
    [SerializeField] private int targetCount = 6;
    [SerializeField] private float spacing = 2.5f;

    private static readonly Color[] Palette =
    {
        new Color(0.90f, 0.30f, 0.30f), new Color(0.30f, 0.80f, 0.40f),
        new Color(0.30f, 0.55f, 0.95f), new Color(0.95f, 0.80f, 0.25f),
        new Color(0.75f, 0.40f, 0.90f), new Color(0.35f, 0.85f, 0.85f),
    };

    private Camera _camera;
    private TransformHandleManager _manager;
    private TransformHandleSettings _settings;

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
    private Vector2 _logScroll;
    private float _camYaw, _camPitch = 15f, _camDist = 12f;

    // Cached HUD styles (default IMGUI skin is tiny and low-contrast over a bright skybox).
    private GUIStyle _panelStyle;
    private Texture2D _panelTex;
    private bool _uiInit;

    private void Awake()
    {
        _camera = Camera.main;
        if (_camera == null)
        {
            var go = new GameObject("Demo Camera") { tag = "MainCamera" };
            _camera = go.AddComponent<Camera>();
        }

        _manager = TransformHandleManager.Instance;
        _manager.mainCamera = _camera;

        // Build a runtime settings asset (no .asset file needed) to demo Settings injection.
        _settings = TransformHandleSettings.CreateDefault();
    }

    private void Start()
    {
        for (var i = 0; i < targetCount; i++)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"Target_{i}";
            cube.AddComponent<DemoTarget>();
            cube.transform.position = new Vector3((i - (targetCount - 1) * 0.5f) * spacing, 0.5f, 0f);
            cube.GetComponent<Renderer>().material.color = Palette[i % Palette.Length];
            _targets.Add(cube.transform);
        }

        if (!Object.FindAnyObjectByType<Light>())
        {
            var lightGo = new GameObject("Demo Light");
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
    }

    private void Update()
    {
        HandleSelectionInput();
        OrbitCamera();
    }

    // ----- Selection -------------------------------------------------------------------------

    private void HandleSelectionInput()
    {
        if (_interacting) return; // don't pick targets mid-drag

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
        if (!Physics.Raycast(ray, out var hit, 1000f)) return false;
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
        var handle = _manager.CreateHandleFromList(_targets.ToList());
        if (handle == null) return;
        Subscribe(handle);
        foreach (var t in _targets) _targetToHandle[t] = handle;
        _activeHandle = handle;
        ApplyAllToActive();
        Log($"CreateHandleFromList({_targets.Count} targets)");
    }

    private void DestroyAll()
    {
        _manager.DestroyAllHandles();
        _targetToHandle.Clear();
        _activeHandle = null;
        Log("DestroyAllHandles()");
    }

    // ----- Apply HUD state to the active handle ----------------------------------------------

    private void ApplyAllToActive()
    {
        if (_activeHandle == null) return;
        TransformHandleManager.ChangeHandleType(_activeHandle, _type);
        _manager.ChangeHandleSpace(_activeHandle, _space);
        _activeHandle.ChangeAxes(_axes);
        ApplySnapping();
        _activeHandle.autoScale = _autoScale;
        _activeHandle.SetScale(_scaleMultiplier);
        if (_useSettings) _activeHandle.ApplySettings(_settings);
    }

    private void ApplySnapping()
    {
        if (_activeHandle == null) return;
        _activeHandle.snappingType = _snapType;
        _activeHandle.positionSnap = Vector3.one * _posSnap;
        _activeHandle.rotationSnap = _rotSnap;
        _activeHandle.scaleSnap = Vector3.one * _scaleSnap;
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

        var rot = Quaternion.Euler(_camPitch, _camYaw, 0f);
        _camera.transform.position = rot * new Vector3(0f, 0f, -_camDist) + Vector3.up * 0.5f;
        _camera.transform.LookAt(Vector3.up * 0.5f);
    }

    private void Log(string msg)
    {
        _log.Add(msg);
        if (_log.Count > 50) _log.RemoveAt(0);
        _logScroll.y = float.MaxValue;
    }

    // ----- HUD -------------------------------------------------------------------------------

    private void EnsureUi()
    {
        if (_uiInit) return;
        _uiInit = true;

        _panelTex = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        _panelTex.SetPixel(0, 0, new Color(0.07f, 0.07f, 0.09f, 0.94f));
        _panelTex.Apply();

        _panelStyle = new GUIStyle { padding = new RectOffset(14, 14, 14, 14) };
        _panelStyle.normal.background = _panelTex;

        // Bump the shared skin so labels/buttons/toggles/sliders are legible.
        var s = GUI.skin;
        s.label.fontSize = 14;
        s.label.richText = true;
        s.label.normal.textColor = Color.white;
        s.button.fontSize = 14;
        foreach (var st in new[] { s.toggle.normal, s.toggle.onNormal, s.toggle.hover,
                                   s.toggle.onHover, s.toggle.active, s.toggle.onActive })
            st.textColor = Color.white;
        s.toggle.fontSize = 14;
    }

    private void OnGUI()
    {
        EnsureUi();

        GUILayout.BeginArea(new Rect(10, 10, 360, Screen.height - 20), _panelStyle);
        GUILayout.Label("<size=17><b>Transform Handles — Demo</b></size>");
        GUILayout.Label($"Targets: {_targets.Count}   Selected: {_targetToHandle.Count}   " +
                        $"Active: {(_activeHandle ? "yes" : "none")}");
        GUILayout.Label("LMB select · Shift+LMB add · RMB drop · MMB orbit · wheel zoom");
        GUILayout.Label("Keys: W/E/R Pos/Rot/Scale · A All · X space · Z pivot");
        GUILayout.Space(6);

        EnumRow("Type", ref _type, () => { if (_activeHandle != null) TransformHandleManager.ChangeHandleType(_activeHandle, _type); });
        EnumRow("Axes", ref _axes, () => _activeHandle?.ChangeAxes(_axes));
        EnumRow("Space", ref _space, () => { if (_activeHandle != null) _manager.ChangeHandleSpace(_activeHandle, _space); });
        EnumRow("Snap mode", ref _snapType, ApplySnapping);

        GUILayout.Space(4);
        Slider("Position snap", ref _posSnap, 0f, 5f, ApplySnapping);
        Slider("Rotation snap", ref _rotSnap, 0f, 90f, ApplySnapping);
        Slider("Scale snap", ref _scaleSnap, 0f, 1f, ApplySnapping);

        GUILayout.Space(4);
        Slider("Handle scale", ref _scaleMultiplier, 0.1f, 10f, () => _activeHandle?.SetScale(_scaleMultiplier));

        var auto = GUILayout.Toggle(_autoScale, " Auto-scale with distance");
        if (auto != _autoScale) { _autoScale = auto; if (_activeHandle != null) _activeHandle.autoScale = _autoScale; }

        var useSettings = GUILayout.Toggle(_useSettings, " Apply runtime Settings asset");
        if (useSettings != _useSettings) { _useSettings = useSettings; if (_useSettings && _activeHandle != null) _activeHandle.ApplySettings(_settings); }

        GUILayout.Space(6);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Group all")) GroupAll();
        if (GUILayout.Button("Destroy all")) DestroyAll();
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        GUILayout.Label("<b>Event log</b>");
        _logScroll = GUILayout.BeginScrollView(_logScroll, GUILayout.Height(160));
        foreach (var line in _log) GUILayout.Label(line);
        GUILayout.EndScrollView();

        GUILayout.EndArea();
    }

    private void EnumRow<T>(string label, ref T value, System.Action onChange) where T : System.Enum
    {
        GUILayout.Label($"{label}: <b>{value}</b>");
        var names = System.Enum.GetNames(typeof(T));
        var current = System.Array.IndexOf(names, value.ToString());
        // Fewer, wider columns when names are long so they don't clip (e.g. PositionRotation).
        var maxLen = names.Max(n => n.Length);
        var cols = maxLen <= 6 ? 4 : maxLen <= 10 ? 3 : 2;
        var picked = GUILayout.SelectionGrid(current, names, cols);
        if (picked != current)
        {
            value = (T)System.Enum.Parse(typeof(T), names[picked]);
            onChange();
        }
    }

    private static void Slider(string label, ref float value, float min, float max, System.Action onChange)
    {
        GUILayout.Label($"{label}: {value:0.00}");
        var v = GUILayout.HorizontalSlider(value, min, max);
        if (!Mathf.Approximately(v, value)) { value = v; onChange(); }
    }
}

/// <summary>Marker so the demo's raycast only selects spawned targets, not handle gizmos.</summary>
public class DemoTarget : MonoBehaviour { }
