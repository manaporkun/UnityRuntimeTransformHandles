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
        private Transform _quadTransform;
        private Transform _cameraTransform;
        private Material _quadMaterial;

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
            _quadTransform = _quadGameObject.transform;
            _cameraTransform = _handleCamera.transform;

            // Instantiate the material once; Initialize is re-runnable via Handle.ChangeAxes
            // and MeshRenderer.material allocates a new instance on every access.
            if (_quadMaterial == null) _quadMaterial = quadMeshRenderer.material;

            _quadTransform.localPosition = (_axis1 + _axis2) * PlaneVisualOffset;
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
                offset.x = SnapUtils.Snap(offset.x, snapping.x);
                offset.y = SnapUtils.Snap(offset.y, snapping.y);
                offset.z = SnapUtils.Snap(offset.z, snapping.z);
            }

            var position = _startPosition + offset;

            if (snap != 0 && ParentHandle.snappingType == SnappingType.Absolute)
            {
                // Only snap the two in-plane axes; snapping the perpendicular axis jumped the
                // object off the drag plane. (>0.5 ignores float residuals on the perp axis.)
                if (Mathf.Abs(axis.x) > 0.5f) position.x = SnapUtils.Snap(position.x, snapping.x);
                if (Mathf.Abs(axis.y) > 0.5f) position.y = SnapUtils.Snap(position.y, snapping.y);
                if (Mathf.Abs(axis.z) > 0.5f) position.z = SnapUtils.Snap(position.z, snapping.z);
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

            _quadTransform.localPosition = (axis1 + axis2) * PlaneVisualOffset;
        }

        private bool _lastNotVisible;
        private bool _visibilitySet;

        private void LateUpdate()
        {
            var dot = Vector3.Dot(_quadTransform.up, _cameraTransform.forward);
            var notVisible = dot < -PlaneVisibilityDotThreshold || dot > PlaneVisibilityDotThreshold;
            if (_visibilitySet && notVisible == _lastNotVisible) return;
            _lastNotVisible = notVisible;
            _visibilitySet = true;
            _quadGameObject.SetActive(notVisible);
        }

        /// <inheritdoc/>
        public override void SetColor(Color color)
        {
            if (_quadMaterial.color != color) _quadMaterial.color = color;
        }

        /// <inheritdoc/>
        public override void SetDefaultColor()
        {
            if (_quadMaterial.color != DefaultColor) _quadMaterial.color = DefaultColor;
        }
    }
}
