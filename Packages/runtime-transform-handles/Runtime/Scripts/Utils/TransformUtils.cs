using UnityEngine;

namespace TransformHandles.Utils
{
    /// <summary>
    /// Provides utility extension methods for Unity's Transform component.
    /// </summary>
    public static class TransformUtils
    {
        /// <summary>
        /// Checks if the current transform is a deep parent of another transform.
        /// </summary>
        /// <param name="self">The transform to check as a potential parent.</param>
        /// <param name="other">The transform to check as a potential child.</param>
        /// <returns>True if 'self' is a deep parent of 'other', false otherwise.</returns>
        public static bool IsDeepParentOf(this Transform self, Transform other)
        {
            if (self == null || other == null || self == other)
            {
                return false;
            }
        
            return other.IsChildOf(self);
        }

        /// <summary>
        /// Calculates the bounds of a transform, including all child renderers.
        /// </summary>
        /// <param name="transform">The transform to calculate bounds for.</param>
        /// <returns>The calculated bounds.</returns>
        public static Bounds GetBounds(this Transform transform)
        {
            var renderers = transform.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(transform.position, Vector3.zero);
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }
    }
}