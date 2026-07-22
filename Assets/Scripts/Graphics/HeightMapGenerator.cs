using System;

namespace Doom.Graphics
{
    /// Multi-scale height from albedo luminance: fine detail + blurred coarse form.
    /// Output is grayscale RGBA (R=G=B=height, A copied from source). Pure C#,
    /// deterministic, does not mutate the input.
    public static class HeightMapGenerator
    {
        const float LumR = 0.299f;
        const float LumG = 0.587f;
        const float LumB = 0.114f;

        /// Box-blur radius for the coarse pass (applied twice).
        public const int CoarseBlurRadius = 2;

        public static DecodedImage Generate(
            DecodedImage source,
            MaterialSurfaceCategory category,
            PixelWrapMode wrap)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (source.Width <= 0 || source.Height <= 0)
                throw new ArgumentException("Source image must have positive size.", nameof(source));
            if (source.Rgba == null || source.Rgba.Length < source.Width * source.Height * 4)
                throw new ArgumentException("Source RGBA buffer is incomplete.", nameof(source));

            var profile = MaterialSurfaceProfile.For(category);
            int w = source.Width;
            int h = source.Height;
            int n = w * h;
            var src = source.Rgba;

            var fine = new float[n];
            var alpha = new byte[n];
            for (int i = 0, p = 0; i < n; i++, p += 4)
            {
                fine[i] = (LumR * src[p] + LumG * src[p + 1] + LumB * src[p + 2]) / 255f;
                alpha[i] = src[p + 3];
            }

            var scratch = new float[n];
            var coarse = new float[n];
            BoxBlur(fine, scratch, w, h, wrap, CoarseBlurRadius);
            BoxBlur(scratch, coarse, w, h, wrap, CoarseBlurRadius);

            float wf = profile.HeightFineWeight;
            float wc = profile.HeightCoarseWeight;
            var dst = new byte[n * 4];
            for (int i = 0, p = 0; i < n; i++, p += 4)
            {
                float height = wf * fine[i] + wc * coarse[i];
                if (height < 0f) height = 0f;
                else if (height > 1f) height = 1f;
                byte b = (byte)(height * 255f + 0.5f);
                dst[p] = b;
                dst[p + 1] = b;
                dst[p + 2] = b;
                dst[p + 3] = alpha[i];
            }

            return new DecodedImage(w, h, dst);
        }

        static void BoxBlur(
            float[] src, float[] dst, int w, int h, PixelWrapMode wrap, int radius)
        {
            // Separable: horizontal then vertical into dst.
            var temp = new float[src.Length];
            int diam = radius * 2 + 1;
            float inv = 1f / diam;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float sum = 0f;
                    for (int k = -radius; k <= radius; k++)
                        sum += src[SampleIndex(SampleX(x, k, w, wrap), y, w)];
                    temp[y * w + x] = sum * inv;
                }
            }

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float sum = 0f;
                    for (int k = -radius; k <= radius; k++)
                        sum += temp[SampleIndex(x, SampleY(y, k, h, wrap), w)];
                    dst[y * w + x] = sum * inv;
                }
            }
        }

        static int SampleIndex(int x, int y, int w) => y * w + x;

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
