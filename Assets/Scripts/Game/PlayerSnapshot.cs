using System;

namespace Doom.Game
{
    /// Authoritative in-level player state for full-world saves.
    /// Unlike <see cref="PlayerCarryState"/>, this includes position, view,
    /// keys, powers and RNG — everything needed to resume mid-map.
    public sealed class PlayerSnapshot : IEquatable<PlayerSnapshot>
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float YawDegrees { get; }
        public float PitchDegrees { get; }

        public int Health { get; }
        public int Armor { get; }
        public ArmorKind ArmorType { get; }

        public int Bullets { get; }
        public int Shells { get; }
        public int Rockets { get; }
        public bool HasBackpack { get; }

        public bool OwnsFist { get; }
        public bool OwnsPistol { get; }
        public bool OwnsShotgun { get; }
        public bool OwnsChaingun { get; }
        public bool OwnsRocketLauncher { get; }
        public WeaponId CurrentWeapon { get; }
        public bool HasPendingWeapon { get; }
        public WeaponId PendingWeapon { get; }

        /// Bitmask of <see cref="PlayerKey"/> values (1 &lt;&lt; (int)key).
        public int KeyBits { get; }

        public bool Berserk { get; }
        public int IronFeetTics { get; }

        /// <see cref="DoomRandom"/> table index (0..255).
        public int RandomIndex { get; }

        public PlayerSnapshot(
            float x, float y, float z,
            float yawDegrees, float pitchDegrees,
            int health, int armor, ArmorKind armorType,
            int bullets, int shells, bool hasBackpack,
            bool ownsFist, bool ownsPistol, bool ownsShotgun, bool ownsChaingun,
            WeaponId currentWeapon,
            bool hasPendingWeapon, WeaponId pendingWeapon,
            int keyBits,
            bool berserk, int ironFeetTics,
            int randomIndex)
            : this(
                x, y, z, yawDegrees, pitchDegrees,
                health, armor, armorType,
                bullets, shells, 0, hasBackpack,
                ownsFist, ownsPistol, ownsShotgun, ownsChaingun, false,
                currentWeapon, hasPendingWeapon, pendingWeapon,
                keyBits, berserk, ironFeetTics, randomIndex)
        {
        }

        public PlayerSnapshot(
            float x, float y, float z,
            float yawDegrees, float pitchDegrees,
            int health, int armor, ArmorKind armorType,
            int bullets, int shells, int rockets, bool hasBackpack,
            bool ownsFist, bool ownsPistol, bool ownsShotgun, bool ownsChaingun,
            bool ownsRocketLauncher,
            WeaponId currentWeapon,
            bool hasPendingWeapon, WeaponId pendingWeapon,
            int keyBits,
            bool berserk, int ironFeetTics,
            int randomIndex)
        {
            X = x;
            Y = y;
            Z = z;
            YawDegrees = yawDegrees;
            PitchDegrees = pitchDegrees;
            Health = health;
            Armor = armor;
            ArmorType = armorType;
            Bullets = bullets;
            Shells = shells;
            Rockets = rockets;
            HasBackpack = hasBackpack;
            OwnsFist = ownsFist;
            OwnsPistol = ownsPistol;
            OwnsShotgun = ownsShotgun;
            OwnsChaingun = ownsChaingun;
            OwnsRocketLauncher = ownsRocketLauncher;
            CurrentWeapon = currentWeapon;
            HasPendingWeapon = hasPendingWeapon;
            PendingWeapon = pendingWeapon;
            KeyBits = keyBits;
            Berserk = berserk;
            IronFeetTics = ironFeetTics;
            RandomIndex = randomIndex;
        }

        public static bool TryCreate(
            float x, float y, float z,
            float yawDegrees, float pitchDegrees,
            int health, int armor, ArmorKind armorType,
            int bullets, int shells, bool hasBackpack,
            bool ownsFist, bool ownsPistol, bool ownsShotgun, bool ownsChaingun,
            WeaponId currentWeapon,
            bool hasPendingWeapon, WeaponId pendingWeapon,
            int keyBits,
            bool berserk, int ironFeetTics,
            int randomIndex,
            out PlayerSnapshot snapshot,
            out string error)
            => TryCreate(
                x, y, z, yawDegrees, pitchDegrees,
                health, armor, armorType,
                bullets, shells, 0, hasBackpack,
                ownsFist, ownsPistol, ownsShotgun, ownsChaingun, false,
                currentWeapon, hasPendingWeapon, pendingWeapon,
                keyBits, berserk, ironFeetTics, randomIndex,
                out snapshot, out error);

