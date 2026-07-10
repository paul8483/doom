namespace Doom.Game
{
    public enum FaceLook : byte { Center = 0, Right = 1, Left = 2 }

    /// Relative attacker position for directional pain faces. None → neutral ouch.
    public enum FaceAttackerSide : byte { None = 0, Left = 1, Right = 2 }

    /// Pure face-patch naming and timing constants (st_stuff.c).
    public static class FaceRules
    {
        public const int TicRate = 35;
        public const int StraightTics = 17;
        public const int TurnTics = TicRate;       // 1 second
        public const int OuchTics = TicRate;
        public const int EvilGrinTics = TicRate * 2;
        public const int RampageTics = TicRate;
        public const int MuchPain = 20;

        public const string DeadPatch = "STFDEAD0";
        public const string GodPatch = "STFGOD0";

        public static int PainOffset(int health)
        {
            if (health >= 80) return 0;
            if (health >= 60) return 1;
            if (health >= 40) return 2;
            if (health >= 20) return 3;
            if (health > 0) return 4;
            return 4;
        }

        public static string IdlePatch(int health, FaceLook look) =>
            $"STFST{PainOffset(health)}{(int)look}";

        public static string OuchPatch(int health) =>
            $"STFOUCH{PainOffset(health)}";

        public static string TurnPatch(int health, FaceAttackerSide side) =>
            side == FaceAttackerSide.Left
                ? $"STFTL{PainOffset(health)}0"
                : $"STFTR{PainOffset(health)}0";

        public static string EvilGrinPatch(int health) =>
            $"STFEVL{PainOffset(health)}";

        public static string RampagePatch(int health) =>
            $"STFKILL{PainOffset(health)}";
    }
}
