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
        /// Re-runnable so <see cref="Handle.ChangeAxes"/> can filter visible axes/planes after creation.
        /// </summary>
        /// <param name="handle">The parent handle.</param>
        public void Initialize(Handle handle)
        {
            _parentHandle = handle;

            // Planes ship as children of their corresponding axis GameObjects in the prefab.
            // Reparent them onto this handle so masking an axis off doesn't also hide the plane.
            if (xPlane.transform.parent != transform) xPlane.transform.SetParent(transform, true);
            if (yPlane.transform.parent != transform) yPlane.transform.SetParent(transform, true);
            if (zPlane.transform.parent != transform) zPlane.transform.SetParent(transform, true);

            var hasX = handle.axes.HasAxis(HandleAxes.X);
            var hasY = handle.axes.HasAxis(HandleAxes.Y);
            var hasZ = handle.axes.HasAxis(HandleAxes.Z);

            xAxis.gameObject.SetActive(hasX);
            if (hasX) xAxis.Initialize(handle);

            yAxis.gameObject.SetActive(hasY);
            if (hasY) yAxis.Initialize(handle);

            zAxis.gameObject.SetActive(hasZ);
            if (hasZ) zAxis.Initialize(handle);

            var hasXY = handle.axes.HasBothAxes(HandleAxes.X, HandleAxes.Y);
            var hasYZ = handle.axes.HasBothAxes(HandleAxes.Y, HandleAxes.Z);
            var hasXZ = handle.axes.HasBothAxes(HandleAxes.X, HandleAxes.Z);

            zPlane.gameObject.SetActive(hasXY);
            if (hasXY) zPlane.Initialize(handle, Vector3.forward, Vector3.up, -Vector3.right);

            xPlane.gameObject.SetActive(hasYZ);
            if (hasYZ) xPlane.Initialize(handle, Vector3.right, Vector3.forward, Vector3.up);

            yPlane.gameObject.SetActive(hasXZ);
            if (hasXZ) yPlane.Initialize(handle, Vector3.right, Vector3.up, Vector3.forward);
        }
    }
}
