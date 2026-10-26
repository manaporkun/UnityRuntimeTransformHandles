using System;
using System.Collections.Generic;
using System.Linq;
using TransformHandles;
using TransformHandles.Utils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using InputWrapper = TransformHandles.Utils.InputWrapper;

namespace TransformHandles.Samples.ProductConfigurator
{
    /// <summary>A socket assembly and constrained hinge sample using the public Core API.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class ProductConfiguratorDemo : MonoBehaviour
    {
        [SerializeField] private Camera sceneCamera;
        private enum AssemblyState { Free, Attaching, Attached }
        [Serializable]
        private sealed class AssemblyPart
        {
            public Transform root;
            public string displayName;
            public Transform socket;
            public Transform parentRoot;
            public Vector2 angleLimits = new Vector2(-30, 60);
            [NonSerialized] public AssemblyState state;
        }
        private struct PartSnapshot
        {
            public Transform parent;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public PosRotScale world;
            public AssemblyState state;
        }
        [SerializeField] private AssemblyPart[] parts;
        [SerializeField] private Material previewMaterial;
        [SerializeField] private Mesh socketMarkerMesh;
        private const float SnapDistance = .25f, AttachDuration = .18f;
        private readonly Dictionary<Transform, AssemblyPart> _parts = new Dictionary<Transform, AssemblyPart>();
        private readonly Dictionary<AssemblyPart, PartSnapshot> _dragStart = new Dictionary<AssemblyPart, PartSnapshot>();
        private bool _rebuildPending;
        private AssemblyPart _candidate, _attaching;
        private readonly List<MeshFilter> _previewMeshes = new List<MeshFilter>();
        private MaterialPropertyBlock _markerProperties;
        private Vector3 _attachStartPosition;
        private Quaternion _attachStartRotation;
        private float _attachTime;
        private Button _detachButton;
        private Label _jointLabel;
        [SerializeField] private TransformHandleSettings interactionSettings;
        [SerializeField] private VisualTreeAsset uiLayout;
        [SerializeField] private PanelSettings uiPanelSettings;

        private readonly List<Transform> _selection = new List<Transform>();
        private readonly Dictionary<Transform, PosRotScale> _initial = new Dictionary<Transform, PosRotScale>();
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
        private string _notice = "Start with the arm. Move its hinge to the base socket.";
        private float _yaw = 25f, _pitch = 42f, _distance = 12f;
        private Vector3 _cameraFocus = new Vector3(-1.2f, .4f, 0);
        private int _navigationButton = -1;
        private bool _navigationStarted;
        private Vector3 _navigationOrigin, _navigationPrevious;
        private int _pickMask;
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern float TH_ProductConfiguratorReadScroll();
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void TH_ProductConfiguratorResetScroll();
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void TH_ProductConfiguratorReleaseScroll();
#endif

        public int SelectionCount => _selection.Count;
        public Handle ActiveHandle => _handle;
        public bool IsDragging => _dragging;

        private void Awake()
        {
#if ENABLE_INPUT_SYSTEM && !TH_INPUTSYSTEM
            Debug.LogError("Lamp Assembly requires Input System 1.1.1 or later when the new input backend is enabled.", this);
            enabled = false;
            return;
#endif
            if (sceneCamera == null || previewMaterial == null || socketMarkerMesh == null ||
                interactionSettings == null || interactionSettings.EnableShortcuts || !ValidAssembly())
            {
                Debug.LogError("Lamp Assembly requires its camera, materials, marker mesh, shortcuts-disabled settings and an acyclic assembly with unit-scale roots, sockets and valid hinge limits.", this);
                enabled = false;
                return;
            }
            if (uiLayout == null || uiPanelSettings == null)
            {
                Debug.LogError("Lamp Assembly requires its UI Toolkit layout and panel settings. Reimport the complete sample before playing.", this);
                enabled = false;
                return;
            }
            var layer = LayerMask.NameToLayer("TransformHandle");
            if (layer < 0)
            {
                Debug.LogError("Run Tools > Transform Handles > Setup Layer before playing the Lamp Assembly sample.", this);
                enabled = false;
                return;
            }
            _pickMask = ~(1 << layer);
            foreach (var part in parts)
            {
                _parts.Add(part.root, part);
                _initial.Add(part.root, Capture(part.root));
            }
            _markerProperties = new MaterialPropertyBlock();
            _markerProperties.SetColor("_Color", new Color(.14f, .55f, .47f, .85f));
            BuildUi();
            _initialized = true;
            UpdateCamera();
        }

        private void OnEnable()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            TH_ProductConfiguratorResetScroll();
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
            if ((_dragging || _attaching != null) && _dragStart.Keys.Any(p => p.root == null || p.socket == null || p.parentRoot == null))
                CancelDrag();
            ReconcileAttachments();
            if (_selection.Any(t => t == null))
            {
                CancelDrag();
                _selection.RemoveAll(t => t == null);
                _rebuildPending = true;
            }
            if (_rebuildPending) { _rebuildPending = false; RebuildHandle(); }
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
            if (!_dragging && _attaching == null)
            {
                if (!editingSize && InputWrapper.GetKeyDown(KeyCode.W)) SetMode(HandleType.Position);
                if (!editingSize && InputWrapper.GetKeyDown(KeyCode.E)) SetMode(HandleType.Rotation);
            }
        }

