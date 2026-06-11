using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace TransformHandles
{
    /// <summary>
    /// Manages the rotation handle which allows rotating objects around axes.
    /// </summary>
    public class RotationHandle : MonoBehaviour
    {
        [SerializeField, FormerlySerializedAs("xAxis")] private RotationAxis _xAxis;
        [SerializeField, FormerlySerializedAs("yAxis")] private RotationAxis _yAxis;
        [SerializeField, FormerlySerializedAs("zAxis")] private RotationAxis _zAxis;

        /// <summary>The X axis ring component, wired in the handle prefab.</summary>
        public RotationAxis XAxis { get => _xAxis; set => _xAxis = value; }
        /// <summary>The Y axis ring component, wired in the handle prefab.</summary>
        public RotationAxis YAxis { get => _yAxis; set => _yAxis = value; }
        /// <summary>The Z axis ring component, wired in the handle prefab.</summary>
        public RotationAxis ZAxis { get => _zAxis; set => _zAxis = value; }

        /// <inheritdoc cref="XAxis"/>
        [Obsolete("Use XAxis instead.")]
        public RotationAxis xAxis { get => XAxis; set => XAxis = value; }
        /// <inheritdoc cref="YAxis"/>
        [Obsolete("Use YAxis instead.")]
        public RotationAxis yAxis { get => YAxis; set => YAxis = value; }
        /// <inheritdoc cref="ZAxis"/>
        [Obsolete("Use ZAxis instead.")]
        public RotationAxis zAxis { get => ZAxis; set => ZAxis = value; }

        private Handle _parentHandle;

        /// <summary>
        /// Initializes the rotation handle with all its axes.
        /// Re-runnable so <see cref="Handle.Axes"/> can filter visible rings after creation.
        /// </summary>
        /// <param name="handle">The parent handle.</param>
        public void Initialize(Handle handle)
        {
            _parentHandle = handle;
            transform.SetParent(_parentHandle.transform, false);

            var hasX = handle.Axes.HasAxis(HandleAxes.X);
            var hasY = handle.Axes.HasAxis(HandleAxes.Y);
            var hasZ = handle.Axes.HasAxis(HandleAxes.Z);

            _xAxis.gameObject.SetActive(hasX);
            if (hasX) _xAxis.Initialize(handle, Vector3.right);

            _yAxis.gameObject.SetActive(hasY);
            if (hasY) _yAxis.Initialize(handle, Vector3.up);

            _zAxis.gameObject.SetActive(hasZ);
            if (hasZ) _zAxis.Initialize(handle, Vector3.forward);
        }
    }
}
