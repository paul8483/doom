using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Doom.Wad;
using Doom.Map;
using Doom.Map.Tests;

namespace Doom.Specials.Tests
{
    public class NoiseAlertTests
    {
        // ── Синтетика: три сектора в цепочку A-B-C через двусторонние линии ──
        //
        // Layout (DOOM units):
        //   v0(0,0)   v1(64,0)   v2(128,0)   v3(192,0)
        //   v4(0,64)  v5(64,64)  v6(128,64)  v7(192,64)
        //
        //   Sector 0 (A) = v0,v1,v5,v4; Sector 1 (B) = v1,v2,v6,v5;
        //   Sector 2 (C) = v2,v3,v7,v6.
        //   Shared edges: v1↔v5 (A|B) and v2↔v6 (B|C), both two-sided.
        private static MapData BuildChainMap()
        {
            const ushort NONE = 0xFFFF;

            var wadBytes = SyntheticMapBuilder.BuildMapWad(
                "MAP01",
                things: SyntheticMapBuilder.BuildThings((32, 32, 0, 1, 0)),
                vertexes: SyntheticMapBuilder.BuildVertexes(
                    (0, 0),      // v0
                    (64, 0),     // v1
                    (128, 0),    // v2
                    (192, 0),    // v3
                    (0, 64),     // v4
                    (64, 64),    // v5
                    (128, 64),   // v6
                    (192, 64)),  // v7
                linedefs: SyntheticMapBuilder.BuildLineDefs(
                    // Sector 0 outer edges (one-sided)
                    (0, 1, 0, 0, 0, 0, NONE),   // v0→v1 bottom
                    (4, 0, 0, 0, 0, 1, NONE),   // v4→v0 left
                    (5, 4, 0, 0, 0, 2, NONE),   // v5→v4 top
                    // Shared edge A|B: two-sided, front → sector 0, back → sector 1
                    (1, 5, 0, 0, 0, 3, 4),
                    // Sector 1 outer edges (one-sided)
                    (1, 2, 0, 0, 0, 5, NONE),   // v1→v2 bottom
                    (6, 5, 0, 0, 0, 6, NONE),   // v6→v5 top
                    // Shared edge B|C: two-sided, front → sector 1, back → sector 2
                    (2, 6, 0, 0, 0, 7, 8),
                    // Sector 2 outer edges (one-sided)
                    (2, 3, 0, 0, 0, 9, NONE),   // v2→v3 bottom
                    (3, 7, 0, 0, 0, 10, NONE),  // v3→v7 right
                    (7, 6, 0, 0, 0, 11, NONE)), // v7→v6 top
                sidedefs: SyntheticMapBuilder.BuildSideDefs(
                    (0, 0, "-", "-", "WALL01", 0),  // side 0  → sector 0
                    (0, 0, "-", "-", "WALL01", 0),  // side 1  → sector 0
                    (0, 0, "-", "-", "WALL01", 0),  // side 2  → sector 0
                    (0, 0, "-", "-", "-", 0),       // side 3  → sector 0 (A|B front)
                    (0, 0, "-", "-", "-", 1),       // side 4  → sector 1 (A|B back)
                    (0, 0, "-", "-", "WALL01", 1),  // side 5  → sector 1
                    (0, 0, "-", "-", "WALL01", 1),  // side 6  → sector 1
                    (0, 0, "-", "-", "-", 1),       // side 7  → sector 1 (B|C front)
                    (0, 0, "-", "-", "-", 2),       // side 8  → sector 2 (B|C back)
                    (0, 0, "-", "-", "WALL01", 2),  // side 9  → sector 2
                    (0, 0, "-", "-", "WALL01", 2),  // side 10 → sector 2
                    (0, 0, "-", "-", "WALL01", 2)), // side 11 → sector 2
                sectors: SyntheticMapBuilder.BuildSectors(
                    (0, 128, "FLAT01", "FLAT01", 192, 0, 0),
                    (0, 128, "FLAT01", "FLAT01", 192, 0, 0),
                    (0, 128, "FLAT01", "FLAT01", 192, 0, 0)));

            using var wad = new WadFile(new MemoryStream(wadBytes), ownsStream: true);
            return MapData.Load(wad, "MAP01");
        }

        /// Test-local mutable heights: starts from the WAD values, lets a test
        /// collapse individual sectors (close a "door") without touching MapData.
        private sealed class MutableHeights : ISectorHeights
        {
            private readonly int[] floors;
            private readonly int[] ceils;

            public MutableHeights(MapData map)
            {
                floors = new int[map.Sectors.Length];
                ceils = new int[map.Sectors.Length];
                for (int i = 0; i < map.Sectors.Length; i++)
                {
                    floors[i] = map.Sectors[i].FloorHeight;
                    ceils[i] = map.Sectors[i].CeilingHeight;
                }
            }

            public int FloorHeight(int s) => floors[s];
            public int CeilingHeight(int s) => ceils[s];
            public void SetCeiling(int s, int h) => ceils[s] = h;
        }

        private static MutableHeights OpenHeights(MapData map) => new MutableHeights(map);

