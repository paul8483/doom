using System;
using NUnit.Framework;

namespace Doom.Graphics.Tests
{
    public class HeightMapGeneratorTests
    {
        [Test]
        public void Uniform_input_yields_uniform_height()
        {
            var src = Solid(8, 8, 90, 120, 60, 255);
            var h = HeightMapGenerator.Generate(
                src, MaterialSurfaceCategory.Wall, PixelWrapMode.RepeatXY);

            Assert.AreEqual(8, h.Width);
            Assert.AreEqual(8, h.Height);
            var first = h.GetPixel(0, 0);
            Assert.AreEqual(first.r, first.g);
            Assert.AreEqual(first.g, first.b);
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                var p = h.GetPixel(x, y);
                Assert.AreEqual(first.r, p.r, $"({x},{y})");
                Assert.AreEqual(255, p.a);
            }
        }

        [Test]
        public void Brightness_step_yields_monotonic_height_gradient()
        {
            // Left dark, right bright — height must rise across the seam.
            var src = HorizontalStep(16, 8, dark: 40, bright: 200);
            var h = HeightMapGenerator.Generate(
                src, MaterialSurfaceCategory.Wall, PixelWrapMode.Clamp);

            byte left = h.GetPixel(2, 4).r;
            byte midL = h.GetPixel(6, 4).r;
            byte midR = h.GetPixel(9, 4).r;
            byte right = h.GetPixel(13, 4).r;
            Assert.That(left, Is.LessThan(midL));
            Assert.That(midL, Is.LessThanOrEqualTo(midR));
            Assert.That(midR, Is.LessThanOrEqualTo(right));
            Assert.That(left, Is.LessThan(right));
        }

        [Test]
        public void Dedithered_checker_yields_nearly_flat_height()
        {
            // Close-color checker that pattern-gated DeditherFilter collapses.
            var checker = CloseChecker(16, 16, a: 100, b: 120);
            var dedithered = DeditherFilter.Apply(checker, PixelWrapMode.RepeatXY);
            var h = HeightMapGenerator.Generate(
                dedithered, MaterialSurfaceCategory.Wall, PixelWrapMode.RepeatXY);

            double mean = 0;
            for (int i = 0; i < h.Rgba.Length; i += 4)
                mean += h.Rgba[i];
            mean /= (h.Width * h.Height);

            double var = 0;
            for (int i = 0; i < h.Rgba.Length; i += 4)
            {
                double d = h.Rgba[i] - mean;
                var += d * d;
            }
            var /= (h.Width * h.Height);

            Assert.That(var, Is.LessThan(4.0),
                "After dedither, dither checker must not become relief noise");
        }

        [Test]
        public void Metal_and_wall_weights_differ_on_mixed_scale_input()
        {
            var src = MixedScale(32, 32);
            var wall = HeightMapGenerator.Generate(
                src, MaterialSurfaceCategory.Wall, PixelWrapMode.RepeatXY);
            var metal = HeightMapGenerator.Generate(
                src, MaterialSurfaceCategory.Metal, PixelWrapMode.RepeatXY);

            Assert.That(BytesEqual(wall.Rgba, metal.Rgba), Is.False,
                "Metal fine/coarse weights must differ from Wall");
        }

        [Test]
        public void Input_is_not_mutated_and_dimensions_preserved()
        {
            var src = HorizontalStep(8, 4, 30, 220);
            var copy = (byte[])src.Rgba.Clone();
            var h = HeightMapGenerator.Generate(
                src, MaterialSurfaceCategory.Flat, PixelWrapMode.Clamp);

            CollectionAssert.AreEqual(copy, src.Rgba);
            Assert.AreEqual(src.Width, h.Width);
            Assert.AreEqual(src.Height, h.Height);
            Assert.AreEqual(src.Rgba.Length, h.Rgba.Length);
        }

        [Test]
        public void Transparent_pixels_keep_zero_alpha()
        {
            var rgba = new byte[4 * 4 * 4];
            for (int i = 0; i < rgba.Length; i += 4)
            {
                rgba[i] = 180; rgba[i + 1] = 180; rgba[i + 2] = 180; rgba[i + 3] = 255;
            }
            // One transparent texel.
            rgba[(1 * 4 + 1) * 4 + 3] = 0;

            var src = new DecodedImage(4, 4, rgba);
            var h = HeightMapGenerator.Generate(
                src, MaterialSurfaceCategory.Wall, PixelWrapMode.Clamp);
            Assert.AreEqual(0, h.GetPixel(1, 1).a);
        }

        [Test]
        public void Invalid_input_is_rejected()
        {
            Assert.Throws<ArgumentNullException>(() =>
                HeightMapGenerator.Generate(null, MaterialSurfaceCategory.Wall, PixelWrapMode.Clamp));
            Assert.Throws<ArgumentException>(() =>
                HeightMapGenerator.Generate(
                    new DecodedImage(2, 2, new byte[4]), // too short
                    MaterialSurfaceCategory.Wall, PixelWrapMode.Clamp));
        }

        [Test]
        public void Profile_parallax_amplitude_zero_for_fluid()
        {
            var fluid = MaterialSurfaceProfile.For(MaterialSurfaceCategory.Fluid);
            var wall = MaterialSurfaceProfile.For(MaterialSurfaceCategory.Wall);
            Assert.AreEqual(0f, fluid.ParallaxAmplitude);
            Assert.That(wall.ParallaxAmplitude, Is.GreaterThan(0f));
            Assert.That(
                MaterialSurfaceProfile.For(MaterialSurfaceCategory.Metal).HeightFineWeight,
                Is.Not.EqualTo(
                    MaterialSurfaceProfile.For(MaterialSurfaceCategory.Wall).HeightFineWeight));
        }

        static DecodedImage Solid(int w, int h, byte r, byte g, byte b, byte a)
        {
            var rgba = new byte[w * h * 4];
            for (int i = 0; i < rgba.Length; i += 4)
            {
                rgba[i] = r; rgba[i + 1] = g; rgba[i + 2] = b; rgba[i + 3] = a;
            }
            return new DecodedImage(w, h, rgba);
        }

        static DecodedImage HorizontalStep(int w, int h, byte dark, byte bright)
        {
            var rgba = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                byte v = x < w / 2 ? dark : bright;
                Write(rgba, x, y, w, v, v, v, 255);
            }
            return new DecodedImage(w, h, rgba);
        }

        static DecodedImage CloseChecker(int w, int h, byte a, byte b)
        {
            var rgba = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                byte v = ((x + y) & 1) == 0 ? a : b;
                Write(rgba, x, y, w, v, v, v, 255);
            }
            return new DecodedImage(w, h, rgba);
        }

        /// Low-frequency panels plus high-frequency speckles so fine/coarse weights matter.
        static DecodedImage MixedScale(int w, int h)
        {
            var rgba = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                byte baseTone = (byte)(80 + (x / 8) * 25);
                byte speck = ((x * 17 + y * 13) % 5 == 0) ? (byte)40 : (byte)0;
                int v = Math.Clamp(baseTone + speck, 0, 255);
                Write(rgba, x, y, w, (byte)v, (byte)v, (byte)v, 255);
            }
            return new DecodedImage(w, h, rgba);
        }

        static void Write(byte[] rgba, int x, int y, int w, byte r, byte g, byte b, byte a)
        {
            int i = (y * w + x) * 4;
            rgba[i] = r; rgba[i + 1] = g; rgba[i + 2] = b; rgba[i + 3] = a;
        }

        static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }
}
