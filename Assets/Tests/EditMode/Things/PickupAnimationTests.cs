using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Graphics;
using Doom.Wad;

namespace Doom.Things.Tests
{
    public class PickupAnimationTests
    {
        [TestCase(2014, 4, 6)]
        [TestCase(2015, 4, 6)]
        [TestCase(2013, 4, 6)]
        [TestCase(2018, 2, 6)]
        [TestCase(5, 2, 10)]
        public void Genuine_pickup_sequences_match_spawn_states(
            int doomEdNum, int frameCount, int firstTics)
        {
            Assert.That(PickupAnimationTable.TryGet(doomEdNum, out var animation), Is.True);
            Assert.That(animation.Frames.Length, Is.EqualTo(frameCount));
            Assert.That(animation.Tics.Length, Is.EqualTo(frameCount));
            Assert.That(animation.Tics[0], Is.EqualTo(firstTics));
        }

        [Test]
        public void Static_pickups_have_no_animation()
        {
            Assert.That(PickupAnimationTable.TryGet(2011, out _), Is.False);
            Assert.That(PickupAnimationTable.TryGet(2007, out _), Is.False);
            Assert.That(PickupAnimationTable.TryGet(2001, out _), Is.False);
            Assert.That(PickupAnimationTable.TryGet(2025, out _), Is.False);
        }

        [Test]
        public void Every_required_frame_resolves_in_freedoom()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = WadFile.Open(path);
            var sprites = SpriteSet.Load(wad);

            foreach (var def in ThingTable.All)
            {
                if (!PickupAnimationTable.TryGet(def.DoomEdNum, out var animation)) continue;
                foreach (int frame in animation.Frames)
                    Assert.That(sprites.TryGet(def.Sprite, frame, 0, out _), Is.True,
                        $"{def.Sprite} frame {(char)('A' + frame)}");
            }
        }
    }
}
