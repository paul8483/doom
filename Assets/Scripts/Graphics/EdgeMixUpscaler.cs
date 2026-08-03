using System;

namespace Doom.Graphics
{
    /// <summary>
    /// Pickup-only 8× magnifier. Each source texel keeps a 4×4 center while a
    /// four-pixel band (two output pixels per side) across every source boundary
    /// is filled with the arithmetic mean of the adjacent texels. Intersections
    /// average four texels. RGB is averaged in premultiplied-alpha space.
    /// </summary>
    public static class EdgeMixUpscaler
    {
        public const int Scale = 8;
        const int EdgeWidth = 2;

        /// Contrast-gate ramp accepted by the stage-2 Gate 0 interactive verdict
        /// (2026-08-03, aggressive point): full mix at weighted RGB distance <= 16,
        /// hard nearest edge at >= 64.
        public const int GateRampStart = 16;
        public const int GateRampEnd = 64;

        /// Runtime Enhanced path: contrast-gated EdgeMix with the accepted ramp.
        public static DecodedImage Scale8XContrastGated(DecodedImage source) =>
            Scale8XGated(source, GateRampStart, GateRampEnd);

        public static DecodedImage Scale8X(DecodedImage source)
        {
            Validate(source);

            int sourceWidth = source.Width;
            int sourceHeight = source.Height;
            int targetWidth = checked(sourceWidth * Scale);
            int targetHeight = checked(sourceHeight * Scale);
            var output = new byte[checked(targetWidth * targetHeight * 4)];
            byte[] input = source.Rgba;

            for (int targetY = 0; targetY < targetHeight; targetY++)
            {
                int sourceY = targetY / Scale;
                int subY = targetY % Scale;
                int otherY = sourceY;
                bool blendY = false;
                if (subY < EdgeWidth && sourceY > 0)
                {
                    otherY = sourceY - 1;
                    blendY = true;
                }
                else if (subY >= Scale - EdgeWidth && sourceY + 1 < sourceHeight)
                {
                    otherY = sourceY + 1;
                    blendY = true;
                }

                for (int targetX = 0; targetX < targetWidth; targetX++)
                {
                    int sourceX = targetX / Scale;
                    int subX = targetX % Scale;
                    int otherX = sourceX;
                    bool blendX = false;
                    if (subX < EdgeWidth && sourceX > 0)
                    {
                        otherX = sourceX - 1;
                        blendX = true;
                    }
                    else if (subX >= Scale - EdgeWidth && sourceX + 1 < sourceWidth)
                    {
                        otherX = sourceX + 1;
                        blendX = true;
                    }

                    int count = (blendX ? 2 : 1) * (blendY ? 2 : 1);
                    int alphaSum = 0;
                    int premulRed = 0;
                    int premulGreen = 0;
                    int premulBlue = 0;

                    Accumulate(input, sourceWidth, sourceX, sourceY,
                        ref alphaSum, ref premulRed, ref premulGreen, ref premulBlue);
                    if (blendX)
                        Accumulate(input, sourceWidth, otherX, sourceY,
                            ref alphaSum, ref premulRed, ref premulGreen, ref premulBlue);
                    if (blendY)
                        Accumulate(input, sourceWidth, sourceX, otherY,
                            ref alphaSum, ref premulRed, ref premulGreen, ref premulBlue);
                    if (blendX && blendY)
                        Accumulate(input, sourceWidth, otherX, otherY,
                            ref alphaSum, ref premulRed, ref premulGreen, ref premulBlue);

                    int destination = (targetY * targetWidth + targetX) * 4;
                    output[destination + 3] = (byte)((alphaSum + count / 2) / count);
                    if (alphaSum == 0)
                        continue;

                    output[destination] = (byte)((premulRed + alphaSum / 2) / alphaSum);
                    output[destination + 1] = (byte)((premulGreen + alphaSum / 2) / alphaSum);
                    output[destination + 2] = (byte)((premulBlue + alphaSum / 2) / alphaSum);
                }
            }

            return new DecodedImage(targetWidth, targetHeight, output);
        }

