using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TransformHandles.Samples.Demo.Tests
{
    public class HandleDemoTests
    {
        private GameObject _root, _cameraObject, _lightObject, _otherTarget;
        private HandleDemo _demo;
        private TransformHandleManager _manager;
        private Camera _camera;
        private Handle _otherHandle;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private T Get<T>(string name) => (T)typeof(HandleDemo).GetField(name, Flags).GetValue(_demo);
        private void Set(string name, object value) => typeof(HandleDemo).GetField(name, Flags).SetValue(_demo, value);
        private object Call(string name, params object[] args) => typeof(HandleDemo).GetMethod(name, Flags).Invoke(_demo, args);
        private void Select() => Call("SelectSingle", Get<List<Transform>>("_targets")[0]);
        private static void Key(VisualElement field, KeyCode key)
        {
            using (var evt = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = key })) field.SendEvent(evt);
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _cameraObject = new GameObject("Borrowed showcase camera") { tag = "MainCamera" };
            _camera = _cameraObject.AddComponent<Camera>();
            _manager = TransformHandleManager.Instance;
            _manager.MainCamera = _camera; _manager.BlockWhenPointerOverUI = false;
            _lightObject = new GameObject("Existing light", typeof(Light));
            _root = new GameObject("Standalone showcase"); _root.SetActive(false);
            _demo = _root.AddComponent<HandleDemo>();
#if UNITY_EDITOR
            var layout = UnityEditor.AssetDatabase.FindAssets("Showcase t:VisualTreeAsset", new[] { "Assets" }).Single();
            var panel = UnityEditor.AssetDatabase.FindAssets("Showcase Panel t:PanelSettings", new[] { "Assets" }).Single();
            Set("uiLayout", UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UnityEditor.AssetDatabase.GUIDToAssetPath(layout)));
            Set("uiPanelSettings", UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(UnityEditor.AssetDatabase.GUIDToAssetPath(panel)));
#endif
            _root.SetActive(true);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_root != null) Object.Destroy(_root);
            yield return null;
            if (_otherHandle != null) { _manager.RemoveHandle(_otherHandle); Object.Destroy(_otherHandle.gameObject); }
            if (_otherTarget != null) Object.Destroy(_otherTarget);
            if (_manager != null) Object.Destroy(_manager.gameObject);
            Object.Destroy(_cameraObject); Object.Destroy(_lightObject);
            yield return null;
        }

        [Test]
        public void EveryTypeAxisAndSnapModeRemainsAvailable()
        {
            Select();
            foreach (HandleType type in Enum.GetValues(typeof(HandleType)))
            {
                Get<DropdownField>("_typeField").value = type.ToString();
                Assert.AreEqual(type, Get<Handle>("_activeHandle").Type);
            }
            foreach (HandleAxes axes in Enum.GetValues(typeof(HandleAxes)))
            {
                Get<DropdownField>("_axesField").value = axes.ToString();
                Assert.AreEqual(axes, Get<Handle>("_activeHandle").Axes);
            }
            foreach (SnappingType snap in Enum.GetValues(typeof(SnappingType)))
            {
                Get<DropdownField>("_snapField").value = snap.ToString();
                Assert.AreEqual(snap, Get<Handle>("_activeHandle").SnappingType);
            }
        }

        [Test]
        public void AdvancedControlsPreserveSnappingMultiplierAndSettings()
        {
            Select(); var handle = Get<Handle>("_activeHandle");
            Get<Slider>("_positionSlider").value = 2;
            Get<Slider>("_rotationSlider").value = 45;
            Get<Slider>("_scaleSlider").value = .25f;
            Assert.AreEqual(Vector3.one * 2, handle.PositionSnap);
            Assert.AreEqual(45, handle.RotationSnap);
            Assert.AreEqual(Vector3.one * .25f, handle.ScaleSnap);
            Get<SliderInt>("_sizeSlider").value = 320;
            Get<Slider>("_multiplierSlider").value = 2;
            Assert.AreEqual(320, handle.AutoScaleSizeInPixels);
            Assert.AreEqual(2, Get<float>("_scaleMultiplier"));
            Get<Toggle>("_autoToggle").value = false;
            Assert.IsFalse(handle.AutoScale);
            Assert.IsFalse(Get<TextField>("_sizeInput").enabledSelf);
            Get<Toggle>("_settingsToggle").value = true;
            Assert.IsTrue(Get<bool>("_useSettings"));
            Get<Slider>("_multiplierSlider").value = 3;
            Assert.AreEqual(3, handle.ScaleMultiplier, "Explicit scale remains editable after applying settings.");
            Get<Slider>("_positionSlider").value = 1;
            Assert.AreEqual(3, handle.ScaleMultiplier);
        }

        [Test]
        public void ResetOnlyRestoresOwnedSceneAndKeepsPixelPreference()
        {
            _otherTarget = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _otherTarget.transform.position = Vector3.one * 10;
            _otherHandle = _manager.CreateHandle(_otherTarget.transform);
            Select();
            var target = Get<List<Transform>>("_targets")[0];
            var position = target.position;
            target.position += Vector3.one; target.localScale = Vector3.one * 2;
            target.rotation = Quaternion.Euler(20, 30, 40);
            Get<SliderInt>("_sizeSlider").value = 300;
            Get<DropdownField>("_typeField").value = HandleType.All.ToString();
            Set("_camDist", 30f); Call("UpdateCamera");
            _demo.ResetLayout();
            Assert.AreEqual(position, target.position); Assert.AreEqual(Vector3.one, target.localScale);
            Assert.AreEqual(Quaternion.identity, target.rotation);
            Assert.IsNull(Get<Handle>("_activeHandle"));
            Assert.AreEqual(0, Get<List<string>>("_log").Count);
            Assert.AreEqual(HandleType.Position, Get<HandleType>("_type"));
            Assert.AreEqual(12, Get<float>("_camDist"));
            Assert.AreEqual(300, Get<int>("_handleSizePixels"));
            Assert.IsTrue(_otherHandle != null);
            Assert.AreEqual(Vector3.one * 10, _otherTarget.transform.position);
        }

        [Test]
        public void GroupAndClearLeaveBorrowedHandlesAlone()
        {
            _otherTarget = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _otherHandle = _manager.CreateHandle(_otherTarget.transform);
            Call("GroupAll");
            Assert.AreEqual(6, Get<Dictionary<Transform, Handle>>("_targetToHandle").Count);
            Assert.AreEqual(1, Get<Dictionary<Transform, Handle>>("_targetToHandle").Values.Distinct().Count());
            Call("DestroyAll");
            Assert.AreEqual(0, Get<Dictionary<Transform, Handle>>("_targetToHandle").Count);
            Assert.IsTrue(_otherHandle != null);
        }

        [UnityTest]
        public IEnumerator SizeEntryCommitsCancelsAndSurvivesReenable()
        {
            var field = Get<TextField>("_sizeInput");
            field.Focus(); yield return null; field.value = "320"; Key(field, KeyCode.Escape);
            Assert.AreEqual(192, Get<int>("_handleSizePixels"));
            field.Focus(); yield return null; field.value = "320"; Key(field, KeyCode.Return);
            Assert.AreEqual(320, Get<int>("_handleSizePixels"));
            var old = Get<VisualElement>("_uiRoot");
            _demo.enabled = false; yield return null;
            Assert.IsNull(old.panel);
            Assert.IsFalse(_manager.BlockWhenPointerOverUI);
            _demo.enabled = true; yield return null;
            Assert.AreNotSame(old, Get<VisualElement>("_uiRoot"));
            Assert.AreEqual("320", Get<TextField>("_sizeInput").value);
            Assert.IsFalse(Get<VisualElement>("_uiRoot").Q<Button>("demo-gallery").ClassListContains("available"));
            Get<Toggle>("_autoToggle").value = false;
            Assert.IsFalse(Get<bool>("_autoScale"));
        }

