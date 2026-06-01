using UnityEngine;

namespace TransformHandles.Utils
{
    public static class TransformUtils
    {
        public static bool IsDeepParentOf(this Transform self, Transform other)
        {
            if (self == null || self == other)
            {
                return false;
            }
        
            return other.IsChildOf(self);
        }

        public static Bounds GetBounds(this Transform transform)
        {
            var renderers = transform.GetComponentsInChildren<Renderer>();

            // No renderers: return a zero-size bounds at the transform position. The previous
            // code divided by renderers.Length here and produced NaN bounds for renderer-less
            // targets (e.g. empty pivots), which then poisoned pivot/center placement.
            if (renderers.Length == 0)
            {
                return new Bounds(transform.position, Vector3.zero);
            }

            // Encapsulate every child renderer into one combined AABB. Averaging the centers and
            // sizes (the previous approach) yielded a box that neither contained the children nor
            // sat at their collective center once more than one renderer was involved.
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }
    }
}