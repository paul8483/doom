using System.Collections.Generic;
using Doom.Map;

namespace Doom.Specials
{
    /// Pure logic mapping a triggered line to the sectors it moves and the height
    /// each should reach. Reads CURRENT heights via ISectorHeights.
    public static class SectorActions
    {
        /// Sectors carrying the given tag (tag != 0).
        public static IEnumerable<int> FindTaggedSectors(MapData map, int tag)
        {
            if (tag == 0) yield break;
            for (int s = 0; s < map.Sectors.Length; s++)
                if (map.Sectors[s].Tag == tag) yield return s;
        }

        /// Manual door (tag 0, Push): the sector on the line's back side.
        public static IEnumerable<int> FindManualDoorTarget(MapData map, int lineIndex)
        {
            if (lineIndex < 0 || lineIndex >= map.LineDefs.Length) yield break;
            var ld = map.LineDefs[lineIndex];
            int back = (ld.BackSideIdx >= 0 && ld.BackSideIdx < map.SideDefs.Length)
                ? map.SideDefs[ld.BackSideIdx].SectorIdx : -1;
            if (back >= 0) yield return back;
        }

        public static int ComputeTargetHeight(MapData map, ISectorHeights h, int sectorIdx, TargetSpec spec)
        {
            switch (spec)
            {
                case TargetSpec.LowestNeighborCeilingMinus4:
                    return LowestNeighborCeiling(map, h, sectorIdx) - 4;
                case TargetSpec.LowestNeighborCeiling:
                    return LowestNeighborCeiling(map, h, sectorIdx);
                case TargetSpec.LowestNeighborFloor:
                    return LowestNeighborFloor(map, h, sectorIdx);
                case TargetSpec.HighestNeighborFloor:
                    return HighestNeighborFloor(map, h, sectorIdx);
                case TargetSpec.NextHigherFloor:
                    return NextHigherFloor(map, h, sectorIdx);
                case TargetSpec.NextLowerFloor:
                    return NextLowerFloor(map, h, sectorIdx);
                case TargetSpec.ToFloor:
                    return h.FloorHeight(sectorIdx);
                default:
                    return h.FloorHeight(sectorIdx);
            }
        }

        private static int LowestNeighborCeiling(MapData map, ISectorHeights h, int s)
        {
            int best = int.MaxValue; bool any = false;
            foreach (var n in Neighbors.OfSector(map, s)) { any = true; best = System.Math.Min(best, h.CeilingHeight(n)); }
            return any ? best : h.CeilingHeight(s);
        }
        private static int LowestNeighborFloor(MapData map, ISectorHeights h, int s)
        {
            int best = int.MaxValue; bool any = false;
            foreach (var n in Neighbors.OfSector(map, s)) { any = true; best = System.Math.Min(best, h.FloorHeight(n)); }
            return any ? best : h.FloorHeight(s);
        }
        private static int HighestNeighborFloor(MapData map, ISectorHeights h, int s)
        {
            int best = int.MinValue; bool any = false;
            foreach (var n in Neighbors.OfSector(map, s)) { any = true; best = System.Math.Max(best, h.FloorHeight(n)); }
            return any ? best : h.FloorHeight(s);
        }
        private static int NextHigherFloor(MapData map, ISectorHeights h, int s)
        {
            int cur = h.FloorHeight(s); int best = int.MaxValue; bool any = false;
            foreach (var n in Neighbors.OfSector(map, s)) { int f = h.FloorHeight(n); if (f > cur && f < best) { best = f; any = true; } }
            return any ? best : cur;
        }
        private static int NextLowerFloor(MapData map, ISectorHeights h, int s)
        {
            int cur = h.FloorHeight(s); int best = int.MinValue; bool any = false;
            foreach (var n in Neighbors.OfSector(map, s)) { int f = h.FloorHeight(n); if (f < cur && f > best) { best = f; any = true; } }
            return any ? best : cur;
        }

        /// DOOM stair builder: from `startSector`, repeatedly step to an adjacent
        /// sector sharing a two-sided line and having the same floor flat, raising
        /// each floor `stepUnits` above the previous. Returns sectors in order with
        /// their target floor heights.
        public static List<(int sector, int targetFloor)> BuildStairChain(
            MapData map, ISectorHeights h, int startSector, int stepUnits)
        {
            var result = new List<(int, int)>();
            var visited = new HashSet<int>();
            int current = startSector;
            int height = h.FloorHeight(startSector);
            string flat = map.Sectors[startSector].FloorFlat;
            while (current >= 0 && visited.Add(current))
            {
                height += stepUnits;
                result.Add((current, height));
                int next = -1;
                foreach (var n in Neighbors.OfSector(map, current))
                {
                    if (visited.Contains(n)) continue;
                    if (map.Sectors[n].FloorFlat == flat) { next = n; break; }
                }
                current = next;
            }
            return result;
        }
    }
}
