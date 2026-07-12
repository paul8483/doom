using NUnit.Framework;
using Doom.Game;

namespace Doom.Game.Tests
{
    public class MonsterRulesTests
    {
        [Test]
        public void Bullet_damage_is_3_to_15_step_3()
        {
            var r = new DoomRandom();
            for (int i = 0; i < 300; i++)
            {
                int d = MonsterRules.RollDamage(r, 5, 3); // зомби: ((r%5)+1)*3
                Assert.That(d, Is.InRange(3, 15));
                Assert.That(d % 3, Is.EqualTo(0));
            }
        }

        [Test]
        public void Monster_spread_unit_is_4096th_of_circle()
        {
            // (P_Random()-P_Random())<<20 в BAM: 1 ед. = 360/4096 ≈ 0.088°, max ±22.4°.
            var r = new DoomRandom();
            bool any = false;
            for (int i = 0; i < 100; i++)
            {
                float deg = MonsterRules.SpreadOffsetDeg(r);
                Assert.That(deg, Is.InRange(-22.5f, 22.5f));
                if (deg != 0f) any = true;
            }
            Assert.That(any, Is.True);
        }

        [Test]
        public void Melee_range_uses_doom_formula()
        {
            // P_CheckMeleeRange: dist < MELEERANGE - 20 + targetRadius (64-20+16=60 к игроку).
            Assert.That(MonsterRules.InMeleeRange(59.9f, 16f), Is.True);
            Assert.That(MonsterRules.InMeleeRange(60.1f, 16f), Is.False);
        }

        [Test]
        public void Missile_range_check_follows_p_checkmissilerange()
        {
            // dist=264, есть melee: p = 264-64=200 → атака только если P_Random() >= 200.
            var r = new DoomRandom(seed: 0); // первые значения 8,109,220,...
            Assert.That(MonsterRules.CheckMissileRange(r, dist: 264f, hasMelee: true), Is.False);  // 8 < 200
            r = new DoomRandom(seed: 2);     // следующее значение 220
            Assert.That(MonsterRules.CheckMissileRange(r, dist: 264f, hasMelee: true), Is.True);   // 220 >= 200
            // Без melee-атаки дистанция штрафуется ещё на 128.
            r = new DoomRandom(seed: 0);
            Assert.That(MonsterRules.CheckMissileRange(r, dist: 100f, hasMelee: false), Is.True,
                "100-64-128 < 0 → порог 0, любой бросок проходит");
            // В упор с melee порог мал.
            r = new DoomRandom(seed: 0);
            Assert.That(MonsterRules.CheckMissileRange(r, dist: 70f, hasMelee: true), Is.True,
                "70-64=6, бросок 8 >= 6");
        }

        [Test]
        public void Extreme_death_requires_strict_overkill_and_sequence()
        {
            Assert.That(MonsterRules.ShouldUseExtremeDeath(-20, 20, true), Is.False,
                "exactly -spawnHealth is a normal death");
            Assert.That(MonsterRules.ShouldUseExtremeDeath(-21, 20, true), Is.True);
            Assert.That(MonsterRules.ShouldUseExtremeDeath(-21, 20, false), Is.False);
        }
    }
}
