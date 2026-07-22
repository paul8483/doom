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

    /// Decodes sprite lumps into cutout Materials, cached by (lump, variant, spectre).
    /// Mirror is NOT baked into the texture — the billboard flips its X scale.
    /// Header dims/offsets always come from the native PatchHeader (placement invariant).
    public sealed class SpriteCache
    {
        /// Test seam: next Enhanced4X build throws and falls back to native.
        public static bool ForceEnhancedFailureForTests;

        private readonly WadFile wad;
        private readonly SpriteSet sprites;
        private readonly Palette palette;
        private readonly DoomMaterialFactory materials;
        private readonly WorldRenderContext context;
        private readonly int anisoLevel;

        private readonly Dictionary<int, PatchHeader> headerByLump = new();
        private readonly Dictionary<int, DecodedImage> decodedByLump = new();
        private readonly Dictionary<(int lump, WorldTextureVariant variant), Texture2D> texByLumpVariant = new();
        private readonly Dictionary<(int lump, WorldTextureVariant variant, bool spectre), Material> matCache = new();
        private readonly HashSet<int> failedLumps = new();
        private readonly HashSet<int> failedEnhancedLumps = new();
        private readonly List<int> nativeLumpOrder = new();

        int enhancedVariantCount;
        long enhancedTextureBytes;

        public DoomMaterialFactory Materials => materials;
        public int EnhancedVariantCount => enhancedVariantCount;
        public long EnhancedTextureBytes => enhancedTextureBytes;
        public int CachedNativeLumpCount => nativeLumpOrder.Count;

        /// Lumps that already have a native texture (warm order). Used for yielded
        /// Enhanced4X warm without re-scanning the map.
        public IReadOnlyList<int> CachedNativeLumps => nativeLumpOrder;

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

        WorldTextureVariant ActiveSpriteVariant =>
            materials.ActiveProfile.SpritesUpscale4X
                ? WorldTextureVariant.Enhanced4X
                : WorldTextureVariant.Native;

        public SpriteMaterial GetSpectre(string sprite, int frame, int rotationIndex) =>
            Get(sprite, frame, rotationIndex, spectre: true);

        /// Pre-warm native decode/material while the WAD is open. Ignores the
        /// active profile so Enhanced Super-xBR never runs during ThingSpawner.
        public SpriteMaterial WarmNative(
            string sprite, int frame, int rotationIndex, bool spectre = false) =>
            Get(sprite, frame, rotationIndex, spectre, WorldTextureVariant.Native);

        /// Resolve (sprite, frame, rotationIndex 0..7) for the active profile.
        /// Returns an invalid SpriteMaterial (IsValid == false) if missing.
        public SpriteMaterial Get(
            string sprite, int frame, int rotationIndex, bool spectre = false) =>
            Get(sprite, frame, rotationIndex, spectre, ActiveSpriteVariant);

        public SpriteMaterial Get(
            string sprite,
            int frame,
            int rotationIndex,
            bool spectre,
            WorldTextureVariant variant)
        {
            if (variant == WorldTextureVariant.Enhanced2X)
                variant = WorldTextureVariant.Enhanced4X;

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

                var tex = GetOrCreateTexture(refr.LumpIndex, variant);
                if (tex == null)
                    return default;

                var key = (refr.LumpIndex, variant, spectre);
                if (!matCache.TryGetValue(key, out mat))
                {
                    mat = materials.CreateSpriteMaterial(tex, spectre);
                    matCache[key] = mat;
                    // Owned for teardown only — do not RegisterMaterial: world
                    // RetargetMaterial would force world cutout shaders;
                    // SpriteBillboard retargets sprites live on mode switch.
                    context?.RegisterOwned(mat);
                }
                else if (mat.mainTexture != tex)
                {
                    mat.mainTexture = tex;
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

        /// Build Enhanced4X for a lump that already has a native decode. Safe after
        /// WAD close. Returns false if native is missing or Enhanced failed.
        public bool EnsureEnhanced(int lumpIndex)
        {
            if (failedLumps.Contains(lumpIndex) || failedEnhancedLumps.Contains(lumpIndex))
                return false;
            if (!decodedByLump.ContainsKey(lumpIndex))
                return false;

            var tex = GetOrCreateTexture(lumpIndex, WorldTextureVariant.Enhanced4X);
            return tex != null &&
                   texByLumpVariant.TryGetValue(
                       (lumpIndex, WorldTextureVariant.Enhanced4X), out var enhanced) &&
                   ReferenceEquals(tex, enhanced);
        }

        Texture2D GetOrCreateTexture(int lumpIndex, WorldTextureVariant variant)
        {
            if (variant == WorldTextureVariant.Enhanced4X &&
                failedEnhancedLumps.Contains(lumpIndex))
                return GetOrCreateTexture(lumpIndex, WorldTextureVariant.Native);

            var key = (lumpIndex, variant);
            if (texByLumpVariant.TryGetValue(key, out var existing))
                return existing;

            if (variant == WorldTextureVariant.Native)
                return CreateNativeTexture(lumpIndex);

            return CreateEnhancedTexture(lumpIndex);
        }

        Texture2D CreateNativeTexture(int lumpIndex)
        {
            var key = (lumpIndex, WorldTextureVariant.Native);
            if (texByLumpVariant.TryGetValue(key, out var existing))
                return existing;

            var img = DecodeLump(lumpIndex);
            if (img == null)
                return null;

            var tex = ToTexture2D(img);
            texByLumpVariant[key] = tex;
            nativeLumpOrder.Add(lumpIndex);
            context?.RegisterTexture(tex);
            return tex;
        }

        Texture2D CreateEnhancedTexture(int lumpIndex)
        {
            var key = (lumpIndex, WorldTextureVariant.Enhanced4X);
            if (texByLumpVariant.TryGetValue(key, out var existing))
                return existing;

            // Native must exist first (decoded + tracked).
            var nativeTex = CreateNativeTexture(lumpIndex);
            if (nativeTex == null)
                return null;

            if (failedEnhancedLumps.Contains(lumpIndex))
                return nativeTex;

            try
            {
                if (ForceEnhancedFailureForTests)
                    throw new System.InvalidOperationException(
                        "Forced Enhanced4X sprite failure (test seam).");

                if (!decodedByLump.TryGetValue(lumpIndex, out var nativeImg) || nativeImg == null)
                    throw new System.InvalidOperationException(
                        "Missing native DecodedImage for Enhanced sprite.");

                var profile = materials.ActiveProfile;
                var job = EnhancedJob.ForSprite(
                    lumpIndex.ToString(),
                    nativeImg,
                    applyDedither: profile.WorldDedither,
                    applyAlphaBleed: true,
                    applySharpen: true);
                var result = EnhancedJobRunner.Run(job);
                if (!result.Success)
                    throw new System.InvalidOperationException(result.ErrorMessage);

                var tex = ToTexture2D(result.Rgba);
                texByLumpVariant[key] = tex;
                context?.RegisterTexture(tex);
                enhancedVariantCount++;
                enhancedTextureBytes += (long)tex.width * tex.height * 4L;
                return tex;
            }
            catch (System.Exception e)
            {
                failedEnhancedLumps.Add(lumpIndex);
                Debug.LogWarning(
                    $"SpriteCache: Enhanced 4× failed for lump {lumpIndex}: {e.Message} — using native");
                return nativeTex;
            }
        }

        DecodedImage DecodeLump(int lumpIndex)
        {
            if (decodedByLump.TryGetValue(lumpIndex, out var cached))
                return cached;

            try
            {
                var img = Patch.Decode(wad.ReadLump(lumpIndex), palette);
                decodedByLump[lumpIndex] = img;
                return img;
            }
            catch (System.ObjectDisposedException)
            {
                failedLumps.Add(lumpIndex);
                throw;
            }
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
            tex.Apply(updateMipmaps: true, makeNoLongerReadable: true);
            return tex;
        }
    }
}
