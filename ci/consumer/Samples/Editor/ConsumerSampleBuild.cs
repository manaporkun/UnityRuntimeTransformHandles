using System.IO;
using UnityEditor;
using UnityEngine;

namespace ConsumerJourney
{
    public static class ConsumerSampleBuild
    {
        public static void Build()
        {
            var scenes = ConsumerSamples.Scenes();
            const string folder = "Assets/ConsumerJourney/Samples/Runtime/Resources";
            Directory.CreateDirectory(folder);
            var path = folder + "/ConsumerSampleRun.asset";
            var config = AssetDatabase.LoadAssetAtPath<ConsumerSampleRunConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<ConsumerSampleRunConfig>();
                AssetDatabase.CreateAsset(config, path);
            }
            var journey = ConsumerBootstrap.Config;
            config.runId = journey.runId;
            config.source = journey.source;
            config.packageSource = journey.packageSource;
            config.scenes = scenes;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            ConsumerProject.BuildScenes(scenes);
        }
    }
}
