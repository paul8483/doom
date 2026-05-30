using System.Collections.Generic;
using System.IO;
using System.Text;
using Doom.Wad.Tests;

namespace Doom.Map.Tests
{
    /// Билдер байтовых блобов для каждого типа лампа карты,
    /// плюс упаковка их в синтетический WAD с маркером карты.
    public static class SyntheticMapBuilder
    {
        public static byte[] BuildVertexes(params (short x, short y)[] verts)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            foreach (var v in verts) { w.Write(v.x); w.Write(v.y); }
            return ms.ToArray();
        }

        public static byte[] BuildLineDefs(params (ushort v1, ushort v2, ushort flags,
                                                   ushort special, ushort tag,
                                                   ushort front, ushort back)[] lines)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            foreach (var l in lines)
            {
                w.Write(l.v1); w.Write(l.v2); w.Write(l.flags);
                w.Write(l.special); w.Write(l.tag);
                w.Write(l.front); w.Write(l.back);
            }
            return ms.ToArray();
        }

        public static byte[] BuildSideDefs(params (short tx, short ty,
                                                   string upper, string lower, string middle,
                                                   ushort sector)[] sides)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            foreach (var s in sides)
            {
                w.Write(s.tx); w.Write(s.ty);
                w.Write(EncodeName8(s.upper));
                w.Write(EncodeName8(s.lower));
                w.Write(EncodeName8(s.middle));
                w.Write(s.sector);
            }
            return ms.ToArray();
        }

        public static byte[] BuildSectors(params (short floorH, short ceilH,
                                                  string floorFlat, string ceilFlat,
                                                  ushort light, ushort special, ushort tag)[] sectors)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            foreach (var s in sectors)
            {
                w.Write(s.floorH); w.Write(s.ceilH);
                w.Write(EncodeName8(s.floorFlat));
                w.Write(EncodeName8(s.ceilFlat));
                w.Write(s.light); w.Write(s.special); w.Write(s.tag);
            }
            return ms.ToArray();
        }

        public static byte[] BuildThings(params (short x, short y,
                                                  ushort angle, ushort type, ushort flags)[] things)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            foreach (var t in things)
            {
                w.Write(t.x); w.Write(t.y);
                w.Write(t.angle); w.Write(t.type); w.Write(t.flags);
            }
            return ms.ToArray();
        }

        /// Собрать WAD с маркером карты + любыми переданными лампами карты.
        /// Маркер должен идти первым; остальные лампы — после.
        public static byte[] BuildMapWad(string mapName,
            byte[] vertexes = null, byte[] linedefs = null,
            byte[] sidedefs = null, byte[] sectors = null,
            byte[] things = null)
        {
            var lumps = new List<SyntheticWadBuilder.Lump>
            {
                new SyntheticWadBuilder.Lump(mapName, new byte[0]),
            };
            if (things   != null) lumps.Add(new SyntheticWadBuilder.Lump("THINGS",   things));
            if (linedefs != null) lumps.Add(new SyntheticWadBuilder.Lump("LINEDEFS", linedefs));
            if (sidedefs != null) lumps.Add(new SyntheticWadBuilder.Lump("SIDEDEFS", sidedefs));
            if (vertexes != null) lumps.Add(new SyntheticWadBuilder.Lump("VERTEXES", vertexes));
            if (sectors != null) lumps.Add(new SyntheticWadBuilder.Lump("SECTORS", sectors));
            return SyntheticWadBuilder.Build("IWAD", (IReadOnlyList<SyntheticWadBuilder.Lump>)lumps);
        }

        /// One 64x64 square sector with 4 one-sided linedefs, built directly as a
        /// MapData (no WAD round-trip). Floor/ceiling heights are parameterised so
        /// runtime-height rebuild tests can compare against overrides.
        public static MapData SingleSquareSector(int floor = 0, int ceil = 128)
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
            var sectors = new[]
            {
                new Sector((short)floor, (short)ceil, "F", "F", 0, 0, 0),
            };
            return new MapData("TEST", verts, lines, sides, sectors,
                               System.Array.Empty<Thing>());
        }

        private static byte[] EncodeName8(string name)
        {
            var buf = new byte[8];
            if (string.IsNullOrEmpty(name)) return buf;
            var ascii = Encoding.ASCII.GetBytes(name);
            System.Array.Copy(ascii, buf, System.Math.Min(ascii.Length, 8));
            return buf;
        }
    }
}
