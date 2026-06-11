using System;
using TransformHandles.Utils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

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
        [SerializeField, FormerlySerializedAs("autoScale")] private bool _autoScale;

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

        /// <summary>The target transform being manipulated. Read-only; set via <see cref="Enable"/>.</summary>
        public Transform Target { get; private set; }

        /// <inheritdoc cref="Target"/>
        [Obsolete("Use Target instead.")]
        public Transform target => Target;

        [SerializeField, FormerlySerializedAs("axes")] private HandleAxes _axes = HandleAxes.XYZ;
        /// <summary>Active axes for the handle. Assigning rebuilds the child handles.</summary>
        public HandleAxes Axes
        {
            get => _axes;
            set
            {
                if (_axes == value) return;
                _axes = value;
                Clear();
                CreateHandles();
            }
        }

        /// <inheritdoc cref="Axes"/>
        [Obsolete("Use Axes instead.")]
        public HandleAxes axes { get => Axes; set => Axes = value; }

        [SerializeField, FormerlySerializedAs("space")] private Space _space = Space.Self;
        /// <summary>Coordinate space for transformations. Scale handles are always <see cref="UnityEngine.Space.Self"/>.</summary>
        public Space Space
        {
            get => _space;
            set => _space = Type == HandleType.Scale ? Space.Self : (value == Space.Self ? Space.Self : Space.World);
        }

        /// <inheritdoc cref="Space"/>
        [Obsolete("Use Space instead.")]
        public Space space { get => Space; set => Space = value; }

        [SerializeField, FormerlySerializedAs("type")] private HandleType _type = HandleType.Position;
        /// <summary>Current handle type (Position, Rotation, Scale, or combinations). Assigning rebuilds the child handles.</summary>
        public HandleType Type
        {
            get => _type;
            set
            {
                if (_type == value) return;
                _type = value;
                Clear();
                CreateHandles();
            }
        }

        /// <inheritdoc cref="Type"/>
        [Obsolete("Use Type instead.")]
        public HandleType type { get => Type; set => Type = value; }

        [SerializeField, FormerlySerializedAs("snappingType")] private SnappingType _snappingType = SnappingType.Relative;
        /// <summary>Snapping behavior type (Relative or Absolute).</summary>
        public SnappingType SnappingType { get => _snappingType; set => _snappingType = value; }

        /// <inheritdoc cref="SnappingType"/>
        [Obsolete("Use SnappingType instead.")]
        public SnappingType snappingType { get => SnappingType; set => SnappingType = value; }

        [SerializeField, FormerlySerializedAs("positionSnap")] private Vector3 _positionSnap = Vector3.zero;
        /// <summary>Position snapping values for each axis.</summary>
        public Vector3 PositionSnap { get => _positionSnap; set => _positionSnap = value; }

        /// <inheritdoc cref="PositionSnap"/>
        [Obsolete("Use PositionSnap instead.")]
        public Vector3 positionSnap { get => PositionSnap; set => PositionSnap = value; }

        [SerializeField, FormerlySerializedAs("rotationSnap")] private float _rotationSnap;
        /// <summary>Rotation snapping value in degrees.</summary>
        public float RotationSnap { get => _rotationSnap; set => _rotationSnap = value; }

        /// <inheritdoc cref="RotationSnap"/>
        [Obsolete("Use RotationSnap instead.")]
        public float rotationSnap { get => RotationSnap; set => RotationSnap = value; }

        [SerializeField, FormerlySerializedAs("scaleSnap")] private Vector3 _scaleSnap = Vector3.zero;
        /// <summary>Scale snapping values for each axis.</summary>
        public Vector3 ScaleSnap { get => _scaleSnap; set => _scaleSnap = value; }

        /// <inheritdoc cref="ScaleSnap"/>
        [Obsolete("Use ScaleSnap instead.")]
        public Vector3 scaleSnap { get => ScaleSnap; set => ScaleSnap = value; }

        /// <summary>Whether the handle keeps a constant size on screen regardless of camera distance.</summary>
        public bool AutoScale { get => _autoScale; set => _autoScale = value; }

        /// <inheritdoc cref="AutoScale"/>
        [Obsolete("Use AutoScale instead.")]
        public bool autoScale { get => AutoScale; set => AutoScale = value; }

        /// <summary>Camera used for raycasting and screen-to-world conversions. Read-only; set when the handle is enabled.</summary>
        public Camera HandleCamera { get; private set; }

        /// <inheritdoc cref="HandleCamera"/>
        [Obsolete("Use HandleCamera instead.")]
        public Camera handleCamera => HandleCamera;

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
            HandleCamera = Manager.MainCamera;
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

            if (!AutoScale || HandleCamera == null) return;
            transform.PreserveScaleOnScreen(HandleCamera.fieldOfView, autoScaleSizeInPixels, HandleCamera);
        }

        /// <summary>
        /// Enables the handle for a specific target transform.
        /// </summary>
        /// <param name="targetTransform">The transform to manipulate.</param>
        public virtual void Enable(Transform targetTransform)
        {
            Target = targetTransform;
            transform.position = targetTransform.position;

            CreateHandles();
        }

        /// <summary>
        /// Disables the handle and clears the target.
        /// </summary>
        public virtual void Disable()
        {
            Target = null;
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
        [Obsolete("Assign the 'Type' property instead; its setter rebuilds the child handles.")]
        public virtual void ChangeHandleType(HandleType handleType)
        {
            Type = handleType;
        }

        /// <summary>
        /// Changes the coordinate space for transformations.
        /// </summary>
        /// <param name="newSpace">The new coordinate space.</param>
        [Obsolete("Assign the 'Space' property instead; its setter applies the Scale-is-always-Self clamp.")]
        public virtual void ChangeHandleSpace(Space newSpace)
        {
            Space = newSpace;
        }

        /// <summary>
        /// Changes the active axes for the handle.
        /// </summary>
        /// <param name="handleAxes">The new axes configuration.</param>
        [Obsolete("Assign the 'Axes' property instead; its setter rebuilds the child handles.")]
        public virtual void ChangeAxes(HandleAxes handleAxes)
        {
            Axes = handleAxes;
        }

        protected virtual void UpdateHandleTransformation()
        {
            if (!Target) return;

            transform.position = Target.position;
            if (Space == Space.Self || Type == HandleType.Scale)
            {
                transform.rotation = Target.rotation;
            }
            else
            {
                transform.rotation = Quaternion.identity;
            }
        }

        protected virtual void CreateHandles()
        {
            switch (Type)
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
