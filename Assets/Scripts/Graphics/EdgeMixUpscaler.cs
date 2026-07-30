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
