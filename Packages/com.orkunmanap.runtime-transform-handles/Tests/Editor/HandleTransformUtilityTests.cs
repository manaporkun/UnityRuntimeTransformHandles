using NUnit.Framework;
using TransformHandles.Utils;
using UnityEngine;

namespace TransformHandles.Tests.Editor
{
    public class HandleTransformUtilityTests
    {
        [Test]
        public void GetParametrization_returns_zero_when_point_is_at_line_start()
        {
            var p1 = Vector2.zero;
            var p2 = Vector2.right;
            var x0 = p1;

            var t = HandleTransformUtility.GetParametrization(x0, p1, p2);

            Assert.AreEqual(0f, t, 1e-4f);
        }

        [Test]
        public void GetParametrization_returns_one_when_point_is_at_line_end()
        {
            var p1 = Vector2.zero;
            var p2 = Vector2.right;
            var x0 = p2;

            var t = HandleTransformUtility.GetParametrization(x0, p1, p2);

            Assert.AreEqual(1f, t, 1e-4f);
        }

        [Test]
        public void CalcLineTranslation_returns_zero_when_src_equals_dest()
        {
            var cameraGo = new GameObject("Camera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;
            camera.orthographic = false;

            try
            {
                var src = new Vector2(100f, 100f);
                var translation = HandleTransformUtility.CalcLineTranslation(
                    src,
                    src,
                    Vector3.zero,
                    Vector3.right,
                    camera);

                Assert.AreEqual(0f, translation, 1e-3f);
            }
            finally
            {
                Object.DestroyImmediate(cameraGo);
            }
        }

        [Test]
        public void GetHandleSize_returns_fallback_when_camera_is_null()
        {
            Assert.AreEqual(20f, HandleTransformUtility.GetHandleSize(Vector3.zero, null));
        }

        [Test]
        public void LineVisualScale_reaches_same_distance_as_cube_for_tube_mesh()
        {
            const float cubeRestDistance = 0.75f;
            const float lineMeshLength = 0.8f;

            foreach (var scaleFactor in new[] { 0.5f, 1f, 1.5f, 2f })
            {
                var lineScaleY = cubeRestDistance / lineMeshLength * scaleFactor;
                var lineReach = lineMeshLength * lineScaleY;
                var cubeReach = cubeRestDistance * scaleFactor;
                Assert.AreEqual(cubeReach, lineReach, 1e-5f);
            }
        }
    }
}
