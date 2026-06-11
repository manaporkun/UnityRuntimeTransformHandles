using NUnit.Framework;
using UnityEngine;

namespace TransformHandles.Tests.Editor
{
    /// <summary>
    /// Covers HandleBase.GetVectorFromAxes, which converts an axis mask to the
    /// per-component multiplier ScaleGlobal applies during uniform scaling.
    /// A wrong vector scales the wrong axes.
    /// </summary>
    [TestFixture]
    public class GetVectorFromAxesTests
    {
        private static readonly object[] Cases =
        {
            new object[] { HandleAxes.X, new Vector3(1, 0, 0) },
            new object[] { HandleAxes.Y, new Vector3(0, 1, 0) },
            new object[] { HandleAxes.Z, new Vector3(0, 0, 1) },
            new object[] { HandleAxes.XY, new Vector3(1, 1, 0) },
            new object[] { HandleAxes.XZ, new Vector3(1, 0, 1) },
            new object[] { HandleAxes.YZ, new Vector3(0, 1, 1) },
            new object[] { HandleAxes.XYZ, new Vector3(1, 1, 1) },
        };

        [TestCaseSource(nameof(Cases))]
        public void GetVectorFromAxes_maps_mask_to_multiplier(HandleAxes axes, Vector3 expected)
        {
            Assert.AreEqual(expected, HandleBase.GetVectorFromAxes(axes));
        }
    }
}
