using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace TransformHandles.Tests
{
    public class TransformGroupTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private Handle _handle;

        [SetUp]
        public void SetUp()
        {
            // Inactive so Handle.Awake/OnEnable (which would touch the manager singleton) stays dormant;
            // TransformGroup only reads the public `space` field, which defaults to Space.Self.
            var handleGo = NewObject("handle");
            handleGo.SetActive(false);
            _handle = handleGo.AddComponent<Handle>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();
        }

        private GameObject NewObject(string name, Vector3 position = default)
        {
            var go = new GameObject(name) { transform = { position = position } };
            _spawned.Add(go);
            return go;
        }

        private TransformGroup NewGroup() => new TransformGroup(null, _handle);

        [Test]
        public void AddTransform_accepts_unrelated_targets()
        {
            var group = NewGroup();
            Assert.IsTrue(group.AddTransform(NewObject("a").transform));
            Assert.IsTrue(group.AddTransform(NewObject("b").transform));
            Assert.AreEqual(2, group.Transforms.Count);
        }

        [Test]
        public void AddTransform_rejects_null()
        {
            var group = NewGroup();
            // AddTransform logs a warning on null; warnings do not fail the test.
            Assert.IsFalse(group.AddTransform(null));
            Assert.AreEqual(0, group.Transforms.Count);
        }

        [Test]
        public void AddTransform_rejects_child_of_existing_member()
        {
            var parent = NewObject("parent").transform;
            var child = NewObject("child").transform;
            child.SetParent(parent);

            var group = NewGroup();
            Assert.IsTrue(group.AddTransform(parent));
            Assert.IsFalse(group.AddTransform(child), "a child of an existing member is rejected");
            Assert.AreEqual(1, group.Transforms.Count);
            Assert.IsTrue(group.Transforms.Contains(parent));
        }

        [Test]
        public void AddTransform_adding_a_parent_replaces_its_existing_child()
        {
            var parent = NewObject("parent").transform;
            var child = NewObject("child").transform;
            child.SetParent(parent);

            var group = NewGroup();
            Assert.IsTrue(group.AddTransform(child));
            Assert.IsTrue(group.AddTransform(parent), "adding a parent removes the contained child and is accepted");
            Assert.AreEqual(1, group.Transforms.Count);
            Assert.IsTrue(group.Transforms.Contains(parent));
            Assert.IsFalse(group.Transforms.Contains(child));
        }

        [Test]
        public void GetAveragePosRotScale_averages_member_positions()
        {
            var group = NewGroup();
            group.AddTransform(NewObject("a", new Vector3(0f, 0f, 0f)).transform);
            group.AddTransform(NewObject("b", new Vector3(2f, 0f, 0f)).transform);
            group.AddTransform(NewObject("c", new Vector3(4f, 0f, 0f)).transform);

            var result = group.GetAveragePosRotScale();

            Assert.AreEqual(new Vector3(2f, 0f, 0f), result.Position);
            Assert.AreEqual(Vector3.one, result.Scale);
        }

        [Test]
        public void GetAveragePosRotScale_handles_empty_group()
        {
            var group = NewGroup();
            var result = group.GetAveragePosRotScale();

            Assert.AreEqual(Vector3.zero, result.Position);
            Assert.AreEqual(Quaternion.identity, result.Rotation);
            Assert.AreEqual(Vector3.one, result.Scale);
        }

        [Test]
        public void RemoveTransform_reports_when_group_becomes_empty()
        {
            var group = NewGroup();
            var a = NewObject("a").transform;
            var b = NewObject("b").transform;
            group.AddTransform(a);
            group.AddTransform(b);

            Assert.IsFalse(group.RemoveTransform(a), "still has one member");
            Assert.IsTrue(group.RemoveTransform(b), "now empty");
        }
    }
}
