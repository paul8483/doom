using System;
using NUnit.Framework;
using Doom.Map;

namespace Doom.Specials.Tests
{
    public class RuntimeLightRulesTests
    {
        [Test]
        public void Initial_light_matches_SECTORS_for_static()
        {
            var s = RuntimeLightRules.InitFromSector(160, sectorSpecial: 0, lowestNeighborLight: 80);
            Assert.AreEqual(SectorLightKind.None, s.Kind);
            Assert.AreEqual(160, s.Light);
        }

        [Test]
        public void Strobe_alternates_between_max_and_min_deterministically()
        {
            var s = RuntimeLightRules.InitFromSector(200, sectorSpecial: 2, lowestNeighborLight: 40);
            Assert.AreEqual(SectorLightKind.Strobe, s.Kind);
            Assert.AreEqual(200, s.Light);
            Assert.AreEqual(RuntimeLightRules.StrobeBright, s.Count);

            // Burn bright phase
            for (int i = 0; i < RuntimeLightRules.StrobeBright; i++)
                s = RuntimeLightRules.Tick(s, null);
            Assert.AreEqual(40, s.Light);

            for (int i = 0; i < RuntimeLightRules.FastDark; i++)
                s = RuntimeLightRules.Tick(s, null);
            Assert.AreEqual(200, s.Light);
        }

        [Test]
        public void Glow_oscillates_without_crossing_bounds()
        {
            var s = RuntimeLightRules.InitFromSector(128, sectorSpecial: 8, lowestNeighborLight: 64);
            int minSeen = s.Light, maxSeen = s.Light;
            for (int i = 0; i < 64; i++)
            {
                s = RuntimeLightRules.Tick(s, null);
                minSeen = Math.Min(minSeen, s.Light);
                maxSeen = Math.Max(maxSeen, s.Light);
                Assert.That(s.Light, Is.InRange(64, 128));
            }
            Assert.That(minSeen, Is.EqualTo(64));
            Assert.That(maxSeen, Is.EqualTo(128));
        }

        [Test]
        public void Flicker_uses_injected_random_and_stays_in_bounds()
        {
            // (&3)==0 → min; else → max. Count = (r&7)+1.
            int[] seq = { 4, 1, 4, 5 };
            int i = 0;
            int Rng() => seq[i++ % seq.Length];

            var s = RuntimeLightRules.InitFromSector(180, 1, 50);
            // Count starts at 1 → first Tick immediately rolls with seq[0]=4 → min.
            s = RuntimeLightRules.Tick(s, Rng);
            Assert.AreEqual(50, s.Light);
            Assert.That(s.Light, Is.InRange(50, 180));

            while (s.Count > 1)
                s = RuntimeLightRules.Tick(s, Rng);
            s = RuntimeLightRules.Tick(s, Rng); // roll with seq[1]=1 → max
            Assert.AreEqual(180, s.Light);
        }

        [Test]
        public void Linedef_targets_resolve_absolute_and_neighbors()
        {
            Assert.IsTrue(RuntimeLightRules.TryLinedefAction(138, out int bright, out bool strobe));
            Assert.AreEqual(255, bright);
            Assert.IsFalse(strobe);

            Assert.IsTrue(RuntimeLightRules.TryLinedefAction(17, out _, out strobe));
            Assert.IsTrue(strobe);

            Assert.IsTrue(RuntimeLightRules.TryLinedefAction(104, out bright, out _));
            Assert.AreEqual(-2, bright);
        }

        [Test]
        public void Light_clamped_to_0_255()
        {
            var s = SectorLightState.Static(400);
            Assert.AreEqual(255, s.Light);
            s = SectorLightState.Static(-3);
            Assert.AreEqual(0, s.Light);
        }

        [Test]
        public void Sector_special_inventory_covers_E1_light_set()
        {
            foreach (int sp in new[] { 1, 2, 3, 4, 8, 12, 13, 17 })
                Assert.IsTrue(RuntimeLightRules.TryKindFromSectorSpecial(sp, out _),
                    $"special {sp} must map to a light kind");
            Assert.IsFalse(RuntimeLightRules.TryKindFromSectorSpecial(9, out _));
            Assert.IsFalse(RuntimeLightRules.TryKindFromSectorSpecial(7, out _));
        }

        [Test]
        public void Neighbor_helpers_pick_min_and_max()
        {
            // Two sectors sharing a two-sided line: use SyntheticMapBuilder if available;
            // otherwise a minimal MapData via existing test helpers.
            var map = BuildTwoSectorMap(lightA: 100, lightB: 40);
            int low = RuntimeLightRules.LowestNeighborLight(map, 0, s => map.Sectors[s].LightLevel);
            int high = RuntimeLightRules.HighestNeighborLight(map, 0, s => map.Sectors[s].LightLevel);
            Assert.AreEqual(40, low);
            Assert.AreEqual(40, high); // only neighbor is B
        }

        static MapData BuildTwoSectorMap(int lightA, int lightB)
        {
            // Minimal: reuse SyntheticMapBuilder from Map.Tests if referenced —
            // Specials.Tests may not reference Map.Tests. Build lumps inline.
            var verts = new Vertex[]
            {
                new Vertex(0, 0), new Vertex(64, 0), new Vertex(64, 64), new Vertex(0, 64),
                new Vertex(128, 0), new Vertex(128, 64),
            };
            // Sides: 0 front sec0, 1 back sec1 on shared edge
            var sides = new SideDef[]
            {
                new SideDef(0, 0, "-", "-", "-", 0),
                new SideDef(0, 0, "-", "-", "-", 1),
                new SideDef(0, 0, "-", "-", "-", 0),
                new SideDef(0, 0, "-", "-", "-", 0),
                new SideDef(0, 0, "-", "-", "-", 0),
                new SideDef(0, 0, "-", "-", "-", 1),
                new SideDef(0, 0, "-", "-", "-", 1),
                new SideDef(0, 0, "-", "-", "-", 1),
            };
            var lines = new LineDef[]
            {
                // shared two-sided between sec0 and sec1 (verts 1-2)
                new LineDef(1, 2, 0x0004, 0, 0, 0, 1),
                new LineDef(0, 1, 0, 0, 0, 2, -1),
                new LineDef(2, 3, 0, 0, 0, 3, -1),
                new LineDef(3, 0, 0, 0, 0, 4, -1),
                new LineDef(1, 4, 0, 0, 0, 5, -1),
                new LineDef(4, 5, 0, 0, 0, 6, -1),
                new LineDef(5, 2, 0, 0, 0, 7, -1),
            };
            var sectors = new Sector[]
            {
                new Sector(0, 128, "FLOOR", "CEIL", (ushort)lightA, 0, 0),
                new Sector(0, 128, "FLOOR", "CEIL", (ushort)lightB, 0, 0),
            };
            return new MapData("TEST", verts, lines, sides, sectors, Array.Empty<Thing>());
        }
    }
}
