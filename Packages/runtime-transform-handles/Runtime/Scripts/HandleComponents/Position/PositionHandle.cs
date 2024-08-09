using TransformHandles.Utils;
using UnityEngine;

namespace TransformHandles
{
    public class PositionHandle : HandleBase
    {
        [SerializeField] private MeshRenderer _coneMeshRenderer;
        [SerializeField] private MeshRenderer _lineMeshRenderer;

        private Camera _handleCamera;
        
        private Vector3 _startPosition;
        private Vector3 _axis;

        private Vector3 _interactionOffset;
        private Ray _rAxisRay;
        private GameObject _coneGameObject;
        private GameObject _lineGameObject;
        
        private Transform _coneTransform;
        private Transform _cameraTransform;
        
        public void Initialize(HandleGroup handleGroup)
        {
            HandleGroup = handleGroup;
            _handleCamera = HandleGroup._handleCamera;

            _coneGameObject = _coneMeshRenderer.gameObject;
            _lineGameObject = _lineMeshRenderer.gameObject;
            
            _coneTransform = _coneGameObject.transform;
            _cameraTransform = _handleCamera.transform;
            
            _axis = _coneTransform.up;
        }

        public override void OnInteractionActive(Vector3 pPreviousPosition)
        {
            var cameraRay = _handleCamera.ScreenPointToRay(Input.mousePosition);

            var closestT = MathUtils.ClosestPointOnRay(_rAxisRay, cameraRay);
            var hitPoint = _rAxisRay.GetPoint(closestT);
            
            var offset = hitPoint + _interactionOffset - _startPosition;
            
            var snapping = HandleGroup._positionSnap;
            var   snap     = Vector3.Scale(snapping, _axis).magnitude;
            if (snap != 0 && HandleGroup._snappingType == SnappingType.Relative)
            {
                offset = (Mathf.Round(offset.magnitude / snap) * snap) * offset.normalized; 
            }

            var position = _startPosition + offset;
            
            if (snap != 0 && HandleGroup._snappingType == SnappingType.Absolute)
            {
                if (snapping.x != 0) position.x = Mathf.Round(position.x / snapping.x) * snapping.x;
                if (snapping.y != 0) position.y = Mathf.Round(position.y / snapping.y) * snapping.y;
                if (snapping.z != 0) position.z = Mathf.Round(position.z / snapping.z) * snapping.z;
            }
            
            HandleGroup._target.position = position;

            base.OnInteractionActive(pPreviousPosition);
        }
        
        public override void OnInteractionStarted(Vector3 pHitPoint)
        {
            base.OnInteractionStarted(pHitPoint);
            
            _startPosition = HandleGroup._target.position;

            var rAxis = HandleGroup._space == Space.Self
                ? HandleGroup._target.rotation * _axis
                : _axis;
            
            _rAxisRay = new Ray(_startPosition, rAxis);

            var cameraRay = _handleCamera.ScreenPointToRay(Input.mousePosition);

            var closestT = MathUtils.ClosestPointOnRay(_rAxisRay, cameraRay);
            var hitPoint = _rAxisRay.GetPoint(closestT);
            
            _interactionOffset = _startPosition - hitPoint;
        }
        
        public override void SetColor(Color color)
        {
            _coneMeshRenderer.material.color = color;
            _lineMeshRenderer.material.color = color;
        }
        
        public override void SetDefaultColor()
        {
            _coneMeshRenderer.material.color = base.DefaultColor;
            _lineMeshRenderer.material.color = base.DefaultColor;
        }

        private void LateUpdate()
        {
            if (_coneTransform == null || _cameraTransform == null) return;
            
            var dot = Vector3.Dot(_coneTransform.up, _cameraTransform.forward);
            var notVisible = dot is < -.975f or > 0.975f;
            if (_lineGameObject != null) _lineGameObject.SetActive(!notVisible);
            if (_coneGameObject != null) _coneGameObject.SetActive(!notVisible);
        }
    }
}