using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Wad;
using Doom.Graphics;
using Doom.Things;

namespace Doom.Things.Tests
{
    public class BarrelDataTests
    {
        private static string FreedoomPath =>
            Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");

        [Test]
        public void Barrel_has_health_20_no_corpse_no_countkill()
        {
            Assert.That(ThingTable.TryGet(BarrelRules.DoomEdNum, out var def), Is.True);
            Assert.That(def.Sprite, Is.EqualTo(BarrelRules.SpawnSprite));
            Assert.That(def.Health, Is.EqualTo(BarrelRules.Health));
            Assert.That(def.CorpseFrame, Is.EqualTo(-1));
            Assert.That(def.Has(ThingFlags.Solid), Is.True);
            Assert.That(def.Has(ThingFlags.Shootable), Is.True);
            Assert.That(def.Has(ThingFlags.CountKill), Is.False);
        }

        [Test]
        public void BEXP_frames_resolve_in_freedoom()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var sprites = SpriteSet.Load(wad);
            foreach (int f in BarrelRules.ExplodeFrames)
            {
                Assert.That(sprites.TryGet(BarrelRules.ExplodeSprite, f, 0, out _), Is.True,
                    $"BEXP frame {f} should exist in freedoom1");
            }
        }
    }
}
