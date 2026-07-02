using NUnit.Framework;
using Doom.Game;

namespace Doom.Game.Tests
{
    public class WeaponLoadoutTests
    {
        [Test]
        public void Starts_with_fist_and_pistol_current_pistol()
        {
            var l = new WeaponLoadout();
            Assert.That(l.Has(WeaponId.Fist), Is.True);
            Assert.That(l.Has(WeaponId.Pistol), Is.True);
            Assert.That(l.Has(WeaponId.Shotgun), Is.False);
            Assert.That(l.Current, Is.EqualTo(WeaponId.Pistol));
        }

        [Test]
        public void Give_new_weapon_returns_true_and_autoselects()
        {
            var l = new WeaponLoadout();
            Assert.That(l.Give(WeaponId.Shotgun), Is.True);
            Assert.That(l.Current, Is.EqualTo(WeaponId.Shotgun));
            Assert.That(l.Give(WeaponId.Shotgun), Is.False, "повторная выдача — false");
        }

        [Test]
        public void TrySelect_only_owned()
        {
            var l = new WeaponLoadout();
            Assert.That(l.TrySelect(WeaponId.Chaingun), Is.False);
            Assert.That(l.TrySelect(WeaponId.Fist), Is.True);
            Assert.That(l.Current, Is.EqualTo(WeaponId.Fist));
        }

        [Test]
        public void BestAvailable_follows_p_checkammo_order()
        {
            // Порядок P_CheckAmmo: chaingun → shotgun → pistol → fist.
            var l = new WeaponLoadout();
            var ammo = new AmmoModel();          // 50 пуль, 0 дроби
            l.Give(WeaponId.Shotgun); l.Give(WeaponId.Chaingun);
            Assert.That(l.BestAvailable(ammo), Is.EqualTo(WeaponId.Chaingun));
            while (ammo.TryConsume(AmmoType.Bullets, 1)) { }
            Assert.That(l.BestAvailable(ammo), Is.EqualTo(WeaponId.Fist),
                "пули кончились, дроби не было — вниз до кулака");
            ammo.Add(AmmoType.Shells, 4);
            Assert.That(l.BestAvailable(ammo), Is.EqualTo(WeaponId.Shotgun));
        }

        [Test]
        public void Reset_restores_start()
        {
            var l = new WeaponLoadout();
            l.Give(WeaponId.Chaingun);
            l.Reset();
            Assert.That(l.Has(WeaponId.Chaingun), Is.False);
            Assert.That(l.Current, Is.EqualTo(WeaponId.Pistol));
        }
    }
}
