using System.Collections.Generic;
using NUnit.Framework;

namespace Doom.Graphics.Tests
{
    public class PatchTests
    {
        [Test]
        public void Decodes_size_from_header_and_marks_gaps_transparent()
        {
            var pal = new Palette(SyntheticGfxBuilder.BuildPlaypal());

            // 2x2 patch.
            // Column 0: one post at topdelta 0, length 2, pixels [10, 11].
            // Column 1: empty (terminator only) -> fully transparent.
            var col0 = new List<SyntheticGfxBuilder.Post>
            {
                new SyntheticGfxBuilder.Post { TopDelta = 0, Pixels = new byte[] { 10, 11 } }
            };
            var col1 = new List<SyntheticGfxBuilder.Post>();
            var lump = SyntheticGfxBuilder.BuildPatch(
                width: 2, height: 2, leftOffset: 0, topOffset: 0,
                columns: new List<IReadOnlyList<SyntheticGfxBuilder.Post>> { col0, col1 });

            var img = Patch.Decode(lump, pal);

            Assert.That(img.Width, Is.EqualTo(2));
            Assert.That(img.Height, Is.EqualTo(2));
            // Column 0 opaque: index 10 -> (10,0,10,255), index 11 -> (11,0,11,255)
            Assert.That(img.GetPixel(0, 0), Is.EqualTo(((byte)10, (byte)0, (byte)10, (byte)255)));
            Assert.That(img.GetPixel(0, 1), Is.EqualTo(((byte)11, (byte)0, (byte)11, (byte)255)));
            // Column 1 transparent: alpha 0
            Assert.That(img.GetPixel(1, 0).a, Is.EqualTo(0));
            Assert.That(img.GetPixel(1, 1).a, Is.EqualTo(0));
        }

        [Test]
        public void Post_topdelta_offsets_pixels_down_leaving_gap_transparent()
        {
            var pal = new Palette(SyntheticGfxBuilder.BuildPlaypal());
            // 1x3 patch, single post at topdelta 1, length 1, pixel [5].
            // Row 0 transparent, row 1 = index 5, row 2 transparent.
            var col0 = new List<SyntheticGfxBuilder.Post>
            {
                new SyntheticGfxBuilder.Post { TopDelta = 1, Pixels = new byte[] { 5 } }
            };
            var lump = SyntheticGfxBuilder.BuildPatch(
                1, 3, 0, 0,
                new List<IReadOnlyList<SyntheticGfxBuilder.Post>> { col0 });

            var img = Patch.Decode(lump, pal);

            Assert.That(img.GetPixel(0, 0).a, Is.EqualTo(0));
            Assert.That(img.GetPixel(0, 1), Is.EqualTo(((byte)5, (byte)0, (byte)5, (byte)255)));
            Assert.That(img.GetPixel(0, 2).a, Is.EqualTo(0));
        }
    }
}
