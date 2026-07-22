using System;
using System.Collections.Generic;
using UnityEngine;
using Doom.Graphics;
using Doom.MapBuild.Rendering;

namespace Doom.MapBuild
{
    /// Unity Texture2D cache for UI patches. Built from a fully-decoded
    /// <see cref="UiPatchCatalog"/> — never opens the WAD.
    /// Status-bar / face patches follow <see cref="GraphicsProfile.UiUpscale4X"/>;
    /// menus, intermission and title always stay native.
    public sealed class HudTextureCache
    {
        /// Test seam: next Enhanced4X build throws and falls back to native.
        public static bool ForceEnhancedFailureForTests;

        public readonly struct Entry
        {
            public readonly Texture2D Texture;
            /// Native patch dims (DOOM units) — placement never uses Texture size.
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

        sealed class Slot
        {
            public DecodedImage NativeImage;
            public int Width, Height, LeftOffset, TopOffset;
            public Texture2D NativeTex;
            public Texture2D EnhancedTex;
            public bool IsHud;
            public bool EnhancedFailed;
        }

        readonly Dictionary<string, Slot> slots =
            new Dictionary<string, Slot>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> misses =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> hudNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly int anisoLevel;
        readonly WorldRenderContext context;
        readonly bool preferInjectedProfile;
        GraphicsProfile profile;
        int enhancedVariantCount;
        long enhancedTextureBytes;

        public int EnhancedVariantCount => enhancedVariantCount;
        public long EnhancedTextureBytes => enhancedTextureBytes;
        public GraphicsProfile ActiveProfile => ResolveActiveProfile();

        /// Status-bar patch names eligible for Enhanced4X (menus stay native).
        public IEnumerable<string> HudPatchNames
        {
            get
            {
                foreach (var kv in slots)
                    if (kv.Value.IsHud)
                        yield return kv.Key;
            }
        }

        public HudTextureCache(
            UiPatchCatalog catalog,
            int anisoLevel = 1,
            WorldRenderContext context = null,
            GraphicsProfile? profile = null)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            this.anisoLevel = anisoLevel;
            this.context = context;
            preferInjectedProfile = profile.HasValue;
            this.profile = profile ?? GraphicsProfile.Classic;

            foreach (string name in UiPatchCatalog.StatusBarNames)
                hudNames.Add(name);

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

                var slot = new Slot
                {
                    NativeImage = info.Image,
                    Width = info.Width,
                    Height = info.Height,
                    LeftOffset = info.LeftOffset,
                    TopOffset = info.TopOffset,
                    IsHud = hudNames.Contains(info.Name),
                };
                slot.NativeTex = ToTexture2D(info.Image);
                context?.RegisterTexture(slot.NativeTex);
                slots[info.Name] = slot;
            }
        }

        public void SetActiveProfile(GraphicsProfile profile)
        {
            this.profile = profile;
        }

        /// Build Enhanced4X for a status-bar patch that already has a native decode.
        /// No-op for menu/intermission names. Returns false on miss/failure.
        public bool EnsureEnhanced(string name)
        {
            if (string.IsNullOrEmpty(name) || !slots.TryGetValue(name, out var slot))
                return false;
            if (!slot.IsHud || slot.EnhancedFailed)
                return false;

            if (slot.EnhancedTex != null)
                return true;

            if (ForceEnhancedFailureForTests)
            {
                Integrate(name, EnhancedJobResult.Failed(
                    EnhancedJobKind.Hud,
                    "Forced Enhanced4X HUD failure (test seam)."));
                return false;
            }

            if (TryIntegrateFromStore(name))
                return true;

            var job = TryCreateJob(name);
            if (job == null)
                return slot.EnhancedTex != null && !slot.EnhancedFailed;

            var result = EnhancedJobRunner.Run(job);
            Integrate(name, result);
            PublishToStore(name, result);
            return slot.EnhancedTex != null && !slot.EnhancedFailed;
        }

        /// Main-thread: snapshot a HUD Enhanced job, or null if not eligible /
        /// already done / failed.
        public EnhancedJob TryCreateJob(string name)
        {
            if (string.IsNullOrEmpty(name) || !slots.TryGetValue(name, out var slot))
                return null;
            if (!slot.IsHud || slot.EnhancedFailed || slot.EnhancedTex != null)
                return null;
            if (slot.NativeImage == null)
                return null;

            var active = ResolveActiveProfile();
            return EnhancedJob.ForHud(
                name,
                slot.NativeImage,
                applyDedither: active.WorldDedither,
                applyAlphaBleed: true,
                applySharpen: true);
        }

