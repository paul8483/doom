namespace Doom.Specials
{
    /// How a linedef special is activated.
    public enum TriggerKind { Push, Walk, Switch, Gun }

    /// Action family. Runtime executors are exposed through IsExecutable; Scroll is
    /// a build-time renderer effect and therefore intentionally remains outside it.
    public enum SpecialCategory
    {
        None, Door, LockedDoor, Floor, Ceiling, Plat, Stair,
        Crusher, Light, Teleport, Donut, Exit, Scroll
    }

    public enum MoveDirection { Up, Down }

    /// Crawl = FLOORSPEED/4 (stairs build8), Half = PLATSPEED/2 (raise-and-
    /// change plats), Slow = FLOORSPEED (1 u/tic), Normal = VDOORSPEED (2),
    /// Fast = PLATSPEED*4 / FLOORSPEED*4 (4), Turbo = blaze (8).
    public enum MoveSpeed { Slow, Normal, Fast, Turbo, Crawl, Half }

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
        LowestNeighborCeiling,       // raiseFloor: clamped to own ceiling
        ToFloor,                     // close to own floor (door close)
        StairStep,                   // handled by stair builder
        HighestNeighborFloorPlus8,   // turboLower: +8 unless already there
        LowestNeighborCeilingMinus8, // raiseFloorCrush: clamped to own ceiling, −8
        FloorPlus24,                 // raiseFloor24 / raiseAndChange 24
        FloorPlus32,                 // raiseAndChange 32
        FloorPlus512,                // raiseFloor512
        HighestNeighborCeiling,      // ceiling raiseToHighest
        FloorPlus8,                  // lowerAndCrush ceiling target
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

        /// True for categories that have a runtime executor.
        public bool IsExecutable =>
            Category == SpecialCategory.Door || Category == SpecialCategory.LockedDoor ||
            Category == SpecialCategory.Floor || Category == SpecialCategory.Ceiling ||
            Category == SpecialCategory.Plat || Category == SpecialCategory.Stair ||
            Category == SpecialCategory.Crusher ||
            Category == SpecialCategory.Exit || Category == SpecialCategory.Teleport ||
            Category == SpecialCategory.Light;
    }
}
