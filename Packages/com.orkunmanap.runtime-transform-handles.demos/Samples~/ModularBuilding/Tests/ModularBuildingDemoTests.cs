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

namespace TransformHandles.Samples.ModularBuilding.Tests
{
    public class ModularBuildingDemoTests
    {
        private GameObject _root;
        private ModularBuildingDemo _demo;
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
            _demo = _root.AddComponent<ModularBuildingDemo>();
            _camera = new GameObject("Sample camera").AddComponent<Camera>();
            _targets = new Transform[2];
            for (var i = 0; i < 2; i++)
            {
                _targets[i] = new GameObject("Module " + i).transform;
                _targets[i].position = new Vector3(i * 3, 0, 0);
                var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                mesh.SetParent(_targets[i], false);
                mesh.localPosition = new Vector3(1, .65f, 0);
                mesh.localScale = new Vector3(2, 1.3f, .2f);
            }
            _originalMaterials = _targets.Select(t => t.GetComponentInChildren<Renderer>().sharedMaterial).ToArray();
#if UNITY_EDITOR
            var settingsPaths = UnityEditor.AssetDatabase.FindAssets("ModularBuildingSettings t:TransformHandleSettings", new[] {"Assets"});
            Assert.AreEqual(1, settingsPaths.Length, "Import this sample exactly once before testing.");
            _settings = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<TransformHandleSettings>(UnityEditor.AssetDatabase.GUIDToAssetPath(settingsPaths[0])));
#else
            _settings = ScriptableObject.CreateInstance<TransformHandleSettings>();
            typeof(TransformHandleSettings).GetField("enableShortcuts", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_settings, false);
#endif
#if UNITY_EDITOR
            var layouts = UnityEditor.AssetDatabase.FindAssets("ModularBuilding t:VisualTreeAsset", new[] {"Assets"});
            var panels = UnityEditor.AssetDatabase.FindAssets("Workshop Panel t:PanelSettings", new[] {"Assets"});
            Assert.AreEqual(1, layouts.Length); Assert.AreEqual(1, panels.Length);
            Set("uiLayout", UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UnityEditor.AssetDatabase.GUIDToAssetPath(layouts[0])));
            Set("uiPanelSettings", UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(UnityEditor.AssetDatabase.GUIDToAssetPath(panels[0])));
#else
            Assert.Fail("This sample suite loads its imported UI assets through the Editor AssetDatabase.");
