using System;
using System.Collections.Generic;
using TransformHandles.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
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

        /// <summary>The main camera used for raycasting.</summary>
        public Camera mainCamera;

        [Header("Prefabs")]
        [SerializeField] private GameObject transformHandlePrefab;
        [SerializeField] private GameObject ghostPrefab;

        [Header("Settings")]
        [Tooltip("Optional settings asset. If not assigned, uses the values below.")]
        [SerializeField] private TransformHandleSettings settings;
        [SerializeField] private LayerMask layerMask;
        [SerializeField] private string handleLayerName = "TransformHandle";
        [SerializeField] private Color highlightColor = Color.white;

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

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            InitializeManager();
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        protected virtual void OnActiveSceneChanged(Scene arg0, Scene scene)
        {
            _isInitialized = false;
            InitializeManager();
        }

        private void InitializeManager()
        {
            if (_isInitialized) return;

            mainCamera = mainCamera == null ? Camera.main : mainCamera;

            _handleGroupMap = new Dictionary<Handle, TransformGroup>();
            _ghostGroupMap = new Dictionary<Ghost, TransformGroup>();
            _transformHashSet = new HashSet<Transform>();

            InitializeHandleLayer();

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
            if (target == null)
            {
                Debug.LogError("Target transform is null");
                return null;
            }

            if (_transformHashSet.Contains(target))
            {
                Debug.LogWarning($"{target} already has a handle.");
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
                DestroyHandle(transformHandle);
                return null;
            }

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
            if (targets == null || targets.Count == 0)
            {
                Debug.LogWarning("List is empty or null.");
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

            foreach (var target in targets)
            {
                if (_transformHashSet.Contains(target))
                {
                    Debug.LogWarning($"{target} already has a handle.");
                    DestroyHandle(transformHandle);
                    return null;
                }
                AddTarget(target, transformHandle);
            }

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
            if (handle == null)
            {
                Debug.LogError("Handle is already null");
                return;
            }

            if (_handleGroupMap == null) return;

            if (!_handleGroupMap.TryGetValue(handle, out var group))
            {
                Debug.LogError("Handle not found in group map");
                return;
            }

            _handleGroupMap.Remove(handle);
            handle.Disable();

            var groupGhost = group.GroupGhost;
            if (groupGhost != null)
            {
                _ghostGroupMap.Remove(groupGhost);
                group.GroupGhost.Terminate();
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
            foreach (var handle in _handleGroupMap.Keys)
            {
                DestroyHandle(handle);
            }
        }

        /// <summary>
        /// Adds a target transform to an existing handle.
        /// </summary>
        /// <param name="target">The target transform to add.</param>
        /// <param name="handle">The handle to add the target to.</param>
        /// <returns>True if the target was added successfully.</returns>
        public bool AddTarget(Transform target, Handle handle)
        {
            if (_transformHashSet.Contains(target))
            {
                Debug.LogWarning($"{target} already has a handle.");
                return false;
            }

            if (handle == null)
            {
                Debug.LogError("Handle is null");
                return false;
            }

            if (!_handleGroupMap.TryGetValue(handle, out var group))
            {
                Debug.LogError("Handle not found in group map");
                return false;
            }

            var targetAdded = group.AddTransform(target);
            if (!targetAdded)
            {
                Debug.LogWarning($"{target} is relative to the selected ones.");
                return false;
            }

            var averagePosRotScale = group.GetAveragePosRotScale();
            group.GroupGhost.UpdateGhostTransform(averagePosRotScale);

            _transformHashSet.Add(target);

            return true;
        }

        /// <summary>
        /// Removes a target transform from a handle.
        /// </summary>
        /// <param name="target">The target transform to remove.</param>
        /// <param name="handle">The handle to remove the target from.</param>
        public void RemoveTarget(Transform target, Handle handle)
        {
            if (!_transformHashSet.Contains(target))
            {
                Debug.LogWarning($"{target} doesn't have a handle.");
                return;
            }

            if (handle == null)
            {
                Debug.LogError("Handle is null");
                return;
            }

            _transformHashSet.Remove(target);

            if (!_handleGroupMap.TryGetValue(handle, out var group))
            {
                Debug.LogError("Handle not found in group map");
                return;
            }

            var groupElementsRemoved = group.RemoveTransform(target);
            if (groupElementsRemoved)
            {
                DestroyHandle(handle);
                return;
            }

            var averagePosRotScale = group.GetAveragePosRotScale();
            group.GroupGhost.UpdateGhostTransform(averagePosRotScale);
        }

        protected virtual void Update()
        {
            if (!_handleActive) return;

            _hoveredHandle = null;
            _handleHitPoint = Vector3.zero;

            GetHandle(ref _hoveredHandle, ref _handleHitPoint);

            HandleOverEffect(_hoveredHandle);

            MouseInput();
            KeyboardInput();
        }

        protected virtual void GetHandle(ref HandleBase handle, ref Vector3 hitPoint)
        {
            _rayHits = new RaycastHit[MaxRaycastHits];

            var size = 0;
            try
            {
                var ray = mainCamera.ScreenPointToRay(MousePosition);
                size = Physics.RaycastNonAlloc(ray, _rayHits, RaycastMaxDistance, layerMask);
            }
            catch (MissingReferenceException)
            {
                mainCamera = Camera.main;
                Debug.Log("Camera is null, trying to find main camera");

                if (mainCamera == null)
                {
                    Debug.Log("Main camera is null, aborting");
                    Destroy(gameObject);
                    return;
                }
            }

            if (size == 0)
            {
                return;
            }

            Array.Sort(_rayHits, (x, y) => x.distance.CompareTo(y.distance));

            foreach (var hit in _rayHits)
            {
                var hitCollider = hit.collider;
                if (hitCollider == null) continue;
                handle = hit.collider.gameObject.GetComponentInParent<HandleBase>();

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
                handleBase.SetColor(HighlightColorValue);
            }

            _previousAxis = handleBase;
        }

        protected virtual void MouseInput()
        {
            if (GetMouseButton(0) && _draggingHandle != null)
            {
                _draggingHandle.Interact(_previousMousePosition);
                OnInteraction();
            }

            if (GetMouseButtonDown(0) && _hoveredHandle != null)
            {
                _draggingHandle = _hoveredHandle;
                _draggingHandle.StartInteraction(_handleHitPoint);
                OnInteractionStart();
            }

            if (GetMouseButtonUp(0) && _draggingHandle != null)
            {
                _draggingHandle.EndInteraction();
                _draggingHandle = null;
                OnInteractionEnd();
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
                    ChangeHandleSpace(handle, handle.space == Space.World ? Space.Self : Space.World);
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
            _interactedHandle = _draggingHandle.GetComponentInParent<Handle>();
            _interactedGhost = _handleGroupMap[_interactedHandle].GroupGhost;
            _interactedGhost.OnInteractionStart();

            _interactedHandle.InteractionStart();
        }

        protected virtual void OnInteraction()
        {
            _interactedGhost.OnInteraction(_interactedHandle.type);
            _interactedHandle.InteractionStay();
        }

        protected virtual void OnInteractionEnd()
        {
            var group = _handleGroupMap[_interactedHandle];
            group.UpdateBounds();

            _interactedHandle.InteractionEnd();
        }

        /// <summary>
        /// Changes the type of a handle.
        /// </summary>
        /// <param name="handle">The handle to modify.</param>
        /// <param name="type">The new handle type.</param>
        public static void ChangeHandleType(Handle handle, HandleType type)
        {
            if (handle == null)
            {
                Debug.LogError("Handle is null");
                return;
            }
            handle.ChangeHandleType(type);
        }

        /// <summary>
        /// Changes the coordinate space of a handle.
        /// </summary>
        /// <param name="handle">The handle to modify.</param>
        /// <param name="space">The new coordinate space.</param>
        public void ChangeHandleSpace(Handle handle, Space space)
        {
            if (handle == null)
            {
                Debug.LogError("Handle is null");
                return;
            }
            handle.ChangeHandleSpace(space);

            var group = _handleGroupMap[handle];
            group.GroupGhost.UpdateGhostTransform(group.GetAveragePosRotScale());
        }

        /// <summary>
        /// Changes the pivot mode of a transform group.
        /// </summary>
        /// <param name="group">The transform group to modify.</param>
        /// <param name="originToCenter">Whether to set the origin to the center of bounds.</param>
        public void ChangeHandlePivot(TransformGroup group, bool originToCenter)
        {
            if (group == null)
            {
                Debug.LogError("Group is null");
                return;
            }
            group.IsOriginOnCenter = originToCenter;
            group.GroupGhost.UpdateGhostTransform(group.GetAveragePosRotScale());
        }

        /// <summary>
        /// Updates the position of all transforms in a group.
        /// </summary>
        public void UpdateGroupPosition(Ghost ghost, Vector3 positionChange)
        {
            if (_ghostGroupMap.TryGetValue(ghost, out var group))
            {
                group.UpdatePositions(positionChange);
            }
        }

        /// <summary>
        /// Updates the rotation of all transforms in a group.
        /// </summary>
        public void UpdateGroupRotation(Ghost ghost, Quaternion rotationChange)
        {
            if (_ghostGroupMap.TryGetValue(ghost, out var group))
            {
                group.UpdateRotations(rotationChange);
            }
        }

        /// <summary>
        /// Updates the scale of all transforms in a group.
        /// </summary>
        public void UpdateGroupScaleUpdate(Ghost ghost, Vector3 scaleChange)
        {
            if (_ghostGroupMap.TryGetValue(ghost, out var group))
            {
                group.UpdateScales(scaleChange);
            }
        }
    }
}
