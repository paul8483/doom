using System.Collections.Generic;

namespace Doom.Map
{
    /// Восстанавливает замкнутые контуры (outer + holes) каждого сектора
    /// из linedefs/sidedefs.
    ///
    /// Алгоритм:
    /// 1. Для каждого linedef'а сторона с sidedef'ом (front и/или back)
    ///    добавляет линию (V1, V2) в множество рёбер своего сектора.
    ///    Направление V1→V2 в WAD'ах не всегда согласовано с обходом сектора
    ///    (особенно у синтетических тестовых данных), поэтому chain'инг
    ///    работает по НЕНАПРАВЛЕННОМУ графу: у каждой вершины должно быть
    ///    чётное число инцидентных рёбер, и обход — Эйлеров цикл.
    /// 2. Внутри сектора собираем замкнутые loops. Каждое ребро используется
    ///    ровно один раз. Если ребро «висит» (вершина с нечётной степенью) —
    ///    контур открыт, помечаем сектор invalid и логируем warning.
    /// 3. Классифицируем loops: максимальная по |signed area| — outer
    ///    (форсим CCW: положительная площадь), остальные — holes
    ///    (форсим CW: отрицательная площадь).
    public static class SectorPolygonBuilder
    {
        public static SectorPolygon[] Build(MapData map)
        {
            // edges[sector] = список ненаправленных рёбер (a, b),
            // где a и b — индексы вершин (порядок неважен).
            var edges = new Dictionary<int, List<(int a, int b)>>();

            for (int i = 0; i < map.LineDefs.Length; i++)
            {
                var ld = map.LineDefs[i];

                if (!IsValidVertex(map, ld.V1) || !IsValidVertex(map, ld.V2))
                {
                    MapLog.Warning($"LineDef {i}: vertex out of range, пропускаем");
                    continue;
                }

                if (ld.FrontSideIdx >= 0)
                {
                    int sec = SafeSectorOf(map, ld.FrontSideIdx, i);
                    if (sec >= 0) Push(edges, sec, ld.V1, ld.V2);
                }
                if (ld.BackSideIdx >= 0)
                {
                    int sec = SafeSectorOf(map, ld.BackSideIdx, i);
                    if (sec >= 0) Push(edges, sec, ld.V1, ld.V2);
                }
            }

            var result = new SectorPolygon[map.Sectors.Length];
            for (int s = 0; s < map.Sectors.Length; s++)
            {
                if (!edges.TryGetValue(s, out var list))
                {
                    result[s] = SectorPolygon.Invalid(s);
                    continue;
                }

                result[s] = BuildForSector(map, s, list);
                if (!result[s].IsValid)
                {
                    MapLog.Warning($"Sector {s}: открытый контур, не удалось замкнуть полигон");
                }
            }
            return result;
        }

        private static bool IsValidVertex(MapData map, int vIdx)
            => vIdx >= 0 && vIdx < map.Vertexes.Length;

        private static int SafeSectorOf(MapData map, int sideIdx, int lineIdx)
        {
            if (sideIdx < 0 || sideIdx >= map.SideDefs.Length)
            {
                MapLog.Warning($"LineDef {lineIdx}: sidedef {sideIdx} out of range");
                return -1;
            }
            int sec = map.SideDefs[sideIdx].SectorIdx;
            if (sec < 0 || sec >= map.Sectors.Length)
            {
                MapLog.Warning($"SideDef {sideIdx}: sector {sec} out of range");
                return -1;
            }
            return sec;
        }

        private static void Push(Dictionary<int, List<(int a, int b)>> edges,
                                 int sector, int a, int b)
        {
            if (!edges.TryGetValue(sector, out var list))
            {
                list = new List<(int a, int b)>();
                edges[sector] = list;
            }
            list.Add((a, b));
        }

