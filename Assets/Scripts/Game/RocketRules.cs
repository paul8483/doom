namespace Doom.Game
{
    /// Canonical player rocket constants from DOOM info.c / p_mobj.c.
    public static class RocketRules
    {
        public const int SnapshotType = 2003;
        public const string Sprite = "MISL";
        public const string ExplodeSound = "DSBAREXP";
        public const int SpeedDoomPerTic = 20;
        public const float RadiusDoom = 11f;
        public const int DirectDamageMod = 8;
        public const int DirectDamageMult = 20;
        public const int SplashDamage = 128;
        public const float SplashRadiusDoom = 128f;

        public static readonly int[] FlyFrames = { 0 };
        public static readonly int[] FlyTics = { 1 };
        public static readonly int[] ExplodeFrames = { 1, 2, 3 };
        public static readonly int[] ExplodeTics = { 8, 6, 4 };
    }
}
