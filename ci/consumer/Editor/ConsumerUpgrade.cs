using System.IO;
using System.Linq;
using TransformHandles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ConsumerJourney
{
    public static class ConsumerUpgrade
    {
        private const string Folder = "Assets/UserUpgrade";

        public static void Apply()
        {
            VerifyIdentity();
            ConsumerProject.CheckLayerSetup();
            var settings = AssetDatabase.LoadAssetAtPath<TransformHandleSettings>(Folder + "/Settings.asset");
            ConsumerBootstrap.Require(settings != null && settings.RotationKey == KeyCode.T && settings.HighlightColor == Color.cyan, "Old settings lost effective values");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Manager.prefab");
            ConsumerBootstrap.Require(prefab.GetComponent<TransformHandleManager>() != null, "Old manager prefab lost its Core component");
            foreach (var name in new[] {"Default", "Next"})
            {
                var scene = EditorSceneManager.OpenScene(Folder + "/" + name + ".unity");
                ConsumerBootstrap.Require(Object.FindObjectOfType<ConsumerExample>() == null, "Candidate-only example was present before upgrade");
                var example = new GameObject("Upgraded README Consumer").AddComponent<ConsumerExample>();
                example.settings = settings;
                example.targets = new[] {GameObject.Find("User Target A").transform, GameObject.Find("User Target B").transform};
                var probe = new GameObject("Upgraded Player Observations").AddComponent<ConsumerProbe>();
                var config = ConsumerBootstrap.Config;
                probe.runId = config.runId;
                probe.source = config.source;
                probe.packageSource = config.packageSource;
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            VerifyIdentity();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConsumerBootstrap.Observe();
        }

        public static void VerifyIdentity()
        {
            var baseline = JsonUtility.FromJson<UpgradeBaselineObservation>(File.ReadAllText(".journey/upgrade-baseline.json"));
            ConsumerBootstrap.Require(baseline.runId == ConsumerBootstrap.Config.runId, "Stale upgrade baseline record");
            var paths = new[] {"Settings.asset", "Manager.prefab", "Handle.prefab", "Target.mat", "Default.unity", "Next.unity"};
            var guids = new[] {baseline.settingsGuid, baseline.managerGuid, baseline.handleGuid, baseline.materialGuid}.Concat(baseline.sceneGuids).ToArray();
            ConsumerBootstrap.Require(paths.Select(path => AssetDatabase.AssetPathToGUID(Folder + "/" + path)).SequenceEqual(guids), "Upgrade replaced a user-owned asset GUID");
            ConsumerBootstrap.Require(guids.All(guid => !string.IsNullOrEmpty(guid)), "Missing baseline user GUID");
        }

        public static void Build()
        {
            VerifyIdentity();
            ConsumerBootstrap.Require(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Handle.prefab").GetComponent<Handle>().AutoScaleSizeInPixels == 160, "Old custom handle configuration was lost");
            ConsumerProject.BuildScenes(new[] {Folder + "/Default.unity", Folder + "/Next.unity"});
        }
    }
}
