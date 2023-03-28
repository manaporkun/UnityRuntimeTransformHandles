using TransformHandles.Utils;
using UnityEngine;

namespace TransformHandles
{
    public class ScaleAxis : HandleBase
    {
        [SerializeField] private Color defaultColor;
        [SerializeField] private MeshRenderer cubeMeshRenderer;
        [SerializeField] private MeshRenderer lineMeshRenderer;

        private Camera _handleCamera;
        
        private const float Size = .75f;
        
        private Vector3 _axis;
        private Vector3 _startScale;

        private float _interactionDistance;
        private Ray _rAxisRay;
        
        public void Initialize(Handle handle, Vector3 pAxis)
        {
            ParentHandle = handle;
            _axis = pAxis;
            DefaultColor = defaultColor;
            
            _handleCamera = ParentHandle.handleCamera;
        }

        protected void Update()
        {
            lineMeshRenderer.transform.localScale = new Vector3(1, 1 + delta, 1);
            cubeMeshRenderer.transform.localPosition = _axis * (Size * (1 + delta));
        }

        public override void Interact(Vector3 pPreviousPosition)
        {
            var cameraRay = _handleCamera.ScreenPointToRay(Input.mousePosition);

            var   closestT = MathUtils.ClosestPointOnRay(_rAxisRay, cameraRay);
            var hitPoint = _rAxisRay.GetPoint(closestT);
            
            var distance = Vector3.Distance(ParentHandle.target.position, hitPoint);
            var axisScaleDelta = distance / _interactionDistance - 1f;

            var snapping = ParentHandle.scaleSnap;
            var snap = Mathf.Abs(Vector3.Dot(snapping, _axis));
            if (snap != 0)
            {
                if (ParentHandle.snappingType == SnappingType.Relative)
                {
                    axisScaleDelta = Mathf.Round(axisScaleDelta / snap) * snap;
                }
                else
                {
                    var axisStartScale = Mathf.Abs(Vector3.Dot(_startScale, _axis));
                    axisScaleDelta = Mathf.Round((axisScaleDelta + axisStartScale) / snap) * snap - axisStartScale;
                }
            }

            delta = axisScaleDelta;
            var scale = Vector3.Scale(_startScale, _axis * axisScaleDelta + Vector3.one);

            ParentHandle.target.localScale = scale;

            base.Interact(pPreviousPosition);
        }

        public override void StartInteraction(Vector3 pHitPoint)
        {
            base.StartInteraction(pHitPoint);
            _startScale = ParentHandle.target.localScale;

            var rAxis = ParentHandle.space == Space.Self
                ? ParentHandle.target.rotation * _axis
                : _axis;

            var position = ParentHandle.target.position;
            _rAxisRay = new Ray(position, rAxis);
            
            var cameraRay = _handleCamera.ScreenPointToRay(Input.mousePosition);
            
            var   closestT = MathUtils.ClosestPointOnRay(_rAxisRay, cameraRay);
            var hitPoint = _rAxisRay.GetPoint(closestT);
            
            _interactionDistance = Vector3.Distance(position, hitPoint);
        }
        
        public override void SetColor(Color color)
        {
            cubeMeshRenderer.material.color = color;
            lineMeshRenderer.material.color = color;
        }
        
        public override void SetDefaultColor()
        {
            cubeMeshRenderer.material.color = DefaultColor;
            lineMeshRenderer.material.color = DefaultColor;
        }
    }
}