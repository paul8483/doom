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
        public void Non_TLITE_flat_is_not_eligible()
        {
            Assert.IsFalse(EnhancedLampGlowRules.IsEligible("FLOOR5_2", 0));
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
        }

        [Test]
        public void Null_or_empty_ceiling_is_not_eligible()
        {
            Assert.IsFalse(EnhancedLampGlowRules.IsEligible(null, 0));
            Assert.IsFalse(EnhancedLampGlowRules.IsEligible("", 0));
        }
    }
}
