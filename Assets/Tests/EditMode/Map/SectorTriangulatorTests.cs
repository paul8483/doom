using NUnit.Framework;

namespace Doom.Map.Tests
{
    public class SectorTriangulatorTests
    {
        private static MapData Square()
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
            var sectors = new[] { new Sector(0, 128, "F", "F", 0, 0, 0) };
            return new MapData("TEST", verts, lines, sides, sectors);
        }

        [Test]
        public void Square_floor_has_2_triangles_normal_up()
        {
            var map = Square();
            var polys = SectorPolygonBuilder.Build(map);

            var floor = SectorTriangulator.TriangulateFloor(map, polys[0]);

            Assert.That(floor.Triangles.Length, Is.EqualTo(6));
            // All Y at floor = floorHeight = 0
            foreach (var v in floor.Vertices)
                Assert.That(v.Y, Is.EqualTo(0f));
            // CCW winding from (+Y): cross product of first triangle must have +Y component
            var n = TriNormal(floor.Vertices, floor.Triangles, 0);
            Assert.That(n.Y, Is.GreaterThan(0));
        }

        [Test]
        public void Square_ceiling_normal_down_indices_reversed()
        {
            var map = Square();
            var polys = SectorPolygonBuilder.Build(map);

            var ceiling = SectorTriangulator.TriangulateCeiling(map, polys[0]);

            Assert.That(ceiling.Triangles.Length, Is.EqualTo(6));
            foreach (var v in ceiling.Vertices)
                Assert.That(v.Y, Is.EqualTo(128f));
            var n = TriNormal(ceiling.Vertices, ceiling.Triangles, 0);
            Assert.That(n.Y, Is.LessThan(0));
        }

        [Test]
        public void Invalid_polygon_returns_empty_mesh()
        {
            var floor = SectorTriangulator.TriangulateFloor(
                new MapData("T", System.Array.Empty<Vertex>(),
                            System.Array.Empty<LineDef>(),
                            System.Array.Empty<SideDef>(),
                            new[] { new Sector(0, 128, "F", "F", 0, 0, 0) }),
                SectorPolygon.Invalid(0));
            Assert.That(floor.IsEmpty, Is.True);
        }

        private static Float3 TriNormal(Float3[] v, int[] t, int triIdx)
        {
            var a = v[t[triIdx*3 + 0]];
            var b = v[t[triIdx*3 + 1]];
            var c = v[t[triIdx*3 + 2]];
            float ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z;
            float wx = c.X - a.X, wy = c.Y - a.Y, wz = c.Z - a.Z;
            return new Float3(uy*wz - uz*wy, uz*wx - ux*wz, ux*wy - uy*wx);
        }
    }
}
