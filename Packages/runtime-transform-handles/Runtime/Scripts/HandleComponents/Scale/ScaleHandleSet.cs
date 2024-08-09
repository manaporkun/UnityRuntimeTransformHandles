using UnityEngine;

namespace TransformHandles
{
    public class ScaleHandleSet : MonoBehaviour
    {
        public ScaleHandle XHandle;
        public ScaleHandle YHandle;
        public ScaleHandle ZHandle;
        public UniformScaleHandle HandleUniformScale;

        private bool _isInitialized;

        public void Initialize(HandleGroup handleGroup)
        {
            if (_isInitialized) return;

            var axes = handleGroup._axes;
            InitializeHandles(axes, handleGroup);
            InitializeUniformScaleHandle(handleGroup, axes);

            _isInitialized = true;
        }

        private void InitializeHandles(HandleAxes axes, HandleGroup handleGroup)
        {
            if ((axes & HandleAxes.X) != 0) InitializeHandle(handleGroup, XHandle, Vector3.right);
            if ((axes & HandleAxes.Y) != 0) InitializeHandle(handleGroup, YHandle, Vector3.up);
            if ((axes & HandleAxes.Z) != 0) InitializeHandle(handleGroup, ZHandle, Vector3.forward);
        }

        private static void InitializeHandle(HandleGroup handleGroup, ScaleHandle handle, Vector3 direction)
        {
            if (handle != null)
            {
                handle.gameObject.SetActive(true);
                handle.Initialize(handleGroup, direction);
            }
        }

        private void InitializeUniformScaleHandle(HandleGroup handleGroup, HandleAxes axes)
        {
            if (IsAllAxes(axes))
            {
                HandleUniformScale.Initialize(handleGroup, HandleBase.GetVectorFromAxes(axes));

                HandleUniformScale.OnInteractionStartedEvent += UniformInteractionStart;
                HandleUniformScale.OnInteractionEndedEvent += UniformInteractionEnded;
                HandleUniformScale.OnInteractionActiveEvent += UniformInteractionActive;
            }
        }

        private static bool IsAllAxes(HandleAxes axes)
        {
            return (axes & (axes - 1)) != 0;
        }

        private void UniformInteractionStart()
        {
            SetHandlesColor(Color.yellow);
        }

        private void UniformInteractionActive(float delta)
        {
            SetHandlesDelta(delta);
        }

        private void UniformInteractionEnded()
        {
            ResetHandles();
        }

        private void ResetHandles()
        {
            SetHandlesDefaultColor();
            SetHandlesDelta(0);
        }

        private void SetHandlesDefaultColor()
        {
            XHandle?.SetColor(XHandle.DefaultColor);
            YHandle?.SetColor(YHandle.DefaultColor);
            ZHandle?.SetColor(ZHandle.DefaultColor);
        }

        private void SetHandlesColor(Color color)
        {
            XHandle?.SetColor(color);
            YHandle?.SetColor(color);
            ZHandle?.SetColor(color);
        }
        
        private void SetHandlesDelta(float delta)
        {
            if (XHandle != null) XHandle.delta = delta;
            if (YHandle != null) YHandle.delta = delta;
            if (ZHandle != null) ZHandle.delta = delta;
        }
    }
}