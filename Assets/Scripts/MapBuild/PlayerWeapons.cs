using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Doom.Game;

namespace Doom.MapBuild
{
    /// Player weapons: input (LMB, slots 1-7), tic scheduler, hitscan/projectile damage.
    /// Rules live in Doom.Game; this is only the Unity glue.
    public sealed class PlayerWeapons : MonoBehaviour
    {
        public AmmoModel Ammo { get; } = new AmmoModel();
        public WeaponLoadout Loadout { get; } = new WeaponLoadout();
        public DoomRandom Rng => rng;
        public WeaponActionScheduler Scheduler => scheduler;

        /// Attack view / fire-sound start (BFG charge sound, psprite sequence).
        public event Action<WeaponDef> Fired;

        /// Ammo spent + projectile/hitscan + gunfire noise. Same frame as Fired
        /// for immediate weapons; delayed to ActionTic for BFG.
        public event Action<WeaponDef> Committed;

        SpriteCache cache;
        float worldScale;
        Transform cam;
        SoundSystem sound;
        readonly DoomRandom rng = new DoomRandom();
        readonly WeaponActionScheduler scheduler = new WeaponActionScheduler();
        readonly List<HitscanShot> volley = new List<HitscanShot>();
        PlayerInventory inventory;

        InputActionMap map;
        InputAction fireAction;
        float ticAccumulator;
        bool heldRefire;
        Vector3 pendingShotDirection = Vector3.forward;

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
            for (int slot = 1; slot <= 7; slot++)
            {
                int s = slot;
                var a = map.AddAction($"slot{s}", InputActionType.Button, $"<Keyboard>/{s}");
                a.performed += _ => SelectSlot(s);
            }
            map.Enable();
        }

        public void SetInventory(PlayerInventory inv) => inventory = inv;

        void OnEnable() { map?.Enable(); }
        void OnDisable() { map?.Disable(); }
        void OnDestroy() => map?.Dispose();

        void SelectSlot(int slot)
        {
            WeaponId? requested = ResolveSlot(slot);
            if (!requested.HasValue) return;

            if (scheduler.IsRunning)
                Loadout.TryQueuePending(requested.Value);
            else
                Loadout.TrySelect(requested.Value);
        }

        WeaponId? ResolveSlot(int slot)
        {
            if (slot == 1)
            {
                if (Loadout.Has(WeaponId.Chainsaw))
                    return WeaponId.Chainsaw;
                return WeaponId.Fist;
            }
            foreach (WeaponId id in Enum.GetValues(typeof(WeaponId)))
                if (WeaponTable.Get(id).Slot == slot && Loadout.Has(id))
                    return id;
            return null;
        }

        void Update()
        {
            bool held = fireAction != null && fireAction.IsPressed();
            if (!held) heldRefire = false;
            if (held)
                TryStartAttack();

            ticAccumulator += Time.deltaTime * 35f;
            while (ticAccumulator >= 1f)
            {
                ticAccumulator -= 1f;
                AdvanceOneTic();
                if (held)
                    TryStartAttack();
            }
        }

        void TryStartAttack()
        {
            // A queued slot request owns the next ready boundary; do not let
            // held-fire refire restart the scheduler and postpone it forever.
            if (scheduler.IsRunning && Loadout.HasPending) return;
            var def = WeaponTable.Get(Loadout.Current);
            if (!scheduler.CanBegin(def)) return;
            if (def.Ammo != AmmoType.None && Ammo.Get(def.Ammo) < def.AmmoPerShot)
            {
                Loadout.TrySelect(Loadout.BestAvailable(Ammo));
                return;
            }

            if (!scheduler.TryBegin(def, Ammo)) return;

            if (cam != null)
                pendingShotDirection = cam.forward.sqrMagnitude > 1e-8f
                    ? cam.forward.normalized : Vector3.forward;
            else
                pendingShotDirection = Vector3.forward;

            Fired?.Invoke(def);

            // Presentation muzzle flash light — same event as WeaponView/sound, before damage.
            if (cam != null && def.FlashSprite != null)
            {
                float tics = 4f;
                if (def.FlashTics != null && def.FlashTics.Length > 0)
                {
                    tics = 0f;
                    for (int i = 0; i < def.FlashTics.Length; i++)
                        tics += def.FlashTics[i];
                }
                // Slightly ahead of the lens so the light fills the room edge,
                // not a full-screen bloom wash from camera-near intensity.
                var muzzlePos = cam.position + cam.forward * (22f * worldScale);
                Rendering.EnhancedLightSystem.Instance?.PulseMuzzle(
                    muzzlePos,
                    worldScale,
                    tics);
                Rendering.ParticleEffectPool.Instance?.Pulse(
                    Rendering.EffectKind.Muzzle,
                    muzzlePos,
                    worldScale);
            }

            if (scheduler.IsCommitted)
                CommitAction(def);
        }

