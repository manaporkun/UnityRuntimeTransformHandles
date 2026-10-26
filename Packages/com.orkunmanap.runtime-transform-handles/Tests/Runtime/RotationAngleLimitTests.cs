using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using TransformHandles.Utils;
using Object = UnityEngine.Object;

namespace TransformHandles.Tests
{
    public class RotationAngleLimitTests
    {
        private TransformHandleManager manager;
        private Handle handle;
        private RotationAxis axis;
        private Transform root, parent;
        private Camera camera;
        private TransformHandleSettings settings;
        private Vector3 up, forward, axisWorld, pivot;
        private Quaternion previousRaw;
        private float accepted;
        private Vector3 localAxis = Vector3.right;
        private Space space = Space.Self;
        private bool filter;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            accepted = 0;
            localAxis = Vector3.right;
            space = Space.Self;
            parent = new GameObject("Rotated socket").transform;
            root = new GameObject("Hinge at socket").transform;
            root.SetParent(parent, false);
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.transform.SetParent(root, false); body.transform.localPosition = Vector3.up;
            body.transform.localScale = new Vector3(.1f, 2, .1f);
            camera = new GameObject("Rotation test camera").AddComponent<Camera>();
            camera.pixelRect = new Rect(0, 0, 1000, 1000);
            settings = TransformHandleSettings.CreateDefault();
            typeof(TransformHandleSettings).GetField("enableShortcuts", Flags).SetValue(settings, false);
            manager = TransformHandleManager.Instance;
            manager.MainCamera = camera; manager.Settings = settings; manager.BlockWhenPointerOverUI = false;
        }

        [TearDown]
        public void TearDown()
        {
            if (manager != null) { manager.DestroyAllHandles(); Object.DestroyImmediate(manager.gameObject); }
            if (parent != null) Object.DestroyImmediate(parent.gameObject);
            if (camera != null) Object.DestroyImmediate(camera.gameObject);
            Object.DestroyImmediate(settings);
        }

        private void Create(bool useFilter, bool rotated = false)
        {
            filter = useFilter;
            parent.rotation = rotated ? Quaternion.Euler(10, 35, 20) : Quaternion.identity;
            handle = manager.CreateHandle(root);
            TransformHandleManager.ChangeHandleType(handle, HandleType.Rotation);
            handle.Axes = localAxis == Vector3.right ? HandleAxes.X : localAxis == Vector3.up ? HandleAxes.Y : HandleAxes.Z; manager.ChangeHandleSpace(handle, space);
            axis = handle.GetComponentInChildren<RotationAxis>();
            if (useFilter) axis.DragAngleLimits = new Vector2(-30, 60);
            else handle.OnInteractionEvent += ConstrainAfterTransform;
            camera.transform.SetPositionAndRotation(root.position - AxisWorld() * 5,
                Quaternion.LookRotation(AxisWorld(), Mathf.Abs(Vector3.Dot(AxisWorld(), Vector3.up)) > .9f ? Vector3.forward : Vector3.up));
        }

        private void StartGrab()
        {
            pivot = root.position; axisWorld = AxisWorld();
            up = Vector3.Cross(axisWorld, Mathf.Abs(Vector3.Dot(axisWorld, Vector3.up)) > .9f ? Vector3.forward : Vector3.up).normalized;
            forward = Vector3.Cross(axisWorld, up);
            if (filter && axis.DragAngleLimits.HasValue)
            {
                accepted = Mathf.DeltaAngle(0, 2 * Mathf.Atan2(root.localRotation.x, root.localRotation.w) * Mathf.Rad2Deg);
                axis.DragAngleLimits = new Vector2(-30 - accepted, 60 - accepted);
            }
            Point(0, true);
            Assert.IsTrue((bool)typeof(TransformHandleManager).GetMethod("BeginInteraction", Flags)
                .Invoke(manager, new object[] { axis, pivot + up }));
            previousRaw = handle.Pivot.rotation;
        }

