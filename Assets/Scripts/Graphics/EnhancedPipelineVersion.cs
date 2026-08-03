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
        public const int Value = 4;
    }
}
