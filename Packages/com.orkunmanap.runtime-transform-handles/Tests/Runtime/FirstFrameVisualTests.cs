using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TransformHandles.Tests
{
    public class FirstFrameVisualTests
    {
        private TransformHandleManager _manager;
        private Camera _camera;
        private readonly List<GameObject> _objects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _camera = NewObject("Visual camera", new Vector3(0, 3, -20)).AddComponent<Camera>();
            _camera.pixelRect = new Rect(0, 0, 640, 480);
            _manager = TransformHandleManager.Instance;
            _manager.MainCamera = _camera;
            _manager.DestroyAllHandles();
        }

        [TearDown]
        public void TearDown()
        {
            if (_manager != null) { _manager.DestroyAllHandles(); Object.DestroyImmediate(_manager.gameObject); }
            foreach (var go in _objects) if (go != null) Object.DestroyImmediate(go);
            _objects.Clear();
        }

        private GameObject NewObject(string name, Vector3 position)
        {
            var go = new GameObject(name); go.transform.position = position; _objects.Add(go); return go;
        }

        private void AssertVisual(Handle handle, Vector3 position, Quaternion rotation)
        {
            Assert.That(Vector3.Distance(handle.transform.position, position), Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(handle.transform.rotation, rotation), Is.LessThan(.02f));
            if (handle.AutoScale && handle.HandleCamera != null)
            {
                // Independent expected value of the existing sizing contract, including orthographic cameras.
                var planes = GeometryUtility.CalculateFrustumPlanes(_camera);
                var distance = Mathf.Abs(planes[4].GetDistanceToPoint(position));
                var size = 2 * Mathf.Tan(_camera.fieldOfView * .5f * Mathf.Deg2Rad) * handle.AutoScaleSizeInPixels / Screen.height * distance;
                Assert.That(Vector3.Distance(handle.transform.localScale, Vector3.one * size), Is.LessThan(.0001f));
            }
        }

        [Test]
        public void SingleFactoryReturnsFinalPositionRotationAndSize()
        {
            var target = NewObject("Off origin", new Vector3(6, 2, 8)).transform;
            target.rotation = Quaternion.Euler(15, 35, 10);
            var handle = _manager.CreateHandle(target);
            AssertVisual(handle, target.position, handle.Space == Space.Self ? target.rotation : Quaternion.identity);
        }

        [Test]
        public void GroupFactoryReturnsFinalAverageBeforeAnyFrameUpdate()
        {
            var a = NewObject("A", new Vector3(3, 2, 7)).transform;
            var b = NewObject("B", new Vector3(9, 4, 11)).transform;
            var handle = _manager.CreateHandleFromList(new List<Transform> { a, b });
            AssertVisual(handle, (a.position + b.position) * .5f, Quaternion.identity);
        }

        [Test]
        public void SpaceAndModeChangesSynchronizeImmediately()
        {
            var target = NewObject("Rotated", new Vector3(4, 2, 8)).transform;
            target.rotation = Quaternion.Euler(20, 45, 5);
            var handle = _manager.CreateHandle(target);
            _manager.ChangeHandleSpace(handle, Space.World);
            AssertVisual(handle, target.position, Quaternion.identity);
            _manager.ChangeHandleSpace(handle, Space.Self);
            AssertVisual(handle, target.position, target.rotation);
            TransformHandleManager.ChangeHandleType(handle, HandleType.Scale);
            AssertVisual(handle, target.position, target.rotation);
        }

        [Test]
        public void MembershipRecenterIsVisibleImmediately()
        {
            var a = NewObject("A", new Vector3(2, 2, 8)).transform;
            var b = NewObject("B", new Vector3(8, 4, 10)).transform;
            var handle = _manager.CreateHandle(a);
            _manager.AddTarget(b, handle);
            AssertVisual(handle, (a.position + b.position) * .5f, Quaternion.identity);
            _manager.RemoveTarget(b, handle);
            AssertVisual(handle, a.position, Quaternion.identity);
        }

        [Test]
        public void DirectEnableAndAxesRebuildHaveNoStaleRoot()
        {
            var target = NewObject("A", new Vector3(5, 1, 8)).transform;
            var handle = _manager.CreateHandle(target);
            handle.transform.position = Vector3.zero;
            handle.Enable(handle.Pivot);
            AssertVisual(handle, target.position, Quaternion.identity);
            handle.transform.position = Vector3.zero;
            handle.Axes = HandleAxes.X;
            AssertVisual(handle, target.position, Quaternion.identity);
        }

        [Test]
        public void OrthographicFirstFrameUsesExistingSizingContract()
        {
            _camera.orthographic = true; _camera.orthographicSize = 8;
            var target = NewObject("A", new Vector3(4, 2, 10)).transform;
            AssertVisual(_manager.CreateHandle(target), target.position, Quaternion.identity);
        }

        [Test]
        public void SynchronizationWithoutCameraDoesNotCreateOne()
        {
            var target = NewObject("A", new Vector3(4, 2, 8)).transform;
            var handle = _manager.CreateHandle(target);
            _manager.MainCamera = null; Object.DestroyImmediate(_camera.gameObject);
            handle.RefreshCamera(null);
            handle.transform.position = Vector3.zero;
            handle.SynchronizeVisualTransform();
            Assert.AreEqual(target.position, handle.transform.position);
            Assert.IsNull(handle.HandleCamera);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CustomFinalSynchronizationCannotReturnDetachedHandle(bool group)
        {
            var prefab = NewObject("Custom handle prefab", Vector3.zero);
            var custom = prefab.AddComponent<DetachingVisualHandle>();
            custom.DetachOnSync = group ? 4 : 3;
            typeof(TransformHandleManager).GetField("transformHandlePrefab", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_manager, prefab);
            var a = NewObject("A", new Vector3(3, 1, 8)).transform;
            var b = NewObject("B", new Vector3(5, 1, 8)).transform;
            var result = group ? _manager.CreateHandleFromList(new List<Transform> {a, b}) : _manager.CreateHandle(a);
            Assert.IsNull(result);
            Assert.IsNotNull(DetachingVisualHandle.Detached);
            var detached = DetachingVisualHandle.Detached;
            _objects.Add(detached.gameObject);
            Assert.IsNull(detached.Pivot);
            DetachingVisualHandle.Detached = null;
            Assert.IsFalse((bool)typeof(TransformHandleManager).GetField("_handleActive", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_manager));
        }

        [Test]
        public void PlanePadsStartCameraFacingAndFollowCameraFlipsAndRebuilds()
        {
            var target = NewObject("Rotated pad target", new Vector3(4, 2, 8)).transform;
            target.rotation = Quaternion.Euler(15, 125, 20);
            _camera.transform.rotation = Quaternion.Euler(30, 40, 0);
            // Keep prefab children uninitialized until the factory instantiates its active copy.
            var staging = NewObject("Inactive prefab staging", Vector3.zero);
            staging.SetActive(false);
            var prefabField = typeof(TransformHandleManager).GetField("transformHandlePrefab", BindingFlags.Instance | BindingFlags.NonPublic);
            var localPrefab = Object.Instantiate((GameObject)prefabField.GetValue(_manager), staging.transform);
            localPrefab.GetComponent<Handle>().Space = Space.Self;
            prefabField.SetValue(_manager, localPrefab);
            var handle = _manager.CreateHandle(target);
            Assert.AreEqual(Space.Self, handle.Space);
            // Observe the actual factory result before any rebuilding API can repair it.
            AssertPlaneFacing(handle, handle.GetComponentInChildren<PositionHandle>(true).XPlane, 0, 2);
            AssertPlaneFacing(handle, handle.GetComponentInChildren<PositionHandle>(true).YPlane, 0, 1);
            AssertPlaneFacing(handle, handle.GetComponentInChildren<PositionHandle>(true).ZPlane, 2, 1);
            foreach (var space in new[] {Space.Self, Space.World})
            {
                _manager.ChangeHandleSpace(handle, space);
                for (var flip = 0; flip < 2; flip++)
                {
                    AssertPlaneFacing(handle, handle.GetComponentInChildren<PositionHandle>(true).XPlane, 0, 2);
                    AssertPlaneFacing(handle, handle.GetComponentInChildren<PositionHandle>(true).YPlane, 0, 1);
                    AssertPlaneFacing(handle, handle.GetComponentInChildren<PositionHandle>(true).ZPlane, 2, 1);
                    _camera.transform.rotation = Quaternion.LookRotation(-_camera.transform.forward);
                    foreach (var plane in handle.GetComponentsInChildren<PositionPlane>(true))
                        plane.SendMessage("Update");
                }
                handle.Axes = HandleAxes.X;
                handle.Axes = HandleAxes.XYZ;
                AssertPlaneFacing(handle, handle.GetComponentInChildren<PositionHandle>(true).YPlane, 0, 1);
            }
        }

        private void AssertPlaneFacing(Handle handle, PositionPlane plane, int first, int second)
        {
            var quad = plane.GetComponentInChildren<MeshRenderer>(true).transform;
            var position = quad.localPosition;
            var configuredOffset = (float)typeof(PositionPlane).GetField("planeVisualOffset", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(plane);
            foreach (var axisIndex in new[] {first, second})
            {
                Assert.That(Mathf.Abs(position[axisIndex]), Is.EqualTo(configuredOffset).Within(.00001f));
                var signedAxis = Vector3.zero; signedAxis[axisIndex] = Mathf.Sign(position[axisIndex]);
                var direction = handle.Space == Space.Self ? handle.Pivot.rotation * signedAxis : signedAxis;
                Assert.LessOrEqual(Vector3.Dot(_camera.transform.forward, direction), .00001f,
                    "Each pad must start in the camera-facing quadrant without waiting for Update.");
            }
            var box = quad.GetComponent<BoxCollider>();
            var halfPickWidth = Mathf.Max(box.size.x * quad.localScale.x, box.size.z * quad.localScale.z) * .5f;
            Assert.Greater(configuredOffset - halfPickWidth, .02f, "Plane picking must clear the axis shaft.");
        }

        [UnityTest]
        public IEnumerator LateUpdateCreationIsCorrectAtItsFirstRender()
        {
            var target = NewObject("Late target", new Vector3(5, 2, 8)).transform;
            var creator = NewObject("Late creator", Vector3.zero).AddComponent<LateVisualCreator>();
            creator.Create = () => _manager.CreateHandle(target);
            var observed = false;
            Camera.CameraCallback callback = camera =>
            {
                if (camera != _camera || creator.Created == null) return;
                AssertVisual(creator.Created, target.position, Quaternion.identity);
                observed = true;
            };
            Camera.onPreRender += callback;
            try
            {
                yield return null;
                Assert.IsNotNull(creator.Created);
                // Explicit rendering works in a graphics-enabled batch player without WaitForEndOfFrame.
                if (!observed) _camera.Render();
                Assert.IsTrue(observed, "A real Built-in camera render must observe the first prepared handle.");
                var first = creator.Created.transform.position;
                yield return null;
                Assert.AreEqual(first, creator.Created.transform.position);
            }
            finally { Camera.onPreRender -= callback; }
        }
    }

    public sealed class DetachingVisualHandle : Handle
    {
        public int DetachOnSync;
        public static Handle Detached;
        private int _syncCount;
        protected override void CreateHandles() { SynchronizeVisualTransform(); }
        protected override void UpdateHandleTransformation()
        {
            base.UpdateHandleTransformation();
            if (++_syncCount != DetachOnSync) return;
            Detached = this;
            TransformHandleManager.ExistingInstance.RemoveHandle(this);
        }
    }

    public sealed class LateVisualCreator : MonoBehaviour
    {
        public Func<Handle> Create;
        public Handle Created;
        private void LateUpdate() { if (Created == null && Create != null) Created = Create(); }
    }
}
