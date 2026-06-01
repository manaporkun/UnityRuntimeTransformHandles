using NUnit.Framework;
using TransformHandles.Utils;

namespace TransformHandles.Tests.Editor
{
    public class SnapUtilsTests
    {
        [TestCase(0.7f, 0.5f, 0.5f)]   // rounds down to nearest 0.5
        [TestCase(0.8f, 0.5f, 1.0f)]   // rounds up to nearest 0.5
        [TestCase(2.4f, 1.0f, 2.0f)]   // rounds to nearest integer
        [TestCase(2.6f, 1.0f, 3.0f)]
        [TestCase(-0.7f, 0.5f, -0.5f)] // negative rounds toward nearest multiple
        [TestCase(0.0f, 0.5f, 0.0f)]
        public void Snap_rounds_to_nearest_multiple(float value, float increment, float expected)
        {
            Assert.AreEqual(expected, SnapUtils.Snap(value, increment), 1e-5f);
        }

        [TestCase(0.73f)]
        [TestCase(-12.5f)]
        [TestCase(0.0f)]
        public void Snap_returns_value_unchanged_when_increment_is_zero(float value)
        {
            // Zero increment means "no snapping" and must not divide by zero / produce NaN.
            var result = SnapUtils.Snap(value, 0f);
            Assert.AreEqual(value, result);
            Assert.IsFalse(float.IsNaN(result));
        }
    }
}
