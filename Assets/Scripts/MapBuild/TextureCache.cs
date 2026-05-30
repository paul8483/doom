using System.Collections.Generic;
using UnityEngine;
using Doom.Wad;
using Doom.Graphics;

namespace Doom.MapBuild
{
    /// Turns decoded WAD images into Unity Texture2D/Material, cached by name.
    /// Resolution order: composite wall texture (TextureSet) -> flat lump -> magenta.
    public sealed class TextureCache
    {
        private readonly WadFile wad;
        private readonly TextureSet textures;
        private readonly Palette palette;
        private readonly Shader opaqueShader;
        private readonly Shader cutoutShader;
        private readonly int anisoLevel;

        private readonly Dictionary<string, Texture2D> texCache = new();
        private readonly Dictionary<(string, bool), Material> matCache = new();

        public TextureCache(WadFile wad, TextureSet textures, Palette palette, int anisoLevel = 9)
        {
            this.wad = wad;
            this.textures = textures;
            this.palette = palette;
            this.anisoLevel = anisoLevel;
            opaqueShader = Shader.Find("Doom/Unlit");
            cutoutShader = Shader.Find("Doom/UnlitCutout");
        }

        public Material GetMaterial(string name, bool masked)
        {
            var key = (name, masked);
            if (matCache.TryGetValue(key, out var m)) return m;
            var mat = new Material(masked ? cutoutShader : opaqueShader);
            mat.mainTexture = GetTexture(name);
            matCache[key] = mat;
            return mat;
        }

        public Texture2D GetTexture(string name)
        {
            if (texCache.TryGetValue(name, out var t)) return t;
            var img = Decode(name);
            var tex = ToTexture2D(img);
            texCache[name] = tex;
            return tex;
        }

        private DecodedImage Decode(string name)
        {
            if (textures.Contains(name))
                return textures.Build(name, palette);

            int idx = wad.FindLump(name);
            if (idx >= 0 && wad.Directory[idx].Size == 64 * 64)
                return Flat.Decode(wad.ReadLump(idx), palette);

            GraphicsLog.Warning($"TextureCache: '{name}' is neither a known texture nor a 64x64 flat");
            return Placeholder.Magenta(64, 64);
        }

        private Texture2D ToTexture2D(DecodedImage img)
        {
            var tex = new Texture2D(img.Width, img.Height, TextureFormat.RGBA32, mipChain: true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Point;
            tex.anisoLevel = anisoLevel;

            // DecodedImage is top-to-bottom; Unity textures are bottom-to-top.
            // Flip rows so the image displays upright.
            int w = img.Width, h = img.Height;
            var flipped = new byte[img.Rgba.Length];
            int stride = w * 4;
            for (int y = 0; y < h; y++)
                System.Array.Copy(img.Rgba, y * stride, flipped, (h - 1 - y) * stride, stride);

            tex.LoadRawTextureData(flipped);
            tex.Apply(updateMipmaps: true, makeNoLongerReadable: false);
            return tex;
        }
    }
}
