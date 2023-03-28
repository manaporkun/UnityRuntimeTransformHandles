using System;
using UnityEngine;

namespace TransformHandles
{
    public class HandleBase : MonoBehaviour
    {
        public event Action InteractionStart;
        public event Action InteractionEnd;
        public event Action<float> InteractionUpdate;
        
        protected Handle ParentHandle;

        protected Color DefaultColor;

        protected Material Material;

        protected Vector3 HitPoint;

        public float delta;

        public virtual void SetDefaultColor()
        {
            Material.color = DefaultColor;
        }
        
        public virtual void SetColor(Color color)
        {
            Material.color = color;
        }
        
        public virtual void StartInteraction(Vector3 pHitPoint)
        {
            HitPoint = pHitPoint;
            InteractionStart?.Invoke();
        }

        public virtual void Interact(Vector3 pPreviousPosition)
        {
            InteractionUpdate?.Invoke(delta);
        }

        public virtual void EndInteraction()
        {
            InteractionEnd?.Invoke();
            delta = 0;
            SetDefaultColor();
        }

        public static Vector3 GetVectorFromAxes(HandleAxes axes)
        {
            return axes switch
            {
                HandleAxes.X => new Vector3(1, 0, 0),
                HandleAxes.Y => new Vector3(0, 1, 0),
                HandleAxes.Z => new Vector3(0, 0, 1),
                HandleAxes.XY => new Vector3(1, 1, 0),
                HandleAxes.XZ => new Vector3(1, 0, 1),
                HandleAxes.YZ => new Vector3(0, 1, 1),
                _ => new Vector3(1, 1, 1)
            };
        }
    }
}