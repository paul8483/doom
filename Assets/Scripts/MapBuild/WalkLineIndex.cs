using System.Collections.Generic;
using Doom.Map;
using Doom.Specials;
using UnityEngine;

namespace Doom.MapBuild
{
    /// Uniform grid over walk-trigger linedefs so HandleWalk is not O(all lines).
    public sealed class WalkLineIndex
    {
        readonly float invCell;
        readonly Dictionary<long, List<int>> cells = new Dictionary<long, List<int>>();
        readonly List<int> scratch = new List<int>(32);

        public WalkLineIndex(MapData map, float worldScale, float cellSizeMeters = 8f)
        {
            invCell = 1f / Mathf.Max(0.5f, cellSizeMeters);
            for (int i = 0; i < map.LineDefs.Length; i++)
            {
                var ld = map.LineDefs[i];
                if (ld.Special == 0) continue;
                if (!LineSpecialTable.TryGet(ld.Special, out var sp)) continue;
                if (sp.Trigger != TriggerKind.Walk) continue;
                if (ld.V1 < 0 || ld.V1 >= map.Vertexes.Length) continue;
                if (ld.V2 < 0 || ld.V2 >= map.Vertexes.Length) continue;
                var v1 = map.Vertexes[ld.V1];
                var v2 = map.Vertexes[ld.V2];
                float ax = v1.X * worldScale, az = v1.Y * worldScale;
                float bx = v2.X * worldScale, bz = v2.Y * worldScale;
                InsertSegment(i, ax, az, bx, bz);
            }
        }

        public void QuerySegment(Vector2 a, Vector2 b, List<int> into)
        {
            into.Clear();
            int minCx = Mathf.FloorToInt(Mathf.Min(a.x, b.x) * invCell);
            int maxCx = Mathf.FloorToInt(Mathf.Max(a.x, b.x) * invCell);
            int minCz = Mathf.FloorToInt(Mathf.Min(a.y, b.y) * invCell);
            int maxCz = Mathf.FloorToInt(Mathf.Max(a.y, b.y) * invCell);
            scratch.Clear();
            for (int cz = minCz; cz <= maxCz; cz++)
            for (int cx = minCx; cx <= maxCx; cx++)
            {
                if (!cells.TryGetValue(Key(cx, cz), out var list)) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    int id = list[i];
                    if (scratch.Contains(id)) continue;
                    scratch.Add(id);
                    into.Add(id);
                }
            }
        }

        void InsertSegment(int lineIndex, float ax, float az, float bx, float bz)
        {
            int minCx = Mathf.FloorToInt(Mathf.Min(ax, bx) * invCell);
            int maxCx = Mathf.FloorToInt(Mathf.Max(ax, bx) * invCell);
            int minCz = Mathf.FloorToInt(Mathf.Min(az, bz) * invCell);
            int maxCz = Mathf.FloorToInt(Mathf.Max(az, bz) * invCell);
            for (int cz = minCz; cz <= maxCz; cz++)
            for (int cx = minCx; cx <= maxCx; cx++)
            {
                long k = Key(cx, cz);
                if (!cells.TryGetValue(k, out var list))
                {
                    list = new List<int>(4);
                    cells[k] = list;
                }
                list.Add(lineIndex);
            }
        }

        static long Key(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;
    }
}
