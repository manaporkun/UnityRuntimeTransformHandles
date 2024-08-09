using TransformHandles.Utils;
using UnityEngine;
using UnityEngine.Serialization;

namespace TransformHandles
{
    public class RotationHandle : HandleBase
    {
        [SerializeField] private Material _arcMaterial;
        [SerializeField] private MeshRenderer _torusMeshRenderer;
        
        private Camera _handleCamera;
        
        private Mesh _arcMesh;
        private Vector3 _axis;
        private Vector3 _rotatedAxis;
        private Plane _axisPlane;
        private Vector3 _tangent;
        private Vector3 _biTangent;
        
        private Quaternion _startRotation;

        private Transform _rotationHandleTransform;

        public void Initialize(HandleGroup handleGroup, Vector3 pAxis)
        {
            HandleGroup = handleGroup;
            _axis = pAxis;
            
            _handleCamera = HandleGroup._handleCamera;

            _rotationHandleTransform = transform.GetComponentInParent<HandleGroup>().transform;
        }

        public override void OnInteractionActive(Vector3 pPreviousPosition)
        {
            var cameraRay = _handleCamera.ScreenPointToRay(Input.mousePosition);
            
            if (!_axisPlane.Raycast(cameraRay, out var hitT))
            {
                base.OnInteractionActive(pPreviousPosition);
                return;
            }
            
            var hitPoint     = cameraRay.GetPoint(hitT);
            var hitDirection = (hitPoint - HandleGroup._target.position).normalized;
            var   x            = Vector3.Dot(hitDirection, _tangent);
            var   y            = Vector3.Dot(hitDirection, _biTangent);
            var   angleRadians = Mathf.Atan2(y, x);
            var   angleDegrees = angleRadians * Mathf.Rad2Deg;

            if (HandleGroup._rotationSnap != 0)
            {
                angleDegrees = Mathf.Round(angleDegrees / HandleGroup._rotationSnap) * HandleGroup._rotationSnap;
                angleRadians = angleDegrees * Mathf.Deg2Rad;
            }

            if (HandleGroup._space == Space.Self)
            {
                HandleGroup._target.localRotation = _startRotation * Quaternion.AngleAxis(angleDegrees, _axis);
            }
            else
            {
                var invertedRotatedAxis = Quaternion.Inverse(_startRotation) * _axis;
                HandleGroup._target.rotation = _startRotation * Quaternion.AngleAxis(angleDegrees, invertedRotatedAxis);
            }
            _arcMesh = MeshUtils.CreateArc(transform.position, HitPoint, _rotatedAxis, 
                _rotationHandleTransform.localScale.x, angleRadians, 
                Mathf.Abs(Mathf.CeilToInt(angleDegrees)) + 1);
            DrawArc();

            base.OnInteractionActive(pPreviousPosition);
        }

        public override void OnInteractionStarted(Vector3 pHitPoint)
        {
            base.OnInteractionStarted(pHitPoint);
            
            _startRotation = HandleGroup._space == Space.Self ? HandleGroup._target.localRotation : HandleGroup._target.rotation;

            if (HandleGroup._space == Space.Self)
            {
                _rotatedAxis = _startRotation * _axis;
            }
            else
            {
                _rotatedAxis = _axis;
            }

            _axisPlane = new Plane(_rotatedAxis, HandleGroup._target.position);

            var     cameraRay = _handleCamera.ScreenPointToRay(Input.mousePosition);
            var startHitPoint = _axisPlane.Raycast(cameraRay, out var hitT) ?
                cameraRay.GetPoint(hitT) : _axisPlane.ClosestPointOnPlane(pHitPoint);
            
            _tangent   = (startHitPoint - HandleGroup._target.position).normalized;
            _biTangent = Vector3.Cross(_rotatedAxis, _tangent);
        }
        
        public override void OnInteractionEnded()
        {
            base.OnInteractionEnded();
            delta = 0;
        }

        private void DrawArc()
        {
            Graphics.DrawMesh(_arcMesh, Matrix4x4.identity, _arcMaterial, gameObject.layer);
        }
        
        public override void SetColor(Color color)
        {
            _torusMeshRenderer.material.color = color;
        }
        
        public override void SetDefaultColor()
        {
            _torusMeshRenderer.material.color = DefaultColor;
        }
    }
}