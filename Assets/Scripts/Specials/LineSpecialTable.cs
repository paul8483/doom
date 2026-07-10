using System.Collections.Generic;

namespace Doom.Specials
{
    /// Ported DOOM linedef-type table, keyed by special number. Categories not
    /// executed in Stage 6a are still classified (the activator skips them).
    ///
    /// Source of truth: the canonical vanilla DOOM/DOOM II linedef-type table
    /// (Doom Wiki "Linedef type" + the original p_spec.c/p_doors.c/p_plats.c/
    /// p_floor.c/p_ceilng.c). Boom/MBF-only extended types are intentionally
    /// omitted. For non-executed categories (Crusher/Light/Teleport/Donut/Exit/
    /// Scroll) Trigger/Repeatable are recorded but Target is usually None.
    public static class LineSpecialTable
    {
        private static readonly Dictionary<int, LineSpecial> Defs = Build();

        public static bool TryGet(int type, out LineSpecial s) => Defs.TryGetValue(type, out s);
        public static IEnumerable<LineSpecial> All => Defs.Values;

        private static Dictionary<int, LineSpecial> Build()
        {
            var d = new Dictionary<int, LineSpecial>();
            void Add(int type, TriggerKind trig, bool rep, bool mon, SpecialCategory cat,
                     MoveDirection dir, MoveSpeed spd, TargetSpec tgt, KeyKind key)
                => d[type] = new LineSpecial(type, trig, rep, mon, cat, dir, spd, tgt, key);

            // ── Doors (manual, Use) ───────────────────────────────────────────
            Add(1,  TriggerKind.Push, true,  true,  SpecialCategory.Door, MoveDirection.Up, MoveSpeed.Normal, TargetSpec.LowestNeighborCeilingMinus4, KeyKind.None); // DR open/close
            Add(31, TriggerKind.Push, false, false, SpecialCategory.Door, MoveDirection.Up, MoveSpeed.Normal, TargetSpec.LowestNeighborCeilingMinus4, KeyKind.None); // D1 open stay
            Add(46, TriggerKind.Gun,  true,  false, SpecialCategory.Door, MoveDirection.Up, MoveSpeed.Normal, TargetSpec.LowestNeighborCeilingMinus4, KeyKind.None); // GR open stay
            Add(117,TriggerKind.Push, true,  false, SpecialCategory.Door, MoveDirection.Up, MoveSpeed.Turbo,  TargetSpec.LowestNeighborCeilingMinus4, KeyKind.None); // DR turbo open/close
            Add(118,TriggerKind.Push, false, false, SpecialCategory.Door, MoveDirection.Up, MoveSpeed.Turbo,  TargetSpec.LowestNeighborCeilingMinus4, KeyKind.None); // D1 turbo open stay

            // ── Locked manual doors ───────────────────────────────────────────
            Add(26, TriggerKind.Push, true,  false, SpecialCategory.LockedDoor, MoveDirection.Up, MoveSpeed.Normal, TargetSpec.LowestNeighborCeilingMinus4, KeyKind.BlueCard);   // DR blue
            Add(28, TriggerKind.Push, true,  false, SpecialCategory.LockedDoor, MoveDirection.Up, MoveSpeed.Normal, TargetSpec.LowestNeighborCeilingMinus4, KeyKind.RedCard);    // DR red
            Add(27, TriggerKind.Push, true,  false, SpecialCategory.LockedDoor, MoveDirection.Up, MoveSpeed.Normal, TargetSpec.LowestNeighborCeilingMinus4, KeyKind.YellowCard); // DR yellow
            Add(32, TriggerKind.Push, false, false, SpecialCategory.LockedDoor, MoveDirection.Up, MoveSpeed.Normal, TargetSpec.LowestNeighborCeilingMinus4, KeyKind.BlueCard);   // D1 blue
            Add(33, TriggerKind.Push, false, false, SpecialCategory.LockedDoor, MoveDirection.Up, MoveSpeed.Normal, TargetSpec.LowestNeighborCeilingMinus4, KeyKind.RedCard);    // D1 red
            Add(34, TriggerKind.Push, false, false, SpecialCategory.LockedDoor, MoveDirection.Up, MoveSpeed.Normal, TargetSpec.LowestNeighborCeilingMinus4, KeyKind.YellowCard); // D1 yellow
            Add(99, TriggerKind.Switch, true,  false, SpecialCategory.LockedDoor, MoveDirection.Up, MoveSpeed.Turbo,  TargetSpec.LowestNeighborCeilingMinus4, KeyKind.BlueCard);   // SR blue turbo open stay
            Add(133,TriggerKind.Switch, false, false, SpecialCategory.LockedDoor, MoveDirection.Up, MoveSpeed.Turbo,  TargetSpec.LowestNeighborCeilingMinus4, KeyKind.BlueCard);   // S1 blue turbo open stay
            Add(134,TriggerKind.Switch, true,  false, SpecialCategory.LockedDoor, MoveDirection.Up, MoveSpeed.Turbo,  TargetSpec.LowestNeighborCeilingMinus4, KeyKind.RedCard);    // SR red turbo open stay
            Add(135,TriggerKind.Switch, false, false, SpecialCategory.LockedDoor, MoveDirection.Up, MoveSpeed.Turbo,  TargetSpec.LowestNeighborCeilingMinus4, KeyKind.RedCard);    // S1 red turbo open stay
            Add(136,TriggerKind.Switch, true,  false, SpecialCategory.LockedDoor, MoveDirection.Up, MoveSpeed.Turbo,  TargetSpec.LowestNeighborCeilingMinus4, KeyKind.YellowCard); // SR yellow turbo open stay
            Add(137,TriggerKind.Switch, false, false, SpecialCategory.LockedDoor, MoveDirection.Up, MoveSpeed.Turbo,  TargetSpec.LowestNeighborCeilingMinus4, KeyKind.YellowCard); // S1 yellow turbo open stay

            // ── Switched / walk / gun doors ───────────────────────────────────
            Add(63, TriggerKind.Switch, true,  false, SpecialCategory.Door, MoveDirection.Up, MoveSpeed.Normal, TargetSpec.LowestNeighborCeilingMinus4, KeyKind.None); // SR open/close
            Add(29, TriggerKind.Switch, false, false, SpecialCategory.Door, MoveDirection.Up, MoveSpeed.Normal, TargetSpec.LowestNeighborCeilingMinus4, KeyKind.None); // S1 open/close
            Add(61, TriggerKind.Switch, true,  false, SpecialCategory.Door, MoveDirection.Up, MoveSpeed.Normal, TargetSpec.LowestNeighborCeilingMinus4, KeyKind.None); // SR open stay
            Add(103,TriggerKind.Switch, false, false, SpecialCategory.Door, MoveDirection.Up, MoveSpeed.Normal, TargetSpec.LowestNeighborCeilingMinus4, KeyKind.None); // S1 open stay
            Add(42, TriggerKind.Switch, true,  false, SpecialCategory.Door, MoveDirection.Down, MoveSpeed.Normal, TargetSpec.ToFloor, KeyKind.None); // SR close
            Add(50, TriggerKind.Switch, false, false, SpecialCategory.Door, MoveDirection.Down, MoveSpeed.Normal, TargetSpec.ToFloor, KeyKind.None); // S1 close
            Add(116,TriggerKind.Switch, true,  false, SpecialCategory.Door, MoveDirection.Down, MoveSpeed.Turbo,  TargetSpec.ToFloor, KeyKind.None); // SR turbo close
            Add(115,TriggerKind.Switch, false, false, SpecialCategory.Door, MoveDirection.Down, MoveSpeed.Turbo,  TargetSpec.ToFloor, KeyKind.None); // S1 turbo close
            Add(114,TriggerKind.Switch, true,  false, SpecialCategory.Door, MoveDirection.Up, MoveSpeed.Turbo,  TargetSpec.LowestNeighborCeilingMinus4, KeyKind.None); // SR turbo open/close
            Add(111,TriggerKind.Switch, false, false, SpecialCategory.Door, MoveDirection.Up, MoveSpeed.Turbo,  TargetSpec.LowestNeighborCeilingMinus4, KeyKind.None); // S1 turbo open/close
            Add(113,TriggerKind.Switch, true,  false, SpecialCategory.Door, MoveDirection.Up, MoveSpeed.Turbo,  TargetSpec.LowestNeighborCeilingMinus4, KeyKind.None); // SR turbo open stay
            Add(112,TriggerKind.Switch, false, false, SpecialCategory.Door, MoveDirection.Up, MoveSpeed.Turbo,  TargetSpec.LowestNeighborCeilingMinus4, KeyKind.None); // S1 turbo open stay

            Add(90, TriggerKind.Walk,   true,  false, SpecialCategory.Door, MoveDirection.Up, MoveSpeed.Normal, TargetSpec.LowestNeighborCeilingMinus4, KeyKind.None); // WR open/close
            Add(4,  TriggerKind.Walk,   false, true,  SpecialCategory.Door, MoveDirection.Up, MoveSpeed.Normal, TargetSpec.LowestNeighborCeilingMinus4, KeyKind.None); // W1 open/close
            Add(86, TriggerKind.Walk,   true,  false, SpecialCategory.Door, MoveDirection.Up, MoveSpeed.Normal, TargetSpec.LowestNeighborCeilingMinus4, KeyKind.None); // WR open stay
            Add(2,  TriggerKind.Walk,   false, false, SpecialCategory.Door, MoveDirection.Up, MoveSpeed.Normal, TargetSpec.LowestNeighborCeilingMinus4, KeyKind.None); // W1 open stay
            Add(75, TriggerKind.Walk,   true,  false, SpecialCategory.Door, MoveDirection.Down, MoveSpeed.Normal, TargetSpec.ToFloor, KeyKind.None); // WR close
            Add(3,  TriggerKind.Walk,   false, false, SpecialCategory.Door, MoveDirection.Down, MoveSpeed.Normal, TargetSpec.ToFloor, KeyKind.None); // W1 close
            Add(16, TriggerKind.Walk,   false, false, SpecialCategory.Door, MoveDirection.Down, MoveSpeed.Normal, TargetSpec.ToFloor, KeyKind.None); // W1 close, then open after 30s
            Add(76, TriggerKind.Walk,   true,  false, SpecialCategory.Door, MoveDirection.Down, MoveSpeed.Normal, TargetSpec.ToFloor, KeyKind.None); // WR close, then open after 30s
            Add(105,TriggerKind.Walk,   true,  false, SpecialCategory.Door, MoveDirection.Up, MoveSpeed.Turbo,  TargetSpec.LowestNeighborCeilingMinus4, KeyKind.None); // WR turbo open/close
            Add(108,TriggerKind.Walk,   false, false, SpecialCategory.Door, MoveDirection.Up, MoveSpeed.Turbo,  TargetSpec.LowestNeighborCeilingMinus4, KeyKind.None); // W1 turbo open/close
            Add(106,TriggerKind.Walk,   true,  false, SpecialCategory.Door, MoveDirection.Up, MoveSpeed.Turbo,  TargetSpec.LowestNeighborCeilingMinus4, KeyKind.None); // WR turbo open stay
            Add(109,TriggerKind.Walk,   false, false, SpecialCategory.Door, MoveDirection.Up, MoveSpeed.Turbo,  TargetSpec.LowestNeighborCeilingMinus4, KeyKind.None); // W1 turbo open stay
            Add(107,TriggerKind.Walk,   true,  false, SpecialCategory.Door, MoveDirection.Down, MoveSpeed.Turbo,  TargetSpec.ToFloor, KeyKind.None); // WR turbo close
            Add(110,TriggerKind.Walk,   false, false, SpecialCategory.Door, MoveDirection.Down, MoveSpeed.Turbo,  TargetSpec.ToFloor, KeyKind.None); // W1 turbo close

            // ── Lifts / platforms ─────────────────────────────────────────────
            Add(62, TriggerKind.Switch, true,  false, SpecialCategory.Plat, MoveDirection.Down, MoveSpeed.Fast, TargetSpec.LowestNeighborFloor, KeyKind.None); // SR lift
            Add(21, TriggerKind.Switch, false, false, SpecialCategory.Plat, MoveDirection.Down, MoveSpeed.Fast, TargetSpec.LowestNeighborFloor, KeyKind.None); // S1 lift
            Add(88, TriggerKind.Walk,   true,  true,  SpecialCategory.Plat, MoveDirection.Down, MoveSpeed.Fast, TargetSpec.LowestNeighborFloor, KeyKind.None); // WR lift
            Add(10, TriggerKind.Walk,   false, true,  SpecialCategory.Plat, MoveDirection.Down, MoveSpeed.Fast, TargetSpec.LowestNeighborFloor, KeyKind.None); // W1 lift
            Add(123,TriggerKind.Switch, true,  false, SpecialCategory.Plat, MoveDirection.Down, MoveSpeed.Turbo, TargetSpec.LowestNeighborFloor, KeyKind.None); // SR blazing lift
            Add(122,TriggerKind.Switch, false, false, SpecialCategory.Plat, MoveDirection.Down, MoveSpeed.Turbo, TargetSpec.LowestNeighborFloor, KeyKind.None); // S1 blazing lift
            Add(120,TriggerKind.Walk,   true,  false, SpecialCategory.Plat, MoveDirection.Down, MoveSpeed.Turbo, TargetSpec.LowestNeighborFloor, KeyKind.None); // WR blazing lift
            Add(121,TriggerKind.Walk,   false, false, SpecialCategory.Plat, MoveDirection.Down, MoveSpeed.Turbo, TargetSpec.LowestNeighborFloor, KeyKind.None); // W1 blazing lift
            Add(53, TriggerKind.Walk,   false, false, SpecialCategory.Plat, MoveDirection.Down, MoveSpeed.Slow, TargetSpec.LowestNeighborFloor, KeyKind.None); // W1 perpetual raise/lower
            Add(87, TriggerKind.Walk,   true,  false, SpecialCategory.Plat, MoveDirection.Down, MoveSpeed.Slow, TargetSpec.LowestNeighborFloor, KeyKind.None); // WR perpetual raise/lower
            Add(54, TriggerKind.Walk,   false, false, SpecialCategory.Plat, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // W1 stop perpetual plat
            Add(89, TriggerKind.Walk,   true,  false, SpecialCategory.Plat, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // WR stop perpetual plat
            Add(66, TriggerKind.Switch, true,  false, SpecialCategory.Plat, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // SR raise floor 24 + change texture
            Add(15, TriggerKind.Switch, false, false, SpecialCategory.Plat, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // S1 raise floor 24 + change texture
            Add(67, TriggerKind.Switch, true,  false, SpecialCategory.Plat, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // SR raise floor 32 + change texture
            Add(14, TriggerKind.Switch, false, false, SpecialCategory.Plat, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // S1 raise floor 32 + change texture
            Add(68, TriggerKind.Switch, true,  false, SpecialCategory.Plat, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.NextHigherFloor, KeyKind.None); // SR raise plat to next higher + change texture
            Add(20, TriggerKind.Switch, false, false, SpecialCategory.Plat, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.NextHigherFloor, KeyKind.None); // S1 raise plat to next higher + change texture
            Add(95, TriggerKind.Walk,   true,  false, SpecialCategory.Plat, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.NextHigherFloor, KeyKind.None); // WR raise plat to next higher + change texture
            Add(22, TriggerKind.Walk,   false, true,  SpecialCategory.Plat, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.NextHigherFloor, KeyKind.None); // W1 raise plat to next higher + change texture
            Add(47, TriggerKind.Gun,    false, false, SpecialCategory.Plat, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.NextHigherFloor, KeyKind.None); // G1 raise plat to next higher + change texture

            // ── Floor movers ──────────────────────────────────────────────────
            Add(18, TriggerKind.Switch, false, false, SpecialCategory.Floor, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.NextHigherFloor,      KeyKind.None); // S1 floor up to next higher
            Add(69, TriggerKind.Switch, true,  false, SpecialCategory.Floor, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.NextHigherFloor,      KeyKind.None); // SR floor up to next higher
            Add(119,TriggerKind.Walk,   false, false, SpecialCategory.Floor, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.NextHigherFloor,      KeyKind.None); // W1 floor up to next higher
            Add(128,TriggerKind.Walk,   true,  false, SpecialCategory.Floor, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.NextHigherFloor,      KeyKind.None); // WR floor up to next higher
            Add(131,TriggerKind.Switch, false, false, SpecialCategory.Floor, MoveDirection.Up,   MoveSpeed.Turbo,TargetSpec.NextHigherFloor,      KeyKind.None); // S1 floor up to next higher (turbo)
            Add(132,TriggerKind.Switch, true,  false, SpecialCategory.Floor, MoveDirection.Up,   MoveSpeed.Turbo,TargetSpec.NextHigherFloor,      KeyKind.None); // SR floor up to next higher (turbo)
            Add(129,TriggerKind.Walk,   true,  false, SpecialCategory.Floor, MoveDirection.Up,   MoveSpeed.Turbo,TargetSpec.NextHigherFloor,      KeyKind.None); // WR floor up to next higher (turbo)
            Add(130,TriggerKind.Walk,   false, false, SpecialCategory.Floor, MoveDirection.Up,   MoveSpeed.Turbo,TargetSpec.NextHigherFloor,      KeyKind.None); // W1 floor up to next higher (turbo)

            Add(101,TriggerKind.Switch, false, false, SpecialCategory.Floor, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.LowestNeighborCeiling, KeyKind.None); // S1 floor up to lowest ceiling
            Add(64, TriggerKind.Switch, true,  false, SpecialCategory.Floor, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.LowestNeighborCeiling, KeyKind.None); // SR floor up to lowest ceiling
            Add(5,  TriggerKind.Walk,   false, false, SpecialCategory.Floor, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.LowestNeighborCeiling, KeyKind.None); // W1 floor up to lowest ceiling
            Add(91, TriggerKind.Walk,   true,  false, SpecialCategory.Floor, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.LowestNeighborCeiling, KeyKind.None); // WR floor up to lowest ceiling
            Add(24, TriggerKind.Gun,    false, false, SpecialCategory.Floor, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.LowestNeighborCeiling, KeyKind.None); // G1 floor up to lowest ceiling

            Add(55, TriggerKind.Switch, false, false, SpecialCategory.Floor, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.LowestNeighborCeiling, KeyKind.None); // S1 floor up to 8 below lowest ceiling (crush)
            Add(65, TriggerKind.Switch, true,  false, SpecialCategory.Floor, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.LowestNeighborCeiling, KeyKind.None); // SR floor up to 8 below lowest ceiling (crush)
            Add(56, TriggerKind.Walk,   false, false, SpecialCategory.Floor, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.LowestNeighborCeiling, KeyKind.None); // W1 floor up to 8 below lowest ceiling (crush)
            Add(94, TriggerKind.Walk,   true,  false, SpecialCategory.Floor, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.LowestNeighborCeiling, KeyKind.None); // WR floor up to 8 below lowest ceiling (crush)

            Add(58, TriggerKind.Walk,   false, false, SpecialCategory.Floor, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // W1 floor up 24
            Add(92, TriggerKind.Walk,   true,  false, SpecialCategory.Floor, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // WR floor up 24
            Add(59, TriggerKind.Walk,   false, false, SpecialCategory.Floor, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // W1 floor up 24 + change texture
            Add(93, TriggerKind.Walk,   true,  false, SpecialCategory.Floor, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // WR floor up 24 + change texture

            Add(140,TriggerKind.Switch, false, false, SpecialCategory.Floor, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // S1 floor up 512

            Add(23, TriggerKind.Switch, false, false, SpecialCategory.Floor, MoveDirection.Down, MoveSpeed.Slow, TargetSpec.LowestNeighborFloor,  KeyKind.None); // S1 floor down to lowest
            Add(70, TriggerKind.Switch, true,  false, SpecialCategory.Floor, MoveDirection.Down, MoveSpeed.Fast, TargetSpec.HighestNeighborFloor, KeyKind.None); // SR floor down to 8 above highest (turbo)
            Add(71, TriggerKind.Switch, false, false, SpecialCategory.Floor, MoveDirection.Down, MoveSpeed.Fast, TargetSpec.HighestNeighborFloor, KeyKind.None); // S1 floor down to 8 above highest (turbo)
            Add(45, TriggerKind.Switch, true,  false, SpecialCategory.Floor, MoveDirection.Down, MoveSpeed.Slow, TargetSpec.HighestNeighborFloor, KeyKind.None); // SR floor down to highest
            Add(60, TriggerKind.Switch, true,  false, SpecialCategory.Floor, MoveDirection.Down, MoveSpeed.Slow, TargetSpec.LowestNeighborFloor,  KeyKind.None); // SR floor down to lowest
            Add(82, TriggerKind.Walk,   true,  false, SpecialCategory.Floor, MoveDirection.Down, MoveSpeed.Slow, TargetSpec.LowestNeighborFloor,  KeyKind.None); // WR floor down to lowest
            Add(38, TriggerKind.Walk,   false, false, SpecialCategory.Floor, MoveDirection.Down, MoveSpeed.Slow, TargetSpec.LowestNeighborFloor,  KeyKind.None); // W1 floor down to lowest
            Add(84, TriggerKind.Walk,   true,  false, SpecialCategory.Floor, MoveDirection.Down, MoveSpeed.Slow, TargetSpec.LowestNeighborFloor,  KeyKind.None); // WR floor down to lowest + change texture/type
            Add(37, TriggerKind.Walk,   false, false, SpecialCategory.Floor, MoveDirection.Down, MoveSpeed.Slow, TargetSpec.LowestNeighborFloor,  KeyKind.None); // W1 floor down to lowest + change texture/type
            Add(83, TriggerKind.Walk,   true,  false, SpecialCategory.Floor, MoveDirection.Down, MoveSpeed.Slow, TargetSpec.HighestNeighborFloor, KeyKind.None); // WR floor down to highest
            Add(19, TriggerKind.Walk,   false, false, SpecialCategory.Floor, MoveDirection.Down, MoveSpeed.Slow, TargetSpec.HighestNeighborFloor, KeyKind.None); // W1 floor down to highest
            Add(102,TriggerKind.Switch, false, false, SpecialCategory.Floor, MoveDirection.Down, MoveSpeed.Slow, TargetSpec.HighestNeighborFloor, KeyKind.None); // S1 floor down to highest
            Add(36, TriggerKind.Walk,   false, false, SpecialCategory.Floor, MoveDirection.Down, MoveSpeed.Fast, TargetSpec.HighestNeighborFloor, KeyKind.None); // W1 floor down to 8 above highest (turbo)
            Add(98, TriggerKind.Walk,   true,  false, SpecialCategory.Floor, MoveDirection.Down, MoveSpeed.Fast, TargetSpec.HighestNeighborFloor, KeyKind.None); // WR floor down to 8 above highest (turbo)
            Add(30, TriggerKind.Walk,   false, false, SpecialCategory.Floor, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // W1 floor up by shortest lower texture
            Add(96, TriggerKind.Walk,   true,  false, SpecialCategory.Floor, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // WR floor up by shortest lower texture

            // ── Donut ─────────────────────────────────────────────────────────
            Add(9,  TriggerKind.Switch, false, false, SpecialCategory.Donut, MoveDirection.Down, MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // S1 donut (raise inner, lower outer)

            // ── Ceilings ──────────────────────────────────────────────────────
            Add(40, TriggerKind.Walk,   false, false, SpecialCategory.Ceiling, MoveDirection.Up,   MoveSpeed.Slow, TargetSpec.HighestNeighborFloor, KeyKind.None); // W1 ceiling up to highest ceiling
            Add(41, TriggerKind.Switch, false, false, SpecialCategory.Ceiling, MoveDirection.Down, MoveSpeed.Slow, TargetSpec.ToFloor, KeyKind.None); // S1 ceiling down to floor
            Add(43, TriggerKind.Switch, true,  false, SpecialCategory.Ceiling, MoveDirection.Down, MoveSpeed.Slow, TargetSpec.ToFloor, KeyKind.None); // SR ceiling down to floor
            Add(44, TriggerKind.Walk,   false, false, SpecialCategory.Ceiling, MoveDirection.Down, MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // W1 ceiling down to 8 above floor (crush)
            Add(72, TriggerKind.Walk,   true,  false, SpecialCategory.Ceiling, MoveDirection.Down, MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // WR ceiling down to 8 above floor (crush)

            // ── Crushers ──────────────────────────────────────────────────────
            Add(6,  TriggerKind.Walk,   false, false, SpecialCategory.Crusher, MoveDirection.Down, MoveSpeed.Normal, TargetSpec.None, KeyKind.None); // W1 fast crusher
            Add(25, TriggerKind.Walk,   false, false, SpecialCategory.Crusher, MoveDirection.Down, MoveSpeed.Slow,   TargetSpec.None, KeyKind.None); // W1 slow crusher
            Add(73, TriggerKind.Walk,   true,  false, SpecialCategory.Crusher, MoveDirection.Down, MoveSpeed.Slow,   TargetSpec.None, KeyKind.None); // WR slow crusher
            Add(77, TriggerKind.Walk,   true,  false, SpecialCategory.Crusher, MoveDirection.Down, MoveSpeed.Normal, TargetSpec.None, KeyKind.None); // WR fast crusher
            Add(49, TriggerKind.Switch, false, false, SpecialCategory.Crusher, MoveDirection.Down, MoveSpeed.Slow,   TargetSpec.None, KeyKind.None); // S1 ceiling crush + raise (slow crusher)
            Add(57, TriggerKind.Walk,   false, false, SpecialCategory.Crusher, MoveDirection.Up,   MoveSpeed.Slow,   TargetSpec.None, KeyKind.None); // W1 stop crusher
            Add(74, TriggerKind.Walk,   true,  false, SpecialCategory.Crusher, MoveDirection.Up,   MoveSpeed.Slow,   TargetSpec.None, KeyKind.None); // WR stop crusher
            Add(141,TriggerKind.Walk,   false, false, SpecialCategory.Crusher, MoveDirection.Down, MoveSpeed.Slow,   TargetSpec.None, KeyKind.None); // W1 silent crusher

            // ── Stairs ────────────────────────────────────────────────────────
            Add(8,  TriggerKind.Walk,   false, false, SpecialCategory.Stair, MoveDirection.Up, MoveSpeed.Slow, TargetSpec.StairStep, KeyKind.None); // W1 stairs 8
            Add(7,  TriggerKind.Switch, false, false, SpecialCategory.Stair, MoveDirection.Up, MoveSpeed.Slow, TargetSpec.StairStep, KeyKind.None); // S1 stairs 8
            Add(100,TriggerKind.Walk,   false, false, SpecialCategory.Stair, MoveDirection.Up, MoveSpeed.Fast, TargetSpec.StairStep, KeyKind.None); // W1 stairs 16 (turbo, crush)
            Add(127,TriggerKind.Switch, false, false, SpecialCategory.Stair, MoveDirection.Up, MoveSpeed.Fast, TargetSpec.StairStep, KeyKind.None); // S1 stairs 16 (turbo, crush)

            // ── Lights (classified, not executed) ─────────────────────────────
            Add(35, TriggerKind.Walk,   false, false, SpecialCategory.Light, MoveDirection.Up, MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // W1 light to 35
            Add(104,TriggerKind.Walk,   false, false, SpecialCategory.Light, MoveDirection.Up, MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // W1 light to lowest neighbour
            Add(12, TriggerKind.Walk,   false, false, SpecialCategory.Light, MoveDirection.Up, MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // W1 light to highest neighbour
            Add(13, TriggerKind.Walk,   false, false, SpecialCategory.Light, MoveDirection.Up, MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // W1 light to 255
            Add(17, TriggerKind.Walk,   false, false, SpecialCategory.Light, MoveDirection.Up, MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // W1 light blink
            Add(79, TriggerKind.Walk,   true,  false, SpecialCategory.Light, MoveDirection.Up, MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // WR light to 35
            Add(80, TriggerKind.Walk,   true,  false, SpecialCategory.Light, MoveDirection.Up, MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // WR light to highest neighbour
            Add(81, TriggerKind.Walk,   true,  false, SpecialCategory.Light, MoveDirection.Up, MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // WR light to 255
            Add(138,TriggerKind.Switch, true,  false, SpecialCategory.Light, MoveDirection.Up, MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // SR light to 255
            Add(139,TriggerKind.Switch, true,  false, SpecialCategory.Light, MoveDirection.Up, MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // SR light to 35

            // ── Teleports (executable in Stage 7e) ────────────────────────────
            Add(39, TriggerKind.Walk,   false, true,  SpecialCategory.Teleport, MoveDirection.Up, MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // W1 teleport
            Add(97, TriggerKind.Walk,   true,  true,  SpecialCategory.Teleport, MoveDirection.Up, MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // WR teleport
            Add(125,TriggerKind.Walk,   false, true,  SpecialCategory.Teleport, MoveDirection.Up, MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // W1 teleport (monsters only)
            Add(126,TriggerKind.Walk,   true,  true,  SpecialCategory.Teleport, MoveDirection.Up, MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // WR teleport (monsters only)

            // ── Exits (classified, not executed) ──────────────────────────────
            Add(11, TriggerKind.Switch, false, false, SpecialCategory.Exit, MoveDirection.Up, MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // S1 exit (normal)
            Add(51, TriggerKind.Switch, false, false, SpecialCategory.Exit, MoveDirection.Up, MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // S1 exit (secret)
            Add(52, TriggerKind.Walk,   false, false, SpecialCategory.Exit, MoveDirection.Up, MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // W1 exit (normal)
            Add(124,TriggerKind.Walk,   false, false, SpecialCategory.Exit, MoveDirection.Up, MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // W1 exit (secret)

            // ── Scrolling textures (classified, not executed) ─────────────────
            Add(48, TriggerKind.Push,   true,  false, SpecialCategory.Scroll, MoveDirection.Up, MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // scroll wall left
            Add(85, TriggerKind.Push,   true,  false, SpecialCategory.Scroll, MoveDirection.Up, MoveSpeed.Slow, TargetSpec.None, KeyKind.None); // scroll wall right (Boom-era; harmless to record)

            return d;
        }
    }
}
