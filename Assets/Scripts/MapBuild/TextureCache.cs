using System.Collections.Generic;
using UnityEngine;
using Doom.Wad;
using Doom.Graphics;
using Doom.MapBuild.Rendering;

namespace Doom.MapBuild
{
    /// Turns decoded WAD images into Unity Texture2D/Material, cached by name.
    /// Resolution order: composite wall texture (TextureSet) -> flat lump -> magenta.
    /// Native and Enhanced 4× albedo variants share one decoded source per name.
    public sealed class TextureCache
    {
        sealed class SourceEntry
        {
            public DecodedImage Native;
            public DecodedImage Enhanced;
            public PaletteMipChain EnhancedMips;
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
        long nativeTextureBytes;
        long enhancedTextureBytes;
        long normalTextureBytes;

        /// Test seam: when true, Enhanced4X transform throws so fallback can be asserted.
        public static bool ForceEnhancedFailureForTests;

        public int NormalMapCount => normalCache.Count;
        public int EnhancedVariantCount => enhancedVariantCount;
        public long NativeTextureBytes => nativeTextureBytes;
        public long EnhancedTextureBytes => enhancedTextureBytes;
        public long NormalTextureBytes => normalTextureBytes;

        /// True when an albedo for (name, variant) is already in the GPU cache
        /// (including Enhanced4X→native fallback aliases).
        public bool HasCachedVariant(string name, WorldTextureVariant variant)
        {
#pragma warning disable CS0618
            if (variant == WorldTextureVariant.Enhanced2X)
                variant = WorldTextureVariant.Enhanced4X;
#pragma warning restore CS0618
            return !string.IsNullOrEmpty(name) &&
                   texCache.ContainsKey((name, variant));
        }

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
            this.materials.SetWorldAnisoLevel(anisoLevel);
            context?.BindTextureCache(this);
        }

        public Material GetMaterial(string name, bool masked)
        {
            var key = (name, masked);
            if (matCache.TryGetValue(key, out var m)) return m;
            // Always bind native albedo at creation. Enhanced4X (Super-xBR) is built
            // in a yielded warm/ApplyProfile pass — doing it here freezes New Game
            // for minutes while GEOMETRY runs without a frame.
            var tex = GetTexture(name, WorldTextureVariant.Native);
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
            // Obsolete Enhanced2X is no longer a runtime creation path.
#pragma warning disable CS0618
            if (variant == WorldTextureVariant.Enhanced2X)
                variant = WorldTextureVariant.Enhanced4X;
#pragma warning restore CS0618

            var key = (name, variant);
            if (texCache.TryGetValue(key, out var existing))
                return existing;

            EnsureSource(name);

            if (variant == WorldTextureVariant.Enhanced4X)
                return GetOrCreateEnhanced(name);

            return GetOrCreateNative(name);
        }

        /// Lazy normal for Enhanced materials. Built from the Enhanced 4× source.
        public Texture2D GetOrCreateNormal(string name)
        {
            var key = (name, WorldTextureVariant.Enhanced4X);
            if (normalCache.TryGetValue(key, out var existing))
                return existing;

            EnsureSource(name);
            var entry = sourceCache[name];
            var enhancedMips = GetEnhancedMipChain(name, entry);
            if (enhancedMips == null)
                return null;

            if (!categoryByName.TryGetValue(name, out var category))
                category = MaterialSurfaceCategory.Unknown;

            var profile = MaterialSurfaceProfile.For(category);
            var tex = ToNormalTexture2D(enhancedMips, profile, category, name, entry);
            normalCache[key] = tex;
            normalTextureBytes += TextureBytes(tex);
            RegisterTextureOnce(tex);
            context?.RegisterOwned(tex);

            // CPU 4× buffers are no longer needed once albedo + normal are uploaded.
            entry.Enhanced = null;
            entry.EnhancedMips = null;

            return tex;
        }

