using System;
using UnityEngine;

namespace TransformHandles
{
    /// <summary>
    /// Main handle component that manages transform manipulation through position, rotation, and scale handles.
    /// Provides unified control for manipulating transforms in 3D space.
    /// </summary>
    public class Handle : MonoBehaviour
    {
        private const float DefaultAutoScaleSizeInPixels = 192f;

        [SerializeField] private float autoScaleSizeInPixels = DefaultAutoScaleSizeInPixels;
        public float AutoScaleSizeInPixels
        {
            get => autoScaleSizeInPixels;
            set => autoScaleSizeInPixels = value;
        }
        [SerializeField] public bool autoScale;

        /// <summary>Event fired when handle interaction starts.</summary>
        public virtual event Action<Handle> OnInteractionStartEvent;
        /// <summary>Event fired during handle interaction.</summary>
        public virtual event Action<Handle> OnInteractionEvent;
        /// <summary>Event fired when handle interaction ends.</summary>
        public virtual event Action<Handle> OnInteractionEndEvent;
        /// <summary>Event fired when handle is destroyed.</summary>
        public virtual event Action<Handle> OnHandleDestroyedEvent;

        /// <summary>The target transform being manipulated.</summary>
        public Transform target;
        /// <summary>Active axes for the handle.</summary>
        public HandleAxes axes = HandleAxes.XYZ;
        /// <summary>Coordinate space for transformations (Self or World).</summary>
        public Space space = Space.Self;
        /// <summary>Current handle type (Position, Rotation, Scale, or combinations).</summary>
        public HandleType type = HandleType.Position;
        /// <summary>Snapping behavior type (Relative or Absolute).</summary>
        public SnappingType snappingType = SnappingType.Relative;

        /// <summary>Position snapping values for each axis.</summary>
        public Vector3 positionSnap = Vector3.zero;
        /// <summary>Rotation snapping value in degrees.</summary>
        public float rotationSnap;
        /// <summary>Scale snapping values for each axis.</summary>
        public Vector3 scaleSnap = Vector3.zero;

        /// <summary>Camera used for raycasting and screen-to-world conversions.</summary>
        public Camera handleCamera;

        private PositionHandle PositionHandle { get; set; }
        private RotationHandle RotationHandle { get; set; }
        private ScaleHandle ScaleHandle { get; set; }

        private static TransformHandleManager Manager => TransformHandleManager.Instance;

        protected virtual void Awake()
        {
            PositionHandle = GetComponentInChildren<PositionHandle>();
            RotationHandle = GetComponentInChildren<RotationHandle>();
            ScaleHandle = GetComponentInChildren<ScaleHandle>();

            Clear();
        }

        protected virtual void OnEnable()
        {
            handleCamera = Manager.mainCamera;
        }

        protected virtual void OnDisable()
        {
            Disable();
        }

        protected void OnDestroy()
        {
            // Clear event subscribers to prevent memory leaks
            OnInteractionStartEvent = null;
            OnInteractionEvent = null;
            OnInteractionEndEvent = null;
            OnHandleDestroyedEvent?.Invoke(this);
            OnHandleDestroyedEvent = null;

            if (Manager == null) return;
            Manager.RemoveHandle(this);
        }

        protected virtual void LateUpdate()
        {
            UpdateHandleTransformation();

            if (!autoScale || handleCamera == null) return;
            transform.PreserveScaleOnScreen(handleCamera.fieldOfView, autoScaleSizeInPixels, handleCamera);
        }

        /// <summary>
        /// Enables the handle for a specific target transform.
        /// </summary>
        /// <param name="targetTransform">The transform to manipulate.</param>
        public virtual void Enable(Transform targetTransform)
        {
            target = targetTransform;
            transform.position = targetTransform.position;

            CreateHandles();
        }

        /// <summary>
        /// Disables the handle and clears the target.
        /// </summary>
        public virtual void Disable()
        {
            target = null;
            Clear();
        }

        /// <summary>
        /// Called when interaction with the handle starts.
        /// </summary>
        public virtual void InteractionStart()
        {
            OnInteractionStartEvent?.Invoke(this);
        }

        /// <summary>
        /// Called during continuous interaction with the handle.
        /// </summary>
        public virtual void InteractionStay()
        {
            OnInteractionEvent?.Invoke(this);
        }

        /// <summary>
        /// Called when interaction with the handle ends.
        /// </summary>
        public virtual void InteractionEnd()
        {
            OnInteractionEndEvent?.Invoke(this);
        }

        /// <summary>
        /// Changes the handle type (Position, Rotation, Scale, or combinations).
        /// </summary>
        /// <param name="handleType">The new handle type.</param>
        public virtual void ChangeHandleType(HandleType handleType)
        {
            type = handleType;

            Clear();
            CreateHandles();
        }

        /// <summary>
        /// Changes the coordinate space for transformations.
        /// </summary>
        /// <param name="newSpace">The new coordinate space.</param>
        public virtual void ChangeHandleSpace(Space newSpace)
        {
            if (type == HandleType.Scale)
                space = Space.Self;
            else
                space = newSpace == Space.Self ? Space.Self : Space.World;
        }

        /// <summary>
        /// Changes the active axes for the handle.
        /// </summary>
        /// <param name="handleAxes">The new axes configuration.</param>
        public virtual void ChangeAxes(HandleAxes handleAxes)
        {
            axes = handleAxes;

            Clear();
            CreateHandles();
        }

        protected virtual void UpdateHandleTransformation()
        {
            if (!target) return;

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
            if (PositionHandle == null) return;
            PositionHandle.Initialize(this);
            PositionHandle.gameObject.SetActive(true);
        }

        private void ActivateRotationHandle()
        {
            if (RotationHandle == null) return;
            RotationHandle.Initialize(this);
            RotationHandle.gameObject.SetActive(true);
        }

        private void ActivateScaleHandle()
        {
            if (ScaleHandle == null) return;
            ScaleHandle.Initialize(this);
            ScaleHandle.gameObject.SetActive(true);
        }

        protected virtual void Clear()
        {
            if (PositionHandle != null && PositionHandle.gameObject.activeSelf)
                PositionHandle.gameObject.SetActive(false);
            if (RotationHandle != null && RotationHandle.gameObject.activeSelf)
                RotationHandle.gameObject.SetActive(false);
            if (ScaleHandle != null && ScaleHandle.gameObject.activeSelf)
                ScaleHandle.gameObject.SetActive(false);
        }
    }
}
