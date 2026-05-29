using NUnit.Framework;

namespace Doom.Map.Tests
{
    public class WallMeshBuilderTests
    {
        [Test]
        public void OneSided_line_produces_one_quad_facing_sector()
        {
            var verts = new[] { new Vertex(0, 0), new Vertex(64, 0) };
            var lines = new[] { new LineDef(0, 1, 0, 0, 0, 0, -1) };
            var sides = new[] { new SideDef(0,0,"-","-","W",0) };
            var sectors = new[] { new Sector(0, 128, "F", "F", 0, 0, 0) };
            var map = new MapData("T", verts, lines, sides, sectors);

            var walls = WallMeshBuilder.BuildForSector(map, 0);

            // квад = 4 вершины, 6 индексов
            Assert.That(walls.Vertices.Length, Is.EqualTo(4));
            Assert.That(walls.Triangles.Length, Is.EqualTo(6));
            // все Y в диапазоне [0, 128]
            foreach (var v in walls.Vertices)
            {
                Assert.That(v.Y, Is.InRange(0f, 128f));
            }
        }

        [Test]
        public void TwoSided_line_with_no_height_diff_produces_no_steps()
        {
            // Front и back сектора имеют одинаковые floor/ceiling — никаких step'ов
            var verts = new[] { new Vertex(0, 0), new Vertex(64, 0) };
            var lines = new[] { new LineDef(0, 1, 0, 0, 0, 0, 1) };
            var sides = new[]
            {
                new SideDef(0,0,"-","-","-",0),
                new SideDef(0,0,"-","-","-",1),
            };
            var sectors = new[]
            {
                new Sector(0, 128, "F", "F", 0, 0, 0),
                new Sector(0, 128, "F", "F", 0, 0, 0),
            };
            var map = new MapData("T", verts, lines, sides, sectors);

            var wallsA = WallMeshBuilder.BuildForSector(map, 0);
            var wallsB = WallMeshBuilder.BuildForSector(map, 1);

            Assert.That(wallsA.Triangles.Length, Is.EqualTo(0));
            Assert.That(wallsB.Triangles.Length, Is.EqualTo(0));
        }

        [Test]
        public void TwoSided_with_floor_step_emits_lower_quad_for_lower_sector()
        {
            // Sector 0: floor=0, ceil=128; sector 1: floor=32, ceil=128
            // Из sector 0 видна ступень (lower) высотой 32. Из sector 1 — ничего (его пол выше).
            var verts = new[] { new Vertex(0, 0), new Vertex(64, 0) };
            var lines = new[] { new LineDef(0, 1, 0, 0, 0, 0, 1) };
            var sides = new[]
            {
                new SideDef(0,0,"-","-","-",0),
                new SideDef(0,0,"-","-","-",1),
            };
            var sectors = new[]
            {
                new Sector(0, 128, "F", "F", 0, 0, 0),
                new Sector(32, 128, "F", "F", 0, 0, 0),
            };
            var map = new MapData("T", verts, lines, sides, sectors);

            var wallsA = WallMeshBuilder.BuildForSector(map, 0);
            var wallsB = WallMeshBuilder.BuildForSector(map, 1);

            // Sector 0 (нижний) видит lower-step: 1 квад
            Assert.That(wallsA.Triangles.Length, Is.EqualTo(6));
            // Sector 1 (верхний) свою сторону пола не видит (его пол выше) — 0 квадов
            Assert.That(wallsB.Triangles.Length, Is.EqualTo(0));
        }
    }
}
