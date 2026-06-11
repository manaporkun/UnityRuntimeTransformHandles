using UnityEngine;

namespace TransformHandles.Utils
{
    /// <summary>
    /// Runtime equivalents of Unity Editor handle math used by the built-in scale tool
    /// (HandleUtility.CalcLineTranslation and HandleUtility.GetHandleSize).
    /// </summary>
    public static class HandleTransformUtility
    {
        /// <summary>Matches Unity Editor <c>HandleUtility.k_KHandleSize</c>.</summary>
        public const float DefaultHandleSizeConstant = 80f;

        /// <summary>
        /// Returns the world-space handle size at <paramref name="position"/> for the given camera.
        /// Mirrors Unity Editor <c>HandleUtility.GetHandleSize</c>.
        /// </summary>
        public static float GetHandleSize(Vector3 position, Camera camera, float handleSizeConstant = DefaultHandleSizeConstant)
        {
            if (camera == null) return 20f;

            var tr = camera.transform;
            var camPos = tr.position;
            var distance = Vector3.Dot(position - camPos, tr.TransformDirection(Vector3.forward));
            var screenPos = camera.WorldToScreenPoint(camPos + tr.TransformDirection(new Vector3(0f, 0f, distance)));
            var screenPos2 = camera.WorldToScreenPoint(camPos + tr.TransformDirection(new Vector3(1f, 0f, distance)));
            var screenDist = (screenPos - screenPos2).magnitude;
            return handleSizeConstant / Mathf.Max(screenDist, 0.0001f);
        }

        /// <summary>
        /// Maps a screen-space drag onto movement along a 3D line. Screen coordinates must use a
        /// bottom-left origin (Unity <c>Input.mousePosition</c> / <c>Camera.WorldToScreenPoint</c>).
        /// Mirrors Unity Editor <c>HandleUtility.CalcLineTranslation</c>.
        /// </summary>
        public static float CalcLineTranslation(Vector2 src, Vector2 dest, Vector3 srcPosition, Vector3 constraintDir, Camera camera)
        {
            var invert = 1f;
            var cameraForward = camera != null ? camera.transform.forward : Vector3.forward;
            if (Vector3.Dot(constraintDir, cameraForward) < 0f)
                invert = -1f;

            Vector2 p1;
            Vector2 p2;
            if (camera == null)
            {
                p1 = Vector2.Scale(srcPosition, new Vector2(1f, -1f));
                p2 = Vector2.Scale(srcPosition + constraintDir * invert, new Vector2(1f, -1f));
            }
            else
            {
                p1 = camera.WorldToScreenPoint(srcPosition);
                p2 = camera.WorldToScreenPoint(srcPosition + constraintDir * invert);
            }

            var p3 = dest;
            var p4 = src;

            if (p1 == p2)
                return 0f;

            var t0 = GetParametrization(p4, p1, p2);
            var t1 = GetParametrization(p3, p1, p2);
            return (t1 - t0) * invert;
        }

        internal static float GetParametrization(Vector2 x0, Vector2 x1, Vector2 x2)
        {
            return -(Vector2.Dot(x1 - x0, x2 - x1) / (x2 - x1).sqrMagnitude);
        }

        /// <summary>
        /// Local Y scale for a scale-axis line mesh so the line ends at the inner face of the
        /// gizmo cube instead of extending through it: the line spans from the handle origin to
        /// <paramref name="cubeReach"/> minus <paramref name="cubeHalfExtent"/>, never negative.
        /// </summary>
        /// <param name="cubeReach">Distance from the handle origin to the cube center.</param>
        /// <param name="cubeHalfExtent">Half the cube's extent along the drag axis.</param>
        /// <param name="lineMeshLength">Unscaled length of the line mesh along its Y axis.</param>
        public static float LineScaleForCubeReach(float cubeReach, float cubeHalfExtent, float lineMeshLength)
        {
            if (lineMeshLength <= 0f) return 0f;
            var lineReach = Mathf.Max(0f, cubeReach - cubeHalfExtent);
            return lineReach / lineMeshLength;
        }
    }
}
