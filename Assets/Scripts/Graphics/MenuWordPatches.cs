using System;
using System.Collections.Generic;

namespace Doom.Graphics
{
    /// Composes big-menu-font word patches the WAD does not ship ("ENHANCED",
    /// "CLASSIC" for the Options Graphics Mode row) by cutting single letters
    /// out of existing menu patches and laying them back out. Every pixel is
    /// WAD data — only the arrangement is new, same as drawing STCFN strings.
    /// The letters of this font touch at their feet, so each cut window is
    /// hand-measured against the Freedoom art and tiny connected blobs inside
    /// a window (a neighboring letter's foot crossing the cut) are dropped.
    /// A missing donor lump or an empty cut simply yields no composed patch;
    /// the menu then falls back to the small STCFN font.
    public static class MenuWordPatches
    {
        public const string EnhancedName = "M_ENHANC";
        public const string ClassicName = "M_CLASSC";

        const string EnhancedWord = "ENHANCED";
        const string ClassicWord = "CLASSIC";

        /// Pixels between adjacent glyphs (source words butt letters together;
        /// one column keeps the composed word readable at 320×200).
        const int GlyphGap = 1;

        /// Below this pixel count an 8-connected blob inside a cut window is a
        /// neighboring letter's residue, not part of the glyph.
        const int MinComponentPixels = 8;

        readonly struct GlyphCut
        {
            public readonly string Donor;
            public readonly int X0;
            public readonly int X1; // exclusive

            public GlyphCut(string donor, int x0, int x1)
            {
                Donor = donor;
                X0 = x0;
                X1 = x1;
            }
        }

        // Hand-measured against Freedoom 0.13 menu art. All cuts are the
        // mid-word ("value") letter form that M_MSGON/M_MSGOFF use (top row 1);
        // H is the word-initial capital from "HORIZONTAL..." (top row 0, the
        // 1 px difference is invisible in the menu).
        static readonly Dictionary<char, GlyphCut> Glyphs = new Dictionary<char, GlyphCut>
        {
            ['E'] = new GlyphCut("M_SETUP", 14, 24),   // "SETUP"
            ['N'] = new GlyphCut("M_MSGON", 11, 23),   // "ON"
            ['H'] = new GlyphCut("M_HORSEN", 0, 11),   // "HORIZONTAL SENSITIVITY"
            ['A'] = new GlyphCut("M_ENDGAM", 53, 64),  // "END GAME"
            ['C'] = new GlyphCut("M_MUSVOL", 47, 57),  // "MUSIC VOLUME"
            ['D'] = new GlyphCut("M_SOUND", 44, 55),   // "SOUND OPTIONS"
            ['L'] = new GlyphCut("M_GENERL", 62, 72),  // "GENERAL"
            ['S'] = new GlyphCut("M_MSENS", 100, 111), // "MOUSE SENSITIVITY"
            ['I'] = new GlyphCut("M_MSENS", 112, 120), // "MOUSE SENSITIVITY"
        };

        /// Donor lumps the composition reads — keep them in the catalog's
        /// load set.
        public static IReadOnlyList<string> DonorNames { get; } = BuildDonorNames();

        static List<string> BuildDonorNames()
        {
            var list = new List<string>();
            foreach (var cut in Glyphs.Values)
                if (!list.Contains(cut.Donor))
                    list.Add(cut.Donor);
            return list;
        }

        /// Compose both words from catalog donors and register them under
        /// <see cref="EnhancedName"/>/<see cref="ClassicName"/>. Safe to call
        /// on any catalog; failures leave the catalog untouched.
        public static void Install(UiPatchCatalog catalog)
        {
            if (catalog == null) return;
            if (TryCompose(catalog, EnhancedWord, EnhancedName, out var enhanced))
                catalog.AddComposed(enhanced);
            if (TryCompose(catalog, ClassicWord, ClassicName, out var classic))
                catalog.AddComposed(classic);
        }

