using System;
using System.IO;
using System.Text;

namespace Doom.Wad
{
    public readonly struct WadHeader
    {
        public readonly string Signature;
        public readonly int NumLumps;
        public readonly int DirOffset;

        public WadHeader(string signature, int numLumps, int dirOffset)
        {
            Signature = signature;
            NumLumps = numLumps;
            DirOffset = dirOffset;
        }
    }

    public readonly struct LumpInfo
    {
        public readonly string Name;
        public readonly int Offset;
        public readonly int Size;

        public LumpInfo(string name, int offset, int size)
        {
            Name = name;
            Offset = offset;
            Size = size;
        }
    }

    public sealed class WadFile : IDisposable
    {
        private readonly Stream stream;
        private readonly bool ownsStream;
        private readonly BinaryReader reader;

        public WadHeader Header { get; }
        public System.Collections.Generic.IReadOnlyList<LumpInfo> Directory { get; }

        public WadFile(Stream stream, bool ownsStream = false)
        {
            this.stream = stream;
            this.ownsStream = ownsStream;
            this.reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

            stream.Position = 0;
            var sig = Encoding.ASCII.GetString(reader.ReadBytes(4));
            var numLumps = reader.ReadInt32();
            var dirOffset = reader.ReadInt32();

            if (sig != "IWAD" && sig != "PWAD")
            {
                throw new InvalidDataException(
                    $"Not a WAD file: signature is '{sig}', expected 'IWAD' or 'PWAD'");
            }
            if (numLumps < 0)
            {
                throw new InvalidDataException(
                    $"Invalid WAD: negative lump count {numLumps}");
            }
            if (dirOffset < 12 || dirOffset > stream.Length)
            {
                throw new InvalidDataException(
                    $"Invalid WAD: directory offset {dirOffset} out of range");
            }

            Header = new WadHeader(sig, numLumps, dirOffset);

            stream.Position = dirOffset;
            var entries = new LumpInfo[numLumps];
            for (int i = 0; i < numLumps; i++)
            {
                var filepos = reader.ReadInt32();
                var size = reader.ReadInt32();
                var nameBytes = reader.ReadBytes(8);
                var name = DecodeName(nameBytes);
                entries[i] = new LumpInfo(name, filepos, size);
            }
            Directory = entries;
        }

        public int FindLump(string name)
        {
            for (int i = 0; i < Directory.Count; i++)
            {
                if (Directory[i].Name == name) return i;
            }
            return -1;
        }

        public byte[] ReadLump(string name)
        {
            int idx = FindLump(name);
            if (idx < 0)
            {
                throw new System.Collections.Generic.KeyNotFoundException(
                    $"Lump '{name}' not found in WAD");
            }
            return ReadLump(idx);
        }

        public byte[] ReadLump(int index)
        {
            var entry = Directory[index];
            if (entry.Size == 0) return System.Array.Empty<byte>();

            stream.Position = entry.Offset;
            var buf = new byte[entry.Size];
            int read = 0;
            while (read < buf.Length)
            {
                int n = stream.Read(buf, read, buf.Length - read);
                if (n <= 0) throw new EndOfStreamException(
                    $"Truncated lump '{entry.Name}': expected {buf.Length} bytes, got {read}");
                read += n;
            }
            return buf;
        }

        public void Dispose()
        {
            reader.Dispose();
            if (ownsStream) stream.Dispose();
        }

        private static string DecodeName(byte[] raw)
        {
            int end = raw.Length;
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] == 0) { end = i; break; }
            }
            return Encoding.ASCII.GetString(raw, 0, end);
        }
    }

    public static class WadMapNames
    {
        // ExMy: x ∈ 1..4, y ∈ 1..9
        // MAPxx: xx ∈ 01..32
        public static bool IsMapMarker(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            if (name.Length == 4 && name[0] == 'E' && name[2] == 'M')
            {
                int episode = name[1] - '0';
                int map = name[3] - '0';
                return episode >= 1 && episode <= 4 && map >= 1 && map <= 9;
            }

            if (name.Length == 5 && name[0] == 'M' && name[1] == 'A' && name[2] == 'P')
            {
                int hi = name[3] - '0';
                int lo = name[4] - '0';
                if (hi < 0 || hi > 9 || lo < 0 || lo > 9) return false;
                int n = hi * 10 + lo;
                return n >= 1 && n <= 32;
            }

            return false;
        }
    }
}
