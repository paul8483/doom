using NUnit.Framework;
using Doom.Game;

namespace Doom.Game.Tests
{
    public class RadiusDamageRulesTests
    {
        [Test]
        public void Point_blank_deals_full_damage()
        {
            Assert.That(RadiusDamageRules.BarrelDamageAt(0f),
                Is.EqualTo(RadiusDamageRules.BarrelMaxDamage));
        }

        [Test]
        public void Falloff_is_linear_with_distance()
        {
            Assert.That(RadiusDamageRules.BarrelDamageAt(28f), Is.EqualTo(100));
            Assert.That(RadiusDamageRules.BarrelDamageAt(64f), Is.EqualTo(64));
            Assert.That(RadiusDamageRules.BarrelDamageAt(127f), Is.EqualTo(1));
        }

        [Test]
        public void At_or_beyond_radius_deals_zero()
        {
            Assert.That(RadiusDamageRules.BarrelDamageAt(128f), Is.EqualTo(0));
            Assert.That(RadiusDamageRules.BarrelDamageAt(200f), Is.EqualTo(0));
        }

        [Test]
        public void Negative_distance_clamps_to_full()
        {
            Assert.That(RadiusDamageRules.BarrelDamageAt(-10f),
                Is.EqualTo(RadiusDamageRules.BarrelMaxDamage));
        }
    }
}
