using System;
using NUnit.Framework;

namespace Doom.Map.Tests
{
    public class MapDataReaderTests
    {
        [Test]
        public void Parses_vertexes_into_short_x_y()
        {
            var bytes = SyntheticMapBuilder.BuildVertexes(
                (10, 20), (-30, 40), (0, 0));

            var verts = MapData.ParseVertexes(bytes);

            Assert.That(verts.Length, Is.EqualTo(3));
            Assert.That(verts[0].X, Is.EqualTo((short)10));
            Assert.That(verts[0].Y, Is.EqualTo((short)20));
            Assert.That(verts[1].X, Is.EqualTo((short)-30));
            Assert.That(verts[1].Y, Is.EqualTo((short)40));
            Assert.That(verts[2].X, Is.EqualTo((short)0));
        }

        [Test]
        public void Vertexes_lump_size_not_multiple_of_4_is_warning_not_throw()
        {
            // 4 байта = одна валидная запись, +1 «хвостовой» байт — игнорируется
            var bytes = new byte[] { 1, 0, 2, 0, 0xFF };

            string captured = null;
            Action<string> handler = m => captured = m;
            MapLog.WarningHandler += handler;
            try
            {
                var verts = MapData.ParseVertexes(bytes);
                Assert.That(captured, Does.Contain("VERTEXES"));
                Assert.That(verts.Length, Is.EqualTo(1));
                Assert.That(verts[0].X, Is.EqualTo((short)1));
                Assert.That(verts[0].Y, Is.EqualTo((short)2));
            }
            finally { MapLog.WarningHandler -= handler; }
        }

        [Test]
        public void LineDefs_lump_size_not_multiple_of_14_is_warning_not_throw()
        {
            // 14 байт = одна валидная запись, +1 «хвостовой» байт — игнорируется
            var bytes = new byte[15];
            // v1=0, v2=1, flags=1, special=0, tag=0, front=0, back=0xFFFF
            bytes[0] = 0; bytes[1] = 0;  // v1
            bytes[2] = 1; bytes[3] = 0;  // v2
            bytes[4] = 1; bytes[5] = 0;  // flags
            bytes[6] = 0; bytes[7] = 0;  // special
            bytes[8] = 0; bytes[9] = 0;  // tag
            bytes[10] = 0; bytes[11] = 0; // front
            bytes[12] = 0xFF; bytes[13] = 0xFF; // back
            bytes[14] = 0xAB; // trailing byte

            string captured = null;
            Action<string> handler = m => captured = m;
            MapLog.WarningHandler += handler;
            try
            {
                var lines = MapData.ParseLineDefs(bytes);
                Assert.That(captured, Does.Contain("LINEDEFS"));
                Assert.That(lines.Length, Is.EqualTo(1));
                Assert.That(lines[0].BackSideIdx, Is.EqualTo(-1));
            }
            finally { MapLog.WarningHandler -= handler; }
        }

        [Test]
        public void Parses_linedefs_into_records()
        {
            var bytes = SyntheticMapBuilder.BuildLineDefs(
                (v1: 0, v2: 1, flags: 0x0001, special: 0, tag: 0, front: 0, back: 0xFFFF),
                (v1: 1, v2: 2, flags: 0x0004, special: 0, tag: 0, front: 1, back: 2));

            var lines = MapData.ParseLineDefs(bytes);

            Assert.That(lines.Length, Is.EqualTo(2));
            Assert.That(lines[0].V1, Is.EqualTo(0));
            Assert.That(lines[0].V2, Is.EqualTo(1));
            Assert.That(lines[0].Flags, Is.EqualTo(0x0001));
            Assert.That(lines[0].FrontSideIdx, Is.EqualTo(0));
            Assert.That(lines[0].BackSideIdx, Is.EqualTo(-1));
            Assert.That(lines[1].BackSideIdx, Is.EqualTo(2));
        }

        [Test]
        public void Parses_sidedefs_with_texture_names_and_sector_idx()
        {
            var bytes = SyntheticMapBuilder.BuildSideDefs(
                (tx: 10, ty: 20, upper: "BRICK1", lower: "-", middle: "WALL01",
                 sector: 5));

            var sides = MapData.ParseSideDefs(bytes);

            Assert.That(sides.Length, Is.EqualTo(1));
            Assert.That(sides[0].TextureXOffset, Is.EqualTo((short)10));
            Assert.That(sides[0].TextureYOffset, Is.EqualTo((short)20));
            Assert.That(sides[0].UpperTexture, Is.EqualTo("BRICK1"));
            Assert.That(sides[0].LowerTexture, Is.EqualTo("-"));
            Assert.That(sides[0].MiddleTexture, Is.EqualTo("WALL01"));
            Assert.That(sides[0].SectorIdx, Is.EqualTo(5));
        }

        [Test]
        public void Parses_sectors_with_heights()
        {
            var bytes = SyntheticMapBuilder.BuildSectors(
                (floorH: 0, ceilH: 128, floorFlat: "FLAT1", ceilFlat: "F_SKY1",
                 light: 192, special: 0, tag: 0),
                (floorH: -16, ceilH: 64, floorFlat: "BLOOD1", ceilFlat: "CEIL1",
                 light: 96, special: 9, tag: 12));

            var sectors = MapData.ParseSectors(bytes);

            Assert.That(sectors.Length, Is.EqualTo(2));
            Assert.That(sectors[0].FloorHeight, Is.EqualTo((short)0));
            Assert.That(sectors[0].CeilingHeight, Is.EqualTo((short)128));
            Assert.That(sectors[0].FloorFlat, Is.EqualTo("FLAT1"));
            Assert.That(sectors[0].CeilingFlat, Is.EqualTo("F_SKY1"));
            Assert.That(sectors[0].LightLevel, Is.EqualTo(192));
            Assert.That(sectors[1].FloorHeight, Is.EqualTo((short)-16));
            Assert.That(sectors[1].CeilingHeight, Is.EqualTo((short)64));
            Assert.That(sectors[1].FloorFlat, Is.EqualTo("BLOOD1"));
            Assert.That(sectors[1].CeilingFlat, Is.EqualTo("CEIL1"));
            Assert.That(sectors[1].LightLevel, Is.EqualTo(96));
            Assert.That(sectors[1].Special, Is.EqualTo(9));
            Assert.That(sectors[1].Tag, Is.EqualTo(12));
        }
    }
}
