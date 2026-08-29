namespace Doom.Graphics
{
    /// Wall/flat texture names whose Enhanced world albedo is a display-grade
    /// GPT redraw (Resources/EnhancedWorld/&lt;NAME&gt;.png, exactly 4× native)
    /// instead of the Super-xBR upscale. Provenance lives in
    /// Textures/WorldRedraw/&lt;NAME&gt;/ (native export, prompt, redraw).
    /// Spec: docs/superpowers/specs/2026-08-23-enhanced-computer-textures-design.md.
    public static class WorldRedrawAllowlist
    {
        public const string ResourcesFolder = "EnhancedWorld";

        /// Redraws are authored at exactly this multiple of the native
        /// composite texture size (the slot Super-xBR 4× occupies today).
        public const int Scale = 4;

        /// Pilot 2026-08-23: full-color variant chosen on the bylo/stalo panel
        /// (PLAYPAL quant kept in provenance). Wave 1 2026-08-24: the rest of
        /// the COMP* family + the aquatex computer AQCOMP01 (AQPANL* examined
        /// and excluded — riveted metal, not computers). Wave 2 2026-08-24:
        /// the STARTAN/STARG/STARGR/STARBR panel family (every variant used on
        /// E1) + masonry BRICK10/STONE/STONE2/STONE3 (BROWN* examined and
        /// deferred — riveted rust metal, not brick). Wave 3 2026-08-24: the
        /// BROWN rust-metal family + the GRAY concrete family (GRAY2/GRAY8/
        /// GRAYPOIS re-rolled after a composition drift and installed with
        /// the wave-4 tail). Wave 4 2026-08-24: metal/supports/door jambs,
        /// slad+nukage walls, TEKWALL greebles, crates, the big doors, and —
        /// as a separate mini-set — the LITE* light strips (they sit in the
        /// lamp-flicker allowlist, which gates on texel luma, so their redraw
        /// pins the segment grid and the lit/dark contrast; flicker QA rides
        /// the interactive gate). Wave 5 2026-08-25: the lift set — PLAT1
        /// lift front + the STEP2-6/STEPTOP step lips (user screenshot;
        /// MIDGRATE examined and excluded — masked texture, the redraw path
        /// is opaque-only). Wave 6 2026-08-25: every remaining door — the
        /// BIGDOOR/AQDOOR gates, small doors, the exit airlock and EXIT
        /// signs (native AGM/EXIT lettering reproduced), and the DOORBLU/
        /// DOORRED/DOORYEL key-lock strips whose lamp colors are a gameplay
        /// signal. Wave 7 2026-08-25: the rarities — hell stone and flesh
        /// (GSTONE/SKIN/SKSNAKE/ASHWALL, the SK_LEFT+SK_RIGHT carved panel
        /// authored as one composition), concrete/brick (ICKWALL/CEMENT/
        /// A-* Freedoom bricks; ICKWALL2 failed composition in the first
        /// pass and was re-rolled 2026-08-25 — two large recessed panels,
        /// matching the native), metal/moss/tech (BRONZE/SHAWN/MC/TEKWALL leftovers,
        /// PIPE2, the PLANET1 space screens), wood, the small crates, and
        /// the remaining LITE strips. The AQ* aquatex family, animated
        /// falls, masked mid-textures, and SW1/SW2 switches stay excluded.
        /// Wave 8 2026-08-25: the aquatex core (every AQ* with 15+ E1
        /// sidedefs; the sub-15 tail stays on Super-xBR by user decision) —
        /// set 1 is the metal family (AQMETL plates/grates/ribs + the
        /// riveted AQPANL06); set 2 is grates/pipes/hazard (the AQMETL15/
        /// 20/21 dark grates, AQPIPE conduits and hazard rows, and the
        /// AQSECT11 composite with its white light bars); set 3 is the
        /// concrete family (AQCONC panels, tiles, bands and the interlock
        /// AQCONC16, plus the AQTRIM05 siding); set 4 is rust strips and
        /// supports (AQRUST, the AQSUPP poles/ladder) + the AQLITE18
        /// light panel, whose capsule grid is pinned to the native cells
        /// for the lamp flicker.
        /// Wave 9 2026-08-25: the non-AQ leftover walls. Set 1 organic/
        /// rock: BROVINE vined bark, A-CAMO4 moss, SP_ROCK1 layered rock,
        /// ROCK5 boulder slabs; A-VINE3 and MC18 failed the green-identity
        /// check (vine/moss texels faded to brown, 20%->7% and 25%->2%
        /// green) and await a re-roll. BRNSMAL1/2 and CEILVINE measured
        /// as true masked mid-textures (26-74% uncovered texels) and stay
        /// excluded with the other masked grates. Set 2 slag/hell: only
        /// SP_FACE2 survived — NUKESLAD/SLADSKUL/A-MARBLE/EGSUPRT3 came
        /// back as lightly-smoothed copies of the input (37-51% of texels
        /// pixel-identical to native, HF energy below native) and await
        /// an anti-copy re-roll. Re-rolls 2026-08-26: the green pair
        /// (A-VINE3/MC18) came back green (23%/26% green texels) and the
        /// anti-copy four came back genuine (copy 0.1-8%, HF above
        /// native) — all installed except SLADSKUL, whose skull medallion
        /// drifted up (eyes 60%->45% height) and awaits a position-pinned
        /// re-roll. The position-pinned third pass landed 2026-08-26
        /// (eyes back at 60%, medallion in the native band) — wave 9
        /// closed with all 11 opaque leftovers installed.
        /// Wave 10 2026-08-26: the first FLAT wave — the static floor/
        /// ceiling core (every non-animated flat with 50+ E1 sector
        /// surfaces, ~4000 surfaces) plus SLIME14 (static despite the
        /// name; only SLIME01-12 animate) and CRATOP2 (paired with
        /// CRATOP1 — crate lids match the accepted CRATE walls). Four
        /// material sets: hex slabs/tiles, concrete/gravel, panels/
        /// planks/metal, stone/organic + the TLITE6_5 lamp plate whose
        /// four bulbs are position-pinned for the lamp flicker.
        /// F_SKY1 and the animated fluids stay excluded.
        /// Wave 11 2026-08-26: the masked mid-textures — grates, bars,
        /// lattice, barbed wire, the broken window, hanging vines and the
        /// BRNSMAL wall stubs. First redraws with REAL alpha: every set
        /// came back with the native hole mask texel-exact (transparent
        /// fraction identical to native), binary alpha, no feathering;
        /// masked redraws pass AlphaBleedGuard before mips.
        /// Wave 12 2026-08-27: every switch pair used on E1 — 18 SW1/SW2
        /// pairs (36 textures), authored as one composition per pair
        /// (only the actuator changes: levers, buttons, screen panels,
        /// the SW1BLUE skull whose eyes light). Pair identity outside
        /// the actuator zone is enforced deterministically
        /// (Tools/enforce_switch_pair.py) so a press cannot pop the
        /// wall. Ships with the P_ChangeSwitchTexture port that makes
        /// SW2 states reachable at all.
        /// Wave 13 2026-08-27: the flat tail (15-49 E1 surfaces, ~690) —
        /// organic/rock (RROCK*, GRASS1, gravel), panels/planks/treads
        /// (incl. the perforated FCGRATE2 and STEP1_F/STEP2_F, the FLAT
        /// namespace aliases of the vanilla STEP flats that collide with
        /// wall-texture names — see TextureCache.FlatKey), dark AQF tech
        /// weaves + the navy CEIL4_2, and the light plates TLITE6_6/
        /// TLITE6_1/FLAT2 (bulb grids position-pinned for the lamp
        /// flicker) + the FLAT22 blue dome. Every non-animated flat
        /// with 15+ E1 surfaces is now covered.
        /// Wave 14 2026-08-29: the AQ wall tail (every aquatex wall under
        /// 15 E1 sidedefs) — installed 23 of the 51-lump scan: riveted
        /// AQPANL panels with machinery cutouts (per-channel tone match
        /// restored the olive-green backing the generation greyed out),
        /// AQPIPE ducts/pipe stacks, AQRUST shutters and streaked walls,
        /// the octagon AQTILE pair, and 9 masked lumps with the native
        /// hole mask re-applied texel-exact after seam healing (chains
        /// AQMETL17, edge-grid fragments AQMETL24/25/27, mesh AQMETL26,
        /// slat rows AQMETL02/29/31, the AQDIRT03 ground fringe). The
        /// AQMETL opaque-grille set and the AQSUPP support set first came
        /// back as smoothed copies (HF energy 0.46-0.99 of native) and
        /// were re-rolled with the anti-copy prompt (HF 1.0-2.0 after);
        /// AQPANL09 (stripe density/color) and AQSECT07 (two light bars
        /// merged, staggered cells regularized) were re-rolled with
        /// percent-pinned composition; the AQLITE flicker five shipped
        /// with capsule grids pinned texel-exact (IoU 0.87-1.00, centroid
        /// shift ~0). The AQCONC/AQTRIM eight ship as the first
        /// generation by user decision (composition and seams clean;
        /// the anti-copy re-roll never landed on disk). Every AQ* wall
        /// used on E1 is now covered.
        /// Wave 15 2026-08-30: the animated walls — every animated wall
        /// texture used on E1 (20 lumps, ~71 sidedefs). The falls
        /// (SFALL/WFALL/BFALL) draw ONLY frame 1; frames 2-4 are the
        /// anchor rolled down 128 px per frame (Tools/make_fall_frames.py)
        /// so the 4-frame loop closes exactly and every frame shares the
        /// same texels — inter-frame consistency by construction (natives:
        /// WFALL is a true 32 px/frame scroll, SFALL/BFALL boil in place;
        /// the synthesized scroll replaces the boil with steady flow by
        /// the wave's approach decision). ROCKRED/SLADRIP animate in
        /// place: frames 2-3 are pinned to the frame-1 anchor outside the
        /// dilated native change zone (Tools/enforce_anim_frames.py, the
        /// switch-pair instrument generalized; outside-diff 0.00% on all
        /// four). FIREBLU is a chaos pair drawn in one session; native
        /// flips just as hard. Ships with WFALL joining IsFluid — it was
        /// the one waterfall without cross-fade and the fluid shader.
        /// Names are WAD texture/flat names.
        public static readonly string[] Names =
        {
            "A-BRICK3",
            "A-CAMO4",
            "A-CONCTE",
            "A-MARBLE",
            "A-DBRI28",
            "A-DROCK1",
            "A-VINE3",
            "AQCOMP01",
            "AQCONC04",
            "AQCONC05",
            "AQCONC06",
            "AQCONC07",
            "AQCONC08",
            "AQCONC10",
            "AQCONC11",
            "AQCONC13",
            "AQCONC14",
            "AQCONC15",
            "AQCONC16",
            "AQCONC18",
            "AQDIRT03",
            "AQDOOR01",
            "AQDOOR02",
            "AQF024",
            "AQF025",
            "AQF028",
            "AQF032",
            "AQF054",
            "AQLITE01",
            "AQLITE07",
            "AQLITE08",
            "AQLITE15",
            "AQLITE17",
            "AQLITE18",
            "AQMETL01",
            "AQMETL02",
            "AQMETL03",
            "AQMETL06",
            "AQMETL07",
            "AQMETL09",
            "AQMETL10",
            "AQMETL11",
            "AQMETL12",
            "AQMETL13",
            "AQMETL14",
            "AQMETL15",
            "AQMETL17",
            "AQMETL20",
            "AQMETL21",
            "AQMETL24",
            "AQMETL25",
            "AQMETL26",
            "AQMETL27",
            "AQMETL28",
            "AQMETL29",
            "AQMETL30",
            "AQMETL31",
            "AQMETL32",
            "AQMETL33",
            "AQPANL01",
            "AQPANL02",
            "AQPANL04",
            "AQPANL05",
            "AQPANL06",
            "AQPANL08",
            "AQPANL09",
            "AQPIPE01",
            "AQPIPE02",
            "AQPIPE05",
            "AQPIPE06",
            "AQPIPE08",
            "AQPIPE09",
            "AQPIPE14",
            "AQRUST02",
            "AQRUST05",
            "AQRUST09",
            "AQRUST10",
            "AQSECT07",
            "AQSECT11",
            "AQSUPP01",
            "AQSUPP04",
            "AQSUPP05",
            "AQSUPP06",
            "AQSUPP08",
            "AQSUPP09",
            "AQSUPP10",
            "AQSUPP11",
            "AQSUPP12",
            "AQSUPP13",
            "AQTILE01",
            "AQTILE02",
            "AQTRIM02",
            "AQTRIM05",
            "AQTRIM07",
            "ASHWALL",
            "ASHWALL2",
            "ASHWALL4",
            "BASE",
            "BASE2",
            "BFALL1",
            "BFALL2",
            "BFALL3",
            "BFALL4",
            "BIGBRIK2",
            "BIGDOOR1",
            "BIGDOOR2",
            "BIGDOOR3",
            "BIGDOOR4",
            "BIGDOOR6",
            "BRICK10",
            "BRNSMAL1",
            "BRNSMAL2",
            "BRONZE1",
            "BRONZE3",
            "BROVINE",
            "BROWN1",
            "BROWN144",
            "BROWN96",
            "BROWNGRN",
            "BROWNHUG",
            "BROWNPIP",
            "CEIL3_3",
            "CEIL3_5",
            "CEIL4_2",
            "CEIL5_1",
            "CEIL5_2",
            "CEIL5_3",
            "CEILVINE",
            "CEMENT1",
            "CEMENT3",
            "CEMENT6",
            "CEMENT7",
            "CEMENT8",
            "COMP2",
            "COMPBLUE",
            "COMPLIT3",
            "COMPOHSO",
            "COMPSPAN",
            "COMPSTA1",
            "COMPSTA2",
            "COMPTALL",
            "COMPTILE",
            "COMPUTE1",
            "COMPUTE2",
            "COMPUTE3",
            "COMPUTE4",
            "COMPVENT",
            "COMPWERA",
            "COMPWERB",
            "COMPWERD",
            "COMPWERE",
            "COMPWERF",
            "CRATE1",
            "CRATE2",
            "CRATE3",
            "CRATELIT",
            "CRATINY",
            "CRATOP1",
            "CRATOP2",
            "CRATWIDE",
            "DOBWIRE",
            "DOOR1",
            "DOOR3",
            "DOORBLU",
            "DOORHI",
            "DOORRED",
            "DOORSTOP",
            "DOORTRAK",
            "DOORYEL",
            "EGSUPRT3",
            "EXITDOOR",
            "EXITSGN2",
            "EXITSIGN",
            "FCGRATE2",
            "FIREBLU1",
            "FIREBLU2",
            "FLAT1",
            "FLAT10",
            "FLAT14",
            "FLAT18",
            "FLAT19",
            "FLAT2",
            "FLAT20",
            "FLAT22",
            "FLAT23",
            "FLAT3",
            "FLAT5",
            "FLAT5_4",
            "FLAT5_5",
            "FLAT8",
            "FLOOR0_1",
            "FLOOR0_2",
            "FLOOR0_3",
            "FLOOR0_5",
            "FLOOR4_1",
            "FLOOR4_8",
            "FLOOR5_1",
            "FLOOR5_2",
            "FLOOR5_3",
            "FLOOR6_2",
            "FLOOR7_1",
            "FLOOR7_2",
            "GRASS1",
            "GRAY1",
            "GRAY2",
            "GRAY4",
            "GRAY5",
            "GRAY7",
            "GRAY8",
            "GRAYBIG",
            "GRAYPOIS",
            "GRAYTALL",
            "GRAYWIDE",
            "GSTONE1",
            "GSTONE2",
            "GSTVINE2",
            "ICKWALL1",
            "ICKWALL2",
            "ICKWALL3",
            "ICKWALL4",
            "LITE3",
            "LITE4",
            "LITE5",
            "LITEBLU1",
            "LITEBLU3",
            "LITEBLU4",
            "LITERED",
            "MARBFAC3",
            "MARBGRAY",
            "MC17",
            "MC18",
            "MC19",
            "MC3",
            "MC5",
            "METAL",
            "METAL1",
            "METAL2",
            "METAL5",
            "MFLR8_1",
            "MIDBARS1",
            "MIDBARS3",
            "MIDBRN1",
            "MIDGRATE",
            "MIDSPACE",
            "MIDSPCSM",
            "NUKE24",
            "NUKEDGE1",
            "NUKESLAD",
            "PIPE2",
            "PLANET1",
            "PLAT1",
            "PWHITE",
            "ROCK5",
            "ROCKRED1",
            "ROCKRED2",
            "ROCKRED3",
            "RROCK03",
            "RROCK14",
            "RROCK17",
            "RROCK18",
            "RROCK19",
            "SFALL1",
            "SFALL2",
            "SFALL3",
            "SFALL4",
            "SHAWN02",
            "SHAWN1",
            "SHAWN2",
            "SHAWN3",
            "SKIN2",
            "SKINEDGE",
            "SKSNAKE1",
            "SKSNAKE2",
            "SK_LEFT",
            "SK_RIGHT",
            "SLADPOIS",
            "SLADRIP1",
            "SLADRIP2",
            "SLADRIP3",
            "SLADSKUL",
            "SLADWALL",
            "SLIME14",
            "SMGLASS1",
            "SPCDOOR3",
            "SP_FACE2",
            "SP_HOT1",
            "SP_ROCK1",
            "STARBR1",
            "STARBR2",
            "STARG1",
            "STARG2",
            "STARG3",
            "STARG4",
            "STARGR1",
            "STARGR2",
            "STARTAN1",
            "STARTAN2",
            "STARTAN3",
            "STEP1",
            "STEP1_F",
            "STEP2",
            "STEP2_F",
            "STEP3",
            "STEP4",
            "STEP5",
            "STEP6",
            "STEPTOP",
            "STONE",
            "STONE2",
            "STONE3",
            "SUPPORT2",
            "SUPPORT3",
            "SW1BLUE",
            "SW1BRCOM",
            "SW1BRN1",
            "SW1BRN2",
            "SW1BRNGN",
            "SW1CMT",
            "SW1COMM",
            "SW1COMP",
            "SW1EXIT",
            "SW1GRAY",
            "SW1GRAY1",
            "SW1MET2",
            "SW1METAL",
            "SW1PIPE",
            "SW1SLAD",
            "SW1STON1",
            "SW1STON2",
            "SW1STRTN",
            "SW2BLUE",
            "SW2BRCOM",
            "SW2BRN1",
            "SW2BRN2",
            "SW2BRNGN",
            "SW2CMT",
            "SW2COMM",
            "SW2COMP",
            "SW2EXIT",
            "SW2GRAY",
            "SW2GRAY1",
            "SW2MET2",
            "SW2METAL",
            "SW2PIPE",
            "SW2SLAD",
            "SW2STON1",
            "SW2STON2",
            "SW2STRTN",
            "TEKWALL1",
            "TEKWALL2",
            "TEKWALL3",
            "TEKWALL4",
            "TEKWALL5",
            "TLITE6_1",
            "TLITE6_5",
            "TLITE6_6",
            "WFALL1",
            "WFALL2",
            "WFALL3",
            "WFALL4",
            "WOOD1",
            "WOODMET1",
            "ZIMMER3",
        };

        public static bool Contains(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            for (int i = 0; i < Names.Length; i++)
                if (Names[i] == name) return true;
            return false;
        }

        public static string ResourcesPath(string name) =>
            ResourcesFolder + "/" + name;
    }
}
