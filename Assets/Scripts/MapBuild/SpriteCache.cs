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
        private readonly HashSet<int> pickupLumps = new();
        private readonly HashSet<int> enemyLumps = new();
        private readonly HashSet<int> weaponLumps = new();
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

        WorldTextureVariant ActivePickupVariant(string sprite, int lumpIndex)
        {
            if (!materials.ActiveProfile.SpritesUpscale4X)
                return WorldTextureVariant.Native;

            if (ShouldUseDisplayRedraw(sprite, lumpIndex))
                return WorldTextureVariant.EnhancedDisplayRedraw;

            // No upscaler fallback: without a display redraw the pickup stays
            // native (Point) so Enhanced 2D is judged against crisp WAD art.
            return WorldTextureVariant.Native;
        }

        bool ShouldUseDisplayRedraw(string sprite, int lumpIndex)
        {
            // 3D On → meshes own the presentation; redraw is the 3D Off path.
            if (Enhanced3DObjectsEnabled)
                return false;
            if (lumpIndex < 0 || lumpIndex >= wad.Directory.Count)
                return false;
            string lumpName = wad.Directory[lumpIndex].Name;
            if (!DisplayRedrawAllowlist.Contains(lumpName))
                return false;
            // BAR1 A/B blink etc.: partial redraw coverage would flicker.
            if (sprites.CountFrames(sprite) > 1)
                return false;
            return true;
        }

        static bool Enhanced3DObjectsEnabled =>
            SettingsController.Instance == null ||
            SettingsController.Instance.Current.Enhanced3DObjects;

        /// Pickups, enemies and first-person weapons render native in Enhanced
        /// (EdgeMix 8× removed 2026-08-08); only unregistered sprites
        /// (projectiles/effects/decorations) keep the Super-xBR 4× path.
        bool IsNativeOnlyLump(int lumpIndex) =>
            pickupLumps.Contains(lumpIndex) ||
            enemyLumps.Contains(lumpIndex) ||
            weaponLumps.Contains(lumpIndex);

        WorldTextureVariant EnhancedVariantForLump(int lumpIndex) =>
            IsNativeOnlyLump(lumpIndex)
                ? WorldTextureVariant.Native
                : WorldTextureVariant.Enhanced4X;

        public EnhancedJobKind EnhancedKindForLump(int lumpIndex) =>
            EnhancedJobKind.Sprite;

        public SpriteMaterial GetSpectre(string sprite, int frame, int rotationIndex) =>
            Get(sprite, frame, rotationIndex, spectre: true);

        /// Pre-warm native decode/material while the WAD is open. Ignores the
        /// active profile so Enhanced Super-xBR never runs during ThingSpawner.
        public SpriteMaterial WarmNative(
            string sprite, int frame, int rotationIndex, bool spectre = false) =>
            Get(sprite, frame, rotationIndex, spectre, WorldTextureVariant.Native);

        /// Register and pre-warm a world pickup frame. Registered lumps render
        /// native in Enhanced unless a display-grade redraw covers them.
        public SpriteMaterial WarmNativePickup(string sprite, int frame, int rotationIndex)
        {
            RegisterPickupLump(sprite, frame, rotationIndex);
            return Get(sprite, frame, rotationIndex, spectre: false, WorldTextureVariant.Native);
        }

        /// Register and pre-warm an enemy frame. Registered lumps render native
        /// in Enhanced; the native patch header still controls placement.
        public SpriteMaterial WarmNativeEnemy(
            string sprite, int frame, int rotationIndex, bool spectre = false)
        {
            RegisterEnemyLump(sprite, frame, rotationIndex);
            return Get(sprite, frame, rotationIndex, spectre, WorldTextureVariant.Native);
        }

        /// Register and pre-warm a first-person weapon / flash frame. Registered
        /// lumps render native in Enhanced; placement stays native-header based.
        public SpriteMaterial WarmNativeWeapon(string sprite, int frame, int rotationIndex = 0)
        {
            RegisterWeaponLump(sprite, frame, rotationIndex);
            return Get(sprite, frame, rotationIndex, spectre: false, WorldTextureVariant.Native);
        }

        /// Resolve (sprite, frame, rotationIndex 0..7) for the active profile.
        /// Returns an invalid SpriteMaterial (IsValid == false) if missing.
        public SpriteMaterial Get(
            string sprite, int frame, int rotationIndex, bool spectre = false) =>
            Get(sprite, frame, rotationIndex, spectre, ActiveSpriteVariant);

        public SpriteMaterial GetPickup(
            string sprite, int frame, int rotationIndex)
        {
            RegisterPickupLump(sprite, frame, rotationIndex);
            if (!sprites.TryGet(sprite, frame, rotationIndex, out var refr))
                return default;
            return Get(sprite, frame, rotationIndex, spectre: false,
                ActivePickupVariant(sprite, refr.LumpIndex));
        }

        public SpriteMaterial GetEnemy(
            string sprite, int frame, int rotationIndex, bool spectre = false)
        {
            RegisterEnemyLump(sprite, frame, rotationIndex);
            return Get(sprite, frame, rotationIndex, spectre, WorldTextureVariant.Native);
        }

        public SpriteMaterial GetWeapon(string sprite, int frame, int rotationIndex = 0)
        {
            RegisterWeaponLump(sprite, frame, rotationIndex);
            return Get(sprite, frame, rotationIndex, spectre: false, WorldTextureVariant.Native);
        }

        void RegisterPickupLump(string sprite, int frame, int rotationIndex)
        {
            if (sprites.TryGet(sprite, frame, rotationIndex, out var refr))
                pickupLumps.Add(refr.LumpIndex);
        }

        void RegisterEnemyLump(string sprite, int frame, int rotationIndex)
        {
            if (sprites.TryGet(sprite, frame, rotationIndex, out var refr))
                enemyLumps.Add(refr.LumpIndex);
        }

        void RegisterWeaponLump(string sprite, int frame, int rotationIndex)
        {
            if (sprites.TryGet(sprite, frame, rotationIndex, out var refr))
                weaponLumps.Add(refr.LumpIndex);
        }

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

            if (variant == WorldTextureVariant.EnhancedDisplayRedraw)
                pickupLumps.Add(refr.LumpIndex);
            // Display-redraw is requested explicitly; registered pickup/enemy/
            // weapon lumps remap to Native, everything else to Enhanced4X.
            if (variant != WorldTextureVariant.Native &&
                variant != WorldTextureVariant.EnhancedDisplayRedraw)
                variant = EnhancedVariantForLump(refr.LumpIndex);

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
            if (IsNativeOnlyLump(lumpIndex))
                return false;
            if (failedLumps.Contains(lumpIndex) || failedEnhancedLumps.Contains(lumpIndex))
                return false;
            if (!decodedByLump.ContainsKey(lumpIndex))
                return false;

            var key = (lumpIndex, EnhancedVariantForLump(lumpIndex));
            if (texByLumpVariant.ContainsKey(key))
                return true;

            if (ForceEnhancedFailureForTests)
            {
                Integrate(lumpIndex, EnhancedJobResult.Failed(
                    EnhancedKindForLump(lumpIndex),
                    "Forced Enhanced4X sprite failure (test seam)."));
                return false;
            }

            if (TryIntegrateFromStore(lumpIndex))
                return true;

            // Never run Super-xBR synchronously while a scheduler warm is in
            // flight: per-frame lazy builds starve the frame loop and stretch
            // the warm itself. The warm integrates this lump; callers retry.
            if (EnhancedWarmScheduler.ActiveWarmCount > 0)
                return false;

            var job = TryCreateJob(lumpIndex);
            if (job == null)
                return texByLumpVariant.ContainsKey(key);

            var result = EnhancedJobRunner.Run(job);
            Integrate(lumpIndex, result);
            PublishToStore(lumpIndex, result);
            return texByLumpVariant.TryGetValue(key, out var enhanced) &&
                   enhanced != null &&
                   !failedEnhancedLumps.Contains(lumpIndex);
        }

        /// Main-thread: snapshot a sprite Enhanced job, or null if already done /
        /// failed / native missing.
        public EnhancedJob TryCreateJob(int lumpIndex)
        {
            // Pickups/enemies/weapons render native — no Enhanced CPU work.
            if (IsNativeOnlyLump(lumpIndex))
                return null;
            if (failedLumps.Contains(lumpIndex) || failedEnhancedLumps.Contains(lumpIndex))
                return null;

            var key = (lumpIndex, EnhancedVariantForLump(lumpIndex));
            if (texByLumpVariant.ContainsKey(key))
                return null;

            if (!decodedByLump.TryGetValue(lumpIndex, out var nativeImg) || nativeImg == null)
                return null;

            // Native GPU entry must exist for tracking / fallback.
            if (CreateNativeTexture(lumpIndex) == null)
                return null;

            var profile = materials.ActiveProfile;
            return EnhancedJob.ForSprite(
                lumpIndex.ToString(),
                nativeImg,
                applyDedither: profile.WorldDedither,
                applyAlphaBleed: true,
                applySharpen: true);
        }

        /// Main-thread: upload Enhanced sprite or mark failed fallback.
        public void Integrate(int lumpIndex, EnhancedJobResult result)
        {
            var variant = EnhancedVariantForLump(lumpIndex);
            var key = (lumpIndex, variant);
            if (texByLumpVariant.ContainsKey(key)) return;
            if (failedEnhancedLumps.Contains(lumpIndex)) return;

            if (ForceEnhancedFailureForTests ||
                result == null ||
                !result.Success ||
                result.Rgba == null)
            {
                failedEnhancedLumps.Add(lumpIndex);
                string msg = ForceEnhancedFailureForTests
                    ? "Forced Enhanced4X sprite failure (test seam)."
                    : (result?.ErrorMessage ?? "Enhanced sprite job failed.");
                Debug.LogWarning(
                    $"SpriteCache: Enhanced {variant} failed for lump {lumpIndex}: {msg} — using native");
                return;
            }

            var tex = ToTexture2D(result.Rgba);
            texByLumpVariant[key] = tex;
            context?.RegisterTexture(tex);
            enhancedVariantCount++;
            enhancedTextureBytes += (long)tex.width * tex.height * 4L;
        }

        /// True when the lump needs no further Enhanced work: its variant texture
        /// exists (native counts for native-only pickup/enemy/weapon lumps).
        public bool HasEnhanced(int lumpIndex) =>
            !failedEnhancedLumps.Contains(lumpIndex) &&
            texByLumpVariant.ContainsKey((lumpIndex, EnhancedVariantForLump(lumpIndex)));

        Texture2D GetOrCreateTexture(int lumpIndex, WorldTextureVariant variant)
        {
            if (variant == WorldTextureVariant.EnhancedDisplayRedraw)
                return CreateDisplayRedrawTexture(lumpIndex);

            if (variant != WorldTextureVariant.Native &&
                failedEnhancedLumps.Contains(lumpIndex))
                return GetOrCreateTexture(lumpIndex, WorldTextureVariant.Native);

            var key = (lumpIndex, variant);
            if (texByLumpVariant.TryGetValue(key, out var existing))
                return existing;

            if (variant == WorldTextureVariant.Native)
                return CreateNativeTexture(lumpIndex);

            return CreateEnhancedTexture(lumpIndex, variant);
        }

        Texture2D CreateDisplayRedrawTexture(int lumpIndex)
        {
            var key = (lumpIndex, WorldTextureVariant.EnhancedDisplayRedraw);
            if (texByLumpVariant.TryGetValue(key, out var existing))
                return existing;

            if (lumpIndex < 0 || lumpIndex >= wad.Directory.Count)
                return CreateNativeTexture(lumpIndex);

            string lumpName = wad.Directory[lumpIndex].Name;
            if (!DisplayRedrawAllowlist.Contains(lumpName))
                return CreateNativeTexture(lumpIndex);

            var resource = Resources.Load<Texture2D>(
                DisplayRedrawAllowlist.ResourcesPath(lumpName));
            if (resource == null)
            {
                Debug.LogWarning(
                    $"SpriteCache: missing EnhancedSprites resource for {lumpName}");
                return CreateNativeTexture(lumpIndex);
            }

            var header = headerByLump.TryGetValue(lumpIndex, out var h)
                ? h
                : Patch.ReadHeader(wad.ReadLump(lumpIndex));
            headerByLump[lumpIndex] = header;

            var canvas = TextureToDecodedTopDown(resource);
            var subject = DisplayRedrawRegistration.ExtractSubjectRect(
                canvas, header.Width, header.Height);
            var tex = ToTexture2D(subject, forcePointFilter: true);
            texByLumpVariant[key] = tex;
            context?.RegisterTexture(tex);
            enhancedVariantCount++;
            enhancedTextureBytes += (long)tex.width * tex.height * 4L;
            return tex;
        }

        static DecodedImage TextureToDecodedTopDown(Texture2D tex)
        {
            // Resources textures may be non-readable; blit via RenderTexture if needed.
            Texture2D readable = tex;
            RenderTexture rt = null;
            if (!tex.isReadable)
            {
                rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
                UnityEngine.Graphics.Blit(tex, rt);
                readable = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                readable.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
                readable.Apply(false);
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }

            try
            {
                var pixels = readable.GetPixels32();
                int w = readable.width, h = readable.height;
                var rgba = new byte[w * h * 4];
                for (int y = 0; y < h; y++)
                {
                    int srcRow = (h - 1 - y) * w;
                    int dstRow = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        Color32 c = pixels[srcRow + x];
                        int o = (dstRow + x) * 4;
                        rgba[o] = c.r;
                        rgba[o + 1] = c.g;
                        rgba[o + 2] = c.b;
                        rgba[o + 3] = c.a;
                    }
                }
                return new DecodedImage(w, h, rgba);
            }
            finally
            {
                if (readable != tex)
                    Object.Destroy(readable);
            }
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

        Texture2D CreateEnhancedTexture(int lumpIndex, WorldTextureVariant requestedVariant)
        {
            var expectedVariant = EnhancedVariantForLump(lumpIndex);
            if (requestedVariant != expectedVariant)
                requestedVariant = expectedVariant;
            var key = (lumpIndex, requestedVariant);
            if (texByLumpVariant.TryGetValue(key, out var existing))
                return existing;

            // Native must exist first (decoded + tracked).
            var nativeTex = CreateNativeTexture(lumpIndex);
            if (nativeTex == null)
                return null;

            if (failedEnhancedLumps.Contains(lumpIndex))
                return nativeTex;

            if (ForceEnhancedFailureForTests)
            {
                Integrate(lumpIndex, EnhancedJobResult.Failed(
                    EnhancedKindForLump(lumpIndex),
                    "Forced Enhanced4X sprite failure (test seam)."));
                return nativeTex;
            }

            if (TryIntegrateFromStore(lumpIndex))
            {
                if (texByLumpVariant.TryGetValue(key, out existing))
                    return existing;
                return nativeTex;
            }

            // Serve native while a scheduler warm runs (see EnsureEnhanced) —
            // billboards re-Get every frame, so the material self-heals to the
            // Enhanced texture right after the warm integrates it.
            if (EnhancedWarmScheduler.ActiveWarmCount > 0)
                return nativeTex;

            var job = TryCreateJob(lumpIndex);
            if (job == null)
            {
                if (texByLumpVariant.TryGetValue(key, out existing))
                    return existing;
                return nativeTex;
            }

            var result = EnhancedJobRunner.Run(job);
            Integrate(lumpIndex, result);
            PublishToStore(lumpIndex, result);
            if (texByLumpVariant.TryGetValue(key, out existing))
                return existing;
            return nativeTex;
        }

        /// Store key layers derived from the same profile TryCreateJob reads,
        /// so published content always matches its key (capture profiles too).
        public EnhancedLayerConfig StoreLayers =>
            EnhancedLayerConfig.FromProfile(materials.ActiveProfile);

        bool TryIntegrateFromStore(int lumpIndex)
        {
            string itemId = lumpIndex.ToString();
            var kind = EnhancedKindForLump(lumpIndex);
            var store = EnhancedVariantStore.Instance;
            bool storeBound = !string.IsNullOrEmpty(store.BoundWadIdentity);
            EnhancedJobResult stored = null;

            // Lazy-build resolution: session store first, then the disk pack (a
            // disk hit is promoted into the store so later lookups stay cheap).
            bool hit = storeBound &&
                store.TryGet(kind, itemId, StoreLayers, out stored);
            if (!hit && EnhancedDiskCache.Enabled &&
                EnhancedDiskCache.Instance.TryGet(
                    kind, itemId, StoreLayers, out stored))
            {
                hit = true;
                if (storeBound)
                    store.Publish(kind, itemId, StoreLayers, stored);
            }

            if (!hit) return false;
            Integrate(lumpIndex, stored);
            return HasEnhanced(lumpIndex);
        }

        void PublishToStore(int lumpIndex, EnhancedJobResult result)
        {
            string itemId = lumpIndex.ToString();
            var kind = EnhancedKindForLump(lumpIndex);
            var store = EnhancedVariantStore.Instance;
            if (!string.IsNullOrEmpty(store.BoundWadIdentity))
                store.Publish(kind, itemId, StoreLayers, result);
            if (EnhancedDiskCache.Enabled)
                EnhancedDiskCache.Instance.Publish(
                    kind, itemId, StoreLayers, result);
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

        private Texture2D ToTexture2D(DecodedImage img, bool forcePointFilter = false)
        {
            int w = Mathf.Max(1, img.Width), h = Mathf.Max(1, img.Height);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = forcePointFilter ? FilterMode.Point : materials.WorldFilterMode;
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