        /// CPU-side Enhanced4X pipeline (dedither → [bleed] → Super-xBR ×2 ×2).
        /// Exposed for diagnostics and PlayMode assertions before GPU upload.
        public static DecodedImage BuildEnhanced4XDecoded(
            DecodedImage native,
            PixelWrapMode wrap,
            bool applyDedither,
            bool applyAlphaBleed)
        {
            if (native == null)
                throw new System.ArgumentNullException(nameof(native));

            var processed = applyDedither
                ? DeditherFilter.Apply(native, wrap)
                : native;

            if (applyAlphaBleed)
                processed = AlphaBleedGuard.Dilate(processed);

            var x2 = SuperXbrUpscaler.Scale2X(processed, wrap);
            return SuperXbrUpscaler.Scale2X(x2, wrap);
        }

        Texture2D GetOrCreateNative(string name)
        {
            var key = (name, WorldTextureVariant.Native);
            if (texCache.TryGetValue(key, out var existing))
                return existing;

            var entry = sourceCache[name];
            var tex = ToAlbedoTexture2D(entry.Native, name, entry);
            texCache[key] = tex;
            albedoToName[tex] = (name, WorldTextureVariant.Native);
            nativeTextureBytes += TextureBytes(tex);
            RegisterTextureOnce(tex);
            return tex;
        }

        Texture2D GetOrCreateEnhanced(string name)
        {
            var key = (name, WorldTextureVariant.Enhanced4X);
            if (texCache.TryGetValue(key, out var existing))
                return existing;

            var entry = sourceCache[name];
            if (entry.EnhancedFailed)
            {
                // Alias native under the Enhanced key so ApplyProfile does not
                // re-enter the failure path. Native stays registered once.
                var native = GetOrCreateNative(name);
                texCache[key] = native;
                return native;
            }

            try
            {
                var enhancedMips = GetEnhancedMipChain(name, entry);
                var tex = ToAlbedoTexture2D(enhancedMips, name, entry);
                texCache[key] = tex;
                albedoToName[tex] = (name, WorldTextureVariant.Enhanced4X);
                RegisterTextureOnce(tex);
                enhancedVariantCount++;
                enhancedTextureBytes += TextureBytes(tex);

                // Mips only needed further for normal gen; drop if normal ready.
                if (normalCache.ContainsKey(key))
                {
                    entry.Enhanced = null;
                    entry.EnhancedMips = null;
                }

                return tex;
            }
            catch (System.Exception e)
            {
                entry.EnhancedFailed = true;
                entry.Enhanced = null;
                entry.EnhancedMips = null;
                GraphicsLog.Warning(
                    $"TextureCache: Enhanced 4× failed for '{name}': {e.Message} — using native");
                var native = GetOrCreateNative(name);
                texCache[key] = native;
                return native;
            }
        }

        DecodedImage GetEnhancedDecoded(string name, SourceEntry entry)
        {
            if (entry.EnhancedFailed)
                return null;
            if (entry.Enhanced != null)
                return entry.Enhanced;

            if (ForceEnhancedFailureForTests)
                throw new System.InvalidOperationException(
                    "Forced Enhanced4X failure (test seam).");

            var wrap = WrapFor(entry);
            var profile = materials.ActiveProfile;
            bool masked = HasTransparent(entry.Native);
            entry.Enhanced = BuildEnhanced4XDecoded(
                entry.Native,
                wrap,
                applyDedither: profile.WorldDedither,
                applyAlphaBleed: masked);
            return entry.Enhanced;
        }

        PaletteMipChain GetEnhancedMipChain(string name, SourceEntry entry)
        {
            if (entry.EnhancedFailed)
                return null;
            if (entry.EnhancedMips != null)
                return entry.EnhancedMips;

            var enhanced = GetEnhancedDecoded(name, entry);
            entry.EnhancedMips = PaletteMipGenerator.Generate(
                enhanced, palette, WrapFor(entry), preserveAlphaCoverage: true);
            // Decoded 4× RGBA is fully captured in the mip chain.
            entry.Enhanced = null;
            return entry.EnhancedMips;
        }

        static PixelWrapMode WrapFor(SourceEntry entry)
        {
            if (entry.IsPlaceholder)
                return PixelWrapMode.Clamp;
            if (entry.IsFlat)
                return PixelWrapMode.RepeatXY;
            return PixelWrapMode.RepeatX;
        }

