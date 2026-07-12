using System.Linq;
using NUnit.Framework;

namespace Doom.Map.Tests
{
    public class WallScrollSectionTests
    {
        sealed class Sizes : Doom.Graphics.ITextureSizeSource
        {
            public bool TryGetSize(string name, out int width, out int height)
            {
                width = 64;
                height = 128;
                return true;
            }
        }

        [Test]
        public void Scrolling_wall_is_split_from_static_wall_with_same_texture()
        {
            var map = Build(0, 48);
            var walls = WallMeshBuilder.BuildForSector(map, 0, new Sizes());

            Assert.AreEqual(2, walls.Count);
            CollectionAssert.AreEquivalent(new[] { 0, 48 },
                walls.Select(w => w.LineSpecial).ToArray());
        }

        [Test]
        public void Gameplay_specials_do_not_split_texture_bucket()
        {
            var map = Build(1, 4);
            var walls = WallMeshBuilder.BuildForSector(map, 0, new Sizes());

            Assert.AreEqual(1, walls.Count);
            Assert.AreEqual(0, walls[0].LineSpecial);
        }

        static MapData Build(int firstSpecial, int secondSpecial)
        {
            var vertices = new[]
            {
                new Vertex(0, 0), new Vertex(64, 0),
                new Vertex(64, 64), new Vertex(0, 64),
            };
            var lines = new[]
            {
                new LineDef(0, 1, 0, (ushort)firstSpecial, 0, 0, -1),
                new LineDef(2, 3, 0, (ushort)secondSpecial, 0, 1, -1),
            };
            var sides = new[]
            {
                new SideDef(0, 0, "-", "-", "WALL", 0),
                new SideDef(0, 0, "-", "-", "WALL", 0),
            };
            var sectors = new[] { new Sector(0, 128, "F", "C", 255, 0, 0) };
            return new MapData("TEST", vertices, lines, sides, sectors,
                System.Array.Empty<Thing>());
        }
    }
}
