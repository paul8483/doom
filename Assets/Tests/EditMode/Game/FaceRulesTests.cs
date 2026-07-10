using NUnit.Framework;

namespace Doom.Game.Tests
{
    public class FaceRulesTests
    {
        [TestCase(100, 0)]
        [TestCase(80, 0)]
        [TestCase(79, 1)]
        [TestCase(60, 1)]
        [TestCase(40, 2)]
        [TestCase(20, 3)]
        [TestCase(19, 4)]
        [TestCase(1, 4)]
        public void Pain_offset_bands(int health, int expected)
        {
            Assert.That(FaceRules.PainOffset(health), Is.EqualTo(expected));
        }

        [Test]
        public void Dead_beats_everything()
        {
            var face = new FaceState();
            face.Reset(100);
            face.OnWeaponPickup(100);
            Assert.That(face.PatchName, Does.StartWith("STFEVL"));

            face.OnDeath();
            Assert.That(face.PatchName, Is.EqualTo(FaceRules.DeadPatch));

            face.OnDamage(50, 30, FaceAttackerSide.Left);
            face.OnWeaponPickup(50);
            face.Advance(10, 50);
            Assert.That(face.PatchName, Is.EqualTo(FaceRules.DeadPatch));
        }

        [Test]
        public void Rapid_damage_ouch_beats_directional_and_grin()
        {
            var face = new FaceState();
            face.Reset(100);

            face.OnDamage(70, FaceRules.MuchPain, FaceAttackerSide.None);
            Assert.That(face.PatchName, Is.EqualTo("STFOUCH1"));

            face.OnDamage(70, 5, FaceAttackerSide.Right);
            Assert.That(face.PatchName, Is.EqualTo("STFOUCH1"), "directional must not interrupt ouch");

            face.OnWeaponPickup(70);
            Assert.That(face.PatchName, Is.EqualTo("STFOUCH1"), "grin must not interrupt ouch");
        }

        [Test]
        public void Directional_pain_beats_evil_grin()
        {
            var face = new FaceState();
            face.Reset(100);

            face.OnDamage(90, 5, FaceAttackerSide.Left);
            Assert.That(face.PatchName, Is.EqualTo("STFTL00"));

            face.OnWeaponPickup(90);
            Assert.That(face.PatchName, Is.EqualTo("STFTL00"));
        }

        [Test]
        public void Evil_grin_beats_idle()
        {
            var face = new FaceState();
            face.Reset(100);
            Assert.That(face.PatchName, Is.EqualTo("STFST00"));

            face.OnWeaponPickup(100);
            Assert.That(face.PatchName, Is.EqualTo("STFEVL0"));
        }

        [Test]
        public void Timers_are_deterministic_in_tics()
        {
            var face = new FaceState();
            face.Reset(100);
            face.OnWeaponPickup(100);
            Assert.That(face.PatchName, Is.EqualTo("STFEVL0"));

            face.Advance(FaceRules.EvilGrinTics - 1, 100);
            Assert.That(face.PatchName, Is.EqualTo("STFEVL0"));

            face.Advance(1, 100);
            Assert.That(face.PatchName, Is.EqualTo("STFST00"));
        }

        [Test]
        public void Idle_look_cycles_on_straight_tics()
        {
            var face = new FaceState();
            face.Reset(100);
            Assert.That(face.PatchName, Is.EqualTo("STFST00"));

            face.Advance(FaceRules.StraightTics, 100);
            Assert.That(face.PatchName, Is.EqualTo("STFST01")); // right

            face.Advance(FaceRules.StraightTics, 100);
            Assert.That(face.PatchName, Is.EqualTo("STFST02")); // left

            face.Advance(FaceRules.StraightTics, 100);
            Assert.That(face.PatchName, Is.EqualTo("STFST00")); // center
        }

        [Test]
        public void Low_health_changes_idle_band()
        {
            var face = new FaceState();
            face.Reset(15);
            Assert.That(face.PatchName, Is.EqualTo("STFST40"));
        }

        [Test]
        public void Advance_to_zero_health_selects_dead_face()
        {
            var face = new FaceState();
            face.Reset(10);
            face.Advance(1, 0);
            Assert.That(face.PatchName, Is.EqualTo(FaceRules.DeadPatch));
        }
    }
}
