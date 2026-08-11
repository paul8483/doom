using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Doom.MapBuild.Editor
{
    /// Reproducible standalone builds (Stage 7e / Stage 8).
    /// Menu path kept stable; output under ignored Builds/.
    public static class Stage7BuildMenu
    {
        const string WindowsOutputDir = "Builds/Windows";
        const string WindowsExeName = "DoomUnity.exe";
        const string LinuxOutputDir = "Builds/Linux";
        const string LinuxExeName = "DoomUnity.x86_64";
        const string ProfilingOutputDir = "Builds/WindowsProfile";
        const string ProfilingExeName = "DoomUnityProfile.exe";

        static readonly string[] RequiredDoomShaders =
        {
            "Doom/ClassicOpaque",
            "Doom/ClassicCutout",
            "Doom/EnhancedWorld",
            "Doom/EnhancedCutout",
            "Doom/EnhancedSprite",
            "Doom/Spectre",
            "Doom/Sky",
            "Doom/Fluid",
        };

        [MenuItem("Tools/Doom/Build Windows Standalone")]
        public static void BuildWindowsStandalone()
        {
            BuildStandalone(
                WindowsOutputDir,
                WindowsExeName,
                BuildTarget.StandaloneWindows64,
                BuildOptions.None,
                exitBatchMode: false);
        }

        /// CLI: -executeMethod Doom.MapBuild.Editor.Stage7BuildMenu.BuildWindowsStandaloneCli -quit
        public static void BuildWindowsStandaloneCli()
        {
            BuildStandalone(
                WindowsOutputDir,
                WindowsExeName,
                BuildTarget.StandaloneWindows64,
                BuildOptions.None,
                exitBatchMode: true);
        }

        [MenuItem("Tools/Doom/Build Linux Standalone")]
        public static void BuildLinuxStandalone()
        {
            BuildStandalone(
                LinuxOutputDir,
                LinuxExeName,
                BuildTarget.StandaloneLinux64,
                BuildOptions.None,
                exitBatchMode: false);
        }

        /// CLI: -executeMethod Doom.MapBuild.Editor.Stage7BuildMenu.BuildLinuxStandaloneCli -quit
        public static void BuildLinuxStandaloneCli()
        {
            BuildStandalone(
                LinuxOutputDir,
                LinuxExeName,
                BuildTarget.StandaloneLinux64,
                BuildOptions.None,
                exitBatchMode: true);
        }

        [MenuItem("Tools/Doom/Build Windows Profiling Standalone")]
        public static void BuildWindowsProfilingStandalone()
        {
            BuildStandalone(
                ProfilingOutputDir,
                ProfilingExeName,
                BuildTarget.StandaloneWindows64,
                BuildOptions.Development,
                exitBatchMode: false);
        }

        /// CLI: -executeMethod Doom.MapBuild.Editor.Stage7BuildMenu.BuildWindowsProfilingStandaloneCli -quit
        public static void BuildWindowsProfilingStandaloneCli()
        {
            BuildStandalone(
                ProfilingOutputDir,
                ProfilingExeName,
                BuildTarget.StandaloneWindows64,
                BuildOptions.Development,
                exitBatchMode: true);
        }

        static void BuildStandalone(
            string relativeOutputDir,
            string exeName,
            BuildTarget target,
            BuildOptions buildOptions,
            bool exitBatchMode)
        {
            // CLI/external tool edits (e.g. TRELLIS albedo paint) need an import pass
            // before BuildPlayer or the player can ship stale Library textures.
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            if (!PreflightBuildInclusion(out string preflightError))
            {
                Debug.LogError($"[Stage8] Build aborted: {preflightError}");
                if (exitBatchMode && Application.isBatchMode)
                    EditorApplication.Exit(1);
                return;
            }

            var targetGroup = BuildPipeline.GetBuildTargetGroup(target);
            if (!BuildPipeline.IsBuildTargetSupported(targetGroup, target))
            {
                Debug.LogError(
                    $"[Stage8] Build aborted: {target} is not supported " +
                    $"(install Linux/Windows Build Support for this Editor)");
                if (exitBatchMode && Application.isBatchMode)
                    EditorApplication.Exit(1);
                return;
            }

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string outDir = Path.Combine(projectRoot, relativeOutputDir);
            Directory.CreateDirectory(outDir);
            string exePath = Path.Combine(outDir, exeName);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Stage2_MapPreview.unity" },
                locationPathName = exePath,
                target = target,
                options = buildOptions,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            bool ok = report.summary.result == BuildResult.Succeeded;
            if (ok)
                Debug.Log($"[Stage8] {target} build OK → {exePath} " +
                          $"({report.summary.totalSize} bytes, options={buildOptions})");
            else
                Debug.LogError($"[Stage8] {target} build failed: {report.summary.result}");

            if (exitBatchMode && Application.isBatchMode)
                EditorApplication.Exit(ok ? 0 : 1);
        }

        /// Stage 8 Task 15: refuse to ship if URP/shaders/WAD/scene are missing.
        public static bool PreflightBuildInclusion(out string error)
        {
            error = null;

            if (GraphicsSettings.defaultRenderPipeline == null)
            {
                error = "GraphicsSettings.defaultRenderPipeline is null (URP not assigned)";
                return false;
            }

            if (QualitySettings.renderPipeline == null)
            {
                error = "QualitySettings.renderPipeline is null (URP not on active quality)";
                return false;
            }

            string wadPath = Path.Combine(
                Application.dataPath, "StreamingAssets", "wads", "freedoom1.wad");
            if (!File.Exists(wadPath))
            {
                error = $"Missing StreamingAssets WAD: {wadPath}";
                return false;
            }

            if (!File.Exists(Path.Combine(Application.dataPath, "Scenes", "Stage2_MapPreview.unity")))
            {
                error = "Missing build scene Assets/Scenes/Stage2_MapPreview.unity";
                return false;
            }

            foreach (string name in RequiredDoomShaders)
            {
                if (Shader.Find(name) == null)
                {
                    error =
                        $"Shader '{name}' not found (add to Always Included Shaders / import)";
                    return false;
                }
            }

            return true;
        }
    }
}
