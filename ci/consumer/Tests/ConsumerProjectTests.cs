using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TransformHandles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ConsumerJourney.Tests
{
    public sealed class ConsumerProjectTests
    {
        private static string UserAssets => ConsumerBootstrap.Config.assetRoot;
        [Test]
        public void SavedScenesKeepConsumerOwnedReferences()
        {
            foreach (var name in new[] {"Default", "Next"})
            {
                var scene = EditorSceneManager.OpenScene(UserAssets + "/" + name + ".unity");
                var example = Object.FindObjectOfType<ConsumerExample>();
                Assert.That(example, Is.Not.Null);
                Assert.That(example.targets, Has.Length.EqualTo(2));
                Assert.That(AssetDatabase.GetAssetPath(example.settings), Is.EqualTo(UserAssets + "/Settings.asset"));
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var child in root.GetComponentsInChildren<Transform>(true))
                        Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject), Is.Zero, child.name);
            }
        }

        [Test]
        public void PublicSetupPreservesOccupiedUserLayers()
        {
            Assert.That(LayerMask.NameToLayer("TransformHandle"), Is.GreaterThanOrEqualTo(8));
            Assert.That(LayerMask.LayerToName(6), Is.EqualTo("UserPreferredLayer"));
            Assert.That(LayerMask.LayerToName(9), Is.EqualTo("UserOtherLayer"));
        }

        [Test]
        public void ManagerUsesConsumerPrefabAndSettingsCopies()
        {
            var manager = AssetDatabase.LoadAssetAtPath<GameObject>(UserAssets + "/Manager.prefab");
            var serialized = new SerializedObject(manager.GetComponent<TransformHandleManager>());
            Assert.That(AssetDatabase.GetAssetPath(serialized.FindProperty("transformHandlePrefab").objectReferenceValue), Is.EqualTo(UserAssets + "/Handle.prefab"));
            Assert.That(AssetDatabase.GetAssetPath(serialized.FindProperty("settings").objectReferenceValue), Is.EqualTo(UserAssets + "/Settings.asset"));
            Assert.That(AssetDatabase.LoadAssetAtPath<Material>(UserAssets + "/Target.mat"), Is.Not.Null);
            Assert.That(File.ReadAllText("Assets/UserNotes.txt"), Is.EqualTo("User-owned content. Preserve byte-for-byte.\n"));
        }

        [Test]
        public void AssetGuidsAreUniqueAfterImportsAndUpgrade()
        {
            var owners = new Dictionary<string, string>();
            foreach (var path in Directory.GetFiles("Assets", "*.meta", SearchOption.AllDirectories))
            {
                var match = Regex.Match(File.ReadAllText(path), @"^guid: ([0-9a-f]{32})$", RegexOptions.Multiline);
                Assert.That(match.Success, Is.True, path);
                Assert.That(owners.ContainsKey(match.Groups[1].Value), Is.False, "Duplicate GUID: " + path);
                owners.Add(match.Groups[1].Value, path);
            }
        }
    }
}
