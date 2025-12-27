using TransformHandles.Utils;
using UnityEngine;

namespace TransformHandles
{
    /// <summary>
    /// Handles position manipulation along a single axis.
    /// </summary>
    public class PositionAxis : HandleBase
    {
        private const float AxisVisibilityDotThreshold = 0.975f;

        [SerializeField] private Color defaultColor;
        [SerializeField] private MeshRenderer coneMeshRenderer;
        [SerializeField] private MeshRenderer lineMeshRenderer;

        private Camera _handleCamera;

        private Vector3 _startPosition;
        private Vector3 _axis;

        private Vector3 _interactionOffset;
        private Ray _rAxisRay;
        private GameObject _coneGameObject;
        private GameObject _lineGameObject;

        private Transform _coneTransform;
        private Transform _cameraTransform;

        /// <summary>
        /// Initializes the position axis component.
        /// </summary>
        /// <param name="handle">The parent handle.</param>
        public void Initialize(Handle handle)
        {
            ParentHandle = handle;
            _handleCamera = ParentHandle.handleCamera;

            _coneGameObject = coneMeshRenderer.gameObject;
            _lineGameObject = lineMeshRenderer.gameObject;

            _coneTransform = _coneGameObject.transform;
            _cameraTransform = _handleCamera.transform;

            _axis = _coneTransform.up;
            DefaultColor = defaultColor;
        }

        /// <inheritdoc/>
        public override void Interact(Vector3 previousPosition)
        {
            var screenPosition = TransformHandleManager.GetPointerScreenPosition();
            var cameraRay = _handleCamera.ScreenPointToRay(screenPosition);
            var closestT = MathUtils.ClosestPointOnRay(_rAxisRay, cameraRay);
            var hitPoint = _rAxisRay.GetPoint(closestT);

            var offset = hitPoint + _interactionOffset - _startPosition;

            var snapping = ParentHandle.positionSnap;
            var snap = Vector3.Scale(snapping, _axis).magnitude;
            if (snap != 0 && ParentHandle.snappingType == SnappingType.Relative)
            {
                offset = (Mathf.Round(offset.magnitude / snap) * snap) * offset.normalized;
            }

            var position = _startPosition + offset;

            if (snap != 0 && ParentHandle.snappingType == SnappingType.Absolute)
            {
                if (snapping.x != 0) position.x = Mathf.Round(position.x / snapping.x) * snapping.x;
                if (snapping.y != 0) position.y = Mathf.Round(position.y / snapping.y) * snapping.y;
                if (snapping.z != 0) position.z = Mathf.Round(position.z / snapping.z) * snapping.z;
            }

            ParentHandle.target.position = position;

            base.Interact(previousPosition);
        }

        /// <inheritdoc/>
        public override void StartInteraction(Vector3 hitPoint)
        {
            base.StartInteraction(hitPoint);

            _startPosition = ParentHandle.target.position;

            var rAxis = GetRotatedAxis(_axis);

            _rAxisRay = new Ray(_startPosition, rAxis);

            var screenPosition = TransformHandleManager.GetPointerScreenPosition();
            var cameraRay = _handleCamera.ScreenPointToRay(screenPosition);
            var closestT = MathUtils.ClosestPointOnRay(_rAxisRay, cameraRay);
            var rayHitPoint = _rAxisRay.GetPoint(closestT);

            _interactionOffset = _startPosition - rayHitPoint;
        }

        /// <inheritdoc/>
        public override void SetColor(Color color)
        {
            coneMeshRenderer.material.color = color;
            lineMeshRenderer.material.color = color;
        }

        /// <inheritdoc/>
        public override void SetDefaultColor()
        {
            coneMeshRenderer.material.color = DefaultColor;
            lineMeshRenderer.material.color = DefaultColor;
        }

        private void LateUpdate()
        {
            var dot = Vector3.Dot(_coneTransform.up, _cameraTransform.forward);
            var notVisible = dot < -AxisVisibilityDotThreshold || dot > AxisVisibilityDotThreshold;
            _lineGameObject.SetActive(!notVisible);
            _coneGameObject.SetActive(!notVisible);
        }
    }
}
