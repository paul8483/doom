using System;

namespace Doom.Graphics
{
    /// Pattern-gated dedither: collapses two-color checkerboard dither into its
    /// midtone while leaving organic noise, grain and edges untouched. A pixel
    /// is smoothed only when its 3×3 neighborhood has true checkerboard
    /// structure: diagonals match the center (phase A), orthogonals match each
    /// other (phase B), and the two phases differ by a small-but-real contrast.
    /// Pure, deterministic, does not mutate the input.
    public static class DeditherFilter
    {
        /// Weighted-RGB phase contrast at or above which the pattern is treated
        /// as real detail (fine grates, 1px grooves) and left untouched.
        public const float CrossDistanceThreshold = 40f;

        /// Weighted-RGB cohesion tolerance inside each checkerboard phase, and
        /// the minimum phase contrast: cross distance must exceed this so that
        /// near-uniform noise blobs are never averaged.
        public const float GroupTolerance = 10f;

        const float WeightR = 0.30f;
        const float WeightG = 0.59f;
        const float WeightB = 0.11f;

        public static DecodedImage Apply(DecodedImage source, PixelWrapMode wrap)
            => Apply(source, wrap, CrossDistanceThreshold, out _);

        /// Explicit-threshold overload for calibration/diagnostic tooling.
        public static DecodedImage Apply(DecodedImage source, PixelWrapMode wrap, float crossThreshold)
            => Apply(source, wrap, crossThreshold, out _);

        /// Diagnostic overload: <paramref name="matchedMask"/> gets one flag per
        /// pixel telling whether the checkerboard gate fired there.
        public static DecodedImage Apply(
            DecodedImage source, PixelWrapMode wrap, float crossThreshold, out bool[] matchedMask)
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
            Array.Copy(src, dst, src.Length);
            matchedMask = new bool[w * h];

            // Neighbor offsets: 4 diagonals (phase A with center), 4 orthogonals
            // (phase B).
            Span<int> diagIdx = stackalloc int[4];
            Span<int> orthoIdx = stackalloc int[4];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int ci = PixelIndex(x, y, w);
                    if (src[ci + 3] == 0)
                        continue;

                    int xm = SampleX(x, -1, w, wrap);
                    int xp = SampleX(x, +1, w, wrap);
                    int ym = SampleY(y, -1, h, wrap);
                    int yp = SampleY(y, +1, h, wrap);

                    diagIdx[0] = PixelIndex(xm, ym, w);
                    diagIdx[1] = PixelIndex(xp, ym, w);
                    diagIdx[2] = PixelIndex(xm, yp, w);
                    diagIdx[3] = PixelIndex(xp, yp, w);
                    orthoIdx[0] = PixelIndex(xm, y, w);
                    orthoIdx[1] = PixelIndex(xp, y, w);
                    orthoIdx[2] = PixelIndex(x, ym, w);
                    orthoIdx[3] = PixelIndex(x, yp, w);

                    if (!AllOpaque(src, diagIdx) || !AllOpaque(src, orthoIdx))
                        continue;

                    byte cr = src[ci], cg = src[ci + 1], cb = src[ci + 2];

                    // Phase A cohesion: all diagonals close to the center.
                    if (!AllWithin(src, diagIdx, cr, cg, cb, GroupTolerance))
                        continue;

                    // Phase B cohesion: all orthogonals close to the first one.
                    byte or0 = src[orthoIdx[0]];
                    byte og0 = src[orthoIdx[0] + 1];
                    byte ob0 = src[orthoIdx[0] + 2];
                    if (!AllWithin(src, orthoIdx, or0, og0, ob0, GroupTolerance))
                        continue;

                    // Phase contrast: real but small — otherwise it is either a
                    // near-uniform blob (nothing to fix) or genuine detail.
                    float ar = (cr + src[diagIdx[0]] + src[diagIdx[1]] + src[diagIdx[2]] + src[diagIdx[3]]) / 5f;
                    float ag = (cg + src[diagIdx[0] + 1] + src[diagIdx[1] + 1] + src[diagIdx[2] + 1] + src[diagIdx[3] + 1]) / 5f;
                    float ab = (cb + src[diagIdx[0] + 2] + src[diagIdx[1] + 2] + src[diagIdx[2] + 2] + src[diagIdx[3] + 2]) / 5f;
                    float br = (src[orthoIdx[0]] + src[orthoIdx[1]] + src[orthoIdx[2]] + src[orthoIdx[3]]) / 4f;
                    float bg = (src[orthoIdx[0] + 1] + src[orthoIdx[1] + 1] + src[orthoIdx[2] + 1] + src[orthoIdx[3] + 1]) / 4f;
                    float bb = (src[orthoIdx[0] + 2] + src[orthoIdx[1] + 2] + src[orthoIdx[2] + 2] + src[orthoIdx[3] + 2]) / 4f;

                    float cross = WeightedDistance(ar, ag, ab, br, bg, bb);
                    if (cross <= GroupTolerance || cross >= crossThreshold)
                        continue;

                    // Collapse both phases toward the shared midtone.
                    dst[ci] = (byte)((ar + br) * 0.5f + 0.5f);
                    dst[ci + 1] = (byte)((ag + bg) * 0.5f + 0.5f);
                    dst[ci + 2] = (byte)((ab + bb) * 0.5f + 0.5f);
                    matchedMask[y * w + x] = true;
                }
            }

            return new DecodedImage(w, h, dst);
        }

        static bool AllOpaque(byte[] src, Span<int> indices)
        {
            for (int i = 0; i < indices.Length; i++)
                if (src[indices[i] + 3] == 0)
                    return false;
            return true;
        }

        static bool AllWithin(
            byte[] src, Span<int> indices, byte r, byte g, byte b, float tolerance)
        {
            for (int i = 0; i < indices.Length; i++)
            {
                int ni = indices[i];
                if (WeightedDistance(r, g, b, src[ni], src[ni + 1], src[ni + 2]) > tolerance)
                    return false;
            }
            return true;
        }

        static float WeightedDistance(
            float r0, float g0, float b0, float r1, float g1, float b1)
        {
            float dr = r0 - r1;
            if (dr < 0f) dr = -dr;
            float dg = g0 - g1;
            if (dg < 0f) dg = -dg;
            float db = b0 - b1;
            if (db < 0f) db = -db;
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
