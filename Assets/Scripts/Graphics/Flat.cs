namespace Doom.Graphics
{
    /// Flats are raw size*size palette indices, row-major top-to-bottom. Opaque.
    public static class Flat
    {
        public static DecodedImage Decode(byte[] flatLump, Palette palette, int size = 64)
        {
            var rgba = new byte[size * size * 4];
            int n = System.Math.Min(flatLump.Length, size * size);
            for (int i = 0; i < n; i++)
            {
                palette.GetColor(flatLump[i], out byte r, out byte g, out byte b);
                int o = i * 4;
                rgba[o] = r; rgba[o + 1] = g; rgba[o + 2] = b; rgba[o + 3] = 255;
            }
            return new DecodedImage(size, size, rgba);
        }
    }
}
