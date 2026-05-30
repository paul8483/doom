using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Wad;

namespace Doom.Graphics.Tests
{
    public class SpriteSetFreedoomTests
    {
        private static string FreedoomPath =>
            Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");

        [Test]
        public void Imp_front_frame_resolves_and_decodes()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var set = SpriteSet.Load(wad);
            var pal = new Palette(wad.ReadLump("PLAYPAL"));

            // TROO frame A (spawn) must resolve for the front rotation.
            Assert.That(set.TryGet("TROO", 0, 0, out var r), Is.True);
            var img = Patch.Decode(wad.ReadLump(r.LumpIndex), pal);
            Assert.That(img.Width, Is.GreaterThan(0));
            Assert.That(img.Height, Is.GreaterThan(0));

            var h = Patch.ReadHeader(wad.ReadLump(r.LumpIndex));
            Assert.That((h.Width, h.Height), Is.EqualTo((img.Width, img.Height)));
        }
    }
}
