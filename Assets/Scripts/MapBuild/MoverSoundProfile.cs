namespace Doom.MapBuild
{
    /// SFX profile for one <see cref="SectorMover"/> lifecycle.
    public readonly struct MoverSoundProfile
    {
        public readonly string StartLump;   // one-shot at begin (door open / plat start)
        public readonly string ReturnLump;  // one-shot when returning (door close / plat restart)
        public readonly string LoopLump;    // looping while moving (floor / ceiling motor)
        public readonly string StopLump;    // one-shot at every stop (floor end, plat bottom and top)

        public MoverSoundProfile(string start = null, string ret = null,
                                 string loop = null, string stop = null)
        {
            StartLump = start;
            ReturnLump = ret;
            LoopLump = loop;
            StopLump = stop;
        }

        /// EV_VerticalDoor / T_VerticalDoor: doropn when it starts opening,
        /// dorcls when it starts closing, nothing at the ends.
        public static MoverSoundProfile Door =>
            new MoverSoundProfile(start: "DSDOROPN", ret: "DSDORCLS");

        /// T_MoveFloor / T_MoveCeiling: stnmov every 8 tics while the plane
        /// moves (a loop here), pstop when it reaches its destination. Also
        /// the raiseAndChange plats, which vanilla treats the same way.
        public static MoverSoundProfile FloorOrLift =>
            new MoverSoundProfile(loop: "DSSTNMOV", stop: "DSPSTOP");

        /// EV_DoPlat / T_PlatRaise for a down-wait-up lift: pstart when it
        /// sets off (down, and again up after the wait), pstop at the bottom
        /// and at the top — no motor loop at all. Until 2026-09-04 lifts ran
        /// the floor profile: a DSSTNMOV loop (a 0.27 s thump in Freedoom
        /// that reads as a repeated jump) and a single pstop at the top.
        public static MoverSoundProfile Lift =>
            new MoverSoundProfile(start: "DSPSTART", ret: "DSPSTART", stop: "DSPSTOP");
    }
}
