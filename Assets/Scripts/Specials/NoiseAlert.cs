using System.Collections.Generic;
using Doom.Map;

namespace Doom.Specials
{
    /// Port of P_NoiseAlert (p_enemy.c), simplified to sector granularity:
    /// sound floods from the source sector across two-sided adjacency and is
    /// stopped by collapsed openings (min ceiling <= max floor). ML_SOUNDBLOCK
    /// half-attenuation is not modeled (deferred).
    public static class NoiseAlert
    {
        /// Set of sector indices that hear a noise made in `sourceSector`,
        /// including the source itself. Empty for a negative source index.
        public static HashSet<int> Compute(MapData map, ISectorHeights heights, int sourceSector)
        {
            var heard = new HashSet<int>();
            if (sourceSector < 0) return heard;
            var queue = new Queue<int>();
            heard.Add(sourceSector);
            queue.Enqueue(sourceSector);
            while (queue.Count > 0)
            {
                int s = queue.Dequeue();
                foreach (int n in Neighbors.OfSector(map, s))
                {
                    if (heard.Contains(n)) continue;
                    if (!OpeningExists(heights, s, n)) continue;
                    heard.Add(n);
                    queue.Enqueue(n);
                }
            }
            return heard;
        }

        private static bool OpeningExists(ISectorHeights h, int a, int b)
        {
            int ceil = System.Math.Min(h.CeilingHeight(a), h.CeilingHeight(b));
            int floor = System.Math.Max(h.FloorHeight(a), h.FloorHeight(b));
            return ceil > floor;
        }
    }
}
