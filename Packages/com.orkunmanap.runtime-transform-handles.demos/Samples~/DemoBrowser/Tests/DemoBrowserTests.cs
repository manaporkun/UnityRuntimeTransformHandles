using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace TransformHandles.Samples.DemoBrowser.Tests
{
    public class DemoBrowserTests : IPrebuildSetup, IPostBuildCleanup
    {
        [Test]
        public void ResolvesFullPathAndOmitsMissingScenes()
        {
            var paths = new[] { "Assets/Custom/ModularBuilding.unity", "Assets/Other/LevelEditor.unity" };
            Assert.AreEqual(paths[0], DemoBrowser.ResolveScenePath(paths, "ModularBuilding"));
            Assert.IsNull(DemoBrowser.ResolveScenePath(paths, "RuntimeTransformHandlesDemo"));
        }

        [Test]
        public void RejectsAmbiguousSceneNames()
        {
            Assert.Throws<InvalidOperationException>(() => DemoBrowser.ResolveScenePath(
                new[] { "Assets/One/LevelEditor.unity", "Assets/Two/LevelEditor.unity" }, "LevelEditor"));
        }

#if UNITY_EDITOR
        private const string BackupKey = "TransformHandles.DemoBrowser.PreviousBuildScenes";
        void IPrebuildSetup.Setup()
        {
            var previous = UnityEditor.EditorBuildSettings.scenes;
            UnityEditor.SessionState.SetString(BackupKey, string.Join("\n", previous.Select(scene => (scene.enabled ? "1" : "0") + scene.path)));
            UnityEditor.EditorBuildSettings.scenes = new[] { "DemoBrowser", "ModularBuilding", "LevelEditor", "RuntimeTransformHandlesDemo", "ProductConfigurator" }
                .Select(FindScene).Where(path => !string.IsNullOrEmpty(path))
                .Select(path => new UnityEditor.EditorBuildSettingsScene(path, true)).ToArray();
        }

        void IPostBuildCleanup.Cleanup()
        {
            var previous = UnityEditor.SessionState.GetString(BackupKey, "");
            UnityEditor.EditorBuildSettings.scenes = previous.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => new UnityEditor.EditorBuildSettingsScene(line.Substring(1), line[0] == '1')).ToArray();
            UnityEditor.SessionState.EraseString(BackupKey);
        }
        private string _browserPath;

        [SetUp]
        public void PrepareScenes()
        {
            _browserPath = FindScene("DemoBrowser");
        }

        private static string FindScene(string name)
        {
            return UnityEditor.AssetDatabase.FindAssets(name + " t:Scene", new[] { "Assets" })
                .Select(UnityEditor.AssetDatabase.GUIDToAssetPath)
                .SingleOrDefault(path => System.IO.Path.GetFileNameWithoutExtension(path) == name);
        }

        [UnityTest]
        public IEnumerator GalleryListsOnlyScenesIncludedInBuild()
        {
            yield return SceneManager.LoadSceneAsync(_browserPath);
            yield return null;
            var browser = UnityEngine.Object.FindObjectOfType<DemoBrowser>();
            Assert.NotNull(browser);
            var root = browser.GetComponentInChildren<UIDocument>().rootVisualElement;
            var paths = Enumerable.Range(0, SceneManager.sceneCountInBuildSettings).Select(SceneUtility.GetScenePathByBuildIndex).ToArray();
            int expected = new[] { "ModularBuilding", "LevelEditor", "RuntimeTransformHandlesDemo", "ProductConfigurator" }
                .Count(name => DemoBrowser.ResolveScenePath(paths, name) != null);
            Assert.AreEqual(expected, root.Q("cards").childCount);
            if (expected == 0)
                StringAssert.Contains("No demo scenes", root.Q<Label>("status").text);
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsOfType<EventSystem>().Length);
        }

        [UnityTest]
        public IEnumerator RepeatedTransitionsReleaseManagersAndKeepOneEventSystem()
        {
            var names = new[] { "ModularBuilding", "LevelEditor", "RuntimeTransformHandlesDemo", "ProductConfigurator" };
            var paths = names.Select(FindScene).ToArray();
            if (paths.Any(string.IsNullOrEmpty)) Assert.Ignore("Import all four optional demos to exercise scene transitions.");
            yield return SceneManager.LoadSceneAsync(_browserPath);
            yield return null;
            var browser = UnityEngine.Object.FindObjectOfType<DemoBrowser>();
            foreach (var path in paths.Concat(new[] { paths[0] }))
            {
                browser.OpenDemo(path);
                // A repeated click during loading must not queue another scene change.
                browser.OpenDemo(path);
                yield return WaitForScene(path, browser);
                Assert.AreEqual(1, UnityEngine.Object.FindObjectsOfType<DemoBrowser>().Length);
                Assert.AreEqual(1, UnityEngine.Object.FindObjectsOfType<EventSystem>().Length);
                Assert.AreEqual(1, UnityEngine.Object.FindObjectsOfType<TransformHandleManager>().Length);
                Assert.NotNull(Camera.main);
                // Leave with a selected target and an active Core interaction, not an empty scene.
                var controller = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>()
                    .First(item => item.GetType().Name == "ModularBuildingDemo" || item.GetType().Name == "LevelEditorDemo" || item.GetType().Name == "HandleDemo" || item.GetType().Name == "ProductConfiguratorDemo");
                var type = controller.GetType();
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
                var field = type.GetField("targets", flags) ?? type.GetField("modules", flags);
                if (field != null)
                {
                    var target = ((Transform[])field.GetValue(controller))[0];
                    var select = type.GetMethod("SelectTarget", flags);
                    select.Invoke(controller, select.GetParameters().Length == 1 ? new object[] { target } : new object[] { target, false });
                }
                else if (type.Name == "ProductConfiguratorDemo")
                {
                    var parts = (System.Array)type.GetField("parts", flags).GetValue(controller);
                    var part = parts.GetValue(0);
                    var target = (Transform)part.GetType().GetField("root").GetValue(part);
                    type.GetMethod("SelectTarget", flags).Invoke(controller, new object[] { target });
                }
                else type.GetMethod("SelectSingle", flags).Invoke(controller, new object[] { GameObject.Find("Target_0").transform });
                yield return null;
                var manager = UnityEngine.Object.FindObjectOfType<TransformHandleManager>();
                var handle = UnityEngine.Object.FindObjectOfType<Handle>();
                Assert.NotNull(handle);
                HandleBase axis = handle.GetComponentInChildren<PositionPlane>();
                if (axis == null) axis = handle.GetComponentInChildren<HandleBase>();
                Assert.IsTrue((bool)typeof(TransformHandleManager).GetMethod("BeginInteraction", flags)
                    .Invoke(manager, new object[] { axis, axis.transform.position }));
                browser.ReturnToGallery();
                yield return WaitForScene(_browserPath, browser);
                Assert.AreEqual(0, UnityEngine.Object.FindObjectsOfType<TransformHandleManager>().Length,
                    "A departed demo must release its persistent manager.");
                Assert.AreEqual(1, UnityEngine.Object.FindObjectsOfType<EventSystem>().Length);
            }
        }

        [UnityTest]
        public IEnumerator RecreatesBorrowedEventSystemAfterItsSceneUnloads()
        {
            var demoPath = FindScene("ModularBuilding");
            if (string.IsNullOrEmpty(demoPath)) Assert.Ignore("Import Modular Building for the borrowed-system regression.");
            var temporaryScene = SceneManager.CreateScene("Borrowed Input Host");
            var existing = new GameObject("Borrowed EventSystem", typeof(EventSystem));
            SceneManager.MoveGameObjectToScene(existing, temporaryScene);
#if ENABLE_INPUT_SYSTEM
            existing.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
#else
            existing.AddComponent<StandaloneInputModule>();
#endif
            yield return SceneManager.LoadSceneAsync(_browserPath, LoadSceneMode.Additive);
            SceneManager.SetActiveScene(SceneManager.GetSceneByPath(_browserPath));
            yield return null;
            var browser = UnityEngine.Object.FindObjectOfType<DemoBrowser>();
            browser.OpenDemo(demoPath);
            yield return WaitForScene(demoPath, browser);
            Assert.IsTrue(existing == null);
            browser.ReturnToGallery();
            yield return WaitForScene(_browserPath, browser);
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsOfType<EventSystem>().Length);
            Assert.NotNull(EventSystem.current.currentInputModule);
        }

        [UnityTest]
        public IEnumerator RestoresBorrowedManagerWithoutDestroyingItsOwner()
        {
            var paths = new[] { "ModularBuilding", "LevelEditor", "RuntimeTransformHandlesDemo", "ProductConfigurator" }.Select(FindScene).ToArray();
            if (paths.Any(string.IsNullOrEmpty)) Assert.Ignore("Import all workspaces for borrowed-manager coverage.");
            var owner = new GameObject("Borrowed manager camera");
            UnityEngine.Object.DontDestroyOnLoad(owner);
            var camera = owner.AddComponent<Camera>();
            var manager = TransformHandleManager.Instance;
            var settings = TransformHandleSettings.CreateDefault();
            manager.MainCamera = camera;
            manager.Settings = settings;
            manager.BlockWhenPointerOverUI = false;
            try
            {
                yield return SceneManager.LoadSceneAsync(_browserPath);
                yield return null;
                var browser = UnityEngine.Object.FindObjectOfType<DemoBrowser>();
                foreach (var path in paths)
                {
                    browser.OpenDemo(path);
                    yield return WaitForScene(path, browser);
                    Assert.AreSame(manager, UnityEngine.Object.FindObjectOfType<TransformHandleManager>());
                    browser.ReturnToGallery();
                    yield return WaitForScene(_browserPath, browser);
                    Assert.IsTrue(manager != null);
                    Assert.AreSame(camera, manager.MainCamera);
                    Assert.AreSame(settings, manager.Settings);
                    Assert.IsFalse(manager.BlockWhenPointerOverUI);
                    Assert.IsTrue(manager.enabled);
                }
            }
            finally
            {
                if (manager != null) UnityEngine.Object.Destroy(manager.gameObject);
                UnityEngine.Object.Destroy(owner);
                UnityEngine.Object.Destroy(settings);
            }
            yield return null;
        }

