namespace Doom.Graphics
{
    /// Decoded image in RGBA32, row-major, top-to-bottom (row 0 = topmost pixel).
    /// Rgba.Length == Width * Height * 4. The Unity glue flips rows on upload,
    /// so this stays the natural "image" orientation for testing.
    public sealed class DecodedImage
    {
        public int Width { get; }
        public int Height { get; }
        public byte[] Rgba { get; }

        public DecodedImage(int width, int height, byte[] rgba)
        {
            Width = width;
            Height = height;
            Rgba = rgba;
        }

        /// Pixel accessor for tests: returns (r,g,b,a) at (x,y), y from top.
        public (byte r, byte g, byte b, byte a) GetPixel(int x, int y)
        {
            int i = (y * Width + x) * 4;
            return (Rgba[i], Rgba[i + 1], Rgba[i + 2], Rgba[i + 3]);
        }
    }
}
