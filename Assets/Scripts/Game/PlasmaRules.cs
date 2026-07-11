namespace Doom.Game
{
    /// Canonical player plasma constants from DOOM info.c / p_pspr.c.
    public static class PlasmaRules
    {
        public const int SnapshotType = 2004;
        public const string Sprite = "PLSS";
        public const string ExplodeSprite = "PLSE";
        public const string FireSound = "DSPLASMA";
        public const string ExplodeSound = "DSFIRXPL";
        public const int SpeedDoomPerTic = 25;
        public const float RadiusDoom = 13f;
        public const float HeightDoom = 8f;
        public const int DirectDamageMod = 8;
        public const int DirectDamageMult = 5;

        public static readonly int[] FlyFrames = { 0, 1 };
        public static readonly int[] FlyTics = { 6, 6 };
        public static readonly int[] ExplodeFrames = { 0, 1, 2, 3, 4 };
        public static readonly int[] ExplodeTics = { 4, 4, 4, 4, 4 };

        /// Direct hit: 5 * (P_Random() % 8 + 1) → 5..40.
        public static int RollDirectDamage(DoomRandom r)
            => MonsterRules.RollDamage(r, DirectDamageMod, DirectDamageMult);
    }
}
