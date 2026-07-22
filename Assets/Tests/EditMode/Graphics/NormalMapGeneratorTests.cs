using System;
using NUnit.Framework;

namespace Doom.Graphics.Tests
{
    public class NormalMapGeneratorTests
    {
        [Test]
        public void Uniform_image_yields_neutral_normals()
        {
            var src = Solid(4, 4, 120, 80, 40, 255);
            var n = NormalMapGenerator.Generate(src, strength: 2f, NormalWrapMode.Repeat);

            byte expectedHeight = HeightByte(120, 80, 40);
            Assert.AreEqual(4, n.Width);
            Assert.AreEqual(4, n.Height);
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
            {
                var p = n.GetPixel(x, y);
                Assert.AreEqual(NormalMapGenerator.NeutralR, p.r);
                Assert.AreEqual(NormalMapGenerator.NeutralG, p.g);
                Assert.AreEqual(NormalMapGenerator.NeutralB, p.b);
                Assert.AreEqual(expectedHeight, p.a);
            }
        }

        [Test]
        public void Horizontal_gradient_tilts_normal_in_signed_X()
        {
            // Left dark → right bright: dx > 0 → nx < 0 → R < 128.
            var src = HorizontalGradient(8, 4, dark: 0, bright: 255);
            var n = NormalMapGenerator.Generate(src, strength: 4f, NormalWrapMode.Clamp);

            var mid = n.GetPixel(4, 2);
            Assert.That(mid.r, Is.LessThan(NormalMapGenerator.NeutralR));
            Assert.AreEqual(NormalMapGenerator.NeutralG, mid.g);
            Assert.That(mid.b, Is.GreaterThan(200));
        }

        [Test]
        public void Vertical_gradient_tilts_normal_in_signed_Y()
        {
            // Top dark → bottom bright (y increases downward): dy > 0 → ny < 0 → G < 128.
            var src = VerticalGradient(4, 8, dark: 0, bright: 255);
            var n = NormalMapGenerator.Generate(src, strength: 4f, NormalWrapMode.Clamp);

            var mid = n.GetPixel(2, 4);
            Assert.AreEqual(NormalMapGenerator.NeutralR, mid.r);
            Assert.That(mid.g, Is.LessThan(NormalMapGenerator.NeutralG));
            Assert.That(mid.b, Is.GreaterThan(200));
        }

        [Test]
        public void Repeat_wrap_differs_from_clamp_at_edges()
        {
            // Checker so wrap samples at edges differ between Repeat and Clamp.
            var src = Checker(8, 8);
            var repeat = NormalMapGenerator.Generate(src, 3f, NormalWrapMode.Repeat);
            var clamp = NormalMapGenerator.Generate(src, 3f, NormalWrapMode.Clamp);

            Assert.That(BytesEqual(repeat.Rgba, clamp.Rgba), Is.False,
                "Repeat vs Clamp must differ on a wrapping checker at edges");
        }

        [Test]
        public void Transparent_pixels_are_neutral()
        {
            var rgba = new byte[2 * 2 * 4];
            // Opaque dark / bright neighbors, center-left transparent.
            Write(rgba, 0, 0, 2, 0, 0, 0, 255);
            Write(rgba, 1, 0, 2, 255, 255, 255, 255);
            Write(rgba, 0, 1, 2, 0, 0, 0, 0);   // transparent
            Write(rgba, 1, 1, 2, 255, 255, 255, 255);

            var src = new DecodedImage(2, 2, rgba);
            var n = NormalMapGenerator.Generate(src, strength: 8f, NormalWrapMode.Clamp);
            var t = n.GetPixel(0, 1);
            Assert.AreEqual(NormalMapGenerator.NeutralR, t.r);
            Assert.AreEqual(NormalMapGenerator.NeutralG, t.g);
            Assert.AreEqual(NormalMapGenerator.NeutralB, t.b);
            Assert.AreEqual(0, t.a);
        }

        [Test]
        public void Height_is_packed_into_alpha_non_constant_on_gradient()
        {
            var src = HorizontalGradient(8, 4, dark: 0, bright: 255);
            var n = NormalMapGenerator.Generate(src, strength: 4f, NormalWrapMode.Clamp);

            Assert.That(n.GetPixel(1, 2).a, Is.LessThan(n.GetPixel(6, 2).a));
        }

