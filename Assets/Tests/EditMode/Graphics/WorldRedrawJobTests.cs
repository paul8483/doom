using NUnit.Framework;

namespace Doom.Graphics.Tests
{
    /// World redraw seam in the Enhanced albedo job: a supplied redraw becomes
    /// level zero verbatim (no dedither/Super-xBR), and its mip levels are NOT
    /// palette-quantized — the palette decision lives in the level-0 file.
    public class WorldRedrawJobTests
    {
        static Palette GrayPalette()
        {
            var bytes = new byte[768];
            for (int i = 0; i < 256; i++)
            {
                bytes[i * 3] = (byte)i;
                bytes[i * 3 + 1] = (byte)i;
                bytes[i * 3 + 2] = (byte)i;
            }
            return new Palette(bytes);
        }

        static DecodedImage Solid(int w, int h, byte r, byte g, byte b)
        {
            var rgba = new byte[w * h * 4];
            for (int i = 0; i < rgba.Length; i += 4)
            {
                rgba[i] = r;
                rgba[i + 1] = g;
                rgba[i + 2] = b;
                rgba[i + 3] = 255;
            }
            return new DecodedImage(w, h, rgba);
        }

        [Test]
        public void Redraw_replaces_superxbr_as_level_zero()
        {
            var palette = GrayPalette();
            var native = Solid(8, 8, 128, 128, 128);
            // Marker color far from any gray palette entry.
            var redraw = Solid(32, 32, 10, 200, 30);

            var job = EnhancedJob.ForWorldAlbedo(
                "SYNTH", native, PixelWrapMode.RepeatX,
                applyDedither: true, applyAlphaBleed: false, palette, redraw);
            var result = EnhancedJobRunner.Run(job);

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreEqual(32, result.AlbedoMips[0].Width);
            Assert.AreEqual(32, result.AlbedoMips[0].Height);
            // Contract: level zero = SharpenFilter over the redraw (the sprite
            // 4× treatment) — for a solid color that is the redraw verbatim.
            Assert.AreEqual(SharpenFilter.Apply(redraw).Rgba, result.AlbedoMips[0].Rgba,
                "redraw (sharpened) must be level zero — not Super-xBR output");
            Assert.AreEqual(6, result.AlbedoMips.Count, "32 -> 1 mip chain");
        }

        [Test]
        public void Redraw_mips_stay_unquantized()
        {
            var palette = GrayPalette();
            var native = Solid(8, 8, 128, 128, 128);
            var redraw = Solid(32, 32, 10, 200, 30);

            var result = EnhancedJobRunner.Run(EnhancedJob.ForWorldAlbedo(
                "SYNTH", native, PixelWrapMode.RepeatX,
                applyDedither: false, applyAlphaBleed: false, palette, redraw));

            Assert.IsTrue(result.Success, result.ErrorMessage);
            // A palette snap would land on a gray; the average of a solid color
            // round-trips through linear space back to itself.
            var level1 = result.AlbedoMips[1];
            Assert.AreEqual(10, level1.Rgba[0], "r must not snap to palette");
            Assert.AreEqual(200, level1.Rgba[1], "g must not snap to palette");
            Assert.AreEqual(30, level1.Rgba[2], "b must not snap to palette");
        }

        [Test]
        public void Job_without_redraw_keeps_quantized_superxbr_path()
        {
            var palette = GrayPalette();
            var native = Solid(8, 8, 128, 128, 128);

            var result = EnhancedJobRunner.Run(EnhancedJob.ForWorldAlbedo(
                "SYNTH", native, PixelWrapMode.RepeatX,
                applyDedither: false, applyAlphaBleed: false, palette));

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreEqual(32, result.AlbedoMips[0].Width,
                "no redraw: Super-xBR 4x path");
        }
    }
}
