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
        private bool _handleInitialized;
        private bool _globalScaleSubscribed;

        /// <summary>
        /// Initializes the scale handle with all its axes.
        /// </summary>
        /// <param name="handle">The parent handle.</param>
        public void Initialize(Handle handle)
        {
            if (_handleInitialized) return;

            _parentHandle = handle;

            if (_parentHandle.axes.HasAxis(HandleAxes.X))
                xAxis.Initialize(_parentHandle, Vector3.right);

            if (_parentHandle.axes.HasAxis(HandleAxes.Y))
                yAxis.Initialize(_parentHandle, Vector3.up);

            if (_parentHandle.axes.HasAxis(HandleAxes.Z))
                zAxis.Initialize(_parentHandle, Vector3.forward);

            if (_parentHandle.axes.IsMultiAxis())
            {
                globalScale.Initialize(_parentHandle, HandleBase.GetVectorFromAxes(_parentHandle.axes));

                globalScale.InteractionStart += OnGlobalInteractionStart;
                globalScale.InteractionUpdate += OnGlobalInteractionUpdate;
                globalScale.InteractionEnd += OnGlobalInteractionEnd;
                _globalScaleSubscribed = true;
            }

            _handleInitialized = true;
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
