using System;
using NUnit.Framework;
using Doom.Game;

namespace Doom.Game.Tests
{
    public class SnapshotModelTests
    {
        [Test]
        public void HealthModel_Capture_Restore_round_trip()
        {
            var h = new HealthModel();
            h.ApplyDamage(40);
            h.GiveArmor(ArmorKind.Blue);
            h.Capture(out int health, out int armor, out var kind);

            h.Reset();
            Assert.That(h.Health, Is.EqualTo(100));
            Assert.That(h.ArmorType, Is.EqualTo(ArmorKind.None));

            h.Restore(health, armor, kind);
            Assert.That(h.Health, Is.EqualTo(health));
            Assert.That(h.Armor, Is.EqualTo(armor));
            Assert.That(h.ArmorType, Is.EqualTo(ArmorKind.Blue));
        }

        [Test]
        public void AmmoModel_Capture_Restore_preserves_backpack_and_counts()
        {
            var a = new AmmoModel();
            a.GiveBackpack();
            a.Add(AmmoType.Shells, 20);
            a.Capture(out int bullets, out int shells, out bool backpack);

            a.Reset();
            a.Restore(bullets, shells, backpack);

            Assert.That(a.HasBackpack, Is.True);
            Assert.That(a.Get(AmmoType.Bullets), Is.EqualTo(bullets));
            Assert.That(a.Get(AmmoType.Shells), Is.EqualTo(shells));
            Assert.That(a.GetMax(AmmoType.Bullets), Is.EqualTo(AmmoModel.MaxBulletsBackpack));
        }

        [Test]
        public void WeaponLoadout_Capture_Restore_includes_pending()
        {
            var l = new WeaponLoadout();
            l.Give(WeaponId.Shotgun);
            l.Give(WeaponId.Chaingun);
            l.TrySelect(WeaponId.Shotgun);
            Assert.That(l.TryQueuePending(WeaponId.Chaingun), Is.True);

            l.Capture(out bool fist, out bool pistol, out bool shotgun, out bool chaingun,
                out WeaponId current, out WeaponId? pending);

            l.Reset();
            Assert.That(l.HasPending, Is.False);

            l.Restore(fist, pistol, shotgun, chaingun, current, pending);
            Assert.That(l.Has(WeaponId.Shotgun), Is.True);
            Assert.That(l.Has(WeaponId.Chaingun), Is.True);
            Assert.That(l.Current, Is.EqualTo(WeaponId.Shotgun));
            Assert.That(l.HasPending, Is.True);
            Assert.That(l.Pending, Is.EqualTo(WeaponId.Chaingun));
        }

        [Test]
        public void WeaponLoadout_Restore_drops_pending_if_not_owned()
        {
            var l = new WeaponLoadout();
            l.Restore(true, true, false, false, WeaponId.Pistol, WeaponId.Chaingun);
            Assert.That(l.HasPending, Is.False);
            Assert.That(l.Current, Is.EqualTo(WeaponId.Pistol));
        }

        [Test]
        public void KeyInventory_Capture_Restore_round_trip()
        {
            var k = new KeyInventory();
            k.Give(PlayerKey.RedCard);
            k.Give(PlayerKey.BlueSkull);
            int bits = k.CaptureBits();

            k.Reset();
            Assert.That(k.HasAny(), Is.False);

            k.RestoreBits(bits);
            Assert.That(k.Has(PlayerKey.RedCard), Is.True);
            Assert.That(k.Has(PlayerKey.BlueSkull), Is.True);
            Assert.That(k.Has(PlayerKey.YellowCard), Is.False);
        }

        [Test]
        public void KeyInventory_RestoreBits_masks_invalid_high_bits()
        {
            var k = new KeyInventory();
            k.RestoreBits(KeyInventory.AllKeysMask | (1 << 10));
            Assert.That(k.CaptureBits(), Is.EqualTo(KeyInventory.AllKeysMask));
        }

        [Test]
        public void PlayerPowers_Capture_Restore_round_trip()
        {
            var p = new PlayerPowers();
            p.GiveBerserk();
            p.GiveIronFeet(2100);
            p.Capture(out bool berserk, out int tics);

            p.Reset();
            p.Restore(berserk, tics);

            Assert.That(p.Berserk, Is.True);
            Assert.That(p.IronFeetTics, Is.EqualTo(2100));
        }

        [Test]
        public void DoomRandom_Restore_resumes_sequence()
        {
            var r = new DoomRandom();
            for (int i = 0; i < 17; i++) r.Next();
            int index = r.Index;
            int nextExpected = new DoomRandom(index).Next();

            var other = new DoomRandom();
            other.Restore(index);
            Assert.That(other.Index, Is.EqualTo(index));
            Assert.That(other.Next(), Is.EqualTo(nextExpected));
        }

