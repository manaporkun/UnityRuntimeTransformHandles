using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TransformHandles.Samples.ProductConfigurator.Tests
{
    public class ProductConfiguratorDemoTests
    {
        private GameObject _root;
        private ProductConfiguratorDemo _demo;
        private Transform[] _targets;
        private Transform _base;
        private Transform[] _sockets;
        private Material _preview;
        private UnityEngine.SceneManagement.Scene _loadedScene;
        private readonly List<Handle> _extraHandles = new List<Handle>();
        private Camera _camera;
        private TransformHandleSettings _settings;
        private TransformHandleManager _manager;
        private Camera _originalCamera;
        private TransformHandleSettings _originalSettings;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.That(LayerMask.NameToLayer("TransformHandle"), Is.GreaterThanOrEqualTo(0), "Run Tools > Transform Handles > Setup Layer before running this sample suite.");
            _originalCamera = new GameObject("Existing camera").AddComponent<Camera>();
            _manager = TransformHandleManager.Instance;
            _manager.MainCamera = _originalCamera;
            _originalSettings = _manager.Settings;
            _manager.BlockWhenPointerOverUI = false;
            _root = new GameObject("Test controller"); _root.SetActive(false);
            _demo = _root.AddComponent<ProductConfiguratorDemo>();
            _camera = new GameObject("Sample camera").AddComponent<Camera>();
            _targets = new Transform[2];
            for (var i = 0; i < 2; i++)
            {
                _targets[i] = new GameObject("Product " + i).transform;
                _targets[i].position = new Vector3(i * 3 - 1.5f, 0, 0);
                var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                mesh.SetParent(_targets[i], false);
                mesh.localPosition = new Vector3(0, .3f, 0);
                mesh.localScale = new Vector3(1.2f, .6f, .8f);
            }
            _base = new GameObject("Fixed base").transform;
            _sockets = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                _sockets[i] = new GameObject("Socket " + i).transform;
                _sockets[i].SetParent(i == 0 ? _base : _targets[0], false);
                _sockets[i].localPosition = Vector3.up * (i == 0 ? .5f : 1.6f);
            }
            _preview = new Material(Shader.Find("TransformHandles/Samples/AssemblyPreview"));
            Set("previewMaterial", _preview);
            Set("socketMarkerMesh", _targets[0].GetComponentInChildren<MeshFilter>().sharedMesh);
#if UNITY_EDITOR
            var settingsPaths = UnityEditor.AssetDatabase.FindAssets("ProductConfiguratorSettings t:TransformHandleSettings", new[] {"Assets"});
            Assert.AreEqual(1, settingsPaths.Length, "Import this sample exactly once before testing.");
            _settings = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<TransformHandleSettings>(UnityEditor.AssetDatabase.GUIDToAssetPath(settingsPaths[0])));
#else
            _settings = ScriptableObject.CreateInstance<TransformHandleSettings>();
            typeof(TransformHandleSettings).GetField("enableShortcuts", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_settings, false);
#endif
#if UNITY_EDITOR
            var layouts = UnityEditor.AssetDatabase.FindAssets("ProductConfigurator t:VisualTreeAsset", new[] {"Assets"});
            var panels = UnityEditor.AssetDatabase.FindAssets("Product Panel t:PanelSettings", new[] {"Assets"});
            Assert.AreEqual(1, layouts.Length); Assert.AreEqual(1, panels.Length);
            Set("uiLayout", UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UnityEditor.AssetDatabase.GUIDToAssetPath(layouts[0])));
            Set("uiPanelSettings", UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(UnityEditor.AssetDatabase.GUIDToAssetPath(panels[0])));
#else
            Assert.Fail("This sample suite loads its imported UI assets through the Editor AssetDatabase.");
