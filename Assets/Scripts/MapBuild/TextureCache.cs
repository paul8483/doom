using System.Collections.Generic;
using UnityEngine;
using Doom.Wad;
using Doom.Graphics;
using Doom.MapBuild.Rendering;

namespace Doom.MapBuild
{
    /// Turns decoded WAD images into Unity Texture2D/Material, cached by name.
    /// Resolution order: composite wall texture (TextureSet) -> flat lump -> magenta.
    /// Native and Enhanced 2× albedo variants share one decoded source per name.
    public sealed class TextureCache
    {
        sealed class SourceEntry
        {
            public DecodedImage Native;
            public DecodedImage Enhanced;
            public bool IsFlat;
            public bool IsPlaceholder;
            public bool EnhancedFailed;
        }

        private readonly WadFile wad;
        private readonly TextureSet textures;
        private readonly Palette palette;
        private readonly DoomMaterialFactory materials;
        private readonly WorldRenderContext context;
        private readonly int anisoLevel;

        private readonly Dictionary<(string name, WorldTextureVariant variant), Texture2D> texCache = new();
        private readonly Dictionary<string, SourceEntry> sourceCache = new();
        private readonly Dictionary<(string name, WorldTextureVariant variant), Texture2D> normalCache = new();
        private readonly Dictionary<string, MaterialSurfaceCategory> categoryByName = new();
        private readonly Dictionary<Texture2D, (string name, WorldTextureVariant variant)> albedoToName = new();
        private readonly Dictionary<(string, bool), Material> matCache = new();
        private readonly HashSet<Texture2D> registeredTextures = new();

        int enhancedVariantCount;
        long enhancedTextureBytes;

        public int NormalMapCount => normalCache.Count;
        public int EnhancedVariantCount => enhancedVariantCount;
        public long EnhancedTextureBytes => enhancedTextureBytes;

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
            context?.BindTextureCache(this);
        }

        public Material GetMaterial(string name, bool masked)
        {
            var key = (name, masked);
            if (matCache.TryGetValue(key, out var m)) return m;
            var tex = GetTextureForProfile(name, materials.ActiveProfile);
            var mat = materials.CreateMaterial(tex, masked);
            matCache[key] = mat;
            context?.RegisterMaterial(mat, masked, name);
            return mat;
        }

        /// Always returns the native (non-upscaled) albedo. Safe for pre-warm.
        public Texture2D GetTexture(string name) =>
            GetTexture(name, WorldTextureVariant.Native);

        public Texture2D GetTextureForProfile(string name, GraphicsProfile profile) =>
            GetTexture(name, profile.WorldTextureVariant);

        public Texture2D GetTexture(string name, WorldTextureVariant variant)
        {
            var key = (name, variant);
            if (texCache.TryGetValue(key, out var existing))
                return existing;

            EnsureSource(name);

            if (variant == WorldTextureVariant.Enhanced2X)
                return GetOrCreateEnhanced(name);

            return GetOrCreateNative(name);
        }

        /// Lazy normal for Enhanced materials. Built from the Enhanced 2× source.
        public Texture2D GetOrCreateNormal(string name)
        {
            var key = (name, WorldTextureVariant.Enhanced2X);
            if (normalCache.TryGetValue(key, out var existing))
                return existing;

            EnsureSource(name);
            var entry = sourceCache[name];
            var enhancedImg = GetEnhancedDecoded(name, entry);
            if (enhancedImg == null)
                return null;

            if (!categoryByName.TryGetValue(name, out var category))
                category = MaterialSurfaceCategory.Unknown;

            var profile = MaterialSurfaceProfile.For(category);
            var normalImg = NormalMapGenerator.Generate(enhancedImg, profile.Strength, profile.Wrap);
            var tex = ToNormalTexture2D(normalImg, name);
            normalCache[key] = tex;
            RegisterTextureOnce(tex);
            context?.RegisterOwned(tex);

            // CPU 2× buffer is no longer needed once albedo + normal are uploaded.
            if (texCache.ContainsKey((name, WorldTextureVariant.Enhanced2X)))
                entry.Enhanced = null;

            return tex;
        }

        Texture2D GetOrCreateNative(string name)
        {
            var key = (name, WorldTextureVariant.Native);
            if (texCache.TryGetValue(key, out var existing))
                return existing;

            var entry = sourceCache[name];
            var tex = ToAlbedoTexture2D(entry.Native, name);
            texCache[key] = tex;
            albedoToName[tex] = (name, WorldTextureVariant.Native);
            RegisterTextureOnce(tex);
            return tex;
        }

