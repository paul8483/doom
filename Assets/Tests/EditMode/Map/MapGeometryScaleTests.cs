using NUnit.Framework;

namespace Doom.Map.Tests
{
    public class MapGeometryScaleTests
    {
        /// square 64x64, sector floor=0, ceiling=128
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
                new SideDef(0, 0, "-", "-", "W", 0), new SideDef(0, 0, "-", "-", "W", 0),
                new SideDef(0, 0, "-", "-", "W", 0), new SideDef(0, 0, "-", "-", "W", 0),
            };
            var sectors = new[] { new Sector(0, 128, "F", "F", 0, 0, 0) };
            return new MapData("TEST", verts, lines, sides, sectors, System.Array.Empty<Thing>());
        }

        [Test]
        public void Floor_at_worldScale_half_halves_all_vertex_coordinates()
        {
            var polys = SectorPolygonBuilder.Build(Square());
            var floor = SectorTriangulator.TriangulateFloor(Square(), polys[0], worldScale: 0.5f);

            foreach (var v in floor.Vertices)
            {
                Assert.That(v.X, Is.InRange(0f, 32f));
                Assert.That(v.Z, Is.InRange(0f, 32f));
                Assert.That(v.Y, Is.EqualTo(0f));
            }
        }

        [Test]
        public void Ceiling_at_worldScale_one_thirtysecond_matches_doom_scale_meters()
        {
            var polys = SectorPolygonBuilder.Build(Square());
            var ceiling = SectorTriangulator.TriangulateCeiling(Square(), polys[0], worldScale: 1f / 32f);

            foreach (var v in ceiling.Vertices)
            {
                Assert.That(v.Y, Is.EqualTo(4f).Within(0.001f));
                Assert.That(v.X, Is.InRange(0f, 2f));
                Assert.That(v.Z, Is.InRange(0f, 2f));
            }
        }

        // Local fallback size source for the wall test (every texture 64x128).
        private sealed class Sizes : Doom.Graphics.ITextureSizeSource
        {
            public bool TryGetSize(string name, out int width, out int height)
            {
                width = 64; height = 128; return true;
            }
        }

        [Test]
        public void Wall_at_worldScale_half_halves_vertex_coordinates()
        {
            var sections = WallMeshBuilder.BuildForSector(Square(), 0, new Sizes(), worldScale: 0.5f);

            int vertCount = 0;
            foreach (var s in sections)
            {
                vertCount += s.Mesh.Vertices.Length;
                foreach (var v in s.Mesh.Vertices)
                {
                    Assert.That(v.X, Is.InRange(0f, 32f));
                    Assert.That(v.Y, Is.InRange(0f, 64f));
                    Assert.That(v.Z, Is.InRange(0f, 32f));
                }
            }
            Assert.That(vertCount, Is.EqualTo(16));
        }

        [Test]
        public void MapGeometryBuilder_passes_worldScale_through()
        {
            var meshes = MapGeometryBuilder.Build(Square(), worldScale: 0.25f);
            Assert.That(meshes.Length, Is.EqualTo(1));
            foreach (var v in meshes[0].Floor.Vertices)
                Assert.That(v.Y, Is.EqualTo(0f));
            foreach (var v in meshes[0].Ceiling.Vertices)
                Assert.That(v.Y, Is.EqualTo(32f).Within(0.001f));
        }
    }
}