        private static SectorPolygon BuildForSector(
            MapData map, int sectorIdx, List<(int a, int b)> all)
        {
            // Adjacency: vertex -> list of edge indices incident to it.
            var inc = new Dictionary<int, List<int>>();
            for (int i = 0; i < all.Count; i++)
            {
                AddInc(inc, all[i].a, i);
                AddInc(inc, all[i].b, i);
            }

            // Detect T-junctions: a vertex with degree > 2 means more than two edges meet
            // there, which breaks simple Eulerian-circuit chaining.
            foreach (var kv in inc)
            {
                if (kv.Value.Count > 2)
                    MapLog.Warning($"Sector {sectorIdx}: T-junction at vertex {kv.Key} (degree {kv.Value.Count})");
            }

            var used = new bool[all.Count];
            var loops = new List<List<int>>();

            for (int start = 0; start < all.Count; start++)
            {
                if (used[start]) continue;

                var loop = new List<int>();
                int startVertex = all[start].a;
                int currentVertex = all[start].b;
                used[start] = true;
                loop.Add(startVertex);

                bool closed = false;
                while (true)
                {
                    if (currentVertex == all[start].a)
                    {
                        // Дошли до начальной вершины — loop замкнут.
                        closed = true;
                        break;
                    }
                    loop.Add(currentVertex);

                    // Найти следующее неиспользованное ребро инцидентное currentVertex.
                    int nextEdge = FindNextEdge(inc, used, currentVertex);
                    if (nextEdge < 0) break; // тупик — контур открыт

                    used[nextEdge] = true;
                    var e = all[nextEdge];
                    int nextVertex = (e.a == currentVertex) ? e.b : e.a;
                    currentVertex = nextVertex;
                }

                if (closed)
                {
                    loops.Add(loop);
                }
                else
                {
                    // Открытый контур — весь сектор inválido.
                    return SectorPolygon.Invalid(sectorIdx);
                }
            }

            // Классификация: максимальный по |area| — outer; остальные — holes.
            int outerIdx = 0;
            double outerAbs = 0;
            var areas = new double[loops.Count];
            for (int k = 0; k < loops.Count; k++)
            {
                areas[k] = SignedArea(map.Vertexes, loops[k]);
                double abs = areas[k] < 0 ? -areas[k] : areas[k];
                if (abs > outerAbs) { outerAbs = abs; outerIdx = k; }
            }

            var outer = loops[outerIdx];
            if (areas[outerIdx] < 0) outer.Reverse();

            var holes = new List<IReadOnlyList<int>>();
            for (int k = 0; k < loops.Count; k++)
            {
                if (k == outerIdx) continue;
                var hole = loops[k];
                // Дырки должны идти в противоположном направлении от outer (CW).
                // Если signed area > 0 (CCW) — реверсим.
                double a = SignedArea(map.Vertexes, hole);
                if (a > 0) hole.Reverse();
                holes.Add(hole);
            }

            return new SectorPolygon(sectorIdx, true, outer, holes);
        }

        private static void AddInc(Dictionary<int, List<int>> inc, int v, int edgeIdx)
        {
            if (!inc.TryGetValue(v, out var l))
            {
                l = new List<int>();
                inc[v] = l;
            }
            l.Add(edgeIdx);
        }

        private static int FindNextEdge(Dictionary<int, List<int>> inc, bool[] used,
                                        int atVertex)
        {
            if (!inc.TryGetValue(atVertex, out var candidates)) return -1;
            for (int i = 0; i < candidates.Count; i++)
            {
                int idx = candidates[i];
                if (!used[idx]) return idx;
            }
            return -1;
        }

        private static double SignedArea(Vertex[] verts, IReadOnlyList<int> ring)
        {
            double a = 0;
            for (int i = 0; i < ring.Count; i++)
            {
                var p0 = verts[ring[i]];
                var p1 = verts[ring[(i + 1) % ring.Count]];
                a += (double)p0.X * p1.Y - (double)p1.X * p0.Y;
            }
            return 0.5 * a;
        }
    }
}
