using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Wad;

namespace Doom.Graphics.Tests
{
    public class DeditherFilterTests
    {
        // Close pair: weighted RGB distance = 12 (inside (GroupTolerance, Cross)).
        static readonly (byte r, byte g, byte b) CloseA = (80, 80, 80);
        static readonly (byte r, byte g, byte b) CloseB = (92, 92, 92);
        const byte CloseMid = 86;

        // Contrasting pair: weighted RGB distance = 255 (≥ CrossDistanceThreshold).
        static readonly (byte r, byte g, byte b) FarA = (0, 0, 0);
        static readonly (byte r, byte g, byte b) FarB = (255, 255, 255);

        [Test]
        public void Close_checkerboard_interior_collapses_to_exact_midtone()
        {
            var src = Checkerboard(8, 8, CloseA, CloseB, 255);
            var outImg = DeditherFilter.Apply(src, PixelWrapMode.Clamp);

            Assert.AreEqual(src.Width, outImg.Width);
            Assert.AreEqual(src.Height, outImg.Height);

            // Full 3×3 pattern exists only away from clamped borders.
            for (int y = 1; y < 7; y++)
            for (int x = 1; x < 7; x++)
            {
                var p = outImg.GetPixel(x, y);
                Assert.AreEqual(CloseMid, p.r, $"r at ({x},{y})");
                Assert.AreEqual(CloseMid, p.g, $"g at ({x},{y})");
                Assert.AreEqual(CloseMid, p.b, $"b at ({x},{y})");
                Assert.AreEqual(255, p.a);
            }
        }

        [Test]
        public void Close_checkerboard_clamped_border_is_untouched()
        {
            var src = Checkerboard(8, 8, CloseA, CloseB, 255);
            var outImg = DeditherFilter.Apply(src, PixelWrapMode.Clamp);

            for (int i = 0; i < 8; i++)
            {
                Assert.AreEqual(src.GetPixel(i, 0), outImg.GetPixel(i, 0), $"top {i}");
                Assert.AreEqual(src.GetPixel(i, 7), outImg.GetPixel(i, 7), $"bottom {i}");
                Assert.AreEqual(src.GetPixel(0, i), outImg.GetPixel(0, i), $"left {i}");
                Assert.AreEqual(src.GetPixel(7, i), outImg.GetPixel(7, i), $"right {i}");
            }
        }

        [Test]
        public void Contrasting_checkerboard_is_unchanged()
        {
            var src = Checkerboard(8, 8, FarA, FarB, 255);
            var outImg = DeditherFilter.Apply(src, PixelWrapMode.Clamp);

            AssertImagesEqual(src, outImg);
        }

        [Test]
        public void Near_uniform_blob_is_unchanged()
        {
            // All pixels within GroupTolerance of each other but not alternating:
            // phase contrast stays ≤ GroupTolerance → gate must not fire.
            var src = Checkerboard(8, 8, (84, 84, 84), (88, 88, 88), 255);
            var outImg = DeditherFilter.Apply(src, PixelWrapMode.RepeatXY);

            AssertImagesEqual(src, outImg);
        }

        [Test]
        public void Organic_noise_is_unchanged()
        {
            // Deterministic non-alternating grain of close colors: no pixel has
            // checkerboard structure, so nothing may change.
            byte[] values =
            {
                80, 85, 91, 83, 88, 80,
                87, 80, 84, 90, 82, 86,
                91, 88, 80, 85, 89, 83,
                82, 84, 89, 80, 87, 91,
                88, 91, 83, 86, 80, 84,
                85, 82, 87, 91, 84, 89,
            };
            var rgba = new byte[6 * 6 * 4];
            for (int i = 0; i < 36; i++)
            {
                rgba[i * 4] = values[i];
                rgba[i * 4 + 1] = values[i];
                rgba[i * 4 + 2] = values[i];
                rgba[i * 4 + 3] = 255;
            }

            var src = new DecodedImage(6, 6, rgba);
            var outImg = DeditherFilter.Apply(src, PixelWrapMode.RepeatXY);

            AssertImagesEqual(src, outImg);
        }

        [Test]
        public void Region_edge_between_close_halves_is_unchanged()
        {
            // Two flat halves of close colors: an edge, not a dither pattern.
            var rgba = new byte[16 * 8 * 4];
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 16; x++)
            {
                var c = x < 8 ? CloseA : CloseB;
                Write(rgba, x, y, 16, c.r, c.g, c.b, 255);
            }

            var src = new DecodedImage(16, 8, rgba);
            var outImg = DeditherFilter.Apply(src, PixelWrapMode.Clamp);

            AssertImagesEqual(src, outImg);
        }

        [Test]
        public void One_pixel_groove_is_preserved()
        {
            // A 1px vertical dark groove in a light wall: orthogonal neighbors
            // mix groove and wall colors → phase cohesion fails → untouched.
            var rgba = new byte[8 * 8 * 4];
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                var c = x == 4 ? (r: (byte)60, g: (byte)60, b: (byte)60) : (r: (byte)90, g: (byte)90, b: (byte)90);
                Write(rgba, x, y, 8, c.r, c.g, c.b, 255);
            }

            var src = new DecodedImage(8, 8, rgba);
            var outImg = DeditherFilter.Apply(src, PixelWrapMode.RepeatXY);

