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
        /// and excluded — riveted metal, not computers). Names are WAD
        /// texture/flat names.
        public static readonly string[] Names =
        {
            "AQCOMP01",
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
