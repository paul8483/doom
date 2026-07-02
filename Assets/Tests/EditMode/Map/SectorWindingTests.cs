using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Wad;

namespace Doom.Map.Tests
{
    /// Регрессия: ориентация треугольников пола/потолка должна быть АБСОЛЮТНОЙ
    /// (пол виден сверху, потолок снизу), независимо от того, как LibTess выбрал
    /// нормаль проекции. У секторов из нескольких НЕСВЯЗНЫХ колец (чередующиеся
    /// ступени лестницы под одним номером сектора — E1M1: 74/85, ящики и т.п.)
    /// SectorPolygonBuilder классифицирует лишние кольца как «дырки» и принудительно
    /// закручивает их по часовой; заливка EvenOdd от этого не страдает, но LibTess
    /// вычисляет нормаль из СУММЫ знаковых площадей контуров, и когда «дырки»
    /// перевешивают outer, нормаль переворачивается — весь пол сектора смотрит вниз,
    /// потолок вверх, оба отсекаются back-face culling'ом (синие дыры в E1M1).
    public class SectorWindingTests
    {
        // ── Unit: сектор из трёх несвязных квадратов ──────────────────────────
        // Outer (максимальный по площади) = 80×80 = 6400; два оставшихся кольца
        // 72×72 = 5184 каждое классифицируются как «дырки» и в сумме (10368)
        // перевешивают outer → суммарная знаковая площадь отрицательна → без
        // фикса LibTess переворачивает все треугольники.
        static MapData ThreeDisjointSquares()
        {
            var verts = new List<Vertex>();
            var lines = new List<LineDef>();
            var sides = new List<SideDef>();

            void AddSquare(short x0, short y0, short size)
            {
                int b = verts.Count;
                verts.Add(new Vertex(x0, y0));
                verts.Add(new Vertex((short)(x0 + size), y0));
                verts.Add(new Vertex((short)(x0 + size), (short)(y0 + size)));
                verts.Add(new Vertex(x0, (short)(y0 + size)));
                for (int i = 0; i < 4; i++)
                {
                    lines.Add(new LineDef(b + i, b + (i + 1) % 4, 0, 0, 0, sides.Count, -1));
                    sides.Add(new SideDef(0, 0, "-", "-", "W", 0));
                }
            }

            AddSquare(0, 0, 80);
            AddSquare(200, 0, 72);
            AddSquare(400, 0, 72);

            var sectors = new[] { new Sector(0, 128, "F", "F", 160, 0, 0) };
            return new MapData("TEST", verts.ToArray(), lines.ToArray(),
                               sides.ToArray(), sectors, System.Array.Empty<Thing>());
        }

        [Test]
        public void Disjoint_rings_floor_faces_up_ceiling_faces_down()
        {
            var map = ThreeDisjointSquares();
            var polys = SectorPolygonBuilder.Build(map);
            Assert.That(polys[0].IsValid, Is.True, "полигон должен собраться");

            var floor = SectorTriangulator.TriangulateFloor(map, polys[0]);
            var ceiling = SectorTriangulator.TriangulateCeiling(map, polys[0]);

            // Все три квадрата покрыты: 3 кольца × 2 треугольника.
            Assert.That(floor.Triangles.Length / 3, Is.EqualTo(6));

            AssertAllFaceY(floor, up: true, "floor");
            AssertAllFaceY(ceiling, up: false, "ceiling");
        }

        // ── Integration: все полы/потолки E1M1 смотрят в правильную сторону ───
        [Test]
        public void E1M1_floors_face_up_ceilings_face_down()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = WadFile.Open(path);
            var map = MapData.Load(wad, "E1M1");
            var meshes = MapGeometryBuilder.Build(map);

