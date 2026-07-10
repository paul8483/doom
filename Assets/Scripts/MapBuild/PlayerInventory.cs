using UnityEngine;
using Doom.Game;

namespace Doom.MapBuild
{
    /// Thin facade over health/ammo/keys/powers for pickups and door key checks.
    public sealed class PlayerInventory : MonoBehaviour
    {
        public KeyInventory Keys { get; } = new KeyInventory();
        public PlayerPowers Powers { get; } = new PlayerPowers();

        PlayerHealth health;
        PlayerWeapons weapons;
        float tickAccum;
        const float TicSeconds = 1f / 35f;

        public void Init(PlayerHealth health, PlayerWeapons weapons)
        {
            this.health = health;
            this.weapons = weapons;
        }

        public bool TryPickup(int doomedNum)
        {
            if (health == null || weapons == null) return false;
            var ctx = new PickupContext
            {
                Health = health.Model,
                Ammo = weapons.Ammo,
                Loadout = weapons.Loadout,
                Keys = Keys,
                Powers = Powers,
            };
            bool ok = ItemRules.TryPickup(doomedNum, ctx);
            if (ok && ctx.PreferFist)
                weapons.Loadout.TrySelect(WeaponId.Fist);
            return ok;
        }

        void Update()
        {
            tickAccum += Time.deltaTime;
            while (tickAccum >= TicSeconds)
            {
                tickAccum -= TicSeconds;
                Powers.Advance(1);
            }
        }

        /// Respawn: clear powers; keys intentionally kept.
        public void OnRespawn() => Powers.Reset();
    }
}
