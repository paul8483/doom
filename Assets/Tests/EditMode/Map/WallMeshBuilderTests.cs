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
            var map = new MapData("T", verts, lines, sides, sectors, System.Array.Empty<Thing>());

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
            var map = new MapData("T", verts, lines, sides, sectors, System.Array.Empty<Thing>());

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
            var map = new MapData("T", verts, lines, sides, sectors, System.Array.Empty<Thing>());

            var wallsA = WallMeshBuilder.BuildForSector(map, 0);
            var wallsB = WallMeshBuilder.BuildForSector(map, 1);

            // Sector 0 (нижний) видит lower-step: 1 квад
            Assert.That(wallsA.Triangles.Length, Is.EqualTo(6));
            // Sector 1 (верхний) свою сторону пола не видит (его пол выше) — 0 квадов
            Assert.That(wallsB.Triangles.Length, Is.EqualTo(0));
        }

        [Test]
        public void TwoSided_onBack_path_emits_quad_with_opposite_winding()
        {
            // Sector 0: front, floor=32, ceil=128.
            // Sector 1: back,  floor=0,  ceil=128.
            // The linedef front sidedef -> sector 0, back sidedef -> sector 1.
            // When building for sector 1 (onBack=true, onFront=false):
            //   neighbor (sector 0) has floor=32 > sector1.floor=0 → lower step emitted.
            //   Expected: 1 quad (6 indices) with back-facing winding (a before b at index 0).
            var verts = new[] { new Vertex(0, 0), new Vertex(64, 0) };
            var lines = new[] { new LineDef(0, 1, 0, 0, 0, 0, 1) };
            var sides = new[]
            {
                new SideDef(0, 0, "-", "-", "-", 0),  // front → sector 0
                new SideDef(0, 0, "-", "-", "-", 1),  // back  → sector 1
            };
            var sectors = new[]
            {
                new Sector(32, 128, "F", "F", 0, 0, 0),  // sector 0: higher floor
                new Sector(0,  128, "F", "F", 0, 0, 0),  // sector 1: lower floor
            };
            var map = new MapData("T", verts, lines, sides, sectors, System.Array.Empty<Thing>());

            // sector 0 (on front): neighbor floor (0) < own floor (32) — no lower step, 0 quads
            var wallsFront = WallMeshBuilder.BuildForSector(map, 0);
            Assert.That(wallsFront.Triangles.Length, Is.EqualTo(0),
                "Front sector has higher floor; no lower step visible from it");

            // sector 1 (on back): neighbor floor (32) > own floor (0) — lower step, 1 quad
            var wallsBack = WallMeshBuilder.BuildForSector(map, 1);
            Assert.That(wallsBack.Triangles.Length, Is.EqualTo(6),
                "Back sector sees the lower step from the neighbour's higher floor");

            // Winding for onBack=false (sector 0 front): first vertex is (b.X, yLow, b.Y) = (64,_,0)
            // Winding for onBack=true  (sector 1 back):  first vertex is (a.X, yLow, a.Y) = (0,_,0)
            // They must differ in the X of their first vertex, confirming opposite winding.
            Assert.That(wallsBack.Vertices[0].X, Is.EqualTo(0f),
                "Back-facing quad starts at vertex a (opposite winding from front)");
            Assert.That(wallsBack.Vertices[1].X, Is.EqualTo(64f),
                "Back-facing quad has vertex b second");
        }
    }
}
