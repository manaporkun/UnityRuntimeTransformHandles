using TransformHandles.Utils;
using UnityEngine;

namespace TransformHandles
{
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

        public void Initialize(HandleGroup handleGroup, Vector3 pAxis)
        {
            HandleGroup = handleGroup;
            _axis = pAxis;
            DefaultColor = defaultColor;
            
            _handleCamera = HandleGroup.handleCamera;

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
            var hitDirection = (hitPoint - HandleGroup.target.position).normalized;
            var   x            = Vector3.Dot(hitDirection, _tangent);
            var   y            = Vector3.Dot(hitDirection, _biTangent);
            var   angleRadians = Mathf.Atan2(y, x);
            var   angleDegrees = angleRadians * Mathf.Rad2Deg;

            if (HandleGroup.rotationSnap != 0)
            {
                angleDegrees = Mathf.Round(angleDegrees / HandleGroup.rotationSnap) * HandleGroup.rotationSnap;
                angleRadians = angleDegrees * Mathf.Deg2Rad;
            }

            if (HandleGroup.space == Space.Self)
            {
                HandleGroup.target.localRotation = _startRotation * Quaternion.AngleAxis(angleDegrees, _axis);
            }
            else
            {
                var invertedRotatedAxis = Quaternion.Inverse(_startRotation) * _axis;
                HandleGroup.target.rotation = _startRotation * Quaternion.AngleAxis(angleDegrees, invertedRotatedAxis);
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
            
            _startRotation = HandleGroup.space == Space.Self ? HandleGroup.target.localRotation : HandleGroup.target.rotation;
            

            if (HandleGroup.space == Space.Self)
            {
                _rotatedAxis = _startRotation * _axis;
            }
            else
            {
                _rotatedAxis = _axis;
            }

            _axisPlane = new Plane(_rotatedAxis, HandleGroup.target.position);

            var     cameraRay = _handleCamera.ScreenPointToRay(Input.mousePosition);
            var startHitPoint = _axisPlane.Raycast(cameraRay, out var hitT) ?
                cameraRay.GetPoint(hitT) : _axisPlane.ClosestPointOnPlane(pHitPoint);
            
            _tangent   = (startHitPoint - HandleGroup.target.position).normalized;
            _biTangent = Vector3.Cross(_rotatedAxis, _tangent);
        }
        
        public override void OnInteractionEnded()
        {
            base.OnInteractionEnded();
            Delta = 0;
        }

        private void DrawArc()
        {
            Graphics.DrawMesh(_arcMesh, Matrix4x4.identity, arcMaterial, gameObject.layer);
        }
        
        public override void SetColor(Color color)
        {
            torusMeshRenderer.material.color = color;
        }
        
        public override void SetDefaultColor()
        {
            torusMeshRenderer.material.color = DefaultColor;
        }
    }
}