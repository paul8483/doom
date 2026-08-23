using System;

namespace Doom.Graphics
{
    /// Kind of Enhanced CPU work unit. World albedo/normal share a texture id;
    /// sprite/HUD are per-patch RGBA (no mip chain).
    public enum EnhancedJobKind
    {
        WorldAlbedo = 0,
        WorldNormal = 1,
        Sprite = 2,
        Hud = 3,
        // 4–6 were EdgeMix Pickup/Enemy/WeaponSprite, removed 2026-08-08.
        // Old packs holding them are invalidated by the pipeline version bump.
    }

    /// Immutable input for <see cref="EnhancedJobRunner"/>. Built on the main
    /// thread from a native decode; workers only read these fields.
    public sealed class EnhancedJob
    {
        public EnhancedJobKind Kind { get; }
        /// Cache/store key fragment (texture name, lump index string, etc.).
        public string ItemId { get; }
        public DecodedImage Native { get; }
        public PixelWrapMode Wrap { get; }
        public bool ApplyDedither { get; }
        public bool ApplyAlphaBleed { get; }
        public bool ApplySharpen { get; }
        public MaterialSurfaceCategory Category { get; }
        /// Material-only flag for sprites; does not affect CPU transforms.
        public bool Spectre { get; }
        public Palette Palette { get; }
        /// Pre-built Enhanced albedo mips — required for <see cref="EnhancedJobKind.WorldNormal"/>.
        public PaletteMipChain AlbedoMips { get; }
        /// Display-grade world redraw (exactly 4× native). When present, the
        /// albedo path uses it as level zero instead of dedither → Super-xBR.
        public DecodedImage Redraw { get; }

        EnhancedJob(
            EnhancedJobKind kind,
            string itemId,
            DecodedImage native,
            PixelWrapMode wrap,
            bool applyDedither,
            bool applyAlphaBleed,
            bool applySharpen,
            MaterialSurfaceCategory category,
            bool spectre,
            Palette palette,
            PaletteMipChain albedoMips,
            DecodedImage redraw = null)
        {
            Kind = kind;
            ItemId = itemId ?? throw new ArgumentNullException(nameof(itemId));
            Native = native;
            Wrap = wrap;
            ApplyDedither = applyDedither;
            ApplyAlphaBleed = applyAlphaBleed;
            ApplySharpen = applySharpen;
            Category = category;
            Spectre = spectre;
            Palette = palette;
            AlbedoMips = albedoMips;
            Redraw = redraw;
        }

        public static EnhancedJob ForWorldAlbedo(
            string itemId,
            DecodedImage native,
            PixelWrapMode wrap,
            bool applyDedither,
            bool applyAlphaBleed,
            Palette palette,
            DecodedImage redraw = null)
        {
            if (native == null) throw new ArgumentNullException(nameof(native));
            if (palette == null) throw new ArgumentNullException(nameof(palette));
            return new EnhancedJob(
                EnhancedJobKind.WorldAlbedo, itemId, native, wrap,
                applyDedither, applyAlphaBleed, applySharpen: false,
                MaterialSurfaceCategory.Unknown, spectre: false, palette,
                albedoMips: null, redraw);
        }

        public static EnhancedJob ForWorldNormal(
            string itemId,
            PaletteMipChain albedoMips,
            MaterialSurfaceCategory category,
            PixelWrapMode wrap)
        {
            if (albedoMips == null) throw new ArgumentNullException(nameof(albedoMips));
            return new EnhancedJob(
                EnhancedJobKind.WorldNormal, itemId, native: null, wrap,
                applyDedither: false, applyAlphaBleed: false, applySharpen: false,
                category, spectre: false, palette: null, albedoMips);
        }

        public static EnhancedJob ForSprite(
            string itemId,
            DecodedImage native,
            bool applyDedither,
            bool applyAlphaBleed = true,
            bool applySharpen = true,
            bool spectre = false)
        {
            if (native == null) throw new ArgumentNullException(nameof(native));
            return new EnhancedJob(
                EnhancedJobKind.Sprite, itemId, native, PixelWrapMode.Clamp,
                applyDedither, applyAlphaBleed, applySharpen,
                MaterialSurfaceCategory.Unknown, spectre, palette: null, albedoMips: null);
        }

        public static EnhancedJob ForHud(
            string itemId,
            DecodedImage native,
            bool applyDedither,
            bool applyAlphaBleed = true,
            bool applySharpen = true)
        {
            if (native == null) throw new ArgumentNullException(nameof(native));
            return new EnhancedJob(
                EnhancedJobKind.Hud, itemId, native, PixelWrapMode.Clamp,
                applyDedither, applyAlphaBleed, applySharpen,
                MaterialSurfaceCategory.Unknown, spectre: false, palette: null, albedoMips: null);
        }

    }

    /// CPU-side result of an Enhanced job. Either success buffers for the kind,
    /// or a failed state with a message (for per-item native fallback).
    public sealed class EnhancedJobResult
    {
        public EnhancedJobKind Kind { get; }
        public bool Success { get; }
        public string ErrorMessage { get; }
        public PaletteMipChain AlbedoMips { get; }
        public PaletteMipChain NormalMips { get; }
        public DecodedImage Rgba { get; }

        EnhancedJobResult(
            EnhancedJobKind kind,
            bool success,
            string errorMessage,
            PaletteMipChain albedoMips,
            PaletteMipChain normalMips,
            DecodedImage rgba)
        {
            Kind = kind;
            Success = success;
            ErrorMessage = errorMessage;
            AlbedoMips = albedoMips;
            NormalMips = normalMips;
            Rgba = rgba;
        }

        public static EnhancedJobResult OkWorldAlbedo(PaletteMipChain mips) =>
            new(EnhancedJobKind.WorldAlbedo, true, null, mips, null, null);

        public static EnhancedJobResult OkWorldNormal(PaletteMipChain mips) =>
            new(EnhancedJobKind.WorldNormal, true, null, null, mips, null);

        public static EnhancedJobResult OkRgba(EnhancedJobKind kind, DecodedImage rgba) =>
            new(kind, true, null, null, null, rgba);

        public static EnhancedJobResult Failed(EnhancedJobKind kind, string errorMessage) =>
            new(kind, false, errorMessage ?? "Enhanced job failed.", null, null, null);
    }
}
