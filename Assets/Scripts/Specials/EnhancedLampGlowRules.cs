using System;

namespace Doom.Specials
{
    /// Eligibility for Enhanced-only per-bulb TLITE ceiling flicker (shader MPB).
    /// Classic stays WAD-faithful; WAD specials / sector light thinkers are never mutated.
    public static class EnhancedLampGlowRules
    {
        public static bool IsEligible(string ceilingFlat, int sectorSpecial)
        {
            if (string.IsNullOrEmpty(ceilingFlat)) return false;
            if (!ceilingFlat.StartsWith("TLITE", StringComparison.OrdinalIgnoreCase))
                return false;
            return !RuntimeLightRules.TryKindFromSectorSpecial(sectorSpecial, out _);
        }
    }
}
