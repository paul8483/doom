using System;
using System.Collections.Generic;

namespace Doom.Specials
{
    /// Eligibility for Enhanced-only per-fixture lamp flicker (shader MPB).
    /// Covers ceiling light flats (TLITE/FLAT2/Freedoom AQF panels), and light
    /// wall textures (AQLITE/LITE). Classic stays WAD-faithful; thinkers untouched.
    public static class EnhancedLampGlowRules
    {
        /// Freedoom AQF ceiling panel-lamp flats (rectangular fixtures).
        static readonly HashSet<string> FreedoomPanelLampFlats =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "AQF010", "AQF011", "AQF012", "AQF014", "AQF015",
                "AQF037", "AQF038", "AQF039", "AQF040", "AQF043", "AQF044",
                "AQF058", "AQF059", "AQF061", "AQF066",
            };

        public static bool IsEligible(string textureOrFlatName, int sectorSpecial)
        {
            if (!IsLightSurfaceName(textureOrFlatName)) return false;
            return !RuntimeLightRules.TryKindFromSectorSpecial(sectorSpecial, out _);
        }

        public static bool IsLightSurfaceName(string textureOrFlatName)
        {
            if (string.IsNullOrEmpty(textureOrFlatName)) return false;

            string name = textureOrFlatName;
            const string normalSuffix = "/Normal";
            if (name.EndsWith(normalSuffix, StringComparison.Ordinal))
                name = name.Substring(0, name.Length - normalSuffix.Length);

            if (name.StartsWith("TLITE", StringComparison.OrdinalIgnoreCase))
                return true;
            if (name.StartsWith("AQLITE", StringComparison.OrdinalIgnoreCase))
                return true;
            if (name.StartsWith("LITE", StringComparison.OrdinalIgnoreCase))
                return true;
            if (FreedoomPanelLampFlats.Contains(name))
                return true;

            switch (name.ToUpperInvariant())
            {
                case "FLAT2":
                case "FLAT17":
                case "FLOOR1_7":
                case "CEIL1_2":
                case "CEIL1_3":
                // Freedoom CEIL3_4 is a double rectangular lamp panel (unlike
                // vanilla Doom, where it is a plain ceiling).
                case "CEIL3_4":
                case "GRNLITE1":
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsLightCeilingFlat(string ceilingFlat) =>
            IsLightSurfaceName(ceilingFlat);
    }
}