        private void LateUpdate()
        {
            if (!_initialized || _manager == null) return;
            AdvanceAttachment(Time.unscaledDeltaTime);
            DrawAssemblyPreview();
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
                        picked = ResolvePart(hit.transform);
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
            return _pressFrame >= 0 && !_pressCancelled && !_pressBlocked && !overUi && !_dragging && _attaching == null &&
                _lastStartFrame < _pressFrame && LogicalPointerDistance(position, _pressPosition) < 6f;
        }

        public void SelectTarget(Transform target)
        {
            if (!CanChange()) return;
            target = ResolvePart(target);
            if (target == null) return;
            _selection.Clear();
            _selection.Add(target);
            RebuildHandle();
            _notice = Selected.state == AssemblyState.Attached
                ? "Rotate the hinge, or detach to move this part." : "Move the hinge into the highlighted socket.";
        }

        private AssemblyPart Selected => _selection.Count == 1 && _selection[0] != null && _parts.TryGetValue(_selection[0], out var part) ? part : null;

        private Transform ResolvePart(Transform hit)
        {
            while (hit != null)
            {
                if (_parts.ContainsKey(hit)) return hit;
                hit = hit.parent;
            }
            return null;
        }

        public void ClearSelection()
        {
            _pressCancelled = true;
            CancelDrag();
            _rebuildPending = false;
            RemoveOwnedHandle();
            _selection.Clear();
            _candidate = null;
            _notice = "Select the arm or lamp head to continue assembling.";
        }

        public void SetMode(HandleType mode)
        {
            if (!CanChange() || Selected == null) return;
            var allowed = Selected.state == AssemblyState.Attached ? HandleType.Rotation : HandleType.Position;
            if (mode == allowed) ConfigureHandle();
        }

        public void DetachSelected()
        {
            if (!CanChange() || Selected == null || Selected.state != AssemblyState.Attached) return;
            var part = Selected;
            RemoveOwnedHandle();
            part.root.SetParent(null, true);
            part.state = AssemblyState.Free;
            RebuildHandle();
            _notice = "Detached. Attached children travel with this part.";
        }

        public void ResetLayout()
        {
            if (!_initialized || !isActiveAndEnabled) return;
            ClearSelection();
            foreach (var part in parts) if (part.root != null) part.root.SetParent(null, true);
            foreach (var part in parts)
            {
                if (part.root != null) Restore(part.root, _initial[part.root]);
                part.state = AssemblyState.Free;
            }
            CancelNavigation();
            _yaw = 25f; _pitch = 42f; _distance = 12f;
            _cameraFocus = new Vector3(-1.2f, .4f, 0);
            UpdateCamera();
            _notice = "Start with the arm. Move its hinge to the base socket.";
        }

        private bool CanChange() => _initialized && isActiveAndEnabled && !_dragging && _attaching == null && _manager != null;

        private void RebuildHandle()
        {
            RemoveOwnedHandle();
            _selection.RemoveAll(t => t == null);
            if (_selection.Count == 0 || _manager == null || !isActiveAndEnabled) return;
            _handle = _manager.CreateHandle(_selection[0]);
            if (_handle == null) return;
            _handle.LocalSpaceReference = _selection[_selection.Count - 1];
            _handle.AutoScale = true;
            _handle.AutoScaleSizeInPixels = _handleSizePixels;
            _handle.OnInteractionStartEvent += OnDragStart;
            _handle.OnInteractionEvent += OnDrag;
            _handle.OnInteractionEndEvent += OnDragEnd;
            ConfigureHandle();
        }

