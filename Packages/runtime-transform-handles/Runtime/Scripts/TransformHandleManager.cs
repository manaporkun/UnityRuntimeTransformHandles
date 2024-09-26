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
            
            var ghost = Instantiate(ghostPrefab).GetComponent<Ghost>();
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
            
            var ghost = Instantiate(ghostPrefab).GetComponent<Ghost>();
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

        private Vector3 getPos() {
            if (Input.touchCount > 0) {
                return Input.GetTouch(0).position;
            } else {
                return Input.mousePosition;
            }
        }

        
        protected virtual void GetHandle(ref HandleBase handle, ref Vector3 hitPoint)
        {
            _rayHits = new RaycastHit[16];

            var size = 0;
            try
            {
                var ray = mainCamera.ScreenPointToRay(getPos());
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
            bool GetMouseButtonExpl(int i) {
                if (Input.touchCount > i) {
                    return true;
                } else {
                    return Input.GetMouseButton(i);
                }
            }
            bool GetMouseButtonUpExpl(int i, TouchPhase touchphase) {
                if (Input.touchCount > i) {
                    return Input.GetTouch(i).phase == touchphase;
                } else {
                    return Input.GetMouseButtonUp(i);
                }
            }
            bool GetMouseButtonDownExpl(int i, TouchPhase touchphase) {
                if (Input.touchCount > i) {
                    return Input.GetTouch(i).phase == touchphase;
                } else {
                    return Input.GetMouseButtonDown(i);
                }
            }
            if (GetMouseButtonExpl(0) && _draggingHandle != null)
            {
                _draggingHandle.Interact(_previousMousePosition);
                OnInteraction();
            }

            if (GetMouseButtonDownExpl(0, TouchPhase.Began) && _hoveredHandle != null)
            {
                _draggingHandle = _hoveredHandle;
                _draggingHandle.StartInteraction(_handleHitPoint);
                OnInteractionStart();
            }

            if (GetMouseButtonUpExpl(0, TouchPhase.Ended) && _draggingHandle != null)
            {
                _draggingHandle.EndInteraction();
                _draggingHandle = null;
                OnInteractionEnd();
            }

            if (Input.simulateMouseWithTouches) {
                _previousMousePosition = Input.mousePosition;
            } else {
                if (Input.touchCount > 0) {
                    _previousMousePosition = Input.GetTouch(0).position;
                } else {
                    _previousMousePosition = new Vector3(0, 0, 0);
                }
            }
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
    
    public class TransformGroup
    {
        public Ghost GroupGhost {get; }
        public Handle GroupHandle {get; }
        
        public bool IsOriginOnCenter;

        public HashSet<Transform> Transforms { get; }
        public Dictionary<Transform, MeshRenderer> RenderersMap { get; }
        public Dictionary<Transform, Bounds> BoundsMap { get; }

        public TransformGroup(Ghost groupGhost, Handle groupHandle)
        {
            GroupGhost = groupGhost;
            GroupHandle = groupHandle;
            
            Transforms = new HashSet<Transform>();
            RenderersMap = new Dictionary<Transform, MeshRenderer>();
            BoundsMap = new Dictionary<Transform, Bounds>();
        }

        public bool AddTransform(Transform tElement)
        {
            if (IsTargetRelativeToSelectedOnes(tElement)) return false;
            
            var meshRenderer = tElement.GetComponent<MeshRenderer>();
            
            Transforms.Add(tElement);
            RenderersMap.Add(tElement, meshRenderer);
            BoundsMap.Add(tElement, meshRenderer != null ? meshRenderer.bounds : tElement.GetBounds());
            
            return true;
        }
        
        public bool RemoveTransform(Transform transform)
        {
            Transforms.Remove(transform);
            RenderersMap.Remove(transform);
            BoundsMap.Remove(transform);
            
            return Transforms.Count == 0;
        }

        public void UpdateBounds()
        {
            foreach (var (tElement, meshRenderer) in RenderersMap)
            {
                var bounds = meshRenderer ? meshRenderer.bounds : tElement.GetBounds();
                BoundsMap[tElement] = bounds;
            }
        }
        
        public void UpdatePositions(Vector3 positionChange)
        {
            foreach (var tElement in RenderersMap.Keys)
            {
                tElement.position += positionChange;
            }
        }

        public void UpdateRotations(Quaternion rotationChange)
        {
            var ghostPosition = GroupGhost.transform.position;
            var rotationAxis = rotationChange.normalized.eulerAngles;
            var rotationChangeMagnitude = rotationChange.eulerAngles.magnitude;
            foreach (var tElement in RenderersMap.Keys)
            {
                if (GroupHandle.space == Space.Self)
                {
                    tElement.position = rotationChange * (tElement.position - ghostPosition) + ghostPosition;
                    tElement.rotation = rotationChange * tElement.rotation;
                }
                else
                {
                    tElement.RotateAround(ghostPosition, rotationAxis, rotationChangeMagnitude);
                }
            }
        }

        public void UpdateScales(Vector3 scaleChange)
        {
            foreach (var (tElement, meshRenderer) in RenderersMap)
            {
                if (IsOriginOnCenter)
                {
                    if (meshRenderer != null)
                    {
                        var oldCenter = meshRenderer.bounds.center;

                        tElement.localScale += scaleChange;
            
                        // ReSharper disable once Unity.InefficientPropertyAccess
                        var newCenter =  meshRenderer.bounds.center;

                        var change = newCenter - oldCenter;
                
                        tElement.position += change * -1;
                    }
                    else
                    {
                        tElement.localScale += scaleChange;
                    }
                }
                else
                {
                    tElement.localScale += scaleChange;
                }
            }
        }

        private Vector3 GetCenterPoint(Transform tElement)
        {
            return IsOriginOnCenter ? BoundsMap[tElement].center : tElement.position;
        }
        
        public PosRotScale GetAveragePosRotScale()
        {
            var space = GroupHandle.space;
            
            var averagePosRotScale = new PosRotScale();
            
            var centerPositions = new List<Vector3>();
            var sumQuaternion = Quaternion.identity;

            var transformsCount = Transforms.Count;

            foreach (var tElement in Transforms)
            {
                var centerPoint = GetCenterPoint(tElement);
                centerPositions.Add(centerPoint);
                
                if (space == Space.World) continue;
                sumQuaternion *= tElement.rotation;
            }

            var averagePosition = Vector3.zero;
            foreach (var centerPosition in centerPositions)
            {
                averagePosition += centerPosition;
            }
            averagePosition /= transformsCount;
            
            averagePosRotScale.Position = averagePosition;
            averagePosRotScale.Rotation = sumQuaternion;
            averagePosRotScale.Scale = Vector3.one;

            return averagePosRotScale;
        }
        
        private bool IsTargetRelativeToSelectedOnes(Transform newTarget)
        {
            foreach (var transformInHash in Transforms)
            {
                if (transformInHash.IsDeepParentOf(newTarget)) return true;

                if (!newTarget.IsDeepParentOf(transformInHash)) continue;
                RemoveTransform(transformInHash);
                return false;
            }

            return false;
        }
    }
}
