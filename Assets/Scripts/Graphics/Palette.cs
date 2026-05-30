using System.IO;

namespace Doom.Graphics
{
    /// PLAYPAL palette. Uses palette 0 (the first 768 bytes = 256 RGB triples).
    public sealed class Palette
    {
        private readonly byte[] rgb; // 256 * 3

        public int Count => 256;

        public Palette(byte[] playpalLump)
        {
            if (playpalLump == null || playpalLump.Length < 256 * 3)
                throw new InvalidDataException(
                    $"PLAYPAL too short: {(playpalLump?.Length ?? 0)} bytes, need at least 768");
            rgb = new byte[256 * 3];
            System.Array.Copy(playpalLump, rgb, 256 * 3);
        }

        public void GetColor(int index, out byte r, out byte g, out byte b)
        {
            int i = index * 3;
            r = rgb[i]; g = rgb[i + 1]; b = rgb[i + 2];
        }
    }
}
