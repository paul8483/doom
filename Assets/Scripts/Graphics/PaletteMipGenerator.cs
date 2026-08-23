using System;
using System.Collections.Generic;

namespace Doom.Graphics
{
    /// Immutable CPU mip chain. Level zero is the supplied image.
    public sealed class PaletteMipChain
    {
        readonly DecodedImage[] levels;

        public PaletteMipChain(DecodedImage[] levels)
        {
            if (levels == null || levels.Length == 0)
                throw new ArgumentException("Mip chain requires at least level zero.", nameof(levels));
            this.levels = levels;
        }

        public int Count => levels.Length;
        public DecodedImage this[int level] => levels[level];
        public IReadOnlyList<DecodedImage> Levels => levels;
    }

    /// Builds deterministic PLAYPAL-quantized mip levels without Unity box filtering.
    public static class PaletteMipGenerator
    {
        const int AlphaCutoff = 128;

        public static PaletteMipChain Generate(
            DecodedImage levelZero,
            Palette palette,
            PixelWrapMode wrap,
            bool preserveAlphaCoverage = true,
            bool quantizeToPalette = true)
        {
            Validate(levelZero, quantizeToPalette ? palette : null, quantizeToPalette);

            byte[] paletteRgb = null;
            double[] paletteLinear = null;
            if (quantizeToPalette)
            {
                paletteRgb = new byte[256 * 3];
                paletteLinear = new double[256 * 3];
                for (int i = 0; i < 256; i++)
                {
                    palette.GetColor(i, out byte r, out byte g, out byte b);
                    int p = i * 3;
                    paletteRgb[p] = r;
                    paletteRgb[p + 1] = g;
                    paletteRgb[p + 2] = b;
                    paletteLinear[p] = SrgbToLinear(r);
                    paletteLinear[p + 1] = SrgbToLinear(g);
                    paletteLinear[p + 2] = SrgbToLinear(b);
                }
            }

            var levels = new List<DecodedImage> { levelZero };
            var current = levelZero;
            while (current.Width > 1 || current.Height > 1)
            {
                int width = Math.Max(1, current.Width >> 1);
                int height = Math.Max(1, current.Height >> 1);
                var rgba = new byte[checked(width * height * 4)];
                Downsample(current, rgba, width, height, wrap, paletteRgb, paletteLinear);
                if (preserveAlphaCoverage)
                    PreserveCoverage(current.Rgba, rgba);
                current = new DecodedImage(width, height, rgba);
                levels.Add(current);
            }

            return new PaletteMipChain(levels.ToArray());
        }

        static void Validate(DecodedImage image, Palette palette, bool requirePalette = true)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (requirePalette && palette == null) throw new ArgumentNullException(nameof(palette));
            if (image.Width <= 0 || image.Height <= 0)
                throw new ArgumentOutOfRangeException(nameof(image), "Image dimensions must be positive.");
            long expected = (long)image.Width * image.Height * 4L;
            if (image.Rgba == null || image.Rgba.Length != expected)
                throw new ArgumentException("Image RGBA buffer does not match its dimensions.", nameof(image));
        }