        Texture2D GetOrCreateEnhanced(string name)
        {
            var key = (name, WorldTextureVariant.Enhanced2X);
            if (texCache.TryGetValue(key, out var existing))
                return existing;

            var entry = sourceCache[name];
            if (entry.EnhancedFailed)
                return GetOrCreateNative(name);

            try
            {
                var enhancedImg = GetEnhancedDecoded(name, entry);
                var tex = ToAlbedoTexture2D(enhancedImg, name);
                texCache[key] = tex;
                albedoToName[tex] = (name, WorldTextureVariant.Enhanced2X);
                RegisterTextureOnce(tex);
                enhancedVariantCount++;
                enhancedTextureBytes += (long)tex.width * tex.height * 4L;
                return tex;
            }
            catch (System.Exception e)
            {
                entry.EnhancedFailed = true;
                entry.Enhanced = null;
                GraphicsLog.Warning(
                    $"TextureCache: Enhanced 2× failed for '{name}': {e.Message} — using native");
                return GetOrCreateNative(name);
            }
        }

        DecodedImage GetEnhancedDecoded(string name, SourceEntry entry)
        {
            if (entry.EnhancedFailed)
                return null;
            if (entry.Enhanced != null)
                return entry.Enhanced;

            var wrap = WrapFor(entry);
            entry.Enhanced = PixelArtUpscaler.Scale2X(entry.Native, wrap);
            return entry.Enhanced;
        }

        static PixelWrapMode WrapFor(SourceEntry entry)
        {
            if (entry.IsPlaceholder)
                return PixelWrapMode.Clamp;
            if (entry.IsFlat)
                return PixelWrapMode.RepeatXY;
            return PixelWrapMode.RepeatX;
        }

        void EnsureSource(string name)
        {
            if (sourceCache.ContainsKey(name))
                return;

            try
            {
                var (img, isFlat, isPlaceholder) = DecodeWithKind(name);
                sourceCache[name] = new SourceEntry
                {
                    Native = img,
                    IsFlat = isFlat,
                    IsPlaceholder = isPlaceholder,
                };
                categoryByName[name] = MaterialSurfaceClassifier.Classify(name, isFlat);
            }
            catch (System.Exception e)
            {
                GraphicsLog.Warning($"TextureCache: failed to load '{name}': {e.Message} — using placeholder");
                sourceCache[name] = new SourceEntry
                {
                    Native = Placeholder.Magenta(64, 64),
                    IsFlat = false,
                    IsPlaceholder = true,
                };
                categoryByName[name] = MaterialSurfaceCategory.Unknown;
            }
        }

        Texture2D GetNormalForAlbedo(Texture2D albedo)
        {
            if (albedo == null) return null;
            if (!albedoToName.TryGetValue(albedo, out var info)) return null;
            // Normals match Enhanced 2× albedo only. Native fallback keeps flat normals.
            if (info.variant != WorldTextureVariant.Enhanced2X)
                return null;
            return GetOrCreateNormal(info.name);
        }

        MaterialSurfaceProfile GetSurfaceForAlbedo(Texture2D albedo)
        {
            if (albedo != null &&
                albedoToName.TryGetValue(albedo, out var info) &&
                categoryByName.TryGetValue(info.name, out var category))
            {
                return MaterialSurfaceProfile.For(category);
            }

            return MaterialSurfaceProfile.For(MaterialSurfaceCategory.Unknown);
        }

        private (DecodedImage img, bool isFlat, bool isPlaceholder) DecodeWithKind(string name)
        {
            if (textures.Contains(name))
                return (textures.Build(name, palette), isFlat: false, isPlaceholder: false);

            int idx = wad.FindLump(name);
            if (idx >= 0 && wad.Directory[idx].Size == 64 * 64)
                return (Flat.Decode(wad.ReadLump(idx), palette), isFlat: true, isPlaceholder: false);

            GraphicsLog.Warning($"TextureCache: '{name}' is neither a known texture nor a 64x64 flat");
            return (Placeholder.Magenta(64, 64), isFlat: false, isPlaceholder: true);
        }

        private Texture2D ToAlbedoTexture2D(DecodedImage img, string name)
        {
            if (img.Width <= 0 || img.Height <= 0)
                img = Placeholder.Magenta(64, 64);

            int w = img.Width, h = img.Height;
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
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: true);
            tex.name = name + "/Normal";
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.anisoLevel = anisoLevel;

            UploadFlipped(tex, img);
            return tex;
        }

        void RegisterTextureOnce(Texture2D tex)
        {
            if (tex == null || !registeredTextures.Add(tex)) return;
            context?.RegisterTexture(tex);
        }

        static void UploadFlipped(Texture2D tex, DecodedImage img)
        {
            int w = img.Width, h = img.Height;
            var flipped = new byte[img.Rgba.Length];
            int stride = w * 4;
            for (int y = 0; y < h; y++)
                System.Array.Copy(img.Rgba, y * stride, flipped, (h - 1 - y) * stride, stride);

            tex.LoadRawTextureData(flipped);
            tex.Apply(updateMipmaps: true, makeNoLongerReadable: true);
        }
    }
}
