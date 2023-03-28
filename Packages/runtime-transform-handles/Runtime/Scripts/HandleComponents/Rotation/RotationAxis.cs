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

        public void Initialize(Handle handle, Vector3 pAxis)
        {
            ParentHandle = handle;
            _axis = pAxis;
            DefaultColor = defaultColor;
            
            _handleCamera = ParentHandle.handleCamera;

            _rotationHandleTransform = transform.GetComponentInParent<Handle>().transform;
        }

        public override void Interact(Vector3 pPreviousPosition)
        {
            var cameraRay = _handleCamera.ScreenPointToRay(Input.mousePosition);
            
            if (!_axisPlane.Raycast(cameraRay, out var hitT))
            {
                base.Interact(pPreviousPosition);
                return;
            }
            
            var hitPoint     = cameraRay.GetPoint(hitT);
            var hitDirection = (hitPoint - ParentHandle.target.position).normalized;
            var   x            = Vector3.Dot(hitDirection, _tangent);
            var   y            = Vector3.Dot(hitDirection, _biTangent);
            var   angleRadians = Mathf.Atan2(y, x);
            var   angleDegrees = angleRadians * Mathf.Rad2Deg;

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

            base.Interact(pPreviousPosition);
        }

        public override void StartInteraction(Vector3 pHitPoint)
        {
            base.StartInteraction(pHitPoint);
            
            _startRotation = ParentHandle.space == Space.Self ? ParentHandle.target.localRotation : ParentHandle.target.rotation;
            

            if (ParentHandle.space == Space.Self)
            {
                _rotatedAxis = _startRotation * _axis;
            }
            else
            {
                _rotatedAxis = _axis;
            }

            _axisPlane = new Plane(_rotatedAxis, ParentHandle.target.position);

            var     cameraRay = _handleCamera.ScreenPointToRay(Input.mousePosition);
            var startHitPoint = _axisPlane.Raycast(cameraRay, out var hitT) ?
                cameraRay.GetPoint(hitT) : _axisPlane.ClosestPointOnPlane(pHitPoint);
            
            _tangent   = (startHitPoint - ParentHandle.target.position).normalized;
            _biTangent = Vector3.Cross(_rotatedAxis, _tangent);
        }
        
        public override void EndInteraction()
        {
            base.EndInteraction();
            delta = 0;
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