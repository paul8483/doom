namespace Doom.MapBuild
{
    /// SFX profile for one <see cref="SectorMover"/> lifecycle.
    public readonly struct MoverSoundProfile
    {
        public readonly string StartLump;   // one-shot at begin (door open)
        public readonly string ReturnLump;  // one-shot when returning (door close)
        public readonly string LoopLump;    // looping while moving (floor/lift)
        public readonly string StopLump;    // one-shot when fully done (floor/lift)

        public MoverSoundProfile(string start = null, string ret = null,
                                 string loop = null, string stop = null)
        {
            StartLump = start;
            ReturnLump = ret;
            LoopLump = loop;
            StopLump = stop;
        }

        public static MoverSoundProfile Door =>
            new MoverSoundProfile(start: "DSDOROPN", ret: "DSDORCLS");

        public static MoverSoundProfile FloorOrLift =>
            new MoverSoundProfile(loop: "DSSTNMOV", stop: "DSPSTOP");
    }
}