        private void ConfigureHandle()
        {
            var part = Selected;
            if (_handle == null || part == null) return;
            _mode = part.state == AssemblyState.Attached ? HandleType.Rotation : HandleType.Position;
            TransformHandleManager.ChangeHandleType(_handle, _mode);
            _handle.Axes = _mode == HandleType.Position ? HandleAxes.XYZ : HandleAxes.X;
            _manager.ChangeHandleSpace(_handle, _mode == HandleType.Position ? Space.World : Space.Self);
            _handle.PositionSnap = Vector3.zero;
            _handle.RotationSnap = 0;
        }

        private float JointAngle(AssemblyPart part)
        {
            var relative = Quaternion.Inverse(part.socket.rotation) * part.root.rotation;
            return Mathf.Clamp(Mathf.DeltaAngle(0, 2 * Mathf.Atan2(relative.x, relative.w) * Mathf.Rad2Deg),
                part.angleLimits.x, part.angleLimits.y);
        }

        private void OnDragStart(Handle handle)
        {
            if (handle != _handle || _tearingDown || Selected == null) return;
            _lastStartFrame = Time.frameCount;
            CancelNavigation();
            _dragging = true;
            _dragStart.Clear();
            foreach (var part in parts)
                if (part.root != null) _dragStart.Add(part, new PartSnapshot {
                    parent = part.root.parent, localPosition = part.root.localPosition,
                    localRotation = part.root.localRotation, world = Capture(part.root), state = part.state });
            if (Selected.state == AssemblyState.Attached && !HasLiveAttachment(Selected))
            {
                CancelDrag();
                ReconcileAttachments();
                return;
            }
            if (Selected.state == AssemblyState.Attached)
            {
                float angle = JointAngle(Selected);
                handle.GetComponentInChildren<RotationAxis>().DragAngleLimits =
                    new Vector2(Selected.angleLimits.x - angle, Selected.angleLimits.y - angle);
            }
        }

        private static bool HasLiveAttachment(AssemblyPart part) => part.root != null && part.socket != null &&
            part.parentRoot != null && part.root.parent == part.parentRoot && part.socket.IsChildOf(part.parentRoot);

        private void ReconcileAttachments()
        {
            foreach (var part in parts)
            {
                if (part.state != AssemblyState.Attached || HasLiveAttachment(part)) continue;
                if (part.root != null) part.root.SetParent(null, true);
                part.state = AssemblyState.Free;
                _rebuildPending = true;
                _notice = "A joint was removed. The surviving part is free to move.";
            }
        }

        private bool CanAttach(AssemblyPart part)
        {
            if (part == null || part.root == null || part.socket == null || part.parentRoot == null || part.state != AssemblyState.Free) return false;
            return !_parts.TryGetValue(part.parentRoot, out var parentPart) || parentPart.state == AssemblyState.Attached;
        }

        private void OnDrag(Handle handle)
        {
            if (handle != _handle || !_dragging || _tearingDown) return;
            var part = Selected;
            _candidate = CanAttach(part) && Vector3.Distance(part.root.position, part.socket.position) <= SnapDistance ? part : null;
            _notice = part != null && part.state == AssemblyState.Attached ? "Rotate within the joint limits."
                : _candidate != null ? "Release to attach." : CanAttach(part) ? "Bring the hinge closer to its socket." : "Attach the arm to the base first.";
        }

        private void CancelDrag()
        {
            if (!_dragging && _attaching == null) return;
            RemoveOwnedHandle();
            _attaching = _candidate = null;
            // Detach all survivors first so restoring a parent cannot transform a restored child twice.
            foreach (var pair in _dragStart) if (pair.Key.root != null) pair.Key.root.SetParent(null, true);
            foreach (var pair in _dragStart)
            {
                var part = pair.Key; var state = pair.Value;
                if (part.root == null) continue;
                part.root.SetParent(state.parent, false);
                if (state.parent != null) { part.root.localPosition = state.localPosition; part.root.localRotation = state.localRotation; part.root.localScale = state.world.Scale; }
                else Restore(part.root, state.world);
                part.state = state.state;
            }
            ReconcileAttachments();
            _dragStart.Clear();
            _rebuildPending = true;
            _notice = "Assembly change cancelled. The previous pose is restored.";
        }

