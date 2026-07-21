// ******* Super XBR Scaler *******
//
// Copyright (c) 2016 Hyllian - sergiogdb@gmail.com
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
// C# port for Doom.Graphics: deterministic Super-xBR 2× (three internal passes)
// over DecodedImage RGBA32 with PixelWrapMode border sampling. Weights and
// pass structure match Hyllian's MIT reference (pastebin cbH8ZQQT).

using System;

namespace Doom.Graphics
{
    /// Deterministic Super-xBR 2× upscale (Hyllian, MIT). Apply twice for 4×.
    public static class SuperXbrUpscaler
    {
        const double Wgt1 = 0.129633;
        const double Wgt2 = 0.175068;
        const double W1 = -Wgt1;
        const double W2 = Wgt1 + 0.5;
        const double W3 = -Wgt2;
        const double W4 = Wgt2 + 0.5;

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

            int w = source.Width;
            int h = source.Height;
            var src = source.Rgba;
            var dst = new byte[outLen];

            // Working buffers reused across all three passes (no per-pixel alloc).
            var r = new double[4, 4];
            var g = new double[4, 4];
            var b = new double[4, 4];
            var a = new double[4, 4];
            var yLuma = new double[4, 4];
            var wp = new double[6];

            // Pass 1 — seed even/odd lattice from source; fill one diagonal sample.
            wp[0] = 2.0; wp[1] = 1.0; wp[2] = -1.0; wp[3] = 4.0; wp[4] = -1.0; wp[5] = 1.0;
            for (int y = 0; y < outH; y += 2)
            {
                for (int x = 0; x < outW; x += 2)
                {
                    int cx = x / 2;
                    int cy = y / 2;

                    for (int sx = -1; sx <= 2; sx++)
                    for (int sy = -1; sy <= 2; sy++)
                    {
                        int csx = SampleX(cx + sx, w, wrap);
                        int csy = SampleY(cy + sy, h, wrap);
                        int si = (csy * w + csx) * 4;
                        int ix = sx + 1;
                        int iy = sy + 1;
                        r[ix, iy] = src[si];
                        g[ix, iy] = src[si + 1];
                        b[ix, iy] = src[si + 2];
                        a[ix, iy] = src[si + 3];
                        yLuma[ix, iy] = 0.2126 * r[ix, iy] + 0.7152 * g[ix, iy] + 0.0722 * b[ix, iy];
                    }

                    GetAntiRingRange(r, g, b, a,
                        out double minR, out double maxR,
                        out double minG, out double maxG,
                        out double minB, out double maxB,
                        out double minA, out double maxA);

                    double dEdge = DiagonalEdge(yLuma, wp);
                    double rf, gf, bf, af;
                    if (dEdge <= 0)
                    {
                        rf = W1 * (r[0, 3] + r[3, 0]) + W2 * (r[1, 2] + r[2, 1]);
                        gf = W1 * (g[0, 3] + g[3, 0]) + W2 * (g[1, 2] + g[2, 1]);
                        bf = W1 * (b[0, 3] + b[3, 0]) + W2 * (b[1, 2] + b[2, 1]);
                        af = W1 * (a[0, 3] + a[3, 0]) + W2 * (a[1, 2] + a[2, 1]);
                    }
                    else
                    {
                        rf = W1 * (r[0, 0] + r[3, 3]) + W2 * (r[1, 1] + r[2, 2]);
                        gf = W1 * (g[0, 0] + g[3, 3]) + W2 * (g[1, 1] + g[2, 2]);
                        bf = W1 * (b[0, 0] + b[3, 3]) + W2 * (b[1, 1] + b[2, 2]);
                        af = W1 * (a[0, 0] + a[3, 3]) + W2 * (a[1, 1] + a[2, 2]);
                    }

                    ClampChannels(ref rf, ref gf, ref bf, ref af,
                        minR, maxR, minG, maxG, minB, maxB, minA, maxA);

                    int siCenter = (cy * w + cx) * 4;
                    WritePixel(dst, x, y, outW, src, siCenter);
                    WritePixel(dst, x + 1, y, outW, src, siCenter);
                    WritePixel(dst, x, y + 1, outW, src, siCenter);
                    WriteBytePixel(dst, x + 1, y + 1, outW,
                        ToByte(rf), ToByte(gf), ToByte(bf), ToByte(af));
                }
            }

