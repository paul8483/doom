using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Wad;

namespace Doom.Graphics.Tests
{
    public class SuperXbrUpscalerTests
    {
        // 4×4 black with white main diagonal, Super-xBR 2× under Clamp.
        // Golden captured from Hyllian MIT reference (weights/passes unchanged).
        static readonly byte[] DiagonalGoldenRgba =
        {
            255,255,255,255,255,255,255,255,128,128,128,255,0,0,0,255,0,0,0,255,0,0,0,255,0,0,0,255,0,0,0,255,
            191,191,191,255,255,255,255,255,120,120,120,255,0,0,0,255,0,0,0,255,0,0,0,255,0,0,0,255,0,0,0,255,
            48,48,48,255,134,134,134,255,255,255,255,255,63,63,63,255,0,0,0,255,0,0,0,255,0,0,0,255,0,0,0,255,
            0,0,0,255,0,0,0,255,179,179,179,255,255,255,255,255,101,101,101,255,0,0,0,255,0,0,0,255,0,0,0,255,
            0,0,0,255,0,0,0,255,0,0,0,255,174,174,174,255,255,255,255,255,65,65,65,255,0,0,0,255,0,0,0,255,
            0,0,0,255,0,0,0,255,0,0,0,255,0,0,0,255,170,170,170,255,255,255,255,255,166,166,166,255,57,57,57,255,
            0,0,0,255,0,0,0,255,0,0,0,255,0,0,0,255,0,0,0,255,203,203,203,255,255,255,255,255,186,186,186,255,
            0,0,0,255,0,0,0,255,0,0,0,255,0,0,0,255,0,0,0,255,48,48,48,255,255,255,255,255,255,255,255,255
        };

        [Test]
        public void Uniform_1x1_duplicates_to_uniform_2x2()
        {
            var src = Solid(1, 1, 10, 20, 30, 255);
            var outImg = SuperXbrUpscaler.Scale2X(src, PixelWrapMode.Clamp);

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
            var outImg = SuperXbrUpscaler.Scale2X(src, PixelWrapMode.Clamp);
            Assert.AreEqual(6, outImg.Width);
            Assert.AreEqual(4, outImg.Height);
            for (int y = 0; y < outImg.Height; y++)
            for (int x = 0; x < outImg.Width; x++)
            {
                var p = outImg.GetPixel(x, y);
                Assert.AreEqual(40, p.r);
                Assert.AreEqual(50, p.g);
                Assert.AreEqual(60, p.b);
                Assert.AreEqual(255, p.a);
            }
        }

        [Test]
        public void Double_application_yields_4x_dimensions()
        {
            var src = Solid(5, 7, 1, 2, 3, 255);
            var x2 = SuperXbrUpscaler.Scale2X(src, PixelWrapMode.Clamp);
            var x4 = SuperXbrUpscaler.Scale2X(x2, PixelWrapMode.Clamp);
            Assert.AreEqual(src.Width * 4, x4.Width);
            Assert.AreEqual(src.Height * 4, x4.Height);
            Assert.AreEqual(x4.Width * x4.Height * 4, x4.Rgba.Length);
        }

        [Test]
        public void Horizontal_line_stays_a_horizontal_band()
        {
            // Middle row white, others black — upscaled band must remain horizontal
            // (no broken/jagged column that flips black inside the band).
            var rgba = new byte[4 * 4 * 4];
            for (int x = 0; x < 4; x++)
                Write(rgba, x, 1, 4, 255, 255, 255, 255);

            var src = new DecodedImage(4, 4, rgba);
            var outImg = SuperXbrUpscaler.Scale2X(src, PixelWrapMode.Clamp);

            // Source row 1 maps into output rows 2–3; those rows should be
            // dominated by bright samples (mean r high), not broken into a stair.
            for (int y = 2; y <= 3; y++)
            {
                int sum = 0;
                for (int x = 0; x < 8; x++)
                    sum += outImg.GetPixel(x, y).r;
                Assert.Greater(sum / 8, 200, $"row {y} mean");
            }

            // Outside the band Super-xBR may spill a little; keep it clearly dark.
            for (int y = 0; y <= 1; y++)
            {
                int sum = 0;
                for (int x = 0; x < 8; x++)
                    sum += outImg.GetPixel(x, y).r;
                Assert.Less(sum / 8, 100, $"row {y} mean");
            }
        }

