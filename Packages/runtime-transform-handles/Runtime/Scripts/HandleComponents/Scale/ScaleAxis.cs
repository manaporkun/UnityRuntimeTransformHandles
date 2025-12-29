using TransformHandles.Utils;
using UnityEngine;

namespace TransformHandles
{
    /// <summary>
    /// Handles scale manipulation along a single axis.
    /// </summary>
    public class ScaleAxis : HandleBase
    {
        private const float ScaleCubeSize = 0.75f;

        [SerializeField] private Color defaultColor;
        [SerializeField] private MeshRenderer cubeMeshRenderer;
        [SerializeField] private MeshRenderer lineMeshRenderer;

        private Camera _handleCamera;

        private Vector3 _axis;
        private Vector3 _startScale;

        private float _interactionDistance;
        private Ray _rAxisRay;

        /// <summary>
        /// Initializes the scale axis component.
        /// </summary>
        /// <param name="handle">The parent handle.</param>
        /// <param name="axis">The axis of scaling.</param>
        public void Initialize(Handle handle, Vector3 axis)
        {
            ParentHandle = handle;
            _axis = axis;
            DefaultColor = defaultColor;

            _handleCamera = ParentHandle.handleCamera;
        }

        protected void Update()
        {
            lineMeshRenderer.transform.localScale = new Vector3(1, 1 + delta, 1);
            cubeMeshRenderer.transform.localPosition = _axis * (ScaleCubeSize * (1 + delta));
        }

        /// <inheritdoc/>
        public override void Interact(Vector3 previousPosition)
        {
            var cameraRay = _handleCamera.ScreenPointToRay(InputWrapper.MousePosition);

            var closestT = MathUtils.ClosestPointOnRay(_rAxisRay, cameraRay);
            var hitPoint = _rAxisRay.GetPoint(closestT);

            var distance = Vector3.Distance(ParentHandle.target.position, hitPoint);
            var axisScaleDelta = distance / _interactionDistance - 1f;

            var snapping = ParentHandle.scaleSnap;
            var snap = Mathf.Abs(Vector3.Dot(snapping, _axis));
            if (snap != 0)
            {
                if (ParentHandle.snappingType == SnappingType.Relative)
                {
                    axisScaleDelta = Mathf.Round(axisScaleDelta / snap) * snap;
                }
                else
                {
                    var axisStartScale = Mathf.Abs(Vector3.Dot(_startScale, _axis));
                    axisScaleDelta = Mathf.Round((axisScaleDelta + axisStartScale) / snap) * snap - axisStartScale;
                }
            }

            delta = axisScaleDelta;
            var scale = Vector3.Scale(_startScale, _axis * axisScaleDelta + Vector3.one);

            ParentHandle.target.localScale = scale;

            base.Interact(previousPosition);
        }

        /// <inheritdoc/>
        public override void StartInteraction(Vector3 hitPoint)
        {
            base.StartInteraction(hitPoint);
            _startScale = ParentHandle.target.localScale;

            var rAxis = GetRotatedAxis(_axis);

            var position = ParentHandle.target.position;
            _rAxisRay = new Ray(position, rAxis);

            var cameraRay = _handleCamera.ScreenPointToRay(InputWrapper.MousePosition);

            var closestT = MathUtils.ClosestPointOnRay(_rAxisRay, cameraRay);
            var rayHitPoint = _rAxisRay.GetPoint(closestT);

            _interactionDistance = Vector3.Distance(position, rayHitPoint);
        }

        /// <inheritdoc/>
        public override void SetColor(Color color)
        {
            cubeMeshRenderer.material.color = color;
            lineMeshRenderer.material.color = color;
        }

        /// <inheritdoc/>
        public override void SetDefaultColor()
        {
            cubeMeshRenderer.material.color = DefaultColor;
            lineMeshRenderer.material.color = DefaultColor;
        }
    }
}
