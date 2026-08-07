using System.IO;
using UnityEditor;
using UnityEngine;
using Doom.Graphics;
using Doom.Wad;

namespace Doom.MapBuild.Editor
{
    /// Gate 0 diagnostic for Enhanced 3D Objects Toggle: dumps
    /// native | candidate display-redraw | silhouette-registration overlay
    /// panels so the interactive verdict can approve the 2D allowlist and
    /// Point vs Bilinear filtering before any runtime import.
    /// Output: Logs/3d-toggle-gate0/
    public static class ThreeDToggleGatePreviewMenu
    {
        // Representative filter A/B set: weapon / lamp / barrel.
        static readonly string[] FilterLumps = { "SHOTA0", "COLUA0", "BAR1A0" };

        const int DisplayScale = 4;
        const int FilterPanelScale = 8;

        [MenuItem("Tools/Doom/Dump 3D Toggle Gate Preview")]
        public static void Dump()
        {
            DumpCli();
        }

        /// CLI: -executeMethod Doom.MapBuild.Editor.ThreeDToggleGatePreviewMenu.DumpCli -quit
        public static void DumpCli()
        {
            string wadPath = Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            string repoRoot = Path.GetDirectoryName(Application.dataPath);
            string shapeHintsDir = Path.Combine(repoRoot, "Textures", "Trellis2", "ShapeHints");
            string outDir = Path.Combine(repoRoot, "Logs", "3d-toggle-gate0");
            Directory.CreateDirectory(outDir);

            using var wad = WadFile.Open(wadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));

            foreach (string lump in DisplayRedrawAllowlist.Lumps)
            {
                int idx = wad.FindLump(lump);
                if (idx < 0)
                {
                    Debug.LogWarning($"ThreeDToggleGatePreview: lump {lump} missing, skipped");
                    continue;
                }

                string pngPath = Path.Combine(
                    shapeHintsDir, DisplayRedrawAllowlist.ShapeHintFileName(lump));
                if (!File.Exists(pngPath))
                {
                    Debug.LogWarning($"ThreeDToggleGatePreview: {pngPath} missing, skipped");
                    continue;
                }

                var native = Patch.Decode(wad.ReadLump(idx), palette);
                var redraw = DisplayRedrawRegistration.KeyOutLightBackground(LoadPng(pngPath));
                WriteCompare(native, redraw, lump, outDir);
            }

            foreach (string lump in FilterLumps)
            {
                if (!DisplayRedrawAllowlist.Contains(lump)) continue;
                int idx = wad.FindLump(lump);
                string pngPath = Path.Combine(
                    shapeHintsDir, DisplayRedrawAllowlist.ShapeHintFileName(lump));
                if (idx < 0 || !File.Exists(pngPath)) continue;

                var native = Patch.Decode(wad.ReadLump(idx), palette);
                var redraw = DisplayRedrawRegistration.KeyOutLightBackground(LoadPng(pngPath));
                WriteFilterCompare(native, redraw, lump, outDir);
            }

            File.WriteAllText(Path.Combine(outDir, "README.txt"),
                "Gate 0 — Enhanced 3D Objects Toggle\n\n" +
                "Per-lump panels (*-gate.png), left to right:\n" +
                "1. Classic native (nearest xN)\n" +
                "2. Candidate display-redraw (Point, fit to native panel;\n" +
                "   light GPT backdrop keyed to alpha via border flood-fill)\n" +
                "3. Silhouette registration overlay:\n" +
                "   green = native-only, red = redraw-only, yellow = both\n\n" +
                "Filter panels (*-filter.png) for SHOTA0 / COLUA0 / BAR1A0:\n" +
                "1. Native nearest\n" +
                "2. Redraw with Point sampling onto native panel\n" +
                "3. Redraw with Bilinear sampling onto native panel\n");
            Debug.Log($"ThreeDToggleGatePreview: wrote PNGs to {outDir}");
        }

