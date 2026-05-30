using NUnit.Framework;

namespace Doom.Map.Tests
{
    public class RuntimeHeightRebuildTests
    {
        private sealed class Heights : ISectorHeights
        {
            private readonly int[] f, c;
            public Heights(int[] floors, int[] ceils) { f = floors; c = ceils; }
            public int FloorHeight(int s) => f[s];
            public int CeilingHeight(int s) => c[s];
        }

        [Test]
        public void Rebuild_sector_at_overridden_floor_height_moves_floor_mesh()
        {
            // Single square sector, floor 0 ceil 128 in the WAD.
            MapData map = SyntheticMapBuilder.SingleSquareSector(floor: 0, ceil: 128);
            float ws = 1f / 32f;

            var sm0 = MapGeometryBuilder.RebuildSector(map, 0, new StaticSectorHeights(map), ws, null);
            float baseY = sm0.Floor.Vertices[0].Y;

            var raised = new Heights(new[] { 64 }, new[] { 128 });
            var sm1 = MapGeometryBuilder.RebuildSector(map, 0, raised, ws, null);
            float raisedY = sm1.Floor.Vertices[0].Y;

            Assert.That(raisedY, Is.GreaterThan(baseY));
            Assert.That(raisedY, Is.EqualTo(64 * ws).Within(1e-4f));
        }
    }
}
