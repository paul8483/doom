using NUnit.Framework;
using Doom.MapBuild;

namespace Doom.Map.Tests
{
    /// Cue tables per mover kind against p_doors.c / p_floor.c / p_plats.c.
    public class MoverSoundProfileTests
    {
        [Test]
        public void Lift_plays_pstart_at_each_start_and_pstop_at_each_stop_with_no_motor_loop()
        {
            var lift = MoverSoundProfile.Lift;
            Assert.That(lift.StartLump, Is.EqualTo("DSPSTART"));
            Assert.That(lift.ReturnLump, Is.EqualTo("DSPSTART"));
            Assert.That(lift.StopLump, Is.EqualTo("DSPSTOP"));
            Assert.That(lift.LoopLump, Is.Null, "T_PlatRaise never plays stnmov for a down-wait-up plat");
        }

        [Test]
        public void Floor_grinds_stnmov_while_moving_and_stops_with_pstop()
        {
            var floor = MoverSoundProfile.FloorOrLift;
            Assert.That(floor.LoopLump, Is.EqualTo("DSSTNMOV"));
            Assert.That(floor.StopLump, Is.EqualTo("DSPSTOP"));
            Assert.That(floor.StartLump, Is.Null);
            Assert.That(floor.ReturnLump, Is.Null);
        }

        [Test]
        public void Door_has_open_and_close_cues_only()
        {
            var door = MoverSoundProfile.Door;
            Assert.That(door.StartLump, Is.EqualTo("DSDOROPN"));
            Assert.That(door.ReturnLump, Is.EqualTo("DSDORCLS"));
            Assert.That(door.LoopLump, Is.Null);
            Assert.That(door.StopLump, Is.Null, "a door reaching its end plays nothing");
        }
    }
}
