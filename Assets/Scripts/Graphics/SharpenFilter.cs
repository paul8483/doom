using System;

namespace Doom.Graphics
{
    /// Unsharp mask for upscaled sprite/UI art: restores pixel-art crunch that
    /// Super-xBR smooths away on iconic sprites (weapons, monsters, pickups).
    /// RGB only; alpha is never modified; fully transparent pixels neither
    /// change nor contribute to the blur. Pure and deterministic.
    public static class SharpenFilter
    {
        /// Calibrated on Freedoom sprite previews 2026-07-22 (weapon viewmodels,
        /// monsters, pickups): 0.5 restores definition without visible halos.
        public const float DefaultAmount = 0.5f;

        public static DecodedImage Apply(DecodedImage source)
            => Apply(source, DefaultAmount);

        public static DecodedImage Apply(DecodedImage source, float amount)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (source.Width <= 0 || source.Height <= 0)
                throw new ArgumentOutOfRangeException(nameof(source),
                    "Source dimensions must be positive.");
            if (source.Rgba == null)
                throw new ArgumentException("Source RGBA buffer is null.", nameof(source));
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0f)
                throw new ArgumentOutOfRangeException(nameof(amount),
                    "Amount must be a non-negative finite number.");

            long expectedLen = (long)source.Width * source.Height * 4L;
            if (source.Rgba.Length != expectedLen)
                throw new ArgumentException(
                    $"Source RGBA length {source.Rgba.Length} != {expectedLen}.",
                    nameof(source));

            int w = source.Width;
            int h = source.Height;
            var src = source.Rgba;
            var dst = (byte[])src.Clone();
            if (amount == 0f)
                return new DecodedImage(w, h, dst);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int ci = (y * w + x) * 4;
                    if (src[ci + 3] == 0)
                        continue;

                    float sumR = 0f, sumG = 0f, sumB = 0f;
                    int count = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int ny = y + dy;
                        if (ny < 0 || ny >= h) continue;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx;
                            if (nx < 0 || nx >= w) continue;
                            int ni = (ny * w + nx) * 4;
                            if (src[ni + 3] == 0) continue;
                            sumR += src[ni];
                            sumG += src[ni + 1];
                            sumB += src[ni + 2];
                            count++;
                        }
                    }
                    if (count == 0)
                        continue;

                    float blurR = sumR / count;
                    float blurG = sumG / count;
                    float blurB = sumB / count;
                    dst[ci] = ClampByte(src[ci] + amount * (src[ci] - blurR));
                    dst[ci + 1] = ClampByte(src[ci + 1] + amount * (src[ci + 1] - blurG));
                    dst[ci + 2] = ClampByte(src[ci + 2] + amount * (src[ci + 2] - blurB));
                }
            }

            return new DecodedImage(w, h, dst);
        }

        static byte ClampByte(float v)
        {
            if (v <= 0f) return 0;
            if (v >= 255f) return 255;
            return (byte)(v + 0.5f);
        }
    }
}
