using UnityEngine;

namespace TransformHandles
{
    /// <summary>
    /// Manages the position handle which allows moving objects along axes and planes.
    /// </summary>
    public class PositionHandle : MonoBehaviour
    {
        public PositionAxis xAxis;
        public PositionAxis yAxis;
        public PositionAxis zAxis;

        public PositionPlane xPlane;
        public PositionPlane yPlane;
        public PositionPlane zPlane;

        private Handle _parentHandle;

        private bool _handleInitialized;

        /// <summary>
        /// Initializes the position handle with all its axes and planes.
        /// </summary>
        /// <param name="handle">The parent handle.</param>
        public void Initialize(Handle handle)
        {
            if (_handleInitialized) return;

            _parentHandle = handle;

            if (_parentHandle.axes.HasAxis(HandleAxes.X))
            {
                xAxis.gameObject.SetActive(true);
                xAxis.Initialize(handle);
            }

            if (_parentHandle.axes.HasAxis(HandleAxes.Y))
            {
                yAxis.gameObject.SetActive(true);
                yAxis.Initialize(handle);
            }

            if (_parentHandle.axes.HasAxis(HandleAxes.Z))
            {
                zAxis.gameObject.SetActive(true);
                zAxis.Initialize(handle);
            }

            if (_parentHandle.axes.HasBothAxes(HandleAxes.X, HandleAxes.Y))
            {
                zPlane.gameObject.SetActive(true);
                zPlane.Initialize(_parentHandle, Vector3.forward, Vector3.up, -Vector3.right);
            }

            if (_parentHandle.axes.HasBothAxes(HandleAxes.Y, HandleAxes.Z))
            {
                xPlane.gameObject.SetActive(true);
                xPlane.Initialize(_parentHandle, Vector3.right, Vector3.forward, Vector3.up);
            }

            if (_parentHandle.axes.HasBothAxes(HandleAxes.X, HandleAxes.Z))
            {
                yPlane.gameObject.SetActive(true);
                yPlane.Initialize(_parentHandle, Vector3.right, Vector3.up, Vector3.forward);
            }

            _handleInitialized = true;
        }
    }
}