#if ENABLE_INPUT_SYSTEM
        [UnityTest]
        public IEnumerator NavigationClaimsSameFramePressAndDragOutsideWithShowcaseGuardOff()
        {
            var path = FindScene("RuntimeTransformHandlesDemo");
            if (string.IsNullOrEmpty(path)) Assert.Ignore("Import the showcase for navigation input regression.");
            yield return SceneManager.LoadSceneAsync(_browserPath);
            yield return null;
            var browser = UnityEngine.Object.FindObjectOfType<DemoBrowser>();
            browser.OpenDemo(path);
            yield return WaitForScene(path, browser);
            var manager = UnityEngine.Object.FindObjectOfType<TransformHandleManager>();
            manager.BlockWhenPointerOverUI = false;
            var controller = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>().Single(item => item.GetType().Name == "HandleDemo");
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            controller.GetType().GetField("_blockTest", flags).SetValue(controller, false);
            controller.GetType().GetMethod("SelectSingle", flags).Invoke(controller, new object[] { GameObject.Find("Target_0").transform });
            yield return null;
            var axis = UnityEngine.Object.FindObjectOfType<Handle>().GetComponentInChildren<PositionPlane>();
            Assert.IsTrue((bool)typeof(TransformHandleManager).GetMethod("BeginInteraction", flags).Invoke(manager, new object[] { axis, axis.transform.position }));
            var root = UnityEngine.Object.FindObjectsOfType<UIDocument>()
                .Single(doc => doc.rootVisualElement.Q<Button>("demo-gallery") != null).rootVisualElement;
            var center = root.Q("demo-gallery").worldBound.center;
            float scale = Screen.width / root.resolvedStyle.width;
            var point = new Vector2(center.x * scale, Screen.height - center.y * scale);
            var original = UnityEngine.InputSystem.InputSystem.settings;
            var settings = UnityEngine.Object.Instantiate(original);
            settings.updateMode = UnityEngine.InputSystem.InputSettings.UpdateMode.ProcessEventsManually;
            settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            UnityEngine.InputSystem.InputSystem.settings = settings;
            var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            try
            {
                foreach (var button in new[] { UnityEngine.InputSystem.LowLevel.MouseButton.Left, UnityEngine.InputSystem.LowLevel.MouseButton.Middle })
                {
                    var browserUpdate = typeof(DemoBrowser).GetMethod("Update", flags);
                    var demoUpdate = controller.GetType().GetMethod("Update", flags);
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = new Vector2(100, 100) });
                    UnityEngine.InputSystem.InputSystem.Update();
                    // Do not Process() the EventSystem: its hover cache deliberately remains stale.
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = point }
                        .WithButton(button));
                    UnityEngine.InputSystem.InputSystem.Update();
                    browserUpdate.Invoke(browser, null);
                    Assert.IsFalse(manager.enabled, "Navigation must suspend Core before its Update, even with the showcase toggle off.");
                    Assert.IsFalse((bool)typeof(TransformHandleManager).GetProperty("IsInteracting", flags).GetValue(manager));
                    demoUpdate.Invoke(controller, null);
                    var camera = Camera.main.transform;
                    var position = camera.position; var rotation = camera.rotation;
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState
                        { position = new Vector2(Screen.width * .7f, Screen.height * .25f), delta = new Vector2(100, 100), scroll = new Vector2(0, 120) }
                        .WithButton(button));
                    UnityEngine.InputSystem.InputSystem.Update();
                    browserUpdate.Invoke(browser, null); demoUpdate.Invoke(controller, null);
                    Assert.IsFalse(manager.enabled);
                    Assert.AreEqual(position, camera.position); Assert.AreEqual(rotation, camera.rotation);
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = new Vector2(Screen.width * .7f, Screen.height * .25f) });
                    UnityEngine.InputSystem.InputSystem.Update();
                    browserUpdate.Invoke(browser, null); demoUpdate.Invoke(controller, null);
                    Assert.IsFalse(manager.enabled, "Release frame is still claimed.");
                    yield return null;
                    browserUpdate.Invoke(browser, null);
                    Assert.IsTrue(manager.enabled);
                    Assert.IsFalse((bool)controller.GetType().GetField("_blockTest", flags).GetValue(controller), "The experiment toggle must retain its value.");
                }
            }
            finally
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
                UnityEngine.InputSystem.InputSystem.settings = original;
                UnityEngine.Object.Destroy(settings);
            }
            browser.ReturnToGallery();
            yield return WaitForScene(_browserPath, browser);
        }
