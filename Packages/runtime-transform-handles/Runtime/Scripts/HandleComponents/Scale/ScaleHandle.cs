using TransformHandles.Utils;
using UnityEngine;

namespace TransformHandles
{
    public class ScaleHandle : HandleBase
    {
        [SerializeField] private MeshRenderer _cubeMeshRenderer;
        [SerializeField] private MeshRenderer _lineMeshRenderer;
        
        private Camera _handleCamera;
        
        private const float Size = .75f;
        
        private Vector3 _axis;
        private Vector3 _startScale;

        private float _interactionDistance;
        private Ray _ray;
        
        public void Initialize(HandleGroup handleGroup, Vector3 axis)
        {
            HandleGroup = handleGroup;
            _axis = axis;
            
            _handleCamera = HandleGroup._handleCamera;
        }

        protected void Update()
        {
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (_lineMeshRenderer != null) _lineMeshRenderer.transform.localScale = new Vector3(1, 1 + delta, 1);
            if (_cubeMeshRenderer != null) _cubeMeshRenderer.transform.localPosition = _axis * (Size * (1 + delta));
        }
        
        public override void OnInteractionStarted(Vector3 pHitPoint)
        {
            base.OnInteractionStarted(pHitPoint);
            
            if(HandleGroup?._target == null || _handleCamera == null) return;
            
            _startScale = HandleGroup._target.localScale;
            
            var targetPosition = HandleGroup._target.position;
            _ray = CalculateRay(HandleGroup._space, HandleGroup._target.rotation, _axis, targetPosition);
            _interactionDistance = CalculateInteractionDistance(targetPosition, _ray, _handleCamera);
        }

        private static Ray CalculateRay(Space space, Quaternion rotation, Vector3 axis, Vector3 targetPosition)
        {
            var direction = space == Space.Self
                ? rotation * axis
                : axis;

            return new Ray(targetPosition, direction);
        }

        private static float CalculateInteractionDistance(Vector3 targetPosition, Ray ray, Camera handleCamera)
        {
            var cameraRay = handleCamera.ScreenPointToRay(Input.mousePosition);
            var closestPoint = MathUtils.ClosestPointOnRay(ray, cameraRay);
            var hitPoint = ray.GetPoint(closestPoint);
            
            return Vector3.Distance(targetPosition, hitPoint);
        }

        public override void OnInteractionActive(Vector3 pPreviousPosition)
        {
            if (_handleCamera == null || HandleGroup == null || HandleGroup._target == null) return;
            
            var hitPoint = GetHitPoint(_handleCamera, _ray);
            var axisScaleDelta = CalculateAxisScaleDelta(hitPoint, HandleGroup, _interactionDistance);
            delta = ApplySnapping(axisScaleDelta, HandleGroup, _axis, _startScale);
            
            var scale = Vector3.Scale(_startScale, _axis * delta + Vector3.one);
            HandleGroup._target.localScale = scale;

            base.OnInteractionActive(pPreviousPosition);
        }
        
        private static Vector3 GetHitPoint(Camera handleCamera, Ray ray)
        {
            var cameraRay = handleCamera.ScreenPointToRay(Input.mousePosition);
            var closestPointOnRay = MathUtils.ClosestPointOnRay(ray, cameraRay);
            return ray.GetPoint(closestPointOnRay);
        }
        
        private static float CalculateAxisScaleDelta(Vector3 hitPoint, HandleGroup handleGroup, float interactionDistance)
        {
            var distance = Vector3.Distance(handleGroup._target.position, hitPoint);
            return distance / interactionDistance - 1f;
        }
        
        private static float ApplySnapping(float axisScaleDelta, HandleGroup handleGroup, Vector3 axis, Vector3 startScale)
        {
            var snapping = handleGroup._scaleSnap;
            var snap = Mathf.Abs(Vector3.Dot(snapping, axis));
            if (snap != 0)
            {
                if (handleGroup._snappingType == SnappingType.Relative)
                {
                    axisScaleDelta = Mathf.Round(axisScaleDelta / snap) * snap;
                }
                else
                {
                    var axisStartScale = Mathf.Abs(Vector3.Dot(startScale, axis));
                    axisScaleDelta = Mathf.Round((axisScaleDelta + axisStartScale) / snap) * snap - axisStartScale;
                }
            }
            return axisScaleDelta;
        }
        
        public override void SetColor(Color color)
        {
            _cubeMeshRenderer.material.color = color;
            _lineMeshRenderer.material.color = color;
        }
        
        public override void SetDefaultColor()
        {
            _cubeMeshRenderer.material.color = DefaultColor;
            _lineMeshRenderer.material.color = DefaultColor;
        }
    }
}