        private static void CloseSector(MutableHeights heights, int sector)
            => heights.SetCeiling(sector, heights.FloorHeight(sector));

        [Test]
        public void Sound_floods_through_open_sectors()
        {
            var map = BuildChainMap();                    // A(0)-B(1)-C(2), проёмы открыты
            var heights = OpenHeights(map);               // потолки выше полов везде
            var heard = NoiseAlert.Compute(map, heights, sourceSector: 0);
            Assert.That(heard, Is.SupersetOf(new[] { 0, 1, 2 }));
        }

        [Test]
        public void Closed_door_blocks_sound()
        {
            var map = BuildChainMap();
            var heights = OpenHeights(map);
            CloseSector(heights, 1);                      // B: потолок опущен до пола
            var heard = NoiseAlert.Compute(map, heights, sourceSector: 0);
            Assert.That(heard, Has.Member(0));
            Assert.That(heard, Has.No.Member(2), "за закрытой дверью не слышно");
        }

        // ── Freedoom E1M1 integration ─────────────────────────────────────────

        private static string FreedoomPath =>
            Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");

        [Test]
        public void Freedoom_e1m1_closed_door_blocks()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var map = MapData.Load(wad, "E1M1");
            var heights = new StaticSectorHeights(map);

            int start = FindPlayerStartSector(map);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "player-start sector not found");
            Assert.That(map.Sectors[start].CeilingHeight,
                Is.GreaterThan(map.Sectors[start].FloorHeight),
                "player-start sector should be open");

            var heard = NoiseAlert.Compute(map, heights, start);
            Assert.That(heard, Is.Not.Empty);
            Assert.That(heard, Has.Member(start));

            // Closed door sectors (ceiling collapsed onto floor) that touch the map.
            var doors = Enumerable.Range(0, map.Sectors.Length)
                .Where(s => map.Sectors[s].CeilingHeight <= map.Sectors[s].FloorHeight)
                .Where(s => Neighbors.OfSector(map, s).Any())
                .ToList();
            Assert.That(doors, Is.Not.Empty, "E1M1 should contain closed door sectors");

            bool blockedSomething = false;
            foreach (int door in doors)
            {
                // The shut door itself never hears the noise…
                Assert.That(heard, Has.No.Member(door),
                    $"closed door sector {door} heard the noise");

                // …and neither does anything reachable ONLY through that door.
                foreach (int behind in SectorsOnlyReachableThrough(map, start, door))
                {
                    Assert.That(heard, Has.No.Member(behind),
                        $"sector {behind} behind closed door {door} heard the noise");
                    blockedSomething = true;
                }
            }
            Assert.That(blockedSomething, Is.True,
                "expected at least one E1M1 sector to sit exclusively behind a closed door");
        }

        /// Sector containing the Type-1 (Player 1 start) THING, found by a
        /// crossing-number test against the sector's boundary linedefs.
        private static int FindPlayerStartSector(MapData map)
        {
            foreach (var thing in map.Things)
            {
                if (thing.Type != 1) continue;
                for (int s = 0; s < map.Sectors.Length; s++)
                    if (PointInSector(map, s, thing.X, thing.Y)) return s;
            }
            return -1;
        }

        private static bool PointInSector(MapData map, int sector, float px, float py)
        {
            bool inside = false;
            foreach (var ld in map.LineDefs)
            {
                int front = SideSector(map, ld.FrontSideIdx);
                int back = SideSector(map, ld.BackSideIdx);
                // Boundary edge of `sector` = exactly one side faces it
                // (front==back==sector is an internal seam, parity-neutral — skip).
                if ((front == sector) == (back == sector)) continue;

                float x1 = map.Vertexes[ld.V1].X, y1 = map.Vertexes[ld.V1].Y;
                float x2 = map.Vertexes[ld.V2].X, y2 = map.Vertexes[ld.V2].Y;
                if ((y1 > py) != (y2 > py) &&
                    px < x1 + (py - y1) / (y2 - y1) * (x2 - x1))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        private static int SideSector(MapData map, int sideIdx)
            => (sideIdx >= 0 && sideIdx < map.SideDefs.Length)
               ? map.SideDefs[sideIdx].SectorIdx : -1;

        /// Sectors reachable from `from` over raw two-sided adjacency, but only
        /// via paths that pass through `gate` — i.e. cut off when `gate` closes.
        private static IEnumerable<int> SectorsOnlyReachableThrough(
            MapData map, int from, int gate)
        {
            var withGate = AdjacencyFlood(map, from, blocked: -1);
            var withoutGate = AdjacencyFlood(map, from, blocked: gate);
            return withGate.Where(s => s != gate && !withoutGate.Contains(s));
        }

        private static HashSet<int> AdjacencyFlood(MapData map, int from, int blocked)
        {
            var seen = new HashSet<int> { from };
            var queue = new Queue<int>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                int s = queue.Dequeue();
                foreach (int n in Neighbors.OfSector(map, s))
                {
                    if (n == blocked || !seen.Add(n)) continue;
                    queue.Enqueue(n);
                }
            }
            return seen;
        }
    }
}
