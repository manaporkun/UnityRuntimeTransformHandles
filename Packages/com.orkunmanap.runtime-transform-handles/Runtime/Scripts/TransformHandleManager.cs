using System;
using System.Collections.Generic;
using TransformHandles.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using static TransformHandles.Utils.InputWrapper;

namespace TransformHandles
{
    /// <summary>
    /// Central manager for all transform handles in the scene.
    /// Handles creation, destruction, and interaction with transform handles.
    /// </summary>
    public class TransformHandleManager : Singleton<TransformHandleManager>
    {
        private const int MaxRaycastHits = 16;
        private const float RaycastMaxDistance = 1000f;

        [SerializeField, FormerlySerializedAs("mainCamera")] private Camera _mainCamera;

        /// <summary>The main camera used for raycasting. Assign to recover when no <see cref="Camera.main"/> exists.</summary>
        public Camera MainCamera
        {
            get => _mainCamera;
            set
            {
                if (_mainCamera == value) return;

                _mainCamera = value;
                CancelInteractionSafe(null);

                if (this == null || _isTornDown || _handleGroupMap == null) return;
                RefreshHandleCameras();
            }
        }

        /// <inheritdoc cref="MainCamera"/>
        [Obsolete("Use MainCamera instead.")]
        public Camera mainCamera { get => MainCamera; set => MainCamera = value; }

        [Header("Prefabs")]
        [SerializeField] private GameObject transformHandlePrefab;
        [SerializeField] private GameObject ghostPrefab;

        [Header("Settings")]
        [Tooltip("Optional settings asset. If not assigned, uses the values below.")]
        [SerializeField] private TransformHandleSettings settings;
        [SerializeField] private LayerMask layerMask;
        [SerializeField] private string handleLayerName = "TransformHandle";
        [SerializeField] private Color highlightColor = Color.white;

        [Tooltip("Opt-in: when enabled, a handle interaction will not start while the pointer is " +
                 "over a uGUI element (requires an EventSystem in the scene). An interaction " +
                 "already in progress is never interrupted, even if the pointer moves over UI. " +
                 "Off by default so it never silently changes existing input behavior.")]
        [SerializeField] private bool blockWhenPointerOverUI;

        [Header("Shortcuts (used when no Settings asset is assigned)")]
        [SerializeField] private KeyCode positionShortcut = KeyCode.W;
        [SerializeField] private KeyCode rotationShortcut = KeyCode.E;
        [SerializeField] private KeyCode scaleShortcut = KeyCode.R;
        [SerializeField] private KeyCode allShortcut = KeyCode.A;
        [SerializeField] private KeyCode spaceShortcut = KeyCode.X;
        [SerializeField] private KeyCode pivotShortcut = KeyCode.Z;

        /// <summary>
        /// Gets or sets the settings asset used by this manager.
        /// When set, the settings asset values override the serialized field values.
        /// </summary>
        public TransformHandleSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        /// <summary>
        /// Opt-in. When true, a handle interaction will not start while the pointer is over a uGUI
        /// element (requires the uGUI package and an EventSystem in the scene). An in-progress
        /// interaction is never interrupted. Defaults to false so enabling the package never
        /// silently changes input behavior. Has no effect when uGUI is not installed.
        /// </summary>
        public bool BlockWhenPointerOverUI
        {
            get => blockWhenPointerOverUI;
            set => blockWhenPointerOverUI = value;
        }

        // Properties that check settings first, then fall back to serialized fields
        private bool ShortcutsEnabled => settings == null || settings.EnableShortcuts;
        private KeyCode PositionKey => settings != null ? settings.PositionKey : positionShortcut;
        private KeyCode RotationKey => settings != null ? settings.RotationKey : rotationShortcut;
        private KeyCode ScaleKey => settings != null ? settings.ScaleKey : scaleShortcut;
        private KeyCode AllKey => settings != null ? settings.AllKey : allShortcut;
        private KeyCode SpaceToggleKey => settings != null ? settings.SpaceToggleKey : spaceShortcut;
        private KeyCode PivotToggleKey => settings != null ? settings.PivotToggleKey : pivotShortcut;
        private Color HighlightColorValue => settings != null ? settings.HighlightColor : highlightColor;

        private RaycastHit[] _rayHits;
        private static readonly RaycastHitDistanceComparer _rayHitComparer = new RaycastHitDistanceComparer();

        private Vector3 _previousMousePosition;
        private Vector3 _handleHitPoint;

        private HandleBase _previousAxis;
        private HandleBase _draggingHandle;
        private HandleBase _hoveredHandle;

        private Ghost _interactedGhost;
        private Handle _interactedHandle;

        private HashSet<Transform> _transformHashSet;
        private Dictionary<Handle, TransformGroup> _handleGroupMap;
        private Dictionary<Ghost, TransformGroup> _ghostGroupMap;

        private bool _handleActive;
        private bool _isInitialized;
        private int _handleLayer = -1;
        private bool _cameraMissingLogged;
        private bool _isCleaningUp;
        private bool _isTornDown;
        private bool _isFinalTeardownRequested;
        private bool _creationRejectedLogged;

        private readonly List<Handle> _reconcileHandles = new List<Handle>();
        private readonly List<Transform> _reconcileRemovedTargets = new List<Transform>();
        private readonly List<Handle> _reconcileEmptyHandles = new List<Handle>();
        private readonly HashSet<Handle> _pendingLossHandles = new HashSet<Handle>();
        private bool _isReconciling;

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            InitializeManager();

            if (_mainCamera == null) _mainCamera = Camera.main;
            RefreshHandleCameras();
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;

