using System.Collections.Generic;
using NUnit.Framework;
using Doom.Wad;
using Doom.Wad.Tests;

namespace Doom.Graphics.Tests
{
    public class SpriteSetTests
    {
        // 1x1 opaque patch (palette index 5) as a stand-in sprite lump.
        private static byte[] Pixel()
            => SyntheticGfxBuilder.BuildPatch(1, 1, 0, 0,
                new List<IReadOnlyList<SyntheticGfxBuilder.Post>>
                {
                    new List<SyntheticGfxBuilder.Post>
                    { new SyntheticGfxBuilder.Post { TopDelta = 0, Pixels = new byte[] { 5 } } }
                });

        private static WadFile BuildWad(params (string name, byte[] data)[] sprites)
        {
            var lumps = new List<SyntheticWadBuilder.Lump>
            {
                new SyntheticWadBuilder.Lump("S_START", System.Array.Empty<byte>())
            };
            foreach (var (n, dta) in sprites)
                lumps.Add(new SyntheticWadBuilder.Lump(n, dta));
            lumps.Add(new SyntheticWadBuilder.Lump("S_END", System.Array.Empty<byte>()));
            byte[] bytes = SyntheticWadBuilder.Build("IWAD", lumps);
            return new WadFile(new System.IO.MemoryStream(bytes), ownsStream: true);
        }

        [Test]
        public void Single_rotation_frame_resolves_for_any_angle()
        {
            using var wad = BuildWad(("TROOA0", Pixel()));
            var set = SpriteSet.Load(wad);
            for (int rot = 0; rot < 8; rot++)
            {
                Assert.That(set.TryGet("TROO", 0, rot, out var r), Is.True);
                Assert.That(r.Mirrored, Is.False);
            }
        }

        [Test]
        public void Eight_way_frame_resolves_per_rotation()
        {
            using var wad = BuildWad(
                ("POSSA1", Pixel()), ("POSSA2", Pixel()), ("POSSA3", Pixel()),
                ("POSSA4", Pixel()), ("POSSA5", Pixel()), ("POSSA6", Pixel()),
                ("POSSA7", Pixel()), ("POSSA8", Pixel()));
            var set = SpriteSet.Load(wad);
            Assert.That(set.TryGet("POSS", 0, 0, out var r1), Is.True); // rotation '1'
            Assert.That(r1.Mirrored, Is.False);
            Assert.That(set.TryGet("POSS", 0, 7, out var r8), Is.True); // rotation '8'
            Assert.That(r8.Mirrored, Is.False);
        }

        [Test]
        public void Mirrored_pair_marks_second_rotation_flipped()
        {
            // POSSA2A8: lump serves frame A rotation 2 (normal) and rotation 8 (mirrored).
            using var wad = BuildWad(("POSSA2A8", Pixel()));
            var set = SpriteSet.Load(wad);
            Assert.That(set.TryGet("POSS", 0, 1, out var r2), Is.True); // index 1 = rotation 2
            Assert.That(r2.Mirrored, Is.False);
            Assert.That(set.TryGet("POSS", 0, 7, out var r8), Is.True); // index 7 = rotation 8
            Assert.That(r8.Mirrored, Is.True);
        }

        [Test]
        public void Unknown_sprite_or_frame_is_absent()
        {
            using var wad = BuildWad(("TROOA0", Pixel()));
            var set = SpriteSet.Load(wad);
            Assert.That(set.TryGet("NONE", 0, 0, out _), Is.False);
            Assert.That(set.TryGet("TROO", 5, 0, out _), Is.False);
        }
    }
}
