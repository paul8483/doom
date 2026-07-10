using System;
using UnityEngine;
using Doom.Game;

namespace Doom.MapBuild
{
    /// Subscribes player gameplay events to <see cref="SoundSystem"/> local cues.
    public sealed class PlayerSoundController : MonoBehaviour
    {
        SoundSystem sound;
        PlayerWeapons weapons;
        PlayerInventory inventory;
        PlayerHealth health;

        Action<WeaponDef> onFired;
        Action<int, PickupSoundKind> onPickedUp;
        Action<int, FaceAttackerSide> onDamaged;
        Action onDied;

        public void Init(SoundSystem soundSystem, PlayerWeapons weapons,
                         PlayerInventory inventory, PlayerHealth health)
        {
            TearDown();
            sound = soundSystem;
            this.weapons = weapons;
            this.inventory = inventory;
            this.health = health;

            onFired = def =>
            {
                if (def != null && !string.IsNullOrEmpty(def.FireSound))
                    sound?.PlayLocal(def.FireSound);
            };
            onPickedUp = (_, kind) =>
            {
                string lump = PickupSoundTable.LumpName(kind);
                if (lump != null) sound?.PlayLocal(lump);
            };
            onDamaged = (_, __) => sound?.PlayLocal("DSPLPAIN");
            onDied = () => PlayFirst("DSPLDETH", "DSPDIEHI");

            if (weapons != null) weapons.Fired += onFired;
            if (inventory != null) inventory.PickedUp += onPickedUp;
            if (health != null)
            {
                health.Damaged += onDamaged;
                health.Died += onDied;
            }
        }

        void OnDestroy() => TearDown();

        void TearDown()
        {
            if (weapons != null && onFired != null) weapons.Fired -= onFired;
            if (inventory != null && onPickedUp != null) inventory.PickedUp -= onPickedUp;
            if (health != null)
            {
                if (onDamaged != null) health.Damaged -= onDamaged;
                if (onDied != null) health.Died -= onDied;
            }
            onFired = null;
            onPickedUp = null;
            onDamaged = null;
            onDied = null;
            weapons = null;
            inventory = null;
            health = null;
            sound = null;
        }

        void PlayFirst(params string[] lumps)
        {
            if (sound == null) return;
            foreach (string lump in lumps)
            {
                if (sound.Cache != null && sound.Cache.Get(lump) != null)
                {
                    sound.PlayLocal(lump);
                    return;
                }
            }
        }
    }
}