            // A disabled or destroyed manager must not keep an interaction alive: the handle
            // would otherwise lose its end event and retain drag state.
            try
            {
                CancelInteraction(null);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        protected override void OnDestroy()
        {
            _isFinalTeardownRequested = true;

            try
            {
                // A reentrant OnDestroy during an active bulk cleanup must not start a second
                // pass. The active pass owns the maps and sets _isTornDown from the pending flag.
                if (!_isCleaningUp && !_isTornDown)
                {
                    if (_handleGroupMap != null)
                    {
                        CleanupHandles(isFinal: true);
                    }
                    else
                    {
                        _isTornDown = true;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                // Always release the singleton reference, even if cleanup or a user callback threw.
                base.OnDestroy();
            }
        }

        protected virtual void OnActiveSceneChanged(Scene arg0, Scene scene)
        {
            // Ownership survives active-scene changes (Option A). Handles created in an unloaded
            // scene unregister through their own destruction; handles still alive keep their owner.
            try
            {
                CancelInteraction(null);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }

            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera != null) RefreshHandleCameras();
            }
        }

        private void CancelInteractionSafe(Handle owner)
        {
            try
            {
                CancelInteraction(owner);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private void RefreshHandleCameras()
        {
            if (_handleGroupMap == null) return;

            foreach (var handle in _handleGroupMap.Keys)
            {
                if (handle == null) continue;
                handle.RefreshCamera(_mainCamera);
            }
        }

        internal bool TryRestoreHandle(Handle handle)
        {
            if (_handleGroupMap == null || handle == null || !handle.isActiveAndEnabled) return false;
            if (!_handleGroupMap.TryGetValue(handle, out var group)) return false;
            if (!group.HasLiveTargets() || group.GroupGhost == null) return false;

            // Event-free restore through the existing ghost; Enable reinitializes axes.
            handle.Enable(group.GroupGhost.transform);
            return true;
        }

        private bool EnsureCameraForFrame()
        {
            if (_mainCamera == null)
            {
                var fallback = Camera.main;
                if (fallback == null)
                {
                    if (_draggingHandle != null) CancelInteractionSafe(null);
                    return false;
                }

                // A resolved fallback is an identity change. Assign it before cancelling so a
                // callback's later MainCamera assignment (including null) wins through the same
                // setter; never overwrite the callback's last write here.
                MainCamera = fallback;

                if (this == null || _isTornDown || _handleGroupMap == null) return false;
                return false;
            }

            if (_draggingHandle != null && _interactedHandle != null &&
                _interactedHandle.HandleCamera != _mainCamera)
            {
                CancelInteractionSafe(_interactedHandle);
                if (this == null || _isTornDown || _handleGroupMap == null) return false;

                RefreshHandleCameras();
                return false;
            }

            return true;
        }

        private void InitializeManager()
        {
            if (_isInitialized) return;

            _mainCamera = _mainCamera == null ? Camera.main : _mainCamera;

            _handleGroupMap = new Dictionary<Handle, TransformGroup>();
            _ghostGroupMap = new Dictionary<Ghost, TransformGroup>();
            _pendingLossHandles.Clear();
            _transformHashSet = new HashSet<Transform>();

            InitializeHandleLayer();

            _rayHits = new RaycastHit[MaxRaycastHits];

            _isInitialized = true;
        }

        private void InitializeHandleLayer()
        {
            if (string.IsNullOrEmpty(handleLayerName))
            {
                _handleLayer = 0; // Default layer
                return;
            }

            _handleLayer = LayerMask.NameToLayer(handleLayerName);

            if (_handleLayer == -1)
            {
                Debug.LogWarning($"TransformHandles: Layer '{handleLayerName}' not found. Using default layer. " +
                                 $"Please add a layer named '{handleLayerName}' in Edit > Project Settings > Tags and Layers, " +
                                 $"or change the 'Handle Layer Name' in the TransformHandleManager.");
                _handleLayer = 0;
            }
            else
            {
                // Update the layerMask to include the handle layer
                layerMask |= (1 << _handleLayer);
            }
        }

        private void SetLayerRecursively(GameObject obj, int layer)
        {
            if (obj == null) return;

            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        /// <summary>
        /// Creates a new handle for the specified target transform.
        /// </summary>
        /// <param name="target">The transform to create a handle for.</param>
        /// <returns>The created handle, or null if the target already has a handle.</returns>
        public Handle CreateHandle(Transform target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (RejectCreationDuringTeardown()) return null;

            if (_transformHashSet.Contains(target))
            {
                Debug.LogWarning($"TransformHandles: {target} already has a handle.");
                return null;
            }

            var ghost = CreateGhost();
            if (ghost == null)
            {
                Debug.LogError("Failed to create ghost");
                return null;
            }

            ghost.Initialize();

            var transformHandle = Instantiate(transformHandlePrefab).GetComponent<Handle>();
            SetLayerRecursively(transformHandle.gameObject, _handleLayer);
            transformHandle.Enable(ghost.transform);

            var group = new TransformGroup(ghost, transformHandle);

            _handleGroupMap.Add(transformHandle, group);
            _ghostGroupMap.Add(ghost, group);

            var success = AddTarget(target, transformHandle);
            if (!success)
            {
                // Tear down maps + ghost before destroying the GameObject so no orphan state leaks.
                RemoveHandle(transformHandle);
                DestroyHandle(transformHandle);
                return null;
            }

            if (transformHandle == null || !_handleGroupMap.TryGetValue(transformHandle, out var currentGroup) ||
                !ReferenceEquals(currentGroup, group)) return null;
            transformHandle.SynchronizeVisualTransform();
            // A custom transformation override can detach or destroy the handle during sync.
            if (this == null || transformHandle == null ||
                !_handleGroupMap.TryGetValue(transformHandle, out currentGroup) ||
                !ReferenceEquals(currentGroup, group)) return null;
            _handleActive = true;

            return transformHandle;
        }

        /// <summary>
        /// Creates a handle for multiple transforms.
        /// </summary>
        /// <param name="targets">The list of transforms to create a handle for.</param>
        /// <returns>The created handle, or null if any target already has a handle.</returns>
        public Handle CreateHandleFromList(List<Transform> targets)
        {
            if (targets == null) throw new ArgumentNullException(nameof(targets));
            if (targets.Count == 0) throw new ArgumentException("Target list is empty.", nameof(targets));
            if (RejectCreationDuringTeardown()) return null;

            var validTargets = new List<Transform>();
            foreach (var target in targets)
            {
                if (ReferenceEquals(target, null))
                {
                    Debug.LogWarning("TransformHandles: skipping a null target in the list.");
                    continue;
                }

                if (target == null)
                {
                    Debug.LogWarning("TransformHandles: skipping a destroyed target in the list.");
                    continue;
                }

                validTargets.Add(target);
            }

            if (validTargets.Count == 0)
            {
                Debug.LogWarning("TransformHandles: target list has no live transforms.");
                return null;
            }

            var ghost = CreateGhost();
            if (ghost == null)
            {
                Debug.LogError("Failed to create ghost");
                return null;
            }

            ghost.Initialize();

            var transformHandle = Instantiate(transformHandlePrefab).GetComponent<Handle>();
            SetLayerRecursively(transformHandle.gameObject, _handleLayer);
            transformHandle.Enable(ghost.transform);

            var group = new TransformGroup(ghost, transformHandle);
            _handleGroupMap.Add(transformHandle, group);
            _ghostGroupMap.Add(ghost, group);

            foreach (var target in validTargets)
            {
                if (_transformHashSet.Contains(target))
                {
                    Debug.LogWarning($"TransformHandles: {target} already has a handle.");
                    // Tear down maps + ghost (and roll back already-added targets) before
                    // destroying the GameObject so no orphan state leaks.
                    RemoveHandle(transformHandle);
                    DestroyHandle(transformHandle);
                    return null;
                }
                AddTarget(target, transformHandle);
            }

            if (group.Transforms.Count == 0)
            {
                // Every element normalized away or was rejected; do not return an empty handle.
                RemoveHandle(transformHandle);
                DestroyHandle(transformHandle);
                return null;
            }

            if (transformHandle == null || !_handleGroupMap.TryGetValue(transformHandle, out var currentGroup) ||
                !ReferenceEquals(currentGroup, group)) return null;
            transformHandle.SynchronizeVisualTransform();
            // A custom transformation override can detach or destroy the handle during sync.
            if (this == null || transformHandle == null ||
                !_handleGroupMap.TryGetValue(transformHandle, out currentGroup) ||
                !ReferenceEquals(currentGroup, group)) return null;
            _handleActive = true;

            return transformHandle;
        }

        private Ghost CreateGhost()
        {
            Ghost ghost;

            if (ghostPrefab == null)
            {
                var ghostObject = new GameObject("Ghost");
                ghost = ghostObject.AddComponent<Ghost>();
            }
            else
            {
                ghost = Instantiate(ghostPrefab).GetComponent<Ghost>();
            }

            return ghost;
        }

        /// <summary>
        /// Removes a handle from the manager.
        /// </summary>
        /// <param name="handle">The handle to remove.</param>
        public void RemoveHandle(Handle handle)
        {
            RemoveHandleCore(handle, out _);
        }

        private void RemoveHandleCore(Handle handle, out bool detachedByCallback)
        {
            detachedByCallback = false;

            if (ReferenceEquals(handle, null)) throw new ArgumentNullException(nameof(handle));

            // A destroyed handle (for example after its scene unloaded) has already unregistered
            // itself through OnDestroy; touching gameObject here would throw MissingReferenceException.
            if (handle == null) return;

            if (_handleGroupMap == null) return;

            if (!_handleGroupMap.TryGetValue(handle, out var group))
            {
                // Already unmanaged: a create-path bail removes the handle, then DestroyImmediate
                // re-fires Handle.OnDestroy -> RemoveHandle. Idempotent no-op, not an error.
                return;
            }

            // Deliver the end event before any state is cleared or objects are destroyed. Isolated so
            // a throwing consumer callback cannot abort removal or leave a live ghost behind.
            try
            {
                CancelInteraction(handle);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }

            // The end callback may have detached or destroyed this handle already. In that case the
            // callback owns the lifecycle; do not remove it a second time or fire a duplicate event.
            if (_handleGroupMap == null || !_handleGroupMap.ContainsKey(handle))
            {
                detachedByCallback = true;
                return;
            }

            _handleGroupMap.Remove(handle);
            _pendingLossHandles.Remove(handle);
            handle.Disable();

            foreach (var target in group.Transforms)
            {
                _transformHashSet.Remove(target);
            }

            var groupGhost = group.GroupGhost;
            if (!ReferenceEquals(groupGhost, null))
            {
                // Remove even a destroyed ghost reference: a scene unload may have destroyed the
                // ghost before the handle's OnDestroy ran, and a stale key would keep the map dirty.
                _ghostGroupMap.Remove(groupGhost);
                if (groupGhost != null) groupGhost.Terminate();
            }

            if (_handleGroupMap.Count == 0) _handleActive = false;
        }

        private static void DestroyHandle(Handle handle)
        {
            DestroyImmediate(handle.gameObject);
        }

        /// <summary>
        /// Destroys all handles managed by this manager.
        /// </summary>
        public void DestroyAllHandles()
        {
            // Nested calls from a destruction/end callback must not re-enter the snapshot/clear
            // section while the outer pass is still using it.
            if (_isCleaningUp || _isTornDown) return;
            if (_handleGroupMap == null) return;

            CleanupHandles(isFinal: false);
        }

        /// <summary>
        /// Rejects creation while a cleanup runs (scoped) or after final teardown (permanent).
        /// </summary>
        private bool RejectCreationDuringTeardown()
        {
            if (!_isCleaningUp && !_isTornDown) return false;

            if (!_creationRejectedLogged)
            {
                _creationRejectedLogged = true;
                Debug.LogWarning(_isTornDown
                    ? "TransformHandles: this manager has been torn down; no new handles can be created."
                    : "TransformHandles: cannot create handles while handles are being destroyed.");
            }

            return true;
        }

        private void CleanupHandles(bool isFinal)
        {
            _isCleaningUp = true;

            try
            {
                // Step 1: cancel in isolation. A throwing end callback must not skip the destruction
                // of the remaining owned objects.
                try
                {
                    CancelInteraction(null);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }

                // Step 2: snapshot before any callback mutates the collections.
                var handles = new List<Handle>(_handleGroupMap.Keys);
                var ghosts = new List<Ghost>(_ghostGroupMap.Keys);

                // Step 3: clear ownership first. Reentrant RemoveHandle/GetTargets calls from a
                // destruction callback are then safe no-ops instead of touching half-torn state.
                _handleGroupMap.Clear();
                _ghostGroupMap.Clear();
                _transformHashSet.Clear();
                _pendingLossHandles.Clear();
                _handleActive = false;
                _draggingHandle = null;
                _hoveredHandle = null;
                _previousAxis = null;
                _interactedHandle = null;
                _interactedGhost = null;

                // Step 4: destroy handles one by one. An exception (usually a consumer callback in
                // OnDestroy) is logged and contained; the map state above does not depend on it.
                foreach (var handle in handles)
                {
                    // A direct destruction callback may destroy this manager while its handle's
                    // OnDestroy is still running. That handle finishes its own destruction.
                    if (ReferenceEquals(handle, null) || handle == null || handle.IsBeingDestroyed) continue;

                    try
                    {
                        DestroyHandle(handle);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception, this);

                        // Best effort only: Unity may already have completed the destruction. T17
                        // accepts the observable outcome rather than assuming either behavior.
                        try
                        {
                            if (handle != null) Destroy(handle.gameObject);
                        }
                        catch (Exception fallbackException)
                        {
                            Debug.LogException(fallbackException, this);
                        }
                    }
                }

                // Step 5: terminate ghosts even though the maps are already clear, so no helper
                // object is left behind when a handle's OnDestroy callback threw or was skipped.
                foreach (var ghost in ghosts)
                {
                    if (ReferenceEquals(ghost, null) || ghost == null) continue;

                    try
                    {
                        ghost.Terminate();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception, this);

                        try
                        {
                            if (ghost != null) Destroy(ghost.gameObject);
                        }
                        catch (Exception fallbackException)
                        {
                            Debug.LogException(fallbackException, this);
                        }
                    }
                }
            }
            finally
            {
                _isCleaningUp = false;

                // _isTornDown is one-way: a final request that arrived during this pass (for
                // example a callback destroyed the manager) wins, and an outer pass never resets it.
                if (isFinal || _isFinalTeardownRequested) _isTornDown = true;
            }
        }

        private void ReconcileTargets()
        {
            if (_handleGroupMap == null || _isCleaningUp || _isTornDown || _isReconciling) return;

            _isReconciling = true;
            try
            {
                _reconcileHandles.Clear();
                foreach (var handle in _handleGroupMap.Keys) _reconcileHandles.Add(handle);

                _reconcileEmptyHandles.Clear();

                foreach (var handle in _reconcileHandles)
                {
                    if (_isTornDown) break;

                    if (!_handleGroupMap.TryGetValue(handle, out var group))
                    {
                        _pendingLossHandles.Remove(handle);
                        continue;
                    }

                    _reconcileRemovedTargets.Clear();
                    group.PruneDestroyedTargets(_reconcileRemovedTargets);
                    foreach (var removedTarget in _reconcileRemovedTargets)
                        _transformHashSet.Remove(removedTarget);

                    var hadPendingLoss = _pendingLossHandles.Remove(handle);
                    if (_reconcileRemovedTargets.Count == 0 && !hadPendingLoss) continue;

                    try
                    {
                        // Membership is already pruned, so the end callback observes survivors only.
                        CancelInteraction(handle);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception, this);
                    }

                    if (_isTornDown) break;
                    if (!_handleGroupMap.TryGetValue(handle, out group)) continue;

                    if (group.Transforms.Count == 0)
                    {
                        _reconcileEmptyHandles.Add(handle);
                        continue;
                    }

                    if (group.GroupGhost != null)
                    {
                        group.GroupGhost.UpdateGhostTransform(group.GetAveragePosRotScale());
                        SynchronizeGroupVisual(group);
                    }
                }

                foreach (var handle in _reconcileEmptyHandles)
                {
                    if (_isTornDown) break;
                    if (!_handleGroupMap.TryGetValue(handle, out var group) ||
                        group.Transforms.Count != 0) continue;

                    FinalizeEmptyHandle(handle);
                }
            }
            finally
            {
                _isReconciling = false;
                _reconcileHandles.Clear();
                _reconcileRemovedTargets.Clear();
                _reconcileEmptyHandles.Clear();
            }
        }

        private void FinalizeEmptyHandle(Handle handle)
        {
            if (_handleGroupMap == null || !_handleGroupMap.TryGetValue(handle, out var group)) return;
            if (group.Transforms.Count != 0) return;

            // Cancel before unregistering: an end callback may add a target back to this handle.
            try
            {
                CancelInteraction(handle);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }

            if (_isTornDown || handle == null) return;
            if (!_handleGroupMap.TryGetValue(handle, out group)) return;

            if (group.Transforms.Count != 0)
            {
                // Callback rescued the handle. Keep it/ghost alive and clear the queued loss.
                _pendingLossHandles.Remove(handle);
                if (group.GroupGhost != null)
                {
                    group.GroupGhost.UpdateGhostTransform(group.GetAveragePosRotScale());
                    SynchronizeGroupVisual(group);
                }

                return;
            }

            _pendingLossHandles.Remove(handle);

            // Unregister before Unity invokes OnHandleDestroyed. A throwing destroyed callback can
            // therefore not leave a registered handle or a live ghost behind.
            RemoveHandleCore(handle, out var detachedByCallback);
            if (detachedByCallback || handle == null) return;

            try
            {
                DestroyHandle(handle);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);

                // Same best-effort liveness fallback used by bulk cleanup.
                try
                {
                    if (handle != null) Destroy(handle.gameObject);
                }
                catch (Exception fallbackException)
                {
                    Debug.LogException(fallbackException, this);
                }
            }
        }

