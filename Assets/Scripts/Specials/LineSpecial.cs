namespace Doom.Specials
{
    /// How a linedef special is activated.
    public enum TriggerKind { Push, Walk, Switch, Gun }

    /// Action family. Door/Floor/Ceiling/Plat/Stair are executed in Stage 6a;
    /// the rest are recognized but inert (no-op + log) until later stages.
    public enum SpecialCategory
    {
        None, Door, LockedDoor, Floor, Ceiling, Plat, Stair,
        Crusher, Light, Teleport, Donut, Exit, Scroll
    }

    public enum MoveDirection { Up, Down }

    public enum MoveSpeed { Slow, Normal, Fast, Turbo }

    public enum KeyKind { None, RedCard, BlueCard, YellowCard, RedSkull, BlueSkull, YellowSkull, Any }

    /// How a mover's target height is computed from the map + current heights.
    public enum TargetSpec
    {
        None,
        LowestNeighborCeilingMinus4, // door open height
        LowestNeighborFloor,         // lift/plat down, lower-floor
        HighestNeighborFloor,
        NextHigherFloor,
        NextLowerFloor,
        LowestNeighborCeiling,
        ToFloor,                     // close to own floor (door close)
        StairStep                    // handled by stair builder
    }

    /// One ported DOOM linedef type. Repeatable = "R" trigger; once = "1".
    public readonly struct LineSpecial
    {
        public readonly int Type;
        public readonly TriggerKind Trigger;
        public readonly bool Repeatable;
        public readonly bool MonsterActivatable;
        public readonly SpecialCategory Category;
        public readonly MoveDirection Direction;
        public readonly MoveSpeed Speed;
        public readonly TargetSpec Target;
        public readonly KeyKind Key;

        public LineSpecial(int type, TriggerKind trigger, bool repeatable, bool monster,
                           SpecialCategory category, MoveDirection direction, MoveSpeed speed,
                           TargetSpec target, KeyKind key)
        {
            Type = type; Trigger = trigger; Repeatable = repeatable; MonsterActivatable = monster;
            Category = category; Direction = direction; Speed = speed; Target = target; Key = key;
        }

        /// True for categories Stage 6a actually animates.
        public bool IsExecutable =>
            Category == SpecialCategory.Door || Category == SpecialCategory.LockedDoor ||
            Category == SpecialCategory.Floor || Category == SpecialCategory.Ceiling ||
            Category == SpecialCategory.Plat || Category == SpecialCategory.Stair;
    }
}
