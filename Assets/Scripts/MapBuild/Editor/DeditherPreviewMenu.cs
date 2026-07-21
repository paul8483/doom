using System.IO;
using UnityEditor;
using UnityEngine;
using Doom.Graphics;
using Doom.Wad;

namespace Doom.MapBuild.Editor
{
    /// Diagnostic: dumps side-by-side before/after PNGs of DeditherFilter on
    /// representative Freedoom world textures to Logs/dedither-preview/.
    public static class DeditherPreviewMenu
    {
        const int Scale = 4;
        const int Gap = 16;

        static readonly string[] WallNames =
            { "STARTAN2", "BROWN1", "STARG3", "BIGDOOR2", "COMPTALL" };

        static readonly string[] FlatNames =
            { "FLOOR4_8", "NUKAGE1", "FLAT14", "CEIL3_5" };

        [MenuItem("Tools/Doom/Dump Dedither Preview")]
        public static void Dump()
        {
            string wadPath = Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            string outDir = Path.Combine(
                Path.GetDirectoryName(Application.dataPath), "Logs", "dedither-preview");
            Directory.CreateDirectory(outDir);

            using var wad = WadFile.Open(wadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));
            var textures = TextureSet.Load(wad);

            foreach (string name in WallNames)
            {
                if (!textures.Contains(name))
                {
                    Debug.LogWarning($"DeditherPreview: wall texture {name} missing, skipped");
                    continue;
                }
                WriteCompare(textures.Build(name, palette), PixelWrapMode.RepeatX, name, outDir);
            }

            foreach (string name in FlatNames)
            {
                if (wad.FindLump(name) < 0)
                {
                    Debug.LogWarning($"DeditherPreview: flat {name} missing, skipped");
                    continue;
                }
                WriteCompare(Flat.Decode(wad.ReadLump(name), palette), PixelWrapMode.RepeatXY, name, outDir);
            }

            Debug.Log($"DeditherPreview: wrote PNGs to {outDir}");
        }

        static void WriteCompare(DecodedImage before, PixelWrapMode wrap, string name, string outDir)
        {
            // Panels: original | pattern-gated result | gate mask (red = fired).
            var after = DeditherFilter.Apply(
                before, wrap, DeditherFilter.CrossDistanceThreshold, out var mask);

            int matched = 0;
            foreach (bool m in mask)
                if (m) matched++;
            Debug.Log($"DeditherPreview: {name} gate fired on " +
                $"{matched}/{mask.Length} px ({100.0 * matched / mask.Length:F2}%)");

            int w = before.Width, h = before.Height;
            int panelW = w * Scale;
            const int panelCount = 3;
            int outW = panelW * panelCount + Gap * (panelCount - 1);
            int outH = h * Scale;
            var colors = new Color32[outW * outH];
            var gapColor = new Color32(24, 24, 24, 255);
            var maskColor = new Color32(255, 40, 40, 255);

            for (int y = 0; y < outH; y++)
            {
                int srcY = y / Scale;
                // DecodedImage row 0 is the top; Texture2D row 0 is the bottom.
                int texRow = (outH - 1 - y) * outW;
                for (int x = 0; x < outW; x++)
                {
                    int panel = x / (panelW + Gap);
                    int px = x - panel * (panelW + Gap);
                    Color32 c;
                    if (px >= panelW)
                    {
                        c = gapColor;
                    }
                    else
                    {
                        int srcX = px / Scale;
                        if (panel == 0)
                        {
                            var p = before.GetPixel(srcX, srcY);
                            c = new Color32(p.r, p.g, p.b, 255);
                        }
                        else if (panel == 1)
                        {
                            var p = after.GetPixel(srcX, srcY);
                            c = new Color32(p.r, p.g, p.b, 255);
                        }
                        else if (mask[srcY * w + srcX])
                        {
                            c = maskColor;
                        }
                        else
                        {
                            // Dimmed original where the gate stayed closed.
                            var p = before.GetPixel(srcX, srcY);
                            c = new Color32((byte)(p.r / 3), (byte)(p.g / 3), (byte)(p.b / 3), 255);
                        }
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
                    Path.Combine(outDir, $"{name}-compare.png"), tex.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }
    }
}
