using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace Doom.MapBuild.Editor
{
    /// <summary>CLI summary for binary Unity Profiler captures.</summary>
    public static class ProfilerTraceAnalyzer
    {
        sealed class MarkerStats
        {
            public string Name;
            public double TotalMs;
            public double MaxMs;
            public int Frames;
            public int Calls;
        }

        sealed class FrameStats
        {
            public int Index;
            public double TimeMs;
            public List<(string name, double ms)> Samples;
        }

        public static void AnalyzeCli()
        {
            string tracePath = GetArgument("-profilerTracePath");
            string outputPath = GetArgument("-profilerSummaryPath");
            if (string.IsNullOrWhiteSpace(tracePath) || !File.Exists(tracePath))
                throw new FileNotFoundException("Profiler trace not found.", tracePath);

            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = Path.Combine(
                    Path.GetDirectoryName(tracePath) ?? ".",
                    Path.GetFileNameWithoutExtension(tracePath) + "-summary.txt");

            ProfilerDriver.LoadProfile(tracePath, false);

            int firstFrame = ProfilerDriver.firstFrameIndex;
            int lastFrame = ProfilerDriver.lastFrameIndex;
            var markers = new Dictionary<string, MarkerStats>(StringComparer.Ordinal);
            var frames = new List<FrameStats>();

            for (int frameIndex = firstFrame; frameIndex <= lastFrame; frameIndex++)
            {
                using RawFrameDataView frame = ProfilerDriver.GetRawFrameDataView(frameIndex, 0);
                if (!frame.valid) continue;

                int sampleCount = frame.sampleCount;
                var perFrame = new Dictionary<string, (double maxMs, int calls)>(
                    StringComparer.Ordinal);
                var samples = new List<(string name, double ms)>();

                for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                {
                    int markerId = frame.GetSampleMarkerId(sampleIndex);
                    string markerName = frame.GetMarkerName(markerId);
                    double durationMs = frame.GetSampleTimeMs(sampleIndex);
                    if (string.IsNullOrEmpty(markerName) || durationMs <= 0d) continue;

                    if (perFrame.TryGetValue(markerName, out var current))
                        perFrame[markerName] = (Math.Max(current.maxMs, durationMs), current.calls + 1);
                    else
                        perFrame.Add(markerName, (durationMs, 1));

                    if (durationMs >= 0.5d)
                        samples.Add((markerName, durationMs));
                }

                foreach (var pair in perFrame)
                {
                    if (!markers.TryGetValue(pair.Key, out MarkerStats stats))
                    {
                        stats = new MarkerStats { Name = pair.Key };
                        markers.Add(pair.Key, stats);
                    }

                    stats.TotalMs += pair.Value.maxMs;
                    stats.MaxMs = Math.Max(stats.MaxMs, pair.Value.maxMs);
                    stats.Frames++;
                    stats.Calls += pair.Value.calls;
                }

                frames.Add(new FrameStats
                {
                    Index = frameIndex,
                    TimeMs = frame.frameTimeMs,
                    Samples = samples
                        .OrderByDescending(sample => sample.ms)
                        .Take(20)
                        .ToList(),
                });
            }

            WriteSummary(tracePath, outputPath, firstFrame, lastFrame, frames, markers.Values);
            Debug.Log($"[ProfilerTraceAnalyzer] Summary: {outputPath}");

            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }

        static void WriteSummary(
            string tracePath,
            string outputPath,
            int firstFrame,
            int lastFrame,
            List<FrameStats> frames,
            IEnumerable<MarkerStats> markerStats)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Trace: {tracePath}");
            sb.AppendLine($"Frame range: {firstFrame}..{lastFrame}; valid main-thread frames: {frames.Count}");

            if (frames.Count > 0)
            {
                double average = frames.Average(frame => frame.TimeMs);
                double[] sorted = frames.Select(frame => frame.TimeMs).OrderBy(value => value).ToArray();
                sb.AppendLine(
                    $"Frame time ms: avg={F(average)} p50={F(Percentile(sorted, 0.50))} " +
                    $"p95={F(Percentile(sorted, 0.95))} p99={F(Percentile(sorted, 0.99))} " +
                    $"max={F(sorted[^1])}");
            }

            sb.AppendLine();
            sb.AppendLine("Slowest frames (main thread):");
            foreach (FrameStats frame in frames.OrderByDescending(item => item.TimeMs).Take(20))
            {
                sb.AppendLine($"  Frame {frame.Index}: {F(frame.TimeMs)} ms");
                foreach (var sample in frame.Samples.Take(12))
                    sb.AppendLine($"    {F(sample.ms),8} ms  {sample.name}");
            }

            sb.AppendLine();
            sb.AppendLine("Markers by maximum inclusive sample:");
            foreach (MarkerStats marker in markerStats
                         .OrderByDescending(item => item.MaxMs)
                         .Take(80))
            {
                sb.AppendLine(
                    $"  max={F(marker.MaxMs),8} ms avg-present={F(marker.TotalMs / marker.Frames),8} ms " +
                    $"frames={marker.Frames,5} calls={marker.Calls,7}  {marker.Name}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
            File.WriteAllText(outputPath, sb.ToString());
        }

        static double Percentile(double[] values, double percentile)
        {
            if (values.Length == 0) return 0d;
            int index = Mathf.Clamp(
                Mathf.CeilToInt((float)(percentile * values.Length)) - 1,
                0,
                values.Length - 1);
            return values[index];
        }

        static string F(double value) =>
            value.ToString("F3", CultureInfo.InvariantCulture);

        static string GetArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }
    }
}
