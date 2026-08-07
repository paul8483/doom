namespace Doom.Things
{
    /// Vanilla MT_BARREL (info.c / states.c): HP 20, BEXP A–E @ 5 tics, DSBAREXP.
    /// No corpse — mobj is removed after the explode sequence (S_NULL).
    public static class BarrelRules
    {
        public const int DoomEdNum = 2035;
        public const int Health = 20;
        public const string SpawnSprite = "BAR1";
        public const string ExplodeSprite = "BEXP";
        public const string ExplodeSound = "DSBAREXP";

        /// BEXP frames A–E (info.c S_BAR1_Die …).
        public static readonly int[] ExplodeFrames = { 0, 1, 2, 3, 4 };

        /// Tics per explode frame (all 5 in vanilla).
        public static readonly int[] ExplodeTics = { 5, 5, 5, 5, 5 };

        /// Idle blink S_BAR1 → S_BAR2 (BAR1 A 6 → B 6, loop).
        public static readonly int[] IdleFrames = { 0, 1 };
        public static readonly int[] IdleTics = { 6, 6 };
    }
}
