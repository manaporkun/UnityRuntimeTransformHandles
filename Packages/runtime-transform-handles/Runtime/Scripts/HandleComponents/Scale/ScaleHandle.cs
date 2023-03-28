using UnityEngine;

namespace TransformHandles
{
    public class ScaleHandle : MonoBehaviour
    {
        private Handle _parentHandle;
        
        public ScaleAxis xAxis;
        public ScaleAxis yAxis;
        public ScaleAxis zAxis;

        public ScaleGlobal globalScale;
        
        private bool _handleInitialized;

        public void Initialize(Handle handle)
        {
            if (_handleInitialized) return;
            
            _parentHandle = handle;
            
            if (_parentHandle.axes is HandleAxes.X or HandleAxes.XY or HandleAxes.XZ or HandleAxes.XYZ)
                xAxis.Initialize(_parentHandle, Vector3.right);
            
            if (_parentHandle.axes is HandleAxes.Y or HandleAxes.XY or HandleAxes.YZ or HandleAxes.XYZ)
                yAxis.Initialize(_parentHandle, Vector3.up);

            if (_parentHandle.axes is HandleAxes.Z or HandleAxes.XZ or HandleAxes.YZ or HandleAxes.XYZ)
                zAxis.Initialize(_parentHandle, Vector3.forward);

            if (_parentHandle.axes != HandleAxes.X && _parentHandle.axes != HandleAxes.Y && _parentHandle.axes != HandleAxes.Z)
            {
                globalScale.Initialize(_parentHandle, HandleBase.GetVectorFromAxes(_parentHandle.axes));
                
                globalScale.InteractionStart += OnGlobalInteractionStart;
                globalScale.InteractionUpdate += OnGlobalInteractionUpdate;
                globalScale.InteractionEnd += OnGlobalInteractionEnd;
            }

            _handleInitialized = true;
        }

        private void OnGlobalInteractionStart()
        {
            xAxis.SetColor(Color.yellow);
            yAxis.SetColor(Color.yellow);
            zAxis.SetColor(Color.yellow);
        }

        private void OnGlobalInteractionUpdate(float pDelta)
        {
            xAxis.delta = pDelta;
            yAxis.delta = pDelta;
            zAxis.delta = pDelta;
        }

        private void OnGlobalInteractionEnd()
        {
            xAxis.SetDefaultColor();
            xAxis.delta = 0;
            
            yAxis.SetDefaultColor();
            yAxis.delta = 0;
            
            zAxis.SetDefaultColor();
            zAxis.delta = 0;
        }
    }
}