using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Doom.Graphics.Tests
{
    public class PaletteMipGeneratorTests
    {
        [Test]
        public void Generates_unity_sized_chain_through_one_by_one()
        {
            var source = Solid(7, 5, 120, 40, 10, 255);
            var chain = PaletteMipGenerator.Generate(source, TestPalette(), PixelWrapMode.Clamp);

            Assert.AreEqual(3, chain.Count);
            Assert.AreEqual((7, 5), (chain[0].Width, chain[0].Height));
            Assert.AreEqual((3, 2), (chain[1].Width, chain[1].Height));
            Assert.AreEqual((1, 1), (chain[2].Width, chain[2].Height));
        }

        [Test]
        public void Uniform_palette_color_stays_exact_and_input_is_unchanged()
        {
            var palette = TestPalette();
            palette.GetColor(73, out byte r, out byte g, out byte b);
            var source = Solid(4, 4, r, g, b, 255);
            var before = (byte[])source.Rgba.Clone();

            var chain = PaletteMipGenerator.Generate(source, palette, PixelWrapMode.RepeatXY);

            Assert.AreEqual(before, source.Rgba);
            for (int level = 1; level < chain.Count; level++)
            for (int i = 0; i < chain[level].Rgba.Length; i += 4)
            {
                Assert.AreEqual(r, chain[level].Rgba[i]);
                Assert.AreEqual(g, chain[level].Rgba[i + 1]);
                Assert.AreEqual(b, chain[level].Rgba[i + 2]);
                Assert.AreEqual(255, chain[level].Rgba[i + 3]);
            }
        }

        [Test]
        public void Every_visible_mip_color_belongs_to_playpal()
        {
            var palette = TestPalette();
            var rgba = new byte[8 * 8 * 4];
            for (int i = 0; i < 64; i++)
            {
                int o = i * 4;
                rgba[o] = (byte)(i * 3);
                rgba[o + 1] = (byte)(255 - i * 2);
                rgba[o + 2] = (byte)(i * 4);
                rgba[o + 3] = 255;
            }

            var chain = PaletteMipGenerator.Generate(
                new DecodedImage(8, 8, rgba), palette, PixelWrapMode.RepeatXY);
            var colors = PaletteColors(palette);
            for (int level = 1; level < chain.Count; level++)
            for (int i = 0; i < chain[level].Rgba.Length; i += 4)
            {
                int packed = Pack(chain[level].Rgba[i], chain[level].Rgba[i + 1],
                    chain[level].Rgba[i + 2]);
                Assert.IsTrue(colors.Contains(packed), $"level {level} color must be in PLAYPAL");
            }
        }

        [Test]
        public void Fully_transparent_hidden_rgb_becomes_canonical_transparent()
        {
            var rgba = new byte[2 * 2 * 4];
            for (int i = 0; i < 4; i++)
            {
                rgba[i * 4] = (byte)(50 + i * 20);
                rgba[i * 4 + 1] = (byte)(200 - i * 10);
                rgba[i * 4 + 2] = 99;
            }

            var chain = PaletteMipGenerator.Generate(
                new DecodedImage(2, 2, rgba), TestPalette(), PixelWrapMode.Clamp);
            Assert.AreEqual((byte)0, chain[1].Rgba[0]);
            Assert.AreEqual((byte)0, chain[1].Rgba[1]);
            Assert.AreEqual((byte)0, chain[1].Rgba[2]);
            Assert.AreEqual((byte)0, chain[1].Rgba[3]);
        }

        [Test]
        public void Alpha_coverage_is_preserved_at_representable_resolution()
        {
            var rgba = new byte[4 * 4 * 4];
            // One opaque source texel in each 2x2 block => 25% source coverage.
            for (int y = 0; y < 4; y += 2)
            for (int x = 0; x < 4; x += 2)
            {
                int i = (y * 4 + x) * 4;
                rgba[i] = rgba[i + 1] = rgba[i + 2] = 255;
                rgba[i + 3] = 255;
            }

            var chain = PaletteMipGenerator.Generate(
                new DecodedImage(4, 4, rgba), TestPalette(), PixelWrapMode.Clamp);
            int covered = 0;
            for (int i = 3; i < chain[1].Rgba.Length; i += 4)
                if (chain[1].Rgba[i] >= 128) covered++;
            Assert.AreEqual(1, covered);
        }

        [Test]
        public void Generation_is_deterministic()
        {
            var source = Solid(8, 4, 17, 99, 201, 255);
            var a = PaletteMipGenerator.Generate(source, TestPalette(), PixelWrapMode.RepeatX);
            var b = PaletteMipGenerator.Generate(source, TestPalette(), PixelWrapMode.RepeatX);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
                Assert.AreEqual(a[i].Rgba, b[i].Rgba);
        }

        [Test]
        public void Invalid_source_is_rejected()
        {
            Assert.Throws<ArgumentNullException>(() =>
                PaletteMipGenerator.Generate(null, TestPalette(), PixelWrapMode.Clamp));
            Assert.Throws<ArgumentException>(() =>
                PaletteMipGenerator.Generate(
                    new DecodedImage(2, 2, new byte[4]), TestPalette(), PixelWrapMode.Clamp));
        }

        [Test]
        public void Freedoom_wall_and_flat_generate_complete_palette_mip_chains()
        {
            string wadPath = System.IO.Path.Combine(
                UnityEngine.Application.dataPath, "StreamingAssets", "wads", "freedoom1.wad");
            if (!System.IO.File.Exists(wadPath))
                Assert.Ignore("freedoom1.wad missing");

            using var wad = Doom.Wad.WadFile.Open(wadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));
            var textures = TextureSet.Load(wad);

            var wall = PixelArtUpscaler.Scale2X(
                textures.Build("STARTAN2", palette), PixelWrapMode.RepeatX);
            var wallMips = PaletteMipGenerator.Generate(
                wall, palette, PixelWrapMode.RepeatX);
            Assert.That(wallMips.Count, Is.GreaterThan(1));
            Assert.AreEqual((1, 1),
                (wallMips[wallMips.Count - 1].Width, wallMips[wallMips.Count - 1].Height));

            int flatIndex = wad.FindLump("FLOOR0_1");
            Assert.That(flatIndex, Is.GreaterThanOrEqualTo(0));
            var flat = PixelArtUpscaler.Scale2X(
                Flat.Decode(wad.ReadLump(flatIndex), palette), PixelWrapMode.RepeatXY);
            var flatMips = PaletteMipGenerator.Generate(
                flat, palette, PixelWrapMode.RepeatXY);
            Assert.AreEqual(8, flatMips.Count); // 128 -> 64 -> ... -> 1
            Assert.AreEqual((1, 1),
                (flatMips[flatMips.Count - 1].Width, flatMips[flatMips.Count - 1].Height));
        }

        static Palette TestPalette()
        {
            var bytes = new byte[256 * 3];
            for (int i = 0; i < 256; i++)
            {
                bytes[i * 3] = (byte)i;
                bytes[i * 3 + 1] = (byte)((i * 73) & 255);
                bytes[i * 3 + 2] = (byte)((i * 151) & 255);
            }
            return new Palette(bytes);
        }

        static HashSet<int> PaletteColors(Palette palette)
        {
            var colors = new HashSet<int>();
            for (int i = 0; i < 256; i++)
            {
                palette.GetColor(i, out byte r, out byte g, out byte b);
                colors.Add(Pack(r, g, b));
            }
            return colors;
        }

        static int Pack(byte r, byte g, byte b) => (r << 16) | (g << 8) | b;

        static DecodedImage Solid(int width, int height, byte r, byte g, byte b, byte a)
        {
            var rgba = new byte[width * height * 4];
            for (int i = 0; i < width * height; i++)
            {
                int o = i * 4;
                rgba[o] = r;
                rgba[o + 1] = g;
                rgba[o + 2] = b;
                rgba[o + 3] = a;
            }
            return new DecodedImage(width, height, rgba);
        }
    }
}