            var bad = new List<string>();
            foreach (var sm in meshes)
            {
                int floorDown = CountFacing(sm.Floor, up: false);
                int ceilUp = CountFacing(sm.Ceiling, up: true);
                if (floorDown > 0 || ceilUp > 0)
                    bad.Add($"sector {sm.SectorIdx}: floorDownTris={floorDown} ceilUpTris={ceilUp}");
            }
            Assert.That(bad, Is.Empty,
                "Полы, смотрящие вниз / потолки, смотрящие вверх (culled → дыры):\n"
                + string.Join("\n", bad));
        }

        // ── Walls: квады должны смотреть В свой сектор ─────────────────────────
        // Unity рисует сторону, куда указывает Cross(p1-p0, p2-p0) (левосторонние
        // координаты, front = по часовой). Стена, принадлежащая сектору S, обязана
        // быть видимой ИЗНУТРИ S: для front-сайда S справа от V1→V2, для back-сайда
        // слева. До фикса EmitQuad заворачивал все квады наизнанку: стены были
        // видимы только с противоположной стороны (изнанка коробок вместо лицевых
        // граней; в открытых зонах — сквозные дыры до фона).

        [Test]
        public void Front_side_wall_faces_front_sector()
        {
            // Одна односторонняя линия (0,0)→(64,0): front-сектор СПРАВА = юг (y<0).
            var verts = new[] { new Vertex(0, 0), new Vertex(64, 0) };
            var lines = new[] { new LineDef(0, 1, 0, 0, 0, 0, -1) };
            var sides = new[] { new SideDef(0, 0, "-", "-", "MID", 0) };
            var sectors = new[] { new Sector(0, 128, "F", "F", 160, 0, 0) };
            var map = new MapData("T", verts, lines, sides, sectors, System.Array.Empty<Thing>());

            var walls = WallMeshBuilder.BuildForSector(map, 0, new FlatSizes());
            Assert.That(walls.Count, Is.EqualTo(1));
            // Нормаль каждого треугольника должна указывать на юг (Unity -Z).
            AssertAllFaceZ(walls[0].Mesh, positiveZ: false, "front-side quad");
        }

        [Test]
        public void Back_side_wall_faces_back_sector()
        {
            // Двусторонняя линия (0,0)→(64,0): front-сектор 0 (юг, пол выше),
            // back-сектор 1 (север, пол ниже) видит lower-ступень со своей стороны.
            var verts = new[] { new Vertex(0, 0), new Vertex(64, 0) };
            var lines = new[] { new LineDef(0, 1, 0, 0, 0, 0, 1) };
            var sides = new[]
            {
                new SideDef(0, 0, "-", "-", "-", 0),
                new SideDef(0, 0, "-", "LOW", "-", 1),
            };
            var sectors = new[]
            {
                new Sector(32, 128, "F", "F", 160, 0, 0),
                new Sector(0, 128, "F", "F", 160, 0, 0),
            };
            var map = new MapData("T", verts, lines, sides, sectors, System.Array.Empty<Thing>());

            var walls = WallMeshBuilder.BuildForSector(map, 1, new FlatSizes());
            Assert.That(walls.Count, Is.EqualTo(1));
            // Back-сектор 1 на севере → нормаль на север (Unity +Z).
            AssertAllFaceZ(walls[0].Mesh, positiveZ: true, "back-side quad");
        }

        [Test]
        public void E1M1_one_sided_walls_face_their_front_sector()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = WadFile.Open(path);
            var map = MapData.Load(wad, "E1M1");
            var meshes = MapGeometryBuilder.Build(map);

