using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Graphics;
using Doom.Wad;

namespace Doom.Map.Tests
{
    /// World redraw assets (Resources/EnhancedWorld): every allowlisted texture
    /// ships a PNG at exactly its authoring scale (4x; SKY1 8x) times the
    /// native composite size, fully opaque, with
    /// a horizontal wrap seam no worse than the native's own (walls tile along
    /// the map). Decoding goes through raw PNG bytes so the suite stays green
    /// under -nographics.
    public class WorldRedrawAssetTests
    {
        static string FreedoomPath => Path.Combine(
            Application.streamingAssetsPath, "wads", "freedoom1.wad");

        static string RedrawDir => Path.Combine(
            Application.dataPath, "Resources", WorldRedrawAllowlist.ResourcesFolder);

        const double MaxSeamRatio = 2.0;
        const double NativeSeamSlack = 1.5;

        static Texture2D LoadPng(string path)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(ImageConversion.LoadImage(tex, File.ReadAllBytes(path)),
                "PNG decode failed: " + path);
            return tex;
        }

        static DecodedImage BuildNative(WadFile wad, TextureSet textures, Palette palette, string name)
        {
            // Flat-namespace alias (TextureCache.FlatSuffix): the flat lump of
            // the base name wins over the same-name wall composite.
            if (name.EndsWith(Doom.MapBuild.TextureCache.FlatSuffix, System.StringComparison.Ordinal))
            {
                string baseName = name.Substring(
                    0, name.Length - Doom.MapBuild.TextureCache.FlatSuffix.Length);
                int flatLump = wad.FindLump(baseName);
                Assert.That(flatLump, Is.GreaterThanOrEqualTo(0), name + ": aliased flat missing");
                return Flat.Decode(wad.ReadLump(flatLump), palette);
            }

            if (textures.Contains(name))
                return textures.Build(name, palette);
            int lump = wad.FindLump(name);
            Assert.That(lump, Is.GreaterThanOrEqualTo(0), name + " is neither texture nor flat");
            return Flat.Decode(wad.ReadLump(lump), palette);
        }

        [Test]
        public void Every_allowlisted_redraw_is_4x_opaque_and_tiles()
        {
            if (WorldRedrawAllowlist.Names.Length == 0)
                Assert.Pass("allowlist empty — pilot redraws not installed yet");

            if (!File.Exists(FreedoomPath)) Assert.Ignore("freedoom1.wad missing");
            using var wad = WadFile.Open(FreedoomPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));
            var textures = TextureSet.Load(wad);

            foreach (var name in WorldRedrawAllowlist.Names)
            {
                string path = Path.Combine(RedrawDir, name + ".png");
                Assert.IsTrue(File.Exists(path), name + " redraw missing: " + path);

                var native = BuildNative(wad, textures, palette, name);
                var tex = LoadPng(path);
                try
                {
                    int scale = WorldRedrawAllowlist.ScaleFor(name);
                    Assert.AreEqual(native.Width * scale, tex.width,
                        name + " width");
                    Assert.AreEqual(native.Height * scale, tex.height,
                        name + " height");

                    var pixels = tex.GetPixels32();
                    double nativeTransparent = TransparentFraction(native);
                    if (nativeTransparent == 0)
                    {
                        foreach (var p in pixels)
                            if (p.a < 255)
                                Assert.Fail(name + " has transparent pixels (walls are opaque)");
                    }
                    else
                    {
                        // Masked mid-textures (grates, vines): the redraw must
                        // keep a hole fraction close to the native silhouette.
                        double redrawTransparent = TransparentFraction(pixels);
                        Assert.That(
                            System.Math.Abs(redrawTransparent - nativeTransparent),
                            Is.LessThanOrEqualTo(0.10),
                            $"{name} transparent fraction {redrawTransparent:F2} vs native {nativeTransparent:F2}");
                        // Seam metric compares RGB; premultiply so hole texels
                        // (arbitrary RGB under alpha 0) cannot skew it.
                        Premultiply(pixels);
                        native = Premultiplied(native);
                    }

                    double redrawRatio = HorizontalSeamRatio(pixels, tex.width, tex.height);
                    double nativeRatio = HorizontalSeamRatio(native);
                    Assert.IsFalse(
                        redrawRatio > MaxSeamRatio && redrawRatio > nativeRatio * NativeSeamSlack,
                        $"{name} horizontal seam ratio {redrawRatio:F2} vs native {nativeRatio:F2}");
                }
                finally
                {
                    Object.DestroyImmediate(tex);
                }
            }
        }

        [Test]
        public void No_orphan_files_outside_the_allowlist()
        {
            if (!Directory.Exists(RedrawDir))
                Assert.Pass("no EnhancedWorld folder yet");

            foreach (var file in Directory.GetFiles(RedrawDir, "*.png"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                Assert.IsTrue(WorldRedrawAllowlist.Contains(name),
                    name + " sits in Resources/EnhancedWorld without an allowlist entry");
            }
        }

        static double TransparentFraction(Color32[] pixels)
        {
            int n = 0;
            foreach (var p in pixels)
                if (p.a == 0) n++;
            return (double)n / pixels.Length;
        }

        static double TransparentFraction(DecodedImage img)
        {
            int n = 0;
            for (int i = 3; i < img.Rgba.Length; i += 4)
                if (img.Rgba[i] == 0) n++;
            return (double)n / (img.Width * img.Height);
        }

        static void Premultiply(Color32[] pixels)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                pixels[i] = new Color32(
                    (byte)(p.r * p.a / 255), (byte)(p.g * p.a / 255),
                    (byte)(p.b * p.a / 255), p.a);
            }
        }

        static DecodedImage Premultiplied(DecodedImage img)
        {
            var rgba = (byte[])img.Rgba.Clone();
            for (int i = 0; i < rgba.Length; i += 4)
            {
                byte a = rgba[i + 3];
                rgba[i] = (byte)(rgba[i] * a / 255);
                rgba[i + 1] = (byte)(rgba[i + 1] * a / 255);
                rgba[i + 2] = (byte)(rgba[i + 2] * a / 255);
            }
            return new DecodedImage(img.Width, img.Height, rgba);
        }

        /// Wrapped edge-pair mean abs diff over interior neighbour-column diff
        /// (same metric as Tools/validate_tile_redraw.py).
        static double HorizontalSeamRatio(Color32[] pixels, int w, int h)
        {
            double ColDiff(int x0, int x1)
            {
                double s = 0;
                for (int y = 0; y < h; y++)
                {
                    var a = pixels[y * w + x0];
                    var b = pixels[y * w + x1];
                    s += Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
                }
                return s / h;
            }

            double interior = 0;
            for (int x = 0; x < w - 1; x++) interior += ColDiff(x, x + 1);
            interior /= (w - 1);
            double seam = ColDiff(w - 1, 0);
            return seam / System.Math.Max(interior, 1e-6);
        }

        static double HorizontalSeamRatio(DecodedImage img)
        {
            int w = img.Width, h = img.Height;
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // DecodedImage is top-down; row order does not affect the metric.
                int i = (y * w + x) * 4;
                pixels[y * w + x] = new Color32(
                    img.Rgba[i], img.Rgba[i + 1], img.Rgba[i + 2], img.Rgba[i + 3]);
            }
            return HorizontalSeamRatio(pixels, w, h);
        }
    }
}
