using System.IO;
using UnityEditor;
using UnityEngine;
using Doom.Graphics;
using Doom.Wad;

namespace Doom.MapBuild.Editor
{
    /// Diagnostic: dumps side-by-side PNGs comparing native (nearest ×4) with the
    /// Enhanced4X pipeline result (dedither → [bleed] → Super-xBR ×2 ×2) to
    /// Logs/upscale-preview/.
    public static class UpscalePreviewMenu
    {
        static readonly string[] WallNames =
            { "STARTAN2", "BROWN1", "STARG3", "BIGDOOR2", "COMPTALL", "MIDGRATE" };

        static readonly string[] FlatNames = { "FLOOR4_8", "NUKAGE1", "CEIL3_5" };

        [MenuItem("Tools/Doom/Dump Upscale Preview")]
        public static void Dump()
        {
            string wadPath = Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            string outDir = Path.Combine(
                Path.GetDirectoryName(Application.dataPath), "Logs", "upscale-preview");
            Directory.CreateDirectory(outDir);

            using var wad = WadFile.Open(wadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));
            var textures = TextureSet.Load(wad);

            foreach (string name in WallNames)
            {
                if (!textures.Contains(name))
                {
                    Debug.LogWarning($"UpscalePreview: wall texture {name} missing, skipped");
                    continue;
                }
                WriteCompare(textures.Build(name, palette), PixelWrapMode.RepeatX, name, outDir);
            }

            foreach (string name in FlatNames)
            {
                if (wad.FindLump(name) < 0)
                {
                    Debug.LogWarning($"UpscalePreview: flat {name} missing, skipped");
                    continue;
                }
                WriteCompare(Flat.Decode(wad.ReadLump(name), palette), PixelWrapMode.RepeatXY, name, outDir);
            }

            Debug.Log($"UpscalePreview: wrote PNGs to {outDir}");
        }

        static void WriteCompare(DecodedImage native, PixelWrapMode wrap, string name, string outDir)
        {
            bool masked = HasTransparent(native);
            var x4 = TextureCache.BuildEnhanced4XDecoded(
                native, wrap, applyDedither: true, applyAlphaBleed: masked);

            // Left: native at nearest ×4 (what Classic texels look like up close).
            // Right: Super-xBR 4× at 1:1 — same on-screen size.
            int w = native.Width, h = native.Height;
            int panelW = w * 4;
            int outH = h * 4;
            const int gap = 16;
            int outW = panelW * 2 + gap;
            var colors = new Color32[outW * outH];
            var gapColor = new Color32(24, 24, 24, 255);
            var transparentBg = new Color32(60, 30, 60, 255);

            for (int y = 0; y < outH; y++)
            {
                int texRow = (outH - 1 - y) * outW;
                for (int x = 0; x < outW; x++)
                {
                    Color32 c;
                    if (x < panelW)
                    {
                        var p = native.GetPixel(x / 4, y / 4);
                        c = p.a == 0 ? transparentBg : new Color32(p.r, p.g, p.b, 255);
                    }
                    else if (x < panelW + gap)
                    {
                        c = gapColor;
                    }
                    else
                    {
                        var p = x4.GetPixel(x - panelW - gap, y);
                        c = p.a <= 128 ? transparentBg : new Color32(p.r, p.g, p.b, 255);
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
                    Path.Combine(outDir, $"{name}-4x.png"), tex.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        static bool HasTransparent(DecodedImage img)
        {
            for (int i = 3; i < img.Rgba.Length; i += 4)
                if (img.Rgba[i] == 0) return true;
            return false;
        }
    }
}
