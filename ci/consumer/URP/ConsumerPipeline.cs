using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ConsumerJourney
{
    // Staged only after the URP profile has resolved its explicit host dependency.
    public static class ConsumerPipeline
    {
        public static void Setup()
        {
            Configure();
            ConsumerProject.Setup();
        }

        public static void ImportSamples()
        {
            if (GraphicsSettings.currentRenderPipeline == null) Configure();
            ConsumerSamples.Import();
        }

        public static void Upgrade()
        {
            Configure();
            ConsumerUpgrade.Apply();
        }

        private static void Configure()
        {
            ConsumerBootstrap.Require(ConsumerBootstrap.Config.urp, "URP setup requested for Built-in profile");
            Directory.CreateDirectory("Assets/UserPipeline");
            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, "Assets/UserPipeline/Renderer.asset");
            var pipeline = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipeline, "Assets/UserPipeline/Pipeline.asset");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            var originalQuality = QualitySettings.GetQualityLevel();
            for (var index = 0; index < QualitySettings.names.Length; index++)
            {
                QualitySettings.SetQualityLevel(index, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(originalQuality, false);
            AssetDatabase.SaveAssets();
            ConsumerBootstrap.Require(GraphicsSettings.currentRenderPipeline == pipeline, "URP is installed but inactive");
        }
    }
}
