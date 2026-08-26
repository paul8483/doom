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
        // v16: wave 6 (2026-08-25) — every remaining door (BIGDOOR/AQDOOR
        // gates, small doors, exit airlock + EXIT signs, key-lock strips);
        // v15 packs hold Super-xBR output under those names.
        // v17: wave 7 (2026-08-25) — 53 rarities (hell stone/flesh, concrete/
        // brick, metal/moss/tech, wood/crates/lights); v16 packs hold
        // Super-xBR output under those names.
        // v18: ICKWALL2 composition re-roll (2026-08-25) — the last wave-7
        // debt; v17 packs hold Super-xBR output under that name.
        // v19: wave 8 set 1 (2026-08-25) — the aquatex metal family
        // (AQMETL plates/ribs + AQPANL06); v18 packs hold Super-xBR
        // output under those names.
        // v20: wave 8 set 2 (2026-08-25) — aquatex grates/pipes/hazard
        // (AQMETL15/20/21, AQPIPE05/14, AQSECT11); v19 packs hold
        // Super-xBR output under those names.
        // v21: wave 8 set 3 (2026-08-25) — aquatex concrete (AQCONC
        // panels/tiles/bands + the AQTRIM05 siding); v20 packs hold
        // Super-xBR output under those names.
        // v22: wave 8 set 4 (2026-08-25) — aquatex rust strips, supports
        // and the AQLITE18 flicker panel (its capsule grid is pinned to
        // the native cells); v21 packs hold Super-xBR output under those
        // names. Wave 8 complete: the AQ* core (15+ sidedefs) is covered.
        // v23: wave 9 set 1 (2026-08-25) — non-AQ organic/rock leftovers
        // (BROVINE, A-CAMO4, SP_ROCK1, ROCK5; A-VINE3/MC18 held back for
        // a green-identity re-roll); v22 packs hold Super-xBR output
        // under those names.
        // v24: wave 9 set 2 (2026-08-25) — SP_FACE2 hell skulls (the only
        // genuine redraw of the set; NUKESLAD/SLADSKUL/A-MARBLE/EGSUPRT3
        // came back as near-copies and await a re-roll); v23 packs hold
        // Super-xBR output under that name.
        // v25: wave 9 re-rolls (2026-08-26) — the green pair A-VINE3/MC18
        // and the anti-copy set NUKESLAD/A-MARBLE/EGSUPRT3 (SLADSKUL held
        // back again: the skull medallion drifted up); v24 packs hold
        // Super-xBR output under those names.
        // v26: wave 9 closes (2026-08-26) — SLADSKUL lands on the third
        // pass (skull medallion position-pinned); v25 packs hold
        // Super-xBR output under that name.
        // v27: wall mips wrap vertically (2026-08-26) — WrapFor walls went
        // RepeatX -> RepeatXY alongside the Repeat-V wrap fix for tall
        // walls, so v26 packs hold mips with stale clamped edge rows.
        // v28: wave 10 (2026-08-26) — 26 flat redraws (the static floor/
        // ceiling core: hex slabs, concrete, panels/planks, stone/organic,
        // TLITE6_5); v27 packs hold Super-xBR output under those names.
        // v29: masked redraw alpha (2026-08-26) — AQMETL21/AQMETL15 redraws
        // regain the native hole mask (wave 8 shipped them opaque, which
        // filled two-sided grate holes solid), and masked redraws now get
        // AlphaBleedGuard before sharpen/mips; v28 packs hold opaque albedo
        // under those names.
        public const int Value = 29;
    }
}