        [Test]
        public void PlayerSnapshot_Capture_ApplyTo_round_trip()
        {
            var health = new HealthModel();
            health.ApplyDamage(25);
            health.GiveArmor(ArmorKind.Green);
            var ammo = new AmmoModel();
            ammo.GiveBackpack();
            ammo.Add(AmmoType.Shells, 12);
            var loadout = new WeaponLoadout();
            loadout.Give(WeaponId.Shotgun);
            loadout.TryQueuePending(WeaponId.Pistol);
            var keys = new KeyInventory();
            keys.Give(PlayerKey.YellowCard);
            var powers = new PlayerPowers();
            powers.GiveBerserk();
            powers.GiveIronFeet(100);
            var rng = new DoomRandom();
            rng.Next();
            rng.Next();

            var snap = PlayerSnapshot.Capture(
                1.5f, 2.25f, 0.75f, 90f, -12f,
                health, ammo, loadout, keys, powers, rng);

            health.Reset();
            ammo.Reset();
            loadout.Reset();
            keys.Reset();
            powers.Reset();
            rng.Restore(0);

            snap.ApplyTo(health, ammo, loadout, keys, powers, rng);

            Assert.That(health.Health, Is.EqualTo(75));
            Assert.That(health.ArmorType, Is.EqualTo(ArmorKind.Green));
            Assert.That(ammo.HasBackpack, Is.True);
            Assert.That(ammo.Get(AmmoType.Shells), Is.EqualTo(snap.Shells));
            Assert.That(snap.Shells, Is.EqualTo(12 + AmmoModel.BackpackClipShells));
            Assert.That(loadout.Has(WeaponId.Shotgun), Is.True);
            Assert.That(loadout.Current, Is.EqualTo(WeaponId.Shotgun));
            Assert.That(loadout.HasPending, Is.True);
            Assert.That(loadout.Pending, Is.EqualTo(WeaponId.Pistol));
            Assert.That(keys.Has(PlayerKey.YellowCard), Is.True);
            Assert.That(powers.Berserk, Is.True);
            Assert.That(powers.IronFeetTics, Is.EqualTo(100));
            Assert.That(rng.Index, Is.EqualTo(snap.RandomIndex));
            Assert.That(snap.X, Is.EqualTo(1.5f));
            Assert.That(snap.YawDegrees, Is.EqualTo(90f));
        }

        [Test]
        public void PlayerSnapshot_TryCreate_rejects_invalid_enums_and_counts()
        {
            Assert.That(PlayerSnapshot.TryCreate(
                0, 0, 0, 0, 0,
                health: -1, armor: 0, armorType: ArmorKind.None,
                bullets: 0, shells: 0, hasBackpack: false,
                ownsFist: true, ownsPistol: true, ownsShotgun: false, ownsChaingun: false,
                currentWeapon: WeaponId.Pistol,
                hasPendingWeapon: false, pendingWeapon: WeaponId.Fist,
                keyBits: 0, berserk: false, ironFeetTics: 0, randomIndex: 0,
                out _, out string err), Is.False);
            Assert.That(err, Does.Contain("Health"));

            Assert.That(PlayerSnapshot.TryCreate(
                0, 0, 0, 0, 0,
                100, 0, ArmorKind.None,
                50, 0, false,
                ownsFist: false, ownsPistol: true, ownsShotgun: false, ownsChaingun: false,
                WeaponId.Pistol, false, WeaponId.Fist,
                0, false, 0, 0,
                out _, out err), Is.False);
            Assert.That(err, Does.Contain("Fist"));

            Assert.That(PlayerSnapshot.TryCreate(
                0, 0, 0, 0, 0,
                100, 0, ArmorKind.None,
                50, 0, false,
                true, true, false, false,
                WeaponId.Shotgun, false, WeaponId.Fist,
                0, false, 0, 0,
                out _, out err), Is.False);
            Assert.That(err, Does.Contain("Current weapon"));

            Assert.That(PlayerSnapshot.TryCreate(
                0, 0, 0, float.NaN, 0,
                100, 0, ArmorKind.None,
                50, 0, false,
                true, true, false, false,
                WeaponId.Pistol, false, WeaponId.Fist,
                0, false, 0, 0,
                out _, out err), Is.False);
            Assert.That(err, Does.Contain("finite"));

            Assert.That(PlayerSnapshot.TryCreate(
                0, 0, 0, 0, 0,
                100, 0, ArmorKind.None,
                50, 0, false,
                true, true, false, false,
                WeaponId.Pistol, false, WeaponId.Fist,
                keyBits: 1 << 8, berserk: false, ironFeetTics: 0, randomIndex: 0,
                out _, out err), Is.False);
            Assert.That(err, Does.Contain("Key bits"));

            Assert.That(PlayerSnapshot.TryCreate(
                0, 0, 0, 0, 0,
                100, 0, ArmorKind.None,
                bullets: 500, shells: 0, hasBackpack: false,
                true, true, false, false,
                WeaponId.Pistol, false, WeaponId.Fist,
                0, false, 0, 0,
                out _, out err), Is.False);
            Assert.That(err, Does.Contain("Ammo"));
        }

