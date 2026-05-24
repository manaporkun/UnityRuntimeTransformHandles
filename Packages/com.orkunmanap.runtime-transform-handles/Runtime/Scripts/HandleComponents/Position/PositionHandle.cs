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

        /// <summary>
        /// Initializes the position handle with all its axes and planes.
        /// Re-runnable so <see cref="Handle.ChangeAxes"/> can filter visible axes after creation.
        /// </summary>
        /// <param name="handle">The parent handle.</param>
        public void Initialize(Handle handle)
        {
            _parentHandle = handle;

            var hasX = _parentHandle.axes.HasAxis(HandleAxes.X);
            var hasY = _parentHandle.axes.HasAxis(HandleAxes.Y);
            var hasZ = _parentHandle.axes.HasAxis(HandleAxes.Z);

            xAxis.gameObject.SetActive(hasX);
            if (hasX) xAxis.Initialize(handle);

            yAxis.gameObject.SetActive(hasY);
            if (hasY) yAxis.Initialize(handle);

            zAxis.gameObject.SetActive(hasZ);
            if (hasZ) zAxis.Initialize(handle);

            var hasXY = _parentHandle.axes.HasBothAxes(HandleAxes.X, HandleAxes.Y);
            var hasYZ = _parentHandle.axes.HasBothAxes(HandleAxes.Y, HandleAxes.Z);
            var hasXZ = _parentHandle.axes.HasBothAxes(HandleAxes.X, HandleAxes.Z);

            zPlane.gameObject.SetActive(hasXY);
            if (hasXY) zPlane.Initialize(_parentHandle, Vector3.forward, Vector3.up, -Vector3.right);

            xPlane.gameObject.SetActive(hasYZ);
            if (hasYZ) xPlane.Initialize(_parentHandle, Vector3.right, Vector3.forward, Vector3.up);

            yPlane.gameObject.SetActive(hasXZ);
            if (hasXZ) yPlane.Initialize(_parentHandle, Vector3.right, Vector3.up, Vector3.forward);
        }
    }
}
