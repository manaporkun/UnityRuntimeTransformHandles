using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
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
            _material = new Material(Shader.Find("TransformHandles/Samples/ModularBuildingOpaque"));
            Set("sceneCamera", _camera); Set("targets", _targets);
            Set("targetMaterial", _material); Set("interactionSettings", _settings);
            _root.SetActive(true);
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
        public void SizePopoverWorksWithoutSelectionAndPreservesPreferenceAfterClose()
        {
            Assert.IsTrue(Get<RectTransform>("_contextBar").gameObject.activeSelf);
            Assert.AreEqual("Select a wall or corner", Get<Text>("_selectionLabel").text);
            Assert.IsFalse(Get<Text>("_modeLabel").gameObject.activeSelf);
            Assert.IsTrue(Get<Button>("_sizeToggleButton").interactable);
            Call("ToggleSizePanel");
            Get<Slider>("_sizeSlider").value = 320;
            Call("RefreshUi");
            StringAssert.Contains("320 px", Get<Button>("_sizeToggleButton").GetComponentInChildren<Text>().text);
            Call("RequestCloseSizePanel"); Call("ApplyDeferredPanelClose");
            Assert.IsFalse(Get<RectTransform>("_selectionCard").gameObject.activeSelf);
            Select(); Assert.AreEqual(320, _demo.ActiveHandle.AutoScaleSizeInPixels);
        }

        [UnityTest]
        public IEnumerator BackdropDismissalBlocksEntirePressUntilAfterManagerUpdates()
        {
            Select(); Call("ToggleSizePanel");
            yield return null; // Newly activated graphics need a render registration before raycasting.
            var canvas = Get<GameObject>("_canvas");
            Canvas.ForceUpdateCanvases();
            var backdrop = Get<Button>("_sizeBackdrop");
            var point = new Vector2(Screen.width * .5f, Screen.height * .5f);
            var hits = new List<RaycastResult>();
            canvas.GetComponent<GraphicRaycaster>().Raycast(new PointerEventData(EventSystem.current) {position = point}, hits);
            Assert.IsTrue(hits.Exists(h => h.gameObject == backdrop.gameObject));
            Call("BeginSelectionPress", Time.frameCount, (Vector3)point, true);
            Call("RequestCloseSizePanel");
            Assert.IsTrue(backdrop.gameObject.activeInHierarchy, "Core input must still see the UI in this Update.");
            Assert.IsFalse((bool)Call("CanSelectOnRelease", (Vector3)point, false));
            Navigate(point, 0, 0);
            Assert.AreEqual(-1, Get<int>("_navigationButton"));
            Call("ApplyDeferredPanelClose");
            hits.Clear();
            canvas.GetComponent<GraphicRaycaster>().Raycast(new PointerEventData(EventSystem.current) {position = point}, hits);
            Assert.IsFalse(hits.Exists(h => h.gameObject == backdrop.gameObject));
            Assert.AreEqual(1, _demo.SelectionCount);
        }

        [UnityTest]
        public IEnumerator EscapeRespectsEditingThenClosesPopoverBeforeClearingSelection()
        {
            Select(); Call("ToggleSizePanel");
            var field = Get<InputField>("_sizeInput"); field.Select(); field.ActivateInputField();
            yield return null;
            Assert.IsTrue(field.isFocused);
            Call("HandleEscape", Time.frameCount);
            Assert.IsFalse(Get<bool>("_closeSizePanelPending"));
            field.DeactivateInputField();
            Call("HandleEscape", Time.frameCount);
            Assert.IsFalse(Get<bool>("_closeSizePanelPending"), "Editing-end frame is consumed.");
            Call("HandleEscape", Time.frameCount + 1);
            Assert.IsTrue(Get<bool>("_closeSizePanelPending"));
            Call("ApplyDeferredPanelClose"); Assert.AreEqual(1, _demo.SelectionCount);
            Call("HandleEscape", Time.frameCount + 2); Assert.AreEqual(0, _demo.SelectionCount);
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
                Assert.AreEqual("256", Get<InputField>("_sizeInput").text);
                Assert.AreEqual(256, Get<Slider>("_sizeSlider").value);
            }
            Select(); Assert.AreEqual(256, _demo.ActiveHandle.AutoScaleSizeInPixels);
        }

        [UnityTest]
        public IEnumerator NumericFocusAndEditEndFrameSuppressShortcuts()
        {
            Select(); Call("ToggleSizePanel");
            var field = Get<InputField>("_sizeInput");
            field.Select(); field.ActivateInputField();
            yield return null;
            Assert.IsTrue(field.isFocused);
            Assert.IsTrue((bool)Call("SuppressShortcuts", Time.frameCount));
            field.text = "280"; field.DeactivateInputField();
            Assert.IsFalse(field.isFocused);
            Assert.IsTrue((bool)Call("SuppressShortcuts", Time.frameCount));
            Assert.IsFalse((bool)Call("SuppressShortcuts", Time.frameCount + 1));
            Assert.AreEqual(1, _demo.SelectionCount);
            Assert.AreEqual(280, _demo.ActiveHandle.AutoScaleSizeInPixels);
        }

        [Test]
        public void SizeControlsCannotMutateDuringHandleDragOrAffectOtherHandles()
        {
            var other = _manager.CreateHandle(_targets[1]); _extraHandles.Add(other);
            other.AutoScale = false; other.AutoScaleSizeInPixels = 111;
            Select(); _demo.ActiveHandle.AutoScale = false;
            Get<Slider>("_sizeSlider").value = 240;
            Assert.IsTrue(_demo.ActiveHandle.AutoScale);
            Assert.IsFalse(other.AutoScale); Assert.AreEqual(111, other.AutoScaleSizeInPixels);
            _demo.ActiveHandle.InteractionStart(); Call("RefreshUi");
            Assert.IsFalse(Get<Slider>("_sizeSlider").interactable);
            Assert.IsFalse(Get<InputField>("_sizeInput").interactable);
            Call("SetHandleSize", 400f); Call("CommitHandleSize", "400");
            Assert.AreEqual(240, _demo.ActiveHandle.AutoScaleSizeInPixels);
            Assert.AreEqual(240, Get<Slider>("_sizeSlider").value);
            Assert.AreEqual("240", Get<InputField>("_sizeInput").text);
        }

        [UnityTest]
        public IEnumerator SliderAndInputBlockSelectionRaycasts()
        {
            Call("ToggleSizePanel");
            yield return null; // The sizing controls now start inactive inside the popover.
            Canvas.ForceUpdateCanvases();
            var canvas = Get<GameObject>("_canvas");
            Assert.That(Get<Slider>("_sizeSlider").handleRect.rect.height, Is.EqualTo(18).Within(.01f));
            foreach (var rect in new[] {(RectTransform)Get<Slider>("_sizeSlider").transform,
                         (RectTransform)Get<InputField>("_sizeInput").transform})
            {
                var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
                var hits = new List<RaycastResult>();
                canvas.GetComponent<GraphicRaycaster>().Raycast(new PointerEventData(EventSystem.current) {position = point}, hits);
                Assert.IsTrue(hits.Exists(h => h.gameObject == rect.gameObject));
                Call("BeginSelectionPress", 100, (Vector3)point, true);
                Assert.IsFalse((bool)Call("CanSelectOnRelease", (Vector3)point, true));
            }
        }

        [Test]
        public void DragCannotOpenSizePopoverAndDisableRemovesItsRaycasts()
        {
            Select(); _demo.ActiveHandle.InteractionStart(); Call("RefreshUi");
            Assert.IsFalse(Get<Button>("_sizeToggleButton").interactable);
            Call("ToggleSizePanel"); Assert.IsFalse(Get<bool>("_sizePanelOpen"));
            _demo.ActiveHandle.InteractionEnd();
            Call("ToggleSizePanel"); Assert.IsTrue(Get<bool>("_sizePanelOpen"));
            _demo.enabled = false; _demo.enabled = true;
            Assert.IsFalse(Get<bool>("_sizePanelOpen"));
            Assert.IsFalse(Get<Button>("_sizeBackdrop").gameObject.activeInHierarchy);
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

        [UnityTest]
        public IEnumerator HelpDismissalConsumesThePressAndEscapeKeepsSelection()
        {
            Select(); Call("ToggleHelpPanel");
            yield return null;
            Canvas.ForceUpdateCanvases();
            var backdrop = Get<Button>("_helpBackdrop");
            var canvas = Get<GameObject>("_canvas");
            var point = new Vector2(Screen.width * .1f, Screen.height * .5f);
            var hits = new List<RaycastResult>();
            canvas.GetComponent<GraphicRaycaster>().Raycast(new PointerEventData(EventSystem.current) {position = point}, hits);
            Assert.IsTrue(hits.Exists(h => h.gameObject == backdrop.gameObject));
            Call("BeginSelectionPress", Time.frameCount, (Vector3)point, true);
            Call("HandleEscape", Time.frameCount);
            Assert.AreEqual(1, _demo.SelectionCount);
            Assert.IsTrue(backdrop.gameObject.activeInHierarchy, "Dismissal must still block Core input until LateUpdate.");
            Assert.IsFalse((bool)Call("CanSelectOnRelease", (Vector3)point, false));
            Navigate(point, 0, 0);
            Assert.AreEqual(-1, Get<int>("_navigationButton"));
            Call("ApplyDeferredPanelClose");
            hits.Clear();
            canvas.GetComponent<GraphicRaycaster>().Raycast(new PointerEventData(EventSystem.current) {position = point}, hits);
            Assert.IsFalse(hits.Exists(h => h.gameObject == backdrop.gameObject));
            Call("HandleEscape", Time.frameCount + 1);
            Assert.AreEqual(0, _demo.SelectionCount);
        }

        [Test]
        public void HelpAndSizeRequireSeparatePressesAndDisableClearsHelp()
        {
            Assert.IsTrue(Get<Button>("_helpToggleButton").interactable, "Help works without a selection.");
            Call("ToggleHelpPanel");
            Call("ToggleSizePanel");
            Assert.IsFalse(Get<bool>("_sizePanelOpen"));
            Assert.IsTrue(Get<bool>("_helpPanelOpen"));
            Call("ApplyDeferredPanelClose");
            Call("ToggleSizePanel");
            Assert.IsTrue(Get<bool>("_sizePanelOpen"));
            Assert.IsFalse(Get<bool>("_helpPanelOpen"));
            Call("ToggleHelpPanel");
            Assert.IsFalse(Get<bool>("_helpPanelOpen"));
            Call("ApplyDeferredPanelClose");
            Call("ToggleHelpPanel");
            _demo.enabled = false;
            Assert.IsFalse(Get<Button>("_helpBackdrop").gameObject.activeInHierarchy);
            Assert.IsFalse(Get<RectTransform>("_helpCard").gameObject.activeSelf);
            _demo.enabled = true;
            Assert.IsFalse(Get<bool>("_helpPanelOpen"));
            Select(); _demo.ActiveHandle.InteractionStart();
            Call("ToggleHelpPanel"); Call("RefreshUi");
            Assert.IsFalse(Get<bool>("_helpPanelOpen"));
            Assert.IsFalse(Get<Button>("_helpToggleButton").interactable);
        }

        [UnityTest]
        public IEnumerator HelpTextFitsAndSceneGuidanceLivesInsideCards()
        {
            Call("ToggleHelpPanel");
            yield return null;
            Canvas.ForceUpdateCanvases();
            foreach (var message in new[] {"Select a wall or corner and arrange your layout.",
                "Drag the handle to place this module.", "Scene reset. Handle size preference is kept."})
            {
                Set("_notice", message); Call("RefreshUi");
                foreach (var label in Get<RectTransform>("_helpCard").GetComponentsInChildren<Text>())
                    Assert.LessOrEqual(label.preferredHeight, label.rectTransform.rect.height + .1f, label.text);
            }
            var status = Get<Text>("_status");
            Assert.IsTrue(status.transform.IsChildOf(Get<RectTransform>("_helpCard")));
            Assert.IsNull(Get<GameObject>("_canvas").transform.Find("Shortcuts"));
            var strip = Get<RectTransform>("_contextBar");
            Assert.GreaterOrEqual(Get<RectTransform>("_helpCard").anchoredPosition.y,
                strip.anchoredPosition.y + 72 + 12);
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
