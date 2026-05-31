using NUnit.Framework;
using TransformHandles.Utils;
using UnityEngine;

namespace TransformHandles.Tests.Editor
{
    public class MathUtilsTests
    {
        [Test]
        public void ClosestPointOnRay_returns_parameter_of_intersection()
        {
            // Ray along +X through the origin.
            var ray = new Ray(Vector3.zero, Vector3.right);
            // Other line is the vertical (Z) line at x = 5, which the X-axis crosses at (5,0,0).
            var other = new Ray(new Vector3(5f, 0f, 3f), Vector3.forward);

            var t = MathUtils.ClosestPointOnRay(ray, other);

            Assert.AreEqual(5f, t, 1e-4f);
            Assert.AreEqual(new Vector3(5f, 0f, 0f), ray.GetPoint(t));
        }

        [Test]
        public void ClosestPointOnRay_returns_zero_for_parallel_rays()
        {
            // Parallel directions make the denominator (bd*bd - 1) collapse to ~0; the
            // degenerate guard must return 0 instead of dividing by (near-)zero.
            var ray = new Ray(Vector3.zero, Vector3.right);
            var other = new Ray(new Vector3(0f, 1f, 0f), Vector3.right);

            var t = MathUtils.ClosestPointOnRay(ray, other);

            Assert.AreEqual(0f, t);
        }

        [Test]
        public void ClosestPointOnRay_returns_zero_for_antiparallel_rays()
        {
            // Anti-parallel (bd = -1) also collapses the denominator to ~0.
            var ray = new Ray(Vector3.zero, Vector3.right);
            var other = new Ray(new Vector3(0f, 1f, 0f), Vector3.left);

            var t = MathUtils.ClosestPointOnRay(ray, other);

            Assert.AreEqual(0f, t);
        }
    }
}