        /// <summary>
        /// Starts an interaction through the same transition as the input loop.
        /// Internal test seam; validates everything before mutating drag state or firing events.
        /// </summary>
        internal bool BeginInteraction(HandleBase axis, Vector3 hitPoint)
        {
            if (!isActiveAndEnabled) return false;
            if (_draggingHandle != null) return false;
            if (axis == null || !axis.isActiveAndEnabled || !axis.gameObject.activeInHierarchy) return false;

            var handle = axis.ParentHandle;
            if (handle == null || !handle.isActiveAndEnabled || !handle.gameObject.activeInHierarchy)
                return false;
            if (!EnsureCameraForFrame()) return false;
            if (_handleGroupMap == null || !_handleGroupMap.TryGetValue(handle, out var group)) return false;

            // Query-time pruning can have removed the dead membership already; pending loss still
            // prevents starting a drag that would require cleanup before its first step.
            if (_pendingLossHandles.Contains(handle)) return false;

            var removed = new List<Transform>();
            group.PruneDestroyedTargets(removed);
            if (removed.Count > 0)
            {
                foreach (var removedTarget in removed) _transformHashSet.Remove(removedTarget);
                _pendingLossHandles.Add(handle);
                return false;
            }

            if (!group.HasLiveTargets())
            {
                _pendingLossHandles.Add(handle);
                return false;
            }

            _draggingHandle = axis;
            axis.StartInteraction(hitPoint);
            OnInteractionStart();

            // OnInteractionStart/completion callbacks may cancel or detach the handle. The bool
            // contract is true only while the interaction it started is still active.
            return _draggingHandle != null;
        }

