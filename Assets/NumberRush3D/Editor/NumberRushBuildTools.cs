using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace NumberRush3D.EditorTools
{
    /// <summary>
    /// Android player-settings preset + one click APK / AAB builds.
    /// All of these values are explained in /Documentation/06-Android-Build.md.
    /// </summary>
    public static class NumberRushBuildTools
    {
        public const string BuildFolder = "Builds/Android";
        public const string BundleIdentifier = "com.yourstudio.numberrush3d";

        [MenuItem("Tools/NUMBER RUSH 3D/2. Configure Android Player Settings", false, 2)]
        public static void ConfigureAndroidMenu()
        {
            ConfigureAndroid(true);
            EditorUtility.DisplayDialog(
                "NUMBER RUSH 3D",
                "Android player settings applied:\n\n" +
                "- Landscape only\n" +
                "- IL2CPP / ARM64\n" +
                "- Vulkan + OpenGLES3\n" +
                "- Linear colour space\n" +
                "- Min API 26\n\n" +
                "Bundle id: " + BundleIdentifier + "\n(change it before publishing!)",
                "OK");
        }

        [MenuItem("Tools/NUMBER RUSH 3D/3. Build Android APK", false, 3)]
        public static void BuildApkMenu()
        {
            Build(false);
        }

        [MenuItem("Tools/NUMBER RUSH 3D/4. Build Android App Bundle (Play Store)", false, 4)]
        public static void BuildAabMenu()
        {
            Build(true);
        }

        [MenuItem("Tools/NUMBER RUSH 3D/6. Project Setup Checklist", false, 30)]
        public static void LogChecklist()
        {
            RenderPipelineAsset pipeline = GraphicsSettings.defaultRenderPipeline != null
                ? GraphicsSettings.defaultRenderPipeline
                : QualitySettings.renderPipeline;

            string pipelineLine = pipeline == null
                ? "Render pipeline : BUILT-IN (no SRP asset assigned). The generator falls back to the Standard shader. Assign a URP asset in Project Settings > Graphics for the intended URP setup."
                : $"Render pipeline : {pipeline.GetType().Name}";

            string inputLine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            inputLine = "Input backend   : Input System package (new)";
#elif ENABLE_LEGACY_INPUT_MANAGER
            inputLine = "Input backend   : Input Manager (legacy) or Both";
#else
            inputLine = "Input backend   : NONE - swipes will not work. Enable an input backend in Project Settings > Player > Active Input Handling.";
#endif

            string report =
                "[NumberRush] Project checklist\n" +
                pipelineLine + "\n" +
                inputLine + "\n" +
                $"Enemy layer     : {(LayerMask.NameToLayer(NumberRushBuilder.EnemyLayerName) >= 0 ? "OK" : "MISSING - run step 1")}\n" +
                $"Scene           : {(File.Exists(NumberRushBuilder.ScenePath) ? "OK" : "MISSING - run step 1")}\n" +
                $"Build target    : {EditorUserBuildSettings.activeBuildTarget}\n" +
                $"Bundle id       : {PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)}\n" +
                $"Graphics APIs   : {string.Join(", ", PlayerSettings.GetGraphicsAPIs(BuildTarget.Android))}\n" +
                $"Scripting       : {PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android)} / {PlayerSettings.Android.targetArchitectures}\n" +
                $"Min SDK         : {PlayerSettings.Android.minSdkVersion}";

            Debug.Log(report);
            EditorUtility.DisplayDialog("NUMBER RUSH 3D", report, "OK");
        }

        [MenuItem("Tools/NUMBER RUSH 3D/5. Open Game Scene", false, 20)]
        public static void OpenGameSceneMenu()
        {
            if (!File.Exists(NumberRushBuilder.ScenePath))
            {
                EditorUtility.DisplayDialog("NUMBER RUSH 3D", "Scene not found. Run step 1 first.", "OK");
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(NumberRushBuilder.ScenePath, OpenSceneMode.Single);
            }
        }

        /// <summary>Applies the mobile preset. Safe to call from a build script.</summary>
        public static void ConfigureAndroid(bool switchBuildTarget)
        {
            if (switchBuildTarget && EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }

            PlayerSettings.companyName = "Number Rush Studio";
            PlayerSettings.productName = "NUMBER RUSH 3D";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleIdentifier);

            // --- Orientation: landscape only ------------------------------------------------
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.useAnimatedAutorotation = false;

            // --- Rendering ------------------------------------------------------------------
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[]
            {
                GraphicsDeviceType.Vulkan,
                GraphicsDeviceType.OpenGLES3
            });

            PlayerSettings.gpuSkinning = true;
            PlayerSettings.stripEngineCode = true;
            QualitySettings.vSyncCount = 0;

            // --- Scripting ------------------------------------------------------------------
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Medium);

            // --- Android specifics ----------------------------------------------------------
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.optimizedFramePacing = true;
            PlayerSettings.Android.forceInternetPermission = false;
            PlayerSettings.Android.forceSDCardPermission = false;
            PlayerSettings.Android.blitType = AndroidBlitType.Always;

            AssetDatabase.SaveAssets();

            Debug.Log(
                "[NumberRush] Android settings applied (landscape, IL2CPP/ARM64, Vulkan+GLES3, linear, min API 26).\n" +
                $"Remember to change the bundle id '{BundleIdentifier}' before publishing.");
        }

        private static void Build(bool appBundle)
        {
            string scenePath = NumberRushBuilder.ScenePath;
            if (!File.Exists(scenePath))
            {
                EditorUtility.DisplayDialog(
                    "NUMBER RUSH 3D",
                    "Scene not found at:\n" + scenePath + "\n\nRun 'Tools > NUMBER RUSH 3D > 1. Build Materials, Prefabs and Scene' first.",
                    "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            ConfigureAndroid(true);

            Directory.CreateDirectory(BuildFolder);
            EditorUserBuildSettings.buildAppBundle = appBundle;

            string extension = appBundle ? ".aab" : ".apk";
            var options = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = $"{BuildFolder}/NumberRush3D{extension}",
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[NumberRush] Build succeeded: {options.locationPathName} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:F1}s)");
                EditorUtility.RevealInFinder(options.locationPathName);
            }
            else
            {
                Debug.LogError($"[NumberRush] Build failed: {summary.result} with {summary.totalErrors} error(s). See the Console.");
            }
        }
    }
}
