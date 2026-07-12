using System;
using NUnit.Framework;

namespace Doom.Graphics.Tests
{
    public class PixelArtUpscalerTests
    {
        [Test]
        public void Uniform_1x1_duplicates_to_2x2()
        {
            var src = Solid(1, 1, 10, 20, 30, 255);
            var outImg = PixelArtUpscaler.Scale2X(src, PixelWrapMode.Clamp);

            Assert.AreEqual(2, outImg.Width);
            Assert.AreEqual(2, outImg.Height);
            Assert.AreEqual(2 * 2 * 4, outImg.Rgba.Length);
            for (int y = 0; y < 2; y++)
            for (int x = 0; x < 2; x++)
                Assert.AreEqual(src.GetPixel(0, 0), outImg.GetPixel(x, y));
        }

        [Test]
        public void Uniform_image_stays_uniform_at_2x()
        {
            var src = Solid(3, 2, 40, 50, 60, 255);
            var outImg = PixelArtUpscaler.Scale2X(src, PixelWrapMode.Clamp);
            Assert.AreEqual(6, outImg.Width);
            Assert.AreEqual(4, outImg.Height);
            for (int y = 0; y < outImg.Height; y++)
            for (int x = 0; x < outImg.Width; x++)
                Assert.AreEqual((byte)40, outImg.GetPixel(x, y).r);
        }

        [Test]
        public void Horizontal_line_preserves_row_colors()
        {
            // Middle row white, others black.
            var rgba = new byte[3 * 3 * 4];
            for (int x = 0; x < 3; x++)
                Write(rgba, x, 1, 3, 255, 255, 255, 255);

            var src = new DecodedImage(3, 3, rgba);
            var outImg = PixelArtUpscaler.Scale2X(src, PixelWrapMode.Clamp);

            for (int x = 0; x < 6; x++)
            {
                Assert.AreEqual(0, outImg.GetPixel(x, 0).r);
                Assert.AreEqual(255, outImg.GetPixel(x, 2).r);
                Assert.AreEqual(255, outImg.GetPixel(x, 3).r);
                Assert.AreEqual(0, outImg.GetPixel(x, 5).r);
            }
        }

        [Test]
        public void Vertical_line_preserves_column_colors()
        {
            var rgba = new byte[3 * 3 * 4];
            for (int y = 0; y < 3; y++)
                Write(rgba, 1, y, 3, 255, 255, 255, 255);

            var src = new DecodedImage(3, 3, rgba);
            var outImg = PixelArtUpscaler.Scale2X(src, PixelWrapMode.Clamp);

            for (int y = 0; y < 6; y++)
            {
                Assert.AreEqual(0, outImg.GetPixel(0, y).r);
                Assert.AreEqual(255, outImg.GetPixel(2, y).r);
                Assert.AreEqual(255, outImg.GetPixel(3, y).r);
                Assert.AreEqual(0, outImg.GetPixel(5, y).r);
            }
        }

        [Test]
        public void Diagonal_fixture_matches_scale2x_formula()
        {
            // Neighborhood that fires E0 = D:
            //   B=W above E, D=W left of E, F and H different.
            //
            //   . W .
            //   W E X
            //   . Y .
            var rgba = new byte[3 * 3 * 4];
            Write(rgba, 1, 0, 3, 255, 255, 255, 255); // B
            Write(rgba, 0, 1, 3, 255, 255, 255, 255); // D
            Write(rgba, 1, 1, 3, 200, 200, 200, 255); // E (distinct)
            Write(rgba, 2, 1, 3, 10, 10, 10, 255);    // F
            Write(rgba, 1, 2, 3, 20, 20, 20, 255);    // H

            var src = new DecodedImage(3, 3, rgba);
            var outImg = PixelArtUpscaler.Scale2X(src, PixelWrapMode.Clamp);

            // Center (1,1) → output block origin (2,2).
            // E0 = D (white), E1/E2/E3 = E (200).
            Assert.AreEqual(255, outImg.GetPixel(2, 2).r);
            Assert.AreEqual(200, outImg.GetPixel(3, 2).r);
            Assert.AreEqual(200, outImg.GetPixel(2, 3).r);
            Assert.AreEqual(200, outImg.GetPixel(3, 3).r);
        }

        [Test]
        public void Input_array_is_not_mutated()
        {
            var src = Solid(2, 2, 7, 8, 9, 255);
            var copy = (byte[])src.Rgba.Clone();
            PixelArtUpscaler.Scale2X(src, PixelWrapMode.Clamp);
            Assert.AreEqual(copy, src.Rgba);
        }

