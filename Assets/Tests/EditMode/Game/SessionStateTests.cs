using System;
using System.Collections.Generic;
using NUnit.Framework;
using Doom.Game;

namespace Doom.Game.Tests
{
    public class SessionStateTests
    {
        static readonly string[] FullE1 =
        {
            "E1M1", "E1M2", "E1M3", "E1M4", "E1M5", "E1M6", "E1M7", "E1M8", "E1M9"
        };

        [Test]
        public void BeginNewGame_sets_map_and_clears_carry()
        {
            var s = new SessionState();
            s.BeginNewGame("e1m1", FullE1);

            Assert.That(s.IsActive, Is.True);
            Assert.That(s.CurrentMap, Is.EqualTo("E1M1"));
            Assert.That(s.Carry, Is.Null);
            Assert.That(s.EpisodeComplete, Is.False);
        }

        [Test]
        public void Advance_normal_carries_inventory_and_changes_map()
        {
            var s = new SessionState();
            s.BeginNewGame("E1M1", FullE1);

            var health = new HealthModel();
            health.ApplyDamage(40); // 60 HP
            health.GiveArmor(ArmorKind.Green);
            var ammo = new AmmoModel();
            ammo.Add(AmmoType.Shells, 8);
            var loadout = new WeaponLoadout();
            loadout.Give(WeaponId.Shotgun);

            var carry = PlayerCarryState.Capture(health, ammo, loadout);
            var result = s.Advance(ExitKind.Normal, carry);

            Assert.That(result.NextMap, Is.EqualTo("E1M2"));
            Assert.That(s.CurrentMap, Is.EqualTo("E1M2"));
            Assert.That(s.Carry, Is.Not.Null);
            Assert.That(s.Carry.Health, Is.EqualTo(60));
            Assert.That(s.Carry.ArmorType, Is.EqualTo(ArmorKind.Green));
            Assert.That(s.Carry.OwnsShotgun, Is.True);
            Assert.That(s.Carry.Shells, Is.EqualTo(8));
        }

        [Test]
        public void Advance_secret_goes_to_E1M9()
        {
            var s = new SessionState();
            s.BeginNewGame("E1M3", FullE1);
            s.Advance(ExitKind.Secret, PlayerCarryState.FreshStart());
            Assert.That(s.CurrentMap, Is.EqualTo("E1M9"));
        }

        [Test]
        public void Advance_E1M8_normal_completes_episode_and_clears_carry()
        {
            var s = new SessionState();
            s.BeginNewGame("E1M8", FullE1);
            var result = s.Advance(ExitKind.Normal, PlayerCarryState.FreshStart());

            Assert.That(result.Outcome, Is.EqualTo(CampaignOutcome.EpisodeComplete));
            Assert.That(s.EpisodeComplete, Is.True);
            Assert.That(s.Carry, Is.Null);
        }

        [Test]
        public void Carry_omits_keys_and_powers_by_design()
        {
            // Keys/powers live on separate models and are never part of PlayerCarryState.
            var carry = PlayerCarryState.FreshStart();
            var keys = new KeyInventory();
            keys.Give(PlayerKey.RedCard);
            var powers = new PlayerPowers();
            powers.GiveBerserk();
            powers.GiveIronFeet(100);

            // Applying carry must not touch keys/powers — caller resets those explicitly.
            var health = new HealthModel();
            var ammo = new AmmoModel();
            var loadout = new WeaponLoadout();
            carry.ApplyTo(health, ammo, loadout);

            Assert.That(keys.Has(PlayerKey.RedCard), Is.True, "carry apply must not clear keys");
            Assert.That(powers.Berserk, Is.True, "carry apply must not clear powers");
        }

        [Test]
        public void RestartCurrentMap_clears_carry_keeps_map()
        {
            var s = new SessionState();
            s.BeginNewGame("E1M2", FullE1);
            s.Advance(ExitKind.Normal, PlayerCarryState.FreshStart());
            Assert.That(s.CurrentMap, Is.EqualTo("E1M3"));
            Assert.That(s.Carry, Is.Not.Null);

            s.RestartCurrentMap();
            Assert.That(s.CurrentMap, Is.EqualTo("E1M3"));
            Assert.That(s.Carry, Is.Null);
        }

        [Test]
        public void Clear_deactivates_session()
        {
            var s = new SessionState();
            s.BeginNewGame("E1M1", FullE1);
            s.Clear();
            Assert.That(s.IsActive, Is.False);
            Assert.That(s.CurrentMap, Is.Null);
            Assert.Throws<InvalidOperationException>(() => s.RestartCurrentMap());
        }

        [Test]
        public void Capture_Apply_round_trip()
        {
            var health = new HealthModel(55, 50, ArmorKind.Blue);
            var ammo = new AmmoModel();
            ammo.GiveBackpack();
            // GiveBackpack already grants BackpackClipShells (4); top up to a known total.
            while (ammo.Get(AmmoType.Shells) < 20)
                ammo.Add(AmmoType.Shells, 1);
            var loadout = new WeaponLoadout();
            loadout.Give(WeaponId.Chaingun);

            var carry = PlayerCarryState.Capture(health, ammo, loadout);

            var h2 = new HealthModel();
            var a2 = new AmmoModel();
            var l2 = new WeaponLoadout();
            carry.ApplyTo(h2, a2, l2);

            Assert.That(h2.Health, Is.EqualTo(55));
            Assert.That(h2.Armor, Is.EqualTo(50));
            Assert.That(h2.ArmorType, Is.EqualTo(ArmorKind.Blue));
            Assert.That(a2.HasBackpack, Is.True);
            Assert.That(a2.Get(AmmoType.Shells), Is.EqualTo(20));
            Assert.That(l2.Has(WeaponId.Chaingun), Is.True);
            Assert.That(l2.Current, Is.EqualTo(WeaponId.Chaingun));
        }
    }
}