            int agree = 0;
            var bad = new List<string>();
            for (int i = 0; i < map.LineDefs.Length; i++)
            {
                var ld = map.LineDefs[i];
                if (ld.IsTwoSided) continue;
                if (ld.FrontSideIdx < 0 || ld.FrontSideIdx >= map.SideDefs.Length) continue;
                int s = map.SideDefs[ld.FrontSideIdx].SectorIdx;
                if (s < 0 || s >= meshes.Length) continue;

                var a = map.Vertexes[ld.V1];
                var b = map.Vertexes[ld.V2];
                float dx = b.X - a.X, dz = b.Y - a.Y;
                float len = Mathf.Sqrt(dx * dx + dz * dz);
                if (len < 1e-3f) continue;
                // Front-сектор справа от V1→V2: (dz, -dx).
                float ex = dz, ez = -dx;

                foreach (var w in meshes[s].Walls)
                {
                    var v = w.Mesh.Vertices;
                    var t = w.Mesh.Triangles;
                    for (int k = 0; k < t.Length; k += 3)
                    {
                        if (!TriOnSegment(v, t, k, a, b, dx, dz, len)) continue;
                        var p0 = v[t[k]]; var p1 = v[t[k + 1]]; var p2 = v[t[k + 2]];
                        var n = Vector3.Cross(
                            new Vector3(p1.X - p0.X, p1.Y - p0.Y, p1.Z - p0.Z),
                            new Vector3(p2.X - p0.X, p2.Y - p0.Y, p2.Z - p0.Z));
                        float dot = n.x * ex + n.z * ez;
                        if (dot > 0f) agree++;
                        else if (dot < 0f && bad.Count < 20)
                            bad.Add($"line {i} sector {s} tex='{w.Texture}'");
                    }
                }
            }

            Assert.That(bad, Is.Empty,
                $"Односторонние стены, вывернутые от своего сектора (правильных {agree}):\n"
                + string.Join("\n", bad));
            Assert.That(agree, Is.GreaterThan(500), "sanity: сегменты вообще матчились");
        }

        static void AssertAllFaceZ(MeshData m, bool positiveZ, string what)
        {
            Assert.That(m.Triangles.Length, Is.GreaterThan(0), $"{what}: пустой меш");
            var v = m.Vertices;
            var t = m.Triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                var p0 = v[t[i]]; var p1 = v[t[i + 1]]; var p2 = v[t[i + 2]];
                var n = Vector3.Cross(
                    new Vector3(p1.X - p0.X, p1.Y - p0.Y, p1.Z - p0.Z),
                    new Vector3(p2.X - p0.X, p2.Y - p0.Y, p2.Z - p0.Z));
                Assert.That(positiveZ ? n.z : -n.z, Is.GreaterThan(0f),
                    $"{what}: треугольник {i / 3} смотрит не в свой сектор (n.z={n.z})");
            }
        }

        static bool TriOnSegment(Float3[] v, int[] t, int k, Vertex a, Vertex b,
                                 float dx, float dz, float len)
        {
            const float eps = 0.5f;
            float minX = Mathf.Min(a.X, b.X) - eps, maxX = Mathf.Max(a.X, b.X) + eps;
            float minZ = Mathf.Min(a.Y, b.Y) - eps, maxZ = Mathf.Max(a.Y, b.Y) + eps;
            for (int j = 0; j < 3; j++)
            {
                var p = v[t[k + j]];
                float dist = Mathf.Abs(dz * (p.X - a.X) - dx * (p.Z - a.Y)) / len;
                if (dist > eps || p.X < minX || p.X > maxX || p.Z < minZ || p.Z > maxZ)
                    return false;
            }
            return true;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        static void AssertAllFaceY(MeshData m, bool up, string what)
        {
            int wrong = CountFacing(m, up: !up);
            int right = CountFacing(m, up: up);
            Assert.That(wrong, Is.EqualTo(0),
                $"{what}: {wrong} треугольников смотрят {(up ? "вниз" : "вверх")} " +
                $"(правильных {right}) — будут отсечены back-face culling'ом");
        }

        /// Количество треугольников, чья нормаль (Cross(p1-p0, p2-p0)) имеет
        /// Y-компоненту нужного знака.
        static int CountFacing(MeshData m, bool up)
        {
            int n = 0;
            var v = m.Vertices;
            var t = m.Triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                var a = v[t[i]]; var b = v[t[i + 1]]; var c = v[t[i + 2]];
                float ny = (b.Z - a.Z) * (c.X - a.X) - (b.X - a.X) * (c.Z - a.Z);
                if (up ? ny > 0f : ny < 0f) n++;
            }
            return n;
        }
    }
}
