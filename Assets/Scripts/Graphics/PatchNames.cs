using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Doom.Graphics
{
    /// PNAMES: int32 count, then count * 8-byte patch names (upper-cased).
    public sealed class PatchNames
    {
        private readonly string[] names;

        public int Count => names.Length;
        public string this[int index] => names[index];

        public PatchNames(byte[] pnamesLump)
        {
            using var ms = new MemoryStream(pnamesLump);
            using var r = new BinaryReader(ms);
            int count = r.ReadInt32();
            names = new string[count];
            for (int i = 0; i < count; i++)
                names[i] = ReadName8(r).ToUpperInvariant();
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
