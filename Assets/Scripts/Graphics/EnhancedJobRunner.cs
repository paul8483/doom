using System;

namespace Doom.Graphics
{
    /// Pure Enhanced CPU pipeline: job → result with no Unity API. Single source
    /// of truth for world albedo/normal and sprite/HUD 4× transforms. Safe for
    /// Parallel.ForEach — transforms are static and do not mutate inputs.
    public static class EnhancedJobRunner
    {
        /// Execute one job. Pipeline exceptions become a failed result; null job throws.
        public static EnhancedJobResult Run(EnhancedJob job)
        {
            if (job == null)
                throw new ArgumentNullException(nameof(job));

            try
            {
                return job.Kind switch
                {
                    EnhancedJobKind.WorldAlbedo => RunWorldAlbedo(job),
                    EnhancedJobKind.WorldNormal => RunWorldNormal(job),
                    EnhancedJobKind.Sprite => RunRgba(job, EnhancedJobKind.Sprite),
                    EnhancedJobKind.Hud => RunRgba(job, EnhancedJobKind.Hud),
                    _ => EnhancedJobResult.Failed(job.Kind, $"Unknown job kind {job.Kind}."),
                };
            }
            catch (Exception e)
            {
                return EnhancedJobResult.Failed(job.Kind, e.Message);
            }
        }

        /// Shared 4× albedo path (dedither → [bleed] → Super-xBR ×2 ×2).
        /// Used by caches and diagnostics that need the decoded image before mips.
        public static DecodedImage BuildEnhanced4X(
            DecodedImage native,
            PixelWrapMode wrap,
            bool applyDedither,
            bool applyAlphaBleed)
        {
            if (native == null)
                throw new ArgumentNullException(nameof(native));

            var processed = applyDedither
                ? DeditherFilter.Apply(native, wrap)
                : native;

            if (applyAlphaBleed)
                processed = AlphaBleedGuard.Dilate(processed);

            var x2 = SuperXbrUpscaler.Scale2X(processed, wrap);
            return SuperXbrUpscaler.Scale2X(x2, wrap);
        }

        static EnhancedJobResult RunWorldAlbedo(EnhancedJob job)
        {
            // Display-grade redraw replaces dedither → Super-xBR as level zero.
            // Downsampled levels stay unquantized: the palette decision lives in
            // the level-0 file, and a PLAYPAL snap per level would make a
            // full-color redraw pop at mip transitions. Sharpen matches the
            // sprite 4× path: at ~2 screen px/texel the world texel-AA sits in
            // its bilinear crossover zone and painted edges read soft without it
            // (wave-1 gate finding, 2026-08-24).
            if (job.Redraw != null)
            {
                // Masked redraws (grates, vines) carry real alpha: dilate opaque
                // RGB under the transparent holes first, else bilinear/mips
                // bleed whatever color the author left there into the edges —
                // same guard the native Super-xBR path gets before upscale.
                var source = job.ApplyAlphaBleed
                    ? AlphaBleedGuard.Dilate(job.Redraw)
                    : job.Redraw;
                var sharpened = SharpenFilter.Apply(source);
                var redrawMips = PaletteMipGenerator.Generate(
                    sharpened, job.Palette, job.Wrap,
                    preserveAlphaCoverage: true, quantizeToPalette: false);
                // Players see levels 1–2 at normal wall distance; box-filtered
                // downscales of painted art read blurrier than the Super-xBR
                // chain they replaced, so every usable level gets the same
                // sharpen as level zero (wave-1 mid-range gate finding).
                var levels = new DecodedImage[redrawMips.Count];
                for (int i = 0; i < redrawMips.Count; i++)
                {
                    var level = redrawMips[i];
                    levels[i] = (i > 0 && level.Width >= 8 && level.Height >= 8)
                        ? SharpenFilter.Apply(level)
                        : level;
                }
                return EnhancedJobResult.OkWorldAlbedo(new PaletteMipChain(levels));
            }

            var enhanced = BuildEnhanced4X(
                job.Native, job.Wrap, job.ApplyDedither, job.ApplyAlphaBleed);
            var mips = PaletteMipGenerator.Generate(
                enhanced, job.Palette, job.Wrap, preserveAlphaCoverage: true);
            return EnhancedJobResult.OkWorldAlbedo(mips);
        }

        static EnhancedJobResult RunWorldNormal(EnhancedJob job)
        {
            var albedo = job.AlbedoMips;
            var profile = MaterialSurfaceProfile.For(job.Category);
            var levels = new DecodedImage[albedo.Count];
            for (int level = 0; level < albedo.Count; level++)
            {
                var height = HeightMapGenerator.Generate(
                    albedo[level], job.Category, job.Wrap);
                levels[level] = NormalMapGenerator.Generate(
                    height, profile.Strength, profile.Wrap);
            }

            return EnhancedJobResult.OkWorldNormal(new PaletteMipChain(levels));
        }

        static EnhancedJobResult RunRgba(EnhancedJob job, EnhancedJobKind kind)
        {
            // Display-grade HUD redraw replaces Super-xBR as the finished 4×
            // level (world-albedo pattern, 2026-08-28). It still takes the
            // alpha-bleed dilate (digits and keys carry real holes — bilinear
            // sampling would bleed the author's backdrop into the edges) and
            // the same sharpen the Super-xBR output gets.
            if (job.Redraw != null)
            {
                var source = job.ApplyAlphaBleed
                    ? AlphaBleedGuard.Dilate(job.Redraw)
                    : job.Redraw;
                if (job.ApplySharpen)
                    source = SharpenFilter.Apply(source);
                return EnhancedJobResult.OkRgba(kind, source);
            }

            var enhanced = BuildEnhanced4X(
                job.Native, job.Wrap, job.ApplyDedither, job.ApplyAlphaBleed);
            if (job.ApplySharpen)
                enhanced = SharpenFilter.Apply(enhanced);
            return EnhancedJobResult.OkRgba(kind, enhanced);
        }
    }
}
