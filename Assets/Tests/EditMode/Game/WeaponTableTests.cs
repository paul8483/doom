using NUnit.Framework;
using Doom.Game;

namespace Doom.Game.Tests
{
    public class WeaponTableTests
    {
        [Test]
        public void All_weapons_have_consistent_defs()
        {
            foreach (WeaponId id in System.Enum.GetValues(typeof(WeaponId)))
            {
                var d = WeaponTable.Get(id);
                Assert.That(d.Slot, Is.InRange(1, 5), $"{id}: слот");
                Assert.That(d.Sprite, Has.Length.EqualTo(4), $"{id}: 4-символьный спрайт");
                Assert.That(d.FireFrames.Length, Is.EqualTo(d.FireTics.Length), $"{id}: кадры/тики");
                foreach (int t in d.FireTics) Assert.That(t, Is.GreaterThan(0));
                Assert.That(d.CycleTics, Is.GreaterThan(0));
                if (d.FlashSprite != null)
                    Assert.That(d.FlashFrames.Length, Is.EqualTo(d.FlashTics.Length));
                Assert.That(d.FireSound, Is.Not.Null.And.Not.Empty, $"{id}: FireSound");
                Assert.That(d.FireSound, Does.StartWith("DS"), $"{id}: FireSound DS*");
            }
        }

        [Test]
        public void Fire_sounds_match_doom_table()
        {
            Assert.That(WeaponTable.Get(WeaponId.Fist).FireSound, Is.EqualTo("DSPUNCH"));
            Assert.That(WeaponTable.Get(WeaponId.Pistol).FireSound, Is.EqualTo("DSPISTOL"));
            Assert.That(WeaponTable.Get(WeaponId.Shotgun).FireSound, Is.EqualTo("DSSHOTGN"));
            Assert.That(WeaponTable.Get(WeaponId.Chaingun).FireSound, Is.EqualTo("DSPISTOL"));
            Assert.That(WeaponTable.Get(WeaponId.RocketLauncher).FireSound, Is.EqualTo("DSRLAUNC"));
        }

        [Test]
        public void Doom_cadence_values()
        {
            // Суммы тиков из state-таблиц p_pspr.c/info.c (linuxdoom-1.10).
            Assert.That(WeaponTable.Get(WeaponId.Fist).CycleTics, Is.EqualTo(22));     // 4+4+5+4+5
            Assert.That(WeaponTable.Get(WeaponId.Pistol).CycleTics, Is.EqualTo(19));   // 4+6+4+5
            Assert.That(WeaponTable.Get(WeaponId.Shotgun).CycleTics, Is.EqualTo(44));  // 3+7+5+5+4+5+5+3+7
            Assert.That(WeaponTable.Get(WeaponId.Chaingun).CycleTics, Is.EqualTo(4));  // 1 выстрел / 4 тика
            Assert.That(WeaponTable.Get(WeaponId.RocketLauncher).CycleTics, Is.EqualTo(20));
            Assert.That(WeaponTable.Get(WeaponId.Shotgun).Pellets, Is.EqualTo(7));
            Assert.That(WeaponTable.Get(WeaponId.Fist).Melee, Is.True);
            Assert.That(WeaponTable.Get(WeaponId.Pistol).FirstShotAccurate, Is.True);
        }
    }
}
