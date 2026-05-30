using System.Collections.Generic;
using NUnit.Framework;
using Doom.Wad;
using Doom.Wad.Tests;

namespace Doom.Graphics.Tests
{
    public class TextureSetTests
    {
        // Build a WAD holding PLAYPAL, PNAMES, TEXTURE1, and two 1x2 patches.
        // Patch "PA" column: index 10 over both rows. Patch "PB": index 20 over both rows.
        private static WadFile BuildWad()
        {
            var playpal = SyntheticGfxBuilder.BuildPlaypal();
            var pnames = SyntheticGfxBuilder.BuildPnames("PA", "PB");

            var colA = new List<SyntheticGfxBuilder.Post>
            { new SyntheticGfxBuilder.Post { TopDelta = 0, Pixels = new byte[] { 10, 10 } } };
            var patchA = SyntheticGfxBuilder.BuildPatch(1, 2, 0, 0,
                new List<IReadOnlyList<SyntheticGfxBuilder.Post>> { colA });

            var colB = new List<SyntheticGfxBuilder.Post>
            { new SyntheticGfxBuilder.Post { TopDelta = 0, Pixels = new byte[] { 20, 20 } } };
            var patchB = SyntheticGfxBuilder.BuildPatch(1, 2, 0, 0,
                new List<IReadOnlyList<SyntheticGfxBuilder.Post>> { colB });

            // Texture "WALL" is 2x2: PA at originX 0, PB at originX 1.
            var tex = SyntheticGfxBuilder.BuildTextureLump(new SyntheticGfxBuilder.TexDef
            {
                Name = "WALL", Width = 2, Height = 2,
                Patches = new[]
                {
                    new SyntheticGfxBuilder.PatchRef { OriginX = 0, OriginY = 0, PatchIndex = 0 },
                    new SyntheticGfxBuilder.PatchRef { OriginX = 1, OriginY = 0, PatchIndex = 1 },
                }
            });

            var lumps = new List<SyntheticWadBuilder.Lump>
            {
                new SyntheticWadBuilder.Lump("PLAYPAL", playpal),
                new SyntheticWadBuilder.Lump("PNAMES", pnames),
                new SyntheticWadBuilder.Lump("TEXTURE1", tex),
                new SyntheticWadBuilder.Lump("PA", patchA),
                new SyntheticWadBuilder.Lump("PB", patchB),
            };
            byte[] wadBytes = SyntheticWadBuilder.Build("IWAD", lumps);
            return new WadFile(new System.IO.MemoryStream(wadBytes), ownsStream: true);
        }

        [Test]
        public void Reports_texture_size_by_name()
        {
            using var wad = BuildWad();
            var set = TextureSet.Load(wad);

            Assert.That(set.Contains("WALL"), Is.True);
            Assert.That(set.TryGetSize("WALL", out int w, out int h), Is.True);
            Assert.That((w, h), Is.EqualTo((2, 2)));
        }

        [Test]
        public void Assembles_patches_at_their_origins()
        {
            using var wad = BuildWad();
            var set = TextureSet.Load(wad);
            var pal = new Palette(wad.ReadLump("PLAYPAL"));

            var img = set.Build("WALL", pal);

            Assert.That((img.Width, img.Height), Is.EqualTo((2, 2)));
            // Column 0 from PA (index 10), column 1 from PB (index 20).
            Assert.That(img.GetPixel(0, 0), Is.EqualTo(((byte)10, (byte)0, (byte)10, (byte)255)));
            Assert.That(img.GetPixel(1, 0), Is.EqualTo(((byte)20, (byte)0, (byte)20, (byte)255)));
        }

        [Test]
        public void Unknown_texture_is_not_contained()
        {
            using var wad = BuildWad();
            var set = TextureSet.Load(wad);
            Assert.That(set.Contains("NOPE"), Is.False);
            Assert.That(set.TryGetSize("NOPE", out _, out _), Is.False);
        }
    }
}
