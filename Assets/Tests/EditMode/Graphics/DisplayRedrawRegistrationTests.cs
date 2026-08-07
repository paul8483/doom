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

        [Test]
        public void Allowlist_has_nine_gate0_lumps_and_excludes_stima()
        {
            Assert.That(DisplayRedrawAllowlist.Lumps.Length, Is.EqualTo(9));
            Assert.That(DisplayRedrawAllowlist.Contains("SHOTA0"), Is.True);
            Assert.That(DisplayRedrawAllowlist.Contains("STIMA0"), Is.False);
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

                Assert.That(System.Math.Abs(nb.minX - rb.minX), Is.LessThanOrEqualTo(BBoxTolerance),
                    $"{lump} minX native={nb.minX} redraw={rb.minX}");
                Assert.That(System.Math.Abs(nb.minY - rb.minY), Is.LessThanOrEqualTo(BBoxTolerance),
                    $"{lump} minY native={nb.minY} redraw={rb.minY}");
                Assert.That(System.Math.Abs(nb.maxX - rb.maxX), Is.LessThanOrEqualTo(BBoxTolerance),
                    $"{lump} maxX native={nb.maxX} redraw={rb.maxX}");
                Assert.That(System.Math.Abs(nb.maxY - rb.maxY), Is.LessThanOrEqualTo(BBoxTolerance),
                    $"{lump} maxY native={nb.maxY} redraw={rb.maxY}");
            }
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
