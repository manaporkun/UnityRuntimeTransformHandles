using System;
using UnityEngine;

namespace TransformHandle
{
    public class HandleCameraEventHandler : MonoBehaviour
    {
        public event Action PreRender;
        
        private void OnPreRender()
        {
            PreRender?.Invoke();
        }
    }
}