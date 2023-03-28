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
        
        public void Initialize(Handle handle)
        {
            ParentHandle = handle;
            _handleCamera = ParentHandle.handleCamera;

            _axis = coneMeshRenderer.transform.up;
            DefaultColor = defaultColor;
        }

        public override void Interact(Vector3 pPreviousPosition)
        {
            var cameraRay = _handleCamera.ScreenPointToRay(Input.mousePosition);

            var closestT = MathUtils.ClosestPointOnRay(_rAxisRay, cameraRay);
            var hitPoint = _rAxisRay.GetPoint(closestT);
            
            var offset = hitPoint + _interactionOffset - _startPosition;
            
            var snapping = ParentHandle.positionSnap;
            var   snap     = Vector3.Scale(snapping, _axis).magnitude;
            if (snap != 0 && ParentHandle.snappingType == SnappingType.Relative)
            {
                offset = (Mathf.Round(offset.magnitude / snap) * snap) * offset.normalized; 
            }

            var position = _startPosition + offset;
            
            if (snap != 0 && ParentHandle.snappingType == SnappingType.Absolute)
            {
                if (snapping.x != 0) position.x = Mathf.Round(position.x / snapping.x) * snapping.x;
                if (snapping.y != 0) position.y = Mathf.Round(position.y / snapping.y) * snapping.y;
                if (snapping.x != 0) position.z = Mathf.Round(position.z / snapping.z) * snapping.z;
            }
            
            ParentHandle.target.position = position;

            base.Interact(pPreviousPosition);
        }
        
        public override void StartInteraction(Vector3 pHitPoint)
        {
            base.StartInteraction(pHitPoint);
            
            _startPosition = ParentHandle.target.position;

            var rAxis = ParentHandle.space == Space.Self
                ? ParentHandle.target.rotation * _axis
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
    }
}