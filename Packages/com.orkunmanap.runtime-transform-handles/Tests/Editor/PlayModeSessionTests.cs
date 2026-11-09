using System.Collections;
using System.IO;
using NUnit.Framework;
using TransformHandles.Utils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TransformHandles.Tests.Editor
{
    /// <summary>Real editor session transitions; never reset package state between entries.</summary>
    public class PlayModeSessionTests
    {
        private const string Key = "TransformHandles.PlayModeSessionTests.";
        private const string TargetName = "P0E Target";
        private const string CameraName = "P0E Camera";
        private const string ProbeScene = "P0E Probe";

        [UnityTest] public IEnumerator Default_factory() => RunCase(EnterPlayModeOptions.None, false);
        [UnityTest] public IEnumerator Default_authored() => RunCase(EnterPlayModeOptions.None, true);
        [UnityTest] public IEnumerator NoDomain_factory() => RunCase(EnterPlayModeOptions.DisableDomainReload, false);
        [UnityTest] public IEnumerator NoDomain_authored() => RunCase(EnterPlayModeOptions.DisableDomainReload, true);
        [UnityTest] public IEnumerator NoScene_factory() => RunCase(EnterPlayModeOptions.DisableSceneReload, false);
        [UnityTest] public IEnumerator NoScene_authored() => RunCase(EnterPlayModeOptions.DisableSceneReload, true);
        [UnityTest] public IEnumerator NoReload_factory() => RunCase(EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload, false);
        [UnityTest] public IEnumerator NoReload_authored() => RunCase(EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload, true);

        private static IEnumerator RunCase(EnterPlayModeOptions options, bool authored)
        {
            Prepare(options, authored);
            // Only the iterator program counter survives a domain reload. All progress is in SessionState.
            while (SessionState.GetInt(Key + "session", 0) < 3)
            {
                Checkpoint("entry-requested");
                yield return new EnterPlayMode((options & EnterPlayModeOptions.DisableDomainReload) == 0);
                CheckEntry(options, authored);
                Checkpoint("entered-and-asserted");
                ExerciseOwnershipAndSceneChange();
                yield return SceneManager.UnloadSceneAsync(ProbeScene);
                BeginExitInteraction();
                Checkpoint("exit-requested");
                yield return new ExitPlayMode();
                CheckExit(options, authored);
                Checkpoint("exited-and-asserted");
                SessionState.SetInt(Key + "session", SessionState.GetInt(Key + "session", 0) + 1);
            }
            Assert.AreEqual(12, SessionState.GetInt(Key + "checkpoints", 0));
            Assert.AreEqual(3, SessionState.GetInt(Key + "session", 0));
        }

        private static void Prepare(EnterPlayModeOptions options, bool authored)
        {
            Assert.IsFalse(EditorApplication.isPlaying);
            Assert.IsFalse(SessionState.GetBool(Key + "active", false), "previous case did not restore its state");
            var scenes = EditorSceneManager.GetSceneManagerSetup();
            // GetSceneManagerSetup omits untitled scenes. Check loaded scenes as well.
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var loaded = SceneManager.GetSceneAt(i);
                Assert.IsFalse(loaded.isDirty, "Refusing to discard an unsaved scene");
                if (string.IsNullOrEmpty(loaded.path))
                    Assert.IsTrue(Application.isBatchMode && scenes.Length == 0 &&
                        (System.Environment.GetEnvironmentVariable("P0E_ISOLATED_QC") == "1" ||
                         System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-transformHandlesIsolatedTests") >= 0),
                        "Untitled scenes require the explicitly isolated batch QC runner");
            }
            foreach (var scene in scenes)
            {
                Assert.IsNotEmpty(scene.path, "Run in an isolated project with saved scenes, not an untitled scene");
                if (scene.isLoaded)
                    Assert.IsFalse(SceneManager.GetSceneByPath(scene.path).isDirty, "Refusing to discard an unsaved scene");
            }
            SessionState.SetBool(Key + "enabled", EditorSettings.enterPlayModeOptionsEnabled);
            SessionState.SetInt(Key + "options", (int)EditorSettings.enterPlayModeOptions);
            SessionState.SetInt(Key + "sceneCount", scenes.Length);
            for (var i = 0; i < scenes.Length; i++)
            {
                SessionState.SetString(Key + "scene" + i, scenes[i].path);
                SessionState.SetBool(Key + "loaded" + i, scenes[i].isLoaded);
                SessionState.SetBool(Key + "selected" + i, scenes[i].isActive);
            }
            SessionState.SetBool(Key + "active", true);
            SessionState.SetString(Key + "case", options + "/" + (authored ? "authored" : "factory"));
            SessionState.SetInt(Key + "session", 0);
            SessionState.SetInt(Key + "checkpoints", 0);
            SessionState.SetInt(Key + "ends", 0);
            SessionState.SetString(Key + "phase", "");
            var folder = "P0E-" + System.Guid.NewGuid().ToString("N");
            SessionState.SetString(Key + "folder", "Assets/" + folder);
            AssetDatabase.CreateFolder("Assets", folder);
            var scenePath = "Assets/" + folder + "/Session.unity";
            SessionState.SetString(Key + "path", scenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject(CameraName).AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 0, -10);
            new GameObject(TargetName);
            if (authored)
            {
                var prefab = Resources.Load<TransformHandleManager>("TransformHandleManager");
                Assert.IsNotNull(prefab);
                PrefabUtility.InstantiatePrefab(prefab.gameObject);
            }
            Assert.IsTrue(EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath));
            EditorSettings.enterPlayModeOptionsEnabled = options != EnterPlayModeOptions.None;
            EditorSettings.enterPlayModeOptions = options;
        }

        private static T[] Find<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<T>(true);
#endif
        }

        private static void CheckEntry(EnterPlayModeOptions options, bool authored)
        {
            Assert.IsTrue(EditorApplication.isPlaying);
            Assert.AreEqual(options != EnterPlayModeOptions.None, EditorSettings.enterPlayModeOptionsEnabled);
            Assert.AreEqual(options, EditorSettings.enterPlayModeOptions);
            Assert.IsFalse(ApplicationQuitManager.ApplicationQuitting, "real session entry must reset the quit flag");
            Assert.AreEqual(0, Find<Handle>().Length, "old session handles survived");
            Assert.AreEqual(0, Find<Ghost>().Length, "old session pivots survived");
            Assert.AreEqual(authored ? 1 : 0, Find<TransformHandleManager>().Length);
            var manager = TransformHandleManager.Instance;
            Assert.IsNotNull(manager);
            Assert.AreSame(manager, TransformHandleManager.Instance);
            Assert.AreEqual(1, Find<TransformHandleManager>().Length);
            Assert.AreSame(GameObject.Find(CameraName).GetComponent<Camera>(), manager.MainCamera);
        }

        private static void ExerciseOwnershipAndSceneChange()
        {
            var manager = Find<TransformHandleManager>()[0];
            var target = GameObject.Find(TargetName).transform;
            var handle = manager.CreateHandle(target);
            Assert.IsNotNull(handle);
            CollectionAssert.AreEquivalent(new[] { target }, manager.GetTargets(handle));
            Assert.AreEqual(1, Find<Handle>().Length);
            Assert.AreEqual(1, Find<Ghost>().Length);
            Assert.AreSame(handle.Pivot, Find<Ghost>()[0].transform);
            SessionState.SetInt(Key + "ends", 0);
            handle.OnInteractionEndEvent += _ => SessionState.SetInt(Key + "ends", SessionState.GetInt(Key + "ends", 0) + 1);
            var axis = handle.GetComponentInChildren<PositionAxis>(true);
            Assert.IsNotNull(axis);
            Assert.IsTrue(manager.BeginInteraction(axis, handle.transform.position));
            Assert.IsTrue(manager.IsInteracting);
            var original = SceneManager.GetActiveScene();
            var probe = SceneManager.CreateScene(ProbeScene);
            Assert.IsTrue(SceneManager.SetActiveScene(probe));
            Assert.AreEqual(1, SessionState.GetInt(Key + "ends", 0));
            Assert.IsFalse(manager.IsInteracting);
            Assert.IsTrue(SceneManager.SetActiveScene(original));
        }

        private static void BeginExitInteraction()
        {
            var manager = Find<TransformHandleManager>()[0];
            var handle = Find<Handle>()[0];
            Assert.AreEqual(1, SessionState.GetInt(Key + "ends", 0));
            Assert.IsTrue(manager.BeginInteraction(handle.GetComponentInChildren<PositionAxis>(true), handle.transform.position));
            Assert.IsTrue(manager.IsInteracting);
            CollectionAssert.AreEquivalent(new[] { GameObject.Find(TargetName).transform }, manager.GetTargets(handle));
            Assert.AreEqual(1, Find<Ghost>().Length);
        }

        private static void CheckExit(EnterPlayModeOptions options, bool authored)
        {
            Assert.IsFalse(EditorApplication.isPlaying);
            if ((options & EnterPlayModeOptions.DisableDomainReload) != 0)
                Assert.IsTrue(ApplicationQuitManager.ApplicationQuitting, "quit must occur naturally, without test seeding");
            Assert.IsNotNull(GameObject.Find(TargetName));
            Assert.AreEqual(Vector3.zero, GameObject.Find(TargetName).transform.position);
            Assert.AreEqual(0, Find<Handle>().Length);
            Assert.AreEqual(0, Find<Ghost>().Length);
            Assert.AreEqual(authored ? 1 : 0, Find<TransformHandleManager>().Length);
        }

        private static void Checkpoint(string phase)
        {
            var count = SessionState.GetInt(Key + "checkpoints", 0);
            var phases = new[] { "entry-requested", "entered-and-asserted", "exit-requested", "exited-and-asserted" };
            Assert.AreEqual(phases[count % 4], phase);
            Assert.AreEqual(count / 4, SessionState.GetInt(Key + "session", 0));
            SessionState.SetString(Key + "phase", phase);
            SessionState.SetInt(Key + "checkpoints", count + 1);
            Evidence(phase);
        }

        private static void Evidence(string phase)
        {
            var line = SessionState.GetString(Key + "case", "unknown") + "\t" +
                SessionState.GetInt(Key + "session", 0) + "\t" + phase + "\tquit=" +
                ApplicationQuitManager.ApplicationQuitting + "\tends=" + SessionState.GetInt(Key + "ends", 0);
            Directory.CreateDirectory("Library/P0E-Evidence");
            File.AppendAllText("Library/P0E-Evidence/transitions.tsv", line + "\n");
            Debug.Log("P0E " + line);
        }

        private static void Attempt(System.Action action, System.Collections.Generic.List<System.Exception> errors)
        {
            try { action(); }
            catch (System.Exception exception) { errors.Add(exception); }
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            if (!SessionState.GetBool(Key + "active", false)) yield break;
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
            var errors = new System.Collections.Generic.List<System.Exception>();
            Attempt(() => Evidence("teardown-at-" + SessionState.GetString(Key + "phase", "")), errors);
            // Each restoration stage runs even if an earlier one fails; errors still fail the test.
            Attempt(() => EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Key + "enabled", false), errors);
            Attempt(() => EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(Key + "options", 0), errors);
            Attempt(() =>
            {
                Directory.CreateDirectory("Library/P0E-Evidence");
                File.AppendAllText("Library/P0E-Evidence/settings.tsv",
                    SessionState.GetString(Key + "case", "unknown") + "\t" +
                    SessionState.GetBool(Key + "enabled", false) + "\t" +
                    SessionState.GetInt(Key + "options", 0) + "\t" +
                    EditorSettings.enterPlayModeOptionsEnabled + "\t" +
                    (int)EditorSettings.enterPlayModeOptions + "\n");
            }, errors);
            var scenes = new SceneSetup[SessionState.GetInt(Key + "sceneCount", 0)];
            for (var i = 0; i < scenes.Length; i++)
            {
                scenes[i] = new SceneSetup
                {
                    path = SessionState.GetString(Key + "scene" + i, ""),
                    isLoaded = SessionState.GetBool(Key + "loaded" + i, false),
                    isActive = SessionState.GetBool(Key + "selected" + i, false)
                };
            }
            Attempt(() =>
            {
                if (scenes.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(scenes);
                else
                {
                    // Match Test Framework RestoreSceneSetupTask for its empty saved-scene setup.
                    EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                    Assert.AreEqual(0, EditorSceneManager.GetSceneManagerSetup().Length);
                }
            }, errors);
            Attempt(() =>
            {
                // Retain the asset for diagnosis if scene restoration failed and it is still loaded.
                if (!SceneManager.GetSceneByPath(SessionState.GetString(Key + "path", "")).isLoaded)
                    AssetDatabase.DeleteAsset(SessionState.GetString(Key + "folder", ""));
            }, errors);
            ApplicationQuitManager.SetApplicationQuitting(false);
            for (var i = 0; i < scenes.Length; i++)
            {
                SessionState.EraseString(Key + "scene" + i);
                SessionState.EraseBool(Key + "loaded" + i);
                SessionState.EraseBool(Key + "selected" + i);
            }
            foreach (var name in new[] { "case", "folder", "path", "phase" }) SessionState.EraseString(Key + name);
            foreach (var name in new[] { "options", "sceneCount", "session", "checkpoints", "ends" }) SessionState.EraseInt(Key + name);
            SessionState.EraseBool(Key + "enabled");
            SessionState.EraseBool(Key + "active");
            if (errors.Count > 0) throw new System.AggregateException("Session fixture restoration failed", errors);
        }
    }
}
