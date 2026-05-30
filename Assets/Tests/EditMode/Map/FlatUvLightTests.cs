using NUnit.Framework;

namespace Doom.Map.Tests
{
    public class FlatUvLightTests
    {
        // 64x64 square, floor=0 ceil=128, light=128, flats "FL"/"CL".
        private static MapData Square(string ceilFlat = "CL", ushort light = 128)
        {
            var verts = new[]
            {
                new Vertex(0, 0), new Vertex(64, 0),
                new Vertex(64, 64), new Vertex(0, 64),
            };
            var lines = new[]
            {
                new LineDef(0, 1, 0, 0, 0, 0, -1),
                new LineDef(1, 2, 0, 0, 0, 1, -1),
                new LineDef(2, 3, 0, 0, 0, 2, -1),
                new LineDef(3, 0, 0, 0, 0, 3, -1),
            };
            var sides = new[]
            {
                new SideDef(0,0,"-","-","W",0), new SideDef(0,0,"-","-","W",0),
                new SideDef(0,0,"-","-","W",0), new SideDef(0,0,"-","-","W",0),
            };
            var sectors = new[] { new Sector(0, 128, "FL", ceilFlat, light, 0, 0) };
            return new MapData("T", verts, lines, sides, sectors, System.Array.Empty<Thing>());
        }

        [Test]
        public void Floor_uv_is_doom_coords_over_64()
        {
            var map = Square();
            var polys = SectorPolygonBuilder.Build(map);
            var floor = SectorTriangulator.TriangulateFloor(map, polys[0]); // worldScale 1

            Assert.That(floor.Uv.Length, Is.EqualTo(floor.Vertices.Length));
            // Every UV component must be within [0,1] for this 64x64 sector.
            foreach (var uv in floor.Uv)
            {
                Assert.That(uv.X, Is.InRange(0f, 1f));
                Assert.That(uv.Y, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void Floor_vertex_color_matches_sector_light()
        {
            var map = Square(light: 128);
            var polys = SectorPolygonBuilder.Build(map);
            var floor = SectorTriangulator.TriangulateFloor(map, polys[0]);

            Assert.That(floor.Colors.Length, Is.EqualTo(floor.Vertices.Length));
            float expected = 128f / 255f;
            foreach (var c in floor.Colors)
            {
                Assert.That(c.X, Is.EqualTo(expected).Within(0.001f));
                Assert.That(c.Y, Is.EqualTo(expected).Within(0.001f));
                Assert.That(c.Z, Is.EqualTo(expected).Within(0.001f));
            }
        }

        [Test]
        public void Sky_ceiling_emits_no_geometry()
        {
            var map = Square(ceilFlat: "F_SKY1");
            var polys = SectorPolygonBuilder.Build(map);
            var ceiling = SectorTriangulator.TriangulateCeiling(map, polys[0]);
            Assert.That(ceiling.IsEmpty, Is.True);
        }

        [Test]
        public void Nonsky_ceiling_still_emits_geometry()
        {
            var map = Square(ceilFlat: "CL");
            var polys = SectorPolygonBuilder.Build(map);
            var ceiling = SectorTriangulator.TriangulateCeiling(map, polys[0]);
            Assert.That(ceiling.IsEmpty, Is.False);
        }
    }
}
