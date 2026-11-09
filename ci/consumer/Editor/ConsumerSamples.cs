using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace ConsumerJourney
{
    [Serializable]
    public sealed class ConsumerImport
    {
        public string name, path, scene, assembly;
    }

    [Serializable]
    public sealed class ConsumerImports
    {
        public string runId, stage;
        public ConsumerImport[] samples;
    }

    public static class ConsumerSamples
    {
        private const string Demos = "com.orkunmanap.runtime-transform-handles.demos";
        [Serializable] private sealed class Descriptor { public string name; }

        public static void Import()
        {
            Import(ConsumerBootstrap.Config.sample == "all", true);
        }

        private static void Import(bool all, bool observe)
        {
            var config = ConsumerBootstrap.Config;
            if (LayerMask.NameToLayer("TransformHandle") < 0) ConsumerProject.CheckLayerSetup();
            var package = PackageInfo.GetAllRegisteredPackages().Single(p => p.name == Demos);
            var selected = Sample.FindByPackage(Demos, package.version).ToArray();
            if (!all)
            {
                // Use the package's public sample registration, never a source-folder copy.
                var manifest = JsonUtility.FromJson<SampleManifest>(File.ReadAllText(Path.Combine(package.resolvedPath, "package.json")));
                var displayName = manifest.samples.Single(s => s.path == "Samples~/" + config.sample).displayName;
                selected = selected.Where(sample => sample.displayName == displayName).ToArray();
            }
            ConsumerBootstrap.Require(selected.Length == (all ? 5 : 1), "Unexpected public sample inventory");
            var imports = new List<ConsumerImport>();
            foreach (var sample in selected)
            {
                var relative = SampleRelativePath(package, sample.displayName);
                var source = Path.Combine(package.resolvedPath, relative);
                var scenes = Directory.GetFiles(source, "*.unity", SearchOption.TopDirectoryOnly);
                var assemblies = Directory.GetFiles(source, "*.asmdef", SearchOption.TopDirectoryOnly);
                ConsumerBootstrap.Require(scenes.Length == 1 && assemblies.Length == 1, "Ambiguous standalone sample scene/assembly");
                var scene = Path.Combine(sample.importPath, Path.GetFileName(scenes[0]));
                RejectDuplicateGuids(source, sample.importPath);
                if (!File.Exists(scene))
                {
                    ConsumerBootstrap.Require(!Directory.Exists(sample.importPath) || !Directory.EnumerateFileSystemEntries(sample.importPath).Any(), "Refusing to overwrite an existing customized import");
                    ConsumerBootstrap.Require(sample.Import(Sample.ImportOptions.HideImportWindow), "Public Sample.Import failed");
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                }
                ConsumerBootstrap.Require(File.Exists(scene), "Public sample import did not create its scene");
                imports.Add(new ConsumerImport {name = relative.Substring("Samples~/".Length), path = AssetsPath(sample.importPath),
                    scene = AssetsPath(scene), assembly = JsonUtility.FromJson<Descriptor>(File.ReadAllText(assemblies[0])).name});
            }
            File.WriteAllText(".journey/imports.json", JsonUtility.ToJson(new ConsumerImports {runId = config.runId, stage = config.stage, samples = imports.ToArray()}, true));
            if (observe)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                ConsumerBootstrap.Observe();
            }
        }

        public static void VerifyExistingImports()
        {
            var imports = JsonUtility.FromJson<ConsumerImports>(File.ReadAllText(".journey/imports.json"));
            var paths = imports.samples.SelectMany(s => Directory.GetFiles(s.path, "*", SearchOption.AllDirectories)).ToArray();
            var saved = paths.ToDictionary(path => path, File.ReadAllBytes);
            Import(true, false);
            ConsumerBootstrap.Require(saved.All(pair => File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value)), "Public import overwrote consumer customization");
            Debug.Log("CONSUMER_IMPORT_PRESERVED " + ConsumerBootstrap.Config.runId);
            const string duplicate = "Assets/ConsumerDuplicateProbe.txt.meta";
            ConsumerBootstrap.Require(!File.Exists(duplicate), "Duplicate GUID probe path is already occupied");
            var rejected = false;
            try
            {
                File.Copy(paths.First(path => path.EndsWith(".meta", StringComparison.Ordinal)), duplicate);
                try { Import(true, false); }
                catch (InvalidOperationException error) when (error.Message.StartsWith("Duplicate sample GUID;", StringComparison.Ordinal)) { rejected = true; }
            }
            finally { File.Delete(duplicate); }
            ConsumerBootstrap.Require(rejected, "Public import accepted a duplicate sample GUID");
            ConsumerBootstrap.Require(saved.All(pair => File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value)), "Rejected import changed consumer assets");
            Debug.Log("CONSUMER_DUPLICATE_REJECTED " + ConsumerBootstrap.Config.runId);
        }

        public static string[] Scenes()
        {
            var config = ConsumerBootstrap.Config;
            var imports = JsonUtility.FromJson<ConsumerImports>(File.ReadAllText(".journey/imports.json"));
            ConsumerBootstrap.Require(imports.runId == config.runId && imports.samples.Length > 0, "Stale or missing public import record");
            foreach (var sample in imports.samples)
                ConsumerBootstrap.Require(AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == sample.assembly), "Reopen Unity after importing a new sample assembly: " + sample.assembly);
            return imports.samples.OrderBy(s => s.name == "DemoBrowser" ? 0 : 1).ThenBy(s => s.name).Select(s => s.scene).ToArray();
        }

        public static void RejectDuplicateGuids(string source, string selectedImport)
        {
            var sourceGuids = new HashSet<string>(Directory.GetFiles(source, "*.meta", SearchOption.AllDirectories).Select(ReadGuid));
            var allowed = Path.GetFullPath(selectedImport).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (var path in Directory.GetFiles("Assets", "*.meta", SearchOption.AllDirectories))
                if (!Path.GetFullPath(path).StartsWith(allowed, StringComparison.Ordinal))
                    ConsumerBootstrap.Require(!sourceGuids.Contains(ReadGuid(path)), "Duplicate sample GUID; back up the old import outside Assets and explicitly remove it: " + path);
        }

        private static string ReadGuid(string path)
        {
            var match = Regex.Match(File.ReadAllText(path), @"^guid: ([0-9a-f]{32})$", RegexOptions.Multiline);
            ConsumerBootstrap.Require(match.Success, "Invalid asset GUID: " + path);
            return match.Groups[1].Value;
        }

        private static string AssetsPath(string path)
        {
            var absolute = Path.GetFullPath(path);
            var root = Path.GetFullPath(".").TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            ConsumerBootstrap.Require(absolute.StartsWith(root + "Assets" + Path.DirectorySeparatorChar, StringComparison.Ordinal), "Sample import escaped project Assets");
            return absolute.Substring(root.Length).Replace('\\', '/');
        }

        [Serializable] private sealed class SampleDescriptor { public string displayName, path; }
        [Serializable] private sealed class SampleManifest { public SampleDescriptor[] samples; }

        private static string SampleRelativePath(PackageInfo package, string displayName)
        {
            return JsonUtility.FromJson<SampleManifest>(File.ReadAllText(Path.Combine(package.resolvedPath, "package.json")))
                .samples.Single(sample => sample.displayName == displayName).path;
        }
    }
}
