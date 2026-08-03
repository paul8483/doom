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
                    EnhancedJobKind.PickupSprite => RunPickupSprite(job),
                    EnhancedJobKind.EnemySprite => RunEnemySprite(job),
                    EnhancedJobKind.WeaponSprite => RunWeaponSprite(job),
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
            var enhanced = BuildEnhanced4X(
                job.Native, job.Wrap, job.ApplyDedither, job.ApplyAlphaBleed);
            if (job.ApplySharpen)
                enhanced = SharpenFilter.Apply(enhanced);
            return EnhancedJobResult.OkRgba(kind, enhanced);
        }

        static EnhancedJobResult RunPickupSprite(EnhancedJob job) =>
            EnhancedJobResult.OkRgba(
                EnhancedJobKind.PickupSprite,
                EdgeMixUpscaler.Scale8XContrastGated(job.Native));

        static EnhancedJobResult RunEnemySprite(EnhancedJob job) =>
            EnhancedJobResult.OkRgba(
                EnhancedJobKind.EnemySprite,
                EdgeMixUpscaler.Scale8XContrastGated(job.Native));

        static EnhancedJobResult RunWeaponSprite(EnhancedJob job) =>
            EnhancedJobResult.OkRgba(
                EnhancedJobKind.WeaponSprite,
                EdgeMixUpscaler.Scale8XContrastGated(job.Native));
    }
}
