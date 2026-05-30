using System.Collections.Generic;
using Doom.Map;

namespace Doom.Specials
{
    /// Sector adjacency derived from two-sided linedefs.
    public static class Neighbors
    {
        /// Sectors sharing at least one two-sided linedef with `sectorIdx`.
        public static IEnumerable<int> OfSector(MapData map, int sectorIdx)
        {
            var seen = new HashSet<int>();
            foreach (var ld in map.LineDefs)
            {
                if (!ld.IsTwoSided) continue;
                int sFront = SideSector(map, ld.FrontSideIdx);
                int sBack  = SideSector(map, ld.BackSideIdx);
                if (sFront == sectorIdx && sBack >= 0 && sBack != sectorIdx) { if (seen.Add(sBack)) yield return sBack; }
                else if (sBack == sectorIdx && sFront >= 0 && sFront != sectorIdx) { if (seen.Add(sFront)) yield return sFront; }
            }
        }

        private static int SideSector(MapData map, int sideIdx)
            => (sideIdx >= 0 && sideIdx < map.SideDefs.Length) ? map.SideDefs[sideIdx].SectorIdx : -1;
    }
}