#if ENABLE_INPUT_SYSTEM
        [UnityTest]
        public IEnumerator CameraMovesOutsideUiAndChromeClaimsPressWithoutBrowser()
        {
            var original = UnityEngine.InputSystem.InputSystem.settings;
            var settings = Object.Instantiate(original);
            settings.updateMode = UnityEngine.InputSystem.InputSettings.UpdateMode.ProcessEventsManually;
            settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            settings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            UnityEngine.InputSystem.InputSystem.settings = settings;
            var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            try
            {
                var point = new Vector2(Screen.width * .65f, Screen.height * .4f);
                var before = _camera.transform.position;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState
                    { position = point, delta = new Vector2(10, 5) }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Middle));
                UnityEngine.InputSystem.InputSystem.Update(); Call("Update");
                Assert.AreNotEqual(before, _camera.transform.position, "Camera orbit must apply its accumulated angles.");
                before = _camera.transform.position;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = point, scroll = new Vector2(0, 1) });
                UnityEngine.InputSystem.InputSystem.Update(); Call("Update");
                Assert.AreNotEqual(before, _camera.transform.position, "Scroll must apply camera distance.");
                Get<Toggle>("_showTestToggle").value = true; Get<Toggle>("_blockTestToggle").value = false;
                yield return null;
                var root = Get<VisualElement>("_uiRoot");
                var test = Get<VisualElement>("_testRegion").worldBound.center;
                float scale = Screen.width / root.resolvedStyle.width;
                point = new Vector2(test.x * scale, Screen.height - test.y * scale);
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = point });
                UnityEngine.InputSystem.InputSystem.Update(); Call("Update");
                Assert.IsFalse((bool)Call("PointerOverUi")); Assert.IsFalse(_manager.BlockWhenPointerOverUI);
                var button = Get<Button>("_resetButton").worldBound.center;
                point = new Vector2(button.x * scale, Screen.height - button.y * scale);
                // No EventSystem.Process: current-position chrome protection must work with a stale hover cache.
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = point }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Middle));
                UnityEngine.InputSystem.InputSystem.Update(); Call("Update");
                Assert.IsFalse(_manager.enabled);
                before = _camera.transform.position;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState
                    { position = new Vector2(Screen.width * .7f, Screen.height * .2f), delta = new Vector2(30, 30) }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Middle));
                UnityEngine.InputSystem.InputSystem.Update(); Call("Update");
                Assert.AreEqual(before, _camera.transform.position);
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = point });
                UnityEngine.InputSystem.InputSystem.Update(); Call("Update");
                Assert.IsFalse(_manager.enabled);
                UnityEngine.EventSystems.EventSystem.current.currentInputModule.Process();
                Call("Update");
                yield return null; Call("Update");
                Assert.IsTrue(_manager.enabled, $"press={Get<bool>("_navigationPress")}, frame={Time.frameCount}, release={Get<int>("_releaseFrame")}, editing={Call("IsEditingText")}");
            }
            finally
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
                UnityEngine.InputSystem.InputSystem.settings = original; Object.Destroy(settings);
            }
        }
#endif
    }
}
