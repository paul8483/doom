namespace Doom.Graphics
{
    /// Gate 0 approved display-grade redraw lumps for Enhanced + 3D Off.
    /// Conditioning sources stay in Textures/Trellis2/ShapeHints/; runtime
    /// copies live under Assets/Resources/EnhancedSprites/.
    public static class DisplayRedrawAllowlist
    {
        public const string ResourcesFolder = "EnhancedSprites";

        /// Approved 2026-08-07 (ARM1B0/BAR1B0 added 2026-08-08 — full frame
        /// coverage for the ARM1/BAR1 blink; trees SMITA0/TRE1A0/TRE2A0 added
        /// 2026-08-08). STIMA0-v3 rejected. Monsters/out-of-scope excluded.
        public static readonly string[] Lumps =
        {
            "ARM1A0",
            "ARM1B0",
            "BAR1A0",
            "BAR1B0",
            "BFUGA0",
            "COLUA0",
            "CSAWA0",
            "LAUNA0",
            "MGUNA0",
            "PLASA0",
            "SHOTA0",
            "SMITA0",
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

        /// ShapeHints file name for an allowlisted lump (import source).
        public static string ShapeHintFileName(string lumpName) =>
            lumpName + "-depth-shapehint.png";
    }
}
