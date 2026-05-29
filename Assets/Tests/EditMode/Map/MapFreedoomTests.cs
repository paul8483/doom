using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Wad;
using Doom.Map;

namespace Doom.Map.Tests
{
    public class MapFreedoomTests
    {
        private static string FreedoomPath =>
            Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");

        [Test]
        public void Loads_E1M1_with_expected_lump_counts()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var map = MapData.Load(wad, "E1M1");

            Assert.That(map.Name, Is.EqualTo("E1M1"));
            Assert.That(map.Vertexes.Length, Is.GreaterThan(100),
                "E1M1 содержит сотни вершин");
            Assert.That(map.LineDefs.Length, Is.GreaterThan(100));
            Assert.That(map.SideDefs.Length, Is.GreaterThan(100));
            Assert.That(map.Sectors.Length, Is.GreaterThan(10));
            Assert.That(map.Things.Length, Is.GreaterThan(0));
        }

        [Test]
        public void E1M1_has_player_start_in_things()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var map = MapData.Load(wad, "E1M1");

            Assert.That(map.Things.Length, Is.GreaterThan(0),
                "E1M1 должна содержать THINGS");
            int playerStartCount = 0;
            foreach (var t in map.Things)
            {
                if (t.Type == 1) playerStartCount++;
            }
            Assert.That(playerStartCount, Is.EqualTo(1),
                "E1M1 содержит ровно один Player 1 start");
        }

        [Test]
        public void Throws_for_nonexistent_map()
        {
            using var wad = WadFile.Open(FreedoomPath);
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
                () => MapData.Load(wad, "E9M9"));
        }

        [Test]
        public void SectorPolygonBuilder_closes_most_E1M1_sectors()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var map = MapData.Load(wad, "E1M1");

            var polys = SectorPolygonBuilder.Build(map);

            int valid = 0;
            foreach (var p in polys) if (p.IsValid) valid++;
            double ratio = (double)valid / polys.Length;
            // Если меньше 90% секторов замкнулись — что-то очень не так с алгоритмом
            Assert.That(ratio, Is.GreaterThan(0.9),
                $"Замкнуто {valid}/{polys.Length} секторов ({ratio:P0})");
        }

        [Test]
        public void Builds_geometry_for_E1M1_without_throwing()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var map = MapData.Load(wad, "E1M1");

            SectorMeshes[] meshes = null;
            Assert.DoesNotThrow(() => meshes = MapGeometryBuilder.Build(map));

            int totalTris = 0;
            foreach (var sm in meshes)
            {
                totalTris += sm.Floor.Triangles.Length / 3;
                totalTris += sm.Ceiling.Triangles.Length / 3;
                totalTris += sm.Walls.Triangles.Length / 3;
            }
            Assert.That(totalTris, Is.GreaterThan(1000),
                "E1M1 должна давать тысячи треугольников");
        }

        [Test]
        public void E1M1_valid_sector_polygons_have_simple_rings()
        {
            // Регрессия: жадная реконструкция контуров в SectorPolygonBuilder на
            // вершинах степени >2 (T-junction) сворачивает не туда — outer-кольцо
            // самопересекается (проходит через одну точку дважды), а в Holes
            // появляются вырожденные 2-вершинные «дырки». LibTess по EvenOdd
            // гасит перекрытия → в полу/потолке исчезают куски (синие прорези).
            // Корректный обход обязан давать ПРОСТЫЕ кольца: ≥3 вершин и без
            // повторного прохода через одну точку.
            using var wad = WadFile.Open(FreedoomPath);
            var map = MapData.Load(wad, "E1M1");
            var polys = SectorPolygonBuilder.Build(map);

            var bad = new List<string>();
            foreach (var p in polys)
            {
                if (!p.IsValid) continue;
                CheckSimpleRing(map, p.Outer, p.SectorIdx, "outer", bad);
                for (int h = 0; h < p.Holes.Count; h++)
                    CheckSimpleRing(map, p.Holes[h], p.SectorIdx, $"hole{h}", bad);
            }

            Assert.That(bad.Count, Is.EqualTo(0),
                $"Не-простых контуров: {bad.Count}\n"
                + string.Join("\n", bad.GetRange(0, System.Math.Min(bad.Count, 30))));
        }

        private static void CheckSimpleRing(MapData map, IReadOnlyList<int> ring,
                                            int sector, string which, List<string> bad)
        {
            if (ring.Count < 3)
            {
                bad.Add($"sector {sector} {which}: {ring.Count} вершин (вырожденный контур)");
                return;
            }
            var seen = new HashSet<(short x, short y)>();
            foreach (var vi in ring)
            {
                var v = map.Vertexes[vi];
                if (!seen.Add((v.X, v.Y)))
                {
                    bad.Add($"sector {sector} {which}: точка ({v.X},{v.Y}) встречается дважды (самопересечение)");
                    return;
                }
            }
        }

        [Test]
        public void Loads_other_E1Mx_maps_without_throwing()
        {
            using var wad = WadFile.Open(FreedoomPath);
            foreach (var name in new[] { "E1M2", "E1M3", "E2M1", "E3M1" })
            {
                Assert.DoesNotThrow(() => {
                    var map = MapData.Load(wad, name);
                    var meshes = MapGeometryBuilder.Build(map);
                    Assert.That(meshes.Length, Is.GreaterThan(0));
                }, $"Failed for {name}");
            }
        }
    }
}
