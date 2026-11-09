using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace TransformHandles
{
    /// <summary>
    /// Manages the scale handle which allows scaling objects along axes.
    /// </summary>
    public class ScaleHandle : MonoBehaviour
    {
        [SerializeField, FormerlySerializedAs("xAxis")] private ScaleAxis _xAxis;
        [SerializeField, FormerlySerializedAs("yAxis")] private ScaleAxis _yAxis;
        [SerializeField, FormerlySerializedAs("zAxis")] private ScaleAxis _zAxis;

        [SerializeField, FormerlySerializedAs("globalScale")] private ScaleGlobal _globalScale;

        /// <summary>The X axis component, wired in the handle prefab.</summary>
        public ScaleAxis XAxis { get => _xAxis; set => _xAxis = value; }
        /// <summary>The Y axis component, wired in the handle prefab.</summary>
        public ScaleAxis YAxis { get => _yAxis; set => _yAxis = value; }
        /// <summary>The Z axis component, wired in the handle prefab.</summary>
        public ScaleAxis ZAxis { get => _zAxis; set => _zAxis = value; }

        /// <summary>The uniform-scale center component, wired in the handle prefab.</summary>
        public ScaleGlobal GlobalScale { get => _globalScale; set => _globalScale = value; }

        /// <inheritdoc cref="XAxis"/>
        [Obsolete("Use XAxis instead.")]
        public ScaleAxis xAxis { get => XAxis; set => XAxis = value; }
        /// <inheritdoc cref="YAxis"/>
        [Obsolete("Use YAxis instead.")]
        public ScaleAxis yAxis { get => YAxis; set => YAxis = value; }
        /// <inheritdoc cref="ZAxis"/>
        [Obsolete("Use ZAxis instead.")]
        public ScaleAxis zAxis { get => ZAxis; set => ZAxis = value; }
        /// <inheritdoc cref="GlobalScale"/>
        [Obsolete("Use GlobalScale instead.")]
        public ScaleGlobal globalScale { get => GlobalScale; set => GlobalScale = value; }

        private Handle _parentHandle;
        private bool _globalScaleSubscribed;

        /// <summary>
        /// Initializes the scale handle with all its axes.
        /// Re-runnable so <see cref="Handle.Axes"/> can filter visible axes after creation.
        /// </summary>
        /// <param name="handle">The parent handle.</param>
        public void Initialize(Handle handle)
        {
            _parentHandle = handle;

            var uniform = handle.UsesUniformGroupScale;
            var hasX = !uniform && handle.Axes.HasAxis(HandleAxes.X);
            var hasY = !uniform && handle.Axes.HasAxis(HandleAxes.Y);
            var hasZ = !uniform && handle.Axes.HasAxis(HandleAxes.Z);

            _xAxis.gameObject.SetActive(hasX);
            if (hasX) _xAxis.Initialize(handle, Vector3.right);

            _yAxis.gameObject.SetActive(hasY);
            if (hasY) _yAxis.Initialize(handle, Vector3.up);

            _zAxis.gameObject.SetActive(hasZ);
            if (hasZ) _zAxis.Initialize(handle, Vector3.forward);

            var multiAxis = uniform ? handle.Axes == HandleAxes.XYZ : handle.Axes.IsMultiAxis();
            _globalScale.gameObject.SetActive(multiAxis);
            if (multiAxis)
            {
                _globalScale.Initialize(handle, HandleBase.GetVectorFromAxes(handle.Axes));

                if (!_globalScaleSubscribed)
                {
                    _globalScale.InteractionStart += OnGlobalInteractionStart;
                    _globalScale.InteractionUpdate += OnGlobalInteractionUpdate;
                    _globalScale.InteractionEnd += OnGlobalInteractionEnd;
                    _globalScaleSubscribed = true;
                }
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from events to prevent memory leaks
            if (_globalScaleSubscribed && _globalScale != null)
            {
                _globalScale.InteractionStart -= OnGlobalInteractionStart;
                _globalScale.InteractionUpdate -= OnGlobalInteractionUpdate;
                _globalScale.InteractionEnd -= OnGlobalInteractionEnd;
            }
        }

        private void OnGlobalInteractionStart()
        {
            if (_xAxis.gameObject.activeSelf) _xAxis.SetColor(Color.yellow);
            if (_yAxis.gameObject.activeSelf) _yAxis.SetColor(Color.yellow);
            if (_zAxis.gameObject.activeSelf) _zAxis.SetColor(Color.yellow);
        }

        private void OnGlobalInteractionUpdate(float scaleDelta)
        {
            if (_xAxis.gameObject.activeSelf) _xAxis.Delta = scaleDelta;
            if (_yAxis.gameObject.activeSelf) _yAxis.Delta = scaleDelta;
            if (_zAxis.gameObject.activeSelf) _zAxis.Delta = scaleDelta;
        }

        private void OnGlobalInteractionEnd()
        {
            // Delta is plain state, safe to clear even on an axis masked out mid-drag and
            // never Initialized; clearing all three avoids a stale delta resurfacing through
            // ScaleAxis.Update if the axis is re-enabled later. SetDefaultColor touches cached
            // materials (null on uninitialized axes), so gate only the color reset by the mask.
            _xAxis.Delta = 0;
            _yAxis.Delta = 0;
            _zAxis.Delta = 0;

            if (_xAxis.gameObject.activeSelf) _xAxis.SetDefaultColor();
            if (_yAxis.gameObject.activeSelf) _yAxis.SetDefaultColor();
            if (_zAxis.gameObject.activeSelf) _zAxis.SetDefaultColor();
        }
    }
}
