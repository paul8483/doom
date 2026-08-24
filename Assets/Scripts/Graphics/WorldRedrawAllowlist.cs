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
        /// the interactive gate). Names are WAD texture/flat names.
        public static readonly string[] Names =
        {
            "AQCOMP01",
            "BIGDOOR2",
            "BIGDOOR6",
            "BRICK10",
            "BROWN1",
            "BROWN144",
            "BROWN96",
            "BROWNGRN",
            "BROWNHUG",
            "BROWNPIP",
            "CRATE1",
            "CRATE2",
            "CRATELIT",
            "CRATWIDE",
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
            "DOORSTOP",
            "DOORTRAK",
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
            "LITE3",
            "LITE5",
            "LITEBLU4",
            "MC17",
            "METAL",
            "METAL1",
            "METAL2",
            "METAL5",
            "NUKE24",
            "NUKEDGE1",
            "SHAWN2",
            "SLADPOIS",
            "SLADWALL",
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
            "STONE",
            "STONE2",
            "STONE3",
            "SUPPORT2",
            "SUPPORT3",
            "TEKWALL1",
            "TEKWALL2",
            "TEKWALL4",
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
