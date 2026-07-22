using System;

namespace Doom.Graphics
{
    /// Wrap mode for height-map sampling when building normals.
    public enum NormalWrapMode
    {
        Repeat = 0,
        Clamp = 1,
    }

    /// Named material categories with fixed strength/roughness/emission policy.
    public enum MaterialSurfaceCategory
    {
        Unknown = 0,
        Wall = 1,
        Flat = 2,
        Metal = 3,
        Fluid = 4,
    }

    /// Immutable surface parameters for Enhanced materials.
    public readonly struct MaterialSurfaceProfile : IEquatable<MaterialSurfaceProfile>
    {
        public readonly float Strength;
        public readonly float Roughness;
        public readonly float Emission;
        public readonly NormalWrapMode Wrap;
        /// Weight of fine (unblurred) luminance in multi-scale height.
        public readonly float HeightFineWeight;
        /// Weight of coarse (blurred) luminance in multi-scale height.
        public readonly float HeightCoarseWeight;
        /// POM amplitude in UV units; 0 disables parallax for this category.
        public readonly float ParallaxAmplitude;

        public MaterialSurfaceProfile(
            float strength, float roughness, float emission, NormalWrapMode wrap,
            float heightFineWeight = 0.4f, float heightCoarseWeight = 0.6f,
            float parallaxAmplitude = 0f)
        {
            Strength = strength;
            Roughness = roughness;
            Emission = emission;
            Wrap = wrap;
            HeightFineWeight = heightFineWeight;
            HeightCoarseWeight = heightCoarseWeight;
            ParallaxAmplitude = parallaxAmplitude;
        }

        /// Unknown receives a weak neutral profile (safe fallback).
        public static MaterialSurfaceProfile For(MaterialSurfaceCategory category) =>
            category switch
            {
                // Conservative parallax: relief, not stereogram. Fluid/Unknown = 0.
                MaterialSurfaceCategory.Wall =>
                    new MaterialSurfaceProfile(2.0f, 0.72f, 0f, NormalWrapMode.Repeat,
                        heightFineWeight: 0.40f, heightCoarseWeight: 0.60f,
                        parallaxAmplitude: 0.028f),
                MaterialSurfaceCategory.Flat =>
                    new MaterialSurfaceProfile(1.2f, 0.88f, 0f, NormalWrapMode.Repeat,
                        heightFineWeight: 0.30f, heightCoarseWeight: 0.70f,
                        parallaxAmplitude: 0.018f),
                MaterialSurfaceCategory.Metal =>
                    new MaterialSurfaceProfile(2.5f, 0.35f, 0f, NormalWrapMode.Repeat,
                        heightFineWeight: 0.55f, heightCoarseWeight: 0.45f,
                        parallaxAmplitude: 0.032f),
                MaterialSurfaceCategory.Fluid =>
                    new MaterialSurfaceProfile(0.6f, 0.95f, 0.15f, NormalWrapMode.Repeat,
                        heightFineWeight: 0.25f, heightCoarseWeight: 0.75f,
                        parallaxAmplitude: 0f),
                _ =>
                    new MaterialSurfaceProfile(0.75f, 0.85f, 0f, NormalWrapMode.Repeat,
                        heightFineWeight: 0.40f, heightCoarseWeight: 0.60f,
                        parallaxAmplitude: 0f),
            };

        public bool Equals(MaterialSurfaceProfile other) =>
            Strength == other.Strength &&
            Roughness == other.Roughness &&
            Emission == other.Emission &&
            Wrap == other.Wrap &&
            HeightFineWeight == other.HeightFineWeight &&
            HeightCoarseWeight == other.HeightCoarseWeight &&
            ParallaxAmplitude == other.ParallaxAmplitude;

        public override bool Equals(object obj) =>
            obj is MaterialSurfaceProfile other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(
                Strength, Roughness, Emission, (int)Wrap,
                HeightFineWeight, HeightCoarseWeight, ParallaxAmplitude);
    }

    /// Classifies WAD texture/flat names into surface categories.
    public static class MaterialSurfaceClassifier
    {
        public static MaterialSurfaceCategory Classify(string name, bool isFlat)
        {
            if (string.IsNullOrEmpty(name))
                return MaterialSurfaceCategory.Unknown;

            string n = name.ToUpperInvariant();
            if (IsFluid(n)) return MaterialSurfaceCategory.Fluid;
            if (IsMetal(n)) return MaterialSurfaceCategory.Metal;
            if (isFlat) return MaterialSurfaceCategory.Flat;
            return MaterialSurfaceCategory.Wall;
        }

