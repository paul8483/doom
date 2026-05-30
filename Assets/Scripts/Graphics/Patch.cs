using System.IO;

namespace Doom.Graphics
{
    /// Picture-format header: dimensions and DOOM draw offsets.
    public readonly struct PatchHeader
    {
        public readonly int Width;
        public readonly int Height;
        public readonly int LeftOffset;
        public readonly int TopOffset;

        public PatchHeader(int width, int height, int left, int top)
        {
            Width = width; Height = height; LeftOffset = left; TopOffset = top;
        }
    }

    /// DOOM picture (patch) format decoder.
    /// A patch is column-major: width column-pointers, each pointing at a list of posts.
    /// Pixels not covered by any post are transparent (alpha = 0).
    public static class Patch
    {
        public static PatchHeader ReadHeader(byte[] patchLump)
        {
            using var ms = new MemoryStream(patchLump);
            using var r = new BinaryReader(ms);
            short width = r.ReadInt16();
            short height = r.ReadInt16();
            short left = r.ReadInt16();
            short top = r.ReadInt16();
            return new PatchHeader(width, height, left, top);
        }

        public static DecodedImage Decode(byte[] patchLump, Palette palette)
        {
            using var ms = new MemoryStream(patchLump);
            using var r = new BinaryReader(ms);

            short width = r.ReadInt16();
            short height = r.ReadInt16();
            r.ReadInt16(); // leftOffset (unused)
            r.ReadInt16(); // topOffset  (unused)

            var colOffsets = new int[width];
            for (int x = 0; x < width; x++)
                colOffsets[x] = r.ReadInt32();

            // RGBA canvas: transparent by default.
            var rgba = new byte[width * height * 4];

            for (int x = 0; x < width; x++)
            {
                ms.Position = colOffsets[x];
                while (true)
                {
                    byte topDelta = r.ReadByte();
                    if (topDelta == 0xFF) break; // column terminator

                    byte count = r.ReadByte();
                    r.ReadByte(); // unused padding pre-pixels

                    for (int i = 0; i < count; i++)
                    {
                        byte paletteIndex = r.ReadByte();
                        int y = topDelta + i;
                        if (y < 0 || y >= height) continue;

                        palette.GetColor(paletteIndex, out byte red, out byte green, out byte blue);
                        int o = (y * width + x) * 4;
                        rgba[o] = red;
                        rgba[o + 1] = green;
                        rgba[o + 2] = blue;
                        rgba[o + 3] = 255;
                    }

                    r.ReadByte(); // unused padding post-pixels
                }
            }

            return new DecodedImage(width, height, rgba);
        }
    }
}
