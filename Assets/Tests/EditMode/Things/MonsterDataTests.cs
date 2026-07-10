using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Wad;
using Doom.Graphics;

namespace Doom.Things.Tests
{
    public class MonsterDataTests
    {
        // info.c: MT_POSSESSED/MT_SHOTGUY/MT_TROOP/MT_SERGEANT.
        // Corpse frame = final death-state frame (tics -1).
        static readonly (int ed, int hp, int corpse)[] Monsters =
        {
            (3004, 20, 11),  // POSS: S_POSS_DIE5, frame 11 = 'L'
            (9,    30, 11),  // SPOS: S_SPOS_DIE5, frame 11 = 'L'
            (3001, 60, 12),  // TROO: S_TROO_DIE5, frame 12 = 'M'
            (3002, 150, 13), // SARG: S_SARG_DIE6, frame 13 = 'N'
            (58,   150, 13), // spectre (same as demon)
            (3003, 1000, 14), // BOSS: S_BOSS_DIE7, frame 14 = 'O'
        };

        private static string FreedoomPath =>
            Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");

        [Test]
        public void E1_monsters_have_health_and_corpse_frame()
        {
            foreach (var (ed, hp, corpse) in Monsters)
            {
                Assert.That(ThingTable.TryGet(ed, out var def), Is.True, $"doomednum {ed}");
                Assert.That(def.Health, Is.EqualTo(hp), $"{ed} health");
                Assert.That(def.CorpseFrame, Is.EqualTo(corpse), $"{ed} corpse frame");
                Assert.That(def.Has(ThingFlags.Shootable), Is.True, $"{ed} shootable");
            }
        }

        [Test]
        public void Corpse_sprites_resolve_in_freedoom()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var sprites = SpriteSet.Load(wad);
            foreach (var (ed, _, corpse) in Monsters)
            {
                ThingTable.TryGet(ed, out var def);
                // Death frames have no rotations → look up rotation 0.
                Assert.That(sprites.TryGet(def.Sprite, corpse, 0, out _), Is.True,
                    $"{def.Sprite} frame {corpse} should exist in freedoom1");
            }
        }
    }
}
