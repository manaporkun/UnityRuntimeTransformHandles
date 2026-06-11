using UnityEngine;

namespace TransformHandles.Utils
{
    /// <summary>
    /// Math helpers for handle interaction geometry.
    /// </summary>
    public static class MathUtils
    {
        private const float PrecisionThreshold = 0.001f;

        /// <summary>
        /// Returns the distance along <paramref name="ray"/> of the point closest to
        /// <paramref name="other"/>. Both ray directions must be normalized. Returns 0 when the
        /// rays are (near-)parallel.
        /// </summary>
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