using System.Collections.Generic;
using UnityEngine;
using Doom.Wad;
using Doom.Graphics;
using Doom.MapBuild.Rendering;

namespace Doom.MapBuild
{
    /// Turns decoded WAD images into Unity Texture2D/Material, cached by name.
    /// Resolution order: composite wall texture (TextureSet) -> flat lump -> magenta.
    /// Enhanced normals are built lazily once per texture name from cached RGBA.
    public sealed class TextureCache
    {
        private readonly WadFile wad;
        private readonly TextureSet textures;
        private readonly Palette palette;
        private readonly DoomMaterialFactory materials;
        private readonly WorldRenderContext context;
        private readonly int anisoLevel;

        private readonly Dictionary<string, Texture2D> texCache = new();
        private readonly Dictionary<string, DecodedImage> sourceCache = new();
        private readonly Dictionary<string, Texture2D> normalCache = new();
        private readonly Dictionary<string, MaterialSurfaceCategory> categoryByName = new();
        private readonly Dictionary<Texture2D, string> albedoToName = new();
        private readonly Dictionary<(string, bool), Material> matCache = new();

        public int NormalMapCount => normalCache.Count;

        public TextureCache(
            WadFile wad,
            TextureSet textures,
            Palette palette,
            DoomMaterialFactory materials = null,
            WorldRenderContext context = null,
            int anisoLevel = 9)
        {
            this.wad = wad;
            this.textures = textures;
            this.palette = palette;
            this.materials = materials ?? new DoomMaterialFactory();
            this.context = context;
            this.anisoLevel = anisoLevel;

            this.materials.SetNormalLookup(GetNormalForAlbedo);
            this.materials.SetSurfaceLookup(GetSurfaceForAlbedo);
        }

        public Material GetMaterial(string name, bool masked)
        {
            var key = (name, masked);
            if (matCache.TryGetValue(key, out var m)) return m;
            var tex = GetTexture(name);
            var mat = materials.CreateMaterial(tex, masked);
            matCache[key] = mat;
            context?.RegisterMaterial(mat, masked);
            return mat;
        }

        public Texture2D GetTexture(string name)
        {
            if (texCache.TryGetValue(name, out var t)) return t;
            Texture2D tex;
            try
            {
                var (img, isFlat) = DecodeWithKind(name);
                sourceCache[name] = img;
                categoryByName[name] = MaterialSurfaceClassifier.Classify(name, isFlat);
                tex = ToAlbedoTexture2D(img, name);
            }
            catch (System.Exception e)
            {
                GraphicsLog.Warning($"TextureCache: failed to load '{name}': {e.Message} — using placeholder");
                var img = Placeholder.Magenta(64, 64);
                sourceCache[name] = img;
                categoryByName[name] = MaterialSurfaceCategory.Unknown;
                tex = ToAlbedoTexture2D(img, name);
            }
            texCache[name] = tex;
            albedoToName[tex] = name;
            context?.RegisterTexture(tex);
            return tex;
        }

        /// Lazy normal for Enhanced materials. Created once per texture name.
        public Texture2D GetOrCreateNormal(string name)
        {
            if (normalCache.TryGetValue(name, out var existing))
                return existing;

            if (!sourceCache.TryGetValue(name, out var img))
            {
                // Ensure albedo path ran so source + category exist.
                GetTexture(name);
                img = sourceCache[name];
            }

            if (!categoryByName.TryGetValue(name, out var category))
                category = MaterialSurfaceCategory.Unknown;

            var profile = MaterialSurfaceProfile.For(category);
            var normalImg = NormalMapGenerator.Generate(img, profile.Strength, profile.Wrap);
            var tex = ToNormalTexture2D(normalImg, name);
            normalCache[name] = tex;
            context?.RegisterTexture(tex);
            context?.RegisterOwned(tex);
            return tex;
        }

        Texture2D GetNormalForAlbedo(Texture2D albedo)
        {
            if (albedo == null) return null;
            if (!albedoToName.TryGetValue(albedo, out var name)) return null;
            return GetOrCreateNormal(name);
        }

        MaterialSurfaceProfile GetSurfaceForAlbedo(Texture2D albedo)
        {
            if (albedo != null &&
                albedoToName.TryGetValue(albedo, out var name) &&
                categoryByName.TryGetValue(name, out var category))
            {
                return MaterialSurfaceProfile.For(category);
            }

            return MaterialSurfaceProfile.For(MaterialSurfaceCategory.Unknown);
        }

        private (DecodedImage img, bool isFlat) DecodeWithKind(string name)
        {
            if (textures.Contains(name))
                return (textures.Build(name, palette), isFlat: false);

            int idx = wad.FindLump(name);
            if (idx >= 0 && wad.Directory[idx].Size == 64 * 64)
                return (Flat.Decode(wad.ReadLump(idx), palette), isFlat: true);

            GraphicsLog.Warning($"TextureCache: '{name}' is neither a known texture nor a 64x64 flat");
            return (Placeholder.Magenta(64, 64), isFlat: false);
        }

        private Texture2D ToAlbedoTexture2D(DecodedImage img, string name)
        {
            if (img.Width <= 0 || img.Height <= 0)
                img = Placeholder.Magenta(64, 64);

            int w = img.Width, h = img.Height;
            // linear: false → sRGB color texture (WAD albedo).
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: false);
            tex.name = name;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = materials.WorldFilterMode;
            tex.anisoLevel = anisoLevel;

            UploadFlipped(tex, img);
            return tex;
        }

        private Texture2D ToNormalTexture2D(DecodedImage img, string name)
        {
            int w = img.Width, h = img.Height;
            // linear: true → data texture; no sRGB curve on normals.
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: true);
            tex.name = name + "/Normal";
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.anisoLevel = anisoLevel;

            UploadFlipped(tex, img);
            return tex;
        }

        static void UploadFlipped(Texture2D tex, DecodedImage img)
        {
            int w = img.Width, h = img.Height;
            var flipped = new byte[img.Rgba.Length];
            int stride = w * 4;
            for (int y = 0; y < h; y++)
                System.Array.Copy(img.Rgba, y * stride, flipped, (h - 1 - y) * stride, stride);

            tex.LoadRawTextureData(flipped);
            tex.Apply(updateMipmaps: true, makeNoLongerReadable: false);
        }
    }
}
