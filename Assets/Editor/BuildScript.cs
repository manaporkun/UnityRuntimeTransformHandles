using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.UI;
using UnityEngine;

public static class BuildScript
{
    private const string DefaultOutputPath = "Build/WebGL";
    private const string EmbedTemplate = "PROJECT:FullscreenEmbed";

    // The public WebGL page builds the package's canonical sample (single source of truth).
    // The sample ships inside the package's hidden Samples~ folder, so it isn't part of the
    // project until imported into Assets/Samples — see ResolveSampleScenePath.
    private const string PackageName = "com.orkunmanap.runtime-transform-handles.demos";
    private const string SampleSceneName = "RuntimeTransformHandlesDemo";

    [MenuItem("Tools/Build/WebGL (GitHub Pages)")]
    public static void BuildWebGLFromMenu()
    {
        BuildWebGL(DefaultOutputPath, "Runtime Transform Handles Demo", "Demo", SampleSceneName);
    }

    public static void BuildWebGLForPages()
    {
        var buildPath = GetCommandLineArgument("-buildPath") ?? DefaultOutputPath;
        BuildWebGL(buildPath, "Runtime Transform Handles Demo", "Demo", SampleSceneName);
    }

    public static void BuildLevelEditorWebGL()
    {
        BuildWebGL(GetCommandLineArgument("-buildPath") ?? "Build/LevelEditorWebGL",
            "Level Editor", "LevelEditor", "LevelEditor");
    }

    // A new sample assembly needs a domain reload before BuildPlayer serializes its scene.
    // For a clean CLI project, prepare in one invocation and build in the next.
    public static void PrepareModularBuildingWebGL()
    {
        ResolveSampleScenePath("Modular Building", "ModularBuilding", "ModularBuilding");
        Debug.Log("Modular Building imported. Run BuildModularBuildingWebGL in a new Editor invocation after compilation.");
    }

    public static void BuildModularBuildingWebGL()
    {
        BuildWebGL(GetCommandLineArgument("-buildPath") ?? "Build/ModularBuildingWebGL",
            "Modular Building", "ModularBuilding", "ModularBuilding");
    }

