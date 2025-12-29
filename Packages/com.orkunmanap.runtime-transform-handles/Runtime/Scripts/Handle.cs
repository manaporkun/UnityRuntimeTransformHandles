using System;
using UnityEngine;
using UnityEngine.Events;

namespace TransformHandles
{
    /// <summary>
    /// UnityEvent that passes the Handle as a parameter.
    /// Use this for Inspector-configurable event handling.
    /// </summary>
    [Serializable]
    public class HandleUnityEvent : UnityEvent<Handle> { }

    /// <summary>
    /// Defines the visual appearance of a handle including axis colors and scale.
    /// </summary>
    [Serializable]
    public struct HandleAppearance
    {
        /// <summary>Color for the X axis.</summary>
        public Color xAxisColor;
        /// <summary>Color for the Y axis.</summary>
        public Color yAxisColor;
        /// <summary>Color for the Z axis.</summary>
        public Color zAxisColor;
        /// <summary>Color for the global/center handle.</summary>
        public Color globalColor;
        /// <summary>Scale multiplier for the handle.</summary>
        public float scale;

        /// <summary>
        /// Creates a default appearance with standard axis colors.
        /// </summary>
        public static HandleAppearance Default => new HandleAppearance
        {
            xAxisColor = new Color(1f, 0.2f, 0.2f, 1f),
            yAxisColor = new Color(0.2f, 1f, 0.2f, 1f),
            zAxisColor = new Color(0.2f, 0.6f, 1f, 1f),
            globalColor = new Color(1f, 0.6f, 0f, 1f),
            scale = 1f
        };
    }

    /// <summary>
    /// Main handle component that manages transform manipulation through position, rotation, and scale handles.
    /// Provides unified control for manipulating transforms in 3D space.
    /// </summary>
    public class Handle : MonoBehaviour
    {
        private const float DefaultAutoScaleSizeInPixels = 192f;

        [Header("Auto Scale")]
        [SerializeField] private float autoScaleSizeInPixels = DefaultAutoScaleSizeInPixels;
        [SerializeField] public bool autoScale;

        [Header("Appearance")]
        [SerializeField] private float handleScaleMultiplier = 1f;

        /// <summary>Event fired when handle interaction starts.</summary>
        public virtual event Action<Handle> OnInteractionStartEvent;
        /// <summary>Event fired during handle interaction.</summary>
        public virtual event Action<Handle> OnInteractionEvent;
        /// <summary>Event fired when handle interaction ends.</summary>
        public virtual event Action<Handle> OnInteractionEndEvent;
        /// <summary>Event fired when handle is destroyed.</summary>
        public virtual event Action<Handle> OnHandleDestroyedEvent;

        [Header("Unity Events (Inspector)")]
        [Tooltip("Fired when handle interaction starts. Configure in Inspector.")]
        [SerializeField] private HandleUnityEvent onInteractionStart = new HandleUnityEvent();
        [Tooltip("Fired during handle interaction. Configure in Inspector.")]
        [SerializeField] private HandleUnityEvent onInteraction = new HandleUnityEvent();
        [Tooltip("Fired when handle interaction ends. Configure in Inspector.")]
        [SerializeField] private HandleUnityEvent onInteractionEnd = new HandleUnityEvent();
        [Tooltip("Fired when handle is destroyed. Configure in Inspector.")]
        [SerializeField] private HandleUnityEvent onHandleDestroyed = new HandleUnityEvent();

        /// <summary>UnityEvent fired when handle interaction starts. Configure in Inspector.</summary>
        public HandleUnityEvent OnInteractionStartUnityEvent => onInteractionStart;
        /// <summary>UnityEvent fired during handle interaction. Configure in Inspector.</summary>
        public HandleUnityEvent OnInteractionUnityEvent => onInteraction;
        /// <summary>UnityEvent fired when handle interaction ends. Configure in Inspector.</summary>
        public HandleUnityEvent OnInteractionEndUnityEvent => onInteractionEnd;
        /// <summary>UnityEvent fired when handle is destroyed. Configure in Inspector.</summary>
        public HandleUnityEvent OnHandleDestroyedUnityEvent => onHandleDestroyed;

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
            // Invoke destroyed events before cleanup
            OnHandleDestroyedEvent?.Invoke(this);
            onHandleDestroyed?.Invoke(this);

            // Clear event subscribers to prevent memory leaks
            OnInteractionStartEvent = null;
            OnInteractionEvent = null;
            OnInteractionEndEvent = null;
            OnHandleDestroyedEvent = null;

            // Clear UnityEvent listeners
            onInteractionStart?.RemoveAllListeners();
            onInteraction?.RemoveAllListeners();
            onInteractionEnd?.RemoveAllListeners();
            onHandleDestroyed?.RemoveAllListeners();

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
            onInteractionStart?.Invoke(this);
        }

        /// <summary>
        /// Called during continuous interaction with the handle.
        /// </summary>
        public virtual void InteractionStay()
        {
            OnInteractionEvent?.Invoke(this);
            onInteraction?.Invoke(this);
        }

        /// <summary>
        /// Called when interaction with the handle ends.
        /// </summary>
        public virtual void InteractionEnd()
        {
            OnInteractionEndEvent?.Invoke(this);
            onInteractionEnd?.Invoke(this);
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

        #region Appearance Customization

        /// <summary>
        /// Gets or sets the scale multiplier for this handle.
        /// Values greater than 1 make the handle larger, less than 1 make it smaller.
        /// </summary>
        public float ScaleMultiplier
        {
            get => handleScaleMultiplier;
            set
            {
                handleScaleMultiplier = Mathf.Clamp(value, 0.1f, 10f);
                ApplyScale();
            }
        }

        /// <summary>
        /// Gets or sets the auto-scale size in pixels.
        /// Used when autoScale is enabled to maintain consistent screen size.
        /// </summary>
        public float AutoScaleSizeInPixels
        {
            get => autoScaleSizeInPixels;
            set => autoScaleSizeInPixels = Mathf.Max(1f, value);
        }

        /// <summary>
        /// Sets the handle scale using a multiplier.
        /// </summary>
        /// <param name="scale">The scale multiplier (1.0 = default size).</param>
        public void SetScale(float scale)
        {
            ScaleMultiplier = scale;
        }

        /// <summary>
        /// Applies the current appearance settings from a TransformHandleSettings asset.
        /// </summary>
        /// <param name="settings">The settings to apply.</param>
        public void ApplySettings(TransformHandleSettings settings)
        {
            if (settings == null) return;

            handleScaleMultiplier = settings.HandleScale;
            ApplyScale();
        }

        /// <summary>
        /// Applies the scale multiplier to the handle transform.
        /// </summary>
        private void ApplyScale()
        {
            transform.localScale = Vector3.one * handleScaleMultiplier;
        }

        #endregion
    }
}
