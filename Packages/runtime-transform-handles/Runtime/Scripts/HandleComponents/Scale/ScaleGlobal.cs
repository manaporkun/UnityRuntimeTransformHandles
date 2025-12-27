using UnityEngine;

namespace TransformHandles
{
    /// <summary>
    /// Handles uniform scale manipulation across all axes.
    /// </summary>
    public class ScaleGlobal : HandleBase
    {
        private const float MouseSensitivity = 2f;

        [SerializeField] private Color defaultColor;
        [SerializeField] private MeshRenderer cubeMeshRenderer;

        private Vector3 _axis;
        private Vector3 _startScale;

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
        }

        /// <inheritdoc/>
        public override void Interact(Vector3 previousPosition)
        {
            var screenPosition = TransformHandleManager.GetPointerScreenPosition();
            var mouseVector = screenPosition - previousPosition;
            var d = (mouseVector.x + mouseVector.y) * Time.deltaTime * MouseSensitivity;
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
            cubeMeshRenderer.material.color = color;
        }

        /// <inheritdoc/>
        public override void SetDefaultColor()
        {
            cubeMeshRenderer.material.color = DefaultColor;
        }
    }
}
