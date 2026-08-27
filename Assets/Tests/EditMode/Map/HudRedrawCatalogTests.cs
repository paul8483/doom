using NUnit.Framework;
using Doom.Graphics;
using Doom.MapBuild.Rendering;

namespace Doom.Map.Tests
{
    /// HudRedrawCatalog gating: size validation and allowlist membership
    /// decide whether a status-bar patch's Enhanced texture comes from a
    /// redraw or the Super-xBR fallback. Exercised through the override seam.
    public class HudRedrawCatalogTests
    {
        [TearDown]
        public void TearDown() => HudRedrawCatalog.ClearForTests();

        static DecodedImage Opaque(int w, int h)
        {
            var rgba = new byte[w * h * 4];
            for (int i = 3; i < rgba.Length; i += 4) rgba[i] = 255;
            return new DecodedImage(w, h, rgba);
        }

        [Test]
        public void Valid_override_is_served()
        {
            var native = Opaque(13, 16);
            var redraw = Opaque(52, 64);
            HudRedrawCatalog.SetOverrideForTests("SYNTHHUD1", redraw);

            Assert.IsTrue(HudRedrawCatalog.TryGet("SYNTHHUD1", native, out var got));
            Assert.AreSame(redraw, got);
        }

        [Test]
        public void Wrong_size_override_falls_back()
        {
            var native = Opaque(13, 16);
            HudRedrawCatalog.SetOverrideForTests("SYNTHHUD2", Opaque(26, 32));

            Assert.IsFalse(HudRedrawCatalog.TryGet("SYNTHHUD2", native, out _),
                "2x is not the 4x contract — Super-xBR fallback expected");
        }

        [Test]
        public void Unlisted_name_without_override_is_ignored()
        {
            var native = Opaque(13, 16);
            Assert.IsFalse(HudRedrawCatalog.TryGet("__NO_SUCH__", native, out _));
        }

        [Test]
        public void Allowlist_scale_matches_superxbr_slot()
        {
            Assert.AreEqual(4, HudRedrawAllowlist.Scale,
                "HUD redraws occupy the Enhanced4X slot — placement is "
                + "native-header based, so the multiple is a hard contract");
        }
    }
}
