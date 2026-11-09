using TransformHandles.Utils;
using UnityEngine;

namespace TransformHandles
{
    /// <summary>
    /// Handles rotation manipulation around a single axis.
    /// </summary>
    public class RotationAxis : HandleBase
    {
        [SerializeField] private Color defaultColor;
        [SerializeField] private Material arcMaterial;
        [SerializeField] private MeshRenderer torusMeshRenderer;

        private Camera _handleCamera;

        private Mesh _arcMesh;
        private Vector3 _axis;
        private Vector3 _rotatedAxis;
        private Plane _axisPlane;
        private Vector3 _tangent;
        private Vector3 _biTangent;

        private Quaternion _startRotation;
        private Vector2? _dragAngleLimits;
        private float _previousPointerAngle;
        private float _limitedAngle;
        private bool _limitsLocked;

        /// <summary>Optional angle bounds in degrees relative to the start of a drag.
        /// Both bounds must be finite, within [-180, 180], and contain zero.
        /// Configure before a drag or in InteractionStart. Changes after the first interaction
        /// step throw InvalidOperationException until EndInteraction. Null preserves unrestricted rotation.
        /// Limits apply after snapping; a boundary can therefore be off the snap grid.</summary>
        public Vector2? DragAngleLimits
        {
            get => _dragAngleLimits;
            set
            {
                if (_limitsLocked)
                    throw new System.InvalidOperationException("Configure angle limits before the first interaction step.");
                if (value.HasValue)
                {
                    var limits = value.Value;
                    if (float.IsNaN(limits.x) || float.IsInfinity(limits.x) ||
                        float.IsNaN(limits.y) || float.IsInfinity(limits.y) ||
                        limits.x > 0 || limits.y < 0 || limits.x < -180 || limits.y > 180)
                        throw new System.ArgumentOutOfRangeException(nameof(value),
                            "Drag angle limits must be finite, within [-180, 180], and contain zero.");
                }
                _dragAngleLimits = value;
            }
        }

        private Transform _rotationHandleTransform;
        private Material _torusMaterial;

        /// <summary>
        /// Initializes the rotation axis component.
        /// </summary>
        /// <param name="handle">The parent handle.</param>
        /// <param name="axis">The axis of rotation.</param>
        public void Initialize(Handle handle, Vector3 axis)
        {
            ParentHandle = handle;
            _axis = axis;
            DefaultColor = defaultColor;

            _handleCamera = ParentHandle.HandleCamera;

            // ParentHandle was set above; reuse its transform instead of walking the hierarchy again.
            _rotationHandleTransform = ParentHandle.transform;

            // Instantiate the material once; Initialize is re-runnable via Handle.ChangeAxes
            // and MeshRenderer.material allocates a new instance on every access.
            if (_torusMaterial == null) _torusMaterial = torusMeshRenderer.material;
        }

        private bool EnsureCamera()
        {
            _handleCamera = ParentHandle != null ? ParentHandle.HandleCamera : null;
            return _handleCamera != null;
        }

        /// <inheritdoc/>
        public override void Interact(Vector3 previousPosition)
        {
            if (!EnsureCamera()) return;
            _limitsLocked = true;

            var cameraRay = _handleCamera.ScreenPointToRay(InputWrapper.MousePosition);

            if (!_axisPlane.Raycast(cameraRay, out var hitT))
            {
                base.Interact(previousPosition);
                return;
            }

            var hitPoint = cameraRay.GetPoint(hitT);
            var hitDirection = (hitPoint - ParentHandle.Pivot.position).normalized;
            var x = Vector3.Dot(hitDirection, _tangent);
            var y = Vector3.Dot(hitDirection, _biTangent);
            var angleRadians = Mathf.Atan2(y, x);
            var angleDegrees = angleRadians * Mathf.Rad2Deg;

            if (ParentHandle.RotationSnap != 0)
            {
                angleDegrees = SnapUtils.Snap(angleDegrees, ParentHandle.RotationSnap);
                angleRadians = angleDegrees * Mathf.Deg2Rad;
            }

            if (_dragAngleLimits.HasValue)
            {
                _limitedAngle = Mathf.Clamp(_limitedAngle + Mathf.DeltaAngle(_previousPointerAngle, angleDegrees),
                    _dragAngleLimits.Value.x, _dragAngleLimits.Value.y);
                _previousPointerAngle = angleDegrees;
                angleDegrees = _limitedAngle;
                angleRadians = angleDegrees * Mathf.Deg2Rad;
            }

            if (ParentHandle.Space == Space.Self)
            {
                ParentHandle.Pivot.localRotation = _startRotation * Quaternion.AngleAxis(angleDegrees, _axis);
            }
            else
            {
                var invertedRotatedAxis = Quaternion.Inverse(_startRotation) * _axis;
                ParentHandle.Pivot.rotation = _startRotation * Quaternion.AngleAxis(angleDegrees, invertedRotatedAxis);
            }

            // Reuse a single Mesh instead of allocating a new one each frame. CreateArc would
            // otherwise leak a native Mesh every frame of the drag (the previous one is never freed).
            if (_arcMesh == null) _arcMesh = new Mesh { name = "RotationArc" };
            MeshUtils.RebuildArc(_arcMesh, transform.position, HitPoint, _rotatedAxis,
                _rotationHandleTransform.localScale.x, angleRadians,
                Mathf.Abs(Mathf.CeilToInt(angleDegrees)) + 1);
            DrawArc();

            base.Interact(previousPosition);
        }

        /// <inheritdoc/>
        public override void StartInteraction(Vector3 hitPoint)
        {
            _limitsLocked = false;
            _previousPointerAngle = _limitedAngle = 0;
            base.StartInteraction(hitPoint);
            if (!EnsureCamera()) return;

            _startRotation = ParentHandle.Space == Space.Self
                ? ParentHandle.Pivot.localRotation
                : ParentHandle.Pivot.rotation;

            _rotatedAxis = ParentHandle.Space == Space.Self
                ? _startRotation * _axis
                : _axis;

            _axisPlane = new Plane(_rotatedAxis, ParentHandle.Pivot.position);

            var cameraRay = _handleCamera.ScreenPointToRay(InputWrapper.MousePosition);
            var startHitPoint = _axisPlane.Raycast(cameraRay, out var hitT)
                ? cameraRay.GetPoint(hitT)
                : _axisPlane.ClosestPointOnPlane(hitPoint);

            _tangent = (startHitPoint - ParentHandle.Pivot.position).normalized;
            _biTangent = Vector3.Cross(_rotatedAxis, _tangent);
        }

        /// <inheritdoc/>
        public override void EndInteraction()
        {
            _limitsLocked = false;
            base.EndInteraction();
            Delta = 0;
        }

        private void DrawArc()
        {
            Graphics.DrawMesh(_arcMesh, Matrix4x4.identity, arcMaterial, gameObject.layer);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_arcMesh != null) Destroy(_arcMesh);
            // Destroy the material instance created in Initialize; renderer.material clones
            // leak per handle create/destroy cycle otherwise.
            if (_torusMaterial != null) Destroy(_torusMaterial);
        }

        /// <inheritdoc/>
        public override void SetColor(Color color)
        {
            if (_torusMaterial.color != color) _torusMaterial.color = color;
        }

        /// <inheritdoc/>
        public override void SetDefaultColor()
        {
            if (_torusMaterial.color != DefaultColor) _torusMaterial.color = DefaultColor;
        }
    }
}
