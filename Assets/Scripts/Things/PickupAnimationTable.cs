using System.Collections.Generic;

namespace Doom.Things
{
    public sealed class PickupAnimation
    {
        public readonly int[] Frames;
        public readonly int[] Tics;

        public PickupAnimation(int[] frames, int[] tics)
        {
            Frames = frames;
            Tics = tics;
        }
    }

    /// Animated pickup spawn states from info.c. Static pickups are intentionally
    /// absent, so callers can attach one animator to both map items and drops.
    public static class PickupAnimationTable
    {
        static readonly int[] FourFrames = { 0, 1, 2, 3 };
        static readonly int[] FourTics = { 6, 6, 6, 6 };
        static readonly int[] TwoFrames = { 0, 1 };
        static readonly int[] TwoTics = { 6, 6 };
        static readonly int[] KeyTics = { 10, 10 };

        static readonly Dictionary<int, PickupAnimation> Defs = new()
        {
            [2014] = new PickupAnimation(FourFrames, FourTics), // BON1
            [2015] = new PickupAnimation(FourFrames, FourTics), // BON2
            [2013] = new PickupAnimation(FourFrames, FourTics), // SOUL
            [2018] = new PickupAnimation(TwoFrames, TwoTics),   // ARM1
            [2019] = new PickupAnimation(TwoFrames, TwoTics),   // ARM2
            [5] = new PickupAnimation(TwoFrames, KeyTics),      // BKEY
            [13] = new PickupAnimation(TwoFrames, KeyTics),     // RKEY
            [6] = new PickupAnimation(TwoFrames, KeyTics),      // YKEY
            [40] = new PickupAnimation(TwoFrames, KeyTics),     // BSKU
            [38] = new PickupAnimation(TwoFrames, KeyTics),     // RSKU
            [39] = new PickupAnimation(TwoFrames, KeyTics),     // YSKU
        };

        public static bool TryGet(int doomEdNum, out PickupAnimation animation)
            => Defs.TryGetValue(doomEdNum, out animation);

        public static IEnumerable<KeyValuePair<int, PickupAnimation>> All => Defs;
    }
}
