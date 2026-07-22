using System.IO;
using UnityEditor;
using UnityEngine;
using Doom.Graphics;
using Doom.Wad;

namespace Doom.MapBuild.Editor
{
    /// Diagnostic: dumps sprite/UI patch comparisons for the Enhanced4X pipeline:
    /// native (nearest) | Super-xBR 4x | 4x + unsharp 0.5 | 4x + unsharp 1.0.
    /// Output: Logs/sprite-preview/. Sharpen here is a prototype for eyeball
    /// calibration before it is promoted into the runtime pipeline.
    public static class SpritePreviewMenu
    {
        // Weapon viewmodels, monsters, pickups, HUD-ish content.
        static readonly string[] LumpNames =
        {
            "PISGA0", "SHTGA0", "CHGGA0",          // weapon viewmodels
            "TROOA1", "POSSA1", "SARGA1",          // monsters
            "MEDIA0", "STIMA0", "SHELA0", "BON1A0" // pickups
        };

        const int DisplayScale = 8; // screen px per native texel in the dump

        [MenuItem("Tools/Doom/Dump Sprite Preview")]
        public static void Dump()
        {
            string wadPath = Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            string outDir = Path.Combine(
                Path.GetDirectoryName(Application.dataPath), "Logs", "sprite-preview");
            Directory.CreateDirectory(outDir);

            using var wad = WadFile.Open(wadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));

            foreach (string name in LumpNames)
            {
                int idx = wad.FindLump(name);
                if (idx < 0)
                {
                    Debug.LogWarning($"SpritePreview: lump {name} missing, skipped");
                    continue;
                }
                WriteCompare(Patch.Decode(wad.ReadLump(idx), palette), name, outDir);
            }

            Debug.Log($"SpritePreview: wrote PNGs to {outDir}");
        }

        static void WriteCompare(DecodedImage native, string name, string outDir)
        {
            var x4 = TextureCache.BuildEnhanced4XDecoded(
                native, PixelWrapMode.Clamp, applyDedither: true, applyAlphaBleed: true);
            var sharpHalf = SharpenFilter.Apply(x4, 0.5f);
            var sharpFull = SharpenFilter.Apply(x4, 1.0f);

            var panels = new[] { native, x4, sharpHalf, sharpFull };
            // Panel width in screen px: native texel * DisplayScale.
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
                        // native is 1x, the rest are 4x: divide accordingly.
                        int div = img.Width == w ? DisplayScale : DisplayScale / 4;
                        var p = img.GetPixel(px / div, y / div);
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
                    Path.Combine(outDir, $"{name}-sharpen.png"), tex.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

    }
}
