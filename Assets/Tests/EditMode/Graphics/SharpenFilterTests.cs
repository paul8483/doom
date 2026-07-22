using System;
using NUnit.Framework;

namespace Doom.Graphics.Tests
{
    public class SharpenFilterTests
    {
        [Test]
        public void Uniform_image_is_unchanged()
        {
            var src = Solid(4, 4, 90, 120, 60, 255);
            var outImg = SharpenFilter.Apply(src, 0.5f);

            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                Assert.AreEqual(src.GetPixel(x, y), outImg.GetPixel(x, y), $"({x},{y})");
        }

        [Test]
        public void Zero_amount_is_identity()
        {
            var src = Gradient8x8();
            var outImg = SharpenFilter.Apply(src, 0f);
            CollectionAssert.AreEqual(src.Rgba, outImg.Rgba);
        }

        [Test]
        public void Edge_contrast_increases()
        {
            // Vertical step 100 | 160: sharpening must push the two columns at
            // the seam apart (dark darker, bright brighter).
            var rgba = new byte[8 * 4 * 4];
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 8; x++)
            {
                byte v = x < 4 ? (byte)100 : (byte)160;
                Write(rgba, x, y, 8, v, v, v, 255);
            }

            var src = new DecodedImage(8, 4, rgba);
            var outImg = SharpenFilter.Apply(src, 0.5f);

            Assert.Less(outImg.GetPixel(3, 1).r, 100, "dark side of seam darkens");
            Assert.Greater(outImg.GetPixel(4, 1).r, 160, "bright side of seam brightens");
            // Far from the seam nothing changes.
            Assert.AreEqual(100, outImg.GetPixel(0, 1).r);
            Assert.AreEqual(160, outImg.GetPixel(7, 1).r);
        }

        [Test]
        public void Higher_amount_sharpens_more()
        {
            var rgba = new byte[8 * 4 * 4];
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 8; x++)
            {
                byte v = x < 4 ? (byte)100 : (byte)160;
                Write(rgba, x, y, 8, v, v, v, 255);
            }

            var src = new DecodedImage(8, 4, rgba);
            var half = SharpenFilter.Apply(src, 0.5f);
            var full = SharpenFilter.Apply(src, 1.0f);

            Assert.Greater(full.GetPixel(4, 1).r, half.GetPixel(4, 1).r);
            Assert.Less(full.GetPixel(3, 1).r, half.GetPixel(3, 1).r);
        }

        [Test]
        public void Transparent_pixels_unchanged_and_excluded_from_blur()
        {
            // Opaque bright column next to transparent junk: junk RGB must not
            // leak into the blur, transparent pixels must stay byte-identical.
            var rgba = new byte[4 * 4 * 4];
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
            {
                if (x < 2)
                    Write(rgba, x, y, 4, 200, 200, 200, 255);
                else
                    Write(rgba, x, y, 4, 255, 0, 0, 0); // transparent red junk
            }

            var src = new DecodedImage(4, 4, rgba);
            var outImg = SharpenFilter.Apply(src, 1.0f);

            for (int y = 0; y < 4; y++)
            {
                for (int x = 2; x < 4; x++)
                    Assert.AreEqual(src.GetPixel(x, y), outImg.GetPixel(x, y),
                        $"transparent ({x},{y})");
                // Uniform opaque area: blur == center → unchanged (junk excluded).
                for (int x = 0; x < 2; x++)
                    Assert.AreEqual(src.GetPixel(x, y), outImg.GetPixel(x, y),
                        $"opaque ({x},{y})");
            }
        }

        [Test]
        public void Alpha_channel_is_never_modified()
        {
            var src = Gradient8x8();
            var outImg = SharpenFilter.Apply(src, 1.0f);
            for (int i = 3; i < src.Rgba.Length; i += 4)
                Assert.AreEqual(src.Rgba[i], outImg.Rgba[i]);
        }

        [Test]
        public void Result_clamps_to_byte_range()
        {
            var rgba = new byte[4 * 4 * 4];
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
            {
                byte v = x < 2 ? (byte)0 : (byte)255;
                Write(rgba, x, y, 4, v, v, v, 255);
            }

            var src = new DecodedImage(4, 4, rgba);
            var outImg = SharpenFilter.Apply(src, 4.0f);
            foreach (byte b in outImg.Rgba)
                Assert.That(b, Is.InRange(0, 255));
        }

        [Test]
        public void Input_array_is_not_mutated()
        {
            var src = Gradient8x8();
            var copy = (byte[])src.Rgba.Clone();
            SharpenFilter.Apply(src, 1.0f);
            CollectionAssert.AreEqual(copy, src.Rgba);
        }

        [Test]
        public void Invalid_arguments_throw()
        {
            Assert.Throws<ArgumentNullException>(() =>
                SharpenFilter.Apply(null, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SharpenFilter.Apply(new DecodedImage(0, 1, Array.Empty<byte>()), 0.5f));
            Assert.Throws<ArgumentException>(() =>
                SharpenFilter.Apply(new DecodedImage(2, 2, new byte[4]), 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SharpenFilter.Apply(Solid(1, 1, 1, 2, 3, 255), -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SharpenFilter.Apply(Solid(1, 1, 1, 2, 3, 255), float.NaN));
        }

        static DecodedImage Gradient8x8()
        {
            var rgba = new byte[8 * 8 * 4];
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                Write(rgba, x, y, 8,
                    (byte)(x * 30), (byte)(y * 30), (byte)((x + y) * 15),
                    (byte)(((x + y) & 1) == 0 ? 255 : 0));
            return new DecodedImage(8, 8, rgba);
        }

        static DecodedImage Solid(int w, int h, byte r, byte g, byte b, byte a)
        {
            var rgba = new byte[w * h * 4];
            for (int i = 0; i < w * h; i++)
            {
                int o = i * 4;
                rgba[o] = r; rgba[o + 1] = g; rgba[o + 2] = b; rgba[o + 3] = a;
            }
            return new DecodedImage(w, h, rgba);
        }

        static void Write(byte[] rgba, int x, int y, int w, byte r, byte g, byte b, byte a)
        {
            int i = (y * w + x) * 4;
            rgba[i] = r; rgba[i + 1] = g; rgba[i + 2] = b; rgba[i + 3] = a;
        }
    }
}
