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
        // v7: world redraw seam (2026-08-23) — allowlisted wall textures build
        // their Enhanced albedo from Resources/EnhancedWorld redraws with
        // unquantized mips; packs must not serve stale Super-xBR results for
        // them. NOTE: every wave that adds redraw files must bump this again.
        // v8: wave 1 (2026-08-24) — 18 more redraws (COMP* family + AQCOMP01);
        // v7 packs hold Super-xBR output under those names.
        // v9: redraw level zero goes through SharpenFilter 0.5 like the sprite
        // 4× path — painted edges read soft in the texel-AA crossover zone.
        // v10: redraw mip levels >= 8px sharpen too — players see levels 1–2 at
        // normal wall distance and box-filtered downscales read blurry there.
        // v11: wave 2 (2026-08-24) — 15 more redraws (STARTAN/STARG/STARGR/
        // STARBR panel family + BRICK10/STONE/STONE2/STONE3 masonry); v10 packs
        // hold Super-xBR output under those names.
        // v12: wave 3 (2026-08-24) — 13 more redraws (BROWN rust-metal family
        // + GRAY concrete family; GRAY2/GRAY8/GRAYPOIS await a composition
        // re-roll); v11 packs hold Super-xBR output under those names.
        // v13: wave 4 (2026-08-24) — 23 more redraws (SHAWN2/SUPPORT*/METAL*
        // + door jambs, SLAD/NUKE walls, TEKWALL greebles, crates, big
        // doors); v12 packs hold Super-xBR output under those names.
        // v14: wave-4 tail (2026-08-24) — LITE5/LITEBLU4/LITE3 light strips
        // + the GRAY2/GRAY8/GRAYPOIS composition re-roll; v13 packs hold
        // Super-xBR output under those names.
        // v15: wave 5 (2026-08-25) — the lift set (PLAT1 + STEP2-6/STEPTOP);
        // v14 packs hold Super-xBR output under those names.
        public const int Value = 15;
    }
}
