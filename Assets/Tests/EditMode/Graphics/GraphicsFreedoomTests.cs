using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Doom.Wad;

namespace Doom.Graphics.Tests
{
    public class GraphicsFreedoomTests
    {
        private static string FreedoomPath =>
            Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");

        [Test]
        public void PLAYPAL_decodes_to_256_colors()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var pal = new Palette(wad.ReadLump("PLAYPAL"));
            Assert.That(pal.Count, Is.EqualTo(256));
        }

        [Test]
        public void TextureSet_loads_and_builds_a_real_texture()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var set = TextureSet.Load(wad);
            var pal = new Palette(wad.ReadLump("PLAYPAL"));

            // Pick any texture the set knows about and assemble it.
            string name = set.Names.First();
            Assert.That(set.TryGetSize(name, out int w, out int h), Is.True);
            Assert.That(w, Is.GreaterThan(0));
            Assert.That(h, Is.GreaterThan(0));

            var img = set.Build(name, pal);
            Assert.That(img.Width, Is.EqualTo(w));
            Assert.That(img.Height, Is.EqualTo(h));
            Assert.That(img.Rgba.Length, Is.EqualTo(w * h * 4));
        }

        [Test]
        public void A_known_flat_decodes_to_64x64()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var pal = new Palette(wad.ReadLump("PLAYPAL"));
            // FLOOR4_8 is a standard DOOM/Freedoom flat used in E1M1.
            int idx = wad.FindLump("FLOOR4_8");
            Assert.That(idx, Is.GreaterThanOrEqualTo(0), "FLOOR4_8 should exist in Freedoom");
            var img = Flat.Decode(wad.ReadLump(idx), pal);
            Assert.That((img.Width, img.Height), Is.EqualTo((64, 64)));
        }
    }
}
