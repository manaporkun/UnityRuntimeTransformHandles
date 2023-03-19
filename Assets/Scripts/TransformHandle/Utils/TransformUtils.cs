using UnityEngine;

namespace TransformHandle.Utils
{
    public static class TransformUtils
    {
        // An extension method that takes another transform and returns true if this transform is a parent of that transform
        public static bool IsDeepParentOf(this Transform self, Transform other)
        {
            // Check if this transform is null or identical to the other transform
            if (self == null || self == other)
            {
                return false;
            }
        
            // Use Transform.IsChildOf to check if the other transform is a child of this transform
            return other.IsChildOf(self);
        }
        
        public static Bounds GetBounds(this Transform transform)
        {
            var bounds = new Bounds(transform.position, Vector3.zero);
            foreach (var renderer in transform.GetComponentsInChildren<Renderer>())
            {
                bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }
    }
}