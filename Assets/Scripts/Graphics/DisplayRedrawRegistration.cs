using System;
using System.Collections.Generic;

namespace Doom.Graphics
{
    /// Maps TRELLIS/GPT display-redraw canvases (sprite ≤416 px major axis,
    /// centered on a transparent 512×512 square; sources may be 1024×1024)
    /// onto the native WAD patch rectangle so billboard world size and
    /// draw-offsets stay identical to Classic.
    public static class DisplayRedrawRegistration
    {
        public const int CanvasSize = 512;
        public const int MaxSubjectPx = 416;

        public static int IntegerScaleToMax(int width, int height, int maxMajor = MaxSubjectPx)
        {
            int major = Math.Max(width, height);
            if (major <= 0) return 1;
            int scale = maxMajor / major;
            return scale < 1 ? 1 : scale;
        }

        public static DecodedImage ScaleNearest(DecodedImage src, int scale)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (scale <= 1) return src;
            int w = src.Width * scale, h = src.Height * scale;
            var rgba = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            {
                int sy = y / scale;
                for (int x = 0; x < w; x++)
                {
                    int sx = x / scale;
                    var p = src.GetPixel(sx, sy);
                    int o = (y * w + x) * 4;
                    rgba[o] = p.r;
                    rgba[o + 1] = p.g;
                    rgba[o + 2] = p.b;
                    rgba[o + 3] = p.a;
                }
            }
            return new DecodedImage(w, h, rgba);
        }

        public static DecodedImage CenterOnCanvas(DecodedImage subject, int canvasSize = CanvasSize)
        {
            if (subject == null) throw new ArgumentNullException(nameof(subject));
            var rgba = new byte[canvasSize * canvasSize * 4];
            int ox = (canvasSize - subject.Width) / 2;
            int oy = (canvasSize - subject.Height) / 2;
            for (int y = 0; y < subject.Height; y++)
            {
                int dy = oy + y;
                if (dy < 0 || dy >= canvasSize) continue;
                for (int x = 0; x < subject.Width; x++)
                {
                    int dx = ox + x;
                    if (dx < 0 || dx >= canvasSize) continue;
                    var p = subject.GetPixel(x, y);
                    int o = (dy * canvasSize + dx) * 4;
                    rgba[o] = p.r;
                    rgba[o + 1] = p.g;
                    rgba[o + 2] = p.b;
                    rgba[o + 3] = p.a;
                }
            }
            return new DecodedImage(canvasSize, canvasSize, rgba);
        }

        public static DecodedImage BuildNativeTrellisCanvas(DecodedImage native)
        {
            int scale = IntegerScaleToMax(native.Width, native.Height);
            return CenterOnCanvas(ScaleNearest(native, scale));
        }

        public static DecodedImage NormalizeToCanvas512(DecodedImage redraw)
        {
            if (redraw == null) throw new ArgumentNullException(nameof(redraw));
            if (redraw.Width == CanvasSize && redraw.Height == CanvasSize)
                return redraw;
            return ResamplePoint(redraw, CanvasSize, CanvasSize);
        }

        /// Sample the centered subject rect of a redraw canvas down to native
        /// patch dimensions (Point). Billboard world size stays native W×H.
        public static DecodedImage MapRedrawToNativeRect(DecodedImage redraw, DecodedImage native)
        {
            if (redraw == null) throw new ArgumentNullException(nameof(redraw));
            if (native == null) throw new ArgumentNullException(nameof(native));

            var canvas = NormalizeToCanvas512(redraw);
            SubjectRect(native.Width, native.Height, out int ox, out int oy, out int sw, out int sh, out int scale);

            var rgba = new byte[native.Width * native.Height * 4];
            for (int y = 0; y < native.Height; y++)
            {
                for (int x = 0; x < native.Width; x++)
                {
                    int cx = ox + x * scale + scale / 2;
                    int cy = oy + y * scale + scale / 2;
                    var p = SamplePoint(canvas, cx, cy);
                    int o = (y * native.Width + x) * 4;
                    rgba[o] = p.r;
                    rgba[o + 1] = p.g;
                    rgba[o + 2] = p.b;
                    rgba[o + 3] = p.a;
                }
            }
            return new DecodedImage(native.Width, native.Height, rgba);
        }