        static void Downsample(
            DecodedImage source,
            byte[] destination,
            int width,
            int height,
            PixelWrapMode wrap,
            byte[] paletteRgb,
            double[] paletteLinear)
        {
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                double sumR = 0, sumG = 0, sumB = 0, sumAlpha = 0;
                int alphaTotal = 0;

                for (int oy = 0; oy < 2; oy++)
                for (int ox = 0; ox < 2; ox++)
                {
                    int sx = SampleX(x * 2 + ox, source.Width, wrap);
                    int sy = SampleY(y * 2 + oy, source.Height, wrap);
                    int si = (sy * source.Width + sx) * 4;
                    int alpha = source.Rgba[si + 3];
                    alphaTotal += alpha;
                    if (alpha == 0) continue;

                    double weight = alpha / 255.0;
                    sumR += SrgbToLinear(source.Rgba[si]) * weight;
                    sumG += SrgbToLinear(source.Rgba[si + 1]) * weight;
                    sumB += SrgbToLinear(source.Rgba[si + 2]) * weight;
                    sumAlpha += weight;
                }

                int di = (y * width + x) * 4;
                byte alphaOut = (byte)((alphaTotal + 2) / 4);
                if (sumAlpha <= 0 || alphaOut == 0)
                {
                    destination[di] = 0;
                    destination[di + 1] = 0;
                    destination[di + 2] = 0;
                    destination[di + 3] = 0;
                    continue;
                }

                if (paletteLinear == null)
                {
                    destination[di] = LinearToSrgb(sumR / sumAlpha);
                    destination[di + 1] = LinearToSrgb(sumG / sumAlpha);
                    destination[di + 2] = LinearToSrgb(sumB / sumAlpha);
                    destination[di + 3] = alphaOut;
                    continue;
                }

                int nearest = NearestPalette(
                    sumR / sumAlpha, sumG / sumAlpha, sumB / sumAlpha, paletteLinear);
                int pi = nearest * 3;
                destination[di] = paletteRgb[pi];
                destination[di + 1] = paletteRgb[pi + 1];
                destination[di + 2] = paletteRgb[pi + 2];
                destination[di + 3] = alphaOut;
            }
        }

        static int NearestPalette(double r, double g, double b, double[] paletteLinear)
        {
            int best = 0;
            double bestDistance = double.MaxValue;
            for (int i = 0; i < 256; i++)
            {
                int p = i * 3;
                double dr = r - paletteLinear[p];
                double dg = g - paletteLinear[p + 1];
                double db = b - paletteLinear[p + 2];
                // Green/luminance differences are most visible in DOOM's palette.
                double distance = dr * dr * 0.299 + dg * dg * 0.587 + db * db * 0.114;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }
            return best;
        }

        static double SrgbToLinear(byte value)
        {
            double c = value / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        static byte LinearToSrgb(double linear)
        {
            double c = linear <= 0.0031308
                ? linear * 12.92
                : 1.055 * Math.Pow(linear, 1.0 / 2.4) - 0.055;
            int v = (int)Math.Round(c * 255.0, MidpointRounding.AwayFromZero);
            return (byte)Math.Max(0, Math.Min(255, v));
        }

        static int SampleX(int x, int width, PixelWrapMode wrap)
        {
            if (x < width) return x;
            if (wrap == PixelWrapMode.RepeatX || wrap == PixelWrapMode.RepeatXY)
                return x % width;
            return width - 1;
        }

        static int SampleY(int y, int height, PixelWrapMode wrap)
        {
            if (y < height) return y;
            if (wrap == PixelWrapMode.RepeatXY)
                return y % height;
            return height - 1;
        }

        static void PreserveCoverage(byte[] source, byte[] destination)
        {
            int sourcePixels = source.Length / 4;
            int destinationPixels = destination.Length / 4;
            int covered = 0;
            for (int i = 3; i < source.Length; i += 4)
                if (source[i] >= AlphaCutoff) covered++;

            int target = (int)Math.Round(
                covered * (double)destinationPixels / sourcePixels,
                MidpointRounding.AwayFromZero);
            if (target <= 0)
                return;

            var candidates = new List<int>();
            for (int i = 3; i < destination.Length; i += 4)
            {
                if (destination[i] > 0)
                    candidates.Add(i);
            }
            target = Math.Min(target, candidates.Count);
            candidates.Sort((a, b) =>
            {
                int alphaOrder = destination[b].CompareTo(destination[a]);
                return alphaOrder != 0 ? alphaOrder : a.CompareTo(b);
            });

            for (int rank = 0; rank < candidates.Count; rank++)
            {
                int index = candidates[rank];
                destination[index] = rank < target
                    ? (byte)Math.Max(AlphaCutoff, (int)destination[index])
                    : (byte)Math.Min(AlphaCutoff - 1, (int)destination[index]);
            }
        }
    }
}
