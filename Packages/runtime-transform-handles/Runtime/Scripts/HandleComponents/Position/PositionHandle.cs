using UnityEngine;

namespace TransformHandles
{
    public class PositionHandle : MonoBehaviour
    {
        public PositionAxis xAxis;
        public PositionAxis yAxis;
        public PositionAxis zAxis;

        public PositionPlane xPlane;
        public PositionPlane yPlane;
        public PositionPlane zPlane;

        private HandleGroup _parentHandleGroup;

        private bool _handleInitialized;

        public void Initialize(HandleGroup handleGroup)
        {
            if (_handleInitialized) return;
            
            _parentHandleGroup = handleGroup;

            if (_parentHandleGroup.axes is HandleAxes.X or HandleAxes.XY or HandleAxes.XZ or HandleAxes.XYZ)
            {
                xAxis.gameObject.SetActive(true);
                xAxis.Initialize(handleGroup);
            }

            if (_parentHandleGroup.axes is HandleAxes.Y or HandleAxes.XY or HandleAxes.YZ or HandleAxes.XYZ)
            {
                yAxis.gameObject.SetActive(true);
                yAxis.Initialize(handleGroup);
            }

            if (_parentHandleGroup.axes is HandleAxes.Z or HandleAxes.XZ or HandleAxes.YZ or HandleAxes.XYZ)
            {
                zAxis.gameObject.SetActive(true);
                zAxis.Initialize(handleGroup);
            }

            if (_parentHandleGroup.axes is HandleAxes.XY or HandleAxes.XYZ)
            {
                zPlane.gameObject.SetActive(true);
                zPlane.Initialize(_parentHandleGroup, Vector3.forward, Vector3.up, -Vector3.right);
            }

            if (_parentHandleGroup.axes is HandleAxes.YZ or HandleAxes.XYZ)
            {
                xPlane.gameObject.SetActive(true);
                xPlane.Initialize(_parentHandleGroup, Vector3.right, Vector3.forward, Vector3.up);
            }

            if (_parentHandleGroup.axes is HandleAxes.XZ or HandleAxes.XYZ)
            {
                yPlane.gameObject.SetActive(true);
                yPlane.Initialize(_parentHandleGroup, Vector3.right, Vector3.up, Vector3.forward);
            }
            
            _handleInitialized = true;
        }
    }
}