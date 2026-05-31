using UnityEngine;

namespace TransformHandles
{
    /// <summary>
    /// Manages the scale handle which allows scaling objects along axes.
    /// </summary>
    public class ScaleHandle : MonoBehaviour
    {
        public ScaleAxis xAxis;
        public ScaleAxis yAxis;
        public ScaleAxis zAxis;

        public ScaleGlobal globalScale;

        private Handle _parentHandle;
        private bool _globalScaleSubscribed;

        /// <summary>
        /// Initializes the scale handle with all its axes.
        /// Re-runnable so <see cref="Handle.ChangeAxes"/> can filter visible axes after creation.
        /// </summary>
        /// <param name="handle">The parent handle.</param>
        public void Initialize(Handle handle)
        {
            _parentHandle = handle;

            var hasX = handle.axes.HasAxis(HandleAxes.X);
            var hasY = handle.axes.HasAxis(HandleAxes.Y);
            var hasZ = handle.axes.HasAxis(HandleAxes.Z);

            xAxis.gameObject.SetActive(hasX);
            if (hasX) xAxis.Initialize(handle, Vector3.right);

            yAxis.gameObject.SetActive(hasY);
            if (hasY) yAxis.Initialize(handle, Vector3.up);

            zAxis.gameObject.SetActive(hasZ);
            if (hasZ) zAxis.Initialize(handle, Vector3.forward);

            var multiAxis = handle.axes.IsMultiAxis();
            globalScale.gameObject.SetActive(multiAxis);
            if (multiAxis)
            {
                globalScale.Initialize(handle, HandleBase.GetVectorFromAxes(handle.axes));

                if (!_globalScaleSubscribed)
                {
                    globalScale.InteractionStart += OnGlobalInteractionStart;
                    globalScale.InteractionUpdate += OnGlobalInteractionUpdate;
                    globalScale.InteractionEnd += OnGlobalInteractionEnd;
                    _globalScaleSubscribed = true;
                }
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from events to prevent memory leaks
            if (_globalScaleSubscribed && globalScale != null)
            {
                globalScale.InteractionStart -= OnGlobalInteractionStart;
                globalScale.InteractionUpdate -= OnGlobalInteractionUpdate;
                globalScale.InteractionEnd -= OnGlobalInteractionEnd;
            }
        }

        private void OnGlobalInteractionStart()
        {
            if (_parentHandle.axes.HasAxis(HandleAxes.X)) xAxis.SetColor(Color.yellow);
            if (_parentHandle.axes.HasAxis(HandleAxes.Y)) yAxis.SetColor(Color.yellow);
            if (_parentHandle.axes.HasAxis(HandleAxes.Z)) zAxis.SetColor(Color.yellow);
        }

        private void OnGlobalInteractionUpdate(float scaleDelta)
        {
            if (_parentHandle.axes.HasAxis(HandleAxes.X)) xAxis.delta = scaleDelta;
            if (_parentHandle.axes.HasAxis(HandleAxes.Y)) yAxis.delta = scaleDelta;
            if (_parentHandle.axes.HasAxis(HandleAxes.Z)) zAxis.delta = scaleDelta;
        }

        private void OnGlobalInteractionEnd()
        {
            if (_parentHandle.axes.HasAxis(HandleAxes.X))
            {
                xAxis.SetDefaultColor();
                xAxis.delta = 0;
            }

            if (_parentHandle.axes.HasAxis(HandleAxes.Y))
            {
                yAxis.SetDefaultColor();
                yAxis.delta = 0;
            }

            if (_parentHandle.axes.HasAxis(HandleAxes.Z))
            {
                zAxis.SetDefaultColor();
                zAxis.delta = 0;
            }
        }
    }
}
