using UnityEngine;

namespace TransformHandles
{
    /// <summary>
    /// Manages the rotation handle which allows rotating objects around axes.
    /// </summary>
    public class RotationHandle : MonoBehaviour
    {
        public RotationAxis xAxis;
        public RotationAxis yAxis;
        public RotationAxis zAxis;

        private Handle _parentHandle;

        private bool _handleInitialized;

        /// <summary>
        /// Initializes the rotation handle with all its axes.
        /// </summary>
        /// <param name="handle">The parent handle.</param>
        public void Initialize(Handle handle)
        {
            if (_handleInitialized) return;

            _parentHandle = handle;
            transform.SetParent(_parentHandle.transform, false);

            if (_parentHandle.axes.HasAxis(HandleAxes.X))
            {
                xAxis.gameObject.SetActive(true);
                xAxis.Initialize(_parentHandle, Vector3.right);
            }

            if (_parentHandle.axes.HasAxis(HandleAxes.Y))
            {
                yAxis.gameObject.SetActive(true);
                yAxis.Initialize(_parentHandle, Vector3.up);
            }

            if (_parentHandle.axes.HasAxis(HandleAxes.Z))
            {
                zAxis.gameObject.SetActive(true);
                zAxis.Initialize(_parentHandle, Vector3.forward);
            }

            _handleInitialized = true;
        }
    }
}
