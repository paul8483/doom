using System;
using System.IO;

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

    public static class MapData
    {
        private const int VertexSize  = 4;
        private const int LineDefSize = 14;

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
    }
}