        [Test]
        public void Vertical_line_stays_a_vertical_band()
        {
            var rgba = new byte[4 * 4 * 4];
            for (int y = 0; y < 4; y++)
                Write(rgba, 1, y, 4, 255, 255, 255, 255);

            var src = new DecodedImage(4, 4, rgba);
            var outImg = SuperXbrUpscaler.Scale2X(src, PixelWrapMode.Clamp);

            for (int x = 2; x <= 3; x++)
            {
                int sum = 0;
                for (int y = 0; y < 8; y++)
                    sum += outImg.GetPixel(x, y).r;
                Assert.Greater(sum / 8, 200, $"col {x} mean");
            }
        }

        [Test]
        public void Diagonal_fixture_matches_golden_snapshot()
        {
            var rgba = new byte[4 * 4 * 4];
            for (int i = 0; i < 16; i++)
                Write(rgba, i % 4, i / 4, 4, 0, 0, 0, 255);
            for (int i = 0; i < 4; i++)
                Write(rgba, i, i, 4, 255, 255, 255, 255);

            var src = new DecodedImage(4, 4, rgba);
            var outImg = SuperXbrUpscaler.Scale2X(src, PixelWrapMode.Clamp);

            Assert.AreEqual(8, outImg.Width);
            Assert.AreEqual(8, outImg.Height);
            Assert.AreEqual(DiagonalGoldenRgba.Length, outImg.Rgba.Length);
            Assert.AreEqual(DiagonalGoldenRgba, outImg.Rgba);
        }

        [Test]
        public void Input_array_is_not_mutated()
        {
            var src = Solid(2, 2, 7, 8, 9, 255);
            var copy = (byte[])src.Rgba.Clone();
            SuperXbrUpscaler.Scale2X(src, PixelWrapMode.Clamp);
            Assert.AreEqual(copy, src.Rgba);
        }