#endif
            Set("sceneCamera", _camera);
            var partType = typeof(ProductConfiguratorDemo).GetNestedType("AssemblyPart", BindingFlags.NonPublic);
            var parts = System.Array.CreateInstance(partType, _targets.Length);
            for (var i = 0; i < _targets.Length; i++)
            {
                var part = System.Activator.CreateInstance(partType);
                partType.GetField("root").SetValue(part, _targets[i]);
                partType.GetField("displayName").SetValue(part, "Product " + i);
                partType.GetField("socket").SetValue(part, _sockets[i]);
                partType.GetField("parentRoot").SetValue(part, i == 0 ? _base : _targets[0]);
                parts.SetValue(part, i);
            }
            Set("parts", parts);
            Set("interactionSettings", _settings);
            _root.SetActive(true);
            yield return null;
            yield return null;
            Assert.IsTrue(_demo.enabled);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_loadedScene.IsValid() && _loadedScene.isLoaded)
                yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(_loadedScene);
            foreach (var handle in _extraHandles)
            {
                if (handle == null) continue;
                if (_manager != null) _manager.RemoveHandle(handle);
                if (handle != null) Object.Destroy(handle.gameObject);
            }
            _extraHandles.Clear();
            if (_root != null) Object.Destroy(_root);
            yield return null;
            foreach (var t in _targets ?? new Transform[0]) if (t != null) Object.Destroy(t.gameObject);
            if (_manager != null) Object.Destroy(_manager.gameObject);
            if (_camera != null) Object.Destroy(_camera.gameObject);
            if (_originalCamera != null) Object.Destroy(_originalCamera.gameObject);
            Object.Destroy(_settings); Object.Destroy(_preview);
            if (_base != null) Object.Destroy(_base.gameObject);
            yield return null;
        }

        private void Set(string name, object value) => typeof(ProductConfiguratorDemo).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_demo, value);
        private T Get<T>(string name) => (T)typeof(ProductConfiguratorDemo).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_demo);
        private object Call(string name, params object[] args) => typeof(ProductConfiguratorDemo).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_demo, args);
        private void Select() => _demo.SelectTarget(_targets[0]);

        private void Navigate(Vector3 position, int down = -1, int held = -1, bool ui = false, bool editing = false, float scroll = 0)
        {
            Call("ProcessNavigation", position, down == 0, down == 1, down == 2,
                held == 0, held == 1, held == 2, ui, editing, scroll, Time.frameCount);
        }

        private void Release()
        {
            Set("_releasePending", true);
            _demo.ActiveHandle.InteractionEnd();
            Set("_releasePending", false);
        }

        private void MoveToSocket(int index, bool complete = true)
        {
            _demo.SelectTarget(_targets[index]);
            _demo.ActiveHandle.InteractionStart();
            _targets[index].position = _sockets[index].position;
            _demo.ActiveHandle.InteractionStay();
            Release();
            if (complete) Call("AdvanceAttachment", 1f);
        }

        [Test]
        public void ChildSelectionIsSingleAndFreePartsOnlyMoveInXYZ()
        {
            _demo.SelectTarget(_targets[0].GetChild(0));
            _demo.SelectTarget(_targets[1].GetChild(0));
            Assert.AreEqual(1, _demo.SelectionCount);
            Assert.AreEqual(HandleAxes.XYZ, _demo.ActiveHandle.Axes);
            Assert.AreEqual(Space.World, _demo.ActiveHandle.Space);
            _demo.SetMode(HandleType.Rotation);
            Assert.AreEqual(HandleType.Position, _demo.ActiveHandle.Type);
            _demo.SelectTarget(_base);
            Assert.AreEqual(1, _demo.SelectionCount);
        }

        [Test]
        public void HeadCannotAttachBeforeArmButCanAfterArmIsMounted()
        {
            MoveToSocket(1);
            Assert.IsNull(_targets[1].parent);
            Assert.AreEqual(HandleType.Position, _demo.ActiveHandle.Type);
            MoveToSocket(0); MoveToSocket(1);
            Assert.AreSame(_base, _targets[0].parent);
            Assert.AreSame(_targets[0], _targets[1].parent);
            Assert.AreEqual(HandleType.Rotation, _demo.ActiveHandle.Type);
            Assert.AreEqual(HandleAxes.X, _demo.ActiveHandle.Axes);
            Assert.AreEqual(Space.Self, _demo.ActiveHandle.Space);
        }

        [Test]
        public void AttachmentRemovesHandleAndCommitsOnlyWhenTweenCompletes()
        {
            MoveToSocket(0, false);
            Assert.IsNull(_demo.ActiveHandle);
            Assert.IsNull(_targets[0].parent);
            _demo.SelectTarget(_targets[1]);
            Assert.IsNull(_demo.ActiveHandle);
            Call("AdvanceAttachment", .05f);
            Assert.IsNull(_targets[0].parent);
            Call("AdvanceAttachment", .2f);
            Assert.AreSame(_base, _targets[0].parent);
            Assert.That(Vector3.Distance(_sockets[0].position, _targets[0].position), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(_sockets[0].rotation, _targets[0].rotation), Is.LessThan(.03f));
            Assert.NotNull(_demo.ActiveHandle);
        }

        [TestCase(.249f, true)]
        [TestCase(.251f, false)]
        public void SocketDistanceIsCheckedOnRelease(float distance, bool attached)
        {
            Select(); _demo.ActiveHandle.InteractionStart();
            _targets[0].position = _sockets[0].position + Vector3.right * distance;
            Release(); Call("AdvanceAttachment", 1f);
            Assert.AreEqual(attached, _targets[0].parent == _base);
        }

        [Test]
        public void DetachingArmKeepsWorldPoseAndAttachedHeadSubtree()
        {
            MoveToSocket(0); MoveToSocket(1);
            _targets[1].localRotation = Quaternion.Euler(25, 0, 0);
            var position = _targets[1].position; var rotation = _targets[1].rotation;
            _demo.SelectTarget(_targets[0]); _demo.DetachSelected();
            Assert.IsNull(_targets[0].parent);
            Assert.AreSame(_targets[0], _targets[1].parent);
            Assert.That(Vector3.Distance(position, _targets[1].position), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(rotation, _targets[1].rotation), Is.LessThan(.03f));
            _demo.SelectTarget(_targets[1]);
            Assert.AreEqual(HandleType.Rotation, _demo.ActiveHandle.Type);
            _demo.DetachSelected();
            Assert.IsNull(_targets[1].parent);
            MoveToSocket(1);
            Assert.IsNull(_targets[1].parent, "A new head attachment requires a mounted arm.");
        }

        [Test]
        public void MovingParentCarriesHeadOnceAndReattachmentPreservesHeadJointAngle()
        {
            MoveToSocket(0); MoveToSocket(1);
            _targets[1].localRotation = Quaternion.Euler(25,0,0);
            _demo.SelectTarget(_targets[0]); _demo.DetachSelected();
            var before = _targets[1].position;
            var h = _demo.ActiveHandle; h.InteractionStart();
            _manager.UpdateGroupPosition(h.Pivot.GetComponent<Ghost>(), Vector3.right);
            Assert.That(Vector3.Distance(before + Vector3.right, _targets[1].position), Is.LessThan(.001f));
            Release(); MoveToSocket(0);
            Assert.That(Quaternion.Angle(Quaternion.Euler(25,0,0), _targets[1].localRotation), Is.LessThan(.03f));
        }

        [TestCase("disable", false)]
        [TestCase("focus", false)]
        [TestCase("manager", false)]
        [TestCase("disable", true)]
        [TestCase("focus", true)]
        [TestCase("manager", true)]
        public void InterruptedDragOrAlignmentRestoresPreDragPose(string reason, bool aligning)
        {
            var initial = _targets[0].position;
            if (aligning) MoveToSocket(0, false);
            else { Select(); _demo.ActiveHandle.InteractionStart(); _targets[0].position += Vector3.forward; }
            if (reason == "disable") _demo.enabled = false;
            else if (reason == "focus") Call("OnApplicationFocus", false);
            else
            {
                _manager.enabled = false;
                if (aligning) Call("AdvanceAttachment", .1f);
                else _demo.ActiveHandle.InteractionEnd();
            }
            Assert.That(Vector3.Distance(initial, _targets[0].position), Is.LessThan(.001f));
            Assert.IsNull(_targets[0].parent);
            Assert.IsFalse(_demo.IsDragging);
        }

        [UnityTest]
        public IEnumerator DestroyedSocketDuringAlignmentCancelsSafely()
        {
            var initial = _targets[0].position;
            MoveToSocket(0, false);
            Object.Destroy(_sockets[0].gameObject);
            yield return null; yield return null;
            Assert.That(Vector3.Distance(initial, _targets[0].position), Is.LessThan(.001f));
            Assert.IsNull(_targets[0].parent);
        }

        [UnityTest]
        public IEnumerator DestroyedSocketWhileIdleReleasesPartWithoutChangingWorldPose()
        {
            MoveToSocket(0);
            var position = _targets[0].position;
            Object.Destroy(_sockets[0].gameObject);
            yield return null; yield return null;
            Assert.IsNull(_targets[0].parent);
            Assert.AreEqual(position, _targets[0].position);
            Assert.AreEqual(HandleType.Position, _demo.ActiveHandle.Type);
            _demo.ActiveHandle.InteractionStart();
            Assert.IsTrue(_demo.IsDragging);
        }

        [UnityTest]
        public IEnumerator SurvivingDetachedChildBecomesFreeWhenItsParentIsDestroyed()
        {
            MoveToSocket(0); MoveToSocket(1);
            _demo.ActiveHandle.InteractionStart();
            _targets[1].SetParent(null, true); // External owner preserves this child before destruction.
            Object.Destroy(_targets[0].gameObject);
            yield return null; yield return null;
            Assert.NotNull(_targets[1]); Assert.IsNull(_targets[1].parent);
            Assert.AreEqual(HandleType.Position, _demo.ActiveHandle.Type);
            Assert.IsFalse(_demo.IsDragging);
        }

        [UnityTest]
        public IEnumerator IdleDisableReenableKeepsAssemblyAndRebindsControls()
        {
            MoveToSocket(0); MoveToSocket(1);
            _demo.enabled = false; yield return null;
            Assert.AreSame(_originalCamera, _manager.MainCamera);
            _demo.enabled = true; yield return null;
            Assert.AreSame(_base, _targets[0].parent);
            Assert.AreSame(_targets[0], _targets[1].parent);
            _demo.SelectTarget(_targets[1]); Call("RefreshUi");
            Assert.IsTrue(Get<Button>("_detachButton").enabledSelf);
            Assert.IsFalse(Get<Button>("_moveButton").enabledSelf);
        }

        [Test]
        public void ResetRestoresExplodedLayoutAndKeepsSizeAndBorrowedHandles()
        {
            var borrowed = _manager.CreateHandle(_camera.transform); _extraHandles.Add(borrowed);
            Select(); Call("SetHandleSize", 256f);
            MoveToSocket(0); MoveToSocket(1); _demo.ResetLayout();
            Assert.AreEqual(256, Get<int>("_handleSizePixels")); Assert.IsTrue(borrowed != null);
            Assert.IsNull(_targets[0].parent); Assert.IsNull(_targets[1].parent);
            Assert.AreEqual(new Vector3(-1.5f,0,0), _targets[0].position);
            Assert.AreEqual(new Vector3(1.5f,0,0), _targets[1].position);
        }

        [Test]
        public void AttachedHeadUsesRealCoreRotationAndArcUnderRotatedArmAcrossGrabs()
        {
            MoveToSocket(0);
            _targets[0].localRotation = Quaternion.Euler(25,0,0);
            MoveToSocket(1);
            _targets[1].localRotation = Quaternion.Euler(-10,0,0);
            _demo.SelectTarget(_targets[1]);
            DriveHinge(100, 60, 70);
            DriveHinge(-20, 40, -20);
        }

        private void DriveHinge(float pointerAngle, float expectedJoint, float expectedArc)
        {
            var h = _demo.ActiveHandle;
            var axis = h.GetComponentInChildren<RotationAxis>();
            var root = _targets[1]; var pivot = root.position;
            var worldAxis = root.right; var up = root.up; var forward = root.forward;
            _camera.transform.SetPositionAndRotation(pivot - worldAxis * 5, Quaternion.LookRotation(worldAxis, up));
            PlaceRay(pivot + up, pivot, worldAxis);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Assert.IsTrue((bool)typeof(TransformHandleManager).GetMethod("BeginInteraction", flags).Invoke(_manager, new object[] { axis, pivot + up }));
            float radians = pointerAngle * Mathf.Deg2Rad;
            PlaceRay(pivot + up * Mathf.Cos(radians) + forward * Mathf.Sin(radians), pivot, worldAxis);
            axis.Interact(Vector3.zero);
            typeof(TransformHandleManager).GetMethod("OnInteraction", flags).Invoke(_manager, null);
            Assert.That(Quaternion.Angle(Quaternion.Euler(expectedJoint,0,0), root.localRotation), Is.LessThan(.04f));
            Assert.That(Vector3.Distance(_sockets[1].position, root.position), Is.LessThan(.001f));
            var mesh = (Mesh)typeof(RotationAxis).GetField("_arcMesh", flags).GetValue(axis);
            var v = mesh.vertices;
            Assert.That(Vector3.SignedAngle(v[0]-v[v.Length-1],v[v.Length-2]-v[v.Length-1],worldAxis), Is.EqualTo(expectedArc).Within(.04f));
            Set("_releasePending", true);
            typeof(TransformHandleManager).GetMethod("CancelInteraction", flags).Invoke(_manager, new object[] { h });
            Set("_releasePending", false);
        }

        private void PlaceRay(Vector3 destination, Vector3 pivot, Vector3 normal)
        {
            var ray = _camera.ScreenPointToRay(TransformHandles.Utils.InputWrapper.MousePosition);
            Assert.IsTrue(new Plane(normal, pivot).Raycast(ray, out var distance));
            _camera.transform.position += destination - ray.GetPoint(distance);
        }

        [Test]
        public void TextEntryEscapeCancelsAndReturnCommitsWithoutCameraMotion()
        {
            var field=Get<TextField>("_sizeInput");var focus=Get<Vector3>("_cameraFocus");
            field.Focus();field.value="256";
            using(var e=KeyDownEvent.GetPooled('\0',KeyCode.Escape,EventModifiers.None))field.SendEvent(e);
            Assert.AreEqual(192,Get<int>("_handleSizePixels"));
            field.Focus();field.value="256";
            using(var e=KeyDownEvent.GetPooled('\0',KeyCode.Return,EventModifiers.None))field.SendEvent(e);
            Assert.AreEqual(256,Get<int>("_handleSizePixels"));
            Navigate(Vector3.zero,1,1,editing:true);Navigate(new Vector3(100,100),held:1);
            Assert.AreEqual(focus,Get<Vector3>("_cameraFocus"));
        }

        [Test]
        public void UiBlocksOrbitPanAndZoomIncludingDragOutsidePanel()
        {
            var yaw=Get<float>("_yaw");var focus=Get<Vector3>("_cameraFocus");var distance=Get<float>("_distance");
            Navigate(Vector3.zero,0,0,ui:true);Navigate(new Vector3(100,100),held:0);
            Navigate(Vector3.zero,1,1,ui:true);Navigate(new Vector3(100,100),held:1);
            Navigate(Vector3.zero,ui:true,scroll:10);
            Assert.AreEqual(yaw,Get<float>("_yaw"));Assert.AreEqual(focus,Get<Vector3>("_cameraFocus"));Assert.AreEqual(distance,Get<float>("_distance"));
        }

        [Test]
        public void ActualManagerCancellationRestoresPreview()
        {
            Select(); var start = _targets[0].position; var h = _demo.ActiveHandle;
            var axis = h.GetComponentInChildren<PositionPlane>();
            Assert.IsTrue((bool)typeof(TransformHandleManager).GetMethod("BeginInteraction", BindingFlags.Instance|BindingFlags.NonPublic).Invoke(_manager,new object[]{axis,axis.transform.position}));
            _targets[0].position += Vector3.forward;
            _manager.enabled = false;
            Assert.AreEqual(start, _targets[0].position);
            Assert.IsFalse(_demo.IsDragging);
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator SuppliedSceneHasTwoSelectablePartsAndFixedBase()
        {
            _root.SetActive(false);
            var scenes=UnityEditor.AssetDatabase.FindAssets("ProductConfigurator t:Scene",new[]{"Assets"});
            Assert.AreEqual(1,scenes.Length);
            var path=UnityEditor.AssetDatabase.GUIDToAssetPath(scenes[0]);
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(path,
                new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Additive));
            _loadedScene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
            yield return null;
            var controller=Object.FindObjectOfType<ProductConfiguratorDemo>();Assert.NotNull(controller);Assert.IsTrue(controller.enabled);
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var parts=(System.Array)typeof(ProductConfiguratorDemo).GetField("parts",flags).GetValue(controller);
            Assert.AreEqual(2,parts.Length);
            foreach(var part in parts)
            {
                var root=(Transform)part.GetType().GetField("root").GetValue(part);
                Assert.GreaterOrEqual(root.GetComponentsInChildren<MeshFilter>().Length,3);
                controller.SelectTarget(root.GetComponentInChildren<Collider>().transform);
                Assert.AreEqual(1,controller.SelectionCount);
            }
        }
#endif
    }
}
