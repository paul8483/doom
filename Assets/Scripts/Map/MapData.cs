using System;
using System.IO;
using System.Text;

namespace Doom.Map
{
    public static class MapLog
    {
        public static event Action<string> WarningHandler;
        public static event Action<string> ErrorHandler;

        public static void Warning(string msg) => WarningHandler?.Invoke(msg);
        public static void Error(string msg) => ErrorHandler?.Invoke(msg);
    }

    public readonly struct Vertex
    {
        public readonly short X;
        public readonly short Y;
        public Vertex(short x, short y) { X = x; Y = y; }
    }

    public readonly struct LineDef
    {
        public readonly int V1;
        public readonly int V2;
        public readonly ushort Flags;
        public readonly ushort Special;
        public readonly ushort Tag;
        public readonly int FrontSideIdx;
        public readonly int BackSideIdx;

        public LineDef(int v1, int v2, ushort flags, ushort special, ushort tag,
                       int front, int back)
        {
            V1 = v1; V2 = v2; Flags = flags; Special = special; Tag = tag;
            FrontSideIdx = front; BackSideIdx = back;
        }

        public bool IsTwoSided => BackSideIdx >= 0;
    }

    public readonly struct SideDef
    {
        public readonly short TextureXOffset;
        public readonly short TextureYOffset;
        public readonly string UpperTexture;
        public readonly string LowerTexture;
        public readonly string MiddleTexture;
        public readonly int SectorIdx;

        public SideDef(short tx, short ty, string upper, string lower, string middle,
                       int sectorIdx)
        {
            TextureXOffset = tx; TextureYOffset = ty;
            UpperTexture = upper; LowerTexture = lower; MiddleTexture = middle;
            SectorIdx = sectorIdx;
        }
    }

    public readonly struct Sector
    {
        public readonly short FloorHeight;
        public readonly short CeilingHeight;
        public readonly string FloorFlat;
        public readonly string CeilingFlat;
        public readonly ushort LightLevel;
        public readonly ushort Special;
        public readonly ushort Tag;

        public Sector(short floorH, short ceilH, string floorFlat, string ceilFlat,
                      ushort light, ushort special, ushort tag)
        {
            FloorHeight = floorH; CeilingHeight = ceilH;
            FloorFlat = floorFlat; CeilingFlat = ceilFlat;
            LightLevel = light; Special = special; Tag = tag;
        }
    }

    public static class MapData
    {
        private const int VertexSize  = 4;
        private const int LineDefSize = 14;
        private const int SideDefSize = 30;
        private const int SectorSize  = 26;

        public static Vertex[] ParseVertexes(byte[] bytes)
        {
            if (bytes == null) return Array.Empty<Vertex>();
            int count = bytes.Length / VertexSize;
            if (bytes.Length % VertexSize != 0)
            {
                MapLog.Warning(
                    $"VERTEXES: размер {bytes.Length} не кратен {VertexSize}, " +
                    $"читаем первые {count} записей");
            }
            var verts = new Vertex[count];
            using var ms = new MemoryStream(bytes);
            using var r = new BinaryReader(ms);
            for (int i = 0; i < count; i++)
            {
                short x = r.ReadInt16();
                short y = r.ReadInt16();
                verts[i] = new Vertex(x, y);
            }
            return verts;
        }

        public static LineDef[] ParseLineDefs(byte[] bytes)
        {
            if (bytes == null) return Array.Empty<LineDef>();
            int count = bytes.Length / LineDefSize;
            if (bytes.Length % LineDefSize != 0)
            {
                MapLog.Warning(
                    $"LINEDEFS: размер {bytes.Length} не кратен {LineDefSize}, " +
                    $"читаем первые {count} записей");
            }
            var lines = new LineDef[count];
            using var ms = new MemoryStream(bytes);
            using var r = new BinaryReader(ms);
            for (int i = 0; i < count; i++)
            {
                ushort v1 = r.ReadUInt16();
                ushort v2 = r.ReadUInt16();
                ushort flags = r.ReadUInt16();
                ushort special = r.ReadUInt16();
                ushort tag = r.ReadUInt16();
                ushort front = r.ReadUInt16();
                ushort back = r.ReadUInt16();
                lines[i] = new LineDef(
                    v1, v2, flags, special, tag,
                    front: front,
                    back: back == 0xFFFF ? -1 : back);
            }
            return lines;
        }

        public static SideDef[] ParseSideDefs(byte[] bytes)
        {
            if (bytes == null) return Array.Empty<SideDef>();
            int count = bytes.Length / SideDefSize;
            if (bytes.Length % SideDefSize != 0)
            {
                MapLog.Warning(
                    $"SIDEDEFS: размер {bytes.Length} не кратен {SideDefSize}, " +
                    $"читаем первые {count} записей");
            }
            var sides = new SideDef[count];
            using var ms = new MemoryStream(bytes);
            using var r = new BinaryReader(ms);
            for (int i = 0; i < count; i++)
            {
                short tx = r.ReadInt16();
                short ty = r.ReadInt16();
                string upper = ReadName8(r);
                string lower = ReadName8(r);
                string middle = ReadName8(r);
                ushort sector = r.ReadUInt16();
                sides[i] = new SideDef(tx, ty, upper, lower, middle, sector);
            }
            return sides;
        }

        public static Sector[] ParseSectors(byte[] bytes)
        {
            if (bytes == null) return Array.Empty<Sector>();
            int count = bytes.Length / SectorSize;
            if (bytes.Length % SectorSize != 0)
            {
                MapLog.Warning(
                    $"SECTORS: размер {bytes.Length} не кратен {SectorSize}, " +
                    $"читаем первые {count} записей");
            }
            var sectors = new Sector[count];
            using var ms = new MemoryStream(bytes);
            using var r = new BinaryReader(ms);
            for (int i = 0; i < count; i++)
            {
                short floorH = r.ReadInt16();
                short ceilH = r.ReadInt16();
                string floorFlat = ReadName8(r);
                string ceilFlat = ReadName8(r);
                ushort light = r.ReadUInt16();
                ushort special = r.ReadUInt16();
                ushort tag = r.ReadUInt16();
                sectors[i] = new Sector(floorH, ceilH, floorFlat, ceilFlat,
                                        light, special, tag);
            }
            return sectors;
        }

        private static string ReadName8(BinaryReader r)
        {
            var raw = r.ReadBytes(8);
            int end = raw.Length;
            for (int i = 0; i < raw.Length; i++)
                if (raw[i] == 0) { end = i; break; }
            return Encoding.ASCII.GetString(raw, 0, end);
        }
    }
}