        [Test]
        public void Null_source_throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                SuperXbrUpscaler.Scale2X(null, PixelWrapMode.Clamp));
        }

        [Test]
        public void Invalid_dimensions_throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SuperXbrUpscaler.Scale2X(new DecodedImage(0, 1, Array.Empty<byte>()),
                    PixelWrapMode.Clamp));
        }

        [Test]
        public void Invalid_rgba_length_throws()
        {
            Assert.Throws<ArgumentException>(() =>
                SuperXbrUpscaler.Scale2X(new DecodedImage(2, 2, new byte[4]),
                    PixelWrapMode.Clamp));
        }

        [Test]
        public void RepeatX_differs_from_Clamp_on_horizontal_seam()
        {
            // Unique orange only on the right column: Clamp cannot bring it into the
            // left-edge neighborhood, RepeatX wraps and must change the left side.
            var rgba = new byte[3 * 3 * 4];
            for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                Write(rgba, x, y, 3, 0, 0, 0, 255);
            Write(rgba, 2, 1, 3, 255, 128, 0, 255);

            var src = new DecodedImage(3, 3, rgba);
            var clamp = SuperXbrUpscaler.Scale2X(src, PixelWrapMode.Clamp);
            var repeatX = SuperXbrUpscaler.Scale2X(src, PixelWrapMode.RepeatX);

            Assert.AreNotEqual(clamp.Rgba, repeatX.Rgba);

            // Left half must carry more of the wrapped orange signal under RepeatX.
            int clampWarm = 0, repeatWarm = 0;
            for (int y = 0; y < 6; y++)
            for (int x = 0; x < 2; x++)
            {
                var c = clamp.GetPixel(x, y);
                var r = repeatX.GetPixel(x, y);
                clampWarm += c.r + c.g;
                repeatWarm += r.r + r.g;
            }
            Assert.Greater(repeatWarm, clampWarm);
        }

        [Test]
        public void RepeatXY_differs_from_Clamp_on_corner_sample()
        {
            // Single white pixel at bottom-right; RepeatXY wraps it to influence
            // the top-left 2×2 block, Clamp does not.
            var rgba = new byte[3 * 3 * 4];
            Write(rgba, 2, 2, 3, 255, 255, 255, 255);
            var src = new DecodedImage(3, 3, rgba);

            var clamp = SuperXbrUpscaler.Scale2X(src, PixelWrapMode.Clamp);
            var repeat = SuperXbrUpscaler.Scale2X(src, PixelWrapMode.RepeatXY);

            Assert.AreNotEqual(clamp.Rgba, repeat.Rgba);
        }

        [Test]
        public void Masked_grid_after_bleed_has_no_dark_fringe_above_cutoff()
        {
            // 4×4 grate: opaque white on even cells, transparent with hidden black RGB.
            var rgba = new byte[4 * 4 * 4];
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
            {
                bool opaque = ((x + y) & 1) == 0;
                if (opaque)
                    Write(rgba, x, y, 4, 220, 220, 220, 255);
                else
                    Write(rgba, x, y, 4, 0, 0, 0, 0);
            }

            var src = new DecodedImage(4, 4, rgba);
            var bled = AlphaBleedGuard.Dilate(src);
            var x2 = SuperXbrUpscaler.Scale2X(bled, PixelWrapMode.Clamp);
            var x4 = SuperXbrUpscaler.Scale2X(x2, PixelWrapMode.Clamp);

            const byte cutoff = 128; // matches Doom cutout _Cutoff 0.5
            for (int y = 0; y < x4.Height; y++)
            for (int x = 0; x < x4.Width; x++)
            {
                var p = x4.GetPixel(x, y);
                if (p.a <= cutoff) continue;
                Assert.Greater(p.r, 40, $"dark fringe r at ({x},{y}) a={p.a}");
                Assert.Greater(p.g, 40, $"dark fringe g at ({x},{y}) a={p.a}");
                Assert.Greater(p.b, 40, $"dark fringe b at ({x},{y}) a={p.a}");
            }
        }

        [Test]
        public void Freedoom_wall_flat_masked_sky_pipeline_4x()
        {
            string wadPath = Path.Combine(
                Application.dataPath, "StreamingAssets", "wads", "freedoom1.wad");
            if (!File.Exists(wadPath))
                Assert.Ignore("freedoom1.wad missing");

            using var wad = WadFile.Open(wadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));
            var textures = TextureSet.Load(wad);

            Assert.IsTrue(textures.Contains("STARTAN2"));
            var wall = textures.Build("STARTAN2", palette);
            AssertPipeline4X(wall, PixelWrapMode.RepeatX, expectMask: false);

            int flatIdx = wad.FindLump("FLOOR4_8");
            Assert.That(flatIdx, Is.GreaterThanOrEqualTo(0));
            var flat = Flat.Decode(wad.ReadLump(flatIdx), palette);
            AssertPipeline4X(flat, PixelWrapMode.RepeatXY, expectMask: false);

            int skyIdx = wad.FindLump("SKY1");
            Assert.That(skyIdx, Is.GreaterThanOrEqualTo(0));
            var sky = Patch.Decode(wad.ReadLump(skyIdx), palette);
            AssertPipeline4X(sky, PixelWrapMode.RepeatX, expectMask: false);

            DecodedImage masked = null;
            string maskedName = null;
            foreach (var name in textures.Names)
            {
                var img = textures.Build(name, palette);
                if (!HasTransparent(img)) continue;
                masked = img;
                maskedName = name;
                break;
            }
            Assert.IsNotNull(masked, "expected at least one masked wall texture");
            AssertPipeline4X(masked, PixelWrapMode.RepeatX, expectMask: true);
            Assert.IsNotNull(maskedName);
        }

        static void AssertPipeline4X(DecodedImage native, PixelWrapMode wrap, bool expectMask)
        {
            var dedithered = DeditherFilter.Apply(native, wrap);
            var source = expectMask ? AlphaBleedGuard.Dilate(dedithered) : dedithered;
            var x2 = SuperXbrUpscaler.Scale2X(source, wrap);
            var x4 = SuperXbrUpscaler.Scale2X(x2, wrap);

            Assert.AreEqual(native.Width * 4, x4.Width);
            Assert.AreEqual(native.Height * 4, x4.Height);
            Assert.AreEqual(x4.Width * x4.Height * 4, x4.Rgba.Length);

            if (expectMask)
            {
                Assert.IsTrue(HasTransparent(native));
                Assert.IsTrue(HasTransparent(x4), "4× must retain a transparency mask");
            }
        }

        static bool HasTransparent(DecodedImage img)
        {
            for (int i = 3; i < img.Rgba.Length; i += 4)
                if (img.Rgba[i] == 0) return true;
            return false;
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
