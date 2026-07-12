namespace Doom.Specials
{
    public enum CrusherBehavior : byte
    {
        None = 0,
        CrushAndRaise = 1,
        LowerAndCrush = 2,
        Stop = 3,
    }

    public readonly struct CrusherDefinition
    {
        public readonly CrusherBehavior Behavior;
        public readonly float SpeedUnitsPerTic;
        public readonly bool SlowsWhenCrushing;
        public readonly bool Silent;

        public CrusherDefinition(
            CrusherBehavior behavior, float speedUnitsPerTic,
            bool slowsWhenCrushing, bool silent)
        {
            Behavior = behavior;
            SpeedUnitsPerTic = speedUnitsPerTic;
            SlowsWhenCrushing = slowsWhenCrushing;
            Silent = silent;
        }

        public bool Cycles => Behavior == CrusherBehavior.CrushAndRaise;
        public float SpeedUnitsPerSecond => SpeedUnitsPerTic * CrusherRules.TicsPerSecond;
    }

    /// Vanilla ceiling crusher rules from p_ceilng.c.
    public static class CrusherRules
    {
        public const int TicsPerSecond = 35;
        public const int ClearanceUnits = 8;
        public const int Damage = 10;
        public const int DamageCadenceTics = 4;
        public const float CrushingSlowdown = 0.125f;

        public static float TargetHeight(float floorHeight) => floorHeight + ClearanceUnits;

        public static bool TryGet(int special, out CrusherDefinition definition)
        {
            switch (special)
            {
                case 6:
                case 77:
                    definition = new CrusherDefinition(
                        CrusherBehavior.CrushAndRaise, 2f, false, false);
                    return true;
                case 25:
                case 49:
                case 73:
                    definition = new CrusherDefinition(
                        CrusherBehavior.CrushAndRaise, 1f, true, false);
                    return true;
                case 141:
                    definition = new CrusherDefinition(
                        CrusherBehavior.CrushAndRaise, 1f, true, true);
                    return true;
                case 44:
                case 72:
                    definition = new CrusherDefinition(
                        CrusherBehavior.LowerAndCrush, 1f, true, false);
                    return true;
                case 57:
                case 74:
                    definition = new CrusherDefinition(
                        CrusherBehavior.Stop, 0f, false, false);
                    return true;
                default:
                    definition = default;
                    return false;
            }
        }
    }
}
