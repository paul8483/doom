using System.Collections.Generic;

namespace Doom.Map
{
    public static class WallMeshBuilder
    {
        /// Собирает все стены, видимые из данного сектора:
        /// - для каждой смежной линии смотрим, с какой стороны мы находимся (front/back)
        /// - one-sided: один квад от floor до ceiling нашего сектора
        /// - two-sided: lower-step (если соседский пол выше нашего)
        ///              + upper-step (если соседский потолок ниже нашего)
        ///              middle (текстуры в Stage 4 — пропускаем)
        public static MeshData BuildForSector(MapData map, int sectorIdx, float worldScale = 1f)
        {
            var verts = new List<Float3>();
            var tris  = new List<int>();
            var sec = map.Sectors[sectorIdx];

            for (int i = 0; i < map.LineDefs.Length; i++)
            {
                var ld = map.LineDefs[i];
                if (!IsValidVertex(map, ld.V1) || !IsValidVertex(map, ld.V2)) continue;

                bool onFront = ld.FrontSideIdx >= 0 &&
                               ld.FrontSideIdx < map.SideDefs.Length &&
                               map.SideDefs[ld.FrontSideIdx].SectorIdx == sectorIdx;
                bool onBack  = ld.BackSideIdx  >= 0 &&
                               ld.BackSideIdx  < map.SideDefs.Length &&
                               map.SideDefs[ld.BackSideIdx].SectorIdx == sectorIdx;

                if (!onFront && !onBack) continue;

                var v1 = map.Vertexes[ld.V1];
                var v2 = map.Vertexes[ld.V2];

                // One-sided: одна сторона = один квад во весь объём своего сектора
                if (!ld.IsTwoSided)
                {
                    if (onFront)
                        EmitQuad(verts, tris,
                                 v1, v2,
                                 sec.FloorHeight * worldScale, sec.CeilingHeight * worldScale,
                                 worldScale,
                                 facingFront: true);
                    // Back-сторона на one-sided не бывает по дефиниции — игнорируем
                    continue;
                }

                // Two-sided: вычисляем сосед
                int otherSec = -1;
                if (onFront && ld.BackSideIdx >= 0 && ld.BackSideIdx < map.SideDefs.Length)
                    otherSec = map.SideDefs[ld.BackSideIdx].SectorIdx;
                else if (onBack && ld.FrontSideIdx >= 0 && ld.FrontSideIdx < map.SideDefs.Length)
                    otherSec = map.SideDefs[ld.FrontSideIdx].SectorIdx;
                if (otherSec < 0 || otherSec >= map.Sectors.Length) continue;
                var other = map.Sectors[otherSec];

                // Lower step: соседский пол выше нашего → стена от sec.Floor до other.Floor
                if (other.FloorHeight > sec.FloorHeight)
                {
                    EmitQuad(verts, tris,
                             v1, v2,
                             sec.FloorHeight * worldScale, other.FloorHeight * worldScale,
                             worldScale,
                             facingFront: onFront);
                }
                // Upper step: соседский потолок ниже нашего → стена от other.Ceiling до sec.Ceiling
                if (other.CeilingHeight < sec.CeilingHeight)
                {
                    EmitQuad(verts, tris,
                             v1, v2,
                             other.CeilingHeight * worldScale, sec.CeilingHeight * worldScale,
                             worldScale,
                             facingFront: onFront);
                }
            }

            return new MeshData(verts.ToArray(), tris.ToArray());
        }

        private static void EmitQuad(List<Float3> verts, List<int> tris,
                                     Vertex a, Vertex b, float yLow, float yHigh,
                                     float worldScale,
                                     bool facingFront)
        {
            float ax = a.X * worldScale, az = a.Y * worldScale;
            float bx = b.X * worldScale, bz = b.Y * worldScale;

            // Вырожденный квад (нулевая высота — закрытый сектор floor==ceiling —
            // или нулевая длина при совпавших вершинах) невидим и ломает
            // MeshCollider (PhysX "cleaning the mesh failed"). Не эмитим его.
            float dx = bx - ax, dz = bz - az;
            if (System.Math.Abs(yHigh - yLow) <= 1e-6f || (dx * dx + dz * dz) <= 1e-12f)
                return;

            // a, b — DOOM XY. Unity: X = a.X, Z = a.Y.
            // Квад с углами (a, low), (b, low), (b, high), (a, high).
            // Нормаль: front sidedef справа от a→b. Чтобы нормаль смотрела в front-sector —
            // обходим против часовой при взгляде со стороны front'а.
            // Для facingFront=true: видна со стороны front (справа от a→b) — порядок CCW из (+normal):
            //   (b,low), (a,low), (a,high), (b,high)
            // Для facingFront=false (стена принадлежит back-сектору): противоположный порядок:
            //   (a,low), (b,low), (b,high), (a,high)
            int baseIdx = verts.Count;
            if (facingFront)
            {
                verts.Add(new Float3(bx, yLow,  bz));
                verts.Add(new Float3(ax, yLow,  az));
                verts.Add(new Float3(ax, yHigh, az));
                verts.Add(new Float3(bx, yHigh, bz));
            }
            else
            {
                verts.Add(new Float3(ax, yLow,  az));
                verts.Add(new Float3(bx, yLow,  bz));
                verts.Add(new Float3(bx, yHigh, bz));
                verts.Add(new Float3(ax, yHigh, az));
            }
            tris.Add(baseIdx + 0); tris.Add(baseIdx + 2); tris.Add(baseIdx + 1);
            tris.Add(baseIdx + 0); tris.Add(baseIdx + 3); tris.Add(baseIdx + 2);
        }

        private static bool IsValidVertex(MapData map, int idx)
            => idx >= 0 && idx < map.Vertexes.Length;
    }
}
