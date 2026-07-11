using System.Collections.Generic;

namespace Doom.Game
{
    /// One BFG tracer ray: yaw offset from the saved shot direction + damage.
    public readonly struct BfgTracerShot
    {
        public readonly float YawOffsetDeg;
        public readonly int Damage;

        public BfgTracerShot(float yawOffsetDeg, int damage)
        {
            YawOffsetDeg = yawOffsetDeg;
            Damage = damage;
        }
    }

    /// Canonical BFG9000 constants from DOOM info.c / p_pspr.c (A_BFGSpray).
    public static class BfgRules
    {
        public const int SnapshotType = 2006;
        public const string Sprite = "BFS1";
        public const string ExplodeSprite = "BFE1";
        public const string TracerSprite = "BFE2";
        public const string FireSound = "DSBFG";
        public const string ExplodeSound = "DSRXPLOD";
        public const int SpeedDoomPerTic = 25;
        public const float RadiusDoom = 13f;
        public const float HeightDoom = 8f;
        public const int DirectDamageMod = 8;
        public const int DirectDamageMult = 100;

        public const int TracerCount = 40;
        public const float TracerRangeDoom = 1024f;
        public const float FanStartDeg = -45f;
        public const float FanWidthDeg = 90f;
        public const int TracerDamageRolls = 15;

        /// Spray runs on entry to explode frame C (index 2), after 16 impact tics.
        public const int SprayFrameIndex = 2;
        public const int SprayAfterImpactTics = 16;

        public static readonly int[] FlyFrames = { 0, 1 };
        public static readonly int[] FlyTics = { 4, 4 };
        public static readonly int[] ExplodeFrames = { 0, 1, 2, 3, 4, 5 };
        public static readonly int[] ExplodeTics = { 8, 8, 8, 8, 8, 8 };
        public static readonly int[] TracerFrames = { 0, 1, 2, 3 };
        public static readonly int[] TracerTics = { 8, 8, 8, 8 };

        public static float FanStepDeg => FanWidthDeg / TracerCount;

        /// Direct hit: 100 * (P_Random() % 8 + 1) → 100..800.
        public static int RollDirectDamage(DoomRandom r)
            => MonsterRules.RollDamage(r, DirectDamageMod, DirectDamageMult);

        /// Literal A_BFGSpray damage: sum of 15 rolls of (P_Random() & 7) + 1.
        public static int RollTracerDamage(DoomRandom r)
        {
            int sum = 0;
            for (int i = 0; i < TracerDamageRolls; i++)
                sum += (r.Next() & 7) + 1;
            return sum;
        }

        public static float TracerYawOffsetDeg(int index)
            => FanStartDeg + index * FanStepDeg;

        /// Pure tracer fan data for Unity raycasts. Physics stays in MapBuild.
        public static void BuildTracers(DoomRandom r, List<BfgTracerShot> outShots)
        {
            outShots.Clear();
            for (int i = 0; i < TracerCount; i++)
                outShots.Add(new BfgTracerShot(TracerYawOffsetDeg(i), RollTracerDamage(r)));
        }
    }
}
