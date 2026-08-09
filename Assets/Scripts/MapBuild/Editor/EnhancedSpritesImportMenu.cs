using System.IO;
using UnityEditor;
using UnityEngine;
using Doom.Graphics;
using Doom.Wad;

namespace Doom.MapBuild.Editor
{
    /// Imports Gate 0 approved display-redraws into
    /// Assets/Resources/EnhancedSprites/&lt;LUMP&gt;.png: key out GPT backdrop,
    /// normalize to 512×512, Point filter + cutout alpha + mips.
    public static class EnhancedSpritesImportMenu
    {
        const string ResourcesRel = "Assets/Resources/EnhancedSprites";

        [MenuItem("Tools/Doom/Import EnhancedSprites")]
        public static void Import()
        {
            ImportCli();
        }

        /// CLI: -executeMethod Doom.MapBuild.Editor.EnhancedSpritesImportMenu.ImportCli -quit
        public static void ImportCli()
        {
            string repoRoot = Path.GetDirectoryName(Application.dataPath);
            string shapeHintsDir = Path.Combine(repoRoot, "Textures", "Trellis2", "ShapeHints");
            string absOutDir = Path.Combine(Application.dataPath, "Resources", "EnhancedSprites");
            Directory.CreateDirectory(absOutDir);

            int imported = 0;
            using var wad = OpenFreedoom();
            foreach (string lump in DisplayRedrawAllowlist.Lumps)
            {
                string src = Path.Combine(shapeHintsDir, DisplayRedrawAllowlist.ShapeHintFileName(lump));
                if (!File.Exists(src))
                {
                    Debug.LogError($"EnhancedSpritesImport: missing source {src}");
                    continue;
                }

                var keyed = DisplayRedrawRegistration.KeyOutLightBackground(LoadPng(src));
                DecodedImage canvas;
                if (DisplayRedrawAllowlist.UsesAggressiveKey(lump))
                {
                    keyed = DisplayRedrawRegistration.KeyOutBackdropAggressive(keyed);
                    keyed = DisplayRedrawRegistration.RecolorLightEdgeRing(keyed);
                    // Tree shapehints overflow the ≤416 px subject contract;
                    // fit their silhouette into the runtime subject rect.
                    var header = ReadPatchHeader(wad, lump);
                    canvas = DisplayRedrawRegistration.NormalizeSubjectToRect(
                        keyed, header.Width, header.Height);
                }
                else
                {
                    canvas = DisplayRedrawRegistration.NormalizeToCanvas512(keyed);
                }
                canvas = DisplayRedrawRegistration.PadTransparentRgb(canvas);
                string dst = Path.Combine(absOutDir, lump + ".png");
                WritePng(canvas, dst);
                imported++;
            }

            AssetDatabase.Refresh();
            ConfigureImporters();
            AssetDatabase.SaveAssets();
            Debug.Log($"EnhancedSpritesImport: imported {imported}/{DisplayRedrawAllowlist.Lumps.Length} → {ResourcesRel}");
        }

        static WadFile OpenFreedoom() =>
            WadFile.Open(Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad"));

        static PatchHeader ReadPatchHeader(WadFile wad, string lump)
        {
            int idx = wad.FindLump(lump);
            if (idx < 0)
                throw new IOException($"EnhancedSpritesImport: lump {lump} not in WAD");
            return Patch.ReadHeader(wad.ReadLump(idx));
        }

        /// Diagnostic: write the exact texture the runtime redraw path samples
        /// (Resources → ExtractSubjectRect) to Logs/redraw-runtime/<LUMP>.png.
        /// CLI: -executeMethod Doom.MapBuild.Editor.EnhancedSpritesImportMenu.DumpRuntimeTexturesCli -quit
        public static void DumpRuntimeTexturesCli()
        {
            string repoRoot = Path.GetDirectoryName(Application.dataPath);
            string outDir = Path.Combine(repoRoot, "Logs", "redraw-runtime");
            Directory.CreateDirectory(outDir);
            using var wad = OpenFreedoom();
            foreach (string lump in DisplayRedrawAllowlist.Lumps)
            {
                var res = Resources.Load<Texture2D>(DisplayRedrawAllowlist.ResourcesPath(lump));
                if (res == null) continue;
                var header = ReadPatchHeader(wad, lump);
                var canvas = LoadTexture(res);
                var subject = DisplayRedrawRegistration.ExtractSubjectRect(
                    canvas, header.Width, header.Height);
                WritePng(subject, Path.Combine(outDir, lump + ".png"));
            }
            Debug.Log($"EnhancedSpritesImport: runtime dump → {outDir}");
        }

        static DecodedImage LoadTexture(Texture2D tex)
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
                    rgba[o] = c.r;
                    rgba[o + 1] = c.g;
                    rgba[o + 2] = c.b;
                    rgba[o + 3] = c.a;
                }
            }
            return new DecodedImage(w, h, rgba);
        }

        static void ConfigureImporters()
        {
            foreach (string lump in DisplayRedrawAllowlist.Lumps)
            {
                string assetPath = $"{ResourcesRel}/{lump}.png";
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogWarning($"EnhancedSpritesImport: no importer for {assetPath}");
                    continue;
                }

                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Point; // Gate 0
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.anisoLevel = 1;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.isReadable = true; // EditMode silhouette / registration tests
                importer.SaveAndReimport();
            }
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

        static void WritePng(DecodedImage img, string path)
        {
            var colors = new Color32[img.Width * img.Height];
            for (int y = 0; y < img.Height; y++)
            {
                int texRow = (img.Height - 1 - y) * img.Width;
                for (int x = 0; x < img.Width; x++)
                {
                    var p = img.GetPixel(x, y);
                    colors[texRow + x] = new Color32(p.r, p.g, p.b, p.a);
                }
            }

            var tex = new Texture2D(img.Width, img.Height, TextureFormat.RGBA32, false);
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
