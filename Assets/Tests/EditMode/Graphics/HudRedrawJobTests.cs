using NUnit.Framework;
using Doom.Graphics;

namespace Doom.Graphics.Tests
{
    /// A HUD job with a display redraw must serve the redraw as the finished
    /// 4× RGBA (with the alpha-bleed dilate and sharpen the Super-xBR output
    /// gets) instead of upscaling the native — the world-albedo pattern on
    /// the flat RGBA path.
    public class HudRedrawJobTests
    {
        static DecodedImage Solid(int w, int h, byte r, byte g, byte b)
        {
            var rgba = new byte[w * h * 4];
            for (int i = 0; i < rgba.Length; i += 4)
            {
                rgba[i] = r; rgba[i + 1] = g; rgba[i + 2] = b; rgba[i + 3] = 255;
            }
            return new DecodedImage(w, h, rgba);
        }

        [Test]
        public void Hud_job_with_redraw_serves_the_redraw()
        {
            var native = Solid(8, 8, 255, 0, 0);       // red native
            var redraw = Solid(32, 32, 0, 255, 0);     // green 4x redraw

            var job = EnhancedJob.ForHud(
                "SYNTH", native, applyDedither: false, redraw: redraw);
            var result = EnhancedJobRunner.Run(job);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(32, result.Rgba.Width);
            Assert.AreEqual(32, result.Rgba.Height);
            // Center pixel comes from the redraw, not from the red upscale.
            int c = (16 * 32 + 16) * 4;
            Assert.Greater(result.Rgba.Rgba[c + 1], (byte)200, "green survives");
            Assert.Less(result.Rgba.Rgba[c], (byte)60, "no red upscale leakage");
        }

        [Test]
        public void Hud_job_without_redraw_upscales_the_native()
        {
            var native = Solid(8, 8, 255, 0, 0);
            var job = EnhancedJob.ForHud("SYNTH", native, applyDedither: false);
            var result = EnhancedJobRunner.Run(job);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(32, result.Rgba.Width);
            int c = (16 * 32 + 16) * 4;
            Assert.Greater(result.Rgba.Rgba[c], (byte)200, "red native upscaled");
        }
    }
}
