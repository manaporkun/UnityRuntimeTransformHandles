using UnityEngine;

namespace TransformHandles.Utils
{
    public static class MathUtils
    {
        private const float PrecisionThreshold = 0.001f;
		
        public static float ClosestPointOnRay(Ray ray, Ray other)
        {
            // based on: https://math.stackexchange.com/questions/1036959/midpoint-of-the-shortest-distance-between-2-rays-in-3d
            // note: directions of both rays must be normalized
            // ray.origin -> a
            // ray.direction -> b
            // other.origin -> c
            // other.direction -> d

            var bd = Vector3.Dot(ray.direction, other.direction);
            var cd = Vector3.Dot(other.origin,  other.direction);
            var ad = Vector3.Dot(ray.origin,    other.direction);
            var bc = Vector3.Dot(ray.direction, other.origin);
            var ab = Vector3.Dot(ray.origin,    ray.direction);
			
            var bottom = bd * bd - 1f;
            if (Mathf.Abs(bottom) < PrecisionThreshold)
            {
                return 0;
            }

            var top = ab - bc + bd * (cd - ad);
            return top / bottom;
        }
    }
}