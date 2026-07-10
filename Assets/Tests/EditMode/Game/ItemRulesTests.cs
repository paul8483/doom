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
