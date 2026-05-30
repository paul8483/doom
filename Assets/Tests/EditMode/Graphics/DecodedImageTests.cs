using NUnit.Framework;

namespace Doom.Graphics.Tests
{
    public class DecodedImageTests
    {
        [Test]
        public void GetPixel_reads_rgba_at_coordinate()
        {
            // 2x1 image: pixel(0,0)=red opaque, pixel(1,0)=green transparent
            var rgba = new byte[] { 255, 0, 0, 255,   0, 255, 0, 0 };
            var img = new DecodedImage(2, 1, rgba);

            Assert.That(img.Width, Is.EqualTo(2));
            Assert.That(img.Height, Is.EqualTo(1));
            Assert.That(img.GetPixel(0, 0), Is.EqualTo(((byte)255, (byte)0, (byte)0, (byte)255)));
            Assert.That(img.GetPixel(1, 0), Is.EqualTo(((byte)0, (byte)255, (byte)0, (byte)0)));
        }
    }
}
