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

    public sealed class WadFile : IDisposable
    {
        private readonly Stream stream;
        private readonly bool ownsStream;
        private readonly BinaryReader reader;

        public WadHeader Header { get; }

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
        }

        public void Dispose()
        {
            reader.Dispose();
            if (ownsStream) stream.Dispose();
        }
    }
}
