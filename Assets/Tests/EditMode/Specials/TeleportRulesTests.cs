using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Map;
using Doom.Map.Tests;
using Doom.Wad;
using Doom.Specials;

namespace Doom.Specials.Tests
{
    public class TeleportRulesTests
    {
        [Test]
        public void Monster_only_specials_reject_player()
        {
            Assert.That(TeleportRules.IsMonsterOnly(125), Is.True);
            Assert.That(TeleportRules.IsMonsterOnly(126), Is.True);
            Assert.That(TeleportRules.IsMonsterOnly(97), Is.False);
            Assert.That(TeleportRules.IsMonsterOnly(39), Is.False);

            Assert.That(TeleportRules.CanActorUse(125, TeleportActorKind.Player), Is.False);
            Assert.That(TeleportRules.CanActorUse(125, TeleportActorKind.Monster), Is.True);
            Assert.That(TeleportRules.CanActorUse(97, TeleportActorKind.Player), Is.True);
            Assert.That(TeleportRules.CanActorUse(97, TeleportActorKind.Monster), Is.True);
        }

        [Test]
        public void Telefrag_when_destination_occupied()
        {
            Assert.That(TeleportRules.ShouldTelefrag(true), Is.True);
            Assert.That(TeleportRules.ShouldTelefrag(false), Is.False);
        }

        [Test]
        public void Front_side_is_right_of_directed_linedef()
        {
            // Line (0,0)→(10,0): front is below (negative Y in DOOM map space).
            Assert.That(TeleportRules.IsOnFrontSide(5f, -1f, 0f, 0f, 10f, 0f), Is.True);
            Assert.That(TeleportRules.IsOnFrontSide(5f, 1f, 0f, 0f, 10f, 0f), Is.False);
        }

        [Test]
        public void Select_picks_landing_in_tagged_sector_by_lowest_thing_index()
        {
            var landings = new[]
            {
                new TeleportLanding(5, 100, 100, 90, sectorIndex: 2),
                new TeleportLanding(3, 110, 100, 0, sectorIndex: 2),
                new TeleportLanding(1, 50, 50, 180, sectorIndex: 0),
            };
            var map = SpecialsTestMaps.TwoTaggedSectors(tag: 7);
            // Retag: sector 2 does not exist on the two-sector map — use sector 1.
            landings = new[]
            {
                new TeleportLanding(5, 100, 100, 90, sectorIndex: 1),
                new TeleportLanding(3, 110, 100, 0, sectorIndex: 1),
                new TeleportLanding(1, 50, 50, 180, sectorIndex: 0),
            };

            Assert.That(TeleportRules.TrySelect(map, tag: 7, landings, out var chosen), Is.True);
            // Lowest sector index with tag 7 is 0 → landing thing 1.
            Assert.That(chosen.ThingIndex, Is.EqualTo(1));
            Assert.That(chosen.SectorIndex, Is.EqualTo(0));
        }

        [Test]
        public void Select_skips_untagged_and_empty_sectors()
        {
            var map = SpecialsTestMaps.TwoAdjacentSectors(0, 128, 0, 128);
            var landings = new[]
            {
                new TeleportLanding(0, 32, 32, 0, sectorIndex: 0),
            };
            Assert.That(TeleportRules.TrySelect(map, tag: 99, landings, out _), Is.False);
            Assert.That(TeleportRules.TrySelect(map, tag: 0, landings, out _), Is.False);
        }

        [Test]
        public void E1M2_collects_teleport_landings_for_real_tags()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = WadFile.Open(path);
            var map = MapData.Load(wad, "E1M2");

            var landings = TeleportRules.CollectLandings(map);
            Assert.That(landings.Length, Is.GreaterThanOrEqualTo(2),
                "E1M2 should have teleport destinations");

            // Every WR teleport (97) on E1M2 must resolve a destination.
            int resolved = 0;
            for (int i = 0; i < map.LineDefs.Length; i++)
            {
                var ld = map.LineDefs[i];
                if (ld.Special != 97) continue;
                Assert.That(TeleportRules.TrySelect(map, ld.Tag, landings, out var dest), Is.True,
                    $"E1M2 line {i} special 97 tag {ld.Tag} must find a landing");
                Assert.That(dest.SectorIndex, Is.GreaterThanOrEqualTo(0));
                resolved++;
            }
            Assert.That(resolved, Is.GreaterThan(0), "E1M2 must contain special 97 teleports");
        }

        [Test]
        public void Executable_includes_teleport_category()
        {
            Assert.That(LineSpecialTable.TryGet(97, out var wr), Is.True);
            Assert.That(wr.Category, Is.EqualTo(SpecialCategory.Teleport));
            Assert.That(wr.IsExecutable, Is.True);
            Assert.That(wr.Repeatable, Is.True);
            Assert.That(wr.MonsterActivatable, Is.True);

            Assert.That(LineSpecialTable.TryGet(125, out var mon), Is.True);
            Assert.That(mon.IsExecutable, Is.True);
            Assert.That(TeleportRules.IsMonsterOnly(mon.Type), Is.True);
        }
    }
}
