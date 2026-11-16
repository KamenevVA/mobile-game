using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BoltRush
{
    public static class BoltRushTools
    {
        const int LevelCount = 150;

        [MenuItem("BoltRush/Generate Levels")]
        public static void GenerateLevels()
        {
            var pack = new LevelPack();
            var hist = new int[Const.DefaultBuffer + 2];
            var sw = Stopwatch.StartNew();
            int failed = 0, replayFail = 0;
            var summary = new System.Text.StringBuilder();

            for (int n = 1; n <= LevelCount; n++)
            {
                var L = Generator.Make(n, 80);
                if (L == null) { failed++; UnityEngine.Debug.LogError("BoltRush: no level for " + n); continue; }

                // Independent check: replay the solver's path through the rules.
                var geo = new LevelGeo(L);
                var path = new List<int>();
                int r = new Solver(geo).Solve(Const.DefaultBuffer, 200000, path);
                var sim = new Sim(geo, Const.DefaultBuffer);
                bool replayOk = r == 1;
                if (replayOk)
                {
                    foreach (int s in path)
                    {
                        int b, sl;
                        if (!sim.IsFree(s) || !sim.TryRemove(s, null, out b, out sl)) { replayOk = false; break; }
                    }
                    replayOk = replayOk && sim.Won;
                }
                if (!replayOk) { replayFail++; UnityEngine.Debug.LogError("BoltRush: replay failed for level " + n); continue; }

                pack.levels.Add(L);
                hist[Mathf.Clamp(L.minBuffer, 0, hist.Length - 1)]++;
                summary.AppendLine("L" + n + " screws=" + L.screws.Count + " plates=" + L.plates.Count + " boxes=" + L.boxes.Count + " minBuffer=" + L.minBuffer + " target=" + Generator.TargetBuffer(n) + " seed=" + L.seed + " nullBuilds=" + Generator.LastBuildNull + " tried=" + Generator.LastTried);
            }

            string dir = Path.Combine(Application.dataPath, "Resources");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "levels.json"), JsonUtility.ToJson(pack));
            AssetDatabase.Refresh();

            UnityEngine.Debug.Log("BoltRush summary\n" + summary);
            UnityEngine.Debug.Log("BoltRush: generated " + pack.levels.Count + "/" + LevelCount + " levels, failed=" + failed + ", replayFail=" + replayFail +
                ", minBuffer histogram [0.." + (hist.Length - 1) + "]=" + string.Join(",", hist) + ", time=" + sw.Elapsed.TotalSeconds.ToString("F1") + "s");
        }

        [MenuItem("BoltRush/Generate And Build Android")]
        public static void GenerateAndBuild()
        {
            GenerateLevels();
            SetupAndroid();
            BuildAndroid();
        }

        // Batch helper: the CI log can lose its tail on abort, so the exception is persisted separately.
        public static void BuildAndroidLogged()
        {
            try
            {
                BuildAndroid();
            }
            catch (Exception e)
            {
                File.WriteAllText("Builds/Android/build_error.txt", e.ToString());
                throw;
            }
        }

        static void SetupAndroid()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Main.unity", true) };
            PlayerSettings.companyName = "Threadlight Studio";
            PlayerSettings.productName = "Bolt Rush";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.boltrush.puzzle");
        }

        public static void BuildAndroid()
        {
            BuildAndroidPlayer("Builds/Android/BoltRush.apk", Array.Empty<string>());
        }

        [MenuItem("BoltRush/Build QA Android")]
        public static void BuildAndroidQA()
        {
            BuildAndroidPlayer("Builds/Android/BoltRush-QA.apk", new[] { "BOLTRUSH_QA" });
        }

        static void BuildAndroidPlayer(string output, string[] defines)
        {
            Directory.CreateDirectory("Builds/Android");
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            // The game boots through RuntimeInitializeOnLoadMethod, so stripping stays off like in ThreadlightBuildTools.
            PlayerSettings.stripEngineCode = false;
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.Android, ManagedStrippingLevel.Disabled);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.None,
                extraScriptingDefines = defines
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new Exception("BoltRush Android build failed: " + report.summary.result);
            UnityEngine.Debug.Log("BoltRush APK built: " + Path.GetFullPath(output));
        }
    }
}
