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

        [Test]
        public void Gated_close_colors_mix_like_ungated()
        {
            var source = Image(
                2, 1,
                (100, 100, 100, 255),
                (110, 110, 110, 255)); // weighted distance 10 <= GateRampStart

            var gated = EdgeMixUpscaler.Scale8XGated(
                source, EdgeMixUpscaler.GateRampStart, EdgeMixUpscaler.GateRampEnd);
            var ungated = EdgeMixUpscaler.Scale8X(source);

            Assert.AreEqual(ungated.Rgba, gated.Rgba);
        }

        [Test]
        public void Gated_contrast_boundary_stays_hard_nearest()
        {
            var source = Image(
                2, 1,
                (0, 0, 0, 255),
                (255, 255, 255, 255)); // weighted distance 255 >= GateRampEnd

            var result = EdgeMixUpscaler.Scale8XGated(
                source, EdgeMixUpscaler.GateRampStart, EdgeMixUpscaler.GateRampEnd);

            for (int x = 0; x < 8; x++)
                Assert.AreEqual(((byte)0, (byte)0, (byte)0, (byte)255), result.GetPixel(x, 0));
            for (int x = 8; x < 16; x++)
                Assert.AreEqual(((byte)255, (byte)255, (byte)255, (byte)255), result.GetPixel(x, 0));
        }

        [Test]
        public void Gated_ramp_midpoint_mixes_partially()
        {
            // Weighted distance 40 sits mid-ramp for 16->64: weight ~0.5, so the
            // band color leans toward the center texel instead of the full mean.
            var source = Image(
                2, 1,
                (0, 0, 0, 255),
                (40, 40, 40, 255));

            var result = EdgeMixUpscaler.Scale8XGated(source, 16, 64);

            var band = result.GetPixel(6, 0);
            Assert.Greater(band.r, 0);
            Assert.Less(band.r, 20); // full mean would be 20
        }

        [Test]
        public void Gated_alpha_silhouette_matches_ungated()
        {
            var source = Image(
                2, 1,
                (200, 40, 20, 255),
                (0, 0, 255, 0));

            var gated = EdgeMixUpscaler.Scale8XGated(
                source, EdgeMixUpscaler.GateRampStart, EdgeMixUpscaler.GateRampEnd);
            var ungated = EdgeMixUpscaler.Scale8X(source);

            Assert.AreEqual(ungated.Rgba, gated.Rgba);
        }

        [Test]
        public void Gated_corner_with_one_contrast_diagonal_excludes_it()
        {
            // Three near-black texels and one white: the white diagonal must not
            // bleed into the corner average.
            var source = Image(
                2, 2,
                (0, 0, 0, 255), (10, 10, 10, 255),
                (10, 10, 10, 255), (255, 255, 255, 255));

            var result = EdgeMixUpscaler.Scale8XGated(
                source, EdgeMixUpscaler.GateRampStart, EdgeMixUpscaler.GateRampEnd);

            var corner = result.GetPixel(7, 7); // top-left texel corner sample
            Assert.Less(corner.r, 30);
        }

        [Test]
        public void Gated_invalid_ramp_throws()
        {
            var source = Image(1, 1, (1, 2, 3, 255));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                EdgeMixUpscaler.Scale8XGated(source, -1, 64));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                EdgeMixUpscaler.Scale8XGated(source, 64, 16));
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
