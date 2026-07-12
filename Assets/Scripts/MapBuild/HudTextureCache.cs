using System;
using System.Collections.Generic;
using UnityEngine;
using Doom.Graphics;

namespace Doom.MapBuild
{
    /// Unity Texture2D cache for UI patches. Built from a fully-decoded
    /// <see cref="UiPatchCatalog"/> — never opens the WAD.
    public sealed class HudTextureCache
    {
        public readonly struct Entry
        {
            public readonly Texture2D Texture;
            public readonly int Width;
            public readonly int Height;
            public readonly int LeftOffset;
            public readonly int TopOffset;

            public Entry(Texture2D texture, int width, int height, int left, int top)
            {
                Texture = texture;
                Width = width;
                Height = height;
                LeftOffset = left;
                TopOffset = top;
            }

            public bool IsValid => Texture != null;
        }

        readonly Dictionary<string, Entry> entries =
            new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> misses =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly int anisoLevel;

        public HudTextureCache(UiPatchCatalog catalog, int anisoLevel = 1)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            this.anisoLevel = anisoLevel;

            foreach (var info in catalog.Entries)
            {
                if (!info.IsPresent)
                {
                    misses.Add(info.Name);
                    continue;
                }

                // Freedoom TITLEPIC tweaks for the main menu: hide copyright,
                // nudge "PHASE 1" one glyph left under the logo.
                if (string.Equals(info.Name, "TITLEPIC", StringComparison.OrdinalIgnoreCase))
                {
                    ScrubFreedoomTitleCopyright(info.Image);
                    ShiftFreedoomTitlePhase1(info.Image);
                }

                var tex = ToTexture2D(info.Image);
                entries[info.Name] = new Entry(
                    tex, info.Width, info.Height, info.LeftOffset, info.TopOffset);
            }
        }

        /// Paint over Freedoom Phase 1 TITLEPIC bottom-left copyright
        /// (y 186–192, x 5–70). Leaves the bottom-right version string alone.
        static void ScrubFreedoomTitleCopyright(DecodedImage img)
        {
            if (img == null || img.Width < 71 || img.Height < 193) return;

            const int x0 = 5, x1 = 70, y0 = 186, y1 = 192;
            var rgba = img.Rgba;
            int w = img.Width;
            int sampleY = y0 - 1;

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int o = (y * w + x) * 4;
                    if (!IsTitleCopyrightOrange(rgba[o], rgba[o + 1], rgba[o + 2]))
                        continue;

                    int s = (sampleY * w + x) * 4;
                    rgba[o] = rgba[s];
                    rgba[o + 1] = rgba[s + 1];
                    rgba[o + 2] = rgba[s + 2];
                }
            }
        }

        static bool IsTitleCopyrightOrange(byte r, byte g, byte b) =>
            r >= 160 && g >= 40 && g <= 160 && b <= 80 && r > g && r > b * 2;

        /// Slide Freedoom TITLEPIC "PHASE 1" one letter left (12 px glyph pitch).
        static void ShiftFreedoomTitlePhase1(DecodedImage img)
        {
            if (img == null || img.Width < 211 || img.Height < 169) return;

            const int x0 = 122, x1 = 198, y0 = 155, y1 = 168;
            const int shift = 12;
            var rgba = img.Rgba;
            int w = img.Width;
            int bandW = x1 - x0 + 1;
            int bandH = y1 - y0 + 1;
            var snap = new byte[bandW * bandH * 4];

            for (int y = 0; y < bandH; y++)
            {
                int src = ((y0 + y) * w + x0) * 4;
                Array.Copy(rgba, src, snap, y * bandW * 4, bandW * 4);
            }

            // Clear the old rect from the row above the subtitle (smoke/logo wash).
            int sampleY = y0 - 1;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int s = (sampleY * w + x) * 4;
                    int o = (y * w + x) * 4;
                    rgba[o] = rgba[s];
                    rgba[o + 1] = rgba[s + 1];
                    rgba[o + 2] = rgba[s + 2];
                    rgba[o + 3] = rgba[s + 3];
                }
            }

            // Prefer true background just right of the glyph for the vacated strip.
            for (int y = y0; y <= y1; y++)
            {
                for (int dx = 0; dx < shift; dx++)
                {
                    int x = x1 - shift + 1 + dx; // 187..198
                    int srcX = x1 + 1 + dx;      // 199..210
                    if (srcX >= w) break;
                    int s = (y * w + srcX) * 4;
                    int o = (y * w + x) * 4;
                    rgba[o] = rgba[s];
                    rgba[o + 1] = rgba[s + 1];
                    rgba[o + 2] = rgba[s + 2];
                    rgba[o + 3] = rgba[s + 3];
                }
            }

            int destX0 = x0 - shift;
            for (int y = 0; y < bandH; y++)
            {
                int dst = ((y0 + y) * w + destX0) * 4;
                Array.Copy(snap, y * bandW * 4, rgba, dst, bandW * 4);
            }
        }

        public bool TryGet(string name, out Entry entry)
        {
            if (string.IsNullOrEmpty(name))
            {
                entry = default;
                return false;
            }

            return entries.TryGetValue(name, out entry);
        }

        public bool IsMiss(string name) =>
            !string.IsNullOrEmpty(name) && misses.Contains(name);

        Texture2D ToTexture2D(DecodedImage img)
        {
            int w = Mathf.Max(1, img.Width);
            int h = Mathf.Max(1, img.Height);
            // HUD stays Point, no mipmaps. linear:false keeps palette colors as
            // sRGB color data for OnGUI (not world Linear sampling).
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Point;
            tex.anisoLevel = anisoLevel;

            var src = img.Rgba;
            var flipped = new byte[w * h * 4];
            int stride = w * 4;
            for (int y = 0; y < h; y++)
                Array.Copy(src, y * stride, flipped, (h - 1 - y) * stride, stride);

            tex.LoadRawTextureData(flipped);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return tex;
        }
    }
}