        private void OnDragEnd(Handle handle)
        {
            if (_tearingDown || handle != _handle || !_dragging) return;
            if (_manager == null || !_manager.isActiveAndEnabled || !handle.isActiveAndEnabled ||
                (!_releasePending && !InputWrapper.GetMouseButtonUp(0)))
            {
                CancelDrag();
                return;
            }
            OnDrag(handle); // Re-evaluate the socket at the final position, not the previous frame.
            var candidate = _candidate;
            _dragging = false;
            if (candidate != null)
            {
                RemoveOwnedHandle();
                _attaching = candidate;
                candidate.state = AssemblyState.Attaching;
                _attachStartPosition = candidate.root.position;
                _attachStartRotation = candidate.root.rotation;
                _attachTime = 0;
                _notice = "Attaching…";
            }
            else { _dragStart.Clear(); _notice = Selected != null && Selected.state == AssemblyState.Attached ? "Joint pose updated." : "Part moved. Bring its hinge to the socket to attach."; }
            _candidate = null;
        }

        private void AdvanceAttachment(float deltaTime)
        {
            if (_attaching == null) return;
            var part = _attaching;
            if (part.root == null || part.socket == null || part.parentRoot == null || _manager == null || !_manager.isActiveAndEnabled)
            { CancelDrag(); return; }
            _attachTime += deltaTime;
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(_attachTime / AttachDuration));
            part.root.SetPositionAndRotation(Vector3.Lerp(_attachStartPosition, part.socket.position, t),
                Quaternion.Slerp(_attachStartRotation, part.socket.rotation, t));
            if (_attachTime < AttachDuration) return;
            part.root.SetParent(part.parentRoot, true);
            part.root.SetPositionAndRotation(part.socket.position, part.socket.rotation);
            part.state = AssemblyState.Attached;
            _attaching = null;
            _dragStart.Clear();
            RebuildHandle();
            _notice = "Attached. Rotate the hinge or select the next part.";
        }

        private void DrawAssemblyPreview()
        {
            var part = Selected;
            if (!CanAttach(part)) return;
            var socket = Matrix4x4.TRS(part.socket.position, part.socket.rotation, Vector3.one * .48f);
            Graphics.DrawMesh(socketMarkerMesh, socket, previewMaterial, 2, sceneCamera, 0, _markerProperties,
                UnityEngine.Rendering.ShadowCastingMode.Off, false);
            if (_candidate != part) return;
            var finalPose = Matrix4x4.TRS(part.socket.position, part.socket.rotation, Vector3.one) * part.root.worldToLocalMatrix;
            part.root.GetComponentsInChildren(false, _previewMeshes);
            foreach (var mesh in _previewMeshes)
            {
                if (mesh.sharedMesh == null) continue;
                Graphics.DrawMesh(mesh.sharedMesh, finalPose * mesh.transform.localToWorldMatrix, previewMaterial, 2,
                    sceneCamera, 0, null, UnityEngine.Rendering.ShadowCastingMode.Off, false);
            }
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
            TH_ProductConfiguratorReleaseScroll();
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
            TH_ProductConfiguratorReleaseScroll();
#endif
            if (_ownedManager != null) Destroy(_ownedManager.gameObject);
            if (_uiObject != null) Destroy(_uiObject);
            if (_runtimePanelSettings != null) Destroy(_runtimePanelSettings);
            if (_ownedEventSystem != null) Destroy(_ownedEventSystem);
        }

        private static PosRotScale Capture(Transform t) => new PosRotScale { Position = t.position, Rotation = t.rotation, Scale = t.localScale };
        private static void Restore(Transform t, PosRotScale state) { t.SetPositionAndRotation(state.Position, state.Rotation); t.localScale = state.Scale; }
        private bool ValidAssembly()
        {
            if (parts == null || parts.Length == 0 || parts.Any(p => p == null || p.root == null || p.socket == null || p.parentRoot == null)) return false;
            if (parts.Select(p => p.root).Distinct().Count() != parts.Length) return false;
            foreach (var part in parts)
            {
                if (part.root.parent != null || !UnitScale(part.root) || !UnitScale(part.parentRoot) || !UnitScale(part.socket) ||
                    !part.socket.IsChildOf(part.parentRoot) || part.socket.IsChildOf(part.root) ||
                    !Finite(part.angleLimits.x) || !Finite(part.angleLimits.y) || part.angleLimits.x > 0 || part.angleLimits.y < 0 ||
                    part.angleLimits.y - part.angleLimits.x > 180 ||
                    !part.root.GetComponentsInChildren<Collider>().Any(c => c.enabled && !c.isTrigger)) return false;
                var visited = new HashSet<Transform> { part.root };
                var parent = part.parentRoot;
                while (parent != null)
                {
                    if (!visited.Add(parent)) return false;
                    var next = parts.FirstOrDefault(p => p.root == parent);
                    parent = next == null ? null : next.parentRoot;
                }
            }
            return true;
        }

        private static bool UnitScale(Transform t) => (t.lossyScale - Vector3.one).sqrMagnitude < .000001f;

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

        private void CancelNavigation()
        {
            _navigationButton = -1;
            _navigationStarted = false;
        }

        private void OnApplicationFocus(bool focused)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            TH_ProductConfiguratorResetScroll();