        [Test]
        public void Multi_scale_height_source_yields_non_constant_alpha()
        {
            var albedo = HorizontalGradient(16, 8, dark: 20, bright: 220);
            var height = HeightMapGenerator.Generate(
                albedo, MaterialSurfaceCategory.Wall, PixelWrapMode.Clamp);
            var n = NormalMapGenerator.Generate(height, strength: 2f, NormalWrapMode.Clamp);

            byte minA = 255, maxA = 0;
            for (int y = 0; y < n.Height; y++)
            for (int x = 0; x < n.Width; x++)
            {
                byte a = n.GetPixel(x, y).a;
                if (a < minA) minA = a;
                if (a > maxA) maxA = a;
            }
            Assert.That(maxA - minA, Is.GreaterThan(20),
                "Solid materials must pack varying height into normal alpha");
        }

        [Test]
        public void Output_is_deterministic()
        {
            var src = Checker(16, 16);
            var a = NormalMapGenerator.Generate(src, 2.5f, NormalWrapMode.Repeat);
            var b = NormalMapGenerator.Generate(src, 2.5f, NormalWrapMode.Repeat);
            Assert.That(BytesEqual(a.Rgba, b.Rgba), Is.True);
        }

        [Test]
        public void Unknown_category_uses_weak_neutral_profile()
        {
            var unknown = MaterialSurfaceProfile.For(MaterialSurfaceCategory.Unknown);
            var wall = MaterialSurfaceProfile.For(MaterialSurfaceCategory.Wall);
            Assert.That(unknown.Strength, Is.LessThan(wall.Strength));
            Assert.That(unknown.Roughness, Is.GreaterThan(0.5f));
            Assert.AreEqual(0f, unknown.Emission);
            Assert.AreEqual(NormalWrapMode.Repeat, unknown.Wrap);
        }

        [Test]
        public void Classifier_maps_fluid_metal_flat_wall()
        {
            Assert.AreEqual(MaterialSurfaceCategory.Fluid,
                MaterialSurfaceClassifier.Classify("NUKAGE1", isFlat: true));
            Assert.AreEqual(MaterialSurfaceCategory.Metal,
                MaterialSurfaceClassifier.Classify("METAL2", isFlat: false));
            Assert.AreEqual(MaterialSurfaceCategory.Flat,
                MaterialSurfaceClassifier.Classify("FLOOR0_1", isFlat: true));
            Assert.AreEqual(MaterialSurfaceCategory.Wall,
                MaterialSurfaceClassifier.Classify("STARTAN3", isFlat: false));
            Assert.AreEqual(MaterialSurfaceCategory.Unknown,
                MaterialSurfaceClassifier.Classify(null, isFlat: false));
        }

        static DecodedImage Solid(int w, int h, byte r, byte g, byte b, byte a)
        {
            var rgba = new byte[w * h * 4];
            for (int i = 0; i < rgba.Length; i += 4)
            {
                rgba[i] = r; rgba[i + 1] = g; rgba[i + 2] = b; rgba[i + 3] = a;
            }
            return new DecodedImage(w, h, rgba);
        }

        static DecodedImage HorizontalGradient(int w, int h, byte dark, byte bright)
        {
            var rgba = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                byte v = (byte)(dark + (bright - dark) * x / Math.Max(1, w - 1));
                Write(rgba, x, y, w, v, v, v, 255);
            }
            return new DecodedImage(w, h, rgba);
        }

        static DecodedImage VerticalGradient(int w, int h, byte dark, byte bright)
        {
            var rgba = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                byte v = (byte)(dark + (bright - dark) * y / Math.Max(1, h - 1));
                Write(rgba, x, y, w, v, v, v, 255);
            }
            return new DecodedImage(w, h, rgba);
        }

        static DecodedImage Checker(int w, int h)
        {
            var rgba = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                byte v = ((x + y) & 1) == 0 ? (byte)0 : (byte)255;
                Write(rgba, x, y, w, v, v, v, 255);
            }
            return new DecodedImage(w, h, rgba);
        }

        static void Write(byte[] rgba, int x, int y, int w, byte r, byte g, byte b, byte a)
        {
            int i = (y * w + x) * 4;
            rgba[i] = r; rgba[i + 1] = g; rgba[i + 2] = b; rgba[i + 3] = a;
        }

        static byte HeightByte(byte r, byte g, byte b) =>
            (byte)((0.299f * r + 0.587f * g + 0.114f * b) + 0.5f);

        static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }
}
