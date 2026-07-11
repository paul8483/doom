using NUnit.Framework;
using Doom.Game;

namespace Doom.Game.Tests
{
    public class PlasmaRulesTests
    {
        [Test]
        public void Snapshot_type_and_motion_constants()
        {
            Assert.That(PlasmaRules.SnapshotType, Is.EqualTo(2004));
            Assert.That(PlasmaRules.SpeedDoomPerTic, Is.EqualTo(25));
            Assert.That(PlasmaRules.RadiusDoom, Is.EqualTo(13f));
            Assert.That(PlasmaRules.HeightDoom, Is.EqualTo(8f));
            Assert.That(PlasmaRules.Sprite, Is.EqualTo("PLSS"));
            Assert.That(PlasmaRules.ExplodeSprite, Is.EqualTo("PLSE"));
            Assert.That(PlasmaRules.ExplodeSound, Is.EqualTo("DSFIRXPL"));
        }

        [Test]
        public void Fly_and_impact_frames_match_info_c()
        {
            Assert.That(PlasmaRules.FlyFrames, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(PlasmaRules.FlyTics, Is.EqualTo(new[] { 6, 6 }));
            Assert.That(PlasmaRules.ExplodeFrames, Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
            Assert.That(PlasmaRules.ExplodeTics, Is.EqualTo(new[] { 4, 4, 4, 4, 4 }));
        }

        [Test]
        public void Direct_damage_is_5_to_40()
        {
            var r = new DoomRandom(0);
            for (int i = 0; i < 256; i++)
            {
                int d = PlasmaRules.RollDirectDamage(r);
                Assert.That(d, Is.InRange(5, 40));
                Assert.That(d % 5, Is.EqualTo(0));
            }
        }
    }
}
