using System;
using System.Collections.Generic;
using TransformHandles.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace TransformHandles
{
    public class TransformHandleManager : Singleton<TransformHandleManager>
    {
        [FormerlySerializedAs("mainCamera")] public Camera _mainCamera;
        
        [FormerlySerializedAs("transformHandlePrefab")]
        [Header("Prefabs")]
        [SerializeField] private GameObject _transformHandlePrefab;
        [FormerlySerializedAs("ghostPrefab")] [SerializeField] private GameObject _ghostPrefab;
        
        [FormerlySerializedAs("layerMask")]
        [Header("Settings")]
        [SerializeField] private LayerMask _layerMask;
        [FormerlySerializedAs("highlightColor")] [SerializeField] private Color _highlightColor = Color.white;
        
        [FormerlySerializedAs("positionShortcut")]
        [Header("Shortcuts")]
        [SerializeField] private KeyCode _positionShortcut = KeyCode.W;
        [FormerlySerializedAs("rotationShortcut")] [SerializeField] private KeyCode _rotationShortcut = KeyCode.E;
        [FormerlySerializedAs("scaleShortcut")] [SerializeField] private KeyCode _scaleShortcut = KeyCode.R;
        [FormerlySerializedAs("allShortcut")] [SerializeField] private KeyCode _allShortcut = KeyCode.A;
        [FormerlySerializedAs("spaceShortcut")] [SerializeField] private KeyCode _spaceShortcut = KeyCode.X;
        [FormerlySerializedAs("pivotShortcut")] [SerializeField] private KeyCode _pivotShortcut = KeyCode.Z;

        private RaycastHit[] _rayHits;
        
        private Vector3 _previousMousePosition;
        private Vector3 _handleHitPoint;

        private HandleBase _previousAxis;
        private HandleBase _draggingHandle;
        private HandleBase _hoveredHandle;
        
        private Ghost _interactedGhost;
        private HandleGroup _interactedHandleGroup;
        
        private HashSet<Transform> _transformHashSet;
        private Dictionary<HandleGroup, TransformGroup> _handleGroupMap;
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
            
            _mainCamera = _mainCamera == null ? Camera.main : _mainCamera;
            
            _handleGroupMap = new Dictionary<HandleGroup, TransformGroup>();
            _ghostGroupMap = new Dictionary<Ghost, TransformGroup>();
            _transformHashSet = new HashSet<Transform>();
            
            _isInitialized = true;
        }

        public HandleGroup CreateHandle(Transform target)
        {
            if (_transformHashSet.Contains(target)) { Debug.LogWarning($"{target} already has a handle."); return null; }

            var ghost = CreateGhost();
            ghost.Initialize();

            var transformHandle = Instantiate(_transformHandlePrefab).GetComponent<HandleGroup>();
            transformHandle.Enable(ghost.transform);            
            
            var group = new TransformGroup(ghost, transformHandle);
            
            _handleGroupMap.Add(transformHandle, group);
            _ghostGroupMap.Add(ghost, group);
            
            var success = AddTarget(target, transformHandle);
            if (!success) { DestroyHandle(transformHandle); }
            
            _handleActive = true;
            
            return transformHandle;
        }
        
        public HandleGroup CreateHandleFromList(List<Transform> targets)
        {
            if(targets.Count == 0) { Debug.LogWarning("List is empty."); return null; }
            
            var ghost = CreateGhost();
            ghost.Initialize();

            var transformHandle = Instantiate(_transformHandlePrefab).GetComponent<HandleGroup>();
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

        public void RemoveHandle(HandleGroup handleGroup)
        {
            if(handleGroup == null) { Debug.LogError("Handle is already null"); return;}
            if(_handleGroupMap == null) return;
            
            var group = _handleGroupMap[handleGroup];
            if (group == null) { Debug.LogError("Group is null"); return;}
            
            _handleGroupMap.Remove(handleGroup);
            handleGroup.Disable();

            var groupGhost = group.groupGhost;
            if (groupGhost != null)
            {
                _ghostGroupMap.Remove(groupGhost);
                group.groupGhost.Terminate();
            }
            
            if (_handleGroupMap.Count == 0) _handleActive = false;
        }

        private static void DestroyHandle(HandleGroup handleGroup)
        {
            DestroyImmediate(handleGroup.gameObject);
        }
        
        public void DestroyAllHandles()
        {
            foreach (var handle in _handleGroupMap.Keys)
            {
                DestroyHandle(handle);
            }
        }
        
        public bool AddTarget(Transform target, HandleGroup handleGroup)
        {
            if (_transformHashSet.Contains(target)) { Debug.LogWarning($"{target} already has a handle."); return false; }
            if(handleGroup == null) { Debug.LogError("Handle is null"); return false;}

            var group = _handleGroupMap[handleGroup];
            var targetAdded = group.AddTransform(target);
            if(!targetAdded) { Debug.LogWarning($"{target} is relative to the selected ones."); return false; }
            
            var averagePosRotScale = group.GetAveragePosRotScale();
            group.groupGhost.UpdateGhostTransform(averagePosRotScale);
            
            _transformHashSet.Add(target);

            return true;
        }

        public void RemoveTarget(Transform target, HandleGroup handleGroup)
        {
            if (!_transformHashSet.Contains(target)) { Debug.LogWarning($"{target} doesn't have a handle."); return;}
            if(handleGroup == null) { Debug.LogError("Handle is null"); return;}

            _transformHashSet.Remove(target);
            
            var group = _handleGroupMap[handleGroup];
            var groupElementsRemoved = group.RemoveTransform(target);
            if (groupElementsRemoved) { DestroyHandle(handleGroup); return; }
            
            var averagePosRotScale = group.GetAveragePosRotScale();
            group.groupGhost.UpdateGhostTransform(averagePosRotScale);
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
                var ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
                size = Physics.RaycastNonAlloc(ray, _rayHits, 1000, _layerMask);
            }
            catch (MissingReferenceException)
            {
                _mainCamera = Camera.main;
                Debug.Log("Camera is null, trying to find main camera");

                if (_mainCamera == null)
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
                handleBase.SetColor(_highlightColor);
            }

            _previousAxis = handleBase;
        }

        protected virtual void MouseInput()
        {
            if (Input.GetMouseButton(0) && _draggingHandle != null)
            {
                _draggingHandle.OnInteractionActive(_previousMousePosition);
                OnInteraction();
            }

            if (Input.GetMouseButtonDown(0) && _hoveredHandle != null)
            {
                _draggingHandle = _hoveredHandle;
                _draggingHandle.OnInteractionStarted(_handleHitPoint);
                OnInteractionStart();
            }

            if (Input.GetMouseButtonUp(0) && _draggingHandle != null)
            {
                _draggingHandle.OnInteractionEnded();
                _draggingHandle = null;
                OnInteractionEnd();
            }

            _previousMousePosition = Input.mousePosition;
        }
        
        protected virtual void KeyboardInput()
        {
            if (Input.GetKeyDown(_positionShortcut))
            {
                foreach (var handle in _handleGroupMap.Keys)
                {
                    ChangeHandleType(handle, HandleType.Position);
                }
            }
            
            if (Input.GetKeyDown(_rotationShortcut))
            {
                foreach (var handle in _handleGroupMap.Keys)
                {
                    ChangeHandleType(handle, HandleType.Rotation);
                }
            }
            
            if (Input.GetKeyDown(_scaleShortcut))
            {
                foreach (var handle in _handleGroupMap.Keys)
                {
                    ChangeHandleType(handle, HandleType.Scale);
                }
            }
            
            if (Input.GetKeyDown(_allShortcut))
            {
                foreach (var handle in _handleGroupMap.Keys)
                {
                    ChangeHandleType(handle, HandleType.All);
                }
            }

            if (Input.GetKeyDown(_spaceShortcut))
            {
                foreach (var handle in _handleGroupMap.Keys)
                {
                    ChangeHandleSpace(handle, handle._space == Space.World ? Space.Self : Space.World);
                }
            }

            if (Input.GetKeyDown(_pivotShortcut))
            {
                foreach (var group in _handleGroupMap.Values)
                {
                    ChangeHandlePivot(group, !group.IsOriginOnCenter);
                }
            }
        }

        protected virtual void OnInteractionStart()
        {
            _interactedHandleGroup = _draggingHandle.GetComponentInParent<HandleGroup>();
            _interactedGhost = _handleGroupMap[_interactedHandleGroup].groupGhost;
            _interactedGhost.OnInteractionStart();
            
            _interactedHandleGroup.InteractionStart();
        }

        protected virtual void OnInteraction()
        {
            _interactedGhost.OnInteraction(_interactedHandleGroup._type);
            
            _interactedHandleGroup.InteractionStay();
        }

        protected virtual void OnInteractionEnd()
        {
            var group = _handleGroupMap[_interactedHandleGroup];
            group.UpdateBounds();

            _interactedHandleGroup.InteractionEnd();
        }
        
        public static void ChangeHandleType(HandleGroup handleGroup, HandleType type)
        {
            if(handleGroup == null) { Debug.LogError("Handle is null"); return;}
            handleGroup.ChangeHandleType(type);
        }

        public void ChangeHandleSpace(HandleGroup handleGroup, Space space)
        {
            if(handleGroup == null) { Debug.LogError("Handle is null"); return;}
            handleGroup.ChangeHandleSpace(space);
            
            var group = _handleGroupMap[handleGroup];
            group.groupGhost.UpdateGhostTransform(group.GetAveragePosRotScale());
        }

        public void ChangeHandlePivot(TransformGroup group, bool originToCenter)
        {
            if(group == null) { Debug.LogError("Group is null"); return;}
            group.IsOriginOnCenter = originToCenter;
            group.groupGhost.UpdateGhostTransform(group.GetAveragePosRotScale());
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
        public Ghost groupGhost {get; }
        public HandleGroup groupHandleGroup {get; }
        
        public bool IsOriginOnCenter;

        public HashSet<Transform> transforms { get; }
        public Dictionary<Transform, MeshRenderer> renderersMap { get; }
        public Dictionary<Transform, Bounds> boundsMap { get; }

        public TransformGroup(Ghost groupGhost, HandleGroup groupHandleGroup)
        {
            this.groupGhost = groupGhost;
            this.groupHandleGroup = groupHandleGroup;
            
            transforms = new HashSet<Transform>();
            renderersMap = new Dictionary<Transform, MeshRenderer>();
            boundsMap = new Dictionary<Transform, Bounds>();
        }

        public bool AddTransform(Transform tElement)
        {
            if (IsTargetRelativeToSelectedOnes(tElement)) return false;
            
            var meshRenderer = tElement.GetComponent<MeshRenderer>();
            
            transforms.Add(tElement);
            renderersMap.Add(tElement, meshRenderer);
            boundsMap.Add(tElement, meshRenderer != null ? meshRenderer.bounds : tElement.GetBounds());
            
            return true;
        }
        
        public bool RemoveTransform(Transform transform)
        {
            transforms.Remove(transform);
            renderersMap.Remove(transform);
            boundsMap.Remove(transform);
            
            return transforms.Count == 0;
        }

        public void UpdateBounds()
        {
            foreach (var (tElement, meshRenderer) in renderersMap)
            {
                var bounds = meshRenderer ? meshRenderer.bounds : tElement.GetBounds();
                boundsMap[tElement] = bounds;
            }
        }
        
        public void UpdatePositions(Vector3 positionChange)
        {
            foreach (var tElement in renderersMap.Keys)
            {
                tElement.position += positionChange;
            }
        }

        public void UpdateRotations(Quaternion rotationChange)
        {
            var ghostPosition = groupGhost.transform.position;
            var rotationAxis = rotationChange.normalized.eulerAngles;
            var rotationChangeMagnitude = rotationChange.eulerAngles.magnitude;
            foreach (var tElement in renderersMap.Keys)
            {
                if (groupHandleGroup._space == Space.Self)
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
            foreach (var (tElement, meshRenderer) in renderersMap)
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
            return IsOriginOnCenter ? boundsMap[tElement].center : tElement.position;
        }
        
        public PosRotScale GetAveragePosRotScale()
        {
            var space = groupHandleGroup._space;
            
            var centerPositions = new List<Vector3>();
            var sumQuaternion = Quaternion.identity;

            var transformsCount = transforms.Count;

            foreach (var tElement in transforms)
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
            
            var averagePosRotScale = new PosRotScale(averagePosition, sumQuaternion, Vector3.one);

            return averagePosRotScale;
        }
        
        private bool IsTargetRelativeToSelectedOnes(Transform newTarget)
        {
            foreach (var transformInHash in transforms)
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