        private void Point(float degrees, bool held)
        {
            float radians = degrees * Mathf.Deg2Rad;
            var world = pivot + up * Mathf.Cos(radians) + forward * Mathf.Sin(radians);
            // Move the camera so the backend's current pointer ray meets the desired point.
            // This exercises the production ray/plane path on both Legacy and Input System.
            var ray = camera.ScreenPointToRay(InputWrapper.MousePosition);
            var plane = new Plane(axisWorld, pivot);
            Assert.IsTrue(plane.Raycast(ray, out var distance));
            camera.transform.position += world - ray.GetPoint(distance);
        }

        private Vector3 AxisWorld() => space == Space.Self ? root.rotation * localAxis : localAxis;

        private void Step(float raw)
        {
            Point(raw, true);
            axis.Interact(Vector3.zero);
            typeof(TransformHandleManager).GetMethod("OnInteraction", Flags).Invoke(manager, null);
        }

        private void Release(float raw)
        {
            manager.CancelInteraction(handle);
            manager.ChangeHandleSpace(handle, space);
        }

        private void ConstrainAfterTransform(Handle owner)
        {
            var raw = owner.Pivot.rotation;
            var delta = Quaternion.Inverse(previousRaw) * raw;
            float signed = Mathf.DeltaAngle(0, 2 * Mathf.Atan2(delta.x, delta.w) * Mathf.Rad2Deg);
            previousRaw = raw;
            accepted = Mathf.Clamp(accepted + signed, -30, 60);
            root.localRotation = Quaternion.AngleAxis(accepted, Vector3.right);
        }

        private float ArcAngle()
        {
            // Read-only test observation: production code never accesses or patches private mesh state.
            var mesh = (Mesh)typeof(RotationAxis).GetField("_arcMesh", Flags).GetValue(axis);
            Assert.NotNull(mesh);
            var vertices = mesh.vertices;
            return Vector3.SignedAngle(vertices[0] - vertices[vertices.Length - 1],
                vertices[vertices.Length - 2] - vertices[vertices.Length - 1], axisWorld);
        }

        private void AssertAngle(float expected)
        {
            Assert.That(Quaternion.Angle(space == Space.Self ? root.localRotation : root.rotation, Quaternion.AngleAxis(expected, localAxis)), Is.LessThan(.03f));
            Assert.That(Vector3.Distance(pivot, root.position), Is.LessThan(.001f));
        }