            // Pass 2 — fill the remaining two edge samples of each 2×2 block.
            wp[0] = 2.0; wp[1] = 0.0; wp[2] = 0.0; wp[3] = 0.0; wp[4] = 0.0; wp[5] = 0.0;
            for (int y = 0; y < outH; y += 2)
            {
                for (int x = 0; x < outW; x += 2)
                {
                    for (int sx = -1; sx <= 2; sx++)
                    for (int sy = -1; sy <= 2; sy++)
                    {
                        int csx = SampleX(sx + sy + x, outW, wrap);
                        int csy = SampleY(sx - sy + y, outH, wrap);
                        int si = (csy * outW + csx) * 4;
                        int ix = sx + 1;
                        int iy = sy + 1;
                        r[ix, iy] = dst[si];
                        g[ix, iy] = dst[si + 1];
                        b[ix, iy] = dst[si + 2];
                        a[ix, iy] = dst[si + 3];
                        yLuma[ix, iy] = 0.2126 * r[ix, iy] + 0.7152 * g[ix, iy] + 0.0722 * b[ix, iy];
                    }

                    GetAntiRingRange(r, g, b, a,
                        out double minR, out double maxR,
                        out double minG, out double maxG,
                        out double minB, out double maxB,
                        out double minA, out double maxA);

                    double dEdge = DiagonalEdge(yLuma, wp);
                    double rf, gf, bf, af;
                    if (dEdge <= 0)
                    {
                        rf = W3 * (r[0, 3] + r[3, 0]) + W4 * (r[1, 2] + r[2, 1]);
                        gf = W3 * (g[0, 3] + g[3, 0]) + W4 * (g[1, 2] + g[2, 1]);
                        bf = W3 * (b[0, 3] + b[3, 0]) + W4 * (b[1, 2] + b[2, 1]);
                        af = W3 * (a[0, 3] + a[3, 0]) + W4 * (a[1, 2] + a[2, 1]);
                    }
                    else
                    {
                        rf = W3 * (r[0, 0] + r[3, 3]) + W4 * (r[1, 1] + r[2, 2]);
                        gf = W3 * (g[0, 0] + g[3, 3]) + W4 * (g[1, 1] + g[2, 2]);
                        bf = W3 * (b[0, 0] + b[3, 3]) + W4 * (b[1, 1] + b[2, 2]);
                        af = W3 * (a[0, 0] + a[3, 3]) + W4 * (a[1, 1] + a[2, 2]);
                    }

                    ClampChannels(ref rf, ref gf, ref bf, ref af,
                        minR, maxR, minG, maxG, minB, maxB, minA, maxA);
                    WriteBytePixel(dst, x + 1, y, outW,
                        ToByte(rf), ToByte(gf), ToByte(bf), ToByte(af));

                    for (int sx = -1; sx <= 2; sx++)
                    for (int sy = -1; sy <= 2; sy++)
                    {
                        int csx = SampleX(sx + sy - 1 + x, outW, wrap);
                        int csy = SampleY(sx - sy + 1 + y, outH, wrap);
                        int si = (csy * outW + csx) * 4;
                        int ix = sx + 1;
                        int iy = sy + 1;
                        r[ix, iy] = dst[si];
                        g[ix, iy] = dst[si + 1];
                        b[ix, iy] = dst[si + 2];
                        a[ix, iy] = dst[si + 3];
                        yLuma[ix, iy] = 0.2126 * r[ix, iy] + 0.7152 * g[ix, iy] + 0.0722 * b[ix, iy];
                    }

                    dEdge = DiagonalEdge(yLuma, wp);
                    if (dEdge <= 0)
                    {
                        rf = W3 * (r[0, 3] + r[3, 0]) + W4 * (r[1, 2] + r[2, 1]);
                        gf = W3 * (g[0, 3] + g[3, 0]) + W4 * (g[1, 2] + g[2, 1]);
                        bf = W3 * (b[0, 3] + b[3, 0]) + W4 * (b[1, 2] + b[2, 1]);
                        af = W3 * (a[0, 3] + a[3, 0]) + W4 * (a[1, 2] + a[2, 1]);
                    }
                    else
                    {
                        rf = W3 * (r[0, 0] + r[3, 3]) + W4 * (r[1, 1] + r[2, 2]);
                        gf = W3 * (g[0, 0] + g[3, 3]) + W4 * (g[1, 1] + g[2, 2]);
                        bf = W3 * (b[0, 0] + b[3, 3]) + W4 * (b[1, 1] + b[2, 2]);
                        af = W3 * (a[0, 0] + a[3, 3]) + W4 * (a[1, 1] + a[2, 2]);
                    }

                    // Anti-ring range intentionally reused from the first half of
                    // this 2×2 block (matches Hyllian reference).
                    ClampChannels(ref rf, ref gf, ref bf, ref af,
                        minR, maxR, minG, maxG, minB, maxB, minA, maxA);
                    WriteBytePixel(dst, x, y + 1, outW,
                        ToByte(rf), ToByte(gf), ToByte(bf), ToByte(af));
                }
            }

