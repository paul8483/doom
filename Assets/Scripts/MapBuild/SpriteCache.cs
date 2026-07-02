using System.Collections.Generic;
using UnityEngine;
using Doom.Wad;
using Doom.Graphics;

namespace Doom.MapBuild
{
    /// Resolved sprite frame ready to render: a cutout material plus the source
    /// patch dimensions/offsets (DOOM units) and the mirror flag.
    public readonly struct SpriteMaterial
    {
        public readonly Material Material;
        public readonly int Width, Height, LeftOffset, TopOffset;
        public readonly bool Mirrored;
        public SpriteMaterial(Material m, int w, int h, int left, int top, bool mirrored)
        {
            Material = m; Width = w; Height = h; LeftOffset = left; TopOffset = top;
            Mirrored = mirrored;
        }
        public bool IsValid => Material != null;
    }

    /// Decodes sprite lumps into cutout Materials, cached by lump index.
    /// Mirror is NOT baked into the texture — the billboard flips its X scale.
    public sealed class SpriteCache
    {
        private readonly WadFile wad;
        private readonly SpriteSet sprites;
        private readonly Palette palette;
        private readonly Shader cutoutShader;
        private readonly int anisoLevel;

        private readonly Dictionary<int, Material> matByLump = new();
        private readonly Dictionary<int, PatchHeader> headerByLump = new();
        private readonly HashSet<int> failedLumps = new();

        public SpriteCache(WadFile wad, SpriteSet sprites, Palette palette, int anisoLevel = 9)
        {
            this.wad = wad;
            this.sprites = sprites;
            this.palette = palette;
            this.anisoLevel = anisoLevel;
            cutoutShader = Shader.Find("Doom/UnlitCutout");
        }

        /// Resolve (sprite, frame, rotationIndex 0..7). Returns an invalid
        /// SpriteMaterial (IsValid == false) if the frame/rotation is missing.
        public SpriteMaterial Get(string sprite, int frame, int rotationIndex)
        {
            if (!sprites.TryGet(sprite, frame, rotationIndex, out var refr))
                return default;

            // Reads from the WAD are lazy; after MapLoader disposes the WAD a cache
            // miss would throw ObjectDisposedException from LateUpdate every frame.
            // Cache the failure so each missing lump warns once and never retries.
            if (failedLumps.Contains(refr.LumpIndex))
                return default;

            PatchHeader header;
            Material mat;
            try
            {
                if (!headerByLump.TryGetValue(refr.LumpIndex, out header))
                {
                    header = Patch.ReadHeader(wad.ReadLump(refr.LumpIndex));
                    headerByLump[refr.LumpIndex] = header;
                }

                if (!matByLump.TryGetValue(refr.LumpIndex, out mat))
                {
                    var img = Patch.Decode(wad.ReadLump(refr.LumpIndex), palette);
                    var tex = ToTexture2D(img);
                    mat = new Material(cutoutShader) { mainTexture = tex };
                    matByLump[refr.LumpIndex] = mat;
                }
            }
            catch (System.ObjectDisposedException)
            {
                failedLumps.Add(refr.LumpIndex);
                Debug.LogWarning($"SpriteCache: sprite '{sprite}' frame {frame} rot {rotationIndex} " +
                                 "requested after the WAD was closed and was not pre-warmed; " +
                                 "it will not render.");
                return default;
            }

            return new SpriteMaterial(mat, header.Width, header.Height,
                                      header.LeftOffset, header.TopOffset, refr.Mirrored);
        }

        // Same point+mip+aniso upload as TextureCache, with row-flip (DecodedImage
        // is top-to-bottom; Unity textures are bottom-to-top). Sprites use Clamp
        // so the transparent border never wraps.
        private Texture2D ToTexture2D(DecodedImage img)
        {
            int w = Mathf.Max(1, img.Width), h = Mathf.Max(1, img.Height);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Point;
            tex.anisoLevel = anisoLevel;

            var src = img.Rgba;
            var flipped = new byte[w * h * 4];
            int stride = w * 4;
            for (int y = 0; y < h; y++)
                System.Array.Copy(src, y * stride, flipped, (h - 1 - y) * stride, stride);

            tex.LoadRawTextureData(flipped);
            tex.Apply(updateMipmaps: true, makeNoLongerReadable: false);
            return tex;
        }
    }
}
