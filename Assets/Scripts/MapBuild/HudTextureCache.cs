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

                var tex = ToTexture2D(info.Image);
                entries[info.Name] = new Entry(
                    tex, info.Width, info.Height, info.LeftOffset, info.TopOffset);
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
