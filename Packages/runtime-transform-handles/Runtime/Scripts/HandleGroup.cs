using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace TransformHandles
{
    public class HandleGroup : MonoBehaviour
    {
        [FormerlySerializedAs("autoScaleSizeInPixels")] [SerializeField] private float _autoScaleSizeInPixels = 192;
        [FormerlySerializedAs("autoScale")] [SerializeField] public bool _autoScale;
        
        public virtual event Action<HandleGroup> OnInteractionStartEvent;
        public virtual event Action<HandleGroup> OnInteractionEvent;
        public virtual event Action<HandleGroup> OnInteractionEndEvent;
        public virtual event Action<HandleGroup> OnHandleDestroyedEvent; 

        [FormerlySerializedAs("target")] public Transform _target;
        [FormerlySerializedAs("axes")] public HandleAxes _axes = HandleAxes.XYZ;
        [FormerlySerializedAs("space")] public Space _space = Space.Self;
        [FormerlySerializedAs("type")] public HandleType _type = HandleType.Position;
        [FormerlySerializedAs("snappingType")] public SnappingType _snappingType = SnappingType.Relative;

        [FormerlySerializedAs("positionSnap")] public Vector3 _positionSnap = Vector3.zero;
        [FormerlySerializedAs("rotationSnap")] public float _rotationSnap;
        [FormerlySerializedAs("scaleSnap")] public Vector3 _scaleSnap = Vector3.zero;

        [FormerlySerializedAs("handleCamera")] public Camera _handleCamera;

        private PositionHandleSet positionHandleSet { get; set; }
        private RotationHandleSet rotationHandleSet { get; set; }
        private ScaleHandleSet scaleHandleSet { get; set; }
        
        private static TransformHandleManager manager => TransformHandleManager.instance;
        
        protected virtual void Awake()
        {
            positionHandleSet = GetComponentInChildren<PositionHandleSet>();
            rotationHandleSet = GetComponentInChildren<RotationHandleSet>();
            scaleHandleSet = GetComponentInChildren<ScaleHandleSet>();
            
            Clear();
        }
        
        protected virtual void OnEnable()
        {
            _handleCamera = manager._mainCamera;
        }

        protected virtual void OnDisable()
        {
            Disable();
        }

        protected void OnDestroy()
        {
            if (manager == null) return;
            
            manager.RemoveHandle(this);
            OnHandleDestroyedEvent?.Invoke(this);
        }

        protected virtual void LateUpdate()
        {
            UpdateHandleTransformation();
            
            if (!_autoScale || _handleCamera == null) return;
            transform.PreserveScaleOnScreen(_handleCamera.fieldOfView, _autoScaleSizeInPixels, _handleCamera);
        }
        
        public virtual void Enable(Transform targetTransform)
        {
            _target = targetTransform;
            transform.position = targetTransform.position;
            
            CreateHandles();
        }

        public virtual void Disable()
        {
            _target = null;

            Clear();
        }

        public virtual void InteractionStart()
        {
            OnInteractionStartEvent?.Invoke(this);
        }

        public virtual void InteractionStay()
        {
            OnInteractionEvent?.Invoke(this);
        }
        
        public virtual void InteractionEnd()
        {
            OnInteractionEndEvent?.Invoke(this);
        }

        public virtual void ChangeHandleType(HandleType handleType)
        {
            _type = handleType;
            
            Clear();
            CreateHandles();
        }

        public virtual void ChangeHandleSpace(Space newSpace)
        {
            if (_type == HandleType.Scale)
                _space = Space.Self;
            else
                _space = newSpace == Space.Self ? Space.Self : Space.World;
        }

        public virtual void ChangeAxes(HandleAxes handleAxes)
        {
            _axes = handleAxes;
            
            Clear();
            CreateHandles();
        }

        protected virtual void UpdateHandleTransformation()
        {
            if(!_target) return;
            
            transform.position = _target.transform.position;
            if (_space == Space.Self || _type == HandleType.Scale)
            {
                transform.rotation = _target.transform.rotation;
            }
            else
            {
                transform.rotation = Quaternion.identity;
            }
        }

        protected virtual void CreateHandles()
        {
            switch (_type)
            {
                case HandleType.Position:
                    ActivatePositionHandle();
                    break;
                case HandleType.Rotation:
                    ActivateRotationHandle();
                    break;
                case HandleType.Scale:
                    ActivateScaleHandle();
                    break;
                case HandleType.PositionRotation:
                    ActivatePositionHandle();
                    ActivateRotationHandle();
                    break;
                case HandleType.PositionScale:
                    ActivatePositionHandle();
                    ActivateScaleHandle();
                    break;
                case HandleType.RotationScale:
                    ActivateRotationHandle();
                    ActivateScaleHandle();
                    break;
                case HandleType.All:
                    ActivatePositionHandle();
                    ActivateRotationHandle();
                    ActivateScaleHandle();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void ActivatePositionHandle()
        {
            positionHandleSet.Initialize(this);
            positionHandleSet.gameObject.SetActive(true);
        }

        private void ActivateRotationHandle()
        {
            rotationHandleSet.Initialize(this);
            rotationHandleSet.gameObject.SetActive(true);
        }
        
        private void ActivateScaleHandle()
        {
            scaleHandleSet.Initialize(this);
            scaleHandleSet.gameObject.SetActive(true);
        }
        
        protected virtual void Clear()
        {
            if (positionHandleSet.gameObject.activeSelf) positionHandleSet.gameObject.SetActive(false);
            if (rotationHandleSet.gameObject.activeSelf) rotationHandleSet.gameObject.SetActive(false);
            if (scaleHandleSet.gameObject.activeSelf) scaleHandleSet.gameObject.SetActive(false);
        }
    }
}