        /// Compose one word; false when any donor/glyph is unusable.
        public static bool TryCompose(
            UiPatchCatalog catalog, string word, string patchName, out UiPatchInfo info)
        {
            info = default;
            if (catalog == null || string.IsNullOrEmpty(word)) return false;

            var glyphs = new List<byte[]>(word.Length);
            var widths = new List<int>(word.Length);
            int height = 0;

            foreach (char ch in word)
            {
                if (!Glyphs.TryGetValue(ch, out var cut)) return false;
                if (!catalog.TryGet(cut.Donor, out var donor) || donor.Image == null)
                    return false;
                if (cut.X1 > donor.Width || donor.Height <= 0) return false;

                // Same row count everywhere or the stamp below misindexes.
                if (height == 0) height = donor.Height;
                else if (donor.Height != height) return false;

                byte[] pixels = CutGlyph(donor.Image, cut.X0, cut.X1, out int w);
                if (pixels == null || w <= 0) return false;

                glyphs.Add(pixels);
                widths.Add(w);
            }

            int totalWidth = GlyphGap * (word.Length - 1);
            foreach (int w in widths) totalWidth += w;

            var rgba = new byte[totalWidth * height * 4];
            int x = 0;
            for (int i = 0; i < glyphs.Count; i++)
            {
                Stamp(rgba, totalWidth, glyphs[i], widths[i], height, x);
                x += widths[i] + GlyphGap;
            }

            info = new UiPatchInfo(
                patchName.ToUpperInvariant(), totalWidth, height, 0, 0,
                new DecodedImage(totalWidth, height, rgba));
            return true;
        }

        /// Cut [x0, x1) columns, drop tiny connected blobs, trim empty columns.
        /// Returns a tight RGBA block (donor height rows × width cols) or null.
        static byte[] CutGlyph(DecodedImage donor, int x0, int x1, out int width)
        {
            width = 0;
            int cutW = x1 - x0;
            int h = donor.Height;
            if (cutW <= 0 || h <= 0) return null;

            var cut = new byte[cutW * h * 4];
            for (int y = 0; y < h; y++)
            {
                Array.Copy(
                    donor.Rgba, (y * donor.Width + x0) * 4,
                    cut, y * cutW * 4, cutW * 4);
            }

            DropSmallComponents(cut, cutW, h);

            int left = -1, right = -1;
            for (int cx = 0; cx < cutW; cx++)
            {
                bool occupied = false;
                for (int y = 0; y < h && !occupied; y++)
                    occupied = cut[(y * cutW + cx) * 4 + 3] > 0;
                if (!occupied) continue;
                if (left < 0) left = cx;
                right = cx;
            }

            if (left < 0) return null;

            width = right - left + 1;
            var trimmed = new byte[width * h * 4];
            for (int y = 0; y < h; y++)
            {
                Array.Copy(
                    cut, (y * cutW + left) * 4,
                    trimmed, y * width * 4, width * 4);
            }

            return trimmed;
        }

        static void DropSmallComponents(byte[] rgba, int w, int h)
        {
            var seen = new bool[w * h];
            var stack = new Stack<int>();
            var component = new List<int>(w * h);

            for (int start = 0; start < w * h; start++)
            {
                if (seen[start] || rgba[start * 4 + 3] == 0) continue;

                component.Clear();
                stack.Push(start);
                seen[start] = true;
                while (stack.Count > 0)
                {
                    int p = stack.Pop();
                    component.Add(p);
                    int px = p % w, py = p / w;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = px + dx, ny = py + dy;
                            if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                            int n = ny * w + nx;
                            if (seen[n] || rgba[n * 4 + 3] == 0) continue;
                            seen[n] = true;
                            stack.Push(n);
                        }
                    }
                }

                if (component.Count >= MinComponentPixels) continue;
                foreach (int p in component)
                {
                    rgba[p * 4] = 0;
                    rgba[p * 4 + 1] = 0;
                    rgba[p * 4 + 2] = 0;
                    rgba[p * 4 + 3] = 0;
                }
            }
        }

        static void Stamp(
            byte[] canvas, int canvasW, byte[] glyph, int glyphW, int h, int atX)
        {
            for (int y = 0; y < h; y++)
            {
                for (int gx = 0; gx < glyphW; gx++)
                {
                    int src = (y * glyphW + gx) * 4;
                    if (glyph[src + 3] == 0) continue;
                    int dst = (y * canvasW + atX + gx) * 4;
                    canvas[dst] = glyph[src];
                    canvas[dst + 1] = glyph[src + 1];
                    canvas[dst + 2] = glyph[src + 2];
                    canvas[dst + 3] = glyph[src + 3];
                }
            }
        }
    }
}
