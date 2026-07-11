using System.Collections.Generic;

namespace Doom.Game
{
    public readonly struct HitscanShot
    {
        public readonly float YawOffsetDeg;
        public readonly int Damage;
        public HitscanShot(float yaw, int damage) { YawOffsetDeg = yaw; Damage = damage; }
    }

    /// Формулы hitscan DOOM (p_pspr.c / p_map.c), поверх DoomRandom.
    public static class HitscanRules
    {
        public const float HitscanRangeDoom = 2048f;  // MISSILERANGE
        public const float MeleeRangeDoom = 64f;      // MELEERANGE
        public const float SawRangeDoom = MeleeRangeDoom + 1f; // A_Saw: MELEERANGE+1
        // (P_Random()-P_Random())<<18 в BAM: 1 ед. = 360/16384 ≈ 0.022°, max ±5.6°.
        const float SpreadUnitDeg = 360f / 16384f;

        public static int GunShotDamage(DoomRandom r) => 5 * (r.Next() % 3 + 1);   // P_GunShot

        /// A_Punch: 2*(1d10); with pw_strength (berserk) ×10.
        public static int PunchDamage(DoomRandom r, bool berserk = false)
        {
            int d = (r.Next() % 10 + 1) * 2;
            return berserk ? d * 10 : d;
        }

        /// A_Saw: 2*(1d10); never multiplied by berserk.
        public static int SawDamage(DoomRandom r) => (r.Next() % 10 + 1) * 2;

        public static float SpreadOffsetDeg(DoomRandom r)
            => (r.Next() - r.Next()) * SpreadUnitDeg;

        /// Залп одного нажатия: (смещение по yaw, урон) на каждый луч.
        public static void FireVolley(WeaponDef def, bool refire, DoomRandom r,
                                      List<HitscanShot> outShots, bool berserk = false)
        {
            for (int i = 0; i < def.Pellets; i++)
            {
                int damage = def.Id == WeaponId.Chainsaw
                    ? SawDamage(r)
                    : def.Melee ? PunchDamage(r, berserk) : GunShotDamage(r);
                bool accurate = def.FirstShotAccurate && !refire && !def.Melee;
                // Дробовик и пулемёт всегда с разбросом; кулак в DOOM тоже
                // рыскает на ±5.6° (A_Punch).
                float yaw = (accurate && def.Pellets == 1) ? 0f : SpreadOffsetDeg(r);
                outShots.Add(new HitscanShot(yaw, damage));
            }
        }
    }
}