        /// <summary>Internal test seam; true while a handle axis is being dragged.</summary>
        internal bool IsInteracting => _draggingHandle != null;

        /// <summary>
        /// Ends the active interaction when it belongs to <paramref name="owner"/>.
        /// A null owner means global cancellation (teardown, disable, scene change).
        /// </summary>
        internal void CancelInteraction(Handle owner)
        {
            var axis = _draggingHandle;
            var interacted = _interactedHandle;

            if (axis == null && interacted == null) return;

            // A destroyed axis cannot report its ParentHandle. Fall back to the handle captured at
            // interaction start so destroying the axis first (scene unload, consumer code) still
            // delivers the handle's end event.
            var dragOwner = axis != null && axis.ParentHandle != null
                ? axis.ParentHandle
                : interacted;

            // ReferenceEquals distinguishes an explicit null owner (global) from a destroyed one.
            if (!ReferenceEquals(owner, null) && !ReferenceEquals(dragOwner, owner)) return;

            EndInteractionForHandle(dragOwner, axis, refreshBounds: false);
        }

        /// <summary>
        /// Single end path for normal mouse-up and cancellation.
        /// The five interaction fields are cleared before any callback runs, so reentrancy is a no-op.
        /// </summary>
        private void EndInteractionForHandle(Handle handle, HandleBase axis, bool refreshBounds)
        {
            if (axis == null && handle == null) return;

            TransformGroup endedGroup = null;
            if (handle != null && _handleGroupMap != null) _handleGroupMap.TryGetValue(handle, out endedGroup);
            try
            {
                var previousAxis = _previousAxis;
                var interactedGhost = _interactedGhost;

                _draggingHandle = null;
                _hoveredHandle = null;
                _previousAxis = null;
                _interactedHandle = null;
                _interactedGhost = null;

                if (previousAxis != null) previousAxis.SetDefaultColor();
                if (axis != null) axis.EndInteraction();

                if (!refreshBounds)
                {
                    // Cancellation bypasses the protected virtual (documented); Handle-level overrides
                    // of InteractionEnd still observe the event.
                    if (handle != null) handle.InteractionEnd();
                    return;
                }

                // Normal mouse-up keeps the historical OnInteractionEnd contract, including subclass
                // overrides and the bounds refresh. Temporary state exists only for that call.
                _interactedHandle = handle;
                _interactedGhost = interactedGhost;
                try
                {
                    OnInteractionEnd();
                }
                finally
                {
                    _interactedHandle = null;
                    _interactedGhost = null;
                }
            }
            finally { endedGroup?.EndInteraction(); }

        }