#endif
            _material = new Material(Shader.Find("TransformHandles/Samples/ModularBuildingOpaque"));
            Set("sceneCamera", _camera); Set("targets", _targets);
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

        private void Set(string name, object value) => typeof(ModularBuildingDemo).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_demo, value);
        private T Get<T>(string name) => (T)typeof(ModularBuildingDemo).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_demo);
        private object Call(string name, params object[] args) => typeof(ModularBuildingDemo).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_demo, args);
        private void Select() => _demo.SelectTarget(_targets[0]);

        private void Navigate(Vector3 position, int down = -1, int held = -1, bool ui = false, bool editing = false, float scroll = 0)
        {
            Call("ProcessNavigation", position, down == 0, down == 1, down == 2,
                held == 0, held == 1, held == 2, ui, editing, scroll, Time.frameCount);
        }

        [Test]
        public void LeftClickSelectsButOrbitReturningToOriginNeverSelects()
        {
            Call("BeginSelectionPress", Time.frameCount, Vector3.zero, false);
            Navigate(Vector3.zero, 0, 0); Navigate(Vector3.zero);
            Assert.IsTrue((bool)Call("CanSelectOnRelease", Vector3.zero, false));
            var yaw = Get<float>("_yaw");
            Call("BeginSelectionPress", Time.frameCount, Vector3.zero, false);
            Navigate(Vector3.zero, 0, 0); Navigate(new Vector3(80, 0), held: 0);
            Assert.AreNotEqual(yaw, Get<float>("_yaw"));
            Navigate(Vector3.zero);
            Assert.IsFalse((bool)Call("CanSelectOnRelease", Vector3.zero, false));
        }

        [Test]
        public void RightDragPansCameraWithoutMovingTargetsOrChangingSelection()
        {
            Select(); var target = _targets[0].position; var focus = Get<Vector3>("_cameraFocus");
            var yaw = Get<float>("_yaw");
            Navigate(Vector3.zero, 1, 1); Navigate(new Vector3(80, 40), held: 1); Navigate(new Vector3(80, 40));
            Assert.AreNotEqual(focus, Get<Vector3>("_cameraFocus"));
            Assert.AreEqual(yaw, Get<float>("_yaw"));
            Assert.AreEqual(target, _targets[0].position);
            Assert.AreEqual(1, _demo.SelectionCount);
        }

        [TestCase(false)]
        public void UiAndTextEntryCannotStartNavigationOrZoom(bool ui)
        {
            var focus = Get<Vector3>("_cameraFocus"); var distance = Get<float>("_distance");
            Navigate(Vector3.zero, 1, 1, ui, !ui); Navigate(new Vector3(80, 40), held: 1);
            Navigate(Vector3.zero, ui: ui, editing: !ui, scroll: 5);
            Assert.AreEqual(focus, Get<Vector3>("_cameraFocus"));
            Assert.AreEqual(distance, Get<float>("_distance"));
        }

        [TestCase(false)]
        public void HandleGestureCannotBecomeOrbitEvenWhenItEndsInStartFrame(bool managerFirst)
        {
            Select(); var yaw = Get<float>("_yaw");
            if (managerFirst) _demo.ActiveHandle.InteractionStart();
            Call("BeginSelectionPress", Time.frameCount, Vector3.zero, false);
            if (!managerFirst) _demo.ActiveHandle.InteractionStart();
            _demo.ActiveHandle.InteractionEnd();
            Navigate(Vector3.zero, 0, 0); Navigate(new Vector3(80, 0), held: 0);
            Assert.AreEqual(yaw, Get<float>("_yaw"));
            Assert.IsFalse((bool)Call("CanSelectOnRelease", Vector3.zero, false));
        }

        [Test]
        public void FocusLossAndDisableDiscardCameraGesture()
        {
            var focus = Get<Vector3>("_cameraFocus");
            Navigate(Vector3.zero, 1, 1); Call("OnApplicationFocus", false);
            Navigate(new Vector3(80, 40), held: 1);
            Assert.AreEqual(focus, Get<Vector3>("_cameraFocus"));
            Navigate(Vector3.zero, 1, 1); _demo.enabled = false; _demo.enabled = true;
            Navigate(new Vector3(80, 40), held: 1);
            Assert.AreEqual(focus, Get<Vector3>("_cameraFocus"));
        }

        [Test]
        public void ButtonChordCancelsCameraOwnership()
        {
            var focus = Get<Vector3>("_cameraFocus");
            Navigate(Vector3.zero, 1, 1);
            Call("ProcessNavigation", new Vector3(80, 40), true, false, false, true, true, false, false, false, 0f, Time.frameCount);
            Assert.AreEqual(focus, Get<Vector3>("_cameraFocus"));
            Assert.AreEqual(-1, Get<int>("_navigationButton"));
        }

        [Test]
        public void FractionalZoomIsProportionalBoundedAndBlockedDuringDrag()
        {
            var start = Get<float>("_distance");
            Navigate(Vector3.zero, scroll: .01f);
            var fine = Get<float>("_distance"); Assert.Less(fine, start); Assert.Greater(fine, start * .99f);
            Navigate(Vector3.zero, scroll: -1000); Assert.LessOrEqual(Get<float>("_distance"), fine * Mathf.Exp(.15f) + .0001f);
            for (var i = 0; i < 100; i++) Navigate(Vector3.zero, scroll: 1000);
            Assert.AreEqual(9f, Get<float>("_distance"));
            Select(); _demo.ActiveHandle.InteractionStart(); Navigate(Vector3.zero, scroll: -1000);
            Assert.AreEqual(9f, Get<float>("_distance"));
        }

        [UnityTest]
        public IEnumerator DisableRestoresSharedManagerAndCanReenableImmediately()
        {
            var other = _manager.CreateHandle(_targets[1]);
            _extraHandles.Add(other);
            Select(); _demo.enabled = false;
            Assert.AreSame(_originalCamera, _manager.MainCamera); Assert.AreSame(_originalSettings, _manager.Settings);
            Assert.IsFalse(_manager.BlockWhenPointerOverUI);
            _demo.enabled = true; Select();
            yield return null;
            Assert.IsNotNull(other); Assert.IsNotNull(_demo.ActiveHandle); Assert.AreSame(_camera, _manager.MainCamera);
        }

        [UnityTest]
        public IEnumerator DestroyRestoresMaterialsAndKeepsBorrowedManager()
        {
            _demo.enabled = false;
            Object.Destroy(_root);
            yield return null;
            Assert.IsNotNull(_manager);
            Assert.AreSame(_originalCamera, _manager.MainCamera);
            for (var i = 0; i < _targets.Length; i++)
                Assert.AreSame(_originalMaterials[i], _targets[i].GetComponentInChildren<Renderer>().sharedMaterial);
        }

        [UnityTest]
        public IEnumerator OwnedManagerSurvivesImmediateReenableAndDiesWithController()
        {
            _demo.enabled = false;
            Object.Destroy(_manager.gameObject);
            yield return null;
            _demo.enabled = true;
            var owned = TransformHandleManager.Instance;
            _demo.enabled = false; _demo.enabled = true; Select();
            yield return null;
            Assert.AreSame(owned, TransformHandleManager.Instance);
            Assert.IsNotNull(_demo.ActiveHandle);
            Object.Destroy(_root);
            yield return null;
            yield return null;
            Assert.IsTrue(owned == null);
        }

        [Test]
        public void HandleSizeIsAppliedBeforeSelectionReturnsAndDoesNotChangeTargets()
        {
            var initial = _targets[1].localScale;
            _demo.SelectTarget(_targets[1]);
            var handle = _demo.ActiveHandle;
            Assert.IsTrue(handle.AutoScale);
            Assert.That(Vector3.Distance(handle.transform.position, _targets[1].position), Is.LessThan(.0001f),
                "The visible handle must be at this non-origin target before selection returns.");
            Assert.AreEqual(192, handle.AutoScaleSizeInPixels);
            var distance = Mathf.Abs(GeometryUtility.CalculateFrustumPlanes(_camera)[4].GetDistanceToPoint(handle.transform.position));
            var expected = 2 * Mathf.Tan(_camera.fieldOfView * .5f * Mathf.Deg2Rad) * 192 / Screen.height * distance;
            Assert.That(handle.transform.localScale.x, Is.EqualTo(expected).Within(.0001f));
            Assert.AreEqual(initial, _targets[1].localScale);
        }

        [Test]
        public void NumericSizeClampsValidIntegersAndRestoresInvalidText()
        {
            Call("CommitHandleSize", "999"); Assert.AreEqual(512, Get<int>("_handleSizePixels"));
            Call("CommitHandleSize", "-20"); Assert.AreEqual(64, Get<int>("_handleSizePixels"));
            Call("CommitHandleSize", "256");
            foreach (var invalid in new[] {"", "abc", "1.5", "99999999999999999999"})
            {
                Call("CommitHandleSize", invalid);
                Assert.AreEqual(256, Get<int>("_handleSizePixels"));
                Assert.AreEqual("256", Get<TextField>("_sizeInput").value);
                Assert.AreEqual(256, Get<SliderInt>("_sizeSlider").value);
            }
            Select(); Assert.AreEqual(256, _demo.ActiveHandle.AutoScaleSizeInPixels);
        }

        [Test]
        public void SizeControlsCannotMutateDuringHandleDragOrAffectOtherHandles()
        {
            var other = _manager.CreateHandle(_targets[1]); _extraHandles.Add(other);
            other.AutoScale = false; other.AutoScaleSizeInPixels = 111;
            Select(); _demo.ActiveHandle.AutoScale = false;
            Get<SliderInt>("_sizeSlider").value = 240;
            Assert.IsTrue(_demo.ActiveHandle.AutoScale);
            Assert.IsFalse(other.AutoScale); Assert.AreEqual(111, other.AutoScaleSizeInPixels);
            _demo.ActiveHandle.InteractionStart(); Call("RefreshUi");
            Assert.IsFalse(Get<SliderInt>("_sizeSlider").enabledSelf);
            Assert.IsFalse(Get<TextField>("_sizeInput").enabledSelf);
            Call("SetHandleSize", 400f); Call("CommitHandleSize", "400");
            Assert.AreEqual(240, _demo.ActiveHandle.AutoScaleSizeInPixels);
            Assert.AreEqual(240, Get<SliderInt>("_sizeSlider").value);
            Assert.AreEqual("240", Get<TextField>("_sizeInput").value);
        }

        [Test]
        public void UiOriginAndUiReleaseBlockSelection()
        {
            Call("BeginSelectionPress", Time.frameCount, Vector3.zero, true);
            Assert.IsFalse((bool)Call("CanSelectOnRelease", Vector3.zero, false));
            Call("BeginSelectionPress", Time.frameCount, Vector3.zero, false);
            Assert.IsFalse((bool)Call("CanSelectOnRelease", Vector3.zero, true));
            Assert.IsTrue((bool)Call("CanSelectOnRelease", Vector3.zero, false));
        }

        [TestCase(false)]
        public void DragReleaseNeverBecomesSelectionRegardlessOfUpdateOrder(bool managerFirst)
        {
            Select();
            if (managerFirst) _demo.ActiveHandle.InteractionStart();
            Call("BeginSelectionPress", Time.frameCount, Vector3.zero, false);
            if (!managerFirst) _demo.ActiveHandle.InteractionStart();
            _demo.ActiveHandle.InteractionEnd();
            Assert.IsFalse((bool)Call("CanSelectOnRelease", Vector3.zero, false));
        }

