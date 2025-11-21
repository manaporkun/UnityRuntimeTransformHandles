using System;
using System.Collections.Generic;
using TransformHandles.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TransformHandles
{
    public class TransformHandleManager : Singleton<TransformHandleManager>
    {
        public Camera mainCamera;
        
        [Header("Prefabs")]
        [SerializeField] private GameObject transformHandlePrefab;
        [SerializeField] private GameObject ghostPrefab;
        
        [Header("Settings")]
        [SerializeField] private LayerMask layerMask;
        [SerializeField] private Color highlightColor = Color.white;
        
        [Header("Shortcuts")]
        [SerializeField] private KeyCode positionShortcut = KeyCode.W;
        [SerializeField] private KeyCode rotationShortcut = KeyCode.E;
        [SerializeField] private KeyCode scaleShortcut = KeyCode.R;
        [SerializeField] private KeyCode allShortcut = KeyCode.A;
        [SerializeField] private KeyCode spaceShortcut = KeyCode.X;
        [SerializeField] private KeyCode pivotShortcut = KeyCode.Z;

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
            if(_isInitialized) return;
            
            mainCamera = mainCamera == null ? Camera.main : mainCamera;
            
            _handleGroupMap = new Dictionary<Handle, TransformGroup>();
            _ghostGroupMap = new Dictionary<Ghost, TransformGroup>();
            _transformHashSet = new HashSet<Transform>();
            
            _isInitialized = true;
        }

        public Handle CreateHandle(Transform target)
        {
            if (_transformHashSet.Contains(target)) { Debug.LogWarning($"{target} already has a handle."); return null; }

            var ghost = CreateGhost();
            ghost.Initialize();

            var transformHandle = Instantiate(transformHandlePrefab).GetComponent<Handle>();
            transformHandle.Enable(ghost.transform);            
            
            var group = new TransformGroup(ghost, transformHandle);
            
            _handleGroupMap.Add(transformHandle, group);
            _ghostGroupMap.Add(ghost, group);
            
            var success = AddTarget(target, transformHandle);
            if (!success) { DestroyHandle(transformHandle); }
            
            _handleActive = true;
            
            return transformHandle;
        }
        
        public Handle CreateHandleFromList(List<Transform> targets)
        {
            if(targets.Count == 0) { Debug.LogWarning("List is empty."); return null; }
            
            var ghost = CreateGhost();
            ghost.Initialize();

            var transformHandle = Instantiate(transformHandlePrefab).GetComponent<Handle>();
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
                var ghostObject = new GameObject();
                ghost = ghostObject.AddComponent<Ghost>();
            }
            else
            {
                ghost = Instantiate(ghostPrefab).GetComponent<Ghost>();
            }
            
            return ghost;
        }

        public void RemoveHandle(Handle handle)
        {
            if(handle == null) { Debug.LogError("Handle is already null"); return;}
            if(_handleGroupMap == null) return;
            
            var group = _handleGroupMap[handle];
            if (group == null) { Debug.LogError("Group is null"); return;}
            
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
        
        public void DestroyAllHandles()
        {
            foreach (var handle in _handleGroupMap.Keys)
            {
                DestroyHandle(handle);
            }
        }
        
        public bool AddTarget(Transform target, Handle handle)
        {
            if (_transformHashSet.Contains(target)) { Debug.LogWarning($"{target} already has a handle."); return false; }
            if(handle == null) { Debug.LogError("Handle is null"); return false;}

            var group = _handleGroupMap[handle];
            var targetAdded = group.AddTransform(target);
            if(!targetAdded) { Debug.LogWarning($"{target} is relative to the selected ones."); return false; }
            
            var averagePosRotScale = group.GetAveragePosRotScale();
            group.GroupGhost.UpdateGhostTransform(averagePosRotScale);
            
            _transformHashSet.Add(target);

            return true;
        }

        public void RemoveTarget(Transform target, Handle handle)
        {
            if (!_transformHashSet.Contains(target)) { Debug.LogWarning($"{target} doesn't have a handle."); return;}
            if(handle == null) { Debug.LogError("Handle is null"); return;}

            _transformHashSet.Remove(target);
            
            var group = _handleGroupMap[handle];
            var groupElementsRemoved = group.RemoveTransform(target);
            if (groupElementsRemoved) { DestroyHandle(handle); return; }
            
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
            _rayHits = new RaycastHit[16];

            var size = 0;
            try
            {
                var ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                size = Physics.RaycastNonAlloc(ray, _rayHits, 1000, layerMask);
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
                if (_hoveredHandle == null) return;
                return;
            }
            
            Array.Sort(_rayHits, (x,y) => x.distance.CompareTo(y.distance));

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
                handleBase.SetColor(highlightColor);
            }

            _previousAxis = handleBase;
        }

        protected virtual void MouseInput()
        {
            if (Input.GetMouseButton(0) && _draggingHandle != null)
            {
                _draggingHandle.Interact(_previousMousePosition);
                OnInteraction();
            }

            if (Input.GetMouseButtonDown(0) && _hoveredHandle != null)
            {
                _draggingHandle = _hoveredHandle;
                _draggingHandle.StartInteraction(_handleHitPoint);
                OnInteractionStart();
            }

            if (Input.GetMouseButtonUp(0) && _draggingHandle != null)
            {
                _draggingHandle.EndInteraction();
                _draggingHandle = null;
                OnInteractionEnd();
            }

            _previousMousePosition = Input.mousePosition;
        }
        
        protected virtual void KeyboardInput()
        {
            if (Input.GetKeyDown(positionShortcut))
            {
                foreach (var handle in _handleGroupMap.Keys)
                {
                    ChangeHandleType(handle, HandleType.Position);
                }
            }
            
            if (Input.GetKeyDown(rotationShortcut))
            {
                foreach (var handle in _handleGroupMap.Keys)
                {
                    ChangeHandleType(handle, HandleType.Rotation);
                }
            }
            
            if (Input.GetKeyDown(scaleShortcut))
            {
                foreach (var handle in _handleGroupMap.Keys)
                {
                    ChangeHandleType(handle, HandleType.Scale);
                }
            }
            
            if (Input.GetKeyDown(allShortcut))
            {
                foreach (var handle in _handleGroupMap.Keys)
                {
                    ChangeHandleType(handle, HandleType.All);
                }
            }

            if (Input.GetKeyDown(spaceShortcut))
            {
                foreach (var handle in _handleGroupMap.Keys)
                {
                    ChangeHandleSpace(handle, handle.space == Space.World ? Space.Self : Space.World);
                }
            }

            if (Input.GetKeyDown(pivotShortcut))
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
        
        public static void ChangeHandleType(Handle handle, HandleType type)
        {
            if(handle == null) { Debug.LogError("Handle is null"); return;}
            handle.ChangeHandleType(type);
        }

        public void ChangeHandleSpace(Handle handle, Space space)
        {
            if(handle == null) { Debug.LogError("Handle is null"); return;}
            handle.ChangeHandleSpace(space);
            
            var group = _handleGroupMap[handle];
            group.GroupGhost.UpdateGhostTransform(group.GetAveragePosRotScale());
        }

        public void ChangeHandlePivot(TransformGroup group, bool originToCenter)
        {
            if(group == null) { Debug.LogError("Group is null"); return;}
            group.IsOriginOnCenter = originToCenter;
            group.GroupGhost.UpdateGhostTransform(group.GetAveragePosRotScale());
        }
        
        public void UpdateGroupPosition(Ghost ghost, Vector3 positionChange)
        {
            var group = _ghostGroupMap[ghost];
            group.UpdatePositions(positionChange);
        }
        
        public void UpdateGroupRotation(Ghost ghost, Quaternion rotationChange)
        {
            var group = _ghostGroupMap[ghost];
            group.UpdateRotations(rotationChange);
        }
        
        public void UpdateGroupScaleUpdate(Ghost ghost, Vector3 scaleChange)
        {
            var group = _ghostGroupMap[ghost];
            group.UpdateScales(scaleChange);
        }
    }
}