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
        private Material[] _originalMaterials;
        private UnityEngine.SceneManagement.Scene _loadedScene;
        private readonly List<Handle> _extraHandles = new List<Handle>();
        private Camera _camera;
        private TransformHandleSettings _settings;
        private Material _material;
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
            _originalMaterials = _targets.Select(t => t.GetComponentInChildren<Renderer>().sharedMaterial).ToArray();
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
            _material = new Material(Shader.Find("TransformHandles/Samples/ProductConfiguratorOpaque"));
            Set("sceneCamera", _camera);
            var partType = typeof(ProductConfiguratorDemo).GetNestedType("ProductPart", BindingFlags.NonPublic);
            var parts = System.Array.CreateInstance(partType, _targets.Length);
            for (var i = 0; i < _targets.Length; i++)
            {
                var part = System.Activator.CreateInstance(partType);
                partType.GetField("root").SetValue(part, _targets[i]);
                partType.GetField("displayName").SetValue(part, "Product " + i);
                partType.GetField("finishRenderers").SetValue(part, _targets[i].GetComponentsInChildren<Renderer>());
                parts.SetValue(part, i);
            }
            Set("products", parts);
            Set("targetMaterial", _material); Set("interactionSettings", _settings);
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
            Object.Destroy(_settings); Object.Destroy(_material);
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

        private void Group() { Select(); _demo.SelectTarget(_targets[1], true); }
        private void Release() { Set("_releasePending", true); _demo.ActiveHandle.InteractionEnd(); Set("_releasePending", false); }

        [Test]
        public void ChildSelectionAndShiftToggleResolveOnlyRegisteredProducts()
        {
            _demo.SelectTarget(_targets[0].GetChild(0));
            Assert.AreEqual(1, _demo.SelectionCount);
            _demo.SelectTarget(_targets[1].GetChild(0), true);
            Assert.AreEqual(2, _demo.SelectionCount);
            _demo.SelectTarget(_targets[0], true);
            Assert.AreEqual(1, _demo.SelectionCount);
            _demo.SelectTarget(_camera.transform);
            Assert.AreEqual(1, _demo.SelectionCount);
        }

        [Test]
        public void WorldLocalGroupToolsKeepSurfaceConstraintsAndRejectScale()
        {
            _targets[1].rotation = Quaternion.Euler(0, 35, 0);
            Group(); var h = _demo.ActiveHandle;
            Assert.AreEqual(HandleAxes.XZ, h.Axes);
            Assert.AreEqual(Space.World, h.Space);
            _demo.ToggleSpace();
            Assert.AreEqual(Space.Self, h.Space);
            Assert.AreEqual(_targets[1], h.LocalSpaceReference);
            Assert.That(Vector3.Distance((_targets[0].position + _targets[1].position) * .5f, h.Pivot.position), Is.LessThan(.001f));
            _demo.SetMode(HandleType.Rotation);
            Assert.AreEqual(HandleAxes.Y, h.Axes);
            _demo.SetMode(HandleType.Scale);
            Assert.AreEqual(HandleType.Rotation, h.Type);
            Assert.IsFalse(_settings.EnableShortcuts);
            Assert.AreEqual(Vector3.zero, h.PositionSnap);
            Assert.AreEqual(0, h.RotationSnap);
        }

        [TestCase(3.39f, 0f, true)]
        [TestCase(3.41f, 0f, false)]
        [TestCase(3.29f, 45f, true)]
        [TestCase(3.31f, 45f, false)]
        public void EntireRotatedMeshMustFitNotOnlyItsPivot(float x, float yaw, bool expected)
        {
            _targets[0].SetPositionAndRotation(new Vector3(x,0,0), Quaternion.Euler(0,yaw,0));
            Assert.AreEqual(expected, (bool)Call("FitsDesk", _targets[0]));
        }

        [Test]
        public void GroupMoveUsesSharedDeltaAndReleaseCommitsValidPlacement()
        {
            Group(); var h = _demo.ActiveHandle; var start = _targets.Select(t=>t.position).ToArray();
            h.InteractionStart();
            _manager.UpdateGroupPosition(h.Pivot.GetComponent<Ghost>(), new Vector3(.2f,0,.3f));
            h.InteractionStay(); Release();
            for(var i=0;i<2;i++) Assert.That(Vector3.Distance(start[i] + new Vector3(.2f,0,.3f), _targets[i].position), Is.LessThan(.001f));
            Assert.IsFalse(_demo.IsDragging);
        }

        [UnityTest]
        public IEnumerator OneInvalidMemberRestoresEveryPoseAndRebuildsAfterRelease()
        {
            Group(); var starts = _targets.Select(t=>t.position).ToArray(); var h = _demo.ActiveHandle;
            h.InteractionStart(); _targets[0].position += Vector3.right * .1f; _targets[1].position = new Vector3(3.8f,0,0);
            h.InteractionStay(); StringAssert.Contains("Outside", Get<string>("_notice"));
            Release();
            for(var i=0;i<2;i++) Assert.AreEqual(starts[i], _targets[i].position);
            yield return null;
            Assert.NotNull(_demo.ActiveHandle); Assert.IsFalse(_demo.IsDragging);
        }

        [Test]
        public void InvalidYawRestoresRotationAndPositionForTheWholeGroup()
        {
            Group(); var starts = _targets.Select(t=>t.position).ToArray(); var h = _demo.ActiveHandle;
            _demo.SetMode(HandleType.Rotation); h.InteractionStart();
            _targets[0].rotation=Quaternion.Euler(0,40,0); _targets[1].SetPositionAndRotation(new Vector3(3.5f,0,1.8f),Quaternion.Euler(0,45,0));
            Release();
            for(var i=0;i<2;i++) { Assert.AreEqual(starts[i], _targets[i].position); Assert.That(Quaternion.Angle(Quaternion.identity,_targets[i].rotation),Is.LessThan(.001f)); }
        }

        [TestCase("disable")]
        [TestCase("focus")]
        [TestCase("manager")]
        public void InterruptedValidPreviewIsCancelled(string interruption)
        {
            Group(); var starts = _targets.Select(t=>t.position).ToArray(); var h = _demo.ActiveHandle;
            h.InteractionStart(); foreach(var t in _targets) t.position += Vector3.forward * .2f;
            if(interruption=="disable") _demo.enabled=false;
            else if(interruption=="focus") Call("OnApplicationFocus",false);
            else { _manager.enabled=false; h.InteractionEnd(); }
            for(var i=0;i<2;i++) Assert.AreEqual(starts[i],_targets[i].position);
            Assert.IsFalse(_demo.IsDragging);
        }

        [UnityTest]
        public IEnumerator DestroyedMemberCancelsAndRestoresSurvivors()
        {
            Group(); var start=_targets[0].position; _demo.ActiveHandle.InteractionStart();
            _targets[0].position += Vector3.forward*.2f; Object.Destroy(_targets[1].gameObject);
            yield return null; yield return null;
            Assert.AreEqual(start,_targets[0].position); Assert.IsFalse(_demo.IsDragging);
            Assert.AreEqual(1,_demo.SelectionCount);
        }

        [Test]
        public void FinishChangesOnlySelectedOwnedMaterialsAndSurvivesReenable()
        {
            var first=_targets[0].GetComponentInChildren<Renderer>();var second=_targets[1].GetComponentInChildren<Renderer>();
            Select();var initial=first.sharedMaterial.color;_demo.SetFinish(1);var chosen=first.sharedMaterial.color;
            Assert.AreNotEqual(initial,chosen); Assert.AreEqual(initial,second.sharedMaterial.color);
            _demo.enabled=false;Assert.AreSame(_originalMaterials[0],first.sharedMaterial);
            _demo.enabled=true;Assert.AreEqual(chosen,first.sharedMaterial.color);
            _demo.ResetLayout();Assert.AreEqual(initial,first.sharedMaterial.color);
        }

        [Test]
        public void MixedFinishAndGroupFinishAreReflectedInInspector()
        {
            Select();_demo.SetFinish(1);_demo.SelectTarget(_targets[1],true);Call("RefreshUi");
            StringAssert.Contains("Mixed",Get<Label>("_finishLabel").text);
            _demo.SetFinish(2);Assert.AreEqual("Finish · Sand",Get<Label>("_finishLabel").text);
            Assert.AreEqual(_targets[0].GetComponentInChildren<Renderer>().sharedMaterial.color,_targets[1].GetComponentInChildren<Renderer>().sharedMaterial.color);
        }

        [Test]
        public void ResetKeepsPixelPreferenceAndBorrowedHandles()
        {
            var borrowed=_manager.CreateHandle(_camera.transform);_extraHandles.Add(borrowed);
            Select();Call("SetHandleSize",256f);_demo.SetFinish(2);_targets[0].position+=Vector3.forward*.2f;
            _demo.ResetLayout();Assert.AreEqual(256,Get<int>("_handleSizePixels"));Assert.IsTrue(borrowed!=null);
            Assert.AreEqual(0,_demo.SelectionCount);Assert.AreEqual(new Vector3(-1.5f,0,0),_targets[0].position);
            _demo.enabled=false;Assert.AreSame(_originalCamera,_manager.MainCamera);Assert.AreSame(_originalSettings,_manager.Settings);Assert.IsFalse(_manager.BlockWhenPointerOverUI);
        }

        [Test]
        public void DragDisablesFinishResetSizeAndSelectionCommands()
        {
            Select();var h=_demo.ActiveHandle;h.InteractionStart();Call("RefreshUi");
            var material=_targets[0].GetComponentInChildren<Renderer>().sharedMaterial;var color=material.color;
            _demo.SetFinish(2);_demo.ResetLayout();_demo.SelectTarget(_targets[1]);_demo.ToggleSpace();Call("SetHandleSize",300f);
            Assert.AreEqual(color,material.color);Assert.AreSame(h,_demo.ActiveHandle);Assert.AreEqual(1,_demo.SelectionCount);
            Assert.AreEqual(Space.World,h.Space);Assert.AreEqual(192,Get<int>("_handleSizePixels"));
            Assert.IsFalse(Get<Button>("_whiteButton").enabledSelf);Assert.IsFalse(Get<Button>("_resetButton").enabledSelf);
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
        public void ActualManagerCancellationRestoresValidPreviewBeforeNavigation()
        {
            Group(); var starts=_targets.Select(t=>t.position).ToArray();var h=_demo.ActiveHandle;
            var axis=h.GetComponentInChildren<PositionPlane>();
            Assert.IsTrue((bool)typeof(TransformHandleManager).GetMethod("BeginInteraction",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(_manager,new object[]{axis,axis.transform.position}));
            foreach(var t in _targets)t.position+=Vector3.forward*.2f;
            _manager.enabled=false;
            for(var i=0;i<2;i++)Assert.AreEqual(starts[i],_targets[i].position);
            Assert.IsFalse(_demo.IsDragging);
        }

        [Test]
        public void GroupYawRotatesPositionsAroundSharedCenterInBothSpaces()
        {
            foreach(var space in new[]{Space.World,Space.Self})
            {
                _demo.ResetLayout();Group();if(space==Space.Self)_demo.ToggleSpace();_demo.SetMode(HandleType.Rotation);
                var h=_demo.ActiveHandle;var center=h.Pivot.position;var starts=_targets.Select(t=>t.position).ToArray();
                h.InteractionStart();var rotation=Quaternion.Euler(0,20,0);
                _manager.UpdateGroupRotation(h.Pivot.GetComponent<Ghost>(),rotation);Release();
                for(var i=0;i<2;i++)
                {
                    Assert.That(Vector3.Distance(center+rotation*(starts[i]-center),_targets[i].position),Is.LessThan(.001f));
                    Assert.That(Quaternion.Angle(rotation,_targets[i].rotation),Is.LessThan(.001f));
                }
            }
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator SuppliedSceneProductsFitDeskAndResolveTheirChildColliders()
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
            var parts=(System.Array)typeof(ProductConfiguratorDemo).GetField("products",flags).GetValue(controller);
            Assert.AreEqual(3,parts.Length);
            foreach(var part in parts)
            {
                var root=(Transform)part.GetType().GetField("root").GetValue(part);
                Assert.IsTrue((bool)typeof(ProductConfiguratorDemo).GetMethod("FitsDesk",flags).Invoke(controller,new object[]{root}));
                Assert.Greater(root.GetComponentsInChildren<MeshFilter>().Length,2);
                controller.SelectTarget(root.GetComponentInChildren<Collider>().transform);
                Assert.AreEqual(1,controller.SelectionCount);
            }
        }
#endif
    }
}
