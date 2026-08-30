using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Wad;

namespace Doom.Graphics.Tests
{
    /// The Options Graphics Mode values are composed big-menu-font words
    /// ("ENHANCED"/"CLASSIC") cut letter-by-letter from WAD menu patches —
    /// the WAD ships no such words. These pins keep the composition working
    /// against the bundled Freedoom art and the fallback graceful.
    public class MenuWordPatchesTests
    {
        static string FreedoomPath =>
            Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");

        [Test]
        public void LoadStandard_composes_graphics_mode_words()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var pal = new Palette(wad.ReadLump("PLAYPAL"));
            var catalog = UiPatchCatalog.LoadStandard(wad, pal);

            Assert.That(catalog.TryGet(MenuWordPatches.EnhancedName, out var enhanced),
                Is.True, "ENHANCED word did not compose from Freedoom donors");
            Assert.That(catalog.TryGet(MenuWordPatches.ClassicName, out var classic),
                Is.True, "CLASSIC word did not compose from Freedoom donors");

            // Same row height as the ON/OFF value patches the row sits beside.
            Assert.That(catalog.TryGet("M_MSGON", out var on), Is.True);
            Assert.That(enhanced.Height, Is.EqualTo(on.Height));
            Assert.That(classic.Height, Is.EqualTo(on.Height));

            // 8 resp. 7 letters of the ~10 px font; must also fit the 320-wide
            // virtual screen right of the value column at x=180.
            Assert.That(enhanced.Width, Is.InRange(60, 140));
            Assert.That(classic.Width, Is.InRange(50, 130));

            AssertLooksLikeGlyphs(enhanced);
            AssertLooksLikeGlyphs(classic);
        }

        [Test]
        public void Compose_without_donors_fails_quietly()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var pal = new Palette(wad.ReadLump("PLAYPAL"));
            var catalog = UiPatchCatalog.Load(wad, pal, new[] { "STBAR" });

            MenuWordPatches.Install(catalog);

            Assert.That(catalog.ContainsKey(MenuWordPatches.EnhancedName), Is.False);
            Assert.That(catalog.ContainsKey(MenuWordPatches.ClassicName), Is.False);
        }

        [Test]
        public void Donor_lumps_are_in_the_standard_load_set()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var pal = new Palette(wad.ReadLump("PLAYPAL"));
            var catalog = UiPatchCatalog.LoadStandard(wad, pal);

            foreach (string donor in MenuWordPatches.DonorNames)
                Assert.That(catalog.TryGet(donor, out _), Is.True,
                    $"donor '{donor}' missing from the standard catalog");
        }

        static void AssertLooksLikeGlyphs(UiPatchInfo word)
        {
            var rgba = word.Image.Rgba;
            int opaque = 0;
            for (int o = 3; o < rgba.Length; o += 4)
            {
                // Patch decode is binary-alpha; composition must keep it so.
                Assert.That(rgba[o], Is.EqualTo(0).Or.EqualTo(255));
                if (rgba[o] == 255) opaque++;
            }

            // A word of solid menu letters, not a few stray pixels.
            Assert.That(opaque, Is.GreaterThan(200));

            // Trim leaves ink in the first and last columns.
            bool first = false, last = false;
            for (int y = 0; y < word.Height; y++)
            {
                first |= rgba[(y * word.Width) * 4 + 3] > 0;
                last |= rgba[(y * word.Width + word.Width - 1) * 4 + 3] > 0;
            }
            Assert.That(first, Is.True);
            Assert.That(last, Is.True);
        }
    }
}
