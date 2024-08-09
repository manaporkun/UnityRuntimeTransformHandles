using UnityEngine;

namespace TransformHandles
{
    public class RotationHandleSet : MonoBehaviour
    {
        public RotationHandle XHandle;
        public RotationHandle YHandle;
        public RotationHandle ZHandle;
        
        private bool _handleInitialized;

        public void Initialize(HandleGroup handleGroup)
        {
            if (_handleInitialized) return;
            
            transform.SetParent(handleGroup.transform, false);

            var axes = handleGroup._axes;
            if ((axes & HandleAxes.X) != 0)
            {
                XHandle.gameObject.SetActive(true);
                XHandle.Initialize(handleGroup, Vector3.right);
            }

            if ((axes & HandleAxes.Y) != 0)
            {
                YHandle.gameObject.SetActive(true);
                YHandle.Initialize(handleGroup, Vector3.up);
            }

            if ((axes & HandleAxes.Z) != 0)
            {
                ZHandle.gameObject.SetActive(true);
                ZHandle.Initialize(handleGroup, Vector3.forward);
            }
            
            _handleInitialized = true;
        }
    }
}