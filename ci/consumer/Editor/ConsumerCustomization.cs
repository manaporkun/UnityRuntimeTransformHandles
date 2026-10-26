using System.IO;
using System.Linq;
using TransformHandles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ConsumerJourney
{
    public static class ConsumerCustomization
    {
        public static void Apply()
        {
            var settings = AssetDatabase.LoadAssetAtPath<TransformHandleSettings>("Assets/UserConsumer/Settings.asset");
            ConsumerBootstrap.Require(settings != null, "Consumer settings are missing");
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("rotationKey").intValue = (int)KeyCode.T;
            serialized.FindProperty("highlightColor").colorValue = Color.cyan;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var handleRoot = PrefabUtility.LoadPrefabContents("Assets/UserConsumer/Handle.prefab");
            var handle = new SerializedObject(handleRoot.GetComponent<Handle>());
            handle.FindProperty("autoScaleSizeInPixels").floatValue = 160;
            handle.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(handleRoot, "Assets/UserConsumer/Handle.prefab");
            PrefabUtility.UnloadPrefabContents(handleRoot);
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/UserConsumer/Target.mat");
            material.color = new Color(1, .6f, .1f, 1);
            EditorUtility.SetDirty(material);
            var imports = JsonUtility.FromJson<ConsumerImports>(File.ReadAllText(".journey/imports.json"));
            ConsumerBootstrap.Require(imports.runId == ConsumerBootstrap.Config.runId, "Stale sample customization target");
            var showcase = imports.samples.Single(s => s.name == "Demo");
            var scene = EditorSceneManager.OpenScene(showcase.scene);
            var camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>()).Single();
            camera.backgroundColor = new Color(.08f, .11f, .16f, 1);
            EditorUtility.SetDirty(camera);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            ConsumerSamples.VerifyExistingImports();
            ConsumerBootstrap.Observe();
        }

        public static void VerifyPersisted()
        {
            var settings = AssetDatabase.LoadAssetAtPath<TransformHandleSettings>("Assets/UserConsumer/Settings.asset");
            ConsumerBootstrap.Require(settings.RotationKey == KeyCode.T && settings.HighlightColor == Color.cyan, "Customized settings did not survive reopen");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UserConsumer/Handle.prefab");
            ConsumerBootstrap.Require(prefab.GetComponent<Handle>().AutoScaleSizeInPixels == 160, "Customized prefab did not survive reopen");
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/UserConsumer/Target.mat");
            ConsumerBootstrap.Require(material != null && material.color == new Color(1, .6f, .1f, 1) && material.renderQueue == 2000,
                "Customized material did not survive reopen");
        }

        public static void Build()
        {
            VerifyPersisted();
            ConsumerProject.Build();
        }
    }
}
