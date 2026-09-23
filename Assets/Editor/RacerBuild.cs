using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class RacerBuild
{
    const string Scene = "Assets/Scenes/SampleScene.unity";

    static void Configure()
    {
        PlayerSettings.companyName = "Coast Racer Studio";
        PlayerSettings.productName = "COAST RACER";
        PlayerSettings.bundleVersion = "1.0.0";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.coastracer.game");
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.runInBackground = false;
        QualitySettings.SetQualityLevel(0);
        QualitySettings.shadows = ShadowQuality.Disable;
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Scene, true) };
    }

    [MenuItem("Coast Racer/Build Android APK")]
    public static void Android()
    {
        Configure();
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        EditorUserBuildSettings.buildAppBundle = false;
        Directory.CreateDirectory("Builds/site");
        Build(BuildTarget.Android, "Builds/site/coast-racer.apk");
        CopySite();
    }

    [MenuItem("Coast Racer/Build Web Game")]
    public static void Web()
    {
        Configure();
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.template = "PROJECT:CoastRacer";
        PlayerSettings.WebGL.memorySize = 256;
        PlayerSettings.defaultWebScreenWidth = 960;
        PlayerSettings.defaultWebScreenHeight = 540;
        Build(BuildTarget.WebGL, "Builds/site/play");
        CopySite();
    }

    static void Build(BuildTarget target, string path)
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
            throw new InvalidOperationException("Install the " + target + " Build Support module in Unity Hub first.");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { Scene }, locationPathName = path, target = target, options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("Build failed: " + report.summary.result + " (" + report.summary.totalErrors + " errors)");
        Debug.Log("COAST_RACER_BUILD_OK " + path);
    }

    static void CopySite()
    {
        Directory.CreateDirectory("Builds/site");
        File.Copy("Deployment/site/index.html", "Builds/site/index.html", true);
        File.Copy("Deployment/site/404.html", "Builds/site/404.html", true);
    }
}
