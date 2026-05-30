using NUnit.Framework;

namespace Doom.Graphics.Tests
{
    public class PaletteTests
    {
        [Test]
        public void Parses_256_colors_from_palette_zero()
        {
            var lump = SyntheticGfxBuilder.BuildPlaypal();
            var pal = new Palette(lump);

            Assert.That(pal.Count, Is.EqualTo(256));
            // Builder formula for palette 0: color c = (c, 0, c)
            pal.GetColor(0, out var r0, out var g0, out var b0);
            Assert.That((r0, g0, b0), Is.EqualTo(((byte)0, (byte)0, (byte)0)));
            pal.GetColor(200, out var r, out var g, out var b);
            Assert.That((r, g, b), Is.EqualTo(((byte)200, (byte)0, (byte)200)));
        }

        [Test]
        public void Throws_on_too_short_lump()
        {
            Assert.Throws<System.IO.InvalidDataException>(() => new Palette(new byte[10]));
        }
    }
}
