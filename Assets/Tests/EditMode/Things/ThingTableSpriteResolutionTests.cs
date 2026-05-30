using System.Collections.Generic;
using System.IO;
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

        [Test]
        public void Every_thing_sprite_and_frame_exists_in_freedoom()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var sprites = SpriteSet.Load(wad);

            var missing = new List<string>();
            foreach (var def in ThingTable.All)
            {
                // Rotation 0 index works whether the frame is all-angle or 8-way.
                if (!sprites.TryGet(def.Sprite, def.Frame, 0, out _))
                    missing.Add($"{def.DoomEdNum}:{def.Sprite} frame {def.Frame}");
            }

            Assert.That(missing, Is.Empty,
                "Sprites/frames not found in freedoom1.wad: " + string.Join(", ", missing));
        }
    }
}