            // Pass 3 — smooth every output pixel (reverse scan, as in reference).
            wp[0] = 2.0; wp[1] = 1.0; wp[2] = -1.0; wp[3] = 4.0; wp[4] = -1.0; wp[5] = 1.0;
            for (int y = outH - 1; y >= 0; y--)
            {
                for (int x = outW - 1; x >= 0; x--)
                {
                    for (int sx = -2; sx <= 1; sx++)
                    for (int sy = -2; sy <= 1; sy++)
                    {
                        int csx = SampleX(sx + x, outW, wrap);
                        int csy = SampleY(sy + y, outH, wrap);
                        int si = (csy * outW + csx) * 4;
                        int ix = sx + 2;
                        int iy = sy + 2;
                        r[ix, iy] = dst[si];
                        g[ix, iy] = dst[si + 1];
                        b[ix, iy] = dst[si + 2];
                        a[ix, iy] = dst[si + 3];
                        yLuma[ix, iy] = 0.2126 * r[ix, iy] + 0.7152 * g[ix, iy] + 0.0722 * b[ix, iy];
                    }

                    GetAntiRingRange(r, g, b, a,
                        out double minR, out double maxR,
                        out double minG, out double maxG,
                        out double minB, out double maxB,
                        out double minA, out double maxA);

                    double dEdge = DiagonalEdge(yLuma, wp);
                    double rf, gf, bf, af;
                    if (dEdge <= 0)
                    {
                        rf = W1 * (r[0, 3] + r[3, 0]) + W2 * (r[1, 2] + r[2, 1]);
                        gf = W1 * (g[0, 3] + g[3, 0]) + W2 * (g[1, 2] + g[2, 1]);
                        bf = W1 * (b[0, 3] + b[3, 0]) + W2 * (b[1, 2] + b[2, 1]);
                        af = W1 * (a[0, 3] + a[3, 0]) + W2 * (a[1, 2] + a[2, 1]);
                    }
                    else
                    {
                        rf = W1 * (r[0, 0] + r[3, 3]) + W2 * (r[1, 1] + r[2, 2]);
                        gf = W1 * (g[0, 0] + g[3, 3]) + W2 * (g[1, 1] + g[2, 2]);
                        bf = W1 * (b[0, 0] + b[3, 3]) + W2 * (b[1, 1] + b[2, 2]);
                        af = W1 * (a[0, 0] + a[3, 3]) + W2 * (a[1, 1] + a[2, 2]);
                    }

                    ClampChannels(ref rf, ref gf, ref bf, ref af,
                        minR, maxR, minG, maxG, minB, maxB, minA, maxA);
                    WriteBytePixel(dst, x, y, outW,
                        ToByte(rf), ToByte(gf), ToByte(bf), ToByte(af));
                }
            }

            return new DecodedImage(outW, outH, dst);
        }

        static double DiagonalEdge(double[,] mat, double[] wp)
        {
            double dw1 =
                wp[0] * (Df(mat[0, 2], mat[1, 1]) + Df(mat[1, 1], mat[2, 0])
                         + Df(mat[1, 3], mat[2, 2]) + Df(mat[2, 2], mat[3, 1]))
                + wp[1] * (Df(mat[0, 3], mat[1, 2]) + Df(mat[2, 1], mat[3, 0]))
                + wp[2] * (Df(mat[0, 3], mat[2, 1]) + Df(mat[1, 2], mat[3, 0]))
                + wp[3] * Df(mat[1, 2], mat[2, 1])
                + wp[4] * (Df(mat[0, 2], mat[2, 0]) + Df(mat[1, 3], mat[3, 1]))
                + wp[5] * (Df(mat[0, 1], mat[1, 0]) + Df(mat[2, 3], mat[3, 2]));

            double dw2 =
                wp[0] * (Df(mat[0, 1], mat[1, 2]) + Df(mat[1, 2], mat[2, 3])
                         + Df(mat[1, 0], mat[2, 1]) + Df(mat[2, 1], mat[3, 2]))
                + wp[1] * (Df(mat[0, 0], mat[1, 1]) + Df(mat[2, 2], mat[3, 3]))
                + wp[2] * (Df(mat[0, 0], mat[2, 2]) + Df(mat[1, 1], mat[3, 3]))
                + wp[3] * Df(mat[1, 1], mat[2, 2])
                + wp[4] * (Df(mat[1, 0], mat[3, 2]) + Df(mat[0, 1], mat[2, 3]))
                + wp[5] * (Df(mat[0, 2], mat[1, 3]) + Df(mat[2, 0], mat[3, 1]));

            return dw1 - dw2;
        }