        public static bool TryCreate(
            float x, float y, float z,
            float yawDegrees, float pitchDegrees,
            int health, int armor, ArmorKind armorType,
            int bullets, int shells, int rockets, bool hasBackpack,
            bool ownsFist, bool ownsPistol, bool ownsShotgun, bool ownsChaingun,
            bool ownsRocketLauncher,
            WeaponId currentWeapon,
            bool hasPendingWeapon, WeaponId pendingWeapon,
            int keyBits,
            bool berserk, int ironFeetTics,
            int randomIndex,
            out PlayerSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            error = null;

            if (!IsFinite(x) || !IsFinite(y) || !IsFinite(z)
                || !IsFinite(yawDegrees) || !IsFinite(pitchDegrees))
            {
                error = "Player transform/view must be finite.";
                return false;
            }

            if (health < 0)
            {
                error = "Health must be non-negative.";
                return false;
            }

            if (armor < 0 || armor > HealthModel.MaxArmor)
            {
                error = "Armor out of range.";
                return false;
            }

            if (!Enum.IsDefined(typeof(ArmorKind), armorType))
            {
                error = "Invalid armor type.";
                return false;
            }

            if (armorType == ArmorKind.None && armor != 0)
            {
                error = "Armor amount requires an armor type.";
                return false;
            }

            if (armor == 0 && armorType != ArmorKind.None)
            {
                error = "Armor type requires a positive armor amount.";
                return false;
            }

            if (bullets < 0 || shells < 0 || rockets < 0)
            {
                error = "Ammo counts must be non-negative.";
                return false;
            }

            int maxBullets = hasBackpack ? AmmoModel.MaxBulletsBackpack : AmmoModel.MaxBullets;
            int maxShells = hasBackpack ? AmmoModel.MaxShellsBackpack : AmmoModel.MaxShells;
            int maxRockets = hasBackpack ? AmmoModel.MaxRocketsBackpack : AmmoModel.MaxRockets;
            if (bullets > maxBullets || shells > maxShells || rockets > maxRockets)
            {
                error = "Ammo exceeds max for backpack state.";
                return false;
            }

            if (!IsValidWeapon(currentWeapon))
            {
                error = "Invalid current weapon.";
                return false;
            }

            if (hasPendingWeapon && !IsValidWeapon(pendingWeapon))
            {
                error = "Invalid pending weapon.";
                return false;
            }

            if (keyBits < 0 || keyBits > KeyInventory.AllKeysMask)
            {
                error = "Key bits out of range.";
                return false;
            }

            if (ironFeetTics < 0)
            {
                error = "IronFeet tics must be non-negative.";
                return false;
            }

            if (randomIndex < 0 || randomIndex > 255)
            {
                error = "Random index must be 0..255.";
                return false;
            }

            // Fist is always owned in DOOM; reject snapshots that claim otherwise.
            if (!ownsFist)
            {
                error = "Fist must be owned.";
                return false;
            }

            if (!Owns(
                    currentWeapon, ownsFist, ownsPistol, ownsShotgun, ownsChaingun,
                    ownsRocketLauncher))
            {
                error = "Current weapon is not owned.";
                return false;
            }

            if (hasPendingWeapon
                && !Owns(
                    pendingWeapon, ownsFist, ownsPistol, ownsShotgun, ownsChaingun,
                    ownsRocketLauncher))
            {
                error = "Pending weapon is not owned.";
                return false;
            }

            snapshot = new PlayerSnapshot(
                x, y, z, yawDegrees, pitchDegrees,
                health, armor, armorType,
                bullets, shells, rockets, hasBackpack,
                ownsFist, ownsPistol, ownsShotgun, ownsChaingun, ownsRocketLauncher,
                currentWeapon, hasPendingWeapon, pendingWeapon,
                keyBits, berserk, ironFeetTics, randomIndex);
            return true;
        }

