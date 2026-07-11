using System.Collections.Generic;
using NUnit.Framework;
using Doom.Game;

namespace Doom.Game.Tests
{
    public class BfgRulesTests
    {
        [Test]
        public void Snapshot_type_and_motion_constants()
        {
            Assert.That(BfgRules.SnapshotType, Is.EqualTo(2006));
            Assert.That(BfgRules.SpeedDoomPerTic, Is.EqualTo(25));
            Assert.That(BfgRules.RadiusDoom, Is.EqualTo(13f));
            Assert.That(BfgRules.HeightDoom, Is.EqualTo(8f));
            Assert.That(BfgRules.Sprite, Is.EqualTo("BFS1"));
            Assert.That(BfgRules.ExplodeSprite, Is.EqualTo("BFE1"));
            Assert.That(BfgRules.TracerSprite, Is.EqualTo("BFE2"));
            Assert.That(BfgRules.ExplodeSound, Is.EqualTo("DSRXPLOD"));
        }

        [Test]
        public void Fly_impact_and_spray_timing_match_info_c()
        {
            Assert.That(BfgRules.FlyFrames, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(BfgRules.FlyTics, Is.EqualTo(new[] { 4, 4 }));
            Assert.That(BfgRules.ExplodeFrames, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 }));
            Assert.That(BfgRules.ExplodeTics, Is.EqualTo(new[] { 8, 8, 8, 8, 8, 8 }));
            Assert.That(BfgRules.SprayFrameIndex, Is.EqualTo(2));
            Assert.That(BfgRules.SprayAfterImpactTics, Is.EqualTo(16));
            Assert.That(BfgRules.TracerRangeDoom, Is.EqualTo(1024f));
            Assert.That(BfgRules.TracerCount, Is.EqualTo(40));
        }

        [Test]
        public void Tracer_fan_offsets_span_minus_45_to_plus_42_75()
        {
            Assert.That(BfgRules.TracerYawOffsetDeg(0), Is.EqualTo(-45f).Within(0.0001f));
            Assert.That(BfgRules.TracerYawOffsetDeg(39), Is.EqualTo(42.75f).Within(0.0001f));
            Assert.That(BfgRules.FanStepDeg, Is.EqualTo(2.25f).Within(0.0001f));
        }

        [Test]
        public void Direct_damage_is_100_to_800()
        {
            var r = new DoomRandom(0);
            for (int i = 0; i < 256; i++)
            {
                int d = BfgRules.RollDirectDamage(r);
                Assert.That(d, Is.InRange(100, 800));
                Assert.That(d % 100, Is.EqualTo(0));
            }
        }

        [Test]
        public void Tracer_damage_uses_fifteen_literal_rolls()
        {
            var r = new DoomRandom(7);
            int expected = 0;
            var probe = new DoomRandom(7);
            for (int i = 0; i < 15; i++)
                expected += (probe.Next() & 7) + 1;

            Assert.That(BfgRules.RollTracerDamage(r), Is.EqualTo(expected));
            Assert.That(r.Index, Is.EqualTo(probe.Index));
        }

        [Test]
        public void BuildTracers_emits_forty_deterministic_shots()
        {
            var shots = new List<BfgTracerShot>();
            var r = new DoomRandom(3);
            BfgRules.BuildTracers(r, shots);

            Assert.That(shots.Count, Is.EqualTo(40));
            Assert.That(shots[0].YawOffsetDeg, Is.EqualTo(-45f).Within(0.0001f));
            Assert.That(shots[39].YawOffsetDeg, Is.EqualTo(42.75f).Within(0.0001f));
            foreach (var s in shots)
                Assert.That(s.Damage, Is.InRange(15, 120));
        }
    }
}
