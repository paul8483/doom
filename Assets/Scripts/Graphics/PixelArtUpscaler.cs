using System;

namespace Doom.Graphics
{
    /// Deterministic Scale2x / EPX upscale for palette-style RGBA images.
    public static class PixelArtUpscaler
    {
        public static DecodedImage Scale2X(DecodedImage source, PixelWrapMode wrap)
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

            int outW;
            int outH;
            int outLen;
            try
            {
                checked
                {
                    outW = source.Width * 2;
                    outH = source.Height * 2;
                    outLen = outW * outH * 4;
                }
            }
            catch (OverflowException)
            {
                throw new ArgumentOutOfRangeException(nameof(source),
                    "Upscaled dimensions overflow Int32.");
            }

            var src = source.Rgba;
            var dst = new byte[outLen];
            int w = source.Width;
            int h = source.Height;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int e = PixelIndex(x, y, w);
                    int b = PixelIndex(SampleX(x, 0, w, wrap), SampleY(y, -1, h, wrap), w);
                    int d = PixelIndex(SampleX(x, -1, w, wrap), SampleY(y, 0, h, wrap), w);
                    int f = PixelIndex(SampleX(x, 1, w, wrap), SampleY(y, 0, h, wrap), w);
                    int hn = PixelIndex(SampleX(x, 0, w, wrap), SampleY(y, 1, h, wrap), w);

                    // Scale2x / EPX:
                    // E0 = D==B && D!=H && B!=F ? D : E
                    // E1 = B==F && B!=D && F!=H ? F : E
                    // E2 = D==H && D!=B && H!=F ? D : E
                    // E3 = H==F && D!=H && B!=F ? F : E
                    int e0 = Equal(src, d, b) && !Equal(src, d, hn) && !Equal(src, b, f) ? d : e;
                    int e1 = Equal(src, b, f) && !Equal(src, b, d) && !Equal(src, f, hn) ? f : e;
                    int e2 = Equal(src, d, hn) && !Equal(src, d, b) && !Equal(src, hn, f) ? d : e;
                    int e3 = Equal(src, hn, f) && !Equal(src, d, hn) && !Equal(src, b, f) ? f : e;

                    int ox = x * 2;
                    int oy = y * 2;
                    WritePixel(dst, ox, oy, outW, src, e0);
                    WritePixel(dst, ox + 1, oy, outW, src, e1);
                    WritePixel(dst, ox, oy + 1, outW, src, e2);
                    WritePixel(dst, ox + 1, oy + 1, outW, src, e3);
                }
            }

            return new DecodedImage(outW, outH, dst);
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

        /// Fully transparent pixels compare equal regardless of RGB (hidden patch color).
        static bool Equal(byte[] rgba, int a, int b)
        {
            if (rgba[a + 3] == 0 && rgba[b + 3] == 0)
                return true;
            return rgba[a] == rgba[b]
                && rgba[a + 1] == rgba[b + 1]
                && rgba[a + 2] == rgba[b + 2]
                && rgba[a + 3] == rgba[b + 3];
        }

        static void WritePixel(byte[] dst, int x, int y, int w, byte[] src, int srcOffset)
        {
            int di = (y * w + x) * 4;
            dst[di] = src[srcOffset];
            dst[di + 1] = src[srcOffset + 1];
            dst[di + 2] = src[srcOffset + 2];
            dst[di + 3] = src[srcOffset + 3];
        }
    }
}
