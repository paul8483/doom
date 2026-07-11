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

        [Test]
        public void Rocket_viewmodel_and_projectile_frames_resolve()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var set = SpriteSet.Load(wad);

            foreach (var entry in new[]
            {
                ("MISG", 0), ("MISG", 1),
                ("MISF", 0), ("MISF", 1), ("MISF", 2), ("MISF", 3),
                ("MISL", 0), ("MISL", 1), ("MISL", 2), ("MISL", 3),
            })
                Assert.That(set.TryGet(entry.Item1, entry.Item2, 0, out _), Is.True,
                    $"{entry.Item1} frame {entry.Item2}");
        }
    }
}
