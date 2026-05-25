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

        /// <summary>
        /// Initializes the rotation handle with all its axes.
        /// Re-runnable so <see cref="Handle.ChangeAxes"/> can filter visible rings after creation.
        /// </summary>
        /// <param name="handle">The parent handle.</param>
        public void Initialize(Handle handle)
        {
            _parentHandle = handle;
            transform.SetParent(_parentHandle.transform, false);

            var hasX = handle.axes.HasAxis(HandleAxes.X);
            var hasY = handle.axes.HasAxis(HandleAxes.Y);
            var hasZ = handle.axes.HasAxis(HandleAxes.Z);

            xAxis.gameObject.SetActive(hasX);
            if (hasX) xAxis.Initialize(handle, Vector3.right);

            yAxis.gameObject.SetActive(hasY);
            if (hasY) yAxis.Initialize(handle, Vector3.up);

            zAxis.gameObject.SetActive(hasZ);
            if (hasZ) zAxis.Initialize(handle, Vector3.forward);
        }
    }
}
