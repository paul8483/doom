using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Doom.Map;

namespace Doom.Specials
{
    /// Sector adjacency derived from two-sided linedefs.
    ///
    /// The adjacency is built once per MapData and cached: the naive version
    /// scanned every linedef (and allocated a HashSet) per call, and callers
    /// hit it in hot paths — NoiseAlert floods the whole map per gunshot,
    /// SectorGeometry asks for the neighbours on every height step of every
    /// moving sector, the light thinkers at init.
    public static class Neighbors
    {
        static readonly ConditionalWeakTable<MapData, int[][]> cache = new();

        /// Sectors sharing at least one two-sided linedef with `sectorIdx`.
        public static IEnumerable<int> OfSector(MapData map, int sectorIdx)
        {
            var adjacency = cache.GetValue(map, Build);
            if (sectorIdx < 0 || sectorIdx >= adjacency.Length) return System.Array.Empty<int>();
            return adjacency[sectorIdx];
        }

        static int[][] Build(MapData map)
        {
            int n = map.Sectors.Length;
            var sets = new List<int>[n];
            var seen = new HashSet<long>();
            foreach (var ld in map.LineDefs)
            {
                if (!ld.IsTwoSided) continue;
                int sFront = SideSector(map, ld.FrontSideIdx);
                int sBack  = SideSector(map, ld.BackSideIdx);
                if (sFront < 0 || sBack < 0 || sFront == sBack) continue;
                if (sFront >= n || sBack >= n) continue;
                Link(sets, seen, sFront, sBack);
                Link(sets, seen, sBack, sFront);
            }
            var result = new int[n][];
            for (int s = 0; s < n; s++)
                result[s] = sets[s] != null ? sets[s].ToArray() : System.Array.Empty<int>();
            return result;
        }

        static void Link(List<int>[] sets, HashSet<long> seen, int from, int to)
        {
            // Preserve first-seen order per sector (the old enumeration order).
            if (!seen.Add(((long)from << 32) | (uint)to)) return;
            (sets[from] ??= new List<int>()).Add(to);
        }

        private static int SideSector(MapData map, int sideIdx)
            => (sideIdx >= 0 && sideIdx < map.SideDefs.Length) ? map.SideDefs[sideIdx].SectorIdx : -1;
    }
}
