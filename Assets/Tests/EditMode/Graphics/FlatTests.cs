using NUnit.Framework;

namespace Doom.Graphics.Tests
{
    public class FlatTests
    {
        [Test]
        public void Decodes_64x64_opaque_image_through_palette()
        {
            var pal = new Palette(SyntheticGfxBuilder.BuildPlaypal());
            var lump = SyntheticGfxBuilder.BuildFlat(fill: 200);

            var img = Flat.Decode(lump, pal);

            Assert.That(img.Width, Is.EqualTo(64));
            Assert.That(img.Height, Is.EqualTo(64));
            Assert.That(img.Rgba.Length, Is.EqualTo(64 * 64 * 4));
            // Index 200 -> palette 0 color (200, 0, 200), fully opaque
            Assert.That(img.GetPixel(0, 0), Is.EqualTo(((byte)200, (byte)0, (byte)200, (byte)255)));
            Assert.That(img.GetPixel(63, 63), Is.EqualTo(((byte)200, (byte)0, (byte)200, (byte)255)));
        }

        [Test]
        public void Per_pixel_indices_map_top_to_bottom_left_to_right()
        {
            var pal = new Palette(SyntheticGfxBuilder.BuildPlaypal());
            var indices = new byte[64 * 64];
            indices[0] = 1;           // top-left
            indices[63] = 2;          // top-right (x=63, y=0)
            indices[64] = 3;          // x=0, y=1
            var img = Flat.Decode(SyntheticGfxBuilder.BuildFlat(indices), pal);

            Assert.That(img.GetPixel(0, 0).r, Is.EqualTo(1));
            Assert.That(img.GetPixel(63, 0).r, Is.EqualTo(2));
            Assert.That(img.GetPixel(0, 1).r, Is.EqualTo(3));
        }
    }
}
