using TransformHandles.Utils;
using UnityEngine;

namespace TransformHandles
{
    public class PositionAxis : HandleBase
    {
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
        
        public void Initialize(HandleGroup handleGroup)
        {
            HandleGroup = handleGroup;
            _handleCamera = HandleGroup.handleCamera;

            _coneGameObject = coneMeshRenderer.gameObject;
            _lineGameObject = lineMeshRenderer.gameObject;
            
            _coneTransform = _coneGameObject.transform;
            _cameraTransform = _handleCamera.transform;
            
            _axis = _coneTransform.up;
            DefaultColor = defaultColor;
        }

        public override void OnInteractionActive(Vector3 pPreviousPosition)
        {
            var cameraRay = _handleCamera.ScreenPointToRay(Input.mousePosition);

            var closestT = MathUtils.ClosestPointOnRay(_rAxisRay, cameraRay);
            var hitPoint = _rAxisRay.GetPoint(closestT);
            
            var offset = hitPoint + _interactionOffset - _startPosition;
            
            var snapping = HandleGroup.positionSnap;
            var   snap     = Vector3.Scale(snapping, _axis).magnitude;
            if (snap != 0 && HandleGroup.snappingType == SnappingType.Relative)
            {
                offset = (Mathf.Round(offset.magnitude / snap) * snap) * offset.normalized; 
            }

            var position = _startPosition + offset;
            
            if (snap != 0 && HandleGroup.snappingType == SnappingType.Absolute)
            {
                if (snapping.x != 0) position.x = Mathf.Round(position.x / snapping.x) * snapping.x;
                if (snapping.y != 0) position.y = Mathf.Round(position.y / snapping.y) * snapping.y;
                if (snapping.z != 0) position.z = Mathf.Round(position.z / snapping.z) * snapping.z;
            }
            
            HandleGroup.target.position = position;

            base.OnInteractionActive(pPreviousPosition);
        }
        
        public override void OnInteractionStarted(Vector3 pHitPoint)
        {
            base.OnInteractionStarted(pHitPoint);
            
            _startPosition = HandleGroup.target.position;

            var rAxis = HandleGroup.space == Space.Self
                ? HandleGroup.target.rotation * _axis
                : _axis;
            
            _rAxisRay = new Ray(_startPosition, rAxis);

            var cameraRay = _handleCamera.ScreenPointToRay(Input.mousePosition);

            var closestT = MathUtils.ClosestPointOnRay(_rAxisRay, cameraRay);
            var hitPoint = _rAxisRay.GetPoint(closestT);
            
            _interactionOffset = _startPosition - hitPoint;
        }
        
        public override void SetColor(Color color)
        {
            coneMeshRenderer.material.color = color;
            lineMeshRenderer.material.color = color;
        }
        
        public override void SetDefaultColor()
        {
            coneMeshRenderer.material.color = DefaultColor;
            lineMeshRenderer.material.color = DefaultColor;
        }

        private void LateUpdate()
        {
            var dot = Vector3.Dot(_coneTransform.up, _cameraTransform.forward);
            var notVisible = dot is < -.975f or > 0.975f;
            _lineGameObject.SetActive(!notVisible);
            _coneGameObject.SetActive(!notVisible);
        }
    }
}