using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace TransformHandles
{
    /// <summary>
    /// Manages the position handle which allows moving objects along axes and planes.
    /// </summary>
    public class PositionHandle : MonoBehaviour
    {
        [SerializeField, FormerlySerializedAs("xAxis")] private PositionAxis _xAxis;
        [SerializeField, FormerlySerializedAs("yAxis")] private PositionAxis _yAxis;
        [SerializeField, FormerlySerializedAs("zAxis")] private PositionAxis _zAxis;

        [SerializeField, FormerlySerializedAs("xPlane")] private PositionPlane _xPlane;
        [SerializeField, FormerlySerializedAs("yPlane")] private PositionPlane _yPlane;
        [SerializeField, FormerlySerializedAs("zPlane")] private PositionPlane _zPlane;

        /// <summary>The X axis component, wired in the handle prefab.</summary>
        public PositionAxis XAxis { get => _xAxis; set => _xAxis = value; }
        /// <summary>The Y axis component, wired in the handle prefab.</summary>
        public PositionAxis YAxis { get => _yAxis; set => _yAxis = value; }
        /// <summary>The Z axis component, wired in the handle prefab.</summary>
        public PositionAxis ZAxis { get => _zAxis; set => _zAxis = value; }

        /// <summary>The XZ plane component, wired in the handle prefab.</summary>
        public PositionPlane XPlane { get => _xPlane; set => _xPlane = value; }
        /// <summary>The XY plane component, wired in the handle prefab.</summary>
        public PositionPlane YPlane { get => _yPlane; set => _yPlane = value; }
        /// <summary>The YZ plane component, wired in the handle prefab.</summary>
        public PositionPlane ZPlane { get => _zPlane; set => _zPlane = value; }

        /// <inheritdoc cref="XAxis"/>
        [Obsolete("Use XAxis instead.")]
        public PositionAxis xAxis { get => XAxis; set => XAxis = value; }
        /// <inheritdoc cref="YAxis"/>
        [Obsolete("Use YAxis instead.")]
        public PositionAxis yAxis { get => YAxis; set => YAxis = value; }
        /// <inheritdoc cref="ZAxis"/>
        [Obsolete("Use ZAxis instead.")]
        public PositionAxis zAxis { get => ZAxis; set => ZAxis = value; }
        /// <inheritdoc cref="XPlane"/>
        [Obsolete("Use XPlane instead.")]
        public PositionPlane xPlane { get => XPlane; set => XPlane = value; }
        /// <inheritdoc cref="YPlane"/>
        [Obsolete("Use YPlane instead.")]
        public PositionPlane yPlane { get => YPlane; set => YPlane = value; }
        /// <inheritdoc cref="ZPlane"/>
        [Obsolete("Use ZPlane instead.")]
        public PositionPlane zPlane { get => ZPlane; set => ZPlane = value; }

        private Handle _parentHandle;

        /// <summary>
        /// Initializes the position handle with all its axes and planes.
        /// Re-runnable so <see cref="Handle.Axes"/> can filter visible axes/planes after creation.
        /// </summary>
        /// <param name="handle">The parent handle.</param>
        public void Initialize(Handle handle)
        {
            _parentHandle = handle;

            // Planes ship as children of their corresponding axis GameObjects in the prefab.
            // Reparent them onto this handle so masking an axis off doesn't also hide the plane.
            if (_xPlane.transform.parent != transform) _xPlane.transform.SetParent(transform, true);
            if (_yPlane.transform.parent != transform) _yPlane.transform.SetParent(transform, true);
            if (_zPlane.transform.parent != transform) _zPlane.transform.SetParent(transform, true);

            var hasX = handle.Axes.HasAxis(HandleAxes.X);
            var hasY = handle.Axes.HasAxis(HandleAxes.Y);
            var hasZ = handle.Axes.HasAxis(HandleAxes.Z);

            _xAxis.gameObject.SetActive(hasX);
            if (hasX) _xAxis.Initialize(handle);

            _yAxis.gameObject.SetActive(hasY);
            if (hasY) _yAxis.Initialize(handle);

            _zAxis.gameObject.SetActive(hasZ);
            if (hasZ) _zAxis.Initialize(handle);

            var hasXY = handle.Axes.HasBothAxes(HandleAxes.X, HandleAxes.Y);
            var hasYZ = handle.Axes.HasBothAxes(HandleAxes.Y, HandleAxes.Z);
            var hasXZ = handle.Axes.HasBothAxes(HandleAxes.X, HandleAxes.Z);

            // Each plane's quad mesh, raycast normal (perp) and visual offset already agree in
            // the prefab: zPlane=YZ (perp -X), xPlane=XZ (perp Y), yPlane=XY (perp Z). Enable each
            // on the matching axis pair so dragging the gizmo moves in the plane it renders.
            _zPlane.gameObject.SetActive(hasYZ);
            if (hasYZ) _zPlane.Initialize(handle, Vector3.forward, Vector3.up, -Vector3.right);

            _xPlane.gameObject.SetActive(hasXZ);
            if (hasXZ) _xPlane.Initialize(handle, Vector3.right, Vector3.forward, Vector3.up);

            _yPlane.gameObject.SetActive(hasXY);
            if (hasXY) _yPlane.Initialize(handle, Vector3.right, Vector3.up, Vector3.forward);
        }
    }
}
