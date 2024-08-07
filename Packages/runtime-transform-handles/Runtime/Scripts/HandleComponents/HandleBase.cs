using System;
using UnityEngine;

namespace TransformHandles
{
    /// <summary>
    /// Base class for all handle types in the transform handle system.
    /// </summary>
    public abstract class HandleBase : MonoBehaviour
    {
        public event Action OnInteractionStartedEvent;
        public event Action OnInteractionActiveEvent;
        public event Action<float> OnInteractionEndedEvent;
        
        protected HandleGroup HandleGroup;
        protected Color DefaultColor;
        
        /// <summary>
        /// The point in world space where the interaction with this handle began.
        /// </summary>
        protected Vector3 HitPoint;

        /// <summary>
        /// The current delta value representing the change in the handle's transformation.
        /// </summary>
        /// <remarks>
        /// This value is typically used to track the amount of change during an interaction,
        /// such as the distance moved for a position handle or the angle rotated for a rotation handle.
        /// </remarks>
        public float Delta { get; set; }

        public abstract void SetDefaultColor();
        public abstract void SetColor(Color color);
        
        /// <summary>
        /// Initiates an interaction with the handle.
        /// </summary>
        /// <param name="hitPoint">The point in world space where the interaction began.</param>
        public virtual void OnInteractionStarted(Vector3 hitPoint)
        {
            HitPoint = hitPoint;
            OnInteractionStartedEvent?.Invoke();
        }

        /// <summary>
        /// Updates the handle during an ongoing interaction.
        /// </summary>
        /// <param name="previousPosition">The previous position of the interaction point.</param>
        public virtual void OnInteractionActive(Vector3 previousPosition)
        {
            OnInteractionEndedEvent?.Invoke(Delta);
        }

        /// <summary>
        /// Concludes the interaction with the handle.
        /// </summary>
        public virtual void OnInteractionEnded()
        {
            Delta = 0;
            SetDefaultColor();
            OnInteractionActiveEvent?.Invoke();
        }

        /// <summary>
        /// Converts a HandleAxes enum value to a corresponding Vector3.
        /// </summary>
        /// <param name="axes">The axes to convert.</param>
        /// <returns>A Vector3 representing the specified axes.</returns>
        /// <remarks>
        /// For single axes (X, Y, Z), returns a unit vector.
        /// For combined axes (XY, XZ, YZ), returns a vector with 1 in the relevant components.
        /// For XYZ or any unhandled value, returns Vector3.one.
        /// </remarks>
        public static Vector3 GetVectorFromAxes(HandleAxes axes)
        {
            return axes switch
            {
                HandleAxes.X => Vector3.right,
                HandleAxes.Y => Vector3.up,
                HandleAxes.Z => Vector3.forward,
                HandleAxes.XY => new Vector3(1, 1, 0),
                HandleAxes.XZ => new Vector3(1, 0, 1),
                HandleAxes.YZ => new Vector3(0, 1, 1),
                _ => Vector3.one
            };
        }
    }
}