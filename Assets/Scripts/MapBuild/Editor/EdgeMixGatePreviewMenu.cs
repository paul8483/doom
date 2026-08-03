using System.IO;
using UnityEditor;
using UnityEngine;
using Doom.Graphics;
using Doom.Wad;

namespace Doom.MapBuild.Editor
{
    /// Stage-2 Gate 0 diagnostic: dumps contrast-gated EdgeMix sweeps as PNG
    /// panels so the interactive verdict can pick a ramp point (or stop the
    /// stage) before any runtime change. Column order per image:
    /// native (nearest) | accepted EdgeMix 8x | gated sweep points.
    /// Output: Logs/edge-mix-contrast-gate0/.
    public static class EdgeMixGatePreviewMenu
    {
        // Standard interactive-verdict set (plan 2026-07-31, stage 2 protocol):
        // medkit, stimpack, armors, key, monsters, shotgun pickup + viewmodel.
        static readonly string[] LumpNames =
        {
            "MEDIA0", "STIMA0",
            "ARM1A0", "BON2A0",
            "RKEYA0",
            "SARGA1", "POSSA1",
            "SHOTA0", "SHTGA0",
        };

        // (rampStart, rampEnd) in weighted RGB distance units, 0..255.
        static readonly (int Start, int End)[] Sweep =
        {
            (16, 64),   // aggressive: most interior edges go hard
            (32, 112),  // middle
            (64, 176),  // conservative: only the strongest contrast goes hard
        };

        const int DisplayScale = 8; // screen px per native texel

        [MenuItem("Tools/Doom/Dump EdgeMix Gate Preview")]
        public static void Dump()
        {
            string wadPath = Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            string outDir = Path.Combine(
                Path.GetDirectoryName(Application.dataPath), "Logs", "edge-mix-contrast-gate0");
            Directory.CreateDirectory(outDir);

            using var wad = WadFile.Open(wadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));

            foreach (string name in LumpNames)
            {
                int idx = wad.FindLump(name);
                if (idx < 0)
                {
                    Debug.LogWarning($"EdgeMixGatePreview: lump {name} missing, skipped");
                    continue;
                }
                WriteCompare(Patch.Decode(wad.ReadLump(idx), palette), name, outDir);
            }

            File.WriteAllText(Path.Combine(outDir, "README.txt"),
                "Columns, left to right:\n" +
                "1. Classic native (nearest, reference)\n" +
                "2. Accepted EdgeMix 8x (current Enhanced runtime)\n" +
                string.Concat(System.Linq.Enumerable.Select(Sweep, (s, i) =>
                    $"{i + 3}. Gated EdgeMix 8x ramp {s.Start}->{s.End}\n")));
            Debug.Log($"EdgeMixGatePreview: wrote PNGs to {outDir}");
        }

        static void WriteCompare(DecodedImage native, string name, string outDir)
        {
            var accepted = EdgeMixUpscaler.Scale8X(native);
            var panels = new DecodedImage[2 + Sweep.Length];
            panels[0] = native;
            panels[1] = accepted;
            for (int i = 0; i < Sweep.Length; i++)
                panels[2 + i] = EdgeMixUpscaler.Scale8XGated(native, Sweep[i].Start, Sweep[i].End);

            int w = native.Width, h = native.Height;
            int panelW = w * DisplayScale;
            int outH = h * DisplayScale;
            const int gap = 12;
            int outW = panelW * panels.Length + gap * (panels.Length - 1);
            var colors = new Color32[outW * outH];
            var gapColor = new Color32(24, 24, 24, 255);
            var bg = new Color32(60, 30, 60, 255);

            for (int y = 0; y < outH; y++)
            {
                int texRow = (outH - 1 - y) * outW;
                for (int x = 0; x < outW; x++)
                {
                    int panel = x / (panelW + gap);
                    int px = x - panel * (panelW + gap);
                    Color32 c;
                    if (px >= panelW)
                    {
                        c = gapColor;
                    }
                    else
                    {
                        var img = panels[panel];
                        // native is 1x, the rest are 8x: EdgeMix output maps 1:1.
                        int div = img.Width == w ? DisplayScale : DisplayScale / EdgeMixUpscaler.Scale;
                        int sx = div == 0 ? px : px / div;
                        int sy = div == 0 ? y : y / div;
                        var p = img.GetPixel(sx, sy);
                        c = p.a <= 128 ? bg : new Color32(p.r, p.g, p.b, 255);
                    }
                    colors[texRow + x] = c;
                }
            }

            var tex = new Texture2D(outW, outH, TextureFormat.RGBA32, false);
            try
            {
                tex.SetPixels32(colors);
                tex.Apply(false);
                File.WriteAllBytes(
                    Path.Combine(outDir, $"{name}-gate.png"), tex.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }
    }
}
