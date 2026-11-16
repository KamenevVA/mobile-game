using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Small repeatable build helpers for the first Threadlight Android slice.</summary>
public static class ThreadlightBuildTools
{
    private const string ScenePath = "Assets/Scenes/Main.unity";
    private const string OutputPath = "Builds/Android/Threadlight.apk";

    public static void SetupAndroid()
    {
        Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        PlayerSettings.companyName = "Threadlight Studio";
        PlayerSettings.productName = "Threadlight";
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.threadlight.puzzle");

        Debug.Log("Threadlight Android scene and player settings are ready.");
    }

    public static void BuildAndroid()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        // The prototype creates its component at runtime, so stripping removed MonoScript and the screen stayed empty.
        PlayerSettings.stripEngineCode = false;
        PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.Android, ManagedStrippingLevel.Disabled);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = OutputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        });

        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new System.Exception("Android build failed: " + report.summary.result);

        Debug.Log("Android APK built: " + Path.GetFullPath(OutputPath));
    }
}
