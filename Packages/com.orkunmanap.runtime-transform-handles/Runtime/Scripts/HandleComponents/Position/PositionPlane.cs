using TransformHandles.Utils;
using UnityEngine;

namespace TransformHandles
{
    /// <summary>
    /// Handles position manipulation along a plane defined by two axes.
    /// </summary>
    public class PositionPlane : HandleBase
    {
        private const float PlaneVisibilityDotThreshold = 0.25f;
        private const float PlaneVisualOffset = 0.2f;
        private const float CameraAngleThreshold = 90f;

        [SerializeField] private Color defaultColor;
        [SerializeField] private MeshRenderer quadMeshRenderer;

        private Camera _handleCamera;

        private Vector3 _startPosition;
        private Vector3 _axis1;
        private Vector3 _axis2;
        private Vector3 _perp;
        private Plane _plane;
        private Vector3 _interactionOffset;

        private GameObject _quadGameObject;
        private Transform _cameraTransform;

        /// <summary>
        /// Initializes the position plane component.
        /// </summary>
        /// <param name="handle">The parent handle.</param>
        /// <param name="axis1">The first axis of the plane.</param>
        /// <param name="axis2">The second axis of the plane.</param>
        /// <param name="perp">The perpendicular axis to the plane.</param>
        public void Initialize(Handle handle, Vector3 axis1, Vector3 axis2, Vector3 perp)
        {
            ParentHandle = handle;
            _axis1 = axis1;
            _axis2 = axis2;
            _perp = perp;

            _handleCamera = ParentHandle.handleCamera;

            DefaultColor = defaultColor;

            _quadGameObject = quadMeshRenderer.gameObject;
            _cameraTransform = _handleCamera.transform;

            _quadGameObject.transform.localPosition = (_axis1 + _axis2) * PlaneVisualOffset;
        }

        /// <inheritdoc/>
        public override void Interact(Vector3 previousPosition)
        {
            var ray = _handleCamera.ScreenPointToRay(InputWrapper.MousePosition);

            _plane.Raycast(ray, out var d);

            var hitPoint = ray.GetPoint(d);

            var offset = hitPoint + _interactionOffset - _startPosition;

            var axis = _axis1 + _axis2;
            var snapping = ParentHandle.positionSnap;

            var snap = Vector3.Scale(snapping, axis).magnitude;
            if (snap != 0 && ParentHandle.snappingType == SnappingType.Relative)
            {
                if (snapping.x != 0) offset.x = Mathf.Round(offset.x / snapping.x) * snapping.x;
                if (snapping.y != 0) offset.y = Mathf.Round(offset.y / snapping.y) * snapping.y;
                if (snapping.z != 0) offset.z = Mathf.Round(offset.z / snapping.z) * snapping.z;
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
            var rPerp = GetRotatedAxis(_perp);

            var position = ParentHandle.target.position;
            _plane = new Plane(rPerp, position);

            var ray = _handleCamera.ScreenPointToRay(InputWrapper.MousePosition);

            _plane.Raycast(ray, out var d);

            var rayHitPoint = ray.GetPoint(d);
            _startPosition = position;
            _interactionOffset = _startPosition - rayHitPoint;
        }

        private void Update()
        {
            if (_handleCamera == null) return;

            var axis1 = _axis1;
            var rAxis1 = GetRotatedAxis(axis1);
            var angle1 = Vector3.Angle(_cameraTransform.forward, rAxis1);
            if (angle1 < CameraAngleThreshold)
                axis1 = -axis1;

            var axis2 = _axis2;
            var rAxis2 = GetRotatedAxis(axis2);
            var angle2 = Vector3.Angle(_cameraTransform.forward, rAxis2);
            if (angle2 < CameraAngleThreshold)
                axis2 = -axis2;

            _quadGameObject.transform.localPosition = (axis1 + axis2) * PlaneVisualOffset;
        }

        private void LateUpdate()
        {
            var dot = Vector3.Dot(_quadGameObject.transform.up, _cameraTransform.forward);
            var notVisible = dot < -PlaneVisibilityDotThreshold || dot > PlaneVisibilityDotThreshold;
            _quadGameObject.SetActive(notVisible);
        }

        /// <inheritdoc/>
        public override void SetColor(Color color)
        {
            quadMeshRenderer.material.color = color;
        }

        /// <inheritdoc/>
        public override void SetDefaultColor()
        {
            quadMeshRenderer.material.color = DefaultColor;
        }
    }
}
