#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IronDistrict.EditorTools
{
    /// <summary>Command-line build entry point used by GitHub Actions / local batch builds.</summary>
    public static class CiBuild
    {
        static void SetActiveInputHandlerBoth()
        {
            try
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
                if (assets == null || assets.Length == 0) return;
                var so = new SerializedObject(assets[0]);
                var prop = so.FindProperty("activeInputHandler");
                if (prop != null && prop.intValue != 2)
                {
                    prop.intValue = 2; // 0 = old, 1 = new only, 2 = both
                    so.ApplyModifiedProperties();
                    Debug.Log("[CiBuild] activeInputHandler = Both");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CiBuild] input handler patch skipped: " + e.Message);
            }
        }

        public static void ApplyPlayerSettings()
        {
            PlayerSettings.productName = "Iron District";
            PlayerSettings.companyName = "Iron District";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.allowFullscreenSwitch = true;

            try { PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x); } catch { }
            try { PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone, ApiCompatibilityLevel.NET_Standard_2_0); } catch { }
            try { PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Standalone, ManagedStrippingLevel.Disabled); } catch { }

            SetActiveInputHandlerBoth();
            ShaderGuard.EnsureAlwaysIncludedShaders();
            AssetDatabase.SaveAssets();
        }

        public static void BuildLinux()
        {
            PerformBuild(BuildTarget.StandaloneLinux64);
        }

        public static void BuildWindows()
        {
            PerformBuild(BuildTarget.StandaloneWindows64);
        }

        public static void BuildMac()
        {
            PerformBuild(BuildTarget.StandaloneOSX);
        }

        static void PerformBuild(BuildTarget target)
        {
            try
            {
                ApplyPlayerSettings();

                if (!Directory.Exists("Assets/Scenes"))
                    Directory.CreateDirectory("Assets/Scenes");

                if (!File.Exists(ProjectAutoSetup.ScenePath))
                {
                    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    var go = new GameObject("Game");
                    go.AddComponent<GameRoot>();
                    EditorSceneManager.SaveScene(scene, ProjectAutoSetup.ScenePath);
                    AssetDatabase.Refresh();
                }
                ProjectAutoSetup.EnsureBuildSettings(ProjectAutoSetup.ScenePath);

                string outDir = "build/" + target;
                Directory.CreateDirectory(outDir);
                string path = target switch
                {
                    BuildTarget.StandaloneLinux64 => Path.Combine(outDir, "IronDistrict.x86_64"),
                    BuildTarget.StandaloneWindows64 => Path.Combine(outDir, "IronDistrict.exe"),
                    BuildTarget.StandaloneOSX => Path.Combine(outDir, "IronDistrict.app"),
                    _ => Path.Combine(outDir, "IronDistrict")
                };

                var options = new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                    locationPathName = path,
                    target = target,
                    options = BuildOptions.None
                };

                var report = BuildPipeline.BuildPlayer(options);
                var summary = report.summary;
                Debug.Log($"[CiBuild] result={summary.result} size={summary.totalSize / (1024 * 1024)} MB errors={summary.totalErrors} time={summary.totalTime}");

                if (summary.result != BuildResult.Succeeded)
                {
                    Exit(1);
                    throw new Exception("[CiBuild] Unity build failed: " + summary.result);
                }
                Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[CiBuild] build failed: " + e);
                Exit(2);
            }
        }

        static void Exit(int code)
        {
            // only exit ourselves when running our own docker runner (game-ci handles -quit)
            bool selfExit = System.Environment.GetEnvironmentVariable("IRD_SELF_EXIT") == "1";
            if (Application.isBatchMode && selfExit) EditorApplication.Exit(code);
        }
    }
}
#endif