        [Test]
        public void ExistingCallbackConstrainsPartButLeavesArcAtRawPointerAngle()
        {
            Create(false); StartGrab(); Step(100);
            AssertAngle(60);
            Assert.That(ArcAngle(), Is.EqualTo(100).Within(.03f));
            Debug.Log("OBSERVED: existing post-transform callback holds part at 60 degrees, but draws 100-degree arc.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LimitedRotationKeepsPartAndArcTogetherAtBothLimitsAndOnReverse(bool rotated)
        {
            Create(true, rotated); StartGrab();
            var raw = new[] { 45f, 100f, 110f, 105f, 20f, -20f, -10f };
            var expected = new[] { 45f, 60f, 60f, 55f, -30f, -30f, -20f };
            for (int i = 0; i < raw.Length; i++)
            {
                Step(raw[i]); AssertAngle(expected[i]);
                Assert.That(ArcAngle(), Is.EqualTo(expected[i]).Within(.03f));
            }
        }

        [Test]
        public void LimitedRotationUnwrapsPointerAcross180WithoutFlippingTheJoint()
        {
            Create(true, true); StartGrab();
            var raw = new[] { 170f, 179f, -179f, -160f, -170f };
            var expected = new[] { 60f, 60f, 60f, 60f, 50f };
            for (int i = 0; i < raw.Length; i++) { Step(raw[i]); AssertAngle(expected[i]); Assert.That(ArcAngle(), Is.EqualTo(expected[i]).Within(.03f)); }
        }

        [Test]
        public void LimitedRotationRepeatedGrabsStartAtAcceptedPoseAndArcMeasuresThisGrab()
        {
            Create(true, true); StartGrab(); Step(100); Release(100);
            AssertAngle(60); StartGrab(); Step(-20); AssertAngle(40);
            Assert.That(ArcAngle(), Is.EqualTo(-20).Within(.03f));
            Release(-20); StartGrab(); Step(100); AssertAngle(60);
            Assert.That(ArcAngle(), Is.EqualTo(20).Within(.03f));
        }

        [Test]
        public void NullConstraintRetainsUnrestrictedCoreRotation()
        {
            Create(true); axis.DragAngleLimits = null; StartGrab(); Step(100); AssertAngle(100);
            Assert.That(ArcAngle(), Is.EqualTo(100).Within(.03f));
        }

        [TestCase(float.NaN, 60)]
        [TestCase(-30, float.PositiveInfinity)]
        [TestCase(10, 60)]
        [TestCase(-30, -10)]
        [TestCase(60, -30)]
        [TestCase(-181, 0)]
        [TestCase(0, 181)]
        [TestCase(-float.MaxValue, float.MaxValue)]
        public void InvalidLimitsAreRejected(float minimum, float maximum)
        {
            Create(true);
            Assert.Throws<ArgumentOutOfRangeException>(() => axis.DragAngleLimits = new Vector2(minimum, maximum));
            Assert.That(axis.DragAngleLimits.Value, Is.EqualTo(new Vector2(-30, 60)));
        }
        [TestCase(0, Space.World)]
        [TestCase(1, Space.World)]
        [TestCase(2, Space.World)]
        [TestCase(1, Space.Self)]
        [TestCase(2, Space.Self)]
        public void LimitsWorkAcrossAxesAndSpaces(int index, Space mode)
        {
            localAxis = index == 0 ? Vector3.right : index == 1 ? Vector3.up : Vector3.forward;
            space = mode;
            Create(true); StartGrab(); Step(100); AssertAngle(60);
            Assert.That(ArcAngle(), Is.EqualTo(60).Within(.03f));
            Step(90); AssertAngle(50);
        }

        [Test]
        public void StartCallbackConfiguresLimitsAndMidDragChangesAreRejected()
        {
            Create(true);
            handle.OnInteractionStartEvent += _ => axis.DragAngleLimits = new Vector2(-10, 25);
            StartGrab(); Step(40); AssertAngle(25);
            Assert.Throws<InvalidOperationException>(() => axis.DragAngleLimits = null);
            Release(40);
            Assert.DoesNotThrow(() => axis.DragAngleLimits = null);
        }

        [Test]
        public void SnappingCanStopAtOffGridBoundaryAndReverseWithoutOvershootDeadZone()
        {
            Create(true); handle.RotationSnap = 15;
            handle.OnInteractionStartEvent += _ => axis.DragAngleLimits = new Vector2(-22, 37);
            StartGrab(); Step(45); AssertAngle(37); Step(75); AssertAngle(37);
            Step(60); AssertAngle(22); Step(0); AssertAngle(-22);
            Assert.That(ArcAngle(), Is.EqualTo(-22).Within(.03f));
        }

        [Test]
        public void ZeroWidthLimitHoldsPoseAndDisableReenableAllowsNextGrab()
        {
            Create(true);
            handle.OnInteractionStartEvent += _ => axis.DragAngleLimits = Vector2.zero;
            StartGrab(); Step(80); AssertAngle(0);
            manager.enabled = false;
            Assert.DoesNotThrow(() => axis.DragAngleLimits = null);
            manager.enabled = true; StartGrab(); Step(-60); AssertAngle(0);
            var mesh = (Mesh)typeof(RotationAxis).GetField("_arcMesh", Flags).GetValue(axis);
            Assert.LessOrEqual(mesh.vertexCount, 183);
        }
    }
}