        /// <summary>
        /// Adds a target transform to an existing handle.
        /// </summary>
        /// <param name="target">The target transform to add.</param>
        /// <param name="handle">The handle to add the target to.</param>
        /// <returns>True if the target was added successfully.</returns>
        public bool AddTarget(Transform target, Handle handle)
        {
            if (handle == null) throw new ArgumentNullException(nameof(handle));

            if (ReferenceEquals(target, null))
            {
                Debug.LogWarning("TransformHandles: cannot add a null target.");
                return false;
            }

            if (target == null)
            {
                // Destroyed native object; it cannot become a target again.
                Debug.LogWarning("TransformHandles: cannot add a destroyed target.");
                return false;
            }

            // Scoped to the cleanup loop only. After final teardown AddTarget keeps its historical
            // throw/no-op behavior for destroyed or unmanaged handles (plan §6).
            if (_isCleaningUp) return false;

            if (_transformHashSet.Contains(target))
            {
                Debug.LogWarning($"TransformHandles: {target} already has a handle.");
                return false;
            }

            if (!_handleGroupMap.TryGetValue(handle, out var group))
                throw new InvalidOperationException("Handle is not managed by this TransformHandleManager.");

            // Local cleanup only: no global reconcile and no callback before validation.
            var removed = new List<Transform>();
            group.PruneDestroyedTargets(removed);
            foreach (var removedTarget in removed) _transformHashSet.Remove(removedTarget);

            var replaced = new List<Transform>();
            var targetAdded = group.AddTransform(target, replaced);
            if (!targetAdded)
            {
                // If this call pruned a dead member but then rejected the new target, retain the
                // pending work so Update cannot forget to cancel/recenter/destroy.
                if (removed.Count > 0) _pendingLossHandles.Add(handle);
                return false;
            }

            foreach (var replacedTarget in replaced) _transformHashSet.Remove(replacedTarget);
            _transformHashSet.Add(target);

            if (removed.Count > 0 || replaced.Count > 0 || (handle.UniformGroupScale && handle.Type == HandleType.Scale))
            {
                try
                {
                    // Membership is already final, so the end callback observes the new group.
                    CancelInteraction(handle);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }

                if (!_handleGroupMap.TryGetValue(handle, out group))
                {
                    // A callback detached/destroyed the handle after the add. Keep the successful
                    // return value; the callback owns the resulting lifecycle.
                    return true;
                }
            }

            if (group.GroupGhost != null)
            {
                var averagePosRotScale = group.GetAveragePosRotScale();
                group.GroupGhost.UpdateGhostTransform(averagePosRotScale);
                SynchronizeGroupVisual(group);
            }

            return true;
        }