#endif

        [UnityTest]
        public IEnumerator HeaderNavigationRebindsAfterEveryControllerReenable()
        {
            var paths = new[] { "ModularBuilding", "LevelEditor", "RuntimeTransformHandlesDemo", "ProductConfigurator" }.Select(FindScene).ToArray();
            if (paths.Any(string.IsNullOrEmpty)) Assert.Ignore("Import all workspaces for header lifecycle coverage.");
            yield return SceneManager.LoadSceneAsync(_browserPath);
            yield return null;
            var browser = UnityEngine.Object.FindObjectOfType<DemoBrowser>();
            foreach (var path in paths)
            {
                browser.OpenDemo(path); yield return WaitForScene(path, browser);
                var controller = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>().Single(item =>
                    item.GetType().Name == "ModularBuildingDemo" || item.GetType().Name == "LevelEditorDemo" || item.GetType().Name == "HandleDemo" || item.GetType().Name == "ProductConfiguratorDemo");
                var document = UnityEngine.Object.FindObjectsOfType<UIDocument>().Single(doc => doc.rootVisualElement.Q<Button>("demo-gallery") != null);
                var previous = document.rootVisualElement.Q<Button>("demo-gallery");
                Assert.IsTrue(previous.ClassListContains("available"));
                controller.enabled = false; yield return null;
                Assert.IsFalse(previous.ClassListContains("available"));
                controller.enabled = true; yield return null;
                var button = document.rootVisualElement.Q<Button>("demo-gallery");
                Assert.AreNotSame(previous, button);
                Assert.IsTrue(button.ClassListContains("available"));
#if UNITY_6000_0_OR_NEWER
                // Adding the available class schedules a style/layout pass. Focus only after
                // the reconstructed button has become visible to the panel.
                yield return null;
                Assert.Greater(button.worldBound.width, 0, path);
                button.Focus();
                Assert.AreSame(button, button.panel.focusController.focusedElement, path);
                Assert.IsTrue(button.enabledInHierarchy, path);
                using (var evt = NavigationSubmitEvent.GetPooled()) button.SendEvent(evt);
                Assert.IsTrue((bool)typeof(DemoBrowser).GetField("_loading", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(browser), "Header did not invoke navigation: " + path);
#else
                using (var evt = MouseDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = button.worldBound.center })) button.SendEvent(evt);
                using (var evt = MouseUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = button.worldBound.center })) button.SendEvent(evt);
#endif
                yield return WaitForScene(_browserPath, browser);
                Assert.AreEqual(1, UnityEngine.Object.FindObjectsOfType<EventSystem>().Length);
            }
        }

        private static IEnumerator WaitForScene(string path, DemoBrowser browser)
        {
            float deadline = Time.realtimeSinceStartup + 20;
            var loading = typeof(DemoBrowser).GetField("_loading", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            while (SceneManager.GetActiveScene().path != path || (bool)loading.GetValue(browser))
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "Scene transition timed out: " + path);
                yield return null;
            }
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator RestoreScenes()
        {
            var browser = UnityEngine.Object.FindObjectOfType<DemoBrowser>();
            if (browser != null) UnityEngine.Object.Destroy(browser.gameObject);
            yield return null;
        }
#else
        void IPrebuildSetup.Setup() { }
        void IPostBuildCleanup.Cleanup() { }
#endif
    }
}
