using System;
using System.IO;
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
    private const string PackageName = "com.orkunmanap.runtime-transform-handles";
    private const string SampleSceneName = "RuntimeTransformHandlesDemo";

    [MenuItem("Tools/Build/WebGL (GitHub Pages)")]
    public static void BuildWebGLFromMenu()
    {
        BuildWebGL(DefaultOutputPath);
    }

    public static void BuildWebGLForPages()
    {
        var buildPath = GetCommandLineArgument("-buildPath") ?? DefaultOutputPath;
        BuildWebGL(buildPath);
    }

    private static void BuildWebGL(string outputPath)
    {
        var scenePath = ResolveSampleScenePath();

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
    private static string ResolveSampleScenePath()
    {
        var existing = FindSampleScene();
        if (existing != null) return existing;

        var samples = Sample.FindByPackage(PackageName, ResolvePackageVersion()).ToArray();
        if (samples.Length == 0)
        {
            throw new InvalidOperationException(
                $"No samples registered for package '{PackageName}'.");
        }

        foreach (var sample in samples)
        {
            if (!sample.Import(Sample.ImportOptions.OverridePreviousImports |
                               Sample.ImportOptions.HideImportWindow))
            {
                throw new InvalidOperationException(
                    $"Failed to import sample '{sample.displayName}' from '{PackageName}'.");
            }
        }

        // Force a synchronous import so the copied scene/scripts are registered (and, in batch
        // mode, compiled) before BuildPlayer runs.
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        var scenePath = FindSampleScene();
        if (scenePath == null)
        {
            throw new InvalidOperationException(
                $"Imported sample(s) but '{SampleSceneName}.unity' was not found under Assets.");
        }

        Debug.Log($"Building WebGL from imported sample scene: {scenePath}");
        return scenePath;
    }

    private static string FindSampleScene()
    {
        foreach (var guid in AssetDatabase.FindAssets($"{SampleSceneName} t:Scene"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == SampleSceneName)
            {
                return path;
            }
        }

        return null;
    }

    private static string ResolvePackageVersion()
    {
        // Offline list (the package is embedded) so this stays fast and deterministic on CI.
        var request = Client.List(offlineMode: true, includeIndirectDependencies: false);
        while (!request.IsCompleted)
        {
            Thread.Sleep(50);
        }

        if (request.Status == StatusCode.Success)
        {
            var package = request.Result.FirstOrDefault(p => p.name == PackageName);
            if (package != null) return package.version;
        }

        throw new InvalidOperationException(
            $"Package '{PackageName}' is not installed in this project.");
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
