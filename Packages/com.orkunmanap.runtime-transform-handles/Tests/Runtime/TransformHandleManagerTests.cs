using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TransformHandles.Tests
{
    public class TransformHandleManagerTests
    {
        private TransformHandleManager _manager;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            var cameraGo = NewObject("Main Camera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.tag = "MainCamera";

            _manager = TransformHandleManager.Instance;
            _manager.MainCamera = camera;
            _manager.DestroyAllHandles();
        }

        [TearDown]
        public void TearDown()
        {
            if (_manager != null)
            {
                _manager.DestroyAllHandles();
                Object.DestroyImmediate(_manager.gameObject);
                _manager = null;
            }

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

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void HoverEmphasisPreservesHueAndRestoresAllComponentTypes(int kind)
        {
            var settings = TransformHandleSettings.CreateDefault();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(TransformHandleSettings).GetField("emphasizeHandleColorOnHover", flags).SetValue(settings, true);
            typeof(TransformHandleSettings).GetField("preserveHandleColorOnHover", flags).SetValue(settings, true);
            _manager.Settings = settings;
            try
            {
                var handle = _manager.CreateHandle(NewObject("target").transform);
                TransformHandleManager.ChangeHandleType(handle, kind < 2 ? HandleType.Position : kind == 2 ? HandleType.Rotation : HandleType.Scale);
                HandleBase component;
                switch (kind)
                {
                    case 0: component = handle.GetComponentInChildren<PositionAxis>(true); break;
                    case 1: component = handle.GetComponentInChildren<PositionPlane>(true); break;
                    case 2: component = handle.GetComponentInChildren<RotationAxis>(true); break;
                    case 3: component = handle.GetComponentInChildren<ScaleAxis>(true); break;
                    default: component = handle.GetComponentInChildren<ScaleGlobal>(true); break;
                }
                var material = component.GetComponentInChildren<MeshRenderer>(true).sharedMaterial;
                var original = material.color;
                var hover = typeof(TransformHandleManager).GetMethod("HandleOverEffect", flags);
                hover.Invoke(_manager, new object[] { component });
                var highlighted = material.color;
                Assert.AreNotEqual(original, highlighted, "Emphasis takes priority over exact preservation.");
                Color.RGBToHSV(original, out var h, out var sat, out var val);
                Color.RGBToHSV(highlighted, out var hh, out var hs, out var hv);
                Assert.That(hh, Is.EqualTo(h).Within(.0001f));
                Assert.That(hs, Is.EqualTo(sat).Within(.0001f));
                Assert.Less(hv, val);
                if (kind == 1) Assert.Greater(highlighted.a, original.a);
                hover.Invoke(_manager, new object[] { component });
                Assert.AreEqual(highlighted, material.color, "Repeated hover cannot accumulate tint.");
                hover.Invoke(_manager, new object[] { null });
                Assert.AreEqual(original, material.color);
                hover.Invoke(_manager, new object[] { component });
                Assert.IsTrue(_manager.BeginInteraction(component, handle.transform.position));
                _manager.enabled = false;
                Assert.AreEqual(original, material.color);
            }
            finally { _manager.Settings = null; Object.DestroyImmediate(settings); }
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void HoverPreservesComponentColorOnlyWhenEnabled(bool preserve, bool plane)
        {
            var settings = TransformHandleSettings.CreateDefault();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(TransformHandleSettings).GetField("preserveHandleColorOnHover", flags).SetValue(settings, preserve);
            _manager.Settings = settings;
            try
            {
                var handle = _manager.CreateHandle(NewObject("target").transform);
                HandleBase component = plane ? (HandleBase)handle.GetComponentInChildren<PositionHandle>(true).XPlane :
                    handle.GetComponentInChildren<PositionAxis>(true);
                var renderer = component.GetComponentInChildren<MeshRenderer>(true);
                var original = renderer.sharedMaterial.color;
                var hover = typeof(TransformHandleManager).GetMethod("HandleOverEffect", flags);
                hover.Invoke(_manager, new object[] { component });
                Assert.AreEqual(preserve ? original : settings.HighlightColor, renderer.sharedMaterial.color);
                hover.Invoke(_manager, new object[] { null });
                Assert.AreEqual(original, renderer.sharedMaterial.color, "Hover exit restores exact RGBA.");
                hover.Invoke(_manager, new object[] { component });
                Assert.IsTrue(_manager.BeginInteraction(component, handle.transform.position));
                _manager.enabled = false;
                Assert.AreEqual(original, renderer.sharedMaterial.color, "Cancellation restores exact RGBA.");
            }
            finally
            {
                _manager.Settings = null;
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void HoverWithoutSettingsRetainsLegacyHighlight()
        {
            var handle = _manager.CreateHandle(NewObject("target").transform);
            var axis = handle.GetComponentInChildren<PositionAxis>(true);
            var renderer = axis.GetComponentInChildren<MeshRenderer>(true);
            var original = renderer.sharedMaterial.color;
            var hover = typeof(TransformHandleManager).GetMethod("HandleOverEffect", BindingFlags.Instance | BindingFlags.NonPublic);
            hover.Invoke(_manager, new object[] { axis });
            Assert.AreEqual(Color.white, renderer.sharedMaterial.color);
            hover.Invoke(_manager, new object[] { null });
            Assert.AreEqual(original, renderer.sharedMaterial.color);
        }

        [Test]
        public void CreateHandle_returns_handle_for_single_target()
        {
            var target = NewObject("target").transform;
            var handle = _manager.CreateHandle(target);

            Assert.IsNotNull(handle);
            Assert.IsNotNull(handle.Pivot, "handle.Pivot is the manipulation pivot (ghost), not the user object");
            Assert.AreNotSame(target, handle.Pivot);
        }

        [Test]
        public void Targets_contains_the_manipulated_object_and_Pivot_is_separate()
        {
            var target = NewObject("target").transform;
            var handle = _manager.CreateHandle(target);

            CollectionAssert.Contains(handle.Targets, target, "Targets must expose the selected object");
            Assert.AreEqual(1, handle.Targets.Count);
            // Pivot is the ghost; the manipulated object is not. (Reference identity, not NUnit
            // collection equality, which is unreliable on UnityEngine.Object's fake-null operator.)
            Assert.AreNotSame(target, handle.Pivot, "the pivot is not the manipulated object");
            Assert.IsNotNull(handle.Pivot.GetComponent<Ghost>(), "Pivot is the ghost pivot");
            Assert.IsNull(target.GetComponent<Ghost>(), "the manipulated object is not a ghost");
        }

        [Test]
        public void Targets_reflects_multi_select_membership()
        {
            var a = NewObject("a").transform;
            var b = NewObject("b", new Vector3(2f, 0f, 0f)).transform;
            var handle = _manager.CreateHandleFromList(new List<Transform> { a, b });

            CollectionAssert.AreEquivalent(new[] { a, b }, handle.Targets);
        }

        [Test]
        public void CreateHandle_throws_on_null_target()
        {
            Assert.Throws<ArgumentNullException>(() => _manager.CreateHandle(null));
        }

        [Test]
        public void CreateHandle_returns_null_when_target_already_managed()
        {
            var target = NewObject("target").transform;
            Assert.IsNotNull(_manager.CreateHandle(target));
            Assert.IsNull(_manager.CreateHandle(target));
        }

        [Test]
        public void CreateHandleFromList_creates_one_handle_for_multiple_targets()
        {
            var a = NewObject("a").transform;
            var b = NewObject("b", new Vector3(2f, 0f, 0f)).transform;
            var handle = _manager.CreateHandleFromList(new List<Transform> { a, b });

            Assert.IsNotNull(handle);
            Assert.IsNull(_manager.CreateHandle(a), "members of a multi-select handle are already managed");
            Assert.IsNull(_manager.CreateHandle(b));
        }

        [Test]
        public void CreateHandleFromList_throws_on_null_or_empty_list()
        {
            Assert.Throws<ArgumentNullException>(() => _manager.CreateHandleFromList(null));
            Assert.Throws<ArgumentException>(() => _manager.CreateHandleFromList(new List<Transform>()));
        }

        [Test]
        public void CreateHandleFromList_rolls_back_when_any_target_is_already_managed()
        {
            var existing = NewObject("existing").transform;
            Assert.IsNotNull(_manager.CreateHandle(existing));

            var other = NewObject("other").transform;
            Assert.IsNull(_manager.CreateHandleFromList(new List<Transform> { existing, other }));

            // Existing handle must remain; `other` must still be free to attach.
            Assert.IsNotNull(_manager.CreateHandle(other));
        }

        [Test]
        public void AddTarget_adds_second_object_to_existing_handle()
        {
            var a = NewObject("a").transform;
            var b = NewObject("b", new Vector3(3f, 0f, 0f)).transform;
            var handle = _manager.CreateHandle(a);

            Assert.IsTrue(_manager.AddTarget(b, handle));
        }

        [Test]
        public void AddTarget_returns_false_for_duplicate_target()
        {
            var target = NewObject("target").transform;
            var handle = _manager.CreateHandle(target);
            var other = NewObject("other").transform;
            _manager.CreateHandle(other);

            Assert.IsFalse(_manager.AddTarget(target, handle));
        }

        [Test]
        public void AddTarget_throws_when_handle_is_not_managed()
        {
            var orphanGo = NewObject("orphan");
            orphanGo.SetActive(false);
            var orphan = orphanGo.AddComponent<Handle>();

            Assert.Throws<InvalidOperationException>(() =>
                _manager.AddTarget(NewObject("t").transform, orphan));
        }

        [Test]
        public void RemoveTarget_keeps_handle_when_other_targets_remain()
        {
            var a = NewObject("a").transform;
            var b = NewObject("b").transform;
            var handle = _manager.CreateHandleFromList(new List<Transform> { a, b });

            _manager.RemoveTarget(a, handle);

            Assert.IsNotNull(handle);
            Assert.IsTrue(handle);
        }

        [Test]
        public void RemoveTarget_destroys_handle_when_last_target_is_removed()
        {
            var target = NewObject("target").transform;
            var handle = _manager.CreateHandle(target);

            _manager.RemoveTarget(target, handle);

            Assert.IsFalse(handle);
        }

        [Test]
        public void DestroyAllHandles_allows_targets_to_be_re_registered()
        {
            var a = NewObject("a").transform;
            var b = NewObject("b").transform;
            _manager.CreateHandle(a);
            _manager.CreateHandle(b);

            _manager.DestroyAllHandles();

            Assert.IsNotNull(_manager.CreateHandle(a));
            Assert.IsNotNull(_manager.CreateHandle(b));
        }

        [Test]
        public void ChangeHandleType_and_space_update_handle_state()
        {
            var handle = _manager.CreateHandle(NewObject("target").transform);

            TransformHandleManager.ChangeHandleType(handle, HandleType.Rotation);
            _manager.ChangeHandleSpace(handle, Space.World);

            Assert.AreEqual(HandleType.Rotation, handle.Type);
            Assert.AreEqual(Space.World, handle.Space);
        }
    }
}