    private static void BuildWebGL(string outputPath, string sampleName, string sampleFolder, string sceneName)
    {
        if (Application.isBatchMode && EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            throw new InvalidOperationException("Start Unity with -buildTarget WebGL before executing this build method.");
        var scenePath = ResolveSampleScenePath(sampleName, sampleFolder, sceneName);
        var assemblyName = "TransformHandles.Samples." + sampleFolder;
        if (!AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == assemblyName))
            throw new InvalidOperationException($"Sample '{sampleName}' has been imported but its Editor assembly is not loaded. " +
                "Let script compilation and domain reload finish, then run the build again in a new Editor invocation. " +
                "For clean Modular Building CLI builds, run BuildScript.PrepareModularBuildingWebGL first.");

        var previousTemplate = PlayerSettings.WebGL.template;
        var previousCompressionFormat = PlayerSettings.WebGL.compressionFormat;
        var previousDecompressionFallback = PlayerSettings.WebGL.decompressionFallback;
        var previousSplashScreen = PlayerSettings.SplashScreen.show;

        var absoluteOutputPath = Path.IsPathRooted(outputPath)
            ? outputPath
            : Path.Combine(Directory.GetCurrentDirectory(), outputPath);

        Directory.CreateDirectory(absoluteOutputPath);

        // Keep compression efficient while still supporting static hosts like GitHub Pages.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.template = EmbedTemplate;
        PlayerSettings.SplashScreen.show = false;

        var buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = new[] { scenePath },
            locationPathName = absoluteOutputPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        try
        {
            var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            var summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                throw new Exception($"WebGL build failed with result: {summary.result}");
            }

            Debug.Log($"WebGL build completed: {absoluteOutputPath}");
        }
        finally
        {
            PlayerSettings.WebGL.compressionFormat = previousCompressionFormat;
            PlayerSettings.WebGL.decompressionFallback = previousDecompressionFallback;
            PlayerSettings.WebGL.template = previousTemplate;
            PlayerSettings.SplashScreen.show = previousSplashScreen;
        }
    }

    // Locates the demo scene, importing the package sample on demand. The sample lives in the
    // package's Samples~ folder (hidden from Unity), so on a clean CI checkout it must be copied
    // into Assets/Samples first. Assets/Samples is gitignored — this is the only place it appears.
    private static string ResolveSampleScenePath(string sampleName, string sampleFolder, string sceneName)
    {
        var package = ResolvePackage();
        var matches = Sample.FindByPackage(PackageName, package.version)
            .Where(sample => sample.displayName == sampleName).ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException($"Expected one '{sampleName}' sample in '{PackageName}'.");

        var selected = matches[0];
        var source = Path.Combine(package.resolvedPath, "Samples~", sampleFolder);
        RejectDuplicateImports(source, selected.importPath);
        var scenePath = Path.Combine(selected.importPath, sceneName + ".unity").Replace('\\', '/');
        if (!File.Exists(scenePath))
        {
            // Existing imports may be customized. Never overwrite or delete them as a build side effect.
            if (Directory.Exists(selected.importPath) && Directory.EnumerateFileSystemEntries(selected.importPath).Any())
                throw new InvalidOperationException($"Existing sample import lacks '{sceneName}.unity': {selected.importPath}");
            if (!selected.Import(Sample.ImportOptions.HideImportWindow))
                throw new InvalidOperationException($"Failed to import '{sampleName}'.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
        if (!File.Exists(scenePath))
            throw new InvalidOperationException($"Imported sample scene not found: {scenePath}");
        // Sample.importPath is absolute in some Editor versions; BuildPipeline expects Assets-relative paths.
        var projectRoot = Path.GetFullPath(".").TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var absoluteScene = Path.GetFullPath(scenePath);
        if (!absoluteScene.StartsWith(projectRoot + "Assets" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException($"Sample scene is outside this project's Assets folder: {scenePath}");
        scenePath = absoluteScene.Substring(projectRoot.Length).Replace('\\', '/');
        Debug.Log($"Building WebGL from imported sample scene: {scenePath}");
        return scenePath;
    }

    private static void RejectDuplicateImports(string sourcePath, string selectedImport)
    {
        var sourceGuids = new HashSet<string>(Directory.GetFiles(sourcePath, "*.meta", SearchOption.AllDirectories)
            .Select(ReadGuid).Where(guid => guid != null));
        var allowed = Path.GetFullPath(selectedImport).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var meta in Directory.GetFiles("Assets", "*.meta", SearchOption.AllDirectories))
        {
            if (Path.GetFullPath(meta).StartsWith(allowed, StringComparison.Ordinal)) continue;
            var guid = ReadGuid(meta);
            if (guid != null && sourceGuids.Contains(guid))
                throw new InvalidOperationException($"Sample GUID already exists in '{meta}'. Back up the old import outside Assets and remove it explicitly before importing the relocated sample.");
        }
    }

    private static string ReadGuid(string path)
    {
        var match = Regex.Match(File.ReadAllText(path), @"^guid: ([0-9a-f]{32})$", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static UnityEditor.PackageManager.PackageInfo ResolvePackage()
    {
        var request = Client.List(offlineMode: true, includeIndirectDependencies: false);
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (!request.IsCompleted)
        {
            if (DateTime.UtcNow >= deadline)
                throw new InvalidOperationException($"Timed out resolving package: {PackageName}");
            Thread.Sleep(50);
        }
        if (request.Status == StatusCode.Success)
        {
            var package = request.Result.FirstOrDefault(p => p.name == PackageName);
            if (package != null) return package;
        }
        throw new InvalidOperationException($"Package '{PackageName}' is not installed in this project.");
    }

    private static string GetCommandLineArgument(string key)
    {
        var args = Environment.GetCommandLineArgs();
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (args[index] == key)
            {
                return args[index + 1];
            }
        }

        return null;
    }
}
