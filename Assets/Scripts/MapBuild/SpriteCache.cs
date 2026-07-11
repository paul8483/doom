using System.Collections.Generic;
using UnityEngine;
using Doom.Wad;
using Doom.Graphics;
using Doom.MapBuild.Rendering;

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
        private readonly DoomMaterialFactory materials;
        private readonly WorldRenderContext context;
        private readonly int anisoLevel;

        private readonly Dictionary<int, Material> matByLump = new();
        private readonly Dictionary<int, PatchHeader> headerByLump = new();
        private readonly HashSet<int> failedLumps = new();

        public SpriteCache(
            WadFile wad,
            SpriteSet sprites,
            Palette palette,
            DoomMaterialFactory materials = null,
            WorldRenderContext context = null,
            int anisoLevel = 9)
        {
            this.wad = wad;
            this.sprites = sprites;
            this.palette = palette;
            this.materials = materials ?? new DoomMaterialFactory();
            this.context = context;
            this.anisoLevel = anisoLevel;
        }

        /// Resolve (sprite, frame, rotationIndex 0..7). Returns an invalid
        /// SpriteMaterial (IsValid == false) if the frame/rotation is missing.
        public SpriteMaterial Get(string sprite, int frame, int rotationIndex)
        {
            if (!sprites.TryGet(sprite, frame, rotationIndex, out var refr))
                return default;

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
                    mat = materials.CreateMaterial(tex, masked: true);
                    matByLump[refr.LumpIndex] = mat;
                    context?.RegisterTexture(tex);
                    context?.RegisterMaterial(mat, masked: true);
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

        private Texture2D ToTexture2D(DecodedImage img)
        {
            int w = Mathf.Max(1, img.Width), h = Mathf.Max(1, img.Height);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = materials.WorldFilterMode;
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
