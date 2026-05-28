using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Doom.Wad.Tests
{
    public class WadFreedoomTests
    {
        private static string FreedoomPath =>
            Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");

        [Test]
        public void Opens_freedoom1_wad_as_IWAD()
        {
            Assert.That(File.Exists(FreedoomPath),
                $"freedoom1.wad missing at {FreedoomPath} — Stage 0 incomplete?");

            using var wad = WadFile.Open(FreedoomPath);

            Assert.That(wad.Header.Signature, Is.EqualTo("IWAD"));
            Assert.That(wad.Directory.Count, Is.GreaterThan(1000),
                "Freedoom Phase 1 contains several thousand lumps");
        }

        [Test]
        public void Contains_PLAYPAL_lump()
        {
            using var wad = WadFile.Open(FreedoomPath);
            int idx = wad.FindLump("PLAYPAL");

            Assert.That(idx, Is.GreaterThanOrEqualTo(0), "PLAYPAL must be present");
            // PLAYPAL = 14 palettes × 256 colors × 3 bytes = 10752 bytes
            Assert.That(wad.Directory[idx].Size, Is.EqualTo(14 * 256 * 3));
        }

        [Test]
        public void Contains_at_least_E1M1_map_marker()
        {
            using var wad = WadFile.Open(FreedoomPath);
            int idx = wad.FindLump("E1M1");

            Assert.That(idx, Is.GreaterThanOrEqualTo(0));
            Assert.That(wad.Directory[idx].Size, Is.EqualTo(0),
                "Map marker is a zero-size lump");
        }

        [Test]
        public void Lumps_after_E1M1_are_expected_map_components()
        {
            using var wad = WadFile.Open(FreedoomPath);
            int idx = wad.FindLump("E1M1");

            // Canonical post-marker order: THINGS, LINEDEFS, SIDEDEFS, VERTEXES, SEGS,
            // SSECTORS, NODES, SECTORS, REJECT, BLOCKMAP
            Assert.That(wad.Directory[idx + 1].Name, Is.EqualTo("THINGS"));
            Assert.That(wad.Directory[idx + 2].Name, Is.EqualTo("LINEDEFS"));
            Assert.That(wad.Directory[idx + 3].Name, Is.EqualTo("SIDEDEFS"));
            Assert.That(wad.Directory[idx + 4].Name, Is.EqualTo("VERTEXES"));
            Assert.That(wad.Directory[idx + 8].Name, Is.EqualTo("SECTORS"));
        }
    }
}
