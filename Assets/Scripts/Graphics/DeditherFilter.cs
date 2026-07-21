using System;

namespace Doom.Graphics
{
    /// Selective 3×3 smoothing that collapses palette dither / banding while
    /// preserving hard edges. Pure, deterministic, does not mutate the input.
    public static class DeditherFilter
    {
        /// Weighted-RGB distance below which a neighbor joins the average.
        /// Calibrated on Freedoom 0.13 STARTAN2 dithered panel (see
        /// <c>DeditherFilterTests.Freedoom_STARTAN2_dither_region_variance_drops_seam_stable</c>):
        /// T=40 reduces mid-panel luma variance without softening high-contrast seams.
        public const float ColorDistanceThreshold = 40f;

        const float WeightR = 0.30f;
        const float WeightG = 0.59f;
        const float WeightB = 0.11f;

        public static DecodedImage Apply(DecodedImage source, PixelWrapMode wrap)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (source.Width <= 0 || source.Height <= 0)
                throw new ArgumentOutOfRangeException(nameof(source),
                    "Source dimensions must be positive.");
            if (source.Rgba == null)
                throw new ArgumentException("Source RGBA buffer is null.", nameof(source));

            long expectedLen = (long)source.Width * source.Height * 4L;
            if (source.Rgba.Length != expectedLen)
                throw new ArgumentException(
                    $"Source RGBA length {source.Rgba.Length} != {expectedLen}.",
                    nameof(source));

            int w = source.Width;
            int h = source.Height;
            var src = source.Rgba;
            var dst = new byte[src.Length];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int ci = PixelIndex(x, y, w);
                    byte ca = src[ci + 3];

                    // Fully transparent: copy unchanged; never participate as center.
                    if (ca == 0)
                    {
                        dst[ci] = src[ci];
                        dst[ci + 1] = src[ci + 1];
                        dst[ci + 2] = src[ci + 2];
                        dst[ci + 3] = 0;
                        continue;
                    }

                    byte cr = src[ci];
                    byte cg = src[ci + 1];
                    byte cb = src[ci + 2];

                    // Center always contributes.
                    float sumR = cr;
                    float sumG = cg;
                    float sumB = cb;
                    int count = 1;

                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0)
                                continue;

                            int sx = SampleX(x, dx, w, wrap);
                            int sy = SampleY(y, dy, h, wrap);
                            int ni = PixelIndex(sx, sy, w);

                            // Transparent neighbors do not contribute RGB.
                            if (src[ni + 3] == 0)
                                continue;

                            byte nr = src[ni];
                            byte ng = src[ni + 1];
                            byte nb = src[ni + 2];
                            if (WeightedDistance(cr, cg, cb, nr, ng, nb) >= ColorDistanceThreshold)
                                continue;

                            sumR += nr;
                            sumG += ng;
                            sumB += nb;
                            count++;
                        }
                    }

                    dst[ci] = (byte)(sumR / count + 0.5f);
                    dst[ci + 1] = (byte)(sumG / count + 0.5f);
                    dst[ci + 2] = (byte)(sumB / count + 0.5f);
                    dst[ci + 3] = ca;
                }
            }

            return new DecodedImage(w, h, dst);
        }

        static float WeightedDistance(
            byte r0, byte g0, byte b0, byte r1, byte g1, byte b1)
        {
            int dr = r0 - r1;
            if (dr < 0) dr = -dr;
            int dg = g0 - g1;
            if (dg < 0) dg = -dg;
            int db = b0 - b1;
            if (db < 0) db = -db;
            return WeightR * dr + WeightG * dg + WeightB * db;
        }

        static int PixelIndex(int x, int y, int w) => (y * w + x) * 4;

        static int SampleX(int x, int dx, int w, PixelWrapMode wrap)
        {
            int nx = x + dx;
            switch (wrap)
            {
                case PixelWrapMode.RepeatX:
                case PixelWrapMode.RepeatXY:
                    nx %= w;
                    if (nx < 0) nx += w;
                    return nx;
                default:
                    if (nx < 0) return 0;
                    if (nx >= w) return w - 1;
                    return nx;
            }
        }

        static int SampleY(int y, int dy, int h, PixelWrapMode wrap)
        {
            int ny = y + dy;
            if (wrap == PixelWrapMode.RepeatXY)
            {
                ny %= h;
                if (ny < 0) ny += h;
                return ny;
            }

            if (ny < 0) return 0;
            if (ny >= h) return h - 1;
            return ny;
        }
    }
}
