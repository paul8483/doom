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

        [Test]
        public void Rocket_uses_canonical_direct_and_splash_constants()
        {
            Assert.That(RocketRules.DirectDamageMult, Is.EqualTo(20));
            Assert.That(RocketRules.DirectDamageMod, Is.EqualTo(8));
            Assert.That(RocketRules.SplashDamage, Is.EqualTo(128));
            Assert.That(RocketRules.SplashRadiusDoom, Is.EqualTo(128f));
            Assert.That(
                RadiusDamageRules.DamageAt(RocketRules.SplashDamage, 64f),
                Is.EqualTo(64));
        }
    }
}
