using System.IO;
using UnityEditor;
using UnityEngine;
using Doom.Graphics;
using Doom.Wad;

namespace Doom.MapBuild.Editor
{
    /// Gate 1 panels: EdgeMix | display-redraw (if any) | mesh albedo contact sheet
    /// for the interactive standalone acceptance set.
    /// Output: Logs/3d-toggle-gate1/
    public static class ThreeDToggleGate1PreviewMenu
    {
        // Protocol set: medkit/armor/monster/shotgun + lamp/barrel/tree.
        static readonly (string Lump, string Label, bool ExpectRedraw, bool ExpectMesh)[] Rows =
        {
            ("MEDIA0", "MEDIA0-medikit", false, true),
            ("ARM1A0", "ARM1A0-armor", true, false),
            ("POSSA1", "POSSA1-monster", false, false),
            ("SHOTA0", "SHOTA0-shotgun", true, true),
            ("COLUA0", "COLUA0-lamp", true, true),
            ("BAR1A0", "BAR1A0-barrel", true, true), // redraw gated by BAR1 animation at runtime
            ("TRE2A0", "TRE2A0-tree", false, true),
        };

        const int DisplayScale = 4;

        [MenuItem("Tools/Doom/Dump 3D Toggle Gate 1 Preview")]
        public static void Dump() => DumpCli();

        /// CLI: -executeMethod Doom.MapBuild.Editor.ThreeDToggleGate1PreviewMenu.DumpCli -quit
        public static void DumpCli()
        {
            string wadPath = Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            string repoRoot = Path.GetDirectoryName(Application.dataPath);
            string outDir = Path.Combine(repoRoot, "Logs", "3d-toggle-gate1");
            Directory.CreateDirectory(outDir);

            using var wad = WadFile.Open(wadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));

            foreach (var (lump, label, expectRedraw, expectMesh) in Rows)
            {
                int idx = wad.FindLump(lump);
                if (idx < 0)
                {
                    Debug.LogWarning($"Gate1Preview: missing lump {lump}");
                    continue;
                }

                var native = Patch.Decode(wad.ReadLump(idx), palette);
                var edge = EdgeMixUpscaler.Scale8XContrastGated(native);
                DecodedImage redraw = null;
                if (expectRedraw && DisplayRedrawAllowlist.Contains(lump))
                {
                    var res = Resources.Load<Texture2D>(
                        DisplayRedrawAllowlist.ResourcesPath(lump));
                    if (res != null)
                    {
                        var canvas = TextureToDecoded(res);
                        redraw = DisplayRedrawRegistration.MapRedrawToNativeRect(canvas, native);
                        redraw = DisplayRedrawRegistration.ScaleNearest(
                            redraw, EdgeMixUpscaler.Scale);
                    }
                }

                string meshNote = expectMesh
                    ? Path.Combine("Assets", "Resources", "ExperimentalPickups", lump)
                    : "(no mesh)";
                WriteCompare(native, edge, redraw, label, meshNote, outDir);
            }

            File.WriteAllText(Path.Combine(outDir, "README.txt"),
                "Gate 1 — Enhanced 3D Objects Toggle\n\n" +
                "Columns left→right:\n" +
                "1. Classic native (nearest)\n" +
                "2. EdgeMix 8× (Enhanced 2D fallback)\n" +
                "3. Display-redraw (if allowlisted; else empty panel)\n\n" +
                "Mesh presence is noted in the filename / README row — live mesh\n" +
                "must be judged in standalone (Enhanced + 3D Objects On).\n");
            Debug.Log($"Gate1Preview: wrote PNGs to {outDir}");
        }

        static DecodedImage TextureToDecoded(Texture2D tex)
        {
            var pixels = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            var rgba = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            {
                int srcRow = (h - 1 - y) * w;
                int dstRow = y * w;
                for (int x = 0; x < w; x++)
                {
                    Color32 c = pixels[srcRow + x];
                    int o = (dstRow + x) * 4;
                    rgba[o] = c.r; rgba[o + 1] = c.g; rgba[o + 2] = c.b; rgba[o + 3] = c.a;
                }
            }
            return new DecodedImage(w, h, rgba);
        }

        static void WriteCompare(
            DecodedImage native,
            DecodedImage edge,
            DecodedImage redrawOrNull,
            string label,
            string meshNote,
            string outDir)
        {
            var nativeDisp = DisplayRedrawRegistration.ScaleNearest(native, DisplayScale);
            // Edge is already 8×; scale display to match nativeDisp height.
            int targetH = nativeDisp.Height;
            var panels = new System.Collections.Generic.List<DecodedImage>
            {
                nativeDisp,
                FitHeight(edge, targetH),
            };
            if (redrawOrNull != null)
                panels.Add(FitHeight(redrawOrNull, targetH));
            else
                panels.Add(EmptyPanel(nativeDisp.Width, targetH));

            WriteRow(panels.ToArray(), Path.Combine(outDir, $"{label}.png"));
            File.AppendAllText(Path.Combine(outDir, "README.txt"),
                $"{label}: mesh={meshNote}; redraw={(redrawOrNull != null ? "yes" : "no")}\n");
        }

        static DecodedImage EmptyPanel(int w, int h)
        {
            var rgba = new byte[w * h * 4];
            return new DecodedImage(w, h, rgba);
        }

        static DecodedImage FitHeight(DecodedImage src, int targetH)
        {
            if (src.Height == targetH) return src;
            // Nearest scale so height matches (EdgeMix is 8× native; DisplayScale=4 → half).
            float scale = (float)targetH / src.Height;
            int dw = Mathf.Max(1, Mathf.RoundToInt(src.Width * scale));
            int dh = targetH;
            var rgba = new byte[dw * dh * 4];
            for (int y = 0; y < dh; y++)
            {
                int sy = Mathf.Clamp((int)(y / scale), 0, src.Height - 1);
                for (int x = 0; x < dw; x++)
                {
                    int sx = Mathf.Clamp((int)(x / scale), 0, src.Width - 1);
                    var p = src.GetPixel(sx, sy);
                    int o = (y * dw + x) * 4;
                    rgba[o] = p.r; rgba[o + 1] = p.g; rgba[o + 2] = p.b; rgba[o + 3] = p.a;
                }
            }
            return new DecodedImage(dw, dh, rgba);
        }

        static void WriteRow(DecodedImage[] panels, string path)
        {
            int panelW = 0;
            foreach (var p in panels) panelW = Mathf.Max(panelW, p.Width);
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
                        c = gapColor;
                    else
                    {
                        var img = panels[panel];
                        if (px >= img.Width || y >= img.Height)
                            c = bg;
                        else
                        {
                            var p = img.GetPixel(px, y);
                            c = p.a <= 128 ? bg : new Color32(p.r, p.g, p.b, 255);
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
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }
    }
}
