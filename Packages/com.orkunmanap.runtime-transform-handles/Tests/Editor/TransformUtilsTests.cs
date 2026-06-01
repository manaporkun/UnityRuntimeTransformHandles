using System.Collections.Generic;
using NUnit.Framework;
using TransformHandles.Utils;
using UnityEngine;

namespace TransformHandles.Tests.Editor
{
    public class TransformUtilsTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();
        }

        private GameObject NewEmpty(string name, Vector3 position)
        {
            var go = new GameObject(name) { transform = { position = position } };
            _spawned.Add(go);
            return go;
        }

        private GameObject NewCube(string name, Vector3 position, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent);
            go.transform.position = position;
            _spawned.Add(go);
            return go;
        }

        [Test]
        public void GetBounds_no_renderers_returns_zero_size_at_position_without_nan()
        {
            // Regression: the old implementation divided by renderers.Length (== 0) and produced
            // NaN bounds for renderer-less targets, poisoning pivot/center placement.
            var empty = NewEmpty("empty", new Vector3(3f, 4f, 5f));

            var bounds = empty.transform.GetBounds();

            Assert.AreEqual(new Vector3(3f, 4f, 5f), bounds.center);
            Assert.AreEqual(Vector3.zero, bounds.size);
            Assert.IsFalse(float.IsNaN(bounds.center.x) || float.IsNaN(bounds.size.x), "bounds must not be NaN");
        }

        [Test]
        public void GetBounds_single_renderer_matches_renderer_bounds()
        {
            var cube = NewCube("cube", new Vector3(2f, 0f, 0f));
            var expected = cube.GetComponent<Renderer>().bounds;

            var bounds = cube.transform.GetBounds();

            Assert.That(Vector3.Distance(bounds.center, expected.center), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(bounds.size, expected.size), Is.LessThan(1e-4f));
        }

        [Test]
        public void GetBounds_multiple_renderers_encapsulates_all()
        {
            // Two unit cubes 4 units apart on X. The combined AABB must contain both and span the
            // full extent (size.x == 5), not the average of the two (which would be ~1).
            var root = NewEmpty("root", Vector3.zero);
            var left = NewCube("left", new Vector3(-2f, 0f, 0f), root.transform);
            var right = NewCube("right", new Vector3(2f, 0f, 0f), root.transform);

            var bounds = root.transform.GetBounds();

            Assert.That(Vector3.Distance(bounds.center, Vector3.zero), Is.LessThan(1e-4f));
            Assert.That(bounds.size.x, Is.EqualTo(5f).Within(1e-3f));
            Assert.IsTrue(bounds.Contains(left.GetComponent<Renderer>().bounds.center));
            Assert.IsTrue(bounds.Contains(right.GetComponent<Renderer>().bounds.center));
        }
    }
}
