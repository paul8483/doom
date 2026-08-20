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

        static readonly Dictionary<int, PickupAnimation> Defs = new()
        {
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
