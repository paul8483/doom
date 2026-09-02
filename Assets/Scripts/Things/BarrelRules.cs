namespace Doom.Things
{
    /// Vanilla MT_BARREL (info.c): HP 20, S_BEXP..S_BEXP5 = BEXP A 5, B 5
    /// (A_Scream), C 5, D 10 (A_Explode), E 10, DSBAREXP.
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

        /// Tics per explode frame (info.c: 5, 5, 5, 10, 10).
        public static readonly int[] ExplodeTics = { 5, 5, 5, 10, 10 };

        /// Index into ExplodeFrames whose entry runs A_Explode (frame D, tic 15
        /// after death) — barrel chains ripple instead of detonating at once.
        public const int ExplodeFrameIndex = 3;

        /// Idle blink S_BAR1 → S_BAR2 (BAR1 A 6 → B 6, loop).
        public static readonly int[] IdleFrames = { 0, 1 };
        public static readonly int[] IdleTics = { 6, 6 };
    }
}