        public static PlayerSnapshot Capture(
            float x, float y, float z,
            float yawDegrees, float pitchDegrees,
            HealthModel health,
            AmmoModel ammo,
            WeaponLoadout loadout,
            KeyInventory keys,
            PlayerPowers powers,
            DoomRandom random)
        {
            if (health == null) throw new ArgumentNullException(nameof(health));
            if (ammo == null) throw new ArgumentNullException(nameof(ammo));
            if (loadout == null) throw new ArgumentNullException(nameof(loadout));
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (powers == null) throw new ArgumentNullException(nameof(powers));
            if (random == null) throw new ArgumentNullException(nameof(random));

            if (!TryCreate(
                    x, y, z, yawDegrees, pitchDegrees,
                    health.Health, health.Armor, health.ArmorType,
                    ammo.Get(AmmoType.Bullets), ammo.Get(AmmoType.Shells),
                    ammo.Get(AmmoType.Rockets), ammo.HasBackpack,
                    loadout.Has(WeaponId.Fist), loadout.Has(WeaponId.Pistol),
                    loadout.Has(WeaponId.Shotgun), loadout.Has(WeaponId.Chaingun),
                    loadout.Has(WeaponId.RocketLauncher),
                    loadout.Current,
                    loadout.HasPending, loadout.HasPending ? loadout.Pending : WeaponId.Fist,
                    keys.CaptureBits(),
                    powers.Berserk, powers.IronFeetTics,
                    random.Index,
                    out var snapshot, out string error))
                throw new InvalidOperationException("Capture produced invalid snapshot: " + error);

            return snapshot;
        }

        public void ApplyTo(
            HealthModel health,
            AmmoModel ammo,
            WeaponLoadout loadout,
            KeyInventory keys,
            PlayerPowers powers,
            DoomRandom random)
        {
            if (health == null) throw new ArgumentNullException(nameof(health));
            if (ammo == null) throw new ArgumentNullException(nameof(ammo));
            if (loadout == null) throw new ArgumentNullException(nameof(loadout));
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (powers == null) throw new ArgumentNullException(nameof(powers));
            if (random == null) throw new ArgumentNullException(nameof(random));

            health.Restore(Health, Armor, ArmorType);
            ammo.Restore(Bullets, Shells, Rockets, HasBackpack);
            loadout.Restore(
                OwnsFist, OwnsPistol, OwnsShotgun, OwnsChaingun, OwnsRocketLauncher,
                CurrentWeapon,
                HasPendingWeapon ? (WeaponId?)PendingWeapon : null);
            keys.RestoreBits(KeyBits);
            powers.Restore(Berserk, IronFeetTics);
            random.Restore(RandomIndex);
        }

        static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        static bool IsValidWeapon(WeaponId id) =>
            id == WeaponId.Fist || id == WeaponId.Pistol
            || id == WeaponId.Shotgun || id == WeaponId.Chaingun
            || id == WeaponId.RocketLauncher;

        static bool Owns(
            WeaponId id, bool fist, bool pistol, bool shotgun, bool chaingun,
            bool rocketLauncher) =>
            id switch
            {
                WeaponId.Fist => fist,
                WeaponId.Pistol => pistol,
                WeaponId.Shotgun => shotgun,
                WeaponId.Chaingun => chaingun,
                WeaponId.RocketLauncher => rocketLauncher,
                _ => false,
            };

        public bool Equals(PlayerSnapshot other)
        {
            if (other is null) return false;
            return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z)
                   && YawDegrees.Equals(other.YawDegrees)
                   && PitchDegrees.Equals(other.PitchDegrees)
                   && Health == other.Health && Armor == other.Armor
                   && ArmorType == other.ArmorType
                   && Bullets == other.Bullets && Shells == other.Shells
                   && Rockets == other.Rockets
                   && HasBackpack == other.HasBackpack
                   && OwnsFist == other.OwnsFist && OwnsPistol == other.OwnsPistol
                   && OwnsShotgun == other.OwnsShotgun && OwnsChaingun == other.OwnsChaingun
                   && OwnsRocketLauncher == other.OwnsRocketLauncher
                   && CurrentWeapon == other.CurrentWeapon
                   && HasPendingWeapon == other.HasPendingWeapon
                   && (!HasPendingWeapon || PendingWeapon == other.PendingWeapon)
                   && KeyBits == other.KeyBits
                   && Berserk == other.Berserk && IronFeetTics == other.IronFeetTics
                   && RandomIndex == other.RandomIndex;
        }

        public override bool Equals(object obj) => Equals(obj as PlayerSnapshot);

        public override int GetHashCode() =>
            HashCode.Combine(
                HashCode.Combine(X, Y, Z, YawDegrees, PitchDegrees, Health, Armor, (int)ArmorType),
                HashCode.Combine(Bullets, Shells, Rockets, HasBackpack, OwnsPistol,
                    OwnsShotgun, OwnsChaingun, OwnsRocketLauncher),
                HashCode.Combine(HasPendingWeapon, (int)PendingWeapon, Berserk, IronFeetTics,
                    RandomIndex, (int)CurrentWeapon, KeyBits));
    }
}
