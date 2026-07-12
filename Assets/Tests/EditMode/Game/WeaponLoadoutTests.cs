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
            // Порядок P_CheckAmmo: BFG → plasma → rocket → chaingun → shotgun → pistol → fist.
            var l = new WeaponLoadout();
            var ammo = new AmmoModel();          // 50 пуль, 0 дроби
            l.Give(WeaponId.Shotgun); l.Give(WeaponId.Chaingun);
            Assert.That(l.BestAvailable(ammo), Is.EqualTo(WeaponId.Chaingun));
            while (ammo.TryConsume(AmmoType.Bullets, 1)) { }
            Assert.That(l.BestAvailable(ammo), Is.EqualTo(WeaponId.Fist),
                "пули кончились, дроби не было — вниз до кулака");
            ammo.Add(AmmoType.Shells, 4);
            Assert.That(l.BestAvailable(ammo), Is.EqualTo(WeaponId.Shotgun));
            l.Give(WeaponId.RocketLauncher);
            ammo.Add(AmmoType.Rockets, 1);
            Assert.That(l.BestAvailable(ammo), Is.EqualTo(WeaponId.RocketLauncher));
            while (ammo.TryConsume(AmmoType.Rockets, 1)) { }
            while (ammo.TryConsume(AmmoType.Shells, 1)) { }
            while (ammo.TryConsume(AmmoType.Bullets, 1)) { }
            l.Give(WeaponId.Chainsaw);
            Assert.That(l.BestAvailable(ammo), Is.EqualTo(WeaponId.Chainsaw));
        }

        [Test]
        public void BestAvailable_requires_full_BFG_cost_and_prefers_plasma()
        {
            var l = new WeaponLoadout();
            var ammo = new AmmoModel();
            while (ammo.TryConsume(AmmoType.Bullets, 1)) { }
            l.Give(WeaponId.PlasmaRifle);
            l.Give(WeaponId.Bfg9000);
            ammo.Add(AmmoType.Cells, 39);
            Assert.That(l.BestAvailable(ammo), Is.EqualTo(WeaponId.PlasmaRifle),
                "BFG needs 40 cells");
            ammo.Add(AmmoType.Cells, 1);
            Assert.That(l.BestAvailable(ammo), Is.EqualTo(WeaponId.Bfg9000));
            while (ammo.TryConsume(AmmoType.Cells, 1)) { }
            Assert.That(l.BestAvailable(ammo), Is.EqualTo(WeaponId.Fist));
        }

        [Test]
        public void Capture_restore_preserves_plasma_and_BFG_ownership()
        {
            var l = new WeaponLoadout();
            l.Give(WeaponId.PlasmaRifle);
            l.Give(WeaponId.Bfg9000);
            l.TrySelect(WeaponId.PlasmaRifle);
            l.TryQueuePending(WeaponId.Bfg9000);

            l.Capture(out bool fist, out bool pistol, out bool shotgun, out bool chaingun,
                out bool rocket, out bool chainsaw, out bool plasma, out bool bfg,
                out var current, out var pending);
            Assert.That(plasma, Is.True);
            Assert.That(bfg, Is.True);
            Assert.That(current, Is.EqualTo(WeaponId.PlasmaRifle));
            Assert.That(pending, Is.EqualTo(WeaponId.Bfg9000));

            var restored = new WeaponLoadout();
            restored.Restore(fist, pistol, shotgun, chaingun, rocket, chainsaw,
                plasma, bfg, current, pending);
            Assert.That(restored.Has(WeaponId.PlasmaRifle), Is.True);
            Assert.That(restored.Has(WeaponId.Bfg9000), Is.True);
            Assert.That(restored.Current, Is.EqualTo(WeaponId.PlasmaRifle));
            Assert.That(restored.Pending, Is.EqualTo(WeaponId.Bfg9000));
        }

        [Test]
        public void Pending_switch_last_valid_request_wins()
        {
            var l = new WeaponLoadout();
            l.Give(WeaponId.Shotgun);

            Assert.That(l.TryQueuePending(WeaponId.Fist), Is.True);
            Assert.That(l.TryQueuePending(WeaponId.Chaingun), Is.False,
                "an unowned request must not replace the queue");
            Assert.That(l.Pending, Is.EqualTo(WeaponId.Fist));
            Assert.That(l.TryQueuePending(WeaponId.Shotgun), Is.True);
            Assert.That(l.Pending, Is.EqualTo(WeaponId.Shotgun));
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
