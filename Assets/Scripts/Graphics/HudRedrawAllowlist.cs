namespace Doom.Graphics
{
    /// Status-bar UI patch names whose Enhanced texture is a display-grade
    /// GPT redraw (Resources/EnhancedHud/&lt;NAME&gt;.png, exactly 4× native)
    /// instead of the Super-xBR upscale. Provenance lives in
    /// Textures/HudRedraw/&lt;NAME&gt;/ (native export, prompt, redraw).
    /// The list grows per accepted set, like the world allowlist.
    public static class HudRedrawAllowlist
    {
        public const string ResourcesFolder = "EnhancedHud";

        /// Redraws are authored at exactly this multiple of the native patch
        /// size (the slot the Super-xBR 4× occupies today). Placement stays
        /// native-header based, so the multiple is a hard contract.
        public const int Scale = 4;

        /// HUD wave 1 set A (2026-08-28): the status-bar plate and the ARMS
        /// inset — labels, bezels and LED strip pinned one-to-one (digits,
        /// keys and the face land on fixed screen coordinates above them).
        /// Set B (2026-08-28): the tall red counter digits — one enamel font
        /// across all twelve (a first generation traced only the outer
        /// silhouettes into blobs; the re-roll gate added an inner-pattern
        /// correlation check beside the silhouette IoU).
        public static readonly string[] Lumps =
        {
            "STARMS",
            "STBAR",
            "STTMINUS",
            "STTNUM0",
            "STTNUM1",
            "STTNUM2",
            "STTNUM3",
            "STTNUM4",
            "STTNUM5",
            "STTNUM6",
            "STTNUM7",
            "STTNUM8",
            "STTNUM9",
            "STTPRCNT",
            // Set C (2026-08-28): the 4x6 small digits are deterministic
            // nearest-4x of the native (Tools/make_hud_crisp_redraw.py) —
            // at 24 pixels every texel is load-bearing and two GPT passes
            // mangled the interiors; the crisp Classic look in the redraw
            // slot is the honest fix for the Super-xBR smear.
            "STGNUM0",
            "STGNUM1",
            "STGNUM2",
            "STGNUM3",
            "STGNUM4",
            "STGNUM5",
            "STGNUM6",
            "STGNUM7",
            "STGNUM8",
            "STGNUM9",
            "STYSNUM0",
            "STYSNUM1",
            "STYSNUM2",
            "STYSNUM3",
            "STYSNUM4",
            "STYSNUM5",
            "STYSNUM6",
            "STYSNUM7",
            "STYSNUM8",
            "STYSNUM9",
            // Set D (2026-08-28): the key icons — cell layout and the color
            // signal (blue/gold/red doors) pinned one-to-one.
            "STKEYS0",
            "STKEYS1",
            "STKEYS2",
            "STKEYS3",
            "STKEYS4",
            "STKEYS5",
            "STKEYS6",
            "STKEYS7",
            "STKEYS8",
            // HUD wave 2 set 1 (2026-08-28): the marine face, healthy band
            // (pain 0) + god mode — the identity anchor for bands 1-4.
            // Canvas filler healed deterministically (install_hud_faces.py):
            // cream margins over the native dark outline go dark, margins
            // over native face go transparent (the STBAR slot shows).
            "STFST00",
            "STFST01",
            "STFST02",
            "STFTL00",
            "STFTR00",
            "STFOUCH0",
            "STFEVL0",
            "STFKILL0",
            "STFGOD0",
            // HUD wave 2 set 2 (2026-08-28): pain bands 1-2 — the same
            // eight grimaces with escalating wounds, identity anchored on
            // the accepted band-0 redraws.
            "STFST10",
            "STFST11",
            "STFST12",
            "STFTL10",
            "STFTR10",
            "STFOUCH1",
            "STFEVL1",
            "STFKILL1",
            "STFST20",
            "STFST21",
            "STFST22",
            "STFTL20",
            "STFTR20",
            "STFOUCH2",
            "STFEVL2",
            "STFKILL2",
            // HUD wave 2 set 3 (2026-08-28): pain bands 3-4 + the death
            // face — the wave's final set; every face patch the status bar
            // can show now routes a display redraw.
            "STFST30",
            "STFST31",
            "STFST32",
            "STFTL30",
            "STFTR30",
            "STFOUCH3",
            "STFEVL3",
            "STFKILL3",
            "STFST40",
            "STFST41",
            "STFST42",
            "STFTL40",
            "STFTR40",
            "STFOUCH4",
            "STFEVL4",
            "STFKILL4",
            "STFDEAD0",
        };

        public static bool Contains(string lumpName)
        {
            if (string.IsNullOrEmpty(lumpName)) return false;
            for (int i = 0; i < Lumps.Length; i++)
                if (Lumps[i] == lumpName) return true;
            return false;
        }

        public static string ResourcesPath(string name) =>
            ResourcesFolder + "/" + name;
    }
}
