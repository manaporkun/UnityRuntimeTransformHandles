using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildScript
{
    private const string DefaultOutputPath = "Build/WebGL";
    private const string EmbedTemplate = "PROJECT:FullscreenEmbed";

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
        var previousTemplate = PlayerSettings.WebGL.template;
        var previousCompressionFormat = PlayerSettings.WebGL.compressionFormat;
        var previousDecompressionFallback = PlayerSettings.WebGL.decompressionFallback;
        var previousSplashScreen = PlayerSettings.SplashScreen.show;

        var enabledScenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (enabledScenes.Length == 0)
        {
            throw new InvalidOperationException("No enabled scenes found in Build Settings.");
        }

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
            scenes = enabledScenes,
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
