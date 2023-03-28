using UnityEngine;

namespace TransformHandles
{
    public class PositionPlane : HandleBase
    {
        [SerializeField] private Color defaultColor;
        [SerializeField] private MeshRenderer quadMeshRenderer;

        private Camera _handleCamera;

        private Vector3 _startPosition;
        private Vector3 _axis1;
        private Vector3 _axis2;
        private Vector3 _perp;
        private Plane _plane;
        private Vector3 _interactionOffset;

        public void Initialize(Handle handle, Vector3 axis1, Vector3 axis2, Vector3 perp)
        {
            ParentHandle = handle;
            _axis1 = axis1;
            _axis2 = axis2;
            _perp = perp;
            
            _handleCamera = ParentHandle.handleCamera;

            DefaultColor = defaultColor;
            
            transform.localPosition = (_axis1 + _axis2) * 0.125f;
        }

        public override void Interact(Vector3 pPreviousPosition)
        {
            var ray = _handleCamera.ScreenPointToRay(Input.mousePosition);

            _plane.Raycast(ray, out var d);
            
            var hitPoint = ray.GetPoint(d);

            var offset = hitPoint + _interactionOffset - _startPosition;

            var axis = _axis1 + _axis2;
            var snapping = ParentHandle.positionSnap;
            
            var snap = Vector3.Scale(snapping, axis).magnitude;
            if (snap != 0 && ParentHandle.snappingType == SnappingType.Relative)
            {
                if (snapping.x != 0) offset.x = Mathf.Round(offset.x / snapping.x) * snapping.x;
                if (snapping.y != 0) offset.y = Mathf.Round(offset.y / snapping.y) * snapping.y;
                if (snapping.z != 0) offset.z = Mathf.Round(offset.z / snapping.z) * snapping.z;
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
            var rPerp = ParentHandle.space == Space.Self
                ? ParentHandle.target.rotation * _perp
                : _perp;

            var position = ParentHandle.target.position;
            _plane = new Plane(rPerp, position);
            
            var ray = _handleCamera.ScreenPointToRay(Input.mousePosition);

            _plane.Raycast(ray, out var d);
            
            var hitPoint = ray.GetPoint(d);
            _startPosition = position;
            _interactionOffset = _startPosition - hitPoint;
        }

        private void Update()
        {
            var axis1 = _axis1;
            var rAxis1 = ParentHandle.space == Space.Self
                ? ParentHandle.target.rotation * axis1
                : axis1;
            var angle1 = Vector3.Angle(ParentHandle.handleCamera.transform.forward, rAxis1);
            if (angle1 < 90)
                axis1 = -axis1;

            var axis2 = _axis2;
            var rAxis2 = ParentHandle.space == Space.Self
                ? ParentHandle.target.rotation * axis2
                : axis2;
            var angle2 = Vector3.Angle(ParentHandle.handleCamera.transform.forward, rAxis2);
            if (angle2 < 90)
                axis2 = -axis2;

            transform.localPosition = (axis1 + axis2) * 0.125f;
        }
        
        public override void SetColor(Color color)
        {
            quadMeshRenderer.material.color = color;
        }
        
        public override void SetDefaultColor()
        {
            quadMeshRenderer.material.color = DefaultColor;
        }
    }
}