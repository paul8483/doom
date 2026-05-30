using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Doom.Wad;
using Doom.Graphics;

namespace Doom.Things.Tests
{
    public class ThingTableSpriteResolutionTests
    {
        private static string FreedoomPath =>
            Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");

        // Doom-2-only things (chaingunner, hell knight, arachnotron, pain elemental,
        // revenant, mancubus, arch-vile, boss brain, SS, commander keen, super
        // shotgun, megasphere, blood/brain pools). Freedoom Phase 1 is a Doom-1-only
        // IWAD and ships none of these under the original DOOM sprite names. Stage 5
        // renders Doom-1 maps (ExMy), where none of these things appear, so their
        // absence is expected. The full mobjinfo table keeps the correct names for
        // Stage 6 / Doom-2 support.
        private static readonly HashSet<string> Doom2OnlySprites = new()
        {
            "CPOS", "BOS2", "BSPI", "PAIN", "SKEL", "FATT", "VILE", "BBRN",
            "SSWV", "KEEN", "SGN2", "MEGA", "POB1", "POB2", "BRS1",
        };

        [Test]
        public void Every_doom1_thing_sprite_resolves_in_freedoom()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var sprites = SpriteSet.Load(wad);

            var missing = new List<string>();
            foreach (var def in ThingTable.All)
            {
                if (Doom2OnlySprites.Contains(def.Sprite)) continue;
                if (!sprites.TryGet(def.Sprite, def.Frame, 0, out _))
                    missing.Add($"{def.DoomEdNum}:{def.Sprite} frame {def.Frame}");
            }

            Assert.That(missing, Is.Empty,
                "Doom-1 sprites/frames not found in freedoom1.wad: " + string.Join(", ", missing));
        }

        [Test]
        public void Doom2_only_sprites_are_the_only_gap()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var sprites = SpriteSet.Load(wad);

            // Each excluded sprite should genuinely be absent (keeps the list honest).
            var unexpectedlyPresent = Doom2OnlySprites
                .Where(s => sprites.TryGet(s, 0, 0, out _))
                .ToList();

            Assert.That(unexpectedlyPresent, Is.Empty,
                "These are listed as Doom-2-only but DO resolve in freedoom1.wad — " +
                "remove them from the exclusion set: " + string.Join(", ", unexpectedlyPresent));
        }
    }
}
