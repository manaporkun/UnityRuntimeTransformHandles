using TransformHandles.Utils;
using UnityEngine;

namespace TransformHandles
{
    /// <summary>
    /// Handles uniform scale manipulation across all axes.
    /// </summary>
    public class ScaleGlobal : HandleBase
    {
        // Scale delta per pixel dragged. Chosen to match the previous feel at 60 fps
        // (the old code used 2f * Time.deltaTime, i.e. ~2/60 per pixel at 60 fps).
        private const float MouseSensitivity = 0.0333f;

        [SerializeField] private Color defaultColor;
        [SerializeField] private MeshRenderer cubeMeshRenderer;

        private Vector3 _axis;
        private Vector3 _startScale;
        private Material _cubeMaterial;

        /// <summary>
        /// Initializes the global scale component.
        /// </summary>
        /// <param name="handle">The parent handle.</param>
        /// <param name="axis">The axes to scale along.</param>
        public void Initialize(Handle handle, Vector3 axis)
        {
            ParentHandle = handle;
            _axis = axis;
            DefaultColor = defaultColor;

            // Instantiate the material once; Initialize is re-runnable via Handle.ChangeAxes
            // and MeshRenderer.material allocates a new instance on every access.
            if (_cubeMaterial == null) _cubeMaterial = cubeMeshRenderer.material;
        }

        /// <inheritdoc/>
        public override void Interact(Vector3 previousPosition)
        {
            var mouseVector = (Vector3)InputWrapper.MousePosition - previousPosition;
            // mouseVector is the per-frame pixel delta; the total scale change should depend on how
            // far the mouse moved, not on the frame rate. Multiplying by Time.deltaTime made scaling
            // speed framerate-dependent (higher fps -> smaller dt -> slower scaling). Drop it.
            var d = (mouseVector.x + mouseVector.y) * MouseSensitivity;
            delta += d;
            ParentHandle.target.localScale = _startScale + Vector3.Scale(_startScale, _axis) * delta;

            base.Interact(previousPosition);
        }

        /// <inheritdoc/>
        public override void StartInteraction(Vector3 hitPoint)
        {
            base.StartInteraction(hitPoint);
            _startScale = ParentHandle.target.localScale;
        }

        /// <inheritdoc/>
        public override void SetColor(Color color)
        {
            if (_cubeMaterial.color != color) _cubeMaterial.color = color;
        }

        /// <inheritdoc/>
        public override void SetDefaultColor()
        {
            if (_cubeMaterial.color != DefaultColor) _cubeMaterial.color = DefaultColor;
        }
    }
}