        /// <summary>
        /// Removes a target transform from a handle.
        /// </summary>
        /// <param name="target">The target transform to remove.</param>
        /// <param name="handle">The handle to remove the target from.</param>
        public void RemoveTarget(Transform target, Handle handle)
        {
            if (handle == null) throw new ArgumentNullException(nameof(handle));

            if (!_handleGroupMap.TryGetValue(handle, out var group))
                throw new InvalidOperationException("Handle is not managed by this TransformHandleManager.");

            if (ReferenceEquals(target, null))
            {
                Debug.LogWarning("TransformHandles: cannot remove a null target.");
                return;
            }

            if (target != null)
            {
                if (!_transformHashSet.Contains(target))
                {
                    Debug.LogWarning($"TransformHandles: {target} doesn't have a handle.");
                    return;
                }

                if (!group.Transforms.Contains(target))
                {
                    throw new InvalidOperationException(
                        "Target is not owned by the supplied TransformHandle.");
                }
            }
            else if (!group.Transforms.Contains(target))
            {
                // Already destroyed/pruned; clean a stale index key if one remains.
                _transformHashSet.Remove(target);

                // Query-first order: GetTargets may already have pruned the last target and
                // recorded pending loss. An explicit RemoveTarget on that destroyed reference must
                // still perform the last-target cleanup now, not wait for another Update.
                if (group.Transforms.Count == 0)
                {
                    _pendingLossHandles.Remove(handle);
                    FinalizeEmptyHandle(handle);
                }

                return;
            }

            // Validate before mutation, then prune this group only. Operation-local buffer so a
            // callback from this call cannot share Update pass storage.
            var removed = new List<Transform>();
            group.PruneDestroyedTargets(removed);
            foreach (var removedTarget in removed) _transformHashSet.Remove(removedTarget);

            if (target != null && group.Transforms.Contains(target))
            {
                group.RemoveTransform(target);
                _transformHashSet.Remove(target);
                removed.Add(target);
            }

            if (removed.Count == 0) return;

            try
            {
                CancelInteraction(handle);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }

            if (!_handleGroupMap.TryGetValue(handle, out group))
            {
                // A callback detached/destroyed the handle; it owns the resulting lifecycle.
                return;
            }

            if (group.Transforms.Count == 0)
            {
                _pendingLossHandles.Remove(handle);
                FinalizeEmptyHandle(handle);
                return;
            }

            if (group.GroupGhost != null)
            {
                group.GroupGhost.UpdateGhostTransform(group.GetAveragePosRotScale());
                SynchronizeGroupVisual(group);
            }
        }