        static double Df(double a, double b)
        {
            double d = a - b;
            return d < 0 ? -d : d;
        }

        static void GetAntiRingRange(
            double[,] r, double[,] g, double[,] b, double[,] a,
            out double minR, out double maxR,
            out double minG, out double maxG,
            out double minB, out double maxB,
            out double minA, out double maxA)
        {
            minR = Min4(r[1, 1], r[2, 1], r[1, 2], r[2, 2]);
            maxR = Max4(r[1, 1], r[2, 1], r[1, 2], r[2, 2]);
            minG = Min4(g[1, 1], g[2, 1], g[1, 2], g[2, 2]);
            maxG = Max4(g[1, 1], g[2, 1], g[1, 2], g[2, 2]);
            minB = Min4(b[1, 1], b[2, 1], b[1, 2], b[2, 2]);
            maxB = Max4(b[1, 1], b[2, 1], b[1, 2], b[2, 2]);
            minA = Min4(a[1, 1], a[2, 1], a[1, 2], a[2, 2]);
            maxA = Max4(a[1, 1], a[2, 1], a[1, 2], a[2, 2]);
        }

        static void ClampChannels(
            ref double rf, ref double gf, ref double bf, ref double af,
            double minR, double maxR, double minG, double maxG,
            double minB, double maxB, double minA, double maxA)
        {
            rf = Clamp(rf, minR, maxR);
            gf = Clamp(gf, minG, maxG);
            bf = Clamp(bf, minB, maxB);
            af = Clamp(af, minA, maxA);
        }

        static double Clamp(double x, double floor, double ceil)
        {
            if (x < floor) return floor;
            if (x > ceil) return ceil;
            return x;
        }

        static double Min4(double a, double b, double c, double d)
        {
            double m = a;
            if (b < m) m = b;
            if (c < m) m = c;
            if (d < m) m = d;
            return m;
        }

        static double Max4(double a, double b, double c, double d)
        {
            double m = a;
            if (b > m) m = b;
            if (c > m) m = c;
            if (d > m) m = d;
            return m;
        }

        static byte ToByte(double v) => (byte)Clamp(Math.Ceiling(v), 0, 255);

        static int SampleX(int x, int w, PixelWrapMode wrap)
        {
            switch (wrap)
            {
                case PixelWrapMode.RepeatX:
                case PixelWrapMode.RepeatXY:
                    x %= w;
                    if (x < 0) x += w;
                    return x;
                default:
                    if (x < 0) return 0;
                    if (x >= w) return w - 1;
                    return x;
            }
        }

        static int SampleY(int y, int h, PixelWrapMode wrap)
        {
            if (wrap == PixelWrapMode.RepeatXY)
            {
                y %= h;
                if (y < 0) y += h;
                return y;
            }

            if (y < 0) return 0;
            if (y >= h) return h - 1;
            return y;
        }

        static void WritePixel(byte[] dst, int x, int y, int w, byte[] src, int srcOffset)
        {
            int di = (y * w + x) * 4;
            dst[di] = src[srcOffset];
            dst[di + 1] = src[srcOffset + 1];
            dst[di + 2] = src[srcOffset + 2];
            dst[di + 3] = src[srcOffset + 3];
        }

        static void WriteBytePixel(byte[] dst, int x, int y, int w, byte r, byte g, byte b, byte a)
        {
            int di = (y * w + x) * 4;
            dst[di] = r;
            dst[di + 1] = g;
            dst[di + 2] = b;
            dst[di + 3] = a;
        }
    }
}
