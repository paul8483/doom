using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Wad;

namespace Doom.Graphics.Tests
{
    public class DisplayRedrawRegistrationTests
    {
        const float WorldScale = 1f / 32f;
        // Redraw silhouettes are not pixel-identical; allow a few native texels.
        const int BBoxTolerance = 6;
        // Aggressive-key trees (NormalizeSubjectToRect) can sit wider than the
        // native WAD padding after registration — TRE2 needs ~13 px.
        const int AggressiveBBoxTolerance = 16;

        [Test]
        public void Allowlist_has_twenty_seven_lumps()
        {
            Assert.That(DisplayRedrawAllowlist.Lumps.Length, Is.EqualTo(27));
            Assert.That(DisplayRedrawAllowlist.Contains("SHOTA0"), Is.True);
            // ARM1/BAR1 blink: both frames covered (2026-08-08).
            Assert.That(DisplayRedrawAllowlist.Contains("ARM1B0"), Is.True);
            Assert.That(DisplayRedrawAllowlist.Contains("BAR1B0"), Is.True);
            // Trees (2026-08-08): single-frame decorations, full coverage.
            Assert.That(DisplayRedrawAllowlist.Contains("TRE1A0"), Is.True);
            Assert.That(DisplayRedrawAllowlist.Contains("TRE2A0"), Is.True);
            Assert.That(DisplayRedrawAllowlist.Contains("SMITA0"), Is.True);
            Assert.That(DisplayRedrawAllowlist.Contains("CLIPA0"), Is.True);
            Assert.That(DisplayRedrawAllowlist.Contains("SBOXA0"), Is.True);
            // Ammo set (2026-08-09): shells/rockets/cells/bullet box.
            Assert.That(DisplayRedrawAllowlist.Contains("AMMOA0"), Is.True);
            Assert.That(DisplayRedrawAllowlist.Contains("CELLA0"), Is.True);
            Assert.That(DisplayRedrawAllowlist.Contains("CELPA0"), Is.True);
            Assert.That(DisplayRedrawAllowlist.Contains("ROCKA0"), Is.True);
            Assert.That(DisplayRedrawAllowlist.Contains("SHELA0"), Is.True);
            // STIMA0 accepted 2026-08-10 (depth shapehint; the old v3 redraw
            // was rejected and never shipped).
            Assert.That(DisplayRedrawAllowlist.Contains("STIMA0"), Is.True);
            // MEDIA0 display redraw from depth shapehint-v2 (2026-08-11).
            Assert.That(DisplayRedrawAllowlist.Contains("MEDIA0"), Is.True);
            // BON2 armor-bonus A–D (A0 redraw reused for B/C/D, 2026-08-11).
            Assert.That(DisplayRedrawAllowlist.Contains("BON2A0"), Is.True);
            Assert.That(DisplayRedrawAllowlist.Contains("BON2B0"), Is.True);
            Assert.That(DisplayRedrawAllowlist.Contains("BON2C0"), Is.True);
            Assert.That(DisplayRedrawAllowlist.Contains("BON2D0"), Is.True);
            Assert.That(DisplayRedrawAllowlist.Contains("POSSA1"), Is.False);
        }

        [Test]
        public void Resources_exist_for_every_allowlisted_lump()
        {
            foreach (string lump in DisplayRedrawAllowlist.Lumps)
            {
                var tex = Resources.Load<Texture2D>(DisplayRedrawAllowlist.ResourcesPath(lump));
                Assert.That(tex, Is.Not.Null, lump);
                Assert.That(tex.width, Is.EqualTo(DisplayRedrawRegistration.CanvasSize), lump);
                Assert.That(tex.height, Is.EqualTo(DisplayRedrawRegistration.CanvasSize), lump);
                Assert.That(tex.filterMode, Is.EqualTo(FilterMode.Point), lump);
            }
        }

        [Test]
        public void Mapped_redraw_world_size_matches_native_patch()
        {
            string wadPath = Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = WadFile.Open(wadPath);

            foreach (string lump in DisplayRedrawAllowlist.Lumps)
            {
                int idx = wad.FindLump(lump);
                Assert.That(idx, Is.GreaterThanOrEqualTo(0), lump);
                var header = Patch.ReadHeader(wad.ReadLump(idx));
                var (w, h) = DisplayRedrawRegistration.BillboardWorldSize(
                    header.Width, header.Height, WorldScale);
                Assert.That(w, Is.EqualTo(header.Width * WorldScale).Within(1e-6f), lump);
                Assert.That(h, Is.EqualTo(header.Height * WorldScale).Within(1e-6f), lump);

                // Mapped texture covers the native rectangle; dims stay native.
                var native = Patch.Decode(wad.ReadLump(idx), new Palette(wad.ReadLump("PLAYPAL")));
                var redraw = TextureToDecoded(
                    Resources.Load<Texture2D>(DisplayRedrawAllowlist.ResourcesPath(lump)));
                var mapped = DisplayRedrawRegistration.MapRedrawToNativeRect(redraw, native);
                Assert.That(mapped.Width, Is.EqualTo(native.Width), lump);
                Assert.That(mapped.Height, Is.EqualTo(native.Height), lump);
            }
        }

