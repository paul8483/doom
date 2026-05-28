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
    }
}
