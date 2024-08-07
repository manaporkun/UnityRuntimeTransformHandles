using UnityEngine;

namespace TransformHandles
{
    public class ScaleGlobal : HandleBase
    {
        [SerializeField] private Color defaultColor;
        [SerializeField] private MeshRenderer cubeMeshRenderer;
        
        private Vector3 _axis;
        private Vector3 _startScale;
        
        public void Initialize(HandleGroup handleGroup, Vector3 pAxis)
        {
            HandleGroup = handleGroup;
            _axis = pAxis;
            DefaultColor = defaultColor;
        }

        public override void OnInteractionActive(Vector3 pPreviousPosition)
        {
            var mouseVector = (Input.mousePosition - pPreviousPosition);
            var d = (mouseVector.x + mouseVector.y) * Time.deltaTime * 2;
            Delta += d;
            HandleGroup.target.localScale = _startScale + Vector3.Scale(_startScale,_axis) * Delta;
            
            base.OnInteractionActive(pPreviousPosition);
        }

        public override void OnInteractionStarted(Vector3 pHitPoint)
        {
            base.OnInteractionStarted(pHitPoint);
            _startScale = HandleGroup.target.localScale;
        }
        
        public override void SetColor(Color color)
        {
            cubeMeshRenderer.material.color = color;
        }
        
        public override void SetDefaultColor()
        {
            cubeMeshRenderer.material.color = DefaultColor;
        }
    }
}