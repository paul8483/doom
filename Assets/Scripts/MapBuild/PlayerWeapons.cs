using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Doom.Game;

namespace Doom.MapBuild
{
    /// Player weapons: input (LMB, 1-4), cooldowns in tics, hitscan rays, damage,
    /// effects. Rules live in Doom.Game; this is only the Unity glue.
    public sealed class PlayerWeapons : MonoBehaviour
    {
        public AmmoModel Ammo { get; } = new AmmoModel();
        public WeaponLoadout Loadout { get; } = new WeaponLoadout();

        /// A shot was fired -- WeaponView plays the fire frames and muzzle flash.
        public event Action<WeaponDef> Fired;

        SpriteCache cache;
        float worldScale;
        Transform cam;
        readonly DoomRandom rng = new DoomRandom();
        readonly List<HitscanShot> volley = new List<HitscanShot>();

        InputActionMap map;
        InputAction fireAction;
        float cooldown;       // seconds until the next shot is allowed
        bool refire;          // LMB has been held since the last shot

        public void Init(SpriteCache cache, float worldScale, Transform cameraTransform)
        {
            this.cache = cache;
            this.worldScale = worldScale;
            cam = cameraTransform;

            map = new InputActionMap("Weapons");
            fireAction = map.AddAction("fire", InputActionType.Button, "<Mouse>/leftButton");
            for (int slot = 1; slot <= 4; slot++)
            {
                int s = slot;
                var a = map.AddAction($"slot{s}", InputActionType.Button, $"<Keyboard>/{s}");
                a.performed += _ => SelectSlot(s);
            }
            map.Enable();
        }

        void OnDestroy() => map?.Dispose();

        void SelectSlot(int slot)
        {
            if (cooldown > 0f) return; // can't switch mid-shot
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

            volley.Clear();
            HitscanRules.FireVolley(def, refire, rng, volley);
            float rangeDoom = def.Melee ? HitscanRules.MeleeRangeDoom
                                        : HitscanRules.HitscanRangeDoom;
            float range = rangeDoom * worldScale;

            foreach (var shot in volley)
            {
                var dir = Quaternion.AngleAxis(shot.YawOffsetDeg, Vector3.up) * cam.forward;
                // Start slightly ahead so we don't hit our own capsule (r=0.5m).
                var origin = cam.position + dir * 0.6f;
                if (!Physics.Raycast(origin, dir, out var hit, range,
                                     ~0, QueryTriggerInteraction.Ignore)) continue;

                var enemy = hit.collider.GetComponent<EnemyHealth>();
                if (enemy != null && !enemy.IsDead)
                {
                    enemy.TakeDamage(shot.Damage);
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

        /// Weapon/ammo pickup. Returns true if the item was consumed (destroy the GO).
        public bool Pickup(int doomedNum)
        {
            switch (doomedNum)
            {
                case 2001: return PickWeapon(WeaponId.Shotgun, AmmoType.Shells, 8);
                case 2002: return PickWeapon(WeaponId.Chaingun, AmmoType.Bullets, 20);
                case 2007: return Ammo.Add(AmmoType.Bullets, 10);
                case 2048: return Ammo.Add(AmmoType.Bullets, 50);
                case 2008: return Ammo.Add(AmmoType.Shells, 4);
                case 2049: return Ammo.Add(AmmoType.Shells, 20);
                default: return false;
            }
        }

        bool PickWeapon(WeaponId id, AmmoType ammo, int give)
        {
            bool gotWeapon = Loadout.Give(id);      // auto-selects if new
            bool gotAmmo = Ammo.Add(ammo, give);
            return gotWeapon || gotAmmo;            // weapon owned AND ammo full -> stays on the ground
        }

        public void ResetToStart() { Ammo.Reset(); Loadout.Reset(); cooldown = 0f; refire = false; }

        // -- for PlayMode tests --------------------------------------------------
        public void FireOnceForTest() { if (cooldown <= 0f) FireCurrent(); }
        public float CooldownForTest => cooldown;
    }
}
