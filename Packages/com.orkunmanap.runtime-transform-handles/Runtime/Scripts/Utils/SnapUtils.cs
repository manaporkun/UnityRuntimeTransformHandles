using UnityEngine;

namespace TransformHandles.Utils
{
    /// <summary>
    /// Snapping helpers shared by the position, rotation, and scale handle components.
    /// </summary>
    public static class SnapUtils
    {
        /// <summary>
        /// Rounds <paramref name="value"/> to the nearest multiple of <paramref name="increment"/>.
        /// Returns <paramref name="value"/> unchanged when <paramref name="increment"/> is zero
        /// (snapping disabled), avoiding a divide-by-zero / NaN result.
        /// </summary>
        /// <param name="value">The raw value to snap.</param>
        /// <param name="increment">The snap step. Zero means "no snapping".</param>
        /// <returns>The snapped value, or the original value when <paramref name="increment"/> is zero.</returns>
        public static float Snap(float value, float increment)
        {
            if (increment == 0f) return value;
            return Mathf.Round(value / increment) * increment;
        }
    }
}