        /// Main-thread: upload Enhanced HUD patch or mark failed fallback.
        public void Integrate(string name, EnhancedJobResult result)
        {
            if (string.IsNullOrEmpty(name) || !slots.TryGetValue(name, out var slot))
                return;
            if (!slot.IsHud || slot.EnhancedFailed || slot.EnhancedTex != null)
                return;

            if (ForceEnhancedFailureForTests ||
                result == null ||
                !result.Success ||
                result.Rgba == null)
            {
                slot.EnhancedFailed = true;
                string msg = ForceEnhancedFailureForTests
                    ? "Forced Enhanced4X HUD failure (test seam)."
                    : (result?.ErrorMessage ?? "Enhanced HUD job failed.");
                Debug.LogWarning(
                    $"HudTextureCache: Enhanced 4× failed for '{name}': {msg} — using native");
                return;
            }

            var tex = ToTexture2D(result.Rgba);
            slot.EnhancedTex = tex;
            context?.RegisterTexture(tex);
            enhancedVariantCount++;
            enhancedTextureBytes += (long)tex.width * tex.height * 4L;
        }

        /// True when an Enhanced (non-fallback) HUD texture exists.
        public bool HasEnhanced(string name) =>
            !string.IsNullOrEmpty(name) &&
            slots.TryGetValue(name, out var slot) &&
            !slot.EnhancedFailed &&
            slot.EnhancedTex != null;

        public bool TryGet(string name, out Entry entry) =>
            TryGet(name, ResolveVariant(name), out entry);

        public bool TryGet(string name, WorldTextureVariant variant, out Entry entry)
        {
            if (string.IsNullOrEmpty(name) || !slots.TryGetValue(name, out var slot))
            {
                entry = default;
                return false;
            }

            if (variant == WorldTextureVariant.Enhanced2X)
                variant = WorldTextureVariant.Enhanced4X;

            // Non-HUD patches never upscale (menus / intermission / title).
            if (!slot.IsHud)
                variant = WorldTextureVariant.Native;

            Texture2D tex = variant == WorldTextureVariant.Enhanced4X
                ? GetOrCreateEnhanced(slot, name)
                : slot.NativeTex;

            entry = new Entry(
                tex, slot.Width, slot.Height, slot.LeftOffset, slot.TopOffset);
            return entry.IsValid;
        }

        public bool IsMiss(string name) =>
            !string.IsNullOrEmpty(name) && misses.Contains(name);

        public bool IsHudPatch(string name) =>
            !string.IsNullOrEmpty(name) && hudNames.Contains(name);

        WorldTextureVariant ResolveVariant(string name)
        {
            if (!IsHudPatch(name))
                return WorldTextureVariant.Native;

            var active = ResolveActiveProfile();
            return active.UiUpscale4X
                ? WorldTextureVariant.Enhanced4X
                : WorldTextureVariant.Native;
        }

        GraphicsProfile ResolveActiveProfile()
        {
            // Tests pin a profile via ctor; runtime follows the live controller.
            if (!preferInjectedProfile && GraphicsModeController.Instance != null)
                return GraphicsModeController.Instance.ActiveProfile;
            return profile;
        }

        Texture2D GetOrCreateEnhanced(Slot slot, string name)
        {
            if (slot.EnhancedFailed)
                return slot.NativeTex;
            if (slot.EnhancedTex != null)
                return slot.EnhancedTex;

            if (ForceEnhancedFailureForTests)
            {
                Integrate(name, EnhancedJobResult.Failed(
                    EnhancedJobKind.Hud,
                    "Forced Enhanced4X HUD failure (test seam)."));
                return slot.NativeTex;
            }

            if (TryIntegrateFromStore(name))
                return slot.EnhancedTex ?? slot.NativeTex;

            var job = TryCreateJob(name);
            if (job == null)
                return slot.EnhancedTex ?? slot.NativeTex;

            var result = EnhancedJobRunner.Run(job);
            Integrate(name, result);
            PublishToStore(name, result);
            return slot.EnhancedTex ?? slot.NativeTex;
        }

        static EnhancedLayerConfig StoreLayers =>
            EnhancedLayerConfig.FromProfile(GraphicsProfile.Enhanced);

        bool TryIntegrateFromStore(string name)
        {
            var store = EnhancedVariantStore.Instance;
            if (string.IsNullOrEmpty(store.BoundWadIdentity)) return false;
            if (!store.TryGet(EnhancedJobKind.Hud, name, StoreLayers, out var stored))
                return false;
            Integrate(name, stored);
            return HasEnhanced(name);
        }

        void PublishToStore(string name, EnhancedJobResult result)
        {
            var store = EnhancedVariantStore.Instance;
            if (string.IsNullOrEmpty(store.BoundWadIdentity)) return;
            store.Publish(EnhancedJobKind.Hud, name, StoreLayers, result);
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