        [Test]
        public void Silhouette_bbox_after_registration_matches_native_within_tolerance()
        {
            string wadPath = Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = WadFile.Open(wadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));

            foreach (string lump in DisplayRedrawAllowlist.Lumps)
            {
                int idx = wad.FindLump(lump);
                var native = Patch.Decode(wad.ReadLump(idx), palette);
                var redraw = TextureToDecoded(
                    Resources.Load<Texture2D>(DisplayRedrawAllowlist.ResourcesPath(lump)));
                var mapped = DisplayRedrawRegistration.MapRedrawToNativeRect(redraw, native);

                var nb = DisplayRedrawRegistration.SilhouetteBounds(native);
                var rb = DisplayRedrawRegistration.SilhouetteBounds(mapped);
                Assert.That(rb.maxX, Is.GreaterThanOrEqualTo(0), $"{lump}: empty redraw silhouette");

                int tol = DisplayRedrawAllowlist.UsesAggressiveKey(lump)
                    ? AggressiveBBoxTolerance
                    : BBoxTolerance;
                Assert.That(System.Math.Abs(nb.minX - rb.minX), Is.LessThanOrEqualTo(tol),
                    $"{lump} minX native={nb.minX} redraw={rb.minX}");
                Assert.That(System.Math.Abs(nb.minY - rb.minY), Is.LessThanOrEqualTo(tol),
                    $"{lump} minY native={nb.minY} redraw={rb.minY}");
                Assert.That(System.Math.Abs(nb.maxX - rb.maxX), Is.LessThanOrEqualTo(tol),
                    $"{lump} maxX native={nb.maxX} redraw={rb.maxX}");
                Assert.That(System.Math.Abs(nb.maxY - rb.maxY), Is.LessThanOrEqualTo(tol),
                    $"{lump} maxY native={nb.maxY} redraw={rb.maxY}");
            }
        }

        [Test]
        public void Aggressive_key_drops_enclosed_pockets_and_floating_islands()
        {
            // 16×16 transparent canvas: a 6×6 dark subject block with a
            // near-white pocket inside it, plus a detached 2-px gray speckle.
            const int S = 16;
            var rgba = new byte[S * S * 4];
            void Set(int x, int y, byte r, byte g, byte b)
            {
                int o = (y * S + x) * 4;
                rgba[o] = r; rgba[o + 1] = g; rgba[o + 2] = b; rgba[o + 3] = 255;
            }
            for (int y = 4; y < 10; y++)
                for (int x = 4; x < 10; x++)
                    Set(x, y, 121, 86, 52);    // bark subject (36 px component)
            Set(6, 6, 245, 244, 240);          // enclosed backdrop pocket
            Set(13, 2, 210, 208, 205);         // floating checker speckle
            Set(14, 2, 214, 212, 209);

            var keyed = DisplayRedrawRegistration.KeyOutBackdropAggressive(
                new DecodedImage(S, S, rgba));

            Assert.That(keyed.GetPixel(6, 6).a, Is.EqualTo(0), "pocket keyed");
            Assert.That(keyed.GetPixel(13, 2).a, Is.EqualTo(0), "speckle keyed");
            Assert.That(keyed.GetPixel(14, 2).a, Is.EqualTo(0), "speckle keyed");
            Assert.That(keyed.GetPixel(4, 4).a, Is.EqualTo(255), "subject kept");
            Assert.That(keyed.GetPixel(9, 9).a, Is.EqualTo(255), "subject kept");
        }

        [Test]
        public void Integer_scale_keeps_major_axis_at_most_416()
        {
            Assert.That(DisplayRedrawRegistration.IntegerScaleToMax(31, 16), Is.EqualTo(13)); // 31*13=403
            Assert.That(DisplayRedrawRegistration.IntegerScaleToMax(64, 64), Is.EqualTo(6));  // 384
            Assert.That(DisplayRedrawRegistration.IntegerScaleToMax(500, 10), Is.EqualTo(1));
        }

        static DecodedImage TextureToDecoded(Texture2D tex)
        {
            Assert.That(tex, Is.Not.Null);
            Assert.That(tex.isReadable, Is.True, tex.name);
            var pixels = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            var rgba = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            {
                int srcRow = (h - 1 - y) * w;
                int dstRow = y * w;
                for (int x = 0; x < w; x++)
                {
                    Color32 c = pixels[srcRow + x];
                    int o = (dstRow + x) * 4;
                    rgba[o] = c.r;
                    rgba[o + 1] = c.g;
                    rgba[o + 2] = c.b;
                    rgba[o + 3] = c.a;
                }
            }
            return new DecodedImage(w, h, rgba);
        }
    }
}