        [Test]
        public void Transparent_hidden_rgb_does_not_create_fringe()
        {
            // Two fully transparent neighbors with different RGB must still compare equal.
            var rgba = new byte[2 * 2 * 4];
            Write(rgba, 0, 0, 2, 255, 0, 0, 0);     // transparent red
            Write(rgba, 1, 0, 2, 0, 255, 0, 0);     // transparent green
            Write(rgba, 0, 1, 2, 0, 0, 255, 255);   // opaque blue
            Write(rgba, 1, 1, 2, 0, 0, 255, 255);   // opaque blue

            var src = new DecodedImage(2, 2, rgba);
            var outImg = PixelArtUpscaler.Scale2X(src, PixelWrapMode.Clamp);

            // Top-left block comes from transparent pixel; alpha stays 0.
            Assert.AreEqual(0, outImg.GetPixel(0, 0).a);
            Assert.AreEqual(0, outImg.GetPixel(1, 0).a);
            Assert.AreEqual(0, outImg.GetPixel(0, 1).a);
            Assert.AreEqual(0, outImg.GetPixel(1, 1).a);
        }

        [Test]
        public void RepeatX_differs_from_Clamp_on_horizontal_seam()
        {
            // At left-middle (0,1)=black, up is white. Clamp left = self (black);
            // RepeatX left wraps to right column white → E0 becomes white only for RepeatX.
            var rgba = new byte[3 * 3 * 4];
            Write(rgba, 0, 0, 3, 255, 255, 255, 255); // up of left-middle
            Write(rgba, 2, 1, 3, 255, 255, 255, 255); // wrapped left neighbor
            Write(rgba, 1, 1, 3, 10, 10, 10, 255);    // F of left-middle
            Write(rgba, 0, 2, 3, 20, 20, 20, 255);    // H of left-middle
            // (0,1) stays black (0)

            var src = new DecodedImage(3, 3, rgba);
            var clamp = PixelArtUpscaler.Scale2X(src, PixelWrapMode.Clamp);
            var repeatX = PixelArtUpscaler.Scale2X(src, PixelWrapMode.RepeatX);

            // E0 of (0,1) lands at output (0,2).
            Assert.AreEqual(0, clamp.GetPixel(0, 2).r);
            Assert.AreEqual(255, repeatX.GetPixel(0, 2).r);
        }

        [Test]
        public void RepeatXY_differs_from_RepeatX_on_vertical_seam()
        {
            // At top-middle (1,0)=black, left is white. RepeatX up clamps to self (black);
            // RepeatXY up wraps to bottom row white → E0 becomes white only for RepeatXY.
            var rgba = new byte[3 * 3 * 4];
            Write(rgba, 0, 0, 3, 255, 255, 255, 255); // D of top-middle
            Write(rgba, 1, 2, 3, 255, 255, 255, 255); // wrapped up neighbor
            Write(rgba, 2, 0, 3, 10, 10, 10, 255);    // F
            Write(rgba, 1, 1, 3, 20, 20, 20, 255);    // H
            // (1,0) stays black (0)

            var src = new DecodedImage(3, 3, rgba);
            var repeatX = PixelArtUpscaler.Scale2X(src, PixelWrapMode.RepeatX);
            var repeatXY = PixelArtUpscaler.Scale2X(src, PixelWrapMode.RepeatXY);

            // E0 of (1,0) lands at output (2,0).
            Assert.AreEqual(0, repeatX.GetPixel(2, 0).r);
            Assert.AreEqual(255, repeatXY.GetPixel(2, 0).r);
        }

        [Test]
        public void Null_source_throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                PixelArtUpscaler.Scale2X(null, PixelWrapMode.Clamp));
        }

        [Test]
        public void Invalid_dimensions_throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                PixelArtUpscaler.Scale2X(new DecodedImage(0, 1, Array.Empty<byte>()),
                    PixelWrapMode.Clamp));
        }

        [Test]
        public void Invalid_rgba_length_throws()
        {
            Assert.Throws<ArgumentException>(() =>
                PixelArtUpscaler.Scale2X(new DecodedImage(2, 2, new byte[4]),
                    PixelWrapMode.Clamp));
        }

        [Test]
        public void Freedoom_wall_and_flat_scale_with_expected_size()
        {
            string wadPath = System.IO.Path.Combine(
                UnityEngine.Application.dataPath, "StreamingAssets", "wads", "freedoom1.wad");
            if (!System.IO.File.Exists(wadPath))
                Assert.Ignore("freedoom1.wad missing");

            using var wad = Doom.Wad.WadFile.Open(wadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));
            var textures = TextureSet.Load(wad);

            Assert.IsTrue(textures.Contains("STARTAN2"));
            var wall = textures.Build("STARTAN2", palette);
            var wall2 = PixelArtUpscaler.Scale2X(wall, PixelWrapMode.RepeatX);
            Assert.AreEqual(wall.Width * 2, wall2.Width);
            Assert.AreEqual(wall.Height * 2, wall2.Height);
            Assert.AreEqual(wall2.Width * wall2.Height * 4, wall2.Rgba.Length);

            int flatIdx = wad.FindLump("FLOOR0_1");
            Assert.That(flatIdx, Is.GreaterThanOrEqualTo(0));
            var flat = Flat.Decode(wad.ReadLump(flatIdx), palette);
            var flat2 = PixelArtUpscaler.Scale2X(flat, PixelWrapMode.RepeatXY);
            Assert.AreEqual(128, flat2.Width);
            Assert.AreEqual(128, flat2.Height);
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

        static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }
}
