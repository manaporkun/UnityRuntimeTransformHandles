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

        private Transform _rotationHandleTransform;

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

            _handleCamera = ParentHandle.handleCamera;

            _rotationHandleTransform = transform.GetComponentInParent<Handle>().transform;
        }

        /// <inheritdoc/>
        public override void Interact(Vector3 previousPosition)
        {
            var cameraRay = _handleCamera.ScreenPointToRay(Input.mousePosition);

            if (!_axisPlane.Raycast(cameraRay, out var hitT))
            {
                base.Interact(previousPosition);
                return;
            }

            var hitPoint = cameraRay.GetPoint(hitT);
            var hitDirection = (hitPoint - ParentHandle.target.position).normalized;
            var x = Vector3.Dot(hitDirection, _tangent);
            var y = Vector3.Dot(hitDirection, _biTangent);
            var angleRadians = Mathf.Atan2(y, x);
            var angleDegrees = angleRadians * Mathf.Rad2Deg;

            if (ParentHandle.rotationSnap != 0)
            {
                angleDegrees = Mathf.Round(angleDegrees / ParentHandle.rotationSnap) * ParentHandle.rotationSnap;
                angleRadians = angleDegrees * Mathf.Deg2Rad;
            }

            if (ParentHandle.space == Space.Self)
            {
                ParentHandle.target.localRotation = _startRotation * Quaternion.AngleAxis(angleDegrees, _axis);
            }
            else
            {
                var invertedRotatedAxis = Quaternion.Inverse(_startRotation) * _axis;
                ParentHandle.target.rotation = _startRotation * Quaternion.AngleAxis(angleDegrees, invertedRotatedAxis);
            }

            _arcMesh = MeshUtils.CreateArc(transform.position, HitPoint, _rotatedAxis,
                _rotationHandleTransform.localScale.x, angleRadians,
                Mathf.Abs(Mathf.CeilToInt(angleDegrees)) + 1);
            DrawArc();

            base.Interact(previousPosition);
        }

        /// <inheritdoc/>
        public override void StartInteraction(Vector3 hitPoint)
        {
            base.StartInteraction(hitPoint);

            _startRotation = ParentHandle.space == Space.Self
                ? ParentHandle.target.localRotation
                : ParentHandle.target.rotation;

            _rotatedAxis = ParentHandle.space == Space.Self
                ? _startRotation * _axis
                : _axis;

            _axisPlane = new Plane(_rotatedAxis, ParentHandle.target.position);

            var cameraRay = _handleCamera.ScreenPointToRay(Input.mousePosition);
            var startHitPoint = _axisPlane.Raycast(cameraRay, out var hitT)
                ? cameraRay.GetPoint(hitT)
                : _axisPlane.ClosestPointOnPlane(hitPoint);

            _tangent = (startHitPoint - ParentHandle.target.position).normalized;
            _biTangent = Vector3.Cross(_rotatedAxis, _tangent);
        }

        /// <inheritdoc/>
        public override void EndInteraction()
        {
            base.EndInteraction();
            delta = 0;
        }

        private void DrawArc()
        {
            Graphics.DrawMesh(_arcMesh, Matrix4x4.identity, arcMaterial, gameObject.layer);
        }

        /// <inheritdoc/>
        public override void SetColor(Color color)
        {
            torusMeshRenderer.material.color = color;
        }

        /// <inheritdoc/>
        public override void SetDefaultColor()
        {
            torusMeshRenderer.material.color = DefaultColor;
        }
    }
}
