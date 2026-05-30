using System.Collections.Generic;
using NUnit.Framework;
using Doom.Graphics;

namespace Doom.Map.Tests
{
    // Minimal ITextureSizeSource that returns 64x128 for every texture name.
    file sealed class FlatSizes : ITextureSizeSource
    {
        public bool TryGetSize(string name, out int width, out int height)
        { width = 64; height = 128; return true; }
    }

    public class WallMeshBuilderTests
    {
        private static readonly ITextureSizeSource Sizes = new FlatSizes();

        // Helper: total vertex count across all sections.
        private static int TotalVerts(IReadOnlyList<WallSection> sections)
        {
            int n = 0;
            foreach (var s in sections) n += s.Mesh.Vertices.Length;
            return n;
        }

        // Helper: total triangle index count across all sections.
        private static int TotalTris(IReadOnlyList<WallSection> sections)
        {
            int n = 0;
            foreach (var s in sections) n += s.Mesh.Triangles.Length;
            return n;
        }

        [Test]
        public void OneSided_line_produces_one_quad_facing_sector()
        {
            var verts = new[] { new Vertex(0, 0), new Vertex(64, 0) };
            var lines = new[] { new LineDef(0, 1, 0, 0, 0, 0, -1) };
            var sides = new[] { new SideDef(0, 0, "-", "-", "W", 0) };
            var sectors = new[] { new Sector(0, 128, "F", "F", 0, 0, 0) };
            var map = new MapData("T", verts, lines, sides, sectors, System.Array.Empty<Thing>());

            var walls = WallMeshBuilder.BuildForSector(map, 0, Sizes);

            // квад = 4 вершины, 6 индексов
            Assert.That(TotalVerts(walls), Is.EqualTo(4));
            Assert.That(TotalTris(walls), Is.EqualTo(6));
            // все Y в диапазоне [0, 128]
            foreach (var sec in walls)
                foreach (var v in sec.Mesh.Vertices)
                    Assert.That(v.Y, Is.InRange(0f, 128f));
        }

        [Test]
        public void TwoSided_line_with_no_height_diff_produces_no_steps()
        {
            // Front и back сектора имеют одинаковые floor/ceiling — никаких step'ов
            var verts = new[] { new Vertex(0, 0), new Vertex(64, 0) };
            var lines = new[] { new LineDef(0, 1, 0, 0, 0, 0, 1) };
            var sides = new[]
            {
                new SideDef(0, 0, "-", "-", "-", 0),
                new SideDef(0, 0, "-", "-", "-", 1),
            };
            var sectors = new[]
            {
                new Sector(0, 128, "F", "F", 0, 0, 0),
                new Sector(0, 128, "F", "F", 0, 0, 0),
            };
            var map = new MapData("T", verts, lines, sides, sectors, System.Array.Empty<Thing>());

            var wallsA = WallMeshBuilder.BuildForSector(map, 0, Sizes);
            var wallsB = WallMeshBuilder.BuildForSector(map, 1, Sizes);

            Assert.That(TotalTris(wallsA), Is.EqualTo(0));
            Assert.That(TotalTris(wallsB), Is.EqualTo(0));
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
                new SideDef(0, 0, "-", "-", "-", 0),
                new SideDef(0, 0, "-", "-", "-", 1),
            };
            var sectors = new[]
            {
                new Sector(0, 128, "F", "F", 0, 0, 0),
                new Sector(32, 128, "F", "F", 0, 0, 0),
            };
            var map = new MapData("T", verts, lines, sides, sectors, System.Array.Empty<Thing>());

            var wallsA = WallMeshBuilder.BuildForSector(map, 0, Sizes);
            var wallsB = WallMeshBuilder.BuildForSector(map, 1, Sizes);

            // Sector 0 (нижний): textureless lower step → no sections (HasTex("-") = false)
            Assert.That(TotalTris(wallsA), Is.EqualTo(0));
            // Sector 1 (верхний): no visible step either
            Assert.That(TotalTris(wallsB), Is.EqualTo(0));
        }

        [Test]
        public void TwoSided_with_floor_step_and_texture_emits_lower_quad()
        {
            // Same geometry as above but with a real lower texture on sector 0's sidedef.
            var verts = new[] { new Vertex(0, 0), new Vertex(64, 0) };
            var lines = new[] { new LineDef(0, 1, 0, 0, 0, 0, 1) };
            var sides = new[]
            {
                new SideDef(0, 0, "-", "STEP", "-", 0),  // lower texture "STEP" on front side
                new SideDef(0, 0, "-", "-",    "-", 1),
            };
            var sectors = new[]
            {
                new Sector(0, 128, "F", "F", 0, 0, 0),
                new Sector(32, 128, "F", "F", 0, 0, 0),
            };
            var map = new MapData("T", verts, lines, sides, sectors, System.Array.Empty<Thing>());

            var wallsA = WallMeshBuilder.BuildForSector(map, 0, Sizes);
            var wallsB = WallMeshBuilder.BuildForSector(map, 1, Sizes);

            // Sector 0 sees a lower step with texture → 1 quad (6 indices).
            Assert.That(TotalTris(wallsA), Is.EqualTo(6));
            // Sector 1 has higher floor — its side has no lower texture either.
            Assert.That(TotalTris(wallsB), Is.EqualTo(0));
        }

        [Test]
        public void TwoSided_onBack_path_emits_quad_with_opposite_winding()
        {
            // Sector 0: front, floor=32, ceil=128.
            // Sector 1: back,  floor=0,  ceil=128.
            var verts = new[] { new Vertex(0, 0), new Vertex(64, 0) };
            var lines = new[] { new LineDef(0, 1, 0, 0, 0, 0, 1) };
            var sides = new[]
            {
                new SideDef(0, 0, "-", "-",     "-", 0),  // front → sector 0 (no lower texture)
                new SideDef(0, 0, "-", "LOWER", "-", 1),  // back  → sector 1 (lower texture)
            };
            var sectors = new[]
            {
                new Sector(32, 128, "F", "F", 0, 0, 0),  // sector 0: higher floor
                new Sector(0,  128, "F", "F", 0, 0, 0),  // sector 1: lower floor
            };
            var map = new MapData("T", verts, lines, sides, sectors, System.Array.Empty<Thing>());

            // sector 0 (on front): no lower step visible from it (its floor is higher), 0 tris
            var wallsFront = WallMeshBuilder.BuildForSector(map, 0, Sizes);
            Assert.That(TotalTris(wallsFront), Is.EqualTo(0),
                "Front sector has higher floor; no lower step visible from it");

            // sector 1 (on back): sees lower step from neighbour's higher floor; lower texture present
            var wallsBack = WallMeshBuilder.BuildForSector(map, 1, Sizes);
            Assert.That(TotalTris(wallsBack), Is.EqualTo(6),
                "Back sector sees the lower step from the neighbour's higher floor");

            // Winding for onBack=true: first vertex should be (a.X, yLow, a.Y) = (0, _, 0)
            var firstSection = wallsBack[0];
            Assert.That(firstSection.Mesh.Vertices[0].X, Is.EqualTo(0f),
                "Back-facing quad starts at vertex a (opposite winding from front)");
            Assert.That(firstSection.Mesh.Vertices[1].X, Is.EqualTo(64f),
                "Back-facing quad has vertex b second");
        }
    }
}
