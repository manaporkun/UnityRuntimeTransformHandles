using UnityEngine;

namespace TransformHandles
{
    public class UniformScaleHandle : HandleBase
    {
        [SerializeField] private MeshRenderer _cubeMeshRenderer;
        
        private Vector3 _axis;
        private Vector3 _startScale;
        
        public void Initialize(HandleGroup handleGroup, Vector3 axis)
        {
            HandleGroup = handleGroup;
            _axis = axis;
        }
        
        public override void OnInteractionStarted(Vector3 pHitPoint)
        {
            base.OnInteractionStarted(pHitPoint);
            if (HandleGroup._target != null) _startScale = HandleGroup._target.localScale;
        }

        public override void OnInteractionActive(Vector3 previousPosition)
        {
            var mouseVector = (Input.mousePosition - previousPosition);
            var scaledDelta = (mouseVector.x + mouseVector.y) * Time.deltaTime * 2;
            delta += scaledDelta;
            
            if (HandleGroup._target != null)
            {
                HandleGroup._target.localScale = _startScale + Vector3.Scale(_startScale, _axis) * delta;
            }

            base.OnInteractionActive(previousPosition);
        }
        
        public override void SetColor(Color color)
        {
            _cubeMeshRenderer.material.color = color;
        }
        
        public override void SetDefaultColor()
        {
            _cubeMeshRenderer.material.color = DefaultColor;
        }
    }
}