        static bool IsFluid(string n) =>
            n.Contains("NUKAGE") || n.Contains("LAVA") || n.Contains("BLOOD") ||
            n.Contains("WATER") || n.Contains("SLIME") || n.Contains("FWATER") ||
            n.Contains("SFALL") || n.Contains("BFALL") || n.Contains("DBRAIN");

        static bool IsMetal(string n) =>
            n.Contains("STEEL") || n.Contains("METAL") || n.Contains("PIPE") ||
            n.StartsWith("SUPPORT", StringComparison.Ordinal) ||
            n.Contains("GRATE");
    }

    /// Builds tangent-space normal maps from a heightmap (CPU, no Unity).
    /// Input is typically <see cref="HeightMapGenerator"/> output (grayscale RGB);
    /// luminance of any RGBA source is accepted. Height is packed into alpha for POM.
    /// Output layout matches <see cref="DecodedImage"/> (top-to-bottom, RGBA32).
    /// Neutral tangent normal encodes as (128, 128, 255, height).
    public static class NormalMapGenerator
    {
        public const byte NeutralR = 128;
        public const byte NeutralG = 128;
        public const byte NeutralB = 255;
        /// Legacy constant; alpha now stores height (0 for transparent texels).
        public const byte NeutralA = 255;

        public static DecodedImage Generate(
            DecodedImage height, float strength, NormalWrapMode wrap)
        {
            if (height == null)
                throw new ArgumentNullException(nameof(height));
            if (height.Width <= 0 || height.Height <= 0)
                throw new ArgumentException("Height image must have positive size.", nameof(height));
            if (height.Rgba == null || height.Rgba.Length < height.Width * height.Height * 4)
                throw new ArgumentException("Height RGBA buffer is incomplete.", nameof(height));

            int w = height.Width;
            int h = height.Height;
            var src = height.Rgba;
            var dst = new byte[w * h * 4];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = (y * w + x) * 4;
                    if (src[i + 3] == 0)
                    {
                        WriteNeutral(dst, i, heightA: 0);
                        continue;
                    }

                    float lL = Luminance(src, SampleIndex(x - 1, y, w, h, wrap));
                    float lR = Luminance(src, SampleIndex(x + 1, y, w, h, wrap));
                    float lU = Luminance(src, SampleIndex(x, y - 1, w, h, wrap));
                    float lD = Luminance(src, SampleIndex(x, y + 1, w, h, wrap));

                    // Central differences; negate so a bright-right slope tilts -X
                    // (standard height→normal convention).
                    float dx = (lR - lL) * strength;
                    float dy = (lD - lU) * strength;
                    float nx = -dx;
                    float ny = -dy;
                    float nz = 1f;

                    float len = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
                    if (len > 1e-8f)
                    {
                        nx /= len;
                        ny /= len;
                        nz /= len;
                    }
                    else
                    {
                        nx = 0f;
                        ny = 0f;
                        nz = 1f;
                    }

                    dst[i] = Encode(nx);
                    dst[i + 1] = Encode(ny);
                    dst[i + 2] = Encode(nz);
                    // Pack center height for POM (_BumpMap.a).
                    dst[i + 3] = EncodeHeightByte(src, i);
                }
            }

            return new DecodedImage(w, h, dst);
        }

        static int SampleIndex(int x, int y, int w, int h, NormalWrapMode wrap)
        {
            if (wrap == NormalWrapMode.Repeat)
            {
                x %= w;
                if (x < 0) x += w;
                y %= h;
                if (y < 0) y += h;
            }
            else
            {
                if (x < 0) x = 0;
                else if (x >= w) x = w - 1;
                if (y < 0) y = 0;
                else if (y >= h) y = h - 1;
            }

            return (y * w + x) * 4;
        }

        static float Luminance(byte[] rgba, int index) =>
            (0.299f * rgba[index] + 0.587f * rgba[index + 1] + 0.114f * rgba[index + 2]) / 255f;

        static byte Encode(float component)
        {
            float u = component * 0.5f + 0.5f;
            if (u <= 0f) return 0;
            if (u >= 1f) return 255;
            return (byte)(u * 255f + 0.5f);
        }

        static byte EncodeHeightByte(byte[] rgba, int index)
        {
            float h = Luminance(rgba, index);
            if (h <= 0f) return 0;
            if (h >= 1f) return 255;
            return (byte)(h * 255f + 0.5f);
        }

        static void WriteNeutral(byte[] dst, int i, byte heightA)
        {
            dst[i] = NeutralR;
            dst[i + 1] = NeutralG;
            dst[i + 2] = NeutralB;
            dst[i + 3] = heightA;
        }
    }
}
