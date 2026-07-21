using System;
using NUnit.Framework;

namespace Doom.Graphics.Tests
{
    public class AlphaBleedGuardTests
    {
        [Test]
        public void Transparent_neighbor_gets_nearest_opaque_rgb()
        {
            // Opaque white next to transparent black RGB (classic fringe source).
            var rgba = new byte[2 * 1 * 4];
            Write(rgba, 0, 0, 2, 200, 180, 160, 255);
            Write(rgba, 1, 0, 2, 0, 0, 0, 0);

            var src = new DecodedImage(2, 1, rgba);
            var outImg = AlphaBleedGuard.Dilate(src, iterations: 1);

            Assert.AreEqual((byte)200, outImg.GetPixel(1, 0).r);
            Assert.AreEqual((byte)180, outImg.GetPixel(1, 0).g);
            Assert.AreEqual((byte)160, outImg.GetPixel(1, 0).b);
            Assert.AreEqual(0, outImg.GetPixel(1, 0).a);
            Assert.AreEqual(src.GetPixel(0, 0), outImg.GetPixel(0, 0));
        }

        [Test]
        public void Opaque_pixels_are_unchanged()
        {
            var src = Solid(3, 3, 10, 20, 30, 255);
            var outImg = AlphaBleedGuard.Dilate(src);
            AssertImagesEqual(src, outImg);
        }

        [Test]
        public void Fully_opaque_image_is_noop()
        {
            var rgba = new byte[2 * 2 * 4];
            Write(rgba, 0, 0, 2, 1, 2, 3, 255);
            Write(rgba, 1, 0, 2, 4, 5, 6, 255);
            Write(rgba, 0, 1, 2, 7, 8, 9, 255);
            Write(rgba, 1, 1, 2, 10, 11, 12, 255);
            var src = new DecodedImage(2, 2, rgba);
            AssertImagesEqual(src, AlphaBleedGuard.Dilate(src));
        }

        [Test]
        public void Alpha_never_changes()
        {
            var rgba = new byte[3 * 3 * 4];
            Write(rgba, 1, 1, 3, 255, 0, 0, 255);
            // Surrounding transparent with junk RGB.
            for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
            {
                if (x == 1 && y == 1) continue;
                Write(rgba, x, y, 3, 0, 0, 0, 0);
            }

            var src = new DecodedImage(3, 3, rgba);
            var outImg = AlphaBleedGuard.Dilate(src);
            for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                Assert.AreEqual(src.GetPixel(x, y).a, outImg.GetPixel(x, y).a);
        }

        [Test]
        public void Input_array_is_not_mutated()
        {
            var rgba = new byte[2 * 1 * 4];
            Write(rgba, 0, 0, 2, 9, 8, 7, 255);
            Write(rgba, 1, 0, 2, 0, 0, 0, 0);
            var src = new DecodedImage(2, 1, rgba);
            var copy = (byte[])src.Rgba.Clone();
            AlphaBleedGuard.Dilate(src);
            Assert.AreEqual(copy, src.Rgba);
        }

        [Test]
        public void Two_iterations_reach_one_pixel_further()
        {
            // O T T  — after 1 iter only the middle bleeds; after 2 both.
            var rgba = new byte[3 * 1 * 4];
            Write(rgba, 0, 0, 3, 50, 60, 70, 255);
            Write(rgba, 1, 0, 3, 0, 0, 0, 0);
            Write(rgba, 2, 0, 3, 0, 0, 0, 0);

            var src = new DecodedImage(3, 1, rgba);
            var one = AlphaBleedGuard.Dilate(src, 1);
            Assert.AreEqual(50, one.GetPixel(1, 0).r);
            Assert.AreEqual(0, one.GetPixel(2, 0).r);

            var two = AlphaBleedGuard.Dilate(src, 2);
            Assert.AreEqual(50, two.GetPixel(1, 0).r);
            Assert.AreEqual(50, two.GetPixel(2, 0).r);
            Assert.AreEqual(0, two.GetPixel(2, 0).a);
        }

        [Test]
        public void Null_and_invalid_inputs_throw()
        {
            Assert.Throws<ArgumentNullException>(() => AlphaBleedGuard.Dilate(null));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                AlphaBleedGuard.Dilate(new DecodedImage(0, 1, Array.Empty<byte>())));
            Assert.Throws<ArgumentException>(() =>
                AlphaBleedGuard.Dilate(new DecodedImage(2, 2, new byte[4])));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                AlphaBleedGuard.Dilate(Solid(1, 1, 0, 0, 0, 255), -1));
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

        static void AssertImagesEqual(DecodedImage a, DecodedImage b)
        {
            Assert.AreEqual(a.Width, b.Width);
            Assert.AreEqual(a.Height, b.Height);
            Assert.AreEqual(a.Rgba, b.Rgba);
        }
    }
}