#if ENABLE_INPUT_SYSTEM
        [UnityTest]
        public IEnumerator PanelPressCannotStartCoreDragWhenManagerRunsBeforeDemo()
        {
            // Batch mode has no focused Game view. Both settings are needed for public Update()
            // to process player actions rather than only changing Editor device state.
            var originalInputSettings = UnityEngine.InputSystem.InputSystem.settings;
            var inputSettings = Object.Instantiate(originalInputSettings);
            inputSettings.updateMode = UnityEngine.InputSystem.InputSettings.UpdateMode.ProcessEventsManually;
            inputSettings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            inputSettings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            UnityEngine.InputSystem.InputSystem.settings = inputSettings;
            var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            try
            {
                Select(); yield return null;
                var plane = _demo.ActiveHandle.GetComponentInChildren<PositionPlane>();
                var collider = plane.GetComponentInChildren<Collider>();
                Assert.IsNotNull(collider);
                Physics.SyncTransforms();
                var point = (Vector2)_camera.WorldToScreenPoint(collider.bounds.center);
                var managerUpdate = typeof(TransformHandleManager).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
                var hovered = typeof(TransformHandleManager).GetField("_hoveredHandle", BindingFlags.Instance | BindingFlags.NonPublic);
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState {position = point});
                UnityEngine.InputSystem.InputSystem.Update();
                EventSystem.current.currentInputModule.Process();
                managerUpdate.Invoke(_manager, null);
                Assert.IsNotNull(hovered.GetValue(_manager), "The unobstructed pointer must actually hit a Core handle.");
                var overlay = new VisualElement();
                overlay.style.position = Position.Absolute;
                overlay.style.left = overlay.style.top = overlay.style.right = overlay.style.bottom = 0;
                Get<VisualElement>("_uiRoot").Add(overlay);
                yield return null;
                // Transition from an outside position to a down press on the UI in one input update.
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState {position = new Vector2(-10,-10)});
                UnityEngine.InputSystem.InputSystem.Update(); EventSystem.current.currentInputModule.Process();
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,
                    new UnityEngine.InputSystem.LowLevel.MouseState {position = point}.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
                UnityEngine.InputSystem.InputSystem.Update(); EventSystem.current.currentInputModule.Process();
                Assert.IsTrue(HitsToolkit(point), "The overlay must raycast at the actual handle point.");
                Assert.IsTrue(EventSystem.current.IsPointerOverGameObject(), "The input module must publish the panel pointer before Core runs.");
                managerUpdate.Invoke(_manager, null); // No demo Update is needed to protect Core.
                Assert.IsNull(hovered.GetValue(_manager));
                Assert.IsFalse(_demo.IsDragging);
                Assert.AreEqual(Vector3.zero, _targets[0].position);
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState {position = point});
                UnityEngine.InputSystem.InputSystem.Update(); EventSystem.current.currentInputModule.Process();
                overlay.RemoveFromHierarchy();
            }
            finally
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
                UnityEngine.InputSystem.InputSystem.settings = originalInputSettings;
                Object.Destroy(inputSettings);
            }
        }
