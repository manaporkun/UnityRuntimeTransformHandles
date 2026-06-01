using NUnit.Framework;
using TransformHandles.Utils;
using UnityEngine;

namespace TransformHandles.Tests.Editor
{
    public class MeshUtilsTests
    {
        [TestCase(8)]
        [TestCase(32)]
        public void CreateArc_simple_has_expected_topology(int segmentCount)
        {
            const float radius = 2f;
            var mesh = MeshUtils.CreateArc(radius, Mathf.PI, segmentCount);
            try
            {
                Assert.AreEqual(segmentCount + 2, mesh.vertexCount, "segment count + 1 arc points + 1 center");
                Assert.AreEqual(segmentCount * 3, mesh.triangles.Length, "one triangle (3 indices) per segment");

                var vertices = mesh.vertices;
                // Final vertex is the fan center.
                Assert.AreEqual(Vector3.zero, vertices[segmentCount + 1]);

                // Every arc point lies on the circle of the requested radius (in the XZ plane).
                for (var i = 0; i <= segmentCount; i++)
                {
                    Assert.AreEqual(radius, vertices[i].magnitude, 1e-3f, $"vertex {i} radius");
                    Assert.AreEqual(0f, vertices[i].y, 1e-5f, $"vertex {i} stays in XZ plane");
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void CreateArc_with_center_places_points_at_radius_around_center()
        {
            var center = new Vector3(1f, 2f, 3f);
            var startPoint = center + new Vector3(4f, 0f, 0f);
            const int segmentCount = 16;
            const float radius = 1.5f;

            var mesh = MeshUtils.CreateArc(center, startPoint, Vector3.up, radius, Mathf.PI * 0.5f, segmentCount);
            try
            {
                Assert.AreEqual(segmentCount + 2, mesh.vertexCount);
                Assert.AreEqual(segmentCount * 3, mesh.triangles.Length);

                var vertices = mesh.vertices;
                Assert.AreEqual(center, vertices[segmentCount + 1], "last vertex is the supplied center");

                for (var i = 0; i <= segmentCount; i++)
                {
                    Assert.AreEqual(radius, (vertices[i] - center).magnitude, 1e-3f, $"vertex {i} radius from center");
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void RebuildArc_reuses_the_same_mesh_instance()
        {
            var mesh = MeshUtils.CreateArc(1f, Mathf.PI, 8);
            try
            {
                MeshUtils.RebuildArc(mesh, Vector3.zero, Vector3.right, Vector3.up, 1f, Mathf.PI, 16);
                Assert.AreEqual(16 + 2, mesh.vertexCount, "rebuild rewrites topology in place");
                Assert.AreEqual(16 * 3, mesh.triangles.Length);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
