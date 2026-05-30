using System.IO;
using Doom.Map;
using Doom.Map.Tests;
using Doom.Wad;

namespace Doom.Specials.Tests
{
    /// Test-side factory for small synthetic <see cref="MapData"/> instances used
    /// by the Specials test suite. Builds maps through <see cref="SyntheticMapBuilder"/>
    /// (byte blobs → synthetic WAD → <see cref="MapData.Load"/>), so the resulting
    /// maps go through the real lump parsers.
    public static class SpecialsTestMaps
    {
        /// Two square sectors side by side, sharing one two-sided linedef.
        ///
        /// Layout (DOOM units):
        ///   v0(0,0)   v1(64,0)   v2(128,0)
        ///   v3(0,64)  v4(64,64)  v5(128,64)
        ///
        ///   Sector 0 = left square (v0,v1,v4,v3).
        ///   Sector 1 = right square (v1,v2,v5,v4).
        ///   Shared edge = v1↔v4: a two-sided line, front → sector 0, back → sector 1.
        public static MapData TwoAdjacentSectors(int floor0, int ceil0, int floor1, int ceil1)
        {
            return Build(
                sectors: new[]
                {
                    new SectorSpec((short)floor0, (short)ceil0),
                    new SectorSpec((short)floor1, (short)ceil1),
                });
        }

        /// Index of the shared two-sided linedef in the fixed two-square layout.
        /// The builder emits the three left-square outer edges first (indices 0..2),
        /// then the shared edge v1↔v4 (index 3, front → sector 0, back → sector 1),
        /// then the three right-square outer edges.
        public const int DoorLineIndex = 3;

        /// Index of the lift sector in <see cref="LiftSetup"/> (the back-side sector).
        public const int LiftSector = 1;

        /// Two adjacent sectors sharing one two-sided line. Sector 0 is the room
        /// (floor 0, ceiling 128). Sector 1 is the DOOR sector on the BACK side of
        /// the shared line, sitting closed: ceiling == floor (0, 0).
        public static MapData DoorSetup()
        {
            return Build(
                sectors: new[]
                {
                    new SectorSpec(0, 128),  // sector 0 — room
                    new SectorSpec(0, 0),    // sector 1 — closed door (ceil == floor)
                });
        }

        /// Two adjacent sectors sharing one two-sided line. The lift sector
        /// (index <see cref="LiftSector"/> = 1) has floor 64; its neighbor
        /// (sector 0) has floor 0. So LowestNeighborFloor of the lift == 0.
        public static MapData LiftSetup()
        {
            return Build(
                sectors: new[]
                {
                    new SectorSpec(0, 128),   // sector 0 — neighbor, floor 0
                    new SectorSpec(64, 128),  // sector 1 — lift, floor 64
                });
        }

        /// Two adjacent sectors BOTH carrying <paramref name="tag"/>, so
        /// FindTaggedSectors(tag) returns {0, 1}.
        public static MapData TwoTaggedSectors(int tag)
        {
            return Build(
                sectors: new[]
                {
                    new SectorSpec(0, 128, (ushort)tag),
                    new SectorSpec(0, 128, (ushort)tag),
                });
        }

        // ── Internal extensible builder ────────────────────────────────────────
        // Constructs the fixed two-square layout, parameterized only by the per-sector
        // heights and tags. The named setups above layer specials/tags onto the same
        // geometry.

        private readonly struct SectorSpec
        {
            public readonly short Floor;
            public readonly short Ceil;
            public readonly ushort Tag;
            public SectorSpec(short floor, short ceil, ushort tag = 0) { Floor = floor; Ceil = ceil; Tag = tag; }
        }

        private static MapData Build(SectorSpec[] sectors)
        {
            // Exactly two sectors for the shared-edge layout.
            short f0 = sectors[0].Floor, c0 = sectors[0].Ceil;
            short f1 = sectors[1].Floor, c1 = sectors[1].Ceil;
            ushort t0 = sectors[0].Tag, t1 = sectors[1].Tag;

            const ushort NONE = 0xFFFF;

            var wadBytes = SyntheticMapBuilder.BuildMapWad(
                "MAP01",
                things: SyntheticMapBuilder.BuildThings((32, 32, 0, 1, 0)),
                vertexes: SyntheticMapBuilder.BuildVertexes(
                    (0, 0),     // v0
                    (64, 0),    // v1
                    (128, 0),   // v2
                    (0, 64),    // v3
                    (64, 64),   // v4
                    (128, 64)), // v5
                linedefs: SyntheticMapBuilder.BuildLineDefs(
                    // Left square outer edges (one-sided, front → side 0..2)
                    (0, 1, 0, 0, 0, 0, NONE),  // v0→v1 bottom
                    (3, 0, 0, 0, 0, 1, NONE),  // v3→v0 left
                    (4, 3, 0, 0, 0, 2, NONE),  // v4→v3 top
                    // Shared edge v1↔v4: two-sided, front → side 3 (sector 0), back → side 4 (sector 1)
                    (1, 4, 0, 0, 0, 3, 4),
                    // Right square outer edges (one-sided, front → side 5..7)
                    (1, 2, 0, 0, 0, 5, NONE),  // v1→v2 bottom
                    (2, 5, 0, 0, 0, 6, NONE),  // v2→v5 right
                    (5, 4, 0, 0, 0, 7, NONE)), // v5→v4 top
                sidedefs: SyntheticMapBuilder.BuildSideDefs(
                    (0, 0, "-", "-", "WALL01", 0),  // side 0 → sector 0
                    (0, 0, "-", "-", "WALL01", 0),  // side 1 → sector 0
                    (0, 0, "-", "-", "WALL01", 0),  // side 2 → sector 0
                    (0, 0, "-", "-", "-", 0),       // side 3 → sector 0 (shared, front)
                    (0, 0, "-", "-", "-", 1),       // side 4 → sector 1 (shared, back)
                    (0, 0, "-", "-", "WALL01", 1),  // side 5 → sector 1
                    (0, 0, "-", "-", "WALL01", 1),  // side 6 → sector 1
                    (0, 0, "-", "-", "WALL01", 1)), // side 7 → sector 1
                sectors: SyntheticMapBuilder.BuildSectors(
                    (f0, c0, "FLAT01", "FLAT01", 192, 0, t0),
                    (f1, c1, "FLAT01", "FLAT01", 192, 0, t1)));

            using var wad = new WadFile(new MemoryStream(wadBytes), ownsStream: true);
            return MapData.Load(wad, "MAP01");
        }
    }
}
