using NUnit.Framework;
using TransformHandles;

namespace TransformHandles.Tests.Editor
{
    public class HandleAxesExtensionsTests
    {
        // HasAxis(X) ----------------------------------------------------------
        [TestCase(HandleAxes.X, true)]
        [TestCase(HandleAxes.Y, false)]
        [TestCase(HandleAxes.Z, false)]
        [TestCase(HandleAxes.XY, true)]
        [TestCase(HandleAxes.XZ, true)]
        [TestCase(HandleAxes.YZ, false)]
        [TestCase(HandleAxes.XYZ, true)]
        public void HasAxis_X(HandleAxes axes, bool expected)
        {
            Assert.AreEqual(expected, axes.HasAxis(HandleAxes.X));
        }

        // HasAxis(Y) ----------------------------------------------------------
        [TestCase(HandleAxes.X, false)]
        [TestCase(HandleAxes.Y, true)]
        [TestCase(HandleAxes.Z, false)]
        [TestCase(HandleAxes.XY, true)]
        [TestCase(HandleAxes.XZ, false)]
        [TestCase(HandleAxes.YZ, true)]
        [TestCase(HandleAxes.XYZ, true)]
        public void HasAxis_Y(HandleAxes axes, bool expected)
        {
            Assert.AreEqual(expected, axes.HasAxis(HandleAxes.Y));
        }

        // HasAxis(Z) ----------------------------------------------------------
        [TestCase(HandleAxes.X, false)]
        [TestCase(HandleAxes.Y, false)]
        [TestCase(HandleAxes.Z, true)]
        [TestCase(HandleAxes.XY, false)]
        [TestCase(HandleAxes.XZ, true)]
        [TestCase(HandleAxes.YZ, true)]
        [TestCase(HandleAxes.XYZ, true)]
        public void HasAxis_Z(HandleAxes axes, bool expected)
        {
            Assert.AreEqual(expected, axes.HasAxis(HandleAxes.Z));
        }

        // HasBothAxes ---------------------------------------------------------
        [Test]
        public void HasBothAxes_XY_true_only_when_both_present()
        {
            Assert.IsTrue(HandleAxes.XY.HasBothAxes(HandleAxes.X, HandleAxes.Y));
            Assert.IsTrue(HandleAxes.XYZ.HasBothAxes(HandleAxes.X, HandleAxes.Y));
            Assert.IsFalse(HandleAxes.X.HasBothAxes(HandleAxes.X, HandleAxes.Y));
            Assert.IsFalse(HandleAxes.XZ.HasBothAxes(HandleAxes.X, HandleAxes.Y));
        }

        // IsMultiAxis ---------------------------------------------------------
        [TestCase(HandleAxes.X, false)]
        [TestCase(HandleAxes.Y, false)]
        [TestCase(HandleAxes.Z, false)]
        [TestCase(HandleAxes.XY, true)]
        [TestCase(HandleAxes.XZ, true)]
        [TestCase(HandleAxes.YZ, true)]
        [TestCase(HandleAxes.XYZ, true)]
        public void IsMultiAxis(HandleAxes axes, bool expected)
        {
            Assert.AreEqual(expected, axes.IsMultiAxis());
        }
    }
}
