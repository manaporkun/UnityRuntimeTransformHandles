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

namespace TransformHandles.Samples.LevelEditor.Tests
{
    public class LevelEditorDemoTests
    {
        private GameObject _root;
        private LevelEditorDemo _demo;
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
            _demo = _root.AddComponent<LevelEditorDemo>();
            _camera = new GameObject("Sample camera").AddComponent<Camera>();
            _targets = new Transform[2];
            for (var i = 0; i < 2; i++)
            {
                _targets[i] = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                _targets[i].position = new Vector3(i * 3, .5f, 0);
            }
            _originalMaterials = _targets.Select(t => t.GetComponent<Renderer>().sharedMaterial).ToArray();
#if UNITY_EDITOR
            var settingsPaths = UnityEditor.AssetDatabase.FindAssets("LevelEditorSettings t:TransformHandleSettings", new[] {"Assets"});
            Assert.AreEqual(1, settingsPaths.Length, "Import this sample exactly once before testing.");
            _settings = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<TransformHandleSettings>(UnityEditor.AssetDatabase.GUIDToAssetPath(settingsPaths[0])));
#else
            _settings = ScriptableObject.CreateInstance<TransformHandleSettings>();
            typeof(TransformHandleSettings).GetField("enableShortcuts", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_settings, false);
#endif
            _material = new Material(Shader.Find("TransformHandles/Samples/LevelEditorOpaque"));
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

