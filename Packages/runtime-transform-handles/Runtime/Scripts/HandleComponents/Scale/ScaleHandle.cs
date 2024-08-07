using UnityEngine;

namespace TransformHandles
{
    public class ScaleHandle : MonoBehaviour
    {
        private HandleGroup _parentHandleGroup;
        
        public ScaleAxis xAxis;
        public ScaleAxis yAxis;
        public ScaleAxis zAxis;

        public ScaleGlobal globalScale;
        
        private bool _handleInitialized;

        public void Initialize(HandleGroup handleGroup)
        {
            if (_handleInitialized) return;
            
            _parentHandleGroup = handleGroup;
            
            if (_parentHandleGroup.axes is HandleAxes.X or HandleAxes.XY or HandleAxes.XZ or HandleAxes.XYZ)
                xAxis.Initialize(_parentHandleGroup, Vector3.right);
            
            if (_parentHandleGroup.axes is HandleAxes.Y or HandleAxes.XY or HandleAxes.YZ or HandleAxes.XYZ)
                yAxis.Initialize(_parentHandleGroup, Vector3.up);

            if (_parentHandleGroup.axes is HandleAxes.Z or HandleAxes.XZ or HandleAxes.YZ or HandleAxes.XYZ)
                zAxis.Initialize(_parentHandleGroup, Vector3.forward);

            if (_parentHandleGroup.axes != HandleAxes.X && _parentHandleGroup.axes != HandleAxes.Y && _parentHandleGroup.axes != HandleAxes.Z)
            {
                globalScale.Initialize(_parentHandleGroup, HandleBase.GetVectorFromAxes(_parentHandleGroup.axes));
                
                globalScale.OnInteractionStartedEvent += OnGlobalOnInteractionStartedEvent;
                globalScale.OnInteractionEndedEvent += OnGlobalOnInteractionEndedEvent;
                globalScale.OnInteractionActiveEvent += OnGlobalOnInteractionActiveEvent;
            }

            _handleInitialized = true;
        }

        private void OnGlobalOnInteractionStartedEvent()
        {
            xAxis.SetColor(Color.yellow);
            yAxis.SetColor(Color.yellow);
            zAxis.SetColor(Color.yellow);
        }

        private void OnGlobalOnInteractionEndedEvent(float pDelta)
        {
            xAxis.Delta = pDelta;
            yAxis.Delta = pDelta;
            zAxis.Delta = pDelta;
        }

        private void OnGlobalOnInteractionActiveEvent()
        {
            xAxis.SetDefaultColor();
            xAxis.Delta = 0;
            
            yAxis.SetDefaultColor();
            yAxis.Delta = 0;
            
            zAxis.SetDefaultColor();
            zAxis.Delta = 0;
        }
    }
}