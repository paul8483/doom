using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Wad;

namespace Doom.Graphics.Tests
{
    public class DeditherFilterTests
    {
        // Close pair: weighted RGB distance ≈ 12.6 (< T).
        static readonly (byte r, byte g, byte b) CloseA = (80, 80, 80);
        static readonly (byte r, byte g, byte b) CloseB = (92, 92, 92);

        // Contrasting pair: weighted RGB distance = 255 (> T).
        static readonly (byte r, byte g, byte b) FarA = (0, 0, 0);
        static readonly (byte r, byte g, byte b) FarB = (255, 255, 255);

        [Test]
        public void Close_color_checkerboard_becomes_homogeneous_midtone()
        {
            var src = Checkerboard(8, 8, CloseA, CloseB, 255);
            var outImg = DeditherFilter.Apply(src, PixelWrapMode.Clamp);

            Assert.AreEqual(src.Width, outImg.Width);
            Assert.AreEqual(src.Height, outImg.Height);

            // Interior should collapse toward the mean of the two close colors.
            byte expectR = (byte)((CloseA.r + CloseB.r) / 2);
            byte expectG = (byte)((CloseA.g + CloseB.g) / 2);
            byte expectB = (byte)((CloseA.b + CloseB.b) / 2);

            for (int y = 1; y < 7; y++)
            for (int x = 1; x < 7; x++)
            {
                var p = outImg.GetPixel(x, y);
                Assert.That(p.r, Is.EqualTo(expectR).Within(1), $"r at ({x},{y})");
                Assert.That(p.g, Is.EqualTo(expectG).Within(1), $"g at ({x},{y})");
                Assert.That(p.b, Is.EqualTo(expectB).Within(1), $"b at ({x},{y})");
                Assert.AreEqual(255, p.a);
            }
        }

        [Test]
        public void Contrasting_checkerboard_is_unchanged()
        {
            var src = Checkerboard(8, 8, FarA, FarB, 255);
            var outImg = DeditherFilter.Apply(src, PixelWrapMode.Clamp);

            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                Assert.AreEqual(src.GetPixel(x, y), outImg.GetPixel(x, y),
                    $"pixel ({x},{y}) must stay put");
        }

        [Test]
        public void Soft_edge_between_close_regions_softens_at_most_one_pixel()
        {
            // Left half CloseA, right half CloseB — close colors across a vertical edge.
            var rgba = new byte[16 * 8 * 4];
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 16; x++)
            {
                var c = x < 8 ? CloseA : CloseB;
                Write(rgba, x, y, 16, c.r, c.g, c.b, 255);
            }

            var src = new DecodedImage(16, 8, rgba);
            var outImg = DeditherFilter.Apply(src, PixelWrapMode.Clamp);

            // Far from the seam: exact source colors.
            for (int y = 0; y < 8; y++)
            {
                Assert.AreEqual(CloseA, Rgb(outImg.GetPixel(2, y)));
                Assert.AreEqual(CloseB, Rgb(outImg.GetPixel(13, y)));
            }