        static bool HasTransparent(DecodedImage img)
        {
            var rgba = img.Rgba;
            for (int i = 3; i < rgba.Length; i += 4)
                if (rgba[i] == 0) return true;
            return false;
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
            // Normals match Enhanced 4× albedo only. Native fallback keeps flat normals.
            if (info.variant != WorldTextureVariant.Enhanced4X)
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

        private Texture2D ToAlbedoTexture2D(
            DecodedImage img, string name, SourceEntry entry)
        {
            if (img.Width <= 0 || img.Height <= 0)
                img = Placeholder.Magenta(64, 64);

            int w = img.Width, h = img.Height;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: false);
            tex.name = name;
            ConfigureWrap(tex, entry);
            tex.filterMode = FilterMode.Point;
            tex.anisoLevel = 1;

            UploadFlipped(tex, img, 0);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return tex;
        }

        private Texture2D ToAlbedoTexture2D(
            PaletteMipChain chain, string name, SourceEntry entry)
        {
            var levelZero = chain[0];
            bool hasMips = chain.Count > 1;
            var tex = new Texture2D(
                levelZero.Width, levelZero.Height, TextureFormat.RGBA32,
                mipChain: hasMips, linear: false);
            tex.name = name;
            ConfigureWrap(tex, entry);
            // Controlled mips: Trilinear minification (LOD0 stays sharp via mip content).
            tex.filterMode = hasMips ? FilterMode.Trilinear : FilterMode.Point;
            tex.anisoLevel = hasMips ? anisoLevel : 1;

            for (int level = 0; level < chain.Count; level++)
                UploadFlipped(tex, chain[level], level);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return tex;
        }

        private Texture2D ToNormalTexture2D(
            PaletteMipChain albedoChain,
            MaterialSurfaceProfile profile,
            MaterialSurfaceCategory category,
            string name,
            SourceEntry entry)
        {
            var levelZero = albedoChain[0];
            bool hasMips = albedoChain.Count > 1;
            var tex = new Texture2D(
                levelZero.Width, levelZero.Height, TextureFormat.RGBA32,
                mipChain: hasMips, linear: true);
            tex.name = name + "/Normal";
            ConfigureWrap(tex, entry);
            tex.filterMode = hasMips ? FilterMode.Trilinear : FilterMode.Bilinear;
            tex.anisoLevel = hasMips ? anisoLevel : 1;

            var wrap = WrapFor(entry);
            for (int level = 0; level < albedoChain.Count; level++)
            {
                // Multi-scale height from processed albedo; normals from height;
                // height packed into alpha for POM.
                var height = HeightMapGenerator.Generate(
                    albedoChain[level], category, wrap);
                var normal = NormalMapGenerator.Generate(
                    height, profile.Strength, profile.Wrap);
                UploadFlipped(tex, normal, level);
            }
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return tex;
        }

        static void ConfigureWrap(Texture2D tex, SourceEntry entry)
        {
            if (entry.IsPlaceholder)
            {
                tex.wrapModeU = TextureWrapMode.Clamp;
                tex.wrapModeV = TextureWrapMode.Clamp;
            }
            else if (entry.IsFlat)
            {
                tex.wrapModeU = TextureWrapMode.Repeat;
                tex.wrapModeV = TextureWrapMode.Repeat;
            }
            else
            {
                tex.wrapModeU = TextureWrapMode.Repeat;
                tex.wrapModeV = TextureWrapMode.Clamp;
            }
        }

        void RegisterTextureOnce(Texture2D tex)
        {
            if (tex == null || !registeredTextures.Add(tex)) return;
            context?.RegisterTexture(tex);
        }

        static void UploadFlipped(Texture2D tex, DecodedImage img, int mipLevel)
        {
            int w = img.Width, h = img.Height;
            var flipped = new byte[img.Rgba.Length];
            int stride = w * 4;
            for (int y = 0; y < h; y++)
                System.Array.Copy(img.Rgba, y * stride, flipped, (h - 1 - y) * stride, stride);

            tex.SetPixelData(flipped, mipLevel);
        }

        static long TextureBytes(Texture2D tex)
        {
            long bytes = 0;
            int width = tex.width;
            int height = tex.height;
            for (int level = 0; level < tex.mipmapCount; level++)
            {
                bytes += (long)width * height * 4L;
                width = System.Math.Max(1, width >> 1);
                height = System.Math.Max(1, height >> 1);
            }
            return bytes;
        }
    }
}
