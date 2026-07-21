using System;

namespace Doom.Graphics
{
    /// Dilates opaque RGB into fully transparent neighboring texels so Super-xBR
    /// (and bilinear sampling) do not pull hidden black/magenta fringe colors into
    /// visible cutout edges. Alpha is never modified. Pure and deterministic.
    public static class AlphaBleedGuard
    {
        /// Default dilation iterations (1–2 is enough for cutout fringes).
        public const int DefaultIterations = 2;

        /// 8-neighbor offsets in fixed scan order (ties break by this order).
        static readonly int[] NeighborDx = { -1, 0, 1, -1, 1, -1, 0, 1 };
        static readonly int[] NeighborDy = { -1, -1, -1, 0, 0, 1, 1, 1 };

        public static DecodedImage Dilate(DecodedImage source)
            => Dilate(source, DefaultIterations);

        public static DecodedImage Dilate(DecodedImage source, int iterations)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (source.Width <= 0 || source.Height <= 0)
                throw new ArgumentOutOfRangeException(nameof(source),
                    "Source dimensions must be positive.");
            if (source.Rgba == null)
                throw new ArgumentException("Source RGBA buffer is null.", nameof(source));
            if (iterations < 0)
                throw new ArgumentOutOfRangeException(nameof(iterations),
                    "Iterations must be non-negative.");

            long expectedLen = (long)source.Width * source.Height * 4L;
            if (source.Rgba.Length != expectedLen)
                throw new ArgumentException(
                    $"Source RGBA length {source.Rgba.Length} != {expectedLen}.",
                    nameof(source));

            int w = source.Width;
            int h = source.Height;
            var cur = (byte[])source.Rgba.Clone();
            if (iterations == 0)
                return new DecodedImage(w, h, cur);

            var next = new byte[cur.Length];
            // Color sources are originally opaque pixels, then any texel that has
            // already received bled RGB (alpha stays 0, so a parallel mask is required).
            var curValid = new bool[w * h];
            var nextValid = new bool[w * h];
            for (int i = 0; i < curValid.Length; i++)
                curValid[i] = cur[i * 4 + 3] != 0;

            for (int iter = 0; iter < iterations; iter++)
            {
                Array.Copy(cur, next, cur.Length);
                Array.Copy(curValid, nextValid, curValid.Length);

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int pi = y * w + x;
                        if (curValid[pi])
                            continue;

                        int bestDist = int.MaxValue;
                        int bestSi = -1;
                        for (int n = 0; n < 8; n++)
                        {
                            int nx = x + NeighborDx[n];
                            int ny = y + NeighborDy[n];
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                                continue;

                            int npi = ny * w + nx;
                            if (!curValid[npi])
                                continue;

                            int dist = Math.Abs(NeighborDx[n]) + Math.Abs(NeighborDy[n]);
                            if (dist < bestDist)
                            {
                                bestDist = dist;
                                bestSi = npi * 4;
                            }
                        }

                        if (bestSi < 0)
                            continue;

                        int i = pi * 4;
                        next[i] = cur[bestSi];
                        next[i + 1] = cur[bestSi + 1];
                        next[i + 2] = cur[bestSi + 2];
                        // alpha stays 0
                        nextValid[pi] = true;
                    }
                }

                var tmp = cur;
                cur = next;
                next = tmp;
                var tmpV = curValid;
                curValid = nextValid;
                nextValid = tmpV;
            }

            return new DecodedImage(w, h, cur);
        }
    }
}