            // Softening may touch only the two columns at the seam (7 and 8).
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 16; x++)
            {
                if (x == 7 || x == 8) continue;
                Assert.AreEqual(src.GetPixel(x, y), outImg.GetPixel(x, y),
                    $"column {x} must be untouched");
            }
        }

        [Test]
        public void Contrasting_edge_is_not_softened()
        {
            var rgba = new byte[16 * 8 * 4];
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 16; x++)
            {
                var c = x < 8 ? FarA : FarB;
                Write(rgba, x, y, 16, c.r, c.g, c.b, 255);
            }

            var src = new DecodedImage(16, 8, rgba);
            var outImg = DeditherFilter.Apply(src, PixelWrapMode.Clamp);

            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 16; x++)
                Assert.AreEqual(src.GetPixel(x, y), outImg.GetPixel(x, y));
        }

        [Test]
        public void Fully_transparent_pixels_are_unchanged_and_do_not_bleed()
        {
            // Opaque checker of close colors on the left; fully transparent (with
            // junk RGB) on the right. Transparent column must stay identical, and
            // the opaque column next to it must not pull transparent RGB in.
            var rgba = new byte[4 * 4 * 4];
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
            {
                if (x < 2)
                {
                    var c = ((x + y) & 1) == 0 ? CloseA : CloseB;
                    Write(rgba, x, y, 4, c.r, c.g, c.b, 255);
                }
                else
                {
                    Write(rgba, x, y, 4, 255, 0, 0, 0); // transparent red junk
                }
            }

            var src = new DecodedImage(4, 4, rgba);
            var copy = (byte[])src.Rgba.Clone();
            var outImg = DeditherFilter.Apply(src, PixelWrapMode.Clamp);

            for (int y = 0; y < 4; y++)
            for (int x = 2; x < 4; x++)
                Assert.AreEqual(src.GetPixel(x, y), outImg.GetPixel(x, y),
                    $"transparent ({x},{y})");

            // Opaque pixels adjacent to transparency must remain opaque and must
            // not pick up the transparent neighbor's red channel as a component
            // that pulls them toward (255,0,0).
            for (int y = 1; y < 3; y++)
            {
                var p = outImg.GetPixel(1, y);
                Assert.AreEqual(255, p.a);
                Assert.That(p.r, Is.LessThan(120),
                    "opaque pixel must not bleed transparent red");
            }

            CollectionAssert.AreEqual(copy, src.Rgba);
        }

        [Test]
        public void RepeatXY_smooths_across_tile_seam()
        {
            // 4×4 checkerboard of close colors. With RepeatXY, every pixel
            // (including corners) sees a balanced 3×3 of both colors → midtone.
            var src = Checkerboard(4, 4, CloseA, CloseB, 255);
            var outImg = DeditherFilter.Apply(src, PixelWrapMode.RepeatXY);

            byte expect = (byte)((CloseA.r + CloseB.r) / 2);
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                Assert.That(outImg.GetPixel(x, y).r, Is.EqualTo(expect).Within(1),
                    $"({x},{y})");
        }

        [Test]
        public void RepeatX_smooths_horizontal_seam_but_clamps_vertical()
        {
            // Top-left checker cell vs wrapped right neighbor: with RepeatX the
            // left border samples the right column. Build a 4×4 where column 0
            // is CloseA and column 3 is CloseB (close), rest CloseA — so only
            // RepeatX lets column 0 see CloseB via wrap.
            var rgba = new byte[4 * 4 * 4];
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
            {
                var c = x == 3 ? CloseB : CloseA;
                Write(rgba, x, y, 4, c.r, c.g, c.b, 255);
            }

            var src = new DecodedImage(4, 4, rgba);
            var clamp = DeditherFilter.Apply(src, PixelWrapMode.Clamp);
            var repeatX = DeditherFilter.Apply(src, PixelWrapMode.RepeatX);

            // Clamp: left column only sees CloseA → stays CloseA.
            Assert.AreEqual(CloseA.r, clamp.GetPixel(0, 1).r);

            // RepeatX: left column also sees CloseB from the right → midtone pull.
            Assert.That(repeatX.GetPixel(0, 1).r, Is.GreaterThan(CloseA.r));
            Assert.That(repeatX.GetPixel(0, 1).r, Is.LessThan(CloseB.r));
        }

        [Test]
        public void Clamp_edge_samples_with_clamp()
        {
            // Solid CloseA with a single CloseB at (0,0). Clamp keeps the
            // out-of-bounds samples as the corner itself; interior far from
            // corner stays CloseA.
            var rgba = new byte[4 * 4 * 4];
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                Write(rgba, x, y, 4, CloseA.r, CloseA.g, CloseA.b, 255);
            Write(rgba, 0, 0, 4, CloseB.r, CloseB.g, CloseB.b, 255);

            var src = new DecodedImage(4, 4, rgba);
            var outImg = DeditherFilter.Apply(src, PixelWrapMode.Clamp);

            Assert.AreEqual(CloseA, Rgb(outImg.GetPixel(3, 3)));
            // Corner mixes with clamped neighbors (mostly CloseA) → between A and B.
            var corner = outImg.GetPixel(0, 0);
            Assert.That(corner.r, Is.InRange(CloseA.r, CloseB.r));
        }

        [Test]
        public void Input_array_is_not_mutated()
        {
            var src = Checkerboard(4, 4, CloseA, CloseB, 255);
            var copy = (byte[])src.Rgba.Clone();
            DeditherFilter.Apply(src, PixelWrapMode.Clamp);
            CollectionAssert.AreEqual(copy, src.Rgba);
        }

        [Test]
        public void Null_source_throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                DeditherFilter.Apply(null, PixelWrapMode.Clamp));
        }

        [Test]
        public void Invalid_dimensions_throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                DeditherFilter.Apply(new DecodedImage(0, 1, Array.Empty<byte>()),
                    PixelWrapMode.Clamp));
        }

        [Test]
        public void Invalid_rgba_length_throws()
        {
            Assert.Throws<ArgumentException>(() =>
                DeditherFilter.Apply(new DecodedImage(2, 2, new byte[4]),
                    PixelWrapMode.Clamp));
        }

        [Test]
        public void Null_rgba_throws()
        {
            Assert.Throws<ArgumentException>(() =>
                DeditherFilter.Apply(new DecodedImage(1, 1, null),
                    PixelWrapMode.Clamp));
        }

        /// Freedoom STARTAN2: dithered panel variance drops; high-edge groove
        /// keeps most of its horizontal luma contrast (T=40 calibrated here).
        [Test]
        public void Freedoom_STARTAN2_dither_region_variance_drops_seam_stable()
        {
            string wadPath = Path.Combine(
                Application.dataPath, "StreamingAssets", "wads", "freedoom1.wad");
            if (!File.Exists(wadPath))
                Assert.Ignore("freedoom1.wad missing");

            using var wad = WadFile.Open(wadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));
            var textures = TextureSet.Load(wad);
            Assert.IsTrue(textures.Contains("STARTAN2"));

            var src = textures.Build("STARTAN2", palette);
            var outImg = DeditherFilter.Apply(src, PixelWrapMode.RepeatX);

            // Dithered panel patch on Freedoom 0.13 STARTAN2 (128×128).
            const int ditherX = 72, ditherY = 88, win = 16;
            double varBefore = LocalLumaVariance(src, ditherX, ditherY, win, win);
            double varAfter = LocalLumaVariance(outImg, ditherX, ditherY, win, win);
            Assert.That(varAfter, Is.LessThan(varBefore * 0.75),
                $"dither variance {varBefore:F2} → {varAfter:F2}; T={DeditherFilter.ColorDistanceThreshold}");

            // High-contrast groove / panel edge: edge strength must stay ≥ 85%.
            const int edgeX = 0, edgeY = 56;
            double edgeBefore = MeanAbsHorizontalLumaGradient(src, edgeX, edgeY, win, win);
            double edgeAfter = MeanAbsHorizontalLumaGradient(outImg, edgeX, edgeY, win, win);
            Assert.That(edgeAfter, Is.GreaterThanOrEqualTo(edgeBefore * 0.85),
                $"edge strength {edgeBefore:F2} → {edgeAfter:F2}; T={DeditherFilter.ColorDistanceThreshold}");
        }

        static double LocalLumaVariance(DecodedImage img, int x0, int y0, int w, int h)
        {
            double sum = 0, sumSq = 0;
            int n = 0;
            for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
            {
                var p = img.GetPixel(x, y);
                double yL = 0.30 * p.r + 0.59 * p.g + 0.11 * p.b;
                sum += yL;
                sumSq += yL * yL;
                n++;
            }

            double mean = sum / n;
            return sumSq / n - mean * mean;
        }

        static double MeanAbsHorizontalLumaGradient(
            DecodedImage img, int x0, int y0, int w, int h)
        {
            double sum = 0;
            int n = 0;
            for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x + 1 < x0 + w; x++)
            {
                var a = img.GetPixel(x, y);
                var b = img.GetPixel(x + 1, y);
                double la = 0.30 * a.r + 0.59 * a.g + 0.11 * a.b;
                double lb = 0.30 * b.r + 0.59 * b.g + 0.11 * b.b;
                sum += Math.Abs(la - lb);
                n++;
            }

            return sum / n;
        }

        static DecodedImage Checkerboard(
            int w, int h,
            (byte r, byte g, byte b) a,
            (byte r, byte g, byte b) b,
            byte alpha)
        {
            var rgba = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var c = ((x + y) & 1) == 0 ? a : b;
                Write(rgba, x, y, w, c.r, c.g, c.b, alpha);
            }

            return new DecodedImage(w, h, rgba);
        }

        static void Write(byte[] rgba, int x, int y, int w, byte r, byte g, byte b, byte a)
        {
            int i = (y * w + x) * 4;
            rgba[i] = r;
            rgba[i + 1] = g;
            rgba[i + 2] = b;
            rgba[i + 3] = a;
        }

        static (byte r, byte g, byte b) Rgb((byte r, byte g, byte b, byte a) p) =>
            (p.r, p.g, p.b);
    }
}
