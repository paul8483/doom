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

            var verts = MapData.ParseVertexes(bytes);

            Assert.That(verts.Length, Is.EqualTo(1));
            Assert.That(verts[0].X, Is.EqualTo((short)1));
            Assert.That(verts[0].Y, Is.EqualTo((short)2));
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
    }
}
