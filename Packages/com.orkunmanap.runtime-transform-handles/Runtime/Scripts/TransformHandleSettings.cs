using UnityEngine;

namespace TransformHandles
{
    /// <summary>
    /// ScriptableObject containing configuration settings for Transform Handles.
    /// Create via Assets > Create > Transform Handles > Settings.
    /// </summary>
    [CreateAssetMenu(fileName = "TransformHandleSettings", menuName = "Transform Handles/Settings", order = 1)]
    public class TransformHandleSettings : ScriptableObject
    {
        [Header("Keyboard Shortcuts")]
        [Tooltip("Enable or disable all keyboard shortcuts")]
        [SerializeField] private bool enableShortcuts = true;

        [Tooltip("Key to switch to Position handle mode")]
        [SerializeField] private KeyCode positionKey = KeyCode.W;

        [Tooltip("Key to switch to Rotation handle mode")]
        [SerializeField] private KeyCode rotationKey = KeyCode.E;

        [Tooltip("Key to switch to Scale handle mode")]
        [SerializeField] private KeyCode scaleKey = KeyCode.R;

        [Tooltip("Key to switch to All handles mode (Position + Rotation + Scale)")]
        [SerializeField] private KeyCode allKey = KeyCode.A;

        [Tooltip("Key to toggle between World and Local coordinate space")]
        [SerializeField] private KeyCode spaceToggleKey = KeyCode.X;

        [Tooltip("Key to toggle between Pivot and Center origin")]
        [SerializeField] private KeyCode pivotToggleKey = KeyCode.Z;

        [Header("Visual Settings")]
        [Tooltip("Color used when hovering over a handle")]
        [SerializeField] private Color highlightColor = Color.white;

        [Tooltip("Color for the X axis (default: red)")]
        [SerializeField] private Color xAxisColor = new Color(1f, 0.2f, 0.2f, 1f);

        [Tooltip("Color for the Y axis (default: green)")]
        [SerializeField] private Color yAxisColor = new Color(0.2f, 1f, 0.2f, 1f);

        [Tooltip("Color for the Z axis (default: blue)")]
        [SerializeField] private Color zAxisColor = new Color(0.2f, 0.6f, 1f, 1f);

        [Tooltip("Color for the global/center handle (default: orange)")]
        [SerializeField] private Color globalHandleColor = new Color(1f, 0.6f, 0f, 1f);

        [Tooltip("Scale multiplier for handle size")]
        [Range(0.5f, 3f)]
        [SerializeField] private float handleScale = 1f;

        [Header("Default Handle Settings")]
        [Tooltip("Default handle type for new handles")]
        [SerializeField] private HandleType defaultHandleType = HandleType.Position;

        [Tooltip("Default coordinate space for new handles")]
        [SerializeField] private Space defaultSpace = Space.Self;

        [Tooltip("Default axes for new handles")]
        [SerializeField] private HandleAxes defaultAxes = HandleAxes.XYZ;

        [Tooltip("Enable auto-scaling handles based on camera distance")]
        [SerializeField] private bool autoScaleHandles = true;

        /// <summary>Whether all keyboard shortcuts are enabled.</summary>
        public bool EnableShortcuts => enableShortcuts;
        /// <summary>Key to switch to Position handle mode.</summary>
        public KeyCode PositionKey => positionKey;
        /// <summary>Key to switch to Rotation handle mode.</summary>
        public KeyCode RotationKey => rotationKey;
        /// <summary>Key to switch to Scale handle mode.</summary>
        public KeyCode ScaleKey => scaleKey;
        /// <summary>Key to switch to All handles mode (Position + Rotation + Scale).</summary>
        public KeyCode AllKey => allKey;
        /// <summary>Key to toggle between World and Local coordinate space.</summary>
        public KeyCode SpaceToggleKey => spaceToggleKey;
        /// <summary>Key to toggle between Pivot and Center origin.</summary>
        public KeyCode PivotToggleKey => pivotToggleKey;

        /// <summary>Color used when hovering over a handle.</summary>
        public Color HighlightColor => highlightColor;
        /// <summary>Color for the X axis.</summary>
        public Color XAxisColor => xAxisColor;
        /// <summary>Color for the Y axis.</summary>
        public Color YAxisColor => yAxisColor;
        /// <summary>Color for the Z axis.</summary>
        public Color ZAxisColor => zAxisColor;
        /// <summary>Color for the global/center handle.</summary>
        public Color GlobalHandleColor => globalHandleColor;
        /// <summary>Scale multiplier for handle size.</summary>
        public float HandleScale => handleScale;

        /// <summary>Default handle type for new handles.</summary>
        public HandleType DefaultHandleType => defaultHandleType;
        /// <summary>Default coordinate space for new handles.</summary>
        public Space DefaultSpace => defaultSpace;
        /// <summary>Default axes for new handles.</summary>
        public HandleAxes DefaultAxes => defaultAxes;
        /// <summary>Whether handles auto-scale to keep a constant screen size.</summary>
        public bool AutoScaleHandles => autoScaleHandles;

        /// <summary>
        /// Creates a default settings instance with standard values.
        /// Useful for runtime initialization when no asset is assigned.
        /// </summary>
        public static TransformHandleSettings CreateDefault()
        {
            var settings = CreateInstance<TransformHandleSettings>();
            settings.name = "DefaultSettings";
            return settings;
        }
    }
}
