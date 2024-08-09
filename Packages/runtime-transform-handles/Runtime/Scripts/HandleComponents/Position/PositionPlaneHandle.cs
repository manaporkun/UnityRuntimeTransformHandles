using UnityEngine;
using UnityEngine.Serialization;

namespace TransformHandles
{
    public class PositionPlaneHandle : HandleBase
    {
        [SerializeField] private MeshRenderer _quadMeshRenderer;

        private Camera _handleCamera;

        private Vector3 _startPosition;
        private Vector3 _axis1;
        private Vector3 _axis2;
        private Vector3 _perp;
        private Plane _plane;
        private Vector3 _interactionOffset;
        
        private GameObject _quadGameObject;
        private Transform _cameraTransform;

        public void Initialize(HandleGroup handleGroup, Vector3 axis1, Vector3 axis2, Vector3 perp)
        {
            HandleGroup = handleGroup;
            _axis1 = axis1;
            _axis2 = axis2;
            _perp = perp;
            
            _handleCamera = HandleGroup._handleCamera;
            
            _quadGameObject = _quadMeshRenderer.gameObject;
            _cameraTransform = _handleCamera.transform;
            
            _quadGameObject.transform.localPosition = (_axis1 + _axis2) * 0.2f;
        }

        public override void OnInteractionActive(Vector3 pPreviousPosition)
        {
            var ray = _handleCamera.ScreenPointToRay(Input.mousePosition);

            _plane.Raycast(ray, out var d);
            
            var hitPoint = ray.GetPoint(d);

            var offset = hitPoint + _interactionOffset - _startPosition;

            var axis = _axis1 + _axis2;
            var snapping = HandleGroup._positionSnap;
            
            var snap = Vector3.Scale(snapping, axis).magnitude;
            if (snap != 0 && HandleGroup._snappingType == SnappingType.Relative)
            {
                if (snapping.x != 0) offset.x = Mathf.Round(offset.x / snapping.x) * snapping.x;
                if (snapping.y != 0) offset.y = Mathf.Round(offset.y / snapping.y) * snapping.y;
                if (snapping.z != 0) offset.z = Mathf.Round(offset.z / snapping.z) * snapping.z;
            }

            var position = _startPosition + offset;
            
            if (snap != 0 && HandleGroup._snappingType == SnappingType.Absolute)
            {
                if (snapping.x != 0) position.x = Mathf.Round(position.x / snapping.x) * snapping.x;
                if (snapping.y != 0) position.y = Mathf.Round(position.y / snapping.y) * snapping.y;
                if (snapping.x != 0) position.z = Mathf.Round(position.z / snapping.z) * snapping.z;
            }

            HandleGroup._target.position = position;

            base.OnInteractionActive(pPreviousPosition);
        }

        public override void OnInteractionStarted(Vector3 pHitPoint)
        {
            var rPerp = HandleGroup._space == Space.Self
                ? HandleGroup._target.rotation * _perp
                : _perp;

            var position = HandleGroup._target.position;
            _plane = new Plane(rPerp, position);
            
            var ray = _handleCamera.ScreenPointToRay(Input.mousePosition);

            _plane.Raycast(ray, out var d);
            
            var hitPoint = ray.GetPoint(d);
            _startPosition = position;
            _interactionOffset = _startPosition - hitPoint;
        }

        private void Update()
        {
            if(_handleCamera == null) return;
            
            var axis1 = _axis1;
            var rAxis1 = HandleGroup._space == Space.Self
                ? HandleGroup._target.rotation * axis1
                : axis1;
            var angle1 = Vector3.Angle(_cameraTransform.forward, rAxis1);
            if (angle1 < 90)
                axis1 = -axis1;
            
            var axis2 = _axis2;
            var rAxis2 = HandleGroup._space == Space.Self
                ? HandleGroup._target.rotation * axis2
                : axis2;
            var angle2 = Vector3.Angle(_cameraTransform.forward, rAxis2);
            if (angle2 < 90)
                axis2 = -axis2;
            
            _quadGameObject.transform.localPosition = (axis1 + axis2) * 0.2f;
        }
        
        private void LateUpdate()
        {
            var dot = Vector3.Dot(_quadGameObject.transform.up, _cameraTransform.forward);
            var notVisible = dot is < -.25f or > 0.25f;
            _quadGameObject.SetActive(notVisible);
        }

        public override void SetColor(Color color)
        {
            _quadMeshRenderer.material.color = color;
        }
        
        public override void SetDefaultColor()
        {
            _quadMeshRenderer.material.color = DefaultColor;
        }
    }
}