        /// <summary>
        /// Contrast-gated variant (stage-2 Gate 0). Opaque-opaque boundaries mix
        /// with a weight that ramps from full (weighted RGB distance &lt;= rampStart)
        /// to none (distance &gt;= rampEnd), so intentional high-contrast detail keeps
        /// hard nearest edges. Alpha/silhouette boundaries always mix fully — the
        /// accepted premultiplied silhouette band is preserved. Corner contributions
        /// are gated by the weakest participating pair.
        /// </summary>
        public static DecodedImage Scale8XGated(DecodedImage source, int rampStart, int rampEnd)
        {
            Validate(source);
            if (rampStart < 0)
                throw new ArgumentOutOfRangeException(nameof(rampStart), "Ramp start must be non-negative.");
            if (rampEnd < rampStart)
                throw new ArgumentOutOfRangeException(nameof(rampEnd), "Ramp end must be >= ramp start.");

            int sourceWidth = source.Width;
            int sourceHeight = source.Height;
            int targetWidth = checked(sourceWidth * Scale);
            int targetHeight = checked(sourceHeight * Scale);
            var output = new byte[checked(targetWidth * targetHeight * 4)];
            byte[] input = source.Rgba;

            for (int targetY = 0; targetY < targetHeight; targetY++)
            {
                int sourceY = targetY / Scale;
                int subY = targetY % Scale;
                int otherY = sourceY;
                bool blendY = false;
                if (subY < EdgeWidth && sourceY > 0)
                {
                    otherY = sourceY - 1;
                    blendY = true;
                }
                else if (subY >= Scale - EdgeWidth && sourceY + 1 < sourceHeight)
                {
                    otherY = sourceY + 1;
                    blendY = true;
                }

                for (int targetX = 0; targetX < targetWidth; targetX++)
                {
                    int sourceX = targetX / Scale;
                    int subX = targetX % Scale;
                    int otherX = sourceX;
                    bool blendX = false;
                    if (subX < EdgeWidth && sourceX > 0)
                    {
                        otherX = sourceX - 1;
                        blendX = true;
                    }
                    else if (subX >= Scale - EdgeWidth && sourceX + 1 < sourceWidth)
                    {
                        otherX = sourceX + 1;
                        blendX = true;
                    }

                    int weightX = blendX
                        ? GateWeight(input, sourceWidth, sourceX, sourceY, otherX, sourceY, rampStart, rampEnd)
                        : 0;
                    int weightY = blendY
                        ? GateWeight(input, sourceWidth, sourceX, sourceY, sourceX, otherY, rampStart, rampEnd)
                        : 0;

                    long weightSum = 0;
                    long alphaSum = 0;
                    long premulRed = 0;
                    long premulGreen = 0;
                    long premulBlue = 0;

                    AccumulateWeighted(input, sourceWidth, sourceX, sourceY, WeightOne,
                        ref weightSum, ref alphaSum, ref premulRed, ref premulGreen, ref premulBlue);
                    if (weightX > 0)
                        AccumulateWeighted(input, sourceWidth, otherX, sourceY, weightX,
                            ref weightSum, ref alphaSum, ref premulRed, ref premulGreen, ref premulBlue);
                    if (weightY > 0)
                        AccumulateWeighted(input, sourceWidth, sourceX, otherY, weightY,
                            ref weightSum, ref alphaSum, ref premulRed, ref premulGreen, ref premulBlue);
                    if (weightX > 0 && weightY > 0)
                    {
                        int weightDiagonal = Math.Min(
                            Math.Min(weightX, weightY),
                            GateWeight(input, sourceWidth, sourceX, sourceY, otherX, otherY, rampStart, rampEnd));
                        if (weightDiagonal > 0)
                            AccumulateWeighted(input, sourceWidth, otherX, otherY, weightDiagonal,
                                ref weightSum, ref alphaSum, ref premulRed, ref premulGreen, ref premulBlue);
                    }

                    int destination = (targetY * targetWidth + targetX) * 4;
                    output[destination + 3] = (byte)((alphaSum + weightSum / 2) / weightSum);
                    if (alphaSum == 0)
                        continue;

                    output[destination] = (byte)((premulRed + alphaSum / 2) / alphaSum);
                    output[destination + 1] = (byte)((premulGreen + alphaSum / 2) / alphaSum);
                    output[destination + 2] = (byte)((premulBlue + alphaSum / 2) / alphaSum);
                }
            }

            return new DecodedImage(targetWidth, targetHeight, output);
        }

        const int WeightOne = 256;

        /// Full weight across alpha transitions (silhouette band stays); between two
        /// fully opaque texels the weight ramps down over weighted RGB distance
        /// (0.30/0.59/0.11 — same perceptual weights as DeditherFilter).
        static int GateWeight(
            byte[] rgba, int width, int x0, int y0, int x1, int y1, int rampStart, int rampEnd)
        {
            int offset0 = (y0 * width + x0) * 4;
            int offset1 = (y1 * width + x1) * 4;
            if (rgba[offset0 + 3] != 255 || rgba[offset1 + 3] != 255)
                return WeightOne;

            int distance = (30 * Math.Abs(rgba[offset0] - rgba[offset1])
                + 59 * Math.Abs(rgba[offset0 + 1] - rgba[offset1 + 1])
                + 11 * Math.Abs(rgba[offset0 + 2] - rgba[offset1 + 2])) / 100;
            if (distance <= rampStart)
                return WeightOne;
            if (distance >= rampEnd)
                return 0;
            return WeightOne * (rampEnd - distance) / (rampEnd - rampStart);
        }

        static void AccumulateWeighted(
            byte[] rgba,
            int width,
            int x,
            int y,
            int weight,
            ref long weightSum,
            ref long alphaSum,
            ref long premulRed,
            ref long premulGreen,
            ref long premulBlue)
        {
            int offset = (y * width + x) * 4;
            long alphaWeighted = (long)rgba[offset + 3] * weight;
            weightSum += weight;
            alphaSum += alphaWeighted;
            premulRed += rgba[offset] * alphaWeighted;
            premulGreen += rgba[offset + 1] * alphaWeighted;
            premulBlue += rgba[offset + 2] * alphaWeighted;
        }

        static void Accumulate(
            byte[] rgba,
            int width,
            int x,
            int y,
            ref int alphaSum,
            ref int premulRed,
            ref int premulGreen,
            ref int premulBlue)
        {
            int offset = (y * width + x) * 4;
            int alpha = rgba[offset + 3];
            alphaSum += alpha;
            premulRed += rgba[offset] * alpha;
            premulGreen += rgba[offset + 1] * alpha;
            premulBlue += rgba[offset + 2] * alpha;
        }

        static void Validate(DecodedImage source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (source.Width <= 0)
                throw new ArgumentOutOfRangeException(nameof(source), "Width must be positive.");
            if (source.Height <= 0)
                throw new ArgumentOutOfRangeException(nameof(source), "Height must be positive.");
            if (source.Rgba == null ||
                source.Rgba.Length != checked(source.Width * source.Height * 4))
                throw new ArgumentException("RGBA length does not match image dimensions.", nameof(source));
        }
    }
}
