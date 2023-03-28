using System;
using UnityEngine;

namespace TransformHandles
{
    public class Handle : MonoBehaviour
    {
        [SerializeField] private float autoScaleSizeInPixels = 192;
        [SerializeField] public bool autoScale;

        public Transform target;
        public HandleAxes axes = HandleAxes.XYZ;
        public Space space = Space.Self;
        public HandleType type = HandleType.Position;
        public SnappingType snappingType = SnappingType.Relative;

        public Vector3 positionSnap = Vector3.zero;
        public float rotationSnap;
        public Vector3 scaleSnap = Vector3.zero;

        public Camera handleCamera;

        private PositionHandle PositionHandle { get; set; }
        private RotationHandle RotationHandle { get; set; }
        private ScaleHandle ScaleHandle { get; set; }
        
        protected virtual void Awake()
        {
            PositionHandle = GetComponentInChildren<PositionHandle>();
            RotationHandle = GetComponentInChildren<RotationHandle>();
            ScaleHandle = GetComponentInChildren<ScaleHandle>();
            
            Clear();
        }
        
        protected virtual void OnEnable()
        {
            handleCamera = TransformHandleManager.Instance.mainCamera;
        }

        protected virtual void OnDisable()
        {
            Disable();
        }

        protected virtual void LateUpdate()
        {
            UpdateHandleTransformation();
            
            if (!autoScale) return;
            transform.PreserveScaleOnScreen(handleCamera.fieldOfView, autoScaleSizeInPixels, handleCamera);
        }
        
        public virtual void Enable(Transform targetTransform)
        {
            target = targetTransform;
            transform.position = targetTransform.position;
            
            CreateHandles();
        }

        public virtual void Disable()
        {
            target = null;

            Clear();
        }

        public virtual void ChangeHandleType(HandleType handleType)
        {
            type = handleType;
            
            Clear();
            CreateHandles();
        }

        public virtual void ChangeHandleSpace(Space newSpace)
        {
            if (type == HandleType.Scale)
                space = Space.Self;
            else
                space = newSpace == Space.Self ? Space.Self : Space.World;
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