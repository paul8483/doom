using NUnit.Framework;

namespace Doom.Specials.Tests
{
    public class SectorDamageTableTests
    {
        [Test]
        public void Nukage_special_7_does_5()
            => Assert.That(SectorDamageTable.DamagePerTick(7), Is.EqualTo(5));

        [Test]
        public void Hellslime_special_5_does_10()
            => Assert.That(SectorDamageTable.DamagePerTick(5), Is.EqualTo(10));

        [Test]
        public void Strobe_hurt_special_4_does_20()
            => Assert.That(SectorDamageTable.DamagePerTick(4), Is.EqualTo(20));

        [Test]
        public void Super_hellslime_special_16_does_20()
            => Assert.That(SectorDamageTable.DamagePerTick(16), Is.EqualTo(20));

        [Test]
        public void Exit_super_damage_special_11_does_20()
            => Assert.That(SectorDamageTable.DamagePerTick(11), Is.EqualTo(20));

        [Test]
        public void Non_damaging_specials_do_zero()
        {
            Assert.That(SectorDamageTable.DamagePerTick(0), Is.EqualTo(0));  // normal
            Assert.That(SectorDamageTable.DamagePerTick(9), Is.EqualTo(0));  // secret
            Assert.That(SectorDamageTable.DamagePerTick(1), Is.EqualTo(0));  // light blink
        }
    }
}
