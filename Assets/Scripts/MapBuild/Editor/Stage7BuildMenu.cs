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

        [MenuItem("Tools/Doom/Build Windows Standalone")]
        public static void BuildWindowsStandalone()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string outDir = Path.Combine(projectRoot, OutputDir);
            Directory.CreateDirectory(outDir);
            string exePath = Path.Combine(outDir, ExeName);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Stage2_MapPreview.unity" },
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
                Debug.Log($"[7e] Windows build OK → {exePath} ({report.summary.totalSize} bytes)");
            else
                Debug.LogError($"[7e] Windows build failed: {report.summary.result}");
        }

        /// CLI: -executeMethod Doom.MapBuild.Editor.Stage7BuildMenu.BuildWindowsStandaloneCli -quit
        public static void BuildWindowsStandaloneCli()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string outDir = Path.Combine(projectRoot, OutputDir);
            Directory.CreateDirectory(outDir);
            string exePath = Path.Combine(outDir, ExeName);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Stage2_MapPreview.unity" },
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            bool ok = report.summary.result == BuildResult.Succeeded;
            if (ok)
                Debug.Log($"[7e] Windows build OK → {exePath} ({report.summary.totalSize} bytes)");
            else
                Debug.LogError($"[7e] Windows build failed: {report.summary.result}");

            if (Application.isBatchMode)
                EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
