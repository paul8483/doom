namespace Doom.Graphics
{
    /// Version of the Enhanced CPU transform pipeline. Bump whenever any
    /// transform changes output bytes: DeditherFilter, SuperXbrUpscaler,
    /// EdgeMixUpscaler,
    /// SharpenFilter, HeightMapGenerator, NormalMapGenerator,
    /// PaletteMipGenerator, AlphaBleedGuard, or the stage order in
    /// <see cref="EnhancedJobRunner"/>. Session/disk caches key on this value;
    /// stale packs must not be served after a pipeline change.
    public static class EnhancedPipelineVersion
    {
        // v5: invalidate packs that cached empty/magenta door-track albedos
        // (DOORTRAK etc.) from closed-WAD lazy TextureSet.Build before map-name
        // prewarm + empty-stamp → Placeholder. Standalone disk cache is on;
        // Editor/PlayMode tests keep it off — those suites never saw the poison.
        public const int Value = 5;
    }
}
