using System;
using NUnit.Framework;

namespace Doom.Graphics.Tests
{
    public class EdgeMixUpscalerTests
    {
        [Test]
        public void Uniform_pixel_expands_to_8x8()
        {
            var source = Image(1, 1, (10, 20, 30, 255));

            var result = EdgeMixUpscaler.Scale8X(source);

            Assert.AreEqual(8, result.Width);
            Assert.AreEqual(8, result.Height);
            for (int y = 0; y < result.Height; y++)
            for (int x = 0; x < result.Width; x++)
                Assert.AreEqual(((byte)10, (byte)20, (byte)30, (byte)255), result.GetPixel(x, y));
        }

        [Test]
        public void Two_colors_keep_six_pixels_and_mix_four_at_boundary()
        {
            var source = Image(
                2, 1,
                (0, 0, 0, 255),
                (100, 100, 100, 255));

            var result = EdgeMixUpscaler.Scale8X(source);

            for (int x = 0; x < 6; x++)
                Assert.AreEqual(((byte)0, (byte)0, (byte)0, (byte)255), result.GetPixel(x, 0));
            for (int x = 6; x < 10; x++)
                Assert.AreEqual(((byte)50, (byte)50, (byte)50, (byte)255), result.GetPixel(x, 0));
            for (int x = 10; x < 16; x++)
                Assert.AreEqual(((byte)100, (byte)100, (byte)100, (byte)255), result.GetPixel(x, 0));
        }

        [Test]
        public void Transparent_neighbor_reduces_alpha_without_dark_halo()
        {
            var source = Image(
                2, 1,
                (200, 40, 20, 255),
                (0, 0, 255, 0));

            var result = EdgeMixUpscaler.Scale8X(source);

            for (int x = 6; x < 10; x++)
                Assert.AreEqual(((byte)200, (byte)40, (byte)20, (byte)128), result.GetPixel(x, 0));
        }

        [Test]
        public void Four_way_crossing_averages_all_source_texels()
        {
            var source = Image(
                2, 2,
                (0, 0, 0, 255), (100, 0, 0, 255),
                (0, 100, 0, 255), (0, 0, 100, 255));

            var result = EdgeMixUpscaler.Scale8X(source);

            Assert.AreEqual(((byte)25, (byte)25, (byte)25, (byte)255), result.GetPixel(7, 7));
            Assert.AreEqual(((byte)25, (byte)25, (byte)25, (byte)255), result.GetPixel(8, 8));
        }

        [Test]
        public void Input_is_not_mutated()
        {
            var source = Image(2, 1, (1, 2, 3, 4), (5, 6, 7, 8));
            var before = (byte[])source.Rgba.Clone();

            EdgeMixUpscaler.Scale8X(source);

            Assert.AreEqual(before, source.Rgba);
        }

        [Test]
        public void Invalid_inputs_throw()
        {
            Assert.Throws<ArgumentNullException>(() => EdgeMixUpscaler.Scale8X(null));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                EdgeMixUpscaler.Scale8X(new DecodedImage(0, 1, Array.Empty<byte>())));
            Assert.Throws<ArgumentException>(() =>
                EdgeMixUpscaler.Scale8X(new DecodedImage(2, 1, new byte[4])));
        }

        static DecodedImage Image(
            int width,
            int height,
            params (byte r, byte g, byte b, byte a)[] pixels)
        {
            Assert.AreEqual(width * height, pixels.Length);
            var rgba = new byte[pixels.Length * 4];
            for (int i = 0; i < pixels.Length; i++)
            {
                int offset = i * 4;
                rgba[offset] = pixels[i].r;
                rgba[offset + 1] = pixels[i].g;
                rgba[offset + 2] = pixels[i].b;
                rgba[offset + 3] = pixels[i].a;
            }
            return new DecodedImage(width, height, rgba);
        }
    }
}
