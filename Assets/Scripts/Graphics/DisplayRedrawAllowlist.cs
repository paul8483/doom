namespace Doom.Graphics
{
    /// Gate 0 approved display-grade redraw lumps for Enhanced + 3D Off.
    /// Conditioning sources stay in Textures/Trellis2/ShapeHints/; runtime
    /// copies live under Assets/Resources/EnhancedSprites/.
    public static class DisplayRedrawAllowlist
    {
        public const string ResourcesFolder = "EnhancedSprites";

        /// Approved 2026-08-07. STIMA0-v3 rejected. Monsters/out-of-scope excluded.
        public static readonly string[] Lumps =
        {
            "ARM1A0",
            "BAR1A0",
            "BFUGA0",
            "COLUA0",
            "CSAWA0",
            "LAUNA0",
            "MGUNA0",
            "PLASA0",
            "SHOTA0",
        };

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
