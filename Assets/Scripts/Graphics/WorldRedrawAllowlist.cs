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
        /// Names are WAD texture/flat names.
        public static readonly string[] Names =
        {
            "A-BRICK3",
            "A-CONCTE",
            "A-DBRI28",
            "A-DROCK1",
            "AQCOMP01",
            "AQCONC04",
            "AQCONC06",
            "AQCONC07",
            "AQCONC10",
            "AQCONC14",
            "AQCONC16",
            "AQDOOR01",
            "AQDOOR02",
            "AQLITE18",
            "AQMETL03",
            "AQMETL07",
            "AQMETL10",
            "AQMETL11",
            "AQMETL12",
            "AQMETL13",
            "AQMETL14",
            "AQMETL15",
            "AQMETL20",
            "AQMETL21",
            "AQPANL06",
            "AQPIPE05",
            "AQPIPE14",
            "AQRUST09",
            "AQRUST10",
            "AQSECT11",
            "AQSUPP06",
            "AQSUPP08",
            "AQSUPP12",
            "AQSUPP13",
            "AQTRIM05",
            "ASHWALL",
            "ASHWALL2",
            "ASHWALL4",
            "BASE",
            "BASE2",
            "BIGBRIK2",
            "BIGDOOR1",
            "BIGDOOR2",
            "BIGDOOR3",
            "BIGDOOR4",
            "BIGDOOR6",
            "BRICK10",
            "BRONZE1",
            "BRONZE3",
            "BROWN1",
            "BROWN144",
            "BROWN96",
            "BROWNGRN",
            "BROWNHUG",
            "BROWNPIP",
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
            "CRATWIDE",
            "DOOR1",
            "DOOR3",
            "DOORBLU",
            "DOORHI",
            "DOORRED",
            "DOORSTOP",
            "DOORTRAK",
            "DOORYEL",
            "EXITDOOR",
            "EXITSGN2",
            "EXITSIGN",
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
            "MC19",
            "MC3",
            "MC5",
            "METAL",
            "METAL1",
            "METAL2",
            "METAL5",
            "NUKE24",
            "NUKEDGE1",
            "PIPE2",
            "PLANET1",
            "PLAT1",
            "PWHITE",
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
            "SLADWALL",
            "SPCDOOR3",
            "SP_HOT1",
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
            "STEP2",
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
            "TEKWALL1",
            "TEKWALL2",
            "TEKWALL3",
            "TEKWALL4",
            "TEKWALL5",
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