#endif

        private Vector2 ScreenPoint(VisualElement element)
        {
            var root = Get<VisualElement>("_uiRoot");
            var point = element.worldBound.center;
            var scale = Screen.width / root.worldBound.width;
            return new Vector2(point.x * scale, Screen.height - point.y * scale);
        }

        private bool HitsToolkit(Vector2 point)
        {
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
            return hits.Any(h => h.module is PanelRaycaster raycaster && raycaster.panel == Get<VisualElement>("_uiRoot").panel);
        }

        private static void SendKey(VisualElement element, KeyCode key)
        {
            using (var evt = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = key }))
                element.SendEvent(evt);
        }

        [Test]
        public void InspectorSizeWorksWithoutSelectionAndKeepsPreference()
        {
            Assert.AreEqual("Select a module", Get<Label>("_selectionLabel").text);
            Assert.IsFalse(Get<Button>("_moveButton").enabledSelf);
            Assert.IsTrue(Get<TextField>("_sizeInput").enabledSelf);
            Get<SliderInt>("_sizeSlider").value = 320;
            Assert.AreEqual("320", Get<TextField>("_sizeInput").value);
            Select(); Assert.AreEqual(320, _demo.ActiveHandle.AutoScaleSizeInPixels);
        }

        [UnityTest]
        public IEnumerator HelpDismissalBlocksEntirePressUntilAfterManagerUpdates()
        {
            Select(); Call("ToggleHelpPanel"); yield return null;
            var point = new Vector2(Screen.width * .6f, Screen.height * .3f);
            Assert.IsTrue(HitsToolkit(point));
            Call("BeginSelectionPress", Time.frameCount, (Vector3)point, true);
            Call("RequestCloseHelpPanel");
            Assert.IsFalse(Get<VisualElement>("_helpBackdrop").ClassListContains("hidden"));
            Assert.IsFalse((bool)Call("CanSelectOnRelease", (Vector3)point, false));
            Navigate(point, 0, 0);
            Assert.AreEqual(-1, Get<int>("_navigationButton"));
            Call("ApplyDeferredPanelClose"); yield return null;
            Assert.IsFalse(HitsToolkit(point));
            Assert.AreEqual(1, _demo.SelectionCount);
        }

        [UnityTest]
        public IEnumerator EscapeRespectsEditingThenClosesHelpBeforeClearingSelection()
        {
            Select(); var field = Get<TextField>("_sizeInput"); field.Focus(); yield return null;
            Assert.IsTrue(Get<bool>("_sizeEditing"));
            Call("HandleEscape", Time.frameCount); Assert.AreEqual(1, _demo.SelectionCount);
            SendKey(field, KeyCode.Escape);
            Call("HandleEscape", Time.frameCount); Assert.AreEqual(1, _demo.SelectionCount);
            Call("ToggleHelpPanel"); Call("HandleEscape", Time.frameCount + 1);
            Assert.IsTrue(Get<bool>("_closeHelpPanelPending"));
            Call("ApplyDeferredPanelClose"); Assert.AreEqual(1, _demo.SelectionCount);
            Call("HandleEscape", Time.frameCount + 2); Assert.AreEqual(0, _demo.SelectionCount);
        }

        [UnityTest]
        public IEnumerator NumericFocusLossCommitsAndEditEndFrameSuppressesShortcuts()
        {
            Select(); var field = Get<TextField>("_sizeInput"); field.Focus(); yield return null;
            Assert.IsTrue((bool)Call("SuppressShortcuts", Time.frameCount));
            field.value = "280"; field.Blur();
            Assert.IsFalse(Get<bool>("_sizeEditing"));
            Assert.IsTrue((bool)Call("SuppressShortcuts", Time.frameCount));
            Assert.IsFalse((bool)Call("SuppressShortcuts", Time.frameCount + 1));
            Assert.AreEqual(1, _demo.SelectionCount);
            Assert.AreEqual(280, _demo.ActiveHandle.AutoScaleSizeInPixels);
        }

        [UnityTest]
        public IEnumerator ToolkitControlsBlockSceneButEmptySpaceDoesNot()
        {
            yield return null;
            foreach (var element in new VisualElement[] {Get<SliderInt>("_sizeSlider"), Get<TextField>("_sizeInput"), Get<Button>("_resetButton")})
            {
                var point = ScreenPoint(element);
                Assert.IsTrue(HitsToolkit(point), element.name);
                Call("BeginSelectionPress", 100, (Vector3)point, true);
                Assert.IsFalse((bool)Call("CanSelectOnRelease", (Vector3)point, false));
            }
            Assert.IsFalse(HitsToolkit(new Vector2(Screen.width * .7f, Screen.height * .2f)));
        }

        [UnityTest]
        public IEnumerator ReenableRebindsNewTreeOnceAndDisableRemovesRaycasts()
        {
            var previous = Get<VisualElement>("_uiRoot");
            var point = ScreenPoint(Get<TextField>("_sizeInput"));
            Call("ToggleHelpPanel");
            _demo.enabled = false; yield return null;
            Assert.IsNull(previous.panel);
            _demo.enabled = true; yield return null;
            Assert.AreNotSame(previous, Get<VisualElement>("_uiRoot"));
            Assert.IsTrue(HitsToolkit(point));
            Assert.IsFalse(Get<bool>("_helpPanelOpen"));
            var button = Get<Button>("_helpToggleButton");
#if UNITY_6000_0_OR_NEWER
            button.Focus();
            using (var evt = NavigationSubmitEvent.GetPooled()) button.SendEvent(evt);
#else
            using (var evt = MouseDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = button.worldBound.center })) button.SendEvent(evt);
            using (var evt = MouseUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = button.worldBound.center })) button.SendEvent(evt);
