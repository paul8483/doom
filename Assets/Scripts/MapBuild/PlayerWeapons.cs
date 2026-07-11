using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Doom.Game;

namespace Doom.MapBuild
{
    /// Player weapons: input (LMB, slots 1-5), cooldowns in tics, hitscan/projectile damage,
    /// effects. Rules live in Doom.Game; this is only the Unity glue.
    public sealed class PlayerWeapons : MonoBehaviour
    {
        public AmmoModel Ammo { get; } = new AmmoModel();
        public WeaponLoadout Loadout { get; } = new WeaponLoadout();
        public DoomRandom Rng => rng;

        /// A shot was fired -- WeaponView plays the fire frames and muzzle flash.
        public event Action<WeaponDef> Fired;

        SpriteCache cache;
        float worldScale;
        Transform cam;
        SoundSystem sound;
        readonly DoomRandom rng = new DoomRandom();
        readonly List<HitscanShot> volley = new List<HitscanShot>();
        PlayerInventory inventory;

        InputActionMap map;
        InputAction fireAction;
        float cooldown;       // seconds until the next shot is allowed
        bool refire;          // LMB has been held since the last shot

        public void Init(
            SpriteCache cache, float worldScale, Transform cameraTransform,
            SoundSystem soundSystem = null)
        {
            this.cache = cache;
            this.worldScale = worldScale;
            cam = cameraTransform;
            sound = soundSystem;

            map = new InputActionMap("Weapons");
            fireAction = map.AddAction("fire", InputActionType.Button, "<Mouse>/leftButton");
            for (int slot = 1; slot <= 5; slot++)
            {
                int s = slot;
                var a = map.AddAction($"slot{s}", InputActionType.Button, $"<Keyboard>/{s}");
                a.performed += _ => SelectSlot(s);
            }
            map.Enable();
        }

        public void SetInventory(PlayerInventory inv) => inventory = inv;

        // Pair the action map with component enable state (as PlayerController does),
        // so disabling this component (e.g. on death) also mutes weapon input.
        // OnEnable runs at AddComponent time, before Init builds the map — the
        // null guards cover that; Init enables the map itself.
        void OnEnable() { map?.Enable(); }
        void OnDisable() { map?.Disable(); }
        void OnDestroy() => map?.Dispose();

        void SelectSlot(int slot)
        {
            if (cooldown > 0f) return; // can't switch mid-shot
            if (slot == 1)
            {
                // Slot 1: prefer chainsaw when owned (DOOM wp_chainsaw over fist).
                if (Loadout.Has(WeaponId.Chainsaw))
                    Loadout.TrySelect(WeaponId.Chainsaw);
                else
                    Loadout.TrySelect(WeaponId.Fist);
                return;
            }
            foreach (WeaponId id in Enum.GetValues(typeof(WeaponId)))
                if (WeaponTable.Get(id).Slot == slot) { Loadout.TrySelect(id); return; }
        }

        void Update()
        {
            cooldown -= Time.deltaTime;
            bool held = fireAction != null && fireAction.IsPressed();
            if (!held) refire = false;
            if (held && cooldown <= 0f) FireCurrent();
        }

        void FireCurrent()
        {
            var def = WeaponTable.Get(Loadout.Current);
            if (!Ammo.TryConsume(def.Ammo, def.AmmoPerShot))
            {
                Loadout.TrySelect(Loadout.BestAvailable(Ammo)); // auto-downgrade
                return;
            }

            if (def.Id == WeaponId.RocketLauncher)
            {
                PlayerRocketProjectile.Launch(
                    cache, worldScale, rng, cam.position, cam.forward, transform, sound);
                cooldown = def.CycleTics / 35f;
                refire = true;
                Fired?.Invoke(def);
                return;
            }

            volley.Clear();
            bool berserk = inventory != null && inventory.Powers.Berserk;
            HitscanRules.FireVolley(def, refire, rng, volley, berserk);
            float rangeDoom = def.Id == WeaponId.Chainsaw
                ? HitscanRules.SawRangeDoom
                : def.Melee ? HitscanRules.MeleeRangeDoom
                            : HitscanRules.HitscanRangeDoom;
            float range = rangeDoom * worldScale;

            foreach (var shot in volley)
            {
                var dir = Quaternion.AngleAxis(shot.YawOffsetDeg, Vector3.up) * cam.forward;
                // Fire from the camera itself: PhysX raycasts never hit a collider
                // the ray starts inside, so the player's own capsule is immune,
                // and any offset would create a point-blank dead zone.
                var origin = cam.position;
                if (!Physics.Raycast(origin, dir, out var hit, range,
                                     ~0, QueryTriggerInteraction.Ignore)) continue;

                var enemy = hit.collider.GetComponent<EnemyHealth>();
                if (enemy != null && !enemy.IsDead)
                {
                    enemy.TakeDamage(shot.Damage, DamageSource.Player());
                    if (!enemy.NoBlood)
                        HitEffect.SpawnBlood(cache, worldScale, hit.point);
                }
                else if (enemy == null)
                {
                    HitEffect.SpawnPuff(cache, worldScale, hit.point, hit.normal);
                }
            }

            cooldown = def.CycleTics / 35f;
            refire = true;
            Fired?.Invoke(def);
        }

        /// Weapon/ammo/item pickup. Delegates to PlayerInventory / ItemRules.
        public bool Pickup(int doomedNum)
            => inventory != null && inventory.TryPickup(doomedNum);

        public void ResetToStart() { Ammo.Reset(); Loadout.Reset(); cooldown = 0f; refire = false; }

        // -- for PlayMode tests --------------------------------------------------
        public void FireOnceForTest() { if (cooldown <= 0f) FireCurrent(); }
        public float CooldownForTest => cooldown;
    }
}
