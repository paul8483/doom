namespace Doom.Graphics
{
    /// Gate 0 approved display-grade redraw lumps for Enhanced + 3D Off.
    /// Sources live in Textures/Trellis2/ShapeHints/2D/ (the 3D/ sibling
    /// holds the volumetric TRELLIS conditioning renders, which are a
    /// different image for a different consumer); runtime copies land under
    /// Assets/Resources/EnhancedSprites/.
    public static class DisplayRedrawAllowlist
    {
        public const string ResourcesFolder = "EnhancedSprites";

        /// Approved 2026-08-07 (ARM1B0/BAR1B0 added 2026-08-08 — full frame
        /// coverage for the ARM1/BAR1 blink; trees SMITA0/TRE1A0/TRE2A0 added
        /// 2026-08-08; ammo CLIPA0/SBOXA0 added 2026-08-09; ammo AMMOA0/CELLA0/
        /// CELPA0/ROCKA0/SHELA0 added 2026-08-09; STIMA0 added 2026-08-10 from
        /// the new depth shapehint — earlier STIMA0-v3 redraw was rejected;
        /// MEDIA0 added 2026-08-11 from depth shapehint-v2 for Enhanced 2D;
        /// BON2A0–D0 added 2026-08-11 — A0 redraw reused for B/C/D so the
        /// four-frame bonus animation stays fully covered).
        /// Monsters/out-of-scope excluded.
        public static readonly string[] Lumps =
        {
            "AMMOA0",
            "ARM1A0",
            "ARM1B0",
            "BAR1A0",
            "BAR1B0",
            "BFUGA0",
            "BON2A0",
            "BON2B0",
            "BON2C0",
            "BON2D0",
            "CELLA0",
            "CELPA0",
            "CLIPA0",
            "COLUA0",
            "CSAWA0",
            "LAUNA0",
            "MEDIA0",
            "MGUNA0",
            "PLASA0",
            "ROCKA0",
            "SBOXA0",
            "SHELA0",
            "SHOTA0",
            "SMITA0",
            "STIMA0",
            "TRE1A0",
            "TRE2A0",
        };

        /// Lumps whose subjects contain no intentional near-white detail, so
        /// the import may key backdrop remnants aggressively (enclosed pockets,
        /// floating checkerboard islands). Weapon/armor redraws keep their
        /// white highlights and stay on the border flood-fill only.
        public static readonly string[] AggressiveKeyLumps =
        {
            "SMITA0",
            "TRE1A0",
            "TRE2A0",
        };

        public static bool UsesAggressiveKey(string lumpName)
        {
            for (int i = 0; i < AggressiveKeyLumps.Length; i++)
                if (AggressiveKeyLumps[i] == lumpName) return true;
            return false;
        }

        public static bool Contains(string lumpName)
        {
            if (string.IsNullOrEmpty(lumpName)) return false;
            for (int i = 0; i < Lumps.Length; i++)
                if (Lumps[i] == lumpName) return true;
            return false;
        }

        public static string ResourcesPath(string lumpName) =>
            ResourcesFolder + "/" + lumpName;

        /// Subfolder of Textures/Trellis2/ShapeHints holding the 2D redraws.
        public const string ShapeHintFolder = "2D";

        /// ShapeHints path for an allowlisted lump, relative to the ShapeHints
        /// root (import source).
        public static string ShapeHintFileName(string lumpName) =>
            ShapeHintFolder + "/" + lumpName + "-depth-shapehint.png";
    }
}