#endif
            Assert.IsTrue(Get<bool>("_helpPanelOpen"), "A duplicate callback would toggle the panel closed again.");
            Assert.IsFalse(Get<bool>("_closeHelpPanelPending"));
            Select(); _demo.ActiveHandle.InteractionStart(); Call("RefreshUi");
            Assert.IsFalse(Get<Button>("_helpToggleButton").enabledSelf);
        }

        [UnityTest]
        public IEnumerator HelpTextAndSelectionFitInsideTheirPanels()
        {
            _targets[0].name = "Duvar İğüş · 90°";
            Select(); Call("ToggleHelpPanel"); Call("RefreshUi"); yield return null;
            Assert.AreEqual(_targets[0].name, Get<Label>("_selectionLabel").text);
            Assert.IsFalse(Get<Label>("_selectionLabel").enableRichText);
            foreach (var message in new[] {"Select a wall or corner and arrange your layout.",
                "Drag the handle to place this module.", "Scene reset. Handle size preference is kept."})
            {
                Set("_notice", message); Call("RefreshUi"); yield return null;
                var card = Get<VisualElement>("_helpCard");
                card.Query<Label>().ForEach(label => {
                    Assert.Greater(label.layout.height, 0, label.text);
                    Assert.LessOrEqual(label.worldBound.yMax, card.worldBound.yMax, label.text);
                    Assert.LessOrEqual(label.worldBound.xMax, card.worldBound.xMax, label.text);
                });
            }
            Assert.IsNull(Get<VisualElement>("_uiRoot").Q("selection-context"));
        }

        [UnityTest]
        public IEnumerator NumericEscapeCancelsAndReturnCommits()
        {
            Select(); var field = Get<TextField>("_sizeInput");
            field.Focus(); yield return null; field.value = "320";
            SendKey(field, KeyCode.Escape);
            Assert.AreEqual(192, Get<int>("_handleSizePixels"));
            Assert.AreEqual("192", field.value); Assert.AreEqual(1, _demo.SelectionCount);
            field.Focus(); yield return null; field.value = "320";
            SendKey(field, KeyCode.Return);
            Assert.AreEqual(320, _demo.ActiveHandle.AutoScaleSizeInPixels);
            Assert.AreEqual("320", field.value);
        }

        [Test]
        public void ChildSelectionReplacesPreviousModuleAndIgnoresFloor()
        {
            _demo.SelectTarget(_targets[0].GetChild(0));
            Assert.AreEqual(1, _demo.SelectionCount);
            var first = _demo.ActiveHandle;
            _demo.SelectTarget(_targets[1].GetChild(0));
            Assert.AreEqual(1, _demo.SelectionCount);
            Assert.AreNotSame(first, _demo.ActiveHandle);
            Assert.That(Vector3.Distance(_demo.ActiveHandle.Pivot.position, _targets[1].position), Is.LessThan(.0001f));
            _demo.SelectTarget(_camera.transform);
            Assert.AreEqual(1, _demo.SelectionCount);
        }

        [UnityTest]
        public IEnumerator ModeTransitionsExposeOnlyHorizontalMoveOrYawAndRejectScale()
        {
            Select();
            Assert.AreEqual(HandleAxes.XZ, _demo.ActiveHandle.Axes);
            Assert.AreEqual(2, _demo.ActiveHandle.GetComponentsInChildren<PositionAxis>().Length);
            Assert.AreEqual(1, _demo.ActiveHandle.GetComponentsInChildren<PositionPlane>().Length);
            _demo.SetMode(HandleType.Rotation);
            yield return null;
            Assert.AreEqual(HandleAxes.Y, _demo.ActiveHandle.Axes);
            Assert.AreEqual(1, _demo.ActiveHandle.GetComponentsInChildren<RotationAxis>().Length);
            Assert.AreEqual(0, _demo.ActiveHandle.GetComponentsInChildren<PositionAxis>().Length);
            _demo.SetMode(HandleType.Scale);
            Assert.AreEqual(HandleType.Rotation, _demo.ActiveHandle.Type);
            _demo.SetMode(HandleType.Position);
            yield return null;
            Assert.AreEqual(HandleAxes.XZ, _demo.ActiveHandle.Axes);
            Assert.AreEqual(0, _demo.ActiveHandle.GetComponentsInChildren<RotationAxis>().Length);
        }

        // Move the camera so the real input ray hits a chosen ground point. This exercises
        // Core's ray/plane interaction and snapping without mocking the snap calculation.
        private void AimPointerAt(Vector3 point)
        {
            var ray = _camera.ScreenPointToRay(TransformHandles.Utils.InputWrapper.MousePosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            Assert.IsTrue(plane.Raycast(ray, out var distance));
            _camera.transform.position += point - ray.GetPoint(distance);
        }

        [Test]
        public void PlaneDragSnapsBothCoordinatesAndPreservesHeightAndScale()
        {
            Select(); var handle = _demo.ActiveHandle;
            AimPointerAt(new Vector3(.2f,0,.2f));
            var plane = handle.GetComponentInChildren<PositionPlane>();
            plane.StartInteraction(new Vector3(.2f,0,.2f));
            AimPointerAt(new Vector3(1.45f,0,2.6f));
            var start = handle.Pivot.position;
            plane.Interact(Vector3.zero);
            _manager.UpdateGroupPosition(handle.Pivot.GetComponent<Ghost>(), handle.Pivot.position - start);
            Assert.That(Vector3.Distance(new Vector3(1,0,2), _targets[0].position), Is.LessThan(.0001f));
            Assert.AreEqual(Vector3.one, _targets[0].localScale);
            Assert.AreEqual(Quaternion.identity, _targets[0].rotation);
            plane.EndInteraction();
        }

        [Test]
        public void RotationDragSnapsToQuarterTurnAndPreservesPositionAndScale()
        {
            Select(); _demo.SetMode(HandleType.Rotation); var handle = _demo.ActiveHandle;
            AimPointerAt(Vector3.right * 2);
            var axis = handle.GetComponentInChildren<RotationAxis>();
            axis.StartInteraction(Vector3.right * 2);
            AimPointerAt(new Vector3(.3f,0,-2));
            var start = handle.Pivot.rotation;
            axis.Interact(Vector3.zero);
            _manager.UpdateGroupRotation(handle.Pivot.GetComponent<Ghost>(), handle.Pivot.rotation * Quaternion.Inverse(start));
            Assert.That(Quaternion.Angle(_targets[0].rotation, Quaternion.Euler(0,90,0)), Is.LessThan(.01f));
            Assert.AreEqual(Vector3.zero, _targets[0].position);
            Assert.AreEqual(Vector3.one, _targets[0].localScale);
            axis.EndInteraction();
        }

        [Test]
        public void ResetRestoresModulesCameraAndModeButKeepsHandleSize()
        {
            Call("SetHandleSize", 300f); Select();
            _targets[0].SetPositionAndRotation(new Vector3(4,0,2), Quaternion.Euler(0,90,0));
            _demo.SetMode(HandleType.Rotation);
            Navigate(Vector3.zero, 1, 1); Navigate(new Vector3(100,50), held:1);
            _demo.ResetLayout();
            Assert.AreEqual(Vector3.zero, _targets[0].position);
            Assert.AreEqual(Quaternion.identity, _targets[0].rotation);
            Assert.AreEqual(new Vector3(0,.5f,0), Get<Vector3>("_cameraFocus"));
            Assert.AreEqual(0, _demo.SelectionCount);
            Select(); Assert.AreEqual(HandleType.Position, _demo.ActiveHandle.Type);
            Assert.AreEqual(300, _demo.ActiveHandle.AutoScaleSizeInPixels);
        }

        [Test]
        public void DragBlocksModeAndResetAndEscapeKeepsLastLayout()
        {
            Select(); _demo.ActiveHandle.InteractionStart();
            _targets[0].position = new Vector3(2,0,1);
            _demo.SetMode(HandleType.Rotation); _demo.ResetLayout();
            Assert.AreEqual(HandleType.Position, _demo.ActiveHandle.Type);
            Assert.AreEqual(new Vector3(2,0,1), _targets[0].position);
            Call("HandleEscape", Time.frameCount);
            Assert.AreEqual(0, _demo.SelectionCount);
            Assert.IsFalse(_demo.IsDragging);
            Assert.AreEqual(new Vector3(2,0,1), _targets[0].position);
        }

        [UnityTest]
        public IEnumerator DestroyedSelectionRemovesItsHandle()
        {
            Select(); Object.Destroy(_targets[0].gameObject);
            yield return null; yield return null;
            Assert.AreEqual(0, _demo.SelectionCount);
            Assert.IsNull(_demo.ActiveHandle);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void InvalidModuleSetupIsRejected(int invalid)
        {
            var t = _targets[0];
            if (invalid == 0) t.localScale = Vector3.one * 2;
            if (invalid == 1) t.position = Vector3.up;
            if (invalid == 2) t.position = Vector3.right * .5f;
            if (invalid == 3) t.rotation = Quaternion.Euler(0,45,0);
            if (invalid == 4) t.rotation = Quaternion.Euler(90,0,0);
            var validate = typeof(ModularBuildingDemo).GetMethod("ValidModule", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsFalse((bool)validate.Invoke(null, new object[] {t}));
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator ShippedSceneBootsAndModuleEndpointsJoinAtEveryQuarterTurn()
        {
            _demo.enabled = false;
            var paths = UnityEditor.AssetDatabase.FindAssets("t:Scene", new[] {"Assets"})
                .Select(UnityEditor.AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith("/ModularBuilding.unity")).ToArray();
            Assert.AreEqual(1, paths.Length);
            _loadedScene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(paths[0],
                new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Additive));
            yield return null;
            var controller = _loadedScene.GetRootGameObjects().Select(go => go.GetComponent<ModularBuildingDemo>()).Single(d => d != null);
            Assert.IsTrue(controller.enabled);
            var shader = Shader.Find("TransformHandles/Samples/ModularBuildingOpaque");
            Assert.IsNotNull(shader); Assert.IsFalse(UnityEditor.ShaderUtil.ShaderHasError(shader));
            var modules = (Transform[])typeof(ModularBuildingDemo).GetField("targets", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(controller);
            Assert.AreEqual(8, modules.Length);
            Physics.SyncTransforms();
            for (var a = 0; a < modules.Length; a++)
                for (var b = a + 1; b < modules.Length; b++)
                    foreach (var ca in modules[a].GetComponentsInChildren<BoxCollider>())
                        foreach (var cb in modules[b].GetComponentsInChildren<BoxCollider>())
                            if (Physics.ComputePenetration(ca, ca.transform.position, ca.transform.rotation,
                                cb, cb.transform.position, cb.transform.rotation, out _, out var depth))
                                Assert.Less(depth, .0001f, "Initial modules must not overlap: " + modules[a].name + "/" + modules[b].name);
            var corner = modules.First(t => t.childCount == 2);
            var straight = modules.First(t => t.childCount == 1);
            for (var turn = 0; turn < 4; turn++)
            {
                corner.SetPositionAndRotation(Vector3.zero,Quaternion.Euler(0,turn*90,0));
                foreach (var direction in new[] {Vector3.right,Vector3.forward})
                {
                    straight.SetPositionAndRotation(corner.TransformPoint(direction*2),
                        corner.rotation * Quaternion.FromToRotation(Vector3.right,direction));
                    Physics.SyncTransforms();
                    var seam = corner.TransformPoint(direction*2 + Vector3.up*.65f);
                    var wall = straight.GetComponentInChildren<BoxCollider>();
                    Assert.That(Vector3.Distance(seam, wall.ClosestPoint(seam)),Is.LessThan(.0001f));
                    var matching = corner.GetComponentsInChildren<BoxCollider>().Single(c => Vector3.Distance(seam,c.ClosestPoint(seam)) < .0001f);
                    var ahead = seam + corner.TransformDirection(direction)*.01f;
                    var behind = seam - corner.TransformDirection(direction)*.01f;
                    Assert.That(Vector3.Distance(ahead,matching.ClosestPoint(ahead)),Is.EqualTo(.01f).Within(.0001f));
                    Assert.That(Vector3.Distance(behind,wall.ClosestPoint(behind)),Is.EqualTo(.01f).Within(.0001f));
                    Assert.That(Vector3.Distance(ahead,wall.ClosestPoint(ahead)),Is.LessThan(.0001f));
                    Assert.That(Vector3.Distance(behind,matching.ClosestPoint(behind)),Is.LessThan(.0001f));
                }
            }
        }
#endif
    }
}