#endif
            if (focused) return;
            CancelDrag();
            _rebuildPending = true;
            CancelNavigation();
            _pressCancelled = true;
            _pressFrame = -1;
            _releasePending = false;
        }

        private void ProcessNavigation(Vector3 position, bool leftDown, bool rightDown, bool middleDown,
            bool leftHeld, bool rightHeld, bool middleHeld, bool overUi, bool editing, float scroll, int frame)
        {
            // A handle press owns the whole gesture, even if its callbacks ended it this frame.
            if (_dragging || _attaching != null || _lastStartFrame == frame)
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
            _distance = Mathf.Clamp(_distance * Mathf.Exp(-Mathf.Clamp(scroll * .08f, -.15f, .15f)), 5f, 22f);
        }

        private static float ReadScroll()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return TH_ProductConfiguratorReadScroll();
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
                _ownedEventSystem = new GameObject("Lamp Assembly Event System", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && TH_INPUTSYSTEM
                _ownedEventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
#else
                _ownedEventSystem.AddComponent<StandaloneInputModule>();
#endif
            }
            _uiObject = new GameObject("Lamp Assembly Controls");
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
            _detachButton = _uiRoot.Q<Button>("detach");
            _jointLabel = _uiRoot.Q<Label>("joint-limits");
            _helpToggleButton = _uiRoot.Q<Button>("help-toggle");
            _helpCloseButton = _uiRoot.Q<Button>("help-close");
            _helpBackdrop = _uiRoot.Q<VisualElement>("help-backdrop");
            _helpCard = _uiRoot.Q<VisualElement>("help-card");
            _sizeSlider = _uiRoot.Q<SliderInt>("size-slider");
            _sizeInput = _uiRoot.Q<TextField>("size-input");
            _sizeInput.SetValueWithoutNotify(_handleSizePixels.ToString());
            _sizeSlider.SetValueWithoutNotify(_handleSizePixels);
            _detachButton.clicked += DetachSelected;
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
            _detachButton.clicked -= DetachSelected;
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
            if (_dragging || _attaching != null) return;
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
            var part = Selected;
            var busy = _dragging || _attaching != null;
            var attached = part != null && part.state == AssemblyState.Attached;
            var selectionName = part != null ? part.displayName : "Select a part";
            var state = $"{selectionName}|{attached}|{busy}|{_handle != null}|{_notice}|{_helpPanelOpen}";
            if (state == _uiState) return;
            _uiState = state;
            _selectionLabel.text = selectionName.Replace('\n', ' ').Replace('\r', ' ');
            _selectionLabel.tooltip = part != null ? selectionName : "";
            _selectionHint.text = _notice;
            _moveButton.SetEnabled(!busy && part != null && !attached);
            _rotateButton.SetEnabled(!busy && part != null && attached);
            _detachButton.SetEnabled(!busy && attached);
            _resetButton.SetEnabled(!busy);
            _helpToggleButton.SetEnabled(!busy);
            _sizeInput.SetEnabled(!busy);
            _sizeSlider.SetEnabled(!busy);
            _moveButton.EnableInClassList("selected", part != null && !attached);
            _rotateButton.EnableInClassList("selected", attached);
            _helpToggleButton.EnableInClassList("selected", _helpPanelOpen);
            _jointLabel.text = part == null ? "Arm → base, then head → arm."
                : attached ? $"Local X hinge · {part.angleLimits.x:0}° to {part.angleLimits.y:0}°"
                : "Free movement · XYZ";
            _status.text = _notice;
        }
    }
}