        [Test]
        public void EntityId_rejects_negative_and_round_trips_kinds()
        {
            Assert.That(SaveEntityId.None.IsNone, Is.True);
            var map = SaveEntityId.MapThing(12);
            Assert.That(map.Kind, Is.EqualTo(EntityKind.MapThing));
            Assert.That(map.Index, Is.EqualTo(12));

            var spawned = SaveEntityId.Spawned(3);
            Assert.That(spawned.Kind, Is.EqualTo(EntityKind.Spawned));
            Assert.That(spawned, Is.Not.EqualTo(map));

            Assert.Throws<ArgumentOutOfRangeException>(() => SaveEntityId.MapThing(-1));
            Assert.That(SaveEntityId.TryCreate(EntityKind.MapThing, -1, out _), Is.False);
            Assert.That(SaveEntityId.TryCreate((EntityKind)99, 0, out _), Is.False);
        }

        [Test]
        public void WorldSnapshot_TryCreate_requires_sorted_unique_ids()
        {
            var stats = new LevelStatsSnapshot(1, 10, 0, 5, 0, 2, 35);
            var sectors = new[]
            {
                new SectorSnapshot(0, 0f, 128f, 160, false, MoverPlane.Floor, MoverPhase.None,
                    0, 0f, 0f, 0),
                new SectorSnapshot(2, 16f, 128f, 160, true, MoverPlane.Ceiling, MoverPhase.Waiting,
                    -1, 64f, 4f, 12),
            };
            var lines = new[] { new LineSnapshot(0, true, true) };
            var things = new[]
            {
                new ThingSnapshot(0, true, 1f, 2f, 0f, 90f, 30, 1, 0, SaveEntityId.None),
                new ThingSnapshot(5, false, 0f, 0f, 0f, 0f, 0, 0, 0, SaveEntityId.None),
            };
            var projectiles = new[]
            {
                new ProjectileSnapshot(1, 1, SaveEntityId.MapThing(0),
                    0f, 0f, 1f, 1f, 0f, 0f, 0.5f),
            };

            Assert.That(WorldSnapshot.TryCreate(
                100, 2, stats,
                killIds: new[] { 3 },
                itemIds: Array.Empty<int>(),
                secretIds: Array.Empty<int>(),
                sectors, lines, things, projectiles,
                Array.Empty<SpawnedPickupSnapshot>(),
                out var world, out _), Is.True);
            Assert.That(world.Sectors.Length, Is.EqualTo(2));
            Assert.That(world.Sectors[1].MoverWaitTics, Is.EqualTo(12));
            Assert.That(world.KillIds, Is.EqualTo(new[] { 3 }));

            // Mutating the source array must not affect the snapshot (defensive copy).
            sectors[0] = new SectorSnapshot(0, 99f, 99f, 0, false, MoverPlane.Floor,
                MoverPhase.None, 0, 0f, 0f, 0);
            Assert.That(world.Sectors[0].FloorHeight, Is.EqualTo(0f));

            var unsorted = new[]
            {
                new ThingSnapshot(5, true, 0, 0, 0, 0, 1, 0, 0, SaveEntityId.None),
                new ThingSnapshot(1, true, 0, 0, 0, 0, 1, 0, 0, SaveEntityId.None),
            };
            Assert.That(WorldSnapshot.TryCreate(
                0, 0, stats,
                Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(),
                sectors, lines, unsorted, Array.Empty<ProjectileSnapshot>(),
                Array.Empty<SpawnedPickupSnapshot>(),
                out _, out string err), Is.False);
            Assert.That(err, Does.Contain("sorted"));
        }

        [Test]
        public void SaveGame_TryCreate_normalizes_map_and_requires_identity()
        {
            Assert.That(PlayerSnapshot.TryCreate(
                0, 0, 0, 0, 0,
                100, 0, ArmorKind.None,
                AmmoModel.StartBullets, 0, false,
                true, true, false, false,
                WeaponId.Pistol, false, WeaponId.Fist,
                0, false, 0, 0,
                out var player, out _), Is.True);

            Assert.That(WorldSnapshot.TryCreate(
                0, 0, default,
                Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(),
                Array.Empty<SectorSnapshot>(),
                Array.Empty<LineSnapshot>(),
                Array.Empty<ThingSnapshot>(),
                Array.Empty<ProjectileSnapshot>(),
                Array.Empty<SpawnedPickupSnapshot>(),
                out var world, out _), Is.True);

            Assert.That(SaveGame.TryCreate(
                "e1m1", "wad:test", player, world, out var save, out _), Is.True);
            Assert.That(save.MapName, Is.EqualTo("E1M1"));
            Assert.That(save.Version, Is.EqualTo(SaveGame.SchemaVersion));
            Assert.That(SaveGame.Magic, Is.EqualTo(0x56415344u));

            Assert.That(SaveGame.TryCreate(
                "E1M1", "", player, world, out _, out string err), Is.False);
            Assert.That(err, Does.Contain("WAD identity"));
        }
    }
}
