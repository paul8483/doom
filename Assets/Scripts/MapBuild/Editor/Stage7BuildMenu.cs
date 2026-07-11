using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Doom.MapBuild.Editor
{
    /// Reproducible Windows standalone build for Stage 7e (output under ignored Builds/).
    public static class Stage7BuildMenu
    {
        const string OutputDir = "Builds/Windows";
        const string ExeName = "DoomUnity.exe";
        const string ProfilingOutputDir = "Builds/WindowsProfile";
        const string ProfilingExeName = "DoomUnityProfile.exe";

        [MenuItem("Tools/Doom/Build Windows Standalone")]
        public static void BuildWindowsStandalone()
        {
            BuildWindows(OutputDir, ExeName, BuildOptions.None, exitBatchMode: false);
        }

        /// CLI: -executeMethod Doom.MapBuild.Editor.Stage7BuildMenu.BuildWindowsStandaloneCli -quit
        public static void BuildWindowsStandaloneCli()
        {
            BuildWindows(OutputDir, ExeName, BuildOptions.None, exitBatchMode: true);
        }

        [MenuItem("Tools/Doom/Build Windows Profiling Standalone")]
        public static void BuildWindowsProfilingStandalone()
        {
            BuildWindows(
                ProfilingOutputDir,
                ProfilingExeName,
                BuildOptions.Development,
                exitBatchMode: false);
        }

        /// CLI: -executeMethod Doom.MapBuild.Editor.Stage7BuildMenu.BuildWindowsProfilingStandaloneCli -quit
        public static void BuildWindowsProfilingStandaloneCli()
        {
            BuildWindows(
                ProfilingOutputDir,
                ProfilingExeName,
                BuildOptions.Development,
                exitBatchMode: true);
        }

        static void BuildWindows(
            string relativeOutputDir,
            string exeName,
            BuildOptions buildOptions,
            bool exitBatchMode)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string outDir = Path.Combine(projectRoot, relativeOutputDir);
            Directory.CreateDirectory(outDir);
            string exePath = Path.Combine(outDir, exeName);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Stage2_MapPreview.unity" },
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = buildOptions,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            bool ok = report.summary.result == BuildResult.Succeeded;
            if (ok)
                Debug.Log($"[7e] Windows build OK → {exePath} " +
                          $"({report.summary.totalSize} bytes, options={buildOptions})");
            else
                Debug.LogError($"[7e] Windows build failed: {report.summary.result}");

            if (exitBatchMode && Application.isBatchMode)
                EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
