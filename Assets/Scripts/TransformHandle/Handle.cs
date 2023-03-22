using System;
using UnityEngine;

namespace TransformHandle
{
    public class Handle : MonoBehaviour
    {
        private bool IsHandlesActive { get; set; }

        [SerializeField]
        private float autoScaleSizeInPixels = 192;

        private PositionHandle PositionHandle { get; set; }
        private RotationHandle RotationHandle { get; set; }
        private ScaleHandle ScaleHandle { get; set; }
        
        public Transform target;
        public HandleAxes axes = HandleAxes.XYZ;
        public Space space = Space.Self;
        public HandleType type = HandleType.Position;
        public SnappingType snappingType = SnappingType.Relative;

        public Vector3 positionSnap = Vector3.zero;
        public float rotationSnap;
        public Vector3 scaleSnap = Vector3.zero;

        public bool autoScale;
        public Camera handleCamera;

        private HandleCameraEventHandler _cameraEventHandler;
        
        protected virtual void Awake()
        {
            PositionHandle = GetComponentInChildren<PositionHandle>();
            RotationHandle = GetComponentInChildren<RotationHandle>();
            ScaleHandle = GetComponentInChildren<ScaleHandle>();

            _cameraEventHandler = GetComponent<HandleCameraEventHandler>();

            Clear();
        }
        
        protected virtual void OnEnable()
        {
            handleCamera = TransformHandleManager.Instance.mainCamera;
            _cameraEventHandler.PreRender += OnPreRenderEvent;
        }

        protected virtual void OnDisable()
        {
            _cameraEventHandler.PreRender -= OnPreRenderEvent;
            Disable();
        }

        protected virtual void OnPreRenderEvent()
        {
            OnAutoScale();
        }

        protected virtual void LateUpdate()
        {
            UpdateHandleTransformation();
        }
        
        public virtual void Enable(Transform targetTransform)
        {
            target = targetTransform;
            transform.position = targetTransform.position;
            
            CreateHandles();
            
            IsHandlesActive = true;
        }

        public virtual void Disable()
        {
            target = null;

            Clear();
            
            IsHandlesActive = false;
        }
        
        protected virtual void OnAutoScale()
        {
            if(!autoScale || !IsHandlesActive) return;
            
            var p1 = transform.TransformPoint(Vector3.zero);
            var p2 = transform.TransformPoint(handleCamera.transform.up);
            
            var s1 = handleCamera.WorldToScreenPoint(p1);
            var s2 = handleCamera.WorldToScreenPoint(p2);
            
            var dist = Vector3.Distance(s1, s2);
            if (dist > 0)
            {
                var scaleMultiplierInPx = autoScaleSizeInPixels / dist;
                transform.localScale *= scaleMultiplierInPx;
            }
            else
            {
                transform.localScale = Vector3.one;
            }
        }
        
        public virtual void ChangeHandleType(HandleType handleType)
        {
            type = handleType;
            
            Clear();
            CreateHandles();
        }

        public virtual void ChangeHandleSpace(Space space)
        {
            if (type == HandleType.Scale)
                this.space = Space.Self;
            else
                this.space = space == Space.Self ? Space.Self : Space.World;
        }

        public virtual void ChangeAxes(HandleAxes handleAxes)
        {
            axes = handleAxes;
            
            Clear();
            CreateHandles();
        }

        protected virtual void UpdateHandleTransformation()
        {
            if(!target) return;
            
            transform.position = target.transform.position;
            if (space == Space.Self || type == HandleType.Scale)
            {
                transform.rotation = target.transform.rotation;
            }
            else
            {
                transform.rotation = Quaternion.identity;
            }
        }

        protected virtual void CreateHandles()
        {
            switch (type)
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
            PositionHandle.Initialize(this);
            PositionHandle.gameObject.SetActive(true);
        }

        private void ActivateRotationHandle()
        {
            RotationHandle.Initialize(this);
            RotationHandle.gameObject.SetActive(true);
        }
        
        private void ActivateScaleHandle()
        {
            ScaleHandle.Initialize(this);
            ScaleHandle.gameObject.SetActive(true);
        }
        
        protected virtual void Clear()
        {
            if (PositionHandle.gameObject.activeSelf) PositionHandle.gameObject.SetActive(false);
            if (RotationHandle.gameObject.activeSelf) RotationHandle.gameObject.SetActive(false);
            if (ScaleHandle.gameObject.activeSelf) ScaleHandle.gameObject.SetActive(false);
        }
    }
}