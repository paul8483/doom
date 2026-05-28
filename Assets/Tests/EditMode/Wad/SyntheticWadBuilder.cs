using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Doom.Wad.Tests
{
    internal static class SyntheticWadBuilder
    {
        public readonly struct Lump
        {
            public readonly string Name;
            public readonly byte[] Data;
            public Lump(string name, byte[] data) { Name = name; Data = data; }
        }

        public static byte[] Build(string signature, IReadOnlyList<Lump> lumps)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);

            // Header: signature (4) + numLumps (4) + dirOffset (4)
            // dirOffset is back-patched after we know lump data length.
            w.Write(Encoding.ASCII.GetBytes(signature));
            w.Write(lumps.Count);
            long dirOffsetField = ms.Position;
            w.Write(0);

            // Lump bodies
            var entries = new (int Offset, int Size, string Name)[lumps.Count];
            for (int i = 0; i < lumps.Count; i++)
            {
                entries[i] = ((int)ms.Position, lumps[i].Data.Length, lumps[i].Name);
                w.Write(lumps[i].Data);
            }

            // Directory
            int dirOffset = (int)ms.Position;
            foreach (var e in entries)
            {
                w.Write(e.Offset);
                w.Write(e.Size);
                w.Write(EncodeName(e.Name));
            }

            // Back-patch the directory offset in the header
            ms.Position = dirOffsetField;
            w.Write(dirOffset);

            return ms.ToArray();
        }

        private static byte[] EncodeName(string name)
        {
            var buf = new byte[8];
            var ascii = Encoding.ASCII.GetBytes(name);
            System.Array.Copy(ascii, buf, System.Math.Min(ascii.Length, 8));
            return buf;
        }
    }
}
