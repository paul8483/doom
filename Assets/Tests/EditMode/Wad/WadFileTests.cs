using System.IO;
using NUnit.Framework;

namespace Doom.Wad.Tests
{
    public class WadFileTests
    {
        [Test]
        public void Reads_IWAD_signature_and_lump_count()
        {
            var bytes = SyntheticWadBuilder.Build("IWAD", new[]
            {
                new SyntheticWadBuilder.Lump("FIRST", new byte[] { 1, 2, 3 }),
                new SyntheticWadBuilder.Lump("SECOND", new byte[] { 4 }),
            });

            using var wad = new WadFile(new MemoryStream(bytes), ownsStream: true);

            Assert.That(wad.Header.Signature, Is.EqualTo("IWAD"));
            Assert.That(wad.Header.NumLumps, Is.EqualTo(2));
            Assert.That(wad.Header.DirOffset, Is.GreaterThan(0));
        }

        [Test]
        public void Accepts_PWAD_signature()
        {
            var bytes = SyntheticWadBuilder.Build(
                "PWAD",
                new[] { new SyntheticWadBuilder.Lump("X", new byte[0]) });

            using var wad = new WadFile(new MemoryStream(bytes), ownsStream: true);

            Assert.That(wad.Header.Signature, Is.EqualTo("PWAD"));
        }

        [Test]
        public void Parses_directory_entries()
        {
            var bytes = SyntheticWadBuilder.Build("IWAD", new[]
            {
                new SyntheticWadBuilder.Lump("FIRST", new byte[] { 0xAA, 0xBB }),
                new SyntheticWadBuilder.Lump("E1M1", new byte[0]),
                new SyntheticWadBuilder.Lump("VERTEXES", new byte[] { 1, 2, 3, 4 }),
            });

            using var wad = new WadFile(new MemoryStream(bytes), ownsStream: true);

            Assert.That(wad.Directory.Count, Is.EqualTo(3));
            Assert.That(wad.Directory[0].Name, Is.EqualTo("FIRST"));
            Assert.That(wad.Directory[0].Size, Is.EqualTo(2));
            Assert.That(wad.Directory[1].Name, Is.EqualTo("E1M1"));
            Assert.That(wad.Directory[1].Size, Is.EqualTo(0));
            Assert.That(wad.Directory[2].Name, Is.EqualTo("VERTEXES"));
            Assert.That(wad.Directory[2].Size, Is.EqualTo(4));
        }

        [Test]
        public void Strips_null_padding_from_lump_names()
        {
            // "F" plus 7 NUL bytes (the on-disk encoding) must decode to "F", not "F\0\0\0\0\0\0\0".
            var bytes = SyntheticWadBuilder.Build("IWAD", new[]
            {
                new SyntheticWadBuilder.Lump("F", new byte[0]),
            });

            using var wad = new WadFile(new MemoryStream(bytes), ownsStream: true);

            Assert.That(wad.Directory[0].Name, Is.EqualTo("F"));
            Assert.That(wad.Directory[0].Name.Length, Is.EqualTo(1));
        }
    }
}
