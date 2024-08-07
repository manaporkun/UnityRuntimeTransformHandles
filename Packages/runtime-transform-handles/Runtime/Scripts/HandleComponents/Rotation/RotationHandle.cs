using UnityEngine;

// ReSharper disable once CheckNamespace
namespace TransformHandles
{
    public class RotationHandle : MonoBehaviour
    {
        public RotationAxis xAxis;
        public RotationAxis yAxis;
        public RotationAxis zAxis;

        private HandleGroup _parentHandleGroup;

        private bool _handleInitialized;

        public void Initialize(HandleGroup handleGroup)
        {
            if (_handleInitialized) return;
            
            _parentHandleGroup = handleGroup;
            transform.SetParent(_parentHandleGroup.transform, false);

            if (_parentHandleGroup.axes is HandleAxes.X or HandleAxes.XY or HandleAxes.XZ or HandleAxes.XYZ)
            {
                xAxis.gameObject.SetActive(true);
                xAxis.Initialize(_parentHandleGroup, Vector3.right);
            }

            if (_parentHandleGroup.axes is HandleAxes.Y or HandleAxes.XY or HandleAxes.YZ or HandleAxes.XYZ)
            {
                yAxis.gameObject.SetActive(true);
                yAxis.Initialize(_parentHandleGroup, Vector3.up);
            }

            if (_parentHandleGroup.axes is HandleAxes.Z or HandleAxes.YZ or HandleAxes.XZ or HandleAxes.XYZ)
            {
                zAxis.gameObject.SetActive(true);
                zAxis.Initialize(_parentHandleGroup, Vector3.forward);
            }
            _handleInitialized = true;
        }
    }
}