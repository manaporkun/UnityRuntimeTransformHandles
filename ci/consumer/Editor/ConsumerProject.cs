using System;
using System.IO;
using System.Linq;
using TransformHandles;
using TransformHandles.Editor;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ConsumerJourney
{
    public static class ConsumerProject
    {
        private const string AssetsRoot = "Assets/UserConsumer";
        private const string Core = "Packages/com.orkunmanap.runtime-transform-handles/";

        public static void Setup()
        {
            ConsumerBootstrap.Require(ConsumerBootstrap.Config.stage == "core-setup", "Wrong setup stage");
            CheckLayerSetup();
            Directory.CreateDirectory(AssetsRoot);
            var settings = ScriptableObject.CreateInstance<TransformHandleSettings>();
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("defaultSpace").intValue = (int)Space.World;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(settings, AssetsRoot + "/Settings.asset");
            AssetDatabase.CopyAsset(Core + "Runtime/Prefabs/PrecisionTransformHandle.prefab", AssetsRoot + "/Handle.prefab");
            AssetDatabase.CopyAsset(Core + "Runtime/Resources/TransformHandleManager.prefab", AssetsRoot + "/Manager.prefab");
            var managerRoot = PrefabUtility.LoadPrefabContents(AssetsRoot + "/Manager.prefab");
            var manager = new SerializedObject(managerRoot.GetComponent<TransformHandleManager>());
            manager.FindProperty("transformHandlePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(AssetsRoot + "/Handle.prefab");
            manager.FindProperty("settings").objectReferenceValue = settings;
            manager.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(managerRoot, AssetsRoot + "/Manager.prefab");
            PrefabUtility.UnloadPrefabContents(managerRoot);
            // A supplied pipeline-independent material copied into consumer-owned Assets.
            AssetDatabase.CopyAsset(Core + "Runtime/Materials/PrecisionMoveX.mat", AssetsRoot + "/Target.mat");
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetsRoot + "/Target.mat");
            material.color = new Color(.95f, .5f, .08f, 1);
            material.renderQueue = 2000;
            EditorUtility.SetDirty(material);
            CreateScene(settings, material, "Default");
            CreateScene(settings, material, "Next");
            AssetDatabase.SaveAssets();
            ConsumerBootstrap.Observe();
        }

        public static void CheckLayerSetup()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            var initial = Enumerable.Range(0, layers.arraySize).Select(i => layers.GetArrayElementAtIndex(i).stringValue).ToArray();
            ConsumerBootstrap.Require(!initial.Contains("TransformHandle"), "Layer was preconfigured instead of exercising setup");
            TransformHandleLayerSetup.SetupLayer();
            ConsumerBootstrap.Require(LayerMask.NameToLayer("TransformHandle") >= 0, "SetupLayer did not create layer");
            var first = File.ReadAllBytes("ProjectSettings/TagManager.asset");
            TransformHandleLayerSetup.SetupLayer();
            ConsumerBootstrap.Require(first.SequenceEqual(File.ReadAllBytes("ProjectSettings/TagManager.asset")), "SetupLayer is not idempotent");
            tagManager.Update();
            for (var i = 0; i < initial.Length; i++)
            {
                if (!string.IsNullOrEmpty(initial[i]))
                    ConsumerBootstrap.Require(layers.GetArrayElementAtIndex(i).stringValue == initial[i], "SetupLayer overwrote user layer");
                else layers.GetArrayElementAtIndex(i).stringValue = "";
            }
            layers.GetArrayElementAtIndex(6).stringValue = "UserPreferredLayer";
            layers.GetArrayElementAtIndex(9).stringValue = "UserOtherLayer";
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            TransformHandleLayerSetup.SetupLayer();
            tagManager.Update();
            ConsumerBootstrap.Require(LayerMask.NameToLayer("TransformHandle") >= 8 &&
                layers.GetArrayElementAtIndex(6).stringValue == "UserPreferredLayer" &&
                layers.GetArrayElementAtIndex(9).stringValue == "UserOtherLayer", "Occupied-layer setup lost user layers");
            TransformHandleLayerSetup.SetupLayer();
            AssetDatabase.SaveAssets();
        }

        private static void CreateScene(TransformHandleSettings settings, Material material, string name)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Consumer Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 0, -12);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.12f, .14f, .18f, 1);
            camera.nearClipPlane = .1f;
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AssetsRoot + "/Manager.prefab"));
            var example = new GameObject("README Consumer Example").AddComponent<ConsumerExample>();
            example.settings = settings;
            example.targets = new[] { CreateTarget("User Target A", new Vector3(-1.25f, 0, 0), material),
                                      CreateTarget("User Target B", new Vector3(1.25f, 0, 0), material) };
            var probe = new GameObject("Read-only Player Observer").AddComponent<ConsumerProbe>();
            var config = ConsumerBootstrap.Config;
            probe.runId = config.runId;
            probe.source = config.source;
            probe.packageSource = config.packageSource;
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), AssetsRoot + "/" + name + ".unity");
        }

        private static Transform CreateTarget(string name, Vector3 position, Material material)
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = name;
            target.transform.position = position;
            target.transform.rotation = Quaternion.Euler(0, 0, 30);
            target.GetComponent<Renderer>().sharedMaterial = material;
            return target.transform;
        }

        public static void Build()
        {
            var scenes = new[] {AssetsRoot + "/Default.unity", AssetsRoot + "/Next.unity"};
            BuildScenes(scenes);
        }

        public static void BuildScenes(string[] scenes)
        {
            var config = ConsumerBootstrap.Config;
            var identity = new ConsumerBuildIdentity {runId = config.runId, source = config.source, packageSource = config.packageSource,
                lifecycleStage = config.stage, buildId = Guid.NewGuid().ToString("N")};
            const string resources = "Assets/ConsumerJourney/Runtime/Resources";
            Directory.CreateDirectory(resources);
            File.WriteAllText(resources + "/ConsumerJourneyBuild.json", JsonUtility.ToJson(identity, true));
            AssetDatabase.ImportAsset(resources + "/ConsumerJourneyBuild.json", ImportAssetOptions.ForceSynchronousImport);
            ConsumerBootstrap.Require((GraphicsSettings.currentRenderPipeline != null) == config.urp, "Rendering pipeline does not match profile");
            foreach (var path in scenes)
            {
                var scene = EditorSceneManager.OpenScene(path);
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                        ConsumerBootstrap.Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0, "Missing script: " + path);
            }
            PlayerSettings.defaultScreenWidth = 960;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SetStackTraceLogType(LogType.Error, StackTraceLogType.Full);
            PlayerSettings.SetStackTraceLogType(LogType.Exception, StackTraceLogType.Full);
            var target = (BuildTarget)Enum.Parse(typeof(BuildTarget), config.buildTarget);
            PlayerSettings.SetScriptingBackend(BuildPipeline.GetBuildTargetGroup(target), ScriptingImplementation.Mono2x);
            var output = ".journey/build/" + config.stage + (target == BuildTarget.StandaloneOSX ? "/Consumer.app" : "/Consumer.x86_64");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes = scenes, target = target, locationPathName = output, options = BuildOptions.Development});
            ConsumerBootstrap.Require(result.summary.result == BuildResult.Succeeded, "Native player build failed");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConsumerBootstrap.Observe(output, scenes, identity.buildId);
        }
    }
}
