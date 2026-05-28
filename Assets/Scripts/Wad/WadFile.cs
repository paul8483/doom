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
}
