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
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            var renderers = transform.GetComponentsInChildren<Renderer>();
            var renderersCount = renderers.Length;
            
            var averageCenter = Vector3.zero;
            var averageSize = Vector3.zero;
            foreach (var renderer in renderers)
            {
                var bound = renderer.bounds;
                averageCenter += bound.center;
                averageSize += bound.size;
            }
            bounds.center = averageCenter/renderersCount;
            bounds.size = averageSize/renderersCount;
            
            return bounds;
        }
    }
}