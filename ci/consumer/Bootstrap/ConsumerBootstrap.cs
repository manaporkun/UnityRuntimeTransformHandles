using System;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ConsumerJourney
{
    // Unity-only bootstrap: must also compile before upgrade and after Core removal.
    [Serializable]
    public sealed class ConsumerConfig
    {
        public int schema;
        public string runId, stage, unity, profile, transport, scope, source, packageSource;
        public string sample, buildTarget, assetRoot;
        public int input;
        public bool urp;
    }

    [Serializable]
    public sealed class PackageObservation
    {
        public string name, version, source, resolvedPath, packageId;
    }

    [Serializable]
    public sealed class EditorObservation
    {
        public int schema = 1;
        public string runId, stage, unity, source, packageSource;
        public int input;
        public PackageObservation[] packages;
        public string[] assemblies;
        public string buildPath;
        public string buildId;
        public string[] scenes;
    }

    [Serializable]
    public sealed class UpgradeBaselineObservation
    {
        public string runId, importPath;
        public string settingsGuid, managerGuid, handleGuid, materialGuid;
        public string[] sceneGuids;
        public bool optionalCameraReference;
    }

    public static class ConsumerBootstrap
    {
        private static readonly object LogLock = new object();
        private static string _logPath;
        private static int _logFailures;

        [InitializeOnLoadMethod]
        private static void TraceInvocation()
        {
            if (!File.Exists(".journey/config.json")) return;
            var config = Config;
            Directory.CreateDirectory(".journey/editor");
            _logPath = ".journey/editor/" + config.stage + ".log";
            File.AppendAllText(_logPath, "CONSUMER_ENGINE " + Application.unityVersion + " " + config.runId + " " + config.stage + "\n");
            File.AppendAllText(_logPath, "CONSUMER_PID " + System.Diagnostics.Process.GetCurrentProcess().Id + "\n");
            Application.logMessageReceivedThreaded += CaptureEditorLog;
            Debug.Log("CONSUMER_INVOCATION " + config.runId + " " + config.stage);
        }

        private static void CaptureEditorLog(string message, string stack, LogType type)
        {
            lock (LogLock)
            {
                try { File.AppendAllText(_logPath, type + ": " + message + "\n" + stack + "\n"); }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { Interlocked.Increment(ref _logFailures); }
            }
        }

        public static ConsumerConfig Config
        {
            get
            {
                var config = JsonUtility.FromJson<ConsumerConfig>(File.ReadAllText(".journey/config.json"));
                Require(config.schema == 1 && !string.IsNullOrEmpty(config.runId), "Invalid journey config");
                Require(config.unity == Application.unityVersion, "Unexpected Editor: " + Application.unityVersion);
                Require(!string.IsNullOrEmpty(config.stage) && config.stage.All(c => char.IsLetterOrDigit(c) || c == '-'), "Unsafe stage");
                return config;
            }
        }

        public static void Run()
        {
            var config = Config;
            if (config.stage == "uninstall-verify")
                Require(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name.StartsWith("com.orkunmanap.runtime-transform-handles", StringComparison.Ordinal) || a.GetName().Name.StartsWith("TransformHandles.", StringComparison.Ordinal)), "Core/sample assembly remains after uninstall");
            Observe();
        }

        public static void RemoveCore()
        {
            Require(Config.stage == "uninstall-core", "Core removal is only for the prepared disposable uninstall copy");
            var request = Client.Remove("com.orkunmanap.runtime-transform-handles");
            var deadline = DateTime.UtcNow.AddSeconds(120);
            while (!request.IsCompleted)
            {
                Require(DateTime.UtcNow < deadline, "UPM Core removal timed out");
                Thread.Sleep(50);
            }
            Require(request.Status == StatusCode.Success, "UPM Core removal failed: " + request.Error?.message);
            Observe();
        }

        public static void PrepareUpgradeBaseline()
        {
            Require(Config.stage == "upgrade-baseline", "Wrong baseline stage");
            const string core = "com.orkunmanap.runtime-transform-handles";
            const string folder = "Assets/UserUpgrade";
            var package = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Single(p => p.name == core);
            Require(package.version == "3.3.1", "Unexpected previous stable Core");
            var oldSample = Sample.FindByPackage(core, package.version).Single();
            Require(!Directory.Exists(oldSample.importPath), "Baseline sample must be a first import");
            Require(oldSample.Import(Sample.ImportOptions.HideImportWindow), "Old showcase public import failed");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Directory.CreateDirectory(folder);
            // Resolve only old public Unity object types. No candidate fixture/API can compile here.
            var settingsType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("TransformHandles.TransformHandleSettings")).Single(type => type != null);
            var settings = ScriptableObject.CreateInstance(settingsType);
            var serializedSettings = new SerializedObject(settings);
            serializedSettings.FindProperty("rotationKey").intValue = (int)KeyCode.T;
            serializedSettings.FindProperty("highlightColor").colorValue = Color.cyan;
            serializedSettings.FindProperty("defaultSpace").intValue = (int)Space.World;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(settings, folder + "/Settings.asset");
            Require(AssetDatabase.CopyAsset("Packages/" + core + "/Runtime/Prefabs/NativeTransformHandle.prefab", folder + "/Handle.prefab"), "Cannot copy old handle prefab");
            Require(AssetDatabase.CopyAsset("Packages/" + core + "/Runtime/Resources/TransformHandleManager.prefab", folder + "/Manager.prefab"), "Cannot copy old manager prefab");
            Require(AssetDatabase.CopyAsset("Packages/" + core + "/Runtime/Materials/Red.mat", folder + "/Target.mat"), "Cannot copy old material");
            var handleRoot = PrefabUtility.LoadPrefabContents(folder + "/Handle.prefab");
            var handleComponent = handleRoot.GetComponents<MonoBehaviour>().Single(component => component.GetType().FullName == "TransformHandles.Handle");
            var handle = new SerializedObject(handleComponent);
            handle.FindProperty("autoScaleSizeInPixels").floatValue = 160;
            handle.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(handleRoot, folder + "/Handle.prefab");
            PrefabUtility.UnloadPrefabContents(handleRoot);
            var managerRoot = PrefabUtility.LoadPrefabContents(folder + "/Manager.prefab");
            var managerComponent = managerRoot.GetComponents<MonoBehaviour>().Single(component => component.GetType().FullName == "TransformHandles.TransformHandleManager");
            var manager = new SerializedObject(managerComponent);
            manager.FindProperty("transformHandlePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/Handle.prefab");
            manager.FindProperty("settings").objectReferenceValue = settings;
            manager.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(managerRoot, folder + "/Manager.prefab");
            PrefabUtility.UnloadPrefabContents(managerRoot);
            var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "/Target.mat");
            material.color = new Color(1, .6f, .1f, 1);
            material.renderQueue = 2000;
            EditorUtility.SetDirty(material);
            foreach (var name in new[] {"Default", "Next"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camera = new GameObject("User Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.transform.position = new Vector3(0, 0, -12);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.12f, .14f, .18f, 1);
                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/Manager.prefab"));
                for (var index = 0; index < 2; index++)
                {
                    var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    target.name = index == 0 ? "User Target A" : "User Target B";
                    target.transform.position = new Vector3(index == 0 ? -1.25f : 1.25f, 0, 0);
                    target.transform.rotation = Quaternion.Euler(0, 0, 30);
                    target.GetComponent<Renderer>().sharedMaterial = material;
                }
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), folder + "/" + name + ".unity");
            }
            AssetDatabase.SaveAssets();
            var legacyScene = Directory.GetFiles(oldSample.importPath, "*.unity").Single();
            var root = Path.GetFullPath(".").TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var imported = Path.GetFullPath(oldSample.importPath);
            Require(imported.StartsWith(root + "Assets" + Path.DirectorySeparatorChar, StringComparison.Ordinal), "Old import escaped Assets");
            var record = new UpgradeBaselineObservation
            {
                runId = Config.runId, importPath = imported.Substring(root.Length).Replace('\\', '/'),
                settingsGuid = AssetDatabase.AssetPathToGUID(folder + "/Settings.asset"), managerGuid = AssetDatabase.AssetPathToGUID(folder + "/Manager.prefab"),
                handleGuid = AssetDatabase.AssetPathToGUID(folder + "/Handle.prefab"), materialGuid = AssetDatabase.AssetPathToGUID(folder + "/Target.mat"),
                sceneGuids = new[] {"Default", "Next"}.Select(name => AssetDatabase.AssetPathToGUID(folder + "/" + name + ".unity")).ToArray(),
                optionalCameraReference = File.ReadAllText(legacyScene).Contains("a79441f348de89743a2939f4d699eac1")
            };
            File.WriteAllText(".journey/upgrade-baseline.json", JsonUtility.ToJson(record, true));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Observe();
        }

        public static void Observe(string buildPath = null, string[] scenes = null, string buildId = null)
        {
            var config = Config;
            Require(Volatile.Read(ref _logFailures) == 0, "Editor log capture failed");
            var request = Client.List(offlineMode: true, includeIndirectDependencies: true);
            var deadline = DateTime.UtcNow.AddSeconds(90);
            while (!request.IsCompleted)
            {
                Require(DateTime.UtcNow < deadline, "Package list timed out");
                Thread.Sleep(50);
            }
            Require(request.Status == StatusCode.Success, "Package list failed: " + request.Error?.message);
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var observedInput = settings.FindProperty("activeInputHandler").intValue;
            Require(observedInput == config.input, "Input backend differs from selected profile");
            var observation = new EditorObservation
            {
                runId = config.runId, stage = config.stage, unity = Application.unityVersion,
                source = config.source, packageSource = config.packageSource, input = observedInput,
                buildPath = buildPath, scenes = scenes, buildId = buildId,
                packages = request.Result.Select(p => new PackageObservation
                {
                    name = p.name, version = p.version, source = p.source.ToString(),
                    resolvedPath = p.resolvedPath.Replace('\\', '/'), packageId = p.packageId
                }).ToArray(),
                assemblies = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name).OrderBy(n => n).ToArray()
            };
            Directory.CreateDirectory(".journey/editor");
            var path = ".journey/editor/" + config.stage + ".json";
            Require(!File.Exists(path), "Refusing to overwrite Editor evidence");
            File.WriteAllText(path, JsonUtility.ToJson(observation, true));
            Debug.Log("CONSUMER_EDITOR " + config.runId + " " + config.stage);
            // The pinned GameCI builder requires this completion marker for every
            // custom executeMethod. It acknowledges this persisted Editor phase;
            // only BuildScenes reports a native build, after BuildResult.Succeeded.
            Debug.Log("Build succeeded!");
        }

        public static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
