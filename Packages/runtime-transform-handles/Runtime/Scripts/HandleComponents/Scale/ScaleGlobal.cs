using UnityEngine;

namespace TransformHandles
{
    public class ScaleGlobal : HandleBase
    {
        [SerializeField] private Color defaultColor;
        [SerializeField] private MeshRenderer cubeMeshRenderer;
        
        private Vector3 _axis;
        private Vector3 _startScale;
        
        public void Initialize(Handle handle, Vector3 pAxis)
        {
            ParentHandle = handle;
            _axis = pAxis;
            DefaultColor = defaultColor;
        }

        public override void Interact(Vector3 pPreviousPosition)
        {
            var mouseVector = (Input.mousePosition - pPreviousPosition);
            var d = (mouseVector.x + mouseVector.y) * Time.deltaTime * 2;
            delta += d;
            ParentHandle.target.localScale = _startScale + Vector3.Scale(_startScale,_axis) * delta;
            
            base.Interact(pPreviousPosition);
        }

        public override void StartInteraction(Vector3 pHitPoint)
        {
            base.StartInteraction(pHitPoint);
            _startScale = ParentHandle.target.localScale;
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