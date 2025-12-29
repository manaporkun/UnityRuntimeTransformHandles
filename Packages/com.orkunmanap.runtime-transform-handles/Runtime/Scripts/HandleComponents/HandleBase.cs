using System;
using UnityEngine;

namespace TransformHandles
{
    /// <summary>
    /// Base class for all handle components (axes, planes, etc.).
    /// Provides common functionality for interaction, color management, and coordinate space transformations.
    /// </summary>
    public class HandleBase : MonoBehaviour
    {
        /// <summary>Event fired when interaction starts.</summary>
        public event Action InteractionStart;
        /// <summary>Event fired when interaction ends.</summary>
        public event Action InteractionEnd;
        /// <summary>Event fired during interaction with the current delta value.</summary>
        public event Action<float> InteractionUpdate;

        /// <summary>The parent handle that owns this component.</summary>
        protected Handle ParentHandle;

        /// <summary>The default color of this handle component.</summary>
        protected Color DefaultColor;

        /// <summary>The material used for rendering this component.</summary>
        protected Material Material;

        /// <summary>The point where the interaction started.</summary>
        protected Vector3 HitPoint;

        /// <summary>The change in value during interaction.</summary>
        public float delta;

        protected virtual void OnDestroy()
        {
            // Clear event subscribers to prevent memory leaks
            InteractionStart = null;
            InteractionEnd = null;
            InteractionUpdate = null;
        }

        /// <summary>
        /// Resets the component to its default color.
        /// </summary>
        public virtual void SetDefaultColor()
        {
            if (Material != null)
                Material.color = DefaultColor;
        }

        /// <summary>
        /// Sets the component to the specified color.
        /// </summary>
        /// <param name="color">The color to apply.</param>
        public virtual void SetColor(Color color)
        {
            if (Material != null)
                Material.color = color;
        }

        /// <summary>
        /// Called when interaction with this component begins.
        /// </summary>
        /// <param name="hitPoint">The world position where the interaction started.</param>
        public virtual void StartInteraction(Vector3 hitPoint)
        {
            HitPoint = hitPoint;
            InteractionStart?.Invoke();
        }

        /// <summary>
        /// Called during continuous interaction with this component.
        /// </summary>
        /// <param name="previousPosition">The previous mouse position.</param>
        public virtual void Interact(Vector3 previousPosition)
        {
            InteractionUpdate?.Invoke(delta);
        }

        /// <summary>
        /// Called when interaction with this component ends.
        /// </summary>
        public virtual void EndInteraction()
        {
            InteractionEnd?.Invoke();
            delta = 0;
            SetDefaultColor();
        }

        /// <summary>
        /// Gets an axis rotated according to the current coordinate space.
        /// </summary>
        /// <param name="axis">The axis in local space.</param>
        /// <returns>The axis rotated to match the current coordinate space.</returns>
        protected Vector3 GetRotatedAxis(Vector3 axis)
        {
            return ParentHandle.space == Space.Self
                ? ParentHandle.target.rotation * axis
                : axis;
        }

        /// <summary>
        /// Converts a HandleAxes enum value to a Vector3 direction.
        /// </summary>
        /// <param name="axes">The axes configuration.</param>
        /// <returns>A Vector3 with 1s for active axes and 0s for inactive ones.</returns>
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