            AssertImagesEqual(src, outImg);
        }

        [Test]
        public void Fully_transparent_pixels_disable_the_gate_and_stay_unchanged()
        {
            // Close-color checker on the left, transparent junk RGB on the right.
            // Any 3×3 touching transparency is skipped → whole image unchanged.
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

            AssertImagesEqual(src, outImg);
            CollectionAssert.AreEqual(copy, src.Rgba);
        }

        [Test]
        public void RepeatXY_collapses_checkerboard_including_tile_seams()
        {
            // Even-sized checker wraps seamlessly: every pixel is in-pattern.
            var src = Checkerboard(4, 4, CloseA, CloseB, 255);
            var outImg = DeditherFilter.Apply(src, PixelWrapMode.RepeatXY);

            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                Assert.AreEqual(CloseMid, outImg.GetPixel(x, y).r, $"({x},{y})");
        }

        [Test]
        public void RepeatX_wraps_horizontally_but_clamps_vertically()
        {
            var src = Checkerboard(4, 4, CloseA, CloseB, 255);
            var repeatX = DeditherFilter.Apply(src, PixelWrapMode.RepeatX);
            var clamp = DeditherFilter.Apply(src, PixelWrapMode.Clamp);

            // RepeatX: interior rows are in-pattern across the X seam.
            for (int y = 1; y < 3; y++)
            for (int x = 0; x < 4; x++)
                Assert.AreEqual(CloseMid, repeatX.GetPixel(x, y).r, $"repeatX ({x},{y})");

            // RepeatX: clamped top/bottom rows break the pattern.
            for (int x = 0; x < 4; x++)
            {
                Assert.AreEqual(src.GetPixel(x, 0), repeatX.GetPixel(x, 0));
                Assert.AreEqual(src.GetPixel(x, 3), repeatX.GetPixel(x, 3));
            }

            // Clamp: X border columns additionally break the pattern.
            for (int y = 0; y < 4; y++)
            {
                Assert.AreEqual(src.GetPixel(0, y), clamp.GetPixel(0, y));
                Assert.AreEqual(src.GetPixel(3, y), clamp.GetPixel(3, y));
            }
        }

        [Test]
        public void Matched_mask_reports_gated_pixels_only()
        {
            var checker = Checkerboard(4, 4, CloseA, CloseB, 255);
            DeditherFilter.Apply(checker, PixelWrapMode.RepeatXY,
                DeditherFilter.CrossDistanceThreshold, out var checkerMask);
            for (int i = 0; i < checkerMask.Length; i++)
                Assert.IsTrue(checkerMask[i], $"checker mask {i}");

            var flat = Checkerboard(4, 4, CloseA, CloseA, 255);
            DeditherFilter.Apply(flat, PixelWrapMode.RepeatXY,
                DeditherFilter.CrossDistanceThreshold, out var flatMask);
            for (int i = 0; i < flatMask.Length; i++)
                Assert.IsFalse(flatMask[i], $"flat mask {i}");
        }

        [Test]
        public void Input_array_is_not_mutated()
        {
            var src = Checkerboard(4, 4, CloseA, CloseB, 255);
            var copy = (byte[])src.Rgba.Clone();
            DeditherFilter.Apply(src, PixelWrapMode.RepeatXY);
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

        /// Freedoom art is dominated by organic grain, not checkerboard dither:
        /// the pattern gate must preserve nearly all of it and never soften
        /// high-contrast seams.
        [Test]
        public void Freedoom_walls_keep_grain_and_edges()
        {
            string wadPath = Path.Combine(
                Application.dataPath, "StreamingAssets", "wads", "freedoom1.wad");
            if (!File.Exists(wadPath))
                Assert.Ignore("freedoom1.wad missing");

            using var wad = WadFile.Open(wadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));
            var textures = TextureSet.Load(wad);

            foreach (string name in new[] { "STARTAN2", "BROWN1" })
            {
                Assert.IsTrue(textures.Contains(name), name);
                var src = textures.Build(name, palette);
                var outImg = DeditherFilter.Apply(src, PixelWrapMode.RepeatX,
                    DeditherFilter.CrossDistanceThreshold, out var mask);

                int matched = 0;
                foreach (bool m in mask)
                    if (m) matched++;
                double fraction = (double)matched / mask.Length;
                Assert.That(fraction, Is.LessThan(0.10),
                    $"{name}: gate fired on {fraction:P1} of pixels — grain must survive");

                // High-contrast groove on STARTAN2 must keep its strength.
                if (name == "STARTAN2")
                {
                    double edgeBefore = MeanAbsHorizontalLumaGradient(src, 0, 56, 16, 16);
                    double edgeAfter = MeanAbsHorizontalLumaGradient(outImg, 0, 56, 16, 16);
                    Assert.That(edgeAfter, Is.GreaterThanOrEqualTo(edgeBefore * 0.95),
                        $"edge strength {edgeBefore:F2} → {edgeAfter:F2}");
                }
            }
        }

        static void AssertImagesEqual(DecodedImage expected, DecodedImage actual)
        {
            Assert.AreEqual(expected.Width, actual.Width);
            Assert.AreEqual(expected.Height, actual.Height);
            for (int y = 0; y < expected.Height; y++)
            for (int x = 0; x < expected.Width; x++)
                Assert.AreEqual(expected.GetPixel(x, y), actual.GetPixel(x, y),
                    $"pixel ({x},{y})");
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
    }
}
