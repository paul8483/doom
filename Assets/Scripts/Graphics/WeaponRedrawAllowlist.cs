namespace Doom.Graphics
{
    /// First-person weapon (viewmodel) lumps whose Enhanced texture is a
    /// display-grade GPT redraw (Resources/EnhancedWeapons/&lt;LUMP&gt;.png,
    /// exactly 4× the native patch) instead of the native decode. Provenance
    /// lives in Textures/WeaponRedraw/&lt;LUMP&gt;/ (native export, prompt,
    /// redraw-raw, healed+toned redraw).
    ///
    /// Weapon wave (2026-08-30), all four sets accepted on panels: every
    /// frame the WeaponTable can request is covered — the fist/saw, pistol/
    /// shotgun (+flashes), chaingun/rocket (+flashes) and plasma/BFG
    /// (+flashes). Frames that exist in the WAD but are never requested
    /// (PISGD0/E0, SAWGD0, CHGFB0, BFGGC0) are deliberately absent. The
    /// redraw silhouette may run slightly narrower than native (the baked
    /// fake-transparency halo was cut — heal_weapon_checker.py), never
    /// wider; BFGGB0 is pinned to BFGGA0 outside the charge-light zone.
    public static class WeaponRedrawAllowlist
    {
        public const string ResourcesFolder = "EnhancedWeapons";

        /// Redraws are authored at exactly this multiple of the native
        /// patch size. Placement stays native-header based, so the multiple
        /// is a hard contract (mirror of the HUD redraw contract).
        public const int Scale = 4;

        public static readonly string[] Lumps =
        {
            "BFGFA0",
            "BFGFB0",
            "BFGGA0",
            "BFGGB0",
            "CHGFA0",
            "CHGGA0",
            "CHGGB0",
            "MISFA0",
            "MISFB0",
            "MISFC0",
            "MISFD0",
            "MISGA0",
            "MISGB0",
            "PISFA0",
            "PISGA0",
            "PISGB0",
            "PISGC0",
            "PLSFA0",
            "PLSFB0",
            "PLSGA0",
            "PLSGB0",
            "PUNGA0",
            "PUNGB0",
            "PUNGC0",
            "PUNGD0",
            "SAWGA0",
            "SAWGB0",
            "SAWGC0",
            "SHTFA0",
            "SHTFB0",
            "SHTGA0",
            "SHTGB0",
            "SHTGC0",
            "SHTGD0",
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
