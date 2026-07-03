namespace Doom.Game
{
    /// Monster combat formulas (p_enemy.c, linuxdoom-1.10) over DoomRandom.
    public static class MonsterRules
    {
        public const float MeleeRangeDoom = 64f;   // MELEERANGE
        // Monster hitscan jitter: (P_Random()-P_Random())<<20 in BAM.
        const float SpreadUnitDeg = 360f / 4096f;

        /// Damage = ((P_Random() % mod) + 1) * mult.
        public static int RollDamage(DoomRandom r, int mod, int mult)
            => (r.Next() % mod + 1) * mult;

        public static float SpreadOffsetDeg(DoomRandom r)
            => (r.Next() - r.Next()) * SpreadUnitDeg;

        /// P_CheckMeleeRange (distances in DOOM units, to target center).
        public static bool InMeleeRange(float dist, float targetRadius)
            => dist < MeleeRangeDoom - 20f + targetRadius;

        /// P_CheckMissileRange distance gate (sight/justHit/reaction are checked
        /// by the caller). Returns true when the monster decides to attack.
        public static bool CheckMissileRange(DoomRandom r, float dist, bool hasMelee)
        {
            dist -= 64f;
            if (!hasMelee) dist -= 128f;   // no melee attack -> keep shooting further out
            if (dist > 200f) dist = 200f;
            return r.Next() >= dist;
        }
    }
}
