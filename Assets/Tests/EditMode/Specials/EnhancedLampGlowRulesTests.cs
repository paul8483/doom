using NUnit.Framework;

namespace Doom.Specials.Tests
{
    public class EnhancedLampGlowRulesTests
    {
        [Test]
        public void TlITE6_6_special_0_is_eligible()
        {
            Assert.IsTrue(EnhancedLampGlowRules.IsEligible("TLITE6_6", 0));
        }

        [Test]
        public void TlITE6_5_special_8_is_not_eligible()
        {
            Assert.IsFalse(EnhancedLampGlowRules.IsEligible("TLITE6_5", 8));
        }

        [Test]
        public void Flat2_panel_light_special_0_is_eligible()
        {
            Assert.IsTrue(EnhancedLampGlowRules.IsEligible("FLAT2", 0));
            Assert.IsTrue(EnhancedLampGlowRules.IsEligible("floor1_7", 0));
            Assert.IsTrue(EnhancedLampGlowRules.IsEligible("CEIL1_2", 0));
        }

        [Test]
        public void Aqlite_and_lite_wall_textures_are_eligible()
        {
            Assert.IsTrue(EnhancedLampGlowRules.IsEligible("AQLITE18", 0));
            Assert.IsTrue(EnhancedLampGlowRules.IsEligible("aqlite07", 0));
            Assert.IsTrue(EnhancedLampGlowRules.IsEligible("LITE5", 0));
            Assert.IsTrue(EnhancedLampGlowRules.IsEligible("LITE3", 0));
        }

        [Test]
        public void Freedoom_AQF_panel_lamp_flats_are_eligible()
        {
            Assert.IsTrue(EnhancedLampGlowRules.IsEligible("AQF010", 0));
            Assert.IsTrue(EnhancedLampGlowRules.IsEligible("aqf012", 0));
            Assert.IsTrue(EnhancedLampGlowRules.IsEligible("AQF014", 0));
            // Generic tech AQF ceilings are not panel lamps.
            Assert.IsFalse(EnhancedLampGlowRules.IsEligible("AQF032", 0));
            Assert.IsFalse(EnhancedLampGlowRules.IsEligible("AQF054", 0));
        }

        [Test]
        public void Non_light_ceiling_flat_is_not_eligible()
        {
            Assert.IsFalse(EnhancedLampGlowRules.IsEligible("FLOOR5_2", 0));
            Assert.IsFalse(EnhancedLampGlowRules.IsEligible("STARTAN3", 0));
        }

        [Test]
        public void Lowercase_tlite_prefix_is_eligible()
        {
            Assert.IsTrue(EnhancedLampGlowRules.IsEligible("tlite6_6", 0));
        }

        [Test]
        public void Fire_flicker_special_17_is_not_eligible()
        {
            Assert.IsFalse(EnhancedLampGlowRules.IsEligible("TLITE6_6", 17));
            Assert.IsFalse(EnhancedLampGlowRules.IsEligible("FLAT2", 8));
        }

        [Test]
        public void Null_or_empty_ceiling_is_not_eligible()
        {
            Assert.IsFalse(EnhancedLampGlowRules.IsEligible(null, 0));
            Assert.IsFalse(EnhancedLampGlowRules.IsEligible("", 0));
        }
    }
}