        void AdvanceOneTic()
        {
            if (!scheduler.IsRunning) return;
            scheduler.Advance(out bool justCommitted, out bool justFinished);
            if (justCommitted)
                CommitAction(scheduler.Active);
            if (justFinished && Loadout.HasPending)
                Loadout.TrySelect(Loadout.Pending);
        }

        void CommitAction(WeaponDef def)
        {
            if (def == null) return;
            if (!Ammo.TryConsume(def.Ammo, def.AmmoPerShot))
            {
                // Should not happen: Begin checked ammo; cancel and downgrade.
                scheduler.Cancel();
                Loadout.TrySelect(Loadout.BestAvailable(Ammo));
                return;
            }

            if (def.Id == WeaponId.RocketLauncher)
            {
                Vector3 origin = cam != null ? cam.position : transform.position;
                PlayerRocketProjectile.Launch(
                    cache, worldScale, rng, origin, pendingShotDirection, transform, sound);
            }
            else if (def.Id == WeaponId.PlasmaRifle)
            {
                Vector3 origin = cam != null ? cam.position : transform.position;
                PlayerPlasmaProjectile.Launch(
                    cache, worldScale, rng, origin, pendingShotDirection, transform, sound);
            }
            else if (def.Id == WeaponId.Bfg9000)
            {
                Vector3 origin = cam != null ? cam.position : transform.position;
                PlayerBfgProjectile.Launch(
                    cache, worldScale, rng, origin, pendingShotDirection, transform, sound);
            }
            else if (def.Pellets > 0)
            {
                FireHitscan(def);
            }

            Committed?.Invoke(def);
        }

        void FireHitscan(WeaponDef def)
        {
            volley.Clear();
            bool berserk = inventory != null && inventory.Powers.Berserk;
            HitscanRules.FireVolley(def, heldRefire, rng, volley, berserk);
            heldRefire = true;
            float rangeDoom = def.Id == WeaponId.Chainsaw
                ? HitscanRules.SawRangeDoom
                : def.Melee ? HitscanRules.MeleeRangeDoom
                            : HitscanRules.HitscanRangeDoom;
            float range = rangeDoom * worldScale;
            Vector3 origin = cam != null ? cam.position : transform.position;
            Vector3 forward = pendingShotDirection;

            foreach (var shot in volley)
            {
                var dir = Quaternion.AngleAxis(shot.YawOffsetDeg, Vector3.up) * forward;
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
        }

        /// Weapon/ammo/item pickup. Delegates to PlayerInventory / ItemRules.
        public bool Pickup(int doomedNum)
            => inventory != null && inventory.TryPickup(doomedNum);

        public void ResetToStart()
        {
            Ammo.Reset();
            Loadout.Reset();
            scheduler.Cancel();
            ticAccumulator = 0f;
            heldRefire = false;
        }

        // -- for PlayMode / EditMode tests --------------------------------------
        public void FireOnceForTest()
        {
            TryStartAttack();
            // Drain immediate ActionTic==0 commit already done in TryStartAttack.
        }

        public void AdvanceTicsForTest(int tics)
        {
            for (int i = 0; i < tics; i++)
                AdvanceOneTic();
        }

        public void SelectSlotForTest(int slot) => SelectSlot(slot);

        public float CooldownForTest =>
            scheduler.IsRunning
                ? (scheduler.Active.EffectiveRefireTics - scheduler.TicsElapsed) / 35f
                : 0f;
    }
}
