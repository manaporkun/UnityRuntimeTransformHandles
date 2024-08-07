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
        
        public void Initialize(HandleGroup handleGroup, Vector3 pAxis)
        {
            HandleGroup = handleGroup;
            _axis = pAxis;
            DefaultColor = defaultColor;
            
            _handleCamera = HandleGroup.handleCamera;
        }

        protected void Update()
        {
            lineMeshRenderer.transform.localScale = new Vector3(1, 1 + Delta, 1);
            cubeMeshRenderer.transform.localPosition = _axis * (Size * (1 + Delta));
        }

        public override void OnInteractionActive(Vector3 pPreviousPosition)
        {
            var cameraRay = _handleCamera.ScreenPointToRay(Input.mousePosition);

            var   closestT = MathUtils.ClosestPointOnRay(_rAxisRay, cameraRay);
            var hitPoint = _rAxisRay.GetPoint(closestT);
            
            var distance = Vector3.Distance(HandleGroup.target.position, hitPoint);
            var axisScaleDelta = distance / _interactionDistance - 1f;

            var snapping = HandleGroup.scaleSnap;
            var snap = Mathf.Abs(Vector3.Dot(snapping, _axis));
            if (snap != 0)
            {
                if (HandleGroup.snappingType == SnappingType.Relative)
                {
                    axisScaleDelta = Mathf.Round(axisScaleDelta / snap) * snap;
                }
                else
                {
                    var axisStartScale = Mathf.Abs(Vector3.Dot(_startScale, _axis));
                    axisScaleDelta = Mathf.Round((axisScaleDelta + axisStartScale) / snap) * snap - axisStartScale;
                }
            }

            Delta = axisScaleDelta;
            var scale = Vector3.Scale(_startScale, _axis * axisScaleDelta + Vector3.one);

            HandleGroup.target.localScale = scale;

            base.OnInteractionActive(pPreviousPosition);
        }

        public override void OnInteractionStarted(Vector3 pHitPoint)
        {
            base.OnInteractionStarted(pHitPoint);
            _startScale = HandleGroup.target.localScale;

            var rAxis = HandleGroup.space == Space.Self
                ? HandleGroup.target.rotation * _axis
                : _axis;

            var position = HandleGroup.target.position;
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