        private void Set(string name, object value) => typeof(LevelEditorDemo).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_demo, value);
        private T Get<T>(string name) => (T)typeof(LevelEditorDemo).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_demo);
        private object Call(string name, params object[] args) => typeof(LevelEditorDemo).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_demo, args);
        private void Select() => _demo.SelectTarget(_targets[0], false);

        private void Navigate(Vector3 position, int down = -1, int held = -1, bool ui = false, bool editing = false, float scroll = 0)
        {
            Call("ProcessNavigation", position, down == 0, down == 1, down == 2,
                held == 0, held == 1, held == 2, ui, editing, scroll, Time.frameCount);
        }

        [Test]
        public void LocalGroupHelpKeepsNamesLiteralAndDoesNotClaimDisabledSnapping()
        {
            _targets[1].name = "<b>Reference</b>";
            Select(); _demo.SelectTarget(_targets[1], true); _demo.SetMode(HandleType.Rotation); _demo.ToggleSpace();
            Call("RefreshUi");
            var text = Get<Text>("_snapLabel");
            Assert.IsFalse(text.supportRichText);
            StringAssert.Contains("<b>Reference</b>", text.text);
            StringAssert.DoesNotContain("15-degree", text.text);
            _demo.ToggleSnapping(); Call("RefreshUi");
            StringAssert.Contains("15-degree", text.text);
        }

        [UnityTest]
        public IEnumerator DestroyedReferencePreservesSurvivingSelection()
        {
            Select(); _demo.SelectTarget(_targets[1], true); _demo.SetMode(HandleType.Rotation); _demo.ToggleSpace();
            Object.Destroy(_targets[1].gameObject);
            yield return null; yield return null;
            Assert.AreEqual(1, _demo.SelectionCount);
            Assert.IsNotNull(_demo.ActiveHandle);
            Assert.AreEqual(HandleType.Rotation, _demo.ActiveHandle.Type);
            Assert.Contains(_targets[0], _demo.ActiveHandle.Targets.ToArray());
        }

        [UnityTest]
        public IEnumerator InvalidGroupFactorRestoresEveryTransformAndShowsError()
        {
            Select(); _demo.SelectTarget(_targets[1], true); _demo.SetMode(HandleType.Scale);
            var handle = _demo.ActiveHandle;
            var positions = _targets.Select(t => t.position).ToArray();
            handle.InteractionStart();
            foreach (var target in _targets) { target.position += Vector3.one; target.localScale *= 2; }
            handle.Pivot.localScale = -Vector3.one;
            handle.InteractionStay();
            for (var i = 0; i < 2; i++)
            {
                Assert.AreEqual(positions[i], _targets[i].position);
                Assert.AreEqual(Vector3.one, _targets[i].localScale);
            }
            yield return null;
            Call("RefreshUi");
            StringAssert.Contains("cancelled", Get<Text>("_status").text);
            Assert.IsNotNull(_demo.ActiveHandle);
        }

        [Test]
        public void MembershipRefreshesUniformControlsAndPartialMasksDisableScaling()
        {
            Select(); _demo.SelectTarget(_targets[1], true); _demo.SetMode(HandleType.Scale);
            var handle = _demo.ActiveHandle;
            handle.Axes = HandleAxes.X;
            Assert.IsNull(handle.GetComponentInChildren<ScaleGlobal>());
            Assert.IsNull(handle.GetComponentInChildren<ScaleAxis>());
            handle.Axes = HandleAxes.XYZ;
            _manager.RemoveTarget(_targets[1], handle);
            Assert.IsNotNull(handle.GetComponentInChildren<ScaleAxis>());
            _manager.AddTarget(_targets[1], handle);
            Assert.IsNull(handle.GetComponentInChildren<ScaleAxis>());
            Assert.IsNotNull(handle.GetComponentInChildren<ScaleGlobal>());
        }

        [Test]
        public void LeftClickSelectsButOrbitReturningToOriginNeverSelects()
        {
            Call("BeginSelectionPress", Time.frameCount, Vector3.zero, false, false);
            Navigate(Vector3.zero, 0, 0); Navigate(Vector3.zero);
            Assert.IsTrue((bool)Call("CanSelectOnRelease", Vector3.zero, false));
            var yaw = Get<float>("_yaw");
            Call("BeginSelectionPress", Time.frameCount, Vector3.zero, false, false);
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

        [TestCase(true)]
        [TestCase(false)]
        public void UiAndTextEntryCannotStartNavigationOrZoom(bool ui)
        {
            var focus = Get<Vector3>("_cameraFocus"); var distance = Get<float>("_distance");
            Navigate(Vector3.zero, 1, 1, ui, !ui); Navigate(new Vector3(80, 40), held: 1);
            Navigate(Vector3.zero, ui: ui, editing: !ui, scroll: 5);
            Assert.AreEqual(focus, Get<Vector3>("_cameraFocus"));
            Assert.AreEqual(distance, Get<float>("_distance"));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void HandleGestureCannotBecomeOrbitEvenWhenItEndsInStartFrame(bool managerFirst)
        {
            Select(); var yaw = Get<float>("_yaw");
            if (managerFirst) _demo.ActiveHandle.InteractionStart();
            Call("BeginSelectionPress", Time.frameCount, Vector3.zero, false, false);
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

        [Test]
        public void InvalidExternalScaleAtDragStartDoesNotResetPositionOrRotation()
        {
            Select(); _demo.SetMode(HandleType.Scale);
            var position = new Vector3(3, 2, 1); var rotation = Quaternion.Euler(10, 20, 30);
            _targets[0].SetPositionAndRotation(position, rotation);
            _targets[0].localScale = Vector3.one * 5;
            _demo.ActiveHandle.InteractionStart();
            Assert.IsNull(_demo.ActiveHandle); Assert.IsFalse(_demo.IsDragging);
            Assert.AreEqual(position, _targets[0].position);
            Assert.That(Quaternion.Angle(rotation, _targets[0].rotation), Is.LessThan(.01f));
            _demo.ResetLayout(); Assert.AreEqual(Vector3.one, _targets[0].localScale);
        }

        [Test]
        public void PreselectedModeBuildsTheMatchingGizmo()
        {
            _demo.SetMode(HandleType.Rotation); Select();
            Assert.AreEqual(HandleType.Rotation, _demo.ActiveHandle.Type);
            Assert.IsNotNull(_demo.ActiveHandle.GetComponentInChildren<RotationAxis>());
        }

        [Test]
        public void GroupAxisSnapsPivotAndPreservesUnevenOffsets()
        {
            _targets[0].position = new Vector3(.13f, .5f, .27f);
            _targets[1].position = new Vector3(3.42f, .5f, -.19f);
            var offset = _targets[1].position - _targets[0].position;
            Select(); _demo.SelectTarget(_targets[1], true); _demo.ToggleSnapping();
            var handle = _demo.ActiveHandle;
            handle.ChangeAxes(HandleAxes.X);
            var axis = handle.GetComponentInChildren<PositionAxis>();
            var start = handle.Pivot.position;
            axis.StartInteraction(start);
            _camera.transform.position += Vector3.right * 1.2f;
            axis.Interact(Vector3.zero);
            var delta = handle.Pivot.position - start;
            Assert.Greater(delta.magnitude, .5f);
            Assert.That(handle.Pivot.position.x * 2, Is.EqualTo(Mathf.Round(handle.Pivot.position.x * 2)).Within(.0001f));
            _manager.UpdateGroupPosition(handle.Pivot.GetComponent<Ghost>(), delta);
            Assert.That(Vector3.Distance(offset, _targets[1].position - _targets[0].position), Is.LessThan(.0001f));
            axis.EndInteraction();
        }

        [UnityTest]
        public IEnumerator InvalidFinalScaleIsRolledBackOnRelease()
        {
            Select(); _demo.SetMode(HandleType.Scale); var handle = _demo.ActiveHandle;
            handle.InteractionStart(); _targets[0].localScale = Vector3.one * 5;
            handle.InteractionEnd();
            Assert.IsNull(_demo.ActiveHandle, "The sample must remove the invalid gesture handle immediately.");
            Assert.AreEqual(Vector3.one, _targets[0].localScale);
            yield return null;
            Assert.AreEqual(HandleType.Scale, _demo.ActiveHandle.Type);
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator ShippedSceneHasValidReferencesAndEnablesController()
        {
            var paths = UnityEditor.AssetDatabase.FindAssets("t:Scene", new[] {"Assets"})
                .Select(UnityEditor.AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith("/LevelEditor.unity")).ToArray();
            Assert.AreEqual(1, paths.Length, "Import the Level Editor sample exactly once before testing.");
            _loadedScene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(paths[0],
                new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Additive));
            yield return null;
            var controller = _loadedScene.GetRootGameObjects().Select(go => go.GetComponent<LevelEditorDemo>()).Single(d => d != null);
            Assert.IsTrue(controller.enabled);
            var shader = Shader.Find("TransformHandles/Samples/LevelEditorOpaque");
            Assert.IsNotNull(shader);
            Assert.IsFalse(UnityEditor.ShaderUtil.ShaderHasError(shader));
        }
#endif

        [Test]
        public void GroupCommandsPreserveModeAndLocalReferenceAcrossScale()
        {
            _targets[1].rotation = Quaternion.Euler(10, 30, 45);
            Select(); _demo.SetMode(HandleType.Rotation); _demo.ToggleSpace();
            _demo.SelectTarget(_targets[1], true);
            Assert.AreEqual(HandleType.Rotation, _demo.ActiveHandle.Type);
            Assert.AreEqual(_targets[1], _demo.ActiveHandle.LocalSpaceReference);
            Assert.That(Quaternion.Angle(_targets[1].rotation, _demo.ActiveHandle.Pivot.rotation), Is.LessThan(.01f));
            _demo.SetMode(HandleType.Scale); _demo.ToggleSpace();
            Assert.AreEqual(HandleType.Scale, _demo.ActiveHandle.Type);
            Assert.IsTrue(_demo.ActiveHandle.UniformGroupScale);
            Assert.IsNull(_demo.ActiveHandle.GetComponentInChildren<ScaleAxis>());
            Assert.IsNotNull(_demo.ActiveHandle.GetComponentInChildren<ScaleGlobal>());
            _demo.SetMode(HandleType.Rotation);
            Assert.AreEqual(Space.Self, _demo.ActiveHandle.Space);
            _demo.ToggleSpace();
            Assert.AreEqual(Space.World, _demo.ActiveHandle.Space);
            _demo.SetMode(HandleType.Scale); _demo.SetMode(HandleType.Position);
            Assert.AreEqual(Space.World, _demo.ActiveHandle.Space);
            Assert.IsFalse(_manager.Settings.EnableShortcuts);
        }

        [Test]
        public void ScaleUsesLocalSpaceAndLocalMoveHasNoGrid()
        {
            Select(); _demo.ToggleSnapping();
            Assert.AreEqual(Vector3.one * .5f, _demo.ActiveHandle.PositionSnap);
            Assert.AreEqual(SnappingType.Absolute, _demo.ActiveHandle.SnappingType);
            _demo.ToggleSpace(); _demo.ToggleSnapping();
            Assert.AreEqual(Vector3.zero, _demo.ActiveHandle.PositionSnap);
            _demo.SetMode(HandleType.Scale); _demo.ToggleSpace();
            Assert.AreEqual(Space.Self, _demo.ActiveHandle.Space);
            Assert.AreEqual(Vector3.one * .1f, _demo.ActiveHandle.ScaleSnap);
        }

        [Test]
        public void ResetRestoresAllTransforms()
        {
            var start = _targets[0].position;
            Select(); _targets[0].SetPositionAndRotation(Vector3.one * 8, Quaternion.Euler(15,20,30));
            _targets[0].localScale = Vector3.one * 2;
            _demo.ResetLayout();
            Assert.AreEqual(start, _targets[0].position); Assert.AreEqual(Quaternion.identity, _targets[0].rotation);
            Assert.AreEqual(Vector3.one, _targets[0].localScale); Assert.AreEqual(0, _demo.SelectionCount);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DragReleaseNeverBecomesSelectionRegardlessOfUpdateOrder(bool managerFirst)
        {
            Select();
            if (managerFirst) _demo.ActiveHandle.InteractionStart();
            Call("BeginSelectionPress", Time.frameCount, Vector3.zero, false, false);
            if (!managerFirst) _demo.ActiveHandle.InteractionStart();
            _demo.ActiveHandle.InteractionEnd();
            Assert.IsFalse((bool)Call("CanSelectOnRelease", Vector3.zero, false));
        }

        [Test]
        public void UiOriginAndUiReleaseBlockSelection()
        {
            Call("BeginSelectionPress", Time.frameCount, Vector3.zero, true, false);
            Assert.IsFalse((bool)Call("CanSelectOnRelease", Vector3.zero, false));
            Call("BeginSelectionPress", Time.frameCount, Vector3.zero, false, false);
            Assert.IsFalse((bool)Call("CanSelectOnRelease", Vector3.zero, true));
            Assert.IsTrue((bool)Call("CanSelectOnRelease", Vector3.zero, false));
        }

        [Test]
        public void DragBlocksCommandsAndClearKeepsLastTransform()
        {
            Select(); var handle = _demo.ActiveHandle; handle.InteractionStart();
            _targets[0].position = Vector3.one * 2;
            _demo.SetMode(HandleType.Rotation); _demo.ToggleSpace(); _demo.ToggleSnapping(); _demo.ResetLayout();
            Assert.AreEqual(HandleType.Position, handle.Type); Assert.AreEqual(Space.World, handle.Space);
            Assert.AreEqual(Vector3.zero, handle.PositionSnap);
            _demo.ClearSelection();
            Assert.AreEqual(Vector3.one * 2, _targets[0].position); Assert.AreEqual(0, _demo.SelectionCount);
            Assert.IsFalse(_demo.IsDragging);
        }

        [UnityTest]
        public IEnumerator InvalidScaleRestoresDragStartThenRebuildsNextFrame()
        {
            Select(); _demo.SetMode(HandleType.Scale); var old = _demo.ActiveHandle;
            var position = _targets[0].position; old.InteractionStart();
            _targets[0].position = Vector3.one * 9; _targets[0].localScale = new Vector3(-1, 2, 3);
            old.InteractionStay();
            Assert.IsNull(_demo.ActiveHandle); Assert.AreEqual(Vector3.one, _targets[0].localScale);
            Assert.AreEqual(position, _targets[0].position);
            yield return null;
            Assert.IsNotNull(_demo.ActiveHandle); Assert.AreNotSame(old, _demo.ActiveHandle);
            Assert.AreEqual(HandleType.Scale, _demo.ActiveHandle.Type);
            Assert.IsNotNull(_demo.ActiveHandle.GetComponentInChildren<ScaleAxis>());
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
                Assert.AreSame(_originalMaterials[i], _targets[i].GetComponent<Renderer>().sharedMaterial);
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
        public void ToolbarReflectsEffectiveSpaceSnapAndGroupRestrictions()
        {
            Select();
            _demo.ToggleSnapping(); Call("RefreshUi");
            Assert.AreEqual("Snap 0.5", Get<Button>("_snapButton").GetComponentInChildren<Text>().text);
            _demo.ToggleSpace(); Call("RefreshUi");
            Assert.AreEqual("Local", Get<Button>("_spaceButton").GetComponentInChildren<Text>().text);
            Assert.AreEqual("Snap off", Get<Button>("_snapButton").GetComponentInChildren<Text>().text);
            Assert.IsFalse(Get<Button>("_snapButton").interactable);
            _demo.SetMode(HandleType.Scale); Call("RefreshUi");
            Assert.IsFalse(Get<Button>("_spaceButton").interactable);
            _demo.SelectTarget(_targets[1], true); Call("RefreshUi");
            Assert.IsTrue(Get<Button>("_rotateButton").interactable);
            Assert.IsTrue(Get<Button>("_scaleButton").interactable);
            Assert.AreEqual("Uniform", Get<Button>("_spaceButton").GetComponentInChildren<Text>().text);
        }

        [Test]
        public void ToolbarAndSelectionCardDoNotOverlapAtSupportedSizes()
        {
            var canvas = Get<GameObject>("_canvas");
            var scaler = canvas.GetComponent<CanvasScaler>();
            Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
            Assert.AreEqual(new Vector2(1280, 720), scaler.referenceResolution);
            scaler.enabled = false;
            var root = (RectTransform)canvas.transform;
            foreach (var size in new[] {new Vector2(1440, 900), new Vector2(1280, 720), new Vector2(960, 640), new Vector2(1920, 1080), new Vector2(3840, 2160)})
            {
                // Match the scaler's logarithmic width/height interpolation at match = 0.5.
                var factor = Mathf.Sqrt(size.x / 1280f * size.y / 720f);
                root.sizeDelta = size / factor;
                Call("UpdateUiLayout"); Canvas.ForceUpdateCanvases();
                var toolbar = Get<RectTransform>("_toolbar");
                var card = Get<RectTransform>("_selectionCard");
                Assert.Greater(-card.anchoredPosition.y, -toolbar.anchoredPosition.y + toolbar.sizeDelta.y);
                var buttons = Get<List<Button>>("_buttons");
                foreach (var button in buttons) Assert.GreaterOrEqual(((RectTransform)button.transform).rect.height * factor, 44);
            }
        }

        [Test]
        public void EnlargedTextFitsLongestStatusAndSnapDescriptions()
        {
            Select();
            _demo.SelectTarget(_targets[1], true);
            Call("RefreshUi");
            foreach (var name in new[] {"_status", "_snapLabel", "_modeLabel"})
            {
                var label = Get<Text>(name);
                Assert.LessOrEqual(label.preferredHeight, label.rectTransform.rect.height + .1f, label.text);
                Assert.GreaterOrEqual(label.fontSize, 20);
            }
            _demo.ResetLayout(); Call("RefreshUi");
            var status = Get<Text>("_status");
            Assert.LessOrEqual(status.preferredHeight, status.rectTransform.rect.height + .1f, status.text);
        }

        [Test]
        public void SizePopoverWorksWithoutSelectionAndPreservesPreferenceAfterClose()
        {
            Assert.IsFalse(Get<RectTransform>("_contextBar").gameObject.activeSelf);
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
            Call("BeginSelectionPress", Time.frameCount, (Vector3)point, true, false);
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
        public void LongSelectionNamesFitAndErrorsSurviveAnEmptySelection()
        {
            foreach (var selectionName in new[] {"A very long selection name that should stay inside the compact context bar", new string('W', 120), "<size=100>Wide name</size>\nsecond line"})
            {
                _targets[0].name = selectionName;
                Select(); Call("RefreshUi"); Canvas.ForceUpdateCanvases();
                var name = Get<Text>("_selectionLabel");
                Assert.IsFalse(name.supportRichText);
                Assert.LessOrEqual(name.preferredHeight, name.rectTransform.rect.height + .1f, selectionName);
            }
            _demo.ClearSelection();
            Set("_notice", "A prop has an unsupported scale. Reset course to recover.");
            Call("RefreshUi");
            Assert.IsFalse(Get<RectTransform>("_contextBar").gameObject.activeSelf);
            Assert.IsTrue(Get<Text>("_status").gameObject.activeInHierarchy);
            StringAssert.Contains("unsupported scale", Get<Text>("_status").text);
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
        public void HandleSizeIsAppliedBeforeSelectionReturnsAndDoesNotChangeTargets()
        {
            var initial = _targets[0].localScale;
            Select();
            var handle = _demo.ActiveHandle;
            Assert.IsTrue(handle.AutoScale);
            Assert.AreEqual(192, handle.AutoScaleSizeInPixels);
            var distance = Mathf.Abs(GeometryUtility.CalculateFrustumPlanes(_camera)[4].GetDistanceToPoint(handle.transform.position));
            var expected = 2 * Mathf.Tan(_camera.fieldOfView * .5f * Mathf.Deg2Rad) * 192 / Screen.height * distance;
            Assert.That(handle.transform.localScale.x, Is.EqualTo(expected).Within(.0001f));
            Assert.AreEqual(initial, _targets[0].localScale);
        }

        [UnityTest]
        public IEnumerator HandleSizeSurvivesSelectionModesGroupsResetDisableAndDeferredRebuild()
        {
            Get<Slider>("_sizeSlider").value = 320;
            Select(); Assert.AreEqual(320, _demo.ActiveHandle.AutoScaleSizeInPixels);
            _demo.SetMode(HandleType.Rotation); Assert.AreEqual(320, _demo.ActiveHandle.AutoScaleSizeInPixels);
            _demo.SelectTarget(_targets[1], true); Assert.AreEqual(320, _demo.ActiveHandle.AutoScaleSizeInPixels);
            _demo.ResetLayout(); Select(); Assert.AreEqual(320, _demo.ActiveHandle.AutoScaleSizeInPixels);
            _demo.enabled = false; _demo.enabled = true;
            Select(); Assert.AreEqual(320, _demo.ActiveHandle.AutoScaleSizeInPixels);
            _demo.SetMode(HandleType.Scale);
            _demo.ActiveHandle.InteractionStart();
            _targets[0].localScale = Vector3.one * 5;
            Call("OnDrag", _demo.ActiveHandle);
            yield return null;
            Assert.AreEqual(320, _demo.ActiveHandle.AutoScaleSizeInPixels);
            Assert.IsTrue(_demo.ActiveHandle.AutoScale);
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
                Call("BeginSelectionPress", 100, (Vector3)point, true, false);
                Assert.IsFalse((bool)Call("CanSelectOnRelease", (Vector3)point, true));
            }
        }

        [Test]
        public void PanelBlocksRaycastsButHelpTextDoesNot()
        {
            Canvas.ForceUpdateCanvases();
            var canvas = Get<GameObject>("_canvas");
            var panel = canvas.transform.Find("Panel").GetComponent<RectTransform>();
            var point = RectTransformUtility.WorldToScreenPoint(null, panel.TransformPoint(panel.rect.center));
            var hits = new List<RaycastResult>();
            canvas.GetComponent<GraphicRaycaster>().Raycast(new PointerEventData(EventSystem.current) {position = point}, hits);
            Assert.IsTrue(hits.Exists(h => h.gameObject == panel.gameObject));
            foreach (var label in canvas.GetComponentsInChildren<Text>()) Assert.IsFalse(label.raycastTarget);
        }
    }
}
