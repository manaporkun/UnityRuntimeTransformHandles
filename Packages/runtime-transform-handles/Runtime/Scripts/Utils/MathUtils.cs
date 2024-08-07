using UnityEngine;

namespace TransformHandles.Utils
{
    /// <summary>
    /// Provides utility methods for mathematical operations.
    /// </summary>
    public static class MathUtils
    {
        /// <summary>
        /// Calculates the parameter t of the point on the first ray that is closest to the second ray.
        /// </summary>
        /// <param name="ray">The first ray.</param>
        /// <param name="other">The second ray.</param>
        /// <param name="precisionThreshold">
        /// The threshold used to determine if the rays are nearly parallel. 
        /// Smaller values increase precision but may lead to instability for nearly parallel rays.
        /// Default value is 0.001.
        /// </param>
        /// <returns>
        /// The parameter t such that ray.GetPoint(t) is the point on the first ray 
        /// closest to the second ray. Returns 0 if the rays are nearly parallel.
        /// </returns>
        /// <remarks>
        /// This method assumes that both ray directions are normalized.
        /// The algorithm is based on the solution described at:
        /// https://math.stackexchange.com/questions/1036959/midpoint-of-the-shortest-distance-between-2-rays-in-3d
        /// </remarks>
        public static float ClosestPointOnRay(Ray ray, Ray other, float precisionThreshold = 0.001f)
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
            if (Mathf.Abs(bottom) < precisionThreshold)
            {
                return 0;
            }

            var top = ab - bc + bd * (cd - ad);
            return top / bottom;
        }
    }
}