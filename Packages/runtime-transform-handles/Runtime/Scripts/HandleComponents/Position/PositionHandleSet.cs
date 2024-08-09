using UnityEngine;

namespace TransformHandles
{
    public class PositionHandleSet : MonoBehaviour
    {
        public PositionHandle XHandle;
        public PositionHandle YHandle;
        public PositionHandle ZHandle;

        public PositionPlaneHandle XPlaneHandle;
        public PositionPlaneHandle YPlaneHandle;
        public PositionPlaneHandle ZPlaneHandle;

        private bool _handleInitialized;

        public void Initialize(HandleGroup handleGroup)
        {
            if (_handleInitialized) return;

            var axes = handleGroup._axes;

            if ((axes & HandleAxes.X) != 0)
            {
                XHandle.gameObject.SetActive(true);
                XHandle.Initialize(handleGroup);
            }

            if ((axes & HandleAxes.Y) != 0)
            {
                YHandle.gameObject.SetActive(true);
                YHandle.Initialize(handleGroup);
            }
            
            if ((axes & HandleAxes.Z) != 0)
            {
                ZHandle.gameObject.SetActive(true);
                ZHandle.Initialize(handleGroup);
            }

            if ((axes & (HandleAxes.X | HandleAxes.Y)) == (HandleAxes.X | HandleAxes.Y))
            {
                ZPlaneHandle.gameObject.SetActive(true);
                ZPlaneHandle.Initialize(handleGroup, Vector3.forward, Vector3.up, -Vector3.right);
            }

            if ((axes & (HandleAxes.Y | HandleAxes.Z)) == (HandleAxes.Y | HandleAxes.Z))
            {
                XPlaneHandle.gameObject.SetActive(true);
                XPlaneHandle.Initialize(handleGroup, Vector3.right, Vector3.forward, Vector3.up);
            }

            if ((axes & (HandleAxes.X | HandleAxes.Z)) == (HandleAxes.X | HandleAxes.Z))
            {
                YPlaneHandle.gameObject.SetActive(true);
                YPlaneHandle.Initialize(handleGroup, Vector3.right, Vector3.up, Vector3.forward);
            }

            _handleInitialized = true;
        }
    }
}