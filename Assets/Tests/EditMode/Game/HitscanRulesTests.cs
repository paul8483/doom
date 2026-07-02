using System.Collections.Generic;
using NUnit.Framework;
using Doom.Game;

namespace Doom.Game.Tests
{
    public class HitscanRulesTests
    {
        [Test]
        public void GunShot_damage_is_5_10_or_15()
        {
            var r = new DoomRandom();
            var seen = new HashSet<int>();
            for (int i = 0; i < 300; i++) seen.Add(HitscanRules.GunShotDamage(r));
            Assert.That(seen, Is.EquivalentTo(new[] { 5, 10, 15 }));
        }

        [Test]
        public void Punch_damage_is_even_2_to_20()
        {
            var r = new DoomRandom();
            for (int i = 0; i < 300; i++)
            {
                int d = HitscanRules.PunchDamage(r);
                Assert.That(d, Is.InRange(2, 20));
                Assert.That(d % 2, Is.EqualTo(0));
            }
        }

        [Test]
        public void Shotgun_volley_is_7_pellets_within_spread()
        {
            var r = new DoomRandom();
            var shots = new List<HitscanShot>();
            HitscanRules.FireVolley(WeaponTable.Get(WeaponId.Shotgun), refire: false, r, shots);
            Assert.That(shots.Count, Is.EqualTo(7));
            foreach (var s in shots)
            {
                Assert.That(s.YawOffsetDeg, Is.InRange(-5.61f, 5.61f));
                Assert.That(new[] { 5, 10, 15 }, Contains.Item(s.Damage));
            }
        }

        [Test]
        public void Pistol_first_shot_accurate_refire_spreads()
        {
            var r = new DoomRandom();
            var shots = new List<HitscanShot>();
            HitscanRules.FireVolley(WeaponTable.Get(WeaponId.Pistol), refire: false, r, shots);
            Assert.That(shots.Count, Is.EqualTo(1));
            Assert.That(shots[0].YawOffsetDeg, Is.EqualTo(0f), "первый выстрел точный");

            bool anySpread = false;
            for (int i = 0; i < 50; i++)
            {
                shots.Clear();
                HitscanRules.FireVolley(WeaponTable.Get(WeaponId.Pistol), refire: true, r, shots);
                if (shots[0].YawOffsetDeg != 0f) anySpread = true;
            }
            Assert.That(anySpread, Is.True, "очередь — с разбросом");
        }
    }
}
