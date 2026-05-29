using System.IO;
using NUnit.Framework;
using Doom.Wad;

namespace Doom.Map.Tests
{
    public class MapDataLoadTests
    {
        [Test]
        public void Loads_map_with_all_four_lumps()
        {
            var wadBytes = SyntheticMapBuilder.BuildMapWad(
                "E1M1",
                vertexes: SyntheticMapBuilder.BuildVertexes((0, 0), (64, 0), (64, 64), (0, 64)),
                linedefs: SyntheticMapBuilder.BuildLineDefs(
                    (0, 1, 0, 0, 0, 0, 0xFFFF),
                    (1, 2, 0, 0, 0, 1, 0xFFFF),
                    (2, 3, 0, 0, 0, 2, 0xFFFF),
                    (3, 0, 0, 0, 0, 3, 0xFFFF)),
                sidedefs: SyntheticMapBuilder.BuildSideDefs(
                    (0, 0, "-", "-", "WALL01", 0),
                    (0, 0, "-", "-", "WALL01", 0),
                    (0, 0, "-", "-", "WALL01", 0),
                    (0, 0, "-", "-", "WALL01", 0)),
                sectors: SyntheticMapBuilder.BuildSectors(
                    (0, 128, "FLAT01", "F_SKY1", 192, 0, 0)));

            using var wad = new WadFile(new MemoryStream(wadBytes), ownsStream: true);
            var map = Doom.Map.MapData.Load(wad, "E1M1");

            Assert.That(map.Vertexes.Length, Is.EqualTo(4));
            Assert.That(map.LineDefs.Length, Is.EqualTo(4));
            Assert.That(map.SideDefs.Length, Is.EqualTo(4));
            Assert.That(map.Sectors.Length, Is.EqualTo(1));
            Assert.That(map.Name, Is.EqualTo("E1M1"));
        }

        [Test]
        public void Throws_when_map_marker_missing()
        {
            var wadBytes = Doom.Wad.Tests.SyntheticWadBuilder.Build("IWAD", new[]
            {
                new Doom.Wad.Tests.SyntheticWadBuilder.Lump("PLAYPAL", new byte[10]),
            });

            using var wad = new WadFile(new MemoryStream(wadBytes), ownsStream: true);

            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
                () => Doom.Map.MapData.Load(wad, "E1M1"));
        }

        [Test]
        public void Throws_when_marker_is_not_a_map_name()
        {
            var wadBytes = Doom.Wad.Tests.SyntheticWadBuilder.Build("IWAD", new[]
            {
                new Doom.Wad.Tests.SyntheticWadBuilder.Lump("PLAYPAL", new byte[10]),
            });

            using var wad = new WadFile(new MemoryStream(wadBytes), ownsStream: true);

            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
                () => Doom.Map.MapData.Load(wad, "PLAYPAL"));
        }

        [Test]
        public void Throws_when_required_lump_missing()
        {
            // маркер есть, VERTEXES — нет
            var wadBytes = SyntheticMapBuilder.BuildMapWad(
                "E1M1",
                vertexes: null,
                linedefs: SyntheticMapBuilder.BuildLineDefs((0, 1, 0, 0, 0, 0, 0xFFFF)),
                sidedefs: SyntheticMapBuilder.BuildSideDefs(
                    (0, 0, "-", "-", "W", 0)),
                sectors: SyntheticMapBuilder.BuildSectors(
                    (0, 128, "F", "F", 0, 0, 0)));

            using var wad = new WadFile(new MemoryStream(wadBytes), ownsStream: true);

            var ex = Assert.Throws<InvalidDataException>(
                () => Doom.Map.MapData.Load(wad, "E1M1"));
            StringAssert.Contains("VERTEXES", ex.Message);
        }
    }
}
