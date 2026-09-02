using System.Collections.Generic;

namespace Doom.Things
{
    /// Looping frame animation for decorations, straight out of info.c.
    ///
    /// The port had none: `PickupAnimationTable` covers bonuses, keys, armor
    /// and the barrel, so every firestick stood frozen on frame A in Classic
    /// too — vanilla runs all six through four frames at four tics each
    /// (S_TBLUETORCH, S_TGREENTORCH, S_TREDTORCH, S_SMBTORCH, S_SMGTORCH,
    /// S_SMRTORCH). Enhanced 3D reads the same cadence, so stand-still fire
    /// would have been the only difference between the modes.
    public static class DecorationAnimationTable
    {
        static readonly int[] FourFrames = { 0, 1, 2, 3 };
        static readonly int[] FourTics = { 4, 4, 4, 4 };
        static readonly int[] TwoFrames = { 0, 1 };
        static readonly int[] HeartTics = { 14, 14 };
        static readonly int[] ThreeFrames = { 0, 1, 2 };
        static readonly int[] ThreeSixTics = { 6, 6, 6 };
        static readonly int[] TwitchFrames = { 0, 1, 2, 1 };   // S_BLOODYTWITCH1-4
        static readonly int[] TwitchTics = { 10, 15, 8, 6 };
        static readonly int[] LiveStickTics = { 6, 8 };        // S_LIVESTICK1-2
        static readonly int[] EyeFrames = { 0, 1, 2, 1 };      // S_EVILEYE1-4
        static readonly int[] EyeTics = { 6, 6, 6, 6 };
        static readonly int[] TwoSixTics = { 6, 6 };           // S_HEADCANDLES1-2

        static readonly Dictionary<int, PickupAnimation> Defs = new()
        {
            [42] = new PickupAnimation(ThreeFrames, ThreeSixTics), // FSKU (S_FLOATSKULL1-3)
            [49] = new PickupAnimation(TwitchFrames, TwitchTics),  // GOR1 hanging, twitching
            [63] = new PickupAnimation(TwitchFrames, TwitchTics),  // GOR1 non-solid
            [26] = new PickupAnimation(TwoFrames, LiveStickTics),  // POL6 twitching impaled
            [41] = new PickupAnimation(EyeFrames, EyeTics),        // CEYE evil eye
            [29] = new PickupAnimation(TwoFrames, TwoSixTics),     // POL3 skulls + candles
            [44] = new PickupAnimation(FourFrames, FourTics),   // TBLU
            [45] = new PickupAnimation(FourFrames, FourTics),   // TGRN
            [46] = new PickupAnimation(FourFrames, FourTics),   // TRED
            [55] = new PickupAnimation(FourFrames, FourTics),   // SMBT
            [56] = new PickupAnimation(FourFrames, FourTics),   // SMGT
            [57] = new PickupAnimation(FourFrames, FourTics),   // SMRT
            [36] = new PickupAnimation(TwoFrames, HeartTics),   // COL5 (S_HEARTCOL)
        };

        public static bool TryGet(int doomEdNum, out PickupAnimation animation)
            => Defs.TryGetValue(doomEdNum, out animation);

        public static IEnumerable<KeyValuePair<int, PickupAnimation>> All => Defs;
    }
}