        /// <summary>
        /// Returns the transforms manipulated by <paramref name="handle"/> — the actual selected
        /// objects, not the pivot. The result is a read-only, live view of the handle's current
        /// group (do not cache it across add/remove); empty if the handle is not managed here.
        /// </summary>
        /// <param name="handle">The handle to query.</param>
        public IReadOnlyCollection<Transform> GetTargets(Handle handle)
        {
            if (ReferenceEquals(handle, null)) throw new ArgumentNullException(nameof(handle));

            // A destroyed handle has already unregistered; querying it is an empty, safe result.
            if (handle == null) return System.Array.Empty<Transform>();

            if (_handleGroupMap != null && _handleGroupMap.TryGetValue(handle, out var group))
            {
                if (group.HasDestroyedTargets())
                {
                    // Query-time pruning is membership-only. No callback, recenter or destruction
                    // runs here; Update finalizes the pending loss at the next enabled frame.
                    var removed = new List<Transform>();
                    group.PruneDestroyedTargets(removed);
                    foreach (var removedTarget in removed) _transformHashSet.Remove(removedTarget);
                    _pendingLossHandles.Add(handle);
                }

                return group.Transforms;
            }

            return System.Array.Empty<Transform>();
        }

        protected virtual void Update()
        {
            if (!_handleActive) return;

            ReconcileTargets();
            if (!_handleActive) return;

            if (!EnsureCameraForFrame()) return;

            // Hover detection is unused while dragging (HandleOverEffect is gated off and a drag
            // can't start mid-hold), so skip the per-frame raycast + GetComponentInParent during a drag.
            if (_draggingHandle == null)
            {
                _hoveredHandle = null;
                _handleHitPoint = Vector3.zero;

                // Leaving the hovered handle null while the pointer is over UI both clears the
                // highlight and prevents MouseInput from starting a drag (it requires a hovered
                // handle). An interaction already in progress is unaffected — this branch only
                // runs when not dragging.
                if (!IsPointerOverUI())
                {
                    GetHandle(ref _hoveredHandle, ref _handleHitPoint);
                }

                HandleOverEffect(_hoveredHandle);
            }

            MouseInput();
            KeyboardInput();
        }

        /// <summary>
        /// Whether the pointer is currently over a uGUI element that should suppress starting a
        /// handle interaction. Returns false when <see cref="BlockWhenPointerOverUI"/> is disabled,
        /// no EventSystem is present, or the uGUI package is not installed. Override to plug in a
        /// different UI stack.
        /// </summary>
        protected virtual bool IsPointerOverUI()
        {
            if (!blockWhenPointerOverUI) return false;
#if TH_UGUI
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem == null) return false;

            // The no-arg overload only queries the mouse pointer. The package treats the first
            // active touch as the primary pointer (see InputWrapper), so on touch devices also
            // test that finger's pointer id — otherwise a tap over UI bypasses the guard.
            if (eventSystem.IsPointerOverGameObject()) return true;
            return HasActiveTouch && eventSystem.IsPointerOverGameObject(PrimaryTouchPointerId);
#else
            return false;
#endif
        }

