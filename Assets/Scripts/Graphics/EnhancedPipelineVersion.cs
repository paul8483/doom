namespace Doom.Graphics
{
    /// Version of the Enhanced CPU transform pipeline. Bump whenever any
    /// transform changes output bytes: DeditherFilter, SuperXbrUpscaler,
    /// SharpenFilter, HeightMapGenerator, NormalMapGenerator,
    /// PaletteMipGenerator, AlphaBleedGuard, or the stage order in
    /// <see cref="EnhancedJobRunner"/>. Session/disk caches key on this value;
    /// stale packs must not be served after a pipeline change.
    public static class EnhancedPipelineVersion
    {
        // v6: EdgeMix 8× removed entirely (2026-08-08) — pickup/enemy/weapon
        // sprites render native in Enhanced; job kinds 4–6 no longer exist, so
        // old packs holding them must be discarded wholesale.
        public const int Value = 6;
    }
}