        static DecodedImage LoadPng(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!tex.LoadImage(bytes, markNonReadable: false))
                    throw new IOException($"Failed to decode PNG: {path}");
                int w = tex.width, h = tex.height;
                var pixels = tex.GetPixels32();
                var rgba = new byte[w * h * 4];
                for (int y = 0; y < h; y++)
                {
                    int srcRow = (h - 1 - y) * w;
                    int dstRow = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        Color32 c = pixels[srcRow + x];
                        int o = (dstRow + x) * 4;
                        rgba[o] = c.r;
                        rgba[o + 1] = c.g;
                        rgba[o + 2] = c.b;
                        rgba[o + 3] = c.a;
                    }
                }
                return new DecodedImage(w, h, rgba);
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        static DecodedImage MapRedrawToDisplay(
            DecodedImage redraw, DecodedImage native, int displayScale, bool bilinear)
        {
            var canvas = DisplayRedrawRegistration.NormalizeToCanvas512(redraw);
            DisplayRedrawRegistration.SubjectRect(
                native.Width, native.Height, out int ox, out int oy, out int sw, out int sh, out _);

            int dw = native.Width * displayScale;
            int dh = native.Height * displayScale;
            var rgba = new byte[dw * dh * 4];
            for (int y = 0; y < dh; y++)
            {
                float vNative = (y + 0.5f) / dh;
                float cy = oy + vNative * sh;
                for (int x = 0; x < dw; x++)
                {
                    float uNative = (x + 0.5f) / dw;
                    float cx = ox + uNative * sw;
                    var p = bilinear
                        ? SampleBilinear(canvas, (cx + 0.5f) / DisplayRedrawRegistration.CanvasSize,
                            (cy + 0.5f) / DisplayRedrawRegistration.CanvasSize)
                        : SamplePoint(canvas, (int)cx, (int)cy);
                    int o = (y * dw + x) * 4;
                    rgba[o] = p.r;
                    rgba[o + 1] = p.g;
                    rgba[o + 2] = p.b;
                    rgba[o + 3] = p.a;
                }
            }
            return new DecodedImage(dw, dh, rgba);
        }

        static (byte r, byte g, byte b, byte a) SamplePoint(DecodedImage img, int x, int y)
        {
            if (x < 0 || y < 0 || x >= img.Width || y >= img.Height)
                return (0, 0, 0, 0);
            return img.GetPixel(x, y);
        }

        static (byte r, byte g, byte b, byte a) SampleBilinear(DecodedImage img, float u, float v)
        {
            float x = u * (img.Width - 1);
            float y = v * (img.Height - 1);
            int x0 = (int)System.Math.Floor(x);
            int y0 = (int)System.Math.Floor(y);
            int x1 = System.Math.Min(x0 + 1, img.Width - 1);
            int y1 = System.Math.Min(y0 + 1, img.Height - 1);
            float fx = x - x0, fy = y - y0;
            var c00 = img.GetPixel(x0, y0);
            var c10 = img.GetPixel(x1, y0);
            var c01 = img.GetPixel(x0, y1);
            var c11 = img.GetPixel(x1, y1);
            return (
                Lerp4(c00.r, c10.r, c01.r, c11.r, fx, fy),
                Lerp4(c00.g, c10.g, c01.g, c11.g, fx, fy),
                Lerp4(c00.b, c10.b, c01.b, c11.b, fx, fy),
                Lerp4(c00.a, c10.a, c01.a, c11.a, fx, fy));
        }

        static byte Lerp4(byte a, byte b, byte c, byte d, float fx, float fy)
        {
            float top = a + (b - a) * fx;
            float bot = c + (d - c) * fx;
            return (byte)System.Math.Clamp((int)(top + (bot - top) * fy + 0.5f), 0, 255);
        }

        static DecodedImage BuildOverlay(DecodedImage nativeDisp, DecodedImage redrawDisp)
        {
            int w = nativeDisp.Width, h = nativeDisp.Height;
            var rgba = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool n = nativeDisp.GetPixel(x, y).a > 128;
                    bool r = redrawDisp.GetPixel(
                        System.Math.Min(x, redrawDisp.Width - 1),
                        System.Math.Min(y, redrawDisp.Height - 1)).a > 128;
                    byte rr, gg, bb, aa;
                    if (n && r) { rr = 220; gg = 200; bb = 40; aa = 255; }
                    else if (n) { rr = 40; gg = 200; bb = 60; aa = 255; }
                    else if (r) { rr = 220; gg = 50; bb = 50; aa = 255; }
                    else { rr = 0; gg = 0; bb = 0; aa = 0; }
                    int o = (y * w + x) * 4;
                    rgba[o] = rr; rgba[o + 1] = gg; rgba[o + 2] = bb; rgba[o + 3] = aa;
                }
            }
            return new DecodedImage(w, h, rgba);
        }

        static void WriteCompare(
            DecodedImage native, DecodedImage redraw, string label, string outDir)
        {
            var nativeDisp = DisplayRedrawRegistration.ScaleNearest(native, DisplayScale);
            var redrawDisp = MapRedrawToDisplay(redraw, native, DisplayScale, bilinear: false);
            var overlay = BuildOverlay(nativeDisp, redrawDisp);
            WriteRow(new[] { nativeDisp, redrawDisp, overlay },
                Path.Combine(outDir, $"{label}-gate.png"));
        }

        static void WriteFilterCompare(
            DecodedImage native, DecodedImage redraw, string label, string outDir)
        {
            var nativeDisp = DisplayRedrawRegistration.ScaleNearest(native, FilterPanelScale);
            var point = MapRedrawToDisplay(redraw, native, FilterPanelScale, bilinear: false);
            var bilinear = MapRedrawToDisplay(redraw, native, FilterPanelScale, bilinear: true);
            WriteRow(new[] { nativeDisp, point, bilinear },
                Path.Combine(outDir, $"{label}-filter.png"));
        }

        static void WriteRow(DecodedImage[] panels, string path)
        {
            int panelW = panels[0].Width;
            int outH = panels[0].Height;
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
                    if (panel >= panels.Length || px >= panelW)
                    {
                        c = gapColor;
                    }
                    else
                    {
                        var img = panels[panel];
                        int sx = System.Math.Min(px, img.Width - 1);
                        int sy = System.Math.Min(y, img.Height - 1);
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
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }
    }
}
