using NUnit.Framework;
using Doom.Graphics;
using Doom.MapBuild.Rendering;

namespace Doom.Map.Tests
{
    /// WorldRedrawCatalog gating: size validation and allowlist membership
    /// decide whether a texture's Enhanced albedo comes from a redraw or the
    /// Super-xBR fallback. Exercised through the test override seam.
    public class WorldRedrawCatalogTests
    {
        [TearDown]
        public void TearDown() => WorldRedrawCatalog.ClearForTests();

        static DecodedImage Opaque(int w, int h)
        {
            var rgba = new byte[w * h * 4];
            for (int i = 3; i < rgba.Length; i += 4) rgba[i] = 255;
            return new DecodedImage(w, h, rgba);
        }

        [Test]
        public void Valid_override_is_served()
        {
            var native = Opaque(16, 32);
            var redraw = Opaque(64, 128);
            WorldRedrawCatalog.SetOverrideForTests("SYNTH1", redraw);

            Assert.IsTrue(WorldRedrawCatalog.TryGet("SYNTH1", native, out var got));
            Assert.AreSame(redraw, got);
        }

        [Test]
        public void Wrong_size_override_falls_back()
        {
            var native = Opaque(16, 32);
            WorldRedrawCatalog.SetOverrideForTests("SYNTH2", Opaque(32, 64));

            Assert.IsFalse(WorldRedrawCatalog.TryGet("SYNTH2", native, out _),
                "2x is not the 4x contract — Super-xBR fallback expected");
        }

        [Test]
        public void Unlisted_name_without_override_is_ignored()
        {
            var native = Opaque(16, 32);
            Assert.IsFalse(WorldRedrawCatalog.TryGet("__NO_SUCH__", native, out _));
        }

        [Test]
        public void Sky_is_authored_at_8x_and_a_4x_sky_falls_back()
        {
            // SKY1 stretches one copy over the whole sky sphere; its contract
            // is 8x (wave 17). Any other name keeps the 4x slot.
            Assert.AreEqual(8, WorldRedrawAllowlist.ScaleFor("SKY1"));
            Assert.AreEqual(4, WorldRedrawAllowlist.ScaleFor("STONE2"));

            var native = Opaque(256, 128);
            WorldRedrawCatalog.SetOverrideForTests("SKY1", Opaque(1024, 512));
            Assert.IsFalse(WorldRedrawCatalog.TryGet("SKY1", native, out _),
                "a 4x sky is not the 8x contract - Super-xBR fallback expected");

            var redraw = Opaque(2048, 1024);
            WorldRedrawCatalog.SetOverrideForTests("SKY1", redraw);
            Assert.IsTrue(WorldRedrawCatalog.TryGet("SKY1", native, out var got));
            Assert.AreSame(redraw, got);
        }

        [Test]
        public void Allowlist_scale_matches_superxbr_slot()
        {
            Assert.AreEqual(4, WorldRedrawAllowlist.Scale,
                "redraws occupy the Enhanced4X slot");
        }
    }
}