        protected virtual void GetHandle(ref HandleBase handle, ref Vector3 hitPoint)
        {
            // Unity's overloaded == reports a destroyed camera as null, so this proactive check
            // covers the MissingReferenceException case the old per-frame try/catch handled.
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null)
                {
                    // No camera this frame (scene load, camera swap, etc.) is a recoverable
                    // transient. Skip raycasting and survive instead of self-destructing the
                    // DontDestroyOnLoad singleton. Assign 'MainCamera' to recover explicitly.
                    if (!_cameraMissingLogged)
                    {
                        Debug.LogWarning("TransformHandles: No main camera found; handle raycasting " +
                                         "is paused until a camera is available. Assign 'MainCamera' to recover.");
                        _cameraMissingLogged = true;
                    }
                    return;
                }
            }

            _cameraMissingLogged = false;

            var ray = _mainCamera.ScreenPointToRay(MousePosition);
            var size = Physics.RaycastNonAlloc(ray, _rayHits, RaycastMaxDistance, layerMask);

            if (size == 0)
            {
                return;
            }

            if (size > 1) Array.Sort(_rayHits, 0, size, _rayHitComparer);

            for (var i = 0; i < size; i++)
            {
                var hit = _rayHits[i];
                var hitCollider = hit.collider;
                if (hitCollider == null) continue;
                handle = hitCollider.gameObject.GetComponentInParent<HandleBase>();

                if (handle == null) continue;
                hitPoint = hit.point;
                return;
            }
        }

        protected virtual void HandleOverEffect(HandleBase handleBase)
        {
            if (_draggingHandle == null && _previousAxis != null && _previousAxis != handleBase)
            {
                _previousAxis.SetDefaultColor();
            }

            if (handleBase != null && _draggingHandle == null)
            {
                if (settings != null && settings.EmphasizeHandleColorOnHover)
                    handleBase.SetHighlightedColor();
                else if (settings != null && settings.PreserveHandleColorOnHover)
                    handleBase.SetDefaultColor();
                else
                    handleBase.SetColor(HighlightColorValue);
            }

            _previousAxis = handleBase;
        }

        protected virtual void MouseInput()
        {
            if (GetMouseButton(0) && _draggingHandle != null)
            {
                _draggingHandle.Interact(_previousMousePosition);

                // An interaction callback may have removed/cancelled the drag. Do not dereference
                // the cleared interaction fields in that case.
                if (_draggingHandle != null) OnInteraction();
            }

            if (GetMouseButtonDown(0) && _hoveredHandle != null)
            {
                BeginInteraction(_hoveredHandle, _handleHitPoint);
            }

            if (GetMouseButtonUp(0) && _draggingHandle != null)
            {
                var axis = _draggingHandle;
                var handle = _interactedHandle;
                EndInteractionForHandle(handle, axis, refreshBounds: true);
            }

            _previousMousePosition = MousePosition;
        }

        protected virtual void KeyboardInput()
        {
            if (!ShortcutsEnabled) return;

            if (GetKeyDown(PositionKey))
            {
                foreach (var handle in _handleGroupMap.Keys)
                {
                    ChangeHandleType(handle, HandleType.Position);
                }
            }

            if (GetKeyDown(RotationKey))
            {
                foreach (var handle in _handleGroupMap.Keys)
                {
                    ChangeHandleType(handle, HandleType.Rotation);
                }
            }

            if (GetKeyDown(ScaleKey))
            {
                foreach (var handle in _handleGroupMap.Keys)
                {
                    ChangeHandleType(handle, HandleType.Scale);
                }
            }

            if (GetKeyDown(AllKey))
            {
                foreach (var handle in _handleGroupMap.Keys)
                {
                    ChangeHandleType(handle, HandleType.All);
                }
            }

            if (GetKeyDown(SpaceToggleKey))
            {
                foreach (var handle in _handleGroupMap.Keys)
                {
                    ChangeHandleSpace(handle, handle.Space == Space.World ? Space.Self : Space.World);
                }
            }

            if (GetKeyDown(PivotToggleKey))
            {
                foreach (var group in _handleGroupMap.Values)
                {
                    ChangeHandlePivot(group, !group.IsOriginOnCenter);
                }
            }
        }

        protected virtual void OnInteractionStart()
        {
            if (_draggingHandle == null) return;

            var handle = _draggingHandle.ParentHandle;
            if (handle == null || _handleGroupMap == null ||
                !_handleGroupMap.TryGetValue(handle, out var group))
            {
                _draggingHandle = null;
                _interactedHandle = null;
                _interactedGhost = null;
                return;
            }

            _interactedHandle = handle;
            _interactedGhost = group.GroupGhost;
            _interactedGhost.OnInteractionStart();
            group.BeginInteraction();

            _interactedHandle.InteractionStart();
        }

        protected virtual void OnInteraction()
        {
            if (_interactedGhost == null || _interactedHandle == null) return;

            _interactedGhost.OnInteraction(_interactedHandle.Type);

            // A ghost/group callback can cancel the interaction or destroy the handle.
            if (_interactedHandle != null) _interactedHandle.InteractionStay();
        }

        protected virtual void OnInteractionEnd()
        {
            if (_interactedHandle == null) return;

            if (_handleGroupMap != null && _handleGroupMap.TryGetValue(_interactedHandle, out var group))
            {
                var removed = new List<Transform>();
                group.PruneDestroyedTargets(removed);
                if (removed.Count > 0)
                {
                    foreach (var removedTarget in removed) _transformHashSet.Remove(removedTarget);
                    _pendingLossHandles.Add(_interactedHandle);
                }

                group.UpdateBounds();
            }

            if (_interactedHandle != null) _interactedHandle.InteractionEnd();
        }

        /// <summary>
        /// Changes the type of a handle.
        /// </summary>
        /// <param name="handle">The handle to modify.</param>
        /// <param name="type">The new handle type.</param>
        public static void ChangeHandleType(Handle handle, HandleType type)
        {
            if (handle == null) throw new ArgumentNullException(nameof(handle));
            handle.Type = type;
        }

        /// <summary>
        /// Changes the coordinate space of a handle.
        /// </summary>
        /// <param name="handle">The handle to modify.</param>
        /// <param name="space">The new coordinate space.</param>
        public void ChangeHandleSpace(Handle handle, Space space)
        {
            if (handle == null) throw new ArgumentNullException(nameof(handle));
            handle.Space = space;

            var group = _handleGroupMap[handle];
            group.GroupGhost.UpdateGhostTransform(group.GetAveragePosRotScale());
            SynchronizeGroupVisual(group);
        }

        /// <summary>
        /// Changes the pivot mode of a transform group.
        /// </summary>
        /// <param name="group">The transform group to modify.</param>
        /// <param name="originToCenter">Whether to set the origin to the center of bounds.</param>
        public void ChangeHandlePivot(TransformGroup group, bool originToCenter)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));
            group.IsOriginOnCenter = originToCenter;
            group.GroupGhost.UpdateGhostTransform(group.GetAveragePosRotScale());
            SynchronizeGroupVisual(group);
        }

        private void SynchronizeGroupVisual(TransformGroup group)
        {
            if (group == null) return;
            var handle = group.GroupHandle;
            if (handle != null && _handleGroupMap.TryGetValue(handle, out var current) && ReferenceEquals(current, group))
            {
                handle.RefreshGroupScaleControls();
                handle.SynchronizeVisualTransform();
            }
        }

        /// <summary>
        /// Updates the position of all transforms in a group.
        /// </summary>
        public void UpdateGroupPosition(Ghost ghost, Vector3 positionChange)
        {
            if (ghost == null) return;
            if (_ghostGroupMap != null && _ghostGroupMap.TryGetValue(ghost, out var group))
            {
                group.UpdatePositions(positionChange);
            }
        }

        /// <summary>
        /// Updates the rotation of all transforms in a group.
        /// </summary>
        public void UpdateGroupRotation(Ghost ghost, Quaternion rotationChange)
        {
            if (ghost == null) return;
            if (_ghostGroupMap != null && _ghostGroupMap.TryGetValue(ghost, out var group))
            {
                group.UpdateRotations(rotationChange);
            }
        }

        /// <summary>
        /// Updates the scale of all transforms in a group.
        /// </summary>
        public void UpdateGroupScaleUpdate(Ghost ghost, Vector3 scaleChange)
        {
            if (ghost == null) return;
            if (_ghostGroupMap != null && _ghostGroupMap.TryGetValue(ghost, out var group))
            {
                group.UpdateScales(scaleChange);
            }
        }
    }

    internal class RaycastHitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
    {
        public int Compare(RaycastHit x, RaycastHit y)
        {
            return x.distance.CompareTo(y.distance);
        }
    }
}
