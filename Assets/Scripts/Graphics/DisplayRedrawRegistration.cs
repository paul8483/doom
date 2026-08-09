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

        /// Backdrop cleanup for subjects with no intentional near-white detail
        /// (trees): the border flood-fill cannot reach backdrop pockets fully
        /// enclosed by the silhouette (e.g. a branch fork), and anti-aliased
        /// checkerboard remnants survive attached to the silhouette by AA
        /// pixel chains. Keys every light-backdrop pixel globally, peels light
        /// gray remnants off the transparency edge, then drops EVERY opaque
        /// connected component smaller than ~1/16384 of the canvas — including
        /// legitimate detached detail that small (accepted loss: after
        /// registration it would shrink below a native texel anyway). Tree
        /// subjects keep their grays: measured interior gray brightness tops
        /// out near 180 while checker remnants sit above it.
        const int PeelIterations = 4;
        const int PeelMinBrightness = 180;
        const int PeelMaxSaturation = 30;
        // Checker remnants embedded inside AA chains read as neutral gray
        // ≥190; measured legitimate tree grays stay below ~180.
        const int AggressiveKeyMinBrightness = 190;
        const int AggressiveKeyMaxSaturation = 20;

        public static DecodedImage KeyOutBackdropAggressive(DecodedImage src)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            int w = src.Width, h = src.Height;
            var rgba = (byte[])src.Rgba.Clone();

            for (int i = 0; i < w * h; i++)
            {
                int o = i * 4;
                if (rgba[o + 3] == 0) continue;
                int r = rgba[o], g = rgba[o + 1], b = rgba[o + 2];
                int max = Math.Max(r, Math.Max(g, b));
                int min = Math.Min(r, Math.Min(g, b));
                bool neutralBright = max >= AggressiveKeyMinBrightness
                    && (max - min) <= AggressiveKeyMaxSaturation;
                if (neutralBright || IsLightBackdrop(rgba, i))
                    rgba[o + 3] = 0;
            }

            var peel = new List<int>();
            for (int pass = 0; pass < PeelIterations; pass++)
            {
                peel.Clear();
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        int o = i * 4;
                        if (rgba[o + 3] == 0) continue;
                        int r = rgba[o], g = rgba[o + 1], b = rgba[o + 2];
                        int max = Math.Max(r, Math.Max(g, b));
                        int min = Math.Min(r, Math.Min(g, b));
                        if (max < PeelMinBrightness || (max - min) > PeelMaxSaturation)
                            continue;
                        bool touchesTransparent =
                            (x > 0 && rgba[(i - 1) * 4 + 3] == 0) ||
                            (x + 1 < w && rgba[(i + 1) * 4 + 3] == 0) ||
                            (y > 0 && rgba[(i - w) * 4 + 3] == 0) ||
                            (y + 1 < h && rgba[(i + w) * 4 + 3] == 0);
                        if (touchesTransparent) peel.Add(i);
                    }
                if (peel.Count == 0) break;
                foreach (int i in peel) rgba[i * 4 + 3] = 0;
            }

            int minIsland = Math.Max(4, w * h / 16384);
            var component = new int[w * h]; // 0 = unvisited
            var sizes = new List<int> { 0 };
            var stack = new Stack<int>();
            for (int start = 0; start < w * h; start++)
            {
                if (component[start] != 0 || rgba[start * 4 + 3] == 0) continue;
                int id = sizes.Count, size = 0;
                sizes.Add(0);
                component[start] = id;
                stack.Push(start);
                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    size++;
                    int x = i % w, y = i / w;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            int n = ny * w + nx;
                            if (component[n] != 0 || rgba[n * 4 + 3] == 0) continue;
                            component[n] = id;
                            stack.Push(n);
                        }
                }
                sizes[id] = size;
            }

            for (int i = 0; i < w * h; i++)
                if (component[i] != 0 && sizes[component[i]] < minIsland)
                    rgba[i * 4 + 3] = 0;

            return new DecodedImage(w, h, rgba);
        }

        /// Recolor light-gray silhouette-edge pixels with the color of their
        /// interior neighbors. Keyed tree shapehints keep a 1–2 px ring of
        /// mid-gray AA residue (white checker × dark outline ≈ 140–190 gray)
        /// that reads as bright dots against dark game backgrounds. Removing
        /// the ring would thin branches; recoloring keeps the silhouette and
        /// kills the sparkle. Alpha is untouched.
        const int EdgeRecolorIterations = 3;
        const int EdgeRecolorMinBrightness = 140;
        const int EdgeRecolorMaxSaturation = 50;

        public static DecodedImage RecolorLightEdgeRing(DecodedImage src)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            int w = src.Width, h = src.Height;
            var rgba = (byte[])src.Rgba.Clone();

            bool Opaque(int x, int y) =>
                x >= 0 && y >= 0 && x < w && y < h && rgba[(y * w + x) * 4 + 3] != 0;

            var recolor = new List<(int i, byte r, byte g, byte b)>();
            for (int pass = 0; pass < EdgeRecolorIterations; pass++)
            {
                recolor.Clear();
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        int o = i * 4;
                        if (rgba[o + 3] == 0) continue;
                        int r = rgba[o], g = rgba[o + 1], b = rgba[o + 2];
                        int max = Math.Max(r, Math.Max(g, b));
                        int min = Math.Min(r, Math.Min(g, b));
                        if (max < EdgeRecolorMinBrightness ||
                            (max - min) > EdgeRecolorMaxSaturation)
                            continue;
                        bool onEdge = !Opaque(x - 1, y) || !Opaque(x + 1, y) ||
                                      !Opaque(x, y - 1) || !Opaque(x, y + 1);
                        if (!onEdge) continue;

                        // Average the darker interior neighbors (8-neigh).
                        int sr = 0, sg = 0, sb = 0, n = 0;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                int nx = x + dx, ny = y + dy;
                                if (!Opaque(nx, ny)) continue;
                                int no = (ny * w + nx) * 4;
                                int nmax = Math.Max(rgba[no],
                                    Math.Max(rgba[no + 1], rgba[no + 2]));
                                if (nmax >= max) continue; // only darker donors
                                sr += rgba[no];
                                sg += rgba[no + 1];
                                sb += rgba[no + 2];
                                n++;
                            }
                        if (n == 0) continue;
                        recolor.Add((i, (byte)(sr / n), (byte)(sg / n), (byte)(sb / n)));
                    }
                if (recolor.Count == 0) break;
                foreach (var (i, r, g, b) in recolor)
                {
                    rgba[i * 4] = r;
                    rgba[i * 4 + 1] = g;
                    rgba[i * 4 + 2] = b;
                }
            }
            return new DecodedImage(w, h, rgba);
        }

        /// Fit the silhouette bounding box of a keyed redraw into the subject
        /// rect the runtime samples (native aspect, ≤416 px major axis,
        /// centered on the 512 canvas). Tree shapehints paint the subject over
        /// nearly the full source canvas, which the fixed subject rect would
        /// otherwise crop with a straight line at the top/bottom.
        public static DecodedImage NormalizeSubjectToRect(
            DecodedImage keyed, int nativeWidth, int nativeHeight)
        {
            if (keyed == null) throw new ArgumentNullException(nameof(keyed));
            var b = SilhouetteBounds(keyed, alphaThreshold: 0);
            if (b.maxX < 0) return NormalizeToCanvas512(keyed);

            SubjectRect(nativeWidth, nativeHeight, out int ox, out int oy,
                out int sw, out int sh, out _);

            int bw = b.maxX - b.minX + 1, bh = b.maxY - b.minY + 1;
            var cropped = new byte[bw * bh * 4];
            for (int y = 0; y < bh; y++)
            {
                for (int x = 0; x < bw; x++)
                {
                    var p = keyed.GetPixel(b.minX + x, b.minY + y);
                    int o = (y * bw + x) * 4;
                    cropped[o] = p.r;
                    cropped[o + 1] = p.g;
                    cropped[o + 2] = p.b;
                    cropped[o + 3] = p.a;
                }
            }
            var subject = ResamplePoint(new DecodedImage(bw, bh, cropped), sw, sh);
            return CenterOnCanvas(subject);
        }

        /// Flood the RGB of transparent pixels with their nearest opaque
        /// neighbor's color (alpha stays 0). The keyed-out backdrop leaves
        /// white RGB under zero alpha; any interpolating sampler (texel-AA,
        /// mip generation) bleeds that white into the silhouette edge as
        /// bright fringing. Visible pixels are untouched.
        public static DecodedImage PadTransparentRgb(DecodedImage src)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            int w = src.Width, h = src.Height;
            var rgba = (byte[])src.Rgba.Clone();
            var filled = new bool[w * h];
            var queue = new Queue<int>();

            for (int i = 0; i < w * h; i++)
            {
                if (rgba[i * 4 + 3] == 0) continue;
                filled[i] = true;
                queue.Enqueue(i);
            }
            if (queue.Count == 0 || queue.Count == w * h)
                return new DecodedImage(w, h, rgba);

            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int x = i % w, y = i / w;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + (d == 0 ? -1 : d == 1 ? 1 : 0);
                    int ny = y + (d == 2 ? -1 : d == 3 ? 1 : 0);
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int n = ny * w + nx;
                    if (filled[n]) continue;
                    filled[n] = true;
                    rgba[n * 4] = rgba[i * 4];
                    rgba[n * 4 + 1] = rgba[i * 4 + 1];
                    rgba[n * 4 + 2] = rgba[i * 4 + 2];
                    queue.Enqueue(n);
                }
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