        /// Hi-res texture for a native-sized billboard quad: the centered subject
        /// rect from the 512 canvas (1:1 canvas pixels). World size still uses
        /// native W×H; UVs stretch this texture over the native quad.
        public static DecodedImage ExtractSubjectRect(
            DecodedImage redraw, int nativeWidth, int nativeHeight)
        {
            if (redraw == null) throw new ArgumentNullException(nameof(redraw));
            if (nativeWidth <= 0 || nativeHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(nativeWidth));

            var canvas = NormalizeToCanvas512(redraw);
            SubjectRect(nativeWidth, nativeHeight, out int ox, out int oy, out int sw, out int sh, out _);

            var rgba = new byte[sw * sh * 4];
            for (int y = 0; y < sh; y++)
            {
                for (int x = 0; x < sw; x++)
                {
                    var p = SamplePoint(canvas, ox + x, oy + y);
                    int o = (y * sw + x) * 4;
                    rgba[o] = p.r;
                    rgba[o + 1] = p.g;
                    rgba[o + 2] = p.b;
                    rgba[o + 3] = p.a;
                }
            }
            return new DecodedImage(sw, sh, rgba);
        }

        public static void SubjectRect(
            int nativeWidth, int nativeHeight,
            out int ox, out int oy, out int sw, out int sh, out int scale)
        {
            scale = IntegerScaleToMax(nativeWidth, nativeHeight);
            sw = nativeWidth * scale;
            sh = nativeHeight * scale;
            ox = (CanvasSize - sw) / 2;
            oy = (CanvasSize - sh) / 2;
        }

        /// Billboard world footprint in Unity meters (same as SpriteBillboard).
        public static (float width, float height) BillboardWorldSize(
            int nativeWidth, int nativeHeight, float worldScale) =>
            (nativeWidth * worldScale, nativeHeight * worldScale);

        public static (int minX, int minY, int maxX, int maxY) SilhouetteBounds(
            DecodedImage img, byte alphaThreshold = 128)
        {
            if (img == null) throw new ArgumentNullException(nameof(img));
            int minX = img.Width, minY = img.Height, maxX = -1, maxY = -1;
            for (int y = 0; y < img.Height; y++)
            {
                for (int x = 0; x < img.Width; x++)
                {
                    if (img.GetPixel(x, y).a <= alphaThreshold) continue;
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            }
            if (maxX < 0) return (0, 0, -1, -1);
            return (minX, minY, maxX, maxY);
        }

        /// ShapeHints from GPT often ship as opaque RGB with a light gray/white
        /// backdrop and no alpha. Flood-fill from the border so white subject
        /// detail (crosses, highlights) is kept.
        public static DecodedImage KeyOutLightBackground(DecodedImage src)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (CountTransparent(src) > src.Width * src.Height / 20)
                return src;

            int w = src.Width, h = src.Height;
            var rgba = (byte[])src.Rgba.Clone();
            var visited = new bool[w * h];
            var q = new Queue<int>(w * 4 + h * 4);

            void TryEnqueue(int x, int y)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) return;
                int i = y * w + x;
                if (visited[i]) return;
                if (!IsLightBackdrop(rgba, i)) return;
                visited[i] = true;
                q.Enqueue(i);
            }

            for (int x = 0; x < w; x++) { TryEnqueue(x, 0); TryEnqueue(x, h - 1); }
            for (int y = 0; y < h; y++) { TryEnqueue(0, y); TryEnqueue(w - 1, y); }

            while (q.Count > 0)
            {
                int i = q.Dequeue();
                rgba[i * 4 + 3] = 0;
                int x = i % w, y = i / w;
                TryEnqueue(x - 1, y);
                TryEnqueue(x + 1, y);
                TryEnqueue(x, y - 1);
                TryEnqueue(x, y + 1);
            }

            return new DecodedImage(w, h, rgba);
        }

        public static DecodedImage ResamplePoint(DecodedImage src, int dw, int dh)
        {
            var rgba = new byte[dw * dh * 4];
            for (int y = 0; y < dh; y++)
            {
                int sy = y * src.Height / dh;
                for (int x = 0; x < dw; x++)
                {
                    int sx = x * src.Width / dw;
                    var p = src.GetPixel(sx, sy);
                    int o = (y * dw + x) * 4;
                    rgba[o] = p.r;
                    rgba[o + 1] = p.g;
                    rgba[o + 2] = p.b;
                    rgba[o + 3] = p.a;
                }
            }
            return new DecodedImage(dw, dh, rgba);
        }

        static int CountTransparent(DecodedImage img)
        {
            int n = 0;
            var rgba = img.Rgba;
            for (int i = 3; i < rgba.Length; i += 4)
                if (rgba[i] < 16) n++;
            return n;
        }

        static bool IsLightBackdrop(byte[] rgba, int pixelIndex)
        {
            int o = pixelIndex * 4;
            int r = rgba[o], g = rgba[o + 1], b = rgba[o + 2];
            int max = Math.Max(r, Math.Max(g, b));
            int min = Math.Min(r, Math.Min(g, b));
            return max >= 230 && (max - min) <= 18;
        }

        static (byte r, byte g, byte b, byte a) SamplePoint(DecodedImage img, int x, int y)
        {
            if (x < 0 || y < 0 || x >= img.Width || y >= img.Height)
                return (0, 0, 0, 0);
            return img.GetPixel(x, y);
        }
    }
}
