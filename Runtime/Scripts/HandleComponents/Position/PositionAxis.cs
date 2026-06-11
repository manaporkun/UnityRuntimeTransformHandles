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

        private Material _coneMaterial;
        private Material _lineMaterial;

        /// <summary>
        /// Initializes the position axis component.
        /// </summary>
        /// <param name="handle">The parent handle.</param>
        public void Initialize(Handle handle)
        {
            ParentHandle = handle;
            _handleCamera = ParentHandle.HandleCamera;

            _coneGameObject = coneMeshRenderer.gameObject;
            _lineGameObject = lineMeshRenderer.gameObject;

            _coneTransform = _coneGameObject.transform;
            _cameraTransform = _handleCamera.transform;

            // Instantiate materials once; Initialize is re-runnable via Handle.ChangeAxes
            // and MeshRenderer.material allocates a new instance on every access.
            if (_coneMaterial == null) _coneMaterial = coneMeshRenderer.material;
            if (_lineMaterial == null) _lineMaterial = lineMeshRenderer.material;

            // Capture the axis in the handle-local frame so it stays canonical regardless of the
            // handle's current world rotation. Using the world-space cone.up directly meant that
            // re-running Initialize (Handle.ChangeAxes/ChangeHandleType) while the handle was already
            // rotated in Self space stored an already-rotated axis, which GetRotatedAxis then rotated
            // a second time -> the drag axis no longer matched the gizmo. Normalized for the
            // unit-direction precondition of MathUtils.ClosestPointOnRay.
            _axis = ParentHandle.transform.InverseTransformDirection(_coneTransform.up).normalized;
            DefaultColor = defaultColor;

            // Initialize re-runs via Handle.ChangeAxes, which re-enables the axis GameObject but
            // leaves the cone/line children's active state and this cache as they were. Clear the
            // cache so the next LateUpdate re-asserts visibility instead of early-returning stale.
            _visibilitySet = false;
        }

        /// <inheritdoc/>
        public override void Interact(Vector3 previousPosition)
        {
            var cameraRay = _handleCamera.ScreenPointToRay(InputWrapper.MousePosition);

            var closestT = MathUtils.ClosestPointOnRay(_rAxisRay, cameraRay);
            var hitPoint = _rAxisRay.GetPoint(closestT);

            var offset = hitPoint + _interactionOffset - _startPosition;

            var snapping = ParentHandle.PositionSnap;
            var snap = Vector3.Scale(snapping, _axis).magnitude;
            if (snap != 0 && ParentHandle.SnappingType == SnappingType.Relative)
            {
                offset = SnapUtils.Snap(offset.magnitude, snap) * offset.normalized;
            }

            var position = _startPosition + offset;

            if (snap != 0 && ParentHandle.SnappingType == SnappingType.Absolute)
            {
                // Only snap the axis this handle controls. Snapping all three yanked the
                // perpendicular axes onto the grid, so dragging X jumped the object in Y/Z.
                // (>0.5 threshold ignores ~1e-7 residuals from the cone's 90-degree rotations.)
                if (Mathf.Abs(_axis.x) > 0.5f) position.x = SnapUtils.Snap(position.x, snapping.x);
                if (Mathf.Abs(_axis.y) > 0.5f) position.y = SnapUtils.Snap(position.y, snapping.y);
                if (Mathf.Abs(_axis.z) > 0.5f) position.z = SnapUtils.Snap(position.z, snapping.z);
            }

            ParentHandle.Pivot.position = position;

            base.Interact(previousPosition);
        }

        /// <inheritdoc/>
        public override void StartInteraction(Vector3 hitPoint)
        {
            base.StartInteraction(hitPoint);

            _startPosition = ParentHandle.Pivot.position;

            var rAxis = GetRotatedAxis(_axis);

            _rAxisRay = new Ray(_startPosition, rAxis);

            var cameraRay = _handleCamera.ScreenPointToRay(InputWrapper.MousePosition);

            var closestT = MathUtils.ClosestPointOnRay(_rAxisRay, cameraRay);
            var rayHitPoint = _rAxisRay.GetPoint(closestT);

            _interactionOffset = _startPosition - rayHitPoint;
        }

        /// <inheritdoc/>
        public override void SetColor(Color color)
        {
            if (_coneMaterial.color != color) _coneMaterial.color = color;
            if (_lineMaterial.color != color) _lineMaterial.color = color;
        }

        /// <inheritdoc/>
        public override void SetDefaultColor()
        {
            if (_coneMaterial.color != DefaultColor) _coneMaterial.color = DefaultColor;
            if (_lineMaterial.color != DefaultColor) _lineMaterial.color = DefaultColor;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            // Destroy the material instances created in Initialize; renderer.material clones
            // leak per handle create/destroy cycle otherwise.
            if (_coneMaterial != null) Destroy(_coneMaterial);
            if (_lineMaterial != null) Destroy(_lineMaterial);
        }

        private bool _lastVisible;
        private bool _visibilitySet;

        private void LateUpdate()
        {
            var dot = Vector3.Dot(_coneTransform.up, _cameraTransform.forward);
            var visible = dot >= -AxisVisibilityDotThreshold && dot <= AxisVisibilityDotThreshold;
            if (_visibilitySet && visible == _lastVisible) return;
            _lastVisible = visible;
            _visibilitySet = true;
            _lineGameObject.SetActive(visible);
            _coneGameObject.SetActive(visible);
        }
    }
}
