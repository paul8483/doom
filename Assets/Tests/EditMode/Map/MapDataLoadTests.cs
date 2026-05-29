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
                things: SyntheticMapBuilder.BuildThings((0, 0, 0, 1, 0)),
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

            // Field-value assertions — catch lump-swap bugs
            Assert.That(map.Vertexes[0].X, Is.EqualTo((short)0));
            Assert.That(map.Vertexes[1].X, Is.EqualTo((short)64));
            Assert.That(map.LineDefs[0].V1, Is.EqualTo(0));
            Assert.That(map.LineDefs[0].V2, Is.EqualTo(1));
            Assert.That(map.Sectors[0].FloorHeight, Is.EqualTo((short)0));
            Assert.That(map.Sectors[0].CeilingHeight, Is.EqualTo((short)128));
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

        [TestCase("THINGS")]
        [TestCase("VERTEXES")]
        [TestCase("LINEDEFS")]
        [TestCase("SIDEDEFS")]
        [TestCase("SECTORS")]
        public void Throws_when_required_lump_missing(string missingLump)
        {
            var wadBytes = SyntheticMapBuilder.BuildMapWad(
                "E1M1",
                things:    missingLump == "THINGS"    ? null : SyntheticMapBuilder.BuildThings((0, 0, 0, 1, 0)),
                vertexes:  missingLump == "VERTEXES"  ? null : SyntheticMapBuilder.BuildVertexes((0, 0), (64, 0)),
                linedefs:  missingLump == "LINEDEFS"  ? null : SyntheticMapBuilder.BuildLineDefs((0, 1, 0, 0, 0, 0, 0xFFFF)),
                sidedefs:  missingLump == "SIDEDEFS"  ? null : SyntheticMapBuilder.BuildSideDefs((0, 0, "-", "-", "W", 0)),
                sectors:   missingLump == "SECTORS"   ? null : SyntheticMapBuilder.BuildSectors((0, 128, "F", "F", 0, 0, 0)));

            using var wad = new WadFile(new MemoryStream(wadBytes), ownsStream: true);

            var ex = Assert.Throws<InvalidDataException>(
                () => Doom.Map.MapData.Load(wad, "E1M1"));
            StringAssert.Contains(missingLump, ex.Message);
        }

        [Test]
        public void Loaded_map_exposes_things_from_THINGS_lump()
        {
            var wadBytes = SyntheticMapBuilder.BuildMapWad("E1M1",
                things:   SyntheticMapBuilder.BuildThings((100, 200, 0, 1, 7)),
                vertexes: SyntheticMapBuilder.BuildVertexes((0, 0), (64, 0), (64, 64), (0, 64)),
                linedefs: SyntheticMapBuilder.BuildLineDefs(
                    (0, 1, 0, 0, 0, 0, 0xFFFF),
                    (1, 2, 0, 0, 0, 1, 0xFFFF),
                    (2, 3, 0, 0, 0, 2, 0xFFFF),
                    (3, 0, 0, 0, 0, 3, 0xFFFF)),
                sidedefs: SyntheticMapBuilder.BuildSideDefs(
                    (0, 0, "-", "-", "W", 0), (0, 0, "-", "-", "W", 0),
                    (0, 0, "-", "-", "W", 0), (0, 0, "-", "-", "W", 0)),
                sectors:  SyntheticMapBuilder.BuildSectors((0, 128, "F", "F", 0, 0, 0)));

            using var wad = new WadFile(new MemoryStream(wadBytes), ownsStream: true);
            var map = Doom.Map.MapData.Load(wad, "E1M1");

            Assert.That(map.Things.Length, Is.EqualTo(1));
            Assert.That(map.Things[0].Type, Is.EqualTo(1));
            Assert.That(map.Things[0].X, Is.EqualTo((short)100));
        }
    }
}
