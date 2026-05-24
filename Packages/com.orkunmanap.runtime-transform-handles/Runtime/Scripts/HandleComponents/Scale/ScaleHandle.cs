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

            var hasX = _parentHandle.axes.HasAxis(HandleAxes.X);
            var hasY = _parentHandle.axes.HasAxis(HandleAxes.Y);
            var hasZ = _parentHandle.axes.HasAxis(HandleAxes.Z);

            xAxis.gameObject.SetActive(hasX);
            if (hasX) xAxis.Initialize(_parentHandle, Vector3.right);

            yAxis.gameObject.SetActive(hasY);
            if (hasY) yAxis.Initialize(_parentHandle, Vector3.up);

            zAxis.gameObject.SetActive(hasZ);
            if (hasZ) zAxis.Initialize(_parentHandle, Vector3.forward);

            var multiAxis = _parentHandle.axes.IsMultiAxis();
            globalScale.gameObject.SetActive(multiAxis);
            if (multiAxis)
            {
                globalScale.Initialize(_parentHandle, HandleBase.GetVectorFromAxes(_parentHandle.axes));

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
            xAxis.SetColor(Color.yellow);
            yAxis.SetColor(Color.yellow);
            zAxis.SetColor(Color.yellow);
        }

        private void OnGlobalInteractionUpdate(float scaleDelta)
        {
            xAxis.delta = scaleDelta;
            yAxis.delta = scaleDelta;
            zAxis.delta = scaleDelta;
        }

        private void OnGlobalInteractionEnd()
        {
            xAxis.SetDefaultColor();
            xAxis.delta = 0;

            yAxis.SetDefaultColor();
            yAxis.delta = 0;

            zAxis.SetDefaultColor();
            zAxis.delta = 0;
        }
    }
}
