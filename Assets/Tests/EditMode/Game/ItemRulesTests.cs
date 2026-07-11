using NUnit.Framework;
using Doom.Game;

namespace Doom.Game.Tests
{
    public class ItemRulesTests
    {
        static PickupContext Fresh() => new PickupContext
        {
            Health = new HealthModel(),
            Ammo = new AmmoModel(),
            Loadout = new WeaponLoadout(),
            Keys = new KeyInventory(),
            Powers = new PlayerPowers(),
        };

        [Test]
        public void Stim_rejected_at_full_health()
        {
            var ctx = Fresh();
            Assert.That(ItemRules.TryPickup(2011, ctx), Is.False);
            Assert.That(ctx.Health.Health, Is.EqualTo(100));
        }

        [Test]
        public void Stim_heals_when_damaged()
        {
            var ctx = Fresh();
            ctx.Health.ApplyDamage(20);
            Assert.That(ItemRules.TryPickup(2011, ctx), Is.True);
            Assert.That(ctx.Health.Health, Is.EqualTo(90));
        }

        [Test]
        public void Green_armor_and_reject_when_already_100()
        {
            var ctx = Fresh();
            Assert.That(ItemRules.TryPickup(2018, ctx), Is.True);
            Assert.That(ctx.Health.Armor, Is.EqualTo(100));
            Assert.That(ItemRules.TryPickup(2018, ctx), Is.False);
        }

        [Test]
        public void Key_always_accepted_even_if_owned()
        {
            var ctx = Fresh();
            Assert.That(ItemRules.TryPickup(5, ctx), Is.True);
            Assert.That(ctx.Keys.Has(PlayerKey.BlueCard), Is.True);
            Assert.That(ItemRules.TryPickup(5, ctx), Is.True);
        }

        [Test]
        public void Backpack_always_accepted()
        {
            var ctx = Fresh();
            Assert.That(ItemRules.TryPickup(8, ctx), Is.True);
            Assert.That(ctx.Ammo.HasBackpack, Is.True);
            Assert.That(ItemRules.TryPickup(8, ctx), Is.True);
        }

        [Test]
        public void Berserk_heals_to_100_and_prefers_fist()
        {
            var ctx = Fresh();
            ctx.Health.ApplyDamage(40);
            Assert.That(ItemRules.TryPickup(2023, ctx), Is.True);
            Assert.That(ctx.Health.Health, Is.EqualTo(100));
            Assert.That(ctx.Powers.Berserk, Is.True);
            Assert.That(ctx.PreferFist, Is.True);
        }

        [Test]
        public void Suit_sets_ironfeet_duration()
        {
            var ctx = Fresh();
            Assert.That(ItemRules.TryPickup(2025, ctx), Is.True);
            Assert.That(ctx.Powers.IronFeetTics, Is.EqualTo(ItemRules.IronFeetDurationTics));
        }

        [Test]
        public void Shotgun_pickup_still_works()
        {
            var ctx = Fresh();
            Assert.That(ItemRules.TryPickup(2001, ctx), Is.True);
            Assert.That(ctx.Loadout.Has(WeaponId.Shotgun), Is.True);
            Assert.That(ctx.Ammo.Get(AmmoType.Shells), Is.EqualTo(8));
        }

        [Test]
        public void Rocket_launcher_and_ammo_pickups_work()
        {
            var ctx = Fresh();
            Assert.That(ItemRules.TryPickup(2003, ctx), Is.True);
            Assert.That(ctx.Loadout.Has(WeaponId.RocketLauncher), Is.True);
            Assert.That(ctx.Ammo.Get(AmmoType.Rockets), Is.EqualTo(2));

            Assert.That(ItemRules.TryPickup(2010, ctx), Is.True);
            Assert.That(ItemRules.TryPickup(2046, ctx), Is.True);
            Assert.That(ctx.Ammo.Get(AmmoType.Rockets), Is.EqualTo(8));
            Assert.That(ItemRules.IsPickup(2003), Is.True);
            Assert.That(ItemRules.IsPickup(2010), Is.True);
            Assert.That(ItemRules.IsPickup(2046), Is.True);
        }

        [Test]
        public void Chainsaw_pickup_gives_weapon_without_ammo()
        {
            var ctx = Fresh();
            Assert.That(ItemRules.TryPickup(2005, ctx), Is.True);
            Assert.That(ctx.Loadout.Has(WeaponId.Chainsaw), Is.True);
            Assert.That(ctx.Loadout.Current, Is.EqualTo(WeaponId.Chainsaw));
            Assert.That(ItemRules.TryPickup(2005, ctx), Is.False, "already owned");
            Assert.That(ItemRules.IsPickup(2005), Is.True);
        }

        [Test]
        public void Plasma_BFG_and_cell_pickups_work()
        {
            var ctx = Fresh();
            Assert.That(ItemRules.TryPickup(2004, ctx), Is.True);
            Assert.That(ctx.Loadout.Has(WeaponId.PlasmaRifle), Is.True);
            Assert.That(ctx.Ammo.Get(AmmoType.Cells), Is.EqualTo(40));

            Assert.That(ItemRules.TryPickup(2047, ctx), Is.True);
            Assert.That(ctx.Ammo.Get(AmmoType.Cells), Is.EqualTo(60));
            Assert.That(ItemRules.TryPickup(17, ctx), Is.True);
            Assert.That(ctx.Ammo.Get(AmmoType.Cells), Is.EqualTo(160));

            Assert.That(ItemRules.TryPickup(2006, ctx), Is.True);
            Assert.That(ctx.Loadout.Has(WeaponId.Bfg9000), Is.True);
            Assert.That(ctx.Ammo.Get(AmmoType.Cells), Is.EqualTo(200));

            // Full ammo + owned weapon → rejected; GO stays.
            while (ctx.Ammo.Add(AmmoType.Cells, 20)) { }
            Assert.That(ItemRules.TryPickup(2004, ctx), Is.False);
            Assert.That(ItemRules.TryPickup(2047, ctx), Is.False);

            Assert.That(ItemRules.IsPickup(2004), Is.True);
            Assert.That(ItemRules.IsPickup(2006), Is.True);
            Assert.That(ItemRules.IsPickup(2047), Is.True);
            Assert.That(ItemRules.IsPickup(17), Is.True);
        }

        [Test]
        public void New_energy_weapon_accepted_at_full_ammo()
        {
            var ctx = Fresh();
            while (ctx.Ammo.Add(AmmoType.Cells, 20)) { }
            Assert.That(ItemRules.TryPickup(2004, ctx), Is.True);
            Assert.That(ctx.Loadout.Has(WeaponId.PlasmaRifle), Is.True);
            Assert.That(ctx.Loadout.Current, Is.EqualTo(WeaponId.PlasmaRifle));
        }
    }

    public class DeathDropTableTests
    {
        [Test]
        public void Poss_and_spos_drop_clip_and_shotgun()
        {
            Assert.That(DeathDropTable.TryGet(3004, out int clip), Is.True);
            Assert.That(clip, Is.EqualTo(2007));
            Assert.That(DeathDropTable.TryGet(9, out int shot), Is.True);
            Assert.That(shot, Is.EqualTo(2001));
            Assert.That(DeathDropTable.TryGet(3001, out _), Is.False); // TROO
        }
    }
}
