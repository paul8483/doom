using UnityEngine;
using Unity.Profiling;
using Doom.Game;
using Doom.Things;

namespace Doom.MapBuild
{
    /// Unity body for MonsterBrain: implements IMonsterWorld (movement sweeps,
    /// sight rays, attacks, door use) and pumps the brain at 35 tics/s.
    /// All brain-facing distances are in DOOM units (+y north = Unity +z).
    public sealed class MonsterController : MonoBehaviour
    {
        const float TicSeconds = 1f / 35f;
        const float StepUpUnits = 24f;    // монстр перешагивает <= 24 юнитов
        const float SightRangeM = 1000f;

        static readonly ProfilerMarker BrainTickMarker = new("Doom.Monster.BrainTick");
        static readonly ProfilerMarker SightMarker = new("Doom.Monster.Sight");
        static readonly ProfilerMarker FaceMarker = new("Doom.Monster.Face");
        static readonly ProfilerMarker StepMarker = new("Doom.Monster.Step");
        static readonly ProfilerMarker DoorUseMarker = new("Doom.Monster.DoorUse");
        static readonly ProfilerMarker MeleeMarker = new("Doom.Monster.Melee");
        static readonly ProfilerMarker HitscanMarker = new("Doom.Monster.Hitscan");
        static readonly ProfilerMarker MissileMarker = new("Doom.Monster.Missile");
        static readonly ProfilerMarker SoundMarker = new("Doom.Monster.Sound");

        MonsterDef def;
        bool ambush;
        MonsterBrain brain;
        DoomRandom rng;
        SpriteCache cache;
        float worldScale;
        int corpseFrame;                  // финальный кадр (ThingDef.CorpseFrame)
        Transform target;                 // игрок или монстр-обидчик (infighting)
        Transform player;
        CapsuleCollider capsule;
        SpriteBillboard bb;
        ExperimentalMonsterModel model;   // optional Enhanced 3D presentation
        EnemyHealth health;
        SoundSystem sound;
        float tickAccum;
        float radiusM, heightM;
        int doomEdNum;
        bool dropSpawned;
        // P_DamageMobj retarget threshold (BASETHRESHOLD = 100 tics): while it
        // runs, a monster keeps its current target instead of flip-flopping
        // between every attacker.
        const int BaseThresholdTics = 100;
        int thresholdTics;
        EnemyHealth targetHealth;         // cached for the dead-target check

        /// Every enabled controller in the scene (noise alerts) — saves a
        /// FindObjectsByType scan per gunshot.
        public static readonly System.Collections.Generic.List<MonsterController> Active = new();

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);

        public MonsterBrain Brain => brain;
        public int DoomEdNum => doomEdNum;
        public bool IsAmbush => ambush;
        public bool SupportsExtremeDeath => def?.XDeath != null;

        public void Init(MonsterDef def, bool ambush, int corpseFrame,
                         SpriteCache cache, float worldScale,
                         Transform player, SpriteBillboard bb, CapsuleCollider capsule,
                         EnemyHealth health, DoomRandom rng, int doomEdNum = 0,
                         SoundSystem sound = null)
        {
            this.def = def; this.ambush = ambush; this.corpseFrame = corpseFrame;
            this.cache = cache; this.worldScale = worldScale;
            this.player = player; this.bb = bb; this.capsule = capsule;
            this.health = health; this.rng = rng; this.doomEdNum = doomEdNum;
            this.sound = sound;
            target = player;
            radiusM = capsule != null ? capsule.radius : 0.5f;
            heightM = capsule != null ? capsule.height : 56f * worldScale;
            brain = new MonsterBrain(def, rng, new WorldAdapter(this), ambush);
        }

        /// Optional stop-motion 3D presentation (attached by ThingSpawner after Init).
        public void SetExperimentalModel(ExperimentalMonsterModel m) => model = m;

        public void SetTarget(Transform t)
        {
            target = t != null ? t : player;
            targetHealth = target != null && target != player
                ? target.GetComponent<EnemyHealth>()
                : null;
        }

        /// Damage landed from <paramref name="attacker"/> (null = the player).
        /// P_DamageMobj: `if (source && source != target && !target->threshold)`
        /// → new target, threshold = BASETHRESHOLD. Player hits count too, so a
        /// monster busy infighting can be pulled back onto the player.
        public void NotifyDamagedBy(Transform attacker)
        {
            Transform t = attacker != null ? attacker : player;
            if (t == null || t == transform || t == target) return;
            if (thresholdTics > 0) return;
            SetTarget(t);
            thresholdTics = BaseThresholdTics;
        }
        public Transform TargetForTest => target;
        public void NotifyNoise() => brain.NotifyNoise();
        public void NotifyDamaged() => brain.NotifyDamaged();
        public void NotifyKilled(bool extreme = false) => brain.NotifyKilled(extreme);

        /// Apply transform/health/frame from a save. Dead monsters become corpses
        /// without re-spawning death drops (those live in SpawnedPickups).
        public void ApplySnapshotRestore(int healthValue, int frame, float angleDegrees, bool dead)
            => ApplySnapshotRestore(healthValue, frame, angleDegrees, dead, MonsterAiSnapshot.None);

        public void ApplySnapshotRestore(
            int healthValue, int frame, float angleDegrees, bool dead, MonsterAiSnapshot ai)
        {
            if (bb != null)
                bb.SetDoomAngle(angleDegrees);

            // v7: a monster caught mid-death keeps falling after the load —
            // the brain resumes its death sequence where the save left it.
            if (ai.Present && ai.State == MonsterState.Die && brain != null)
            {
                if (bb != null) bb.ReseedPose(angleDegrees);
                if (model != null) model.ReseedPose(angleDegrees);
                dropSpawned = true;
                if (health != null) health.RestoreHealth(0);
                if (capsule != null) capsule.enabled = false;
                if (model != null) model.NotifyDeathStarted(ai.Extreme);
                brain.RestoreChaseBookkeeping(
                    MonsterState.Die, ai.SeqIndex, ai.Tics, ai.Dir, ai.Moves, ai.Reaction,
                    ai.Attacked, ai.Hit, ai.Extreme);
                return;
            }

            // The transform was moved by the restore without a gameplay tick;
            // re-seed the pose interpolation so the first frame does not lerp
            // from the spawn point to the saved one.
            if (bb != null) bb.ReseedPose(angleDegrees);
            if (model != null) model.ReseedPose(angleDegrees);

            if (dead || healthValue <= 0)
            {
                dropSpawned = true;
                if (health != null) health.RestoreHealth(0);
                if (capsule != null) capsule.enabled = false;
                // A save taken mid-death-animation (health already 0, frame
                // somewhere in the fall) would otherwise freeze the monster
                // half-fallen forever: land it on the corpse frame.
                int restoredCorpse = SnapToCorpseFrame(frame);
                if (brain != null)
                {
                    // A sequence index past the end clamps to the LAST death
                    // frame, so the brain re-emits the corpse instead of the
                    // first fall frame (which would build an unused death mesh
                    // and start the corpse sliding on load).
                    brain.RestoreChaseBookkeeping(
                        MonsterState.Dead, int.MaxValue, 0, Dir8.None, 0, 0, false, false);
                }
                if (bb != null && restoredCorpse >= 0)
                    bb.SetStaticFrame(restoredCorpse);
                if (model != null)
                {
                    // A saved corpse restores onto its death mesh when covered;
                    // gib corpses and uncovered sets fall back to the billboard.
                    if (restoredCorpse >= 0) model.NotifyRestoredFrame(restoredCorpse);
                    else model.RevertToBillboard();
                }
                return;
            }

            if (health != null) health.RestoreHealth(healthValue);
            if (ai.Present && brain != null &&
                ai.State != MonsterState.Die && ai.State != MonsterState.Dead)
            {
                // v7: resume chase / attack / pain exactly where the save left
                // it (the brain re-emits the sequence frame itself). Pre-v7
                // saves keep the old behaviour: awake monsters restart asleep.
                brain.RestoreChaseBookkeeping(
                    ai.State, ai.SeqIndex, ai.Tics, ai.Dir, ai.Moves, ai.Reaction,
                    ai.Attacked, ai.Hit);
                return;
            }
            if (bb != null && frame >= 0) bb.SetFrame(frame);
            if (model != null && frame >= 0) model.NotifyFrame(frame);
        }

        /// Map a saved frame of a dead monster onto its resting corpse frame:
        /// a frame inside the death (or xdeath) sequence that is not the last
        /// one means the save caught the fall mid-way.
        int SnapToCorpseFrame(int frame)
        {
            if (frame < 0) return corpseFrame;
            if (IsNonFinalFrameOf(def.Death, frame)) return corpseFrame;
            if (def.XDeath != null && IsNonFinalFrameOf(def.XDeath, frame))
                return def.XDeathCorpseFrame >= 0 ? def.XDeathCorpseFrame : frame;
            return frame;
        }

        static bool IsNonFinalFrameOf(MonsterSeq seq, int frame)
        {
            var frames = seq?.Frames;
            if (frames == null || frames.Length == 0) return false;
            for (int i = 0; i < frames.Length - 1; i++)
                if (frames[i] == frame) return true;
            return false;
        }

        static float NormAngle(float deg)
        {
            deg %= 360f;
            return deg < 0f ? deg + 360f : deg;
        }

        void Update()
        {
            if (brain == null) return;
            tickAccum += Time.deltaTime;
            while (tickAccum >= TicSeconds)
            {
                tickAccum -= TicSeconds;
                // A_Chase: a dead (or gone) infight target hands the monster
                // back to the player — corpses are never destroyed, so the
                // Transform alone cannot tell. The threshold decays per tic
                // and clears at once when the target is dead.
                if (target != player &&
                    (target == null || targetHealth == null || targetHealth.IsDead))
                {
                    SetTarget(null);
                    thresholdTics = 0;
                }
                else if (thresholdTics > 0)
                    thresholdTics--;
                using (BrainTickMarker.Auto())
                    brain.Tick();
                if (bb != null)
                {
                    bb.NotifyGameplayPose(transform.position, bb.DoomAngleDegrees);
                    if (model != null)
                        model.NotifyGameplayPose(transform.position, bb.DoomAngleDegrees);
                }
            }
            // Sprite rotation must track the target every render frame — not only on
            // 35 Hz brain ticks — otherwise the billboard lags between chase steps.
            var s = brain.State;
            if (bb != null && target != null &&
                (s == MonsterState.Chase || s == MonsterState.Attack || s == MonsterState.Pain))
                Face();
        }

        // ── IMonsterWorld через адаптер (метод на метод) ──────────────────────
        sealed class WorldAdapter : IMonsterWorld
        {
            readonly MonsterController c;
            public WorldAdapter(MonsterController c) { this.c = c; }
            public bool CanSeeTarget(bool frontOnly)
            {
                using (SightMarker.Auto())
                    return c.CanSee(frontOnly);
            }
            public float DistanceToTarget() => c.DistUnits();
            public float TargetRadiusUnits() => c.TargetRadius();
            public void TargetDelta(out float dx, out float dy) => c.Delta(out dx, out dy);
            public void FaceTarget()
            {
                using (FaceMarker.Auto())
                    c.Face();
            }
            public StepResult TryStep(Dir8 d)
            {
                using (StepMarker.Auto())
                    return c.Step(d);
            }
            public void UseDoor()
            {
                using (DoorUseMarker.Auto())
                    c.UseDoorAhead();
            }
            public void MeleeHit(int dmg)
            {
                using (MeleeMarker.Auto())
                    c.Melee(dmg);
            }
            public void FireHitscan(int n)
            {
                using (HitscanMarker.Auto())
                    c.Hitscan(n);
            }
            public void LaunchMissile()
            {
                using (MissileMarker.Auto())
                    c.Missile();
            }
            public void SetFrame(int f)
            {
                if (c.bb != null) c.bb.SetFrame(f);
                if (c.model != null) c.model.NotifyFrame(f);
            }
            public void OnDeathStarted()
            {
                if (c.capsule != null) c.capsule.enabled = false;
                // Monsters with a covered death tail keep the mesh through the
                // fall; gibs and uncovered sets hand over to the billboard
                // before the first death frame shows.
                if (c.model != null)
                    c.model.NotifyDeathStarted(c.brain.IsExtremeDeath);
                c.SpawnDeathDrop();
            }
            public void OnBecameCorpse()
            {
                int finalFrame = c.brain.IsExtremeDeath && c.def.XDeathCorpseFrame >= 0
                    ? c.def.XDeathCorpseFrame
                    : c.corpseFrame;
                if (finalFrame < 0) return;
                if (c.bb != null) c.bb.SetStaticFrame(finalFrame);
                // Outside coverage (xdeath corpse) this reverts to the billboard.
                if (c.model != null) c.model.NotifyFrame(finalFrame);
            }
            public void PlaySound(MonsterSoundCue cue, int variant)
            {
                using (SoundMarker.Auto())
                    c.PlayCue(cue, variant);
            }
        }

        void PlayCue(MonsterSoundCue cue, int variant)
        {
            if (sound == null || def?.Sounds == null) return;
            string name = ResolveCue(cue, variant);
            if (!string.IsNullOrEmpty(name))
                sound.PlayAt(name, transform.position, SoundCueContext.Monster);
        }

        string ResolveCue(MonsterSoundCue cue, int variant)
        {
            var s = def.Sounds;
            switch (cue)
            {
                case MonsterSoundCue.Sight:
                    return Pick(s.Sight, variant);
                case MonsterSoundCue.Active:
                    return s.Active;
                case MonsterSoundCue.RangedAttack:
                    return s.RangedAttack;
                case MonsterSoundCue.MeleeAttack:
                    return s.MeleeAttack;
                case MonsterSoundCue.Pain:
                    return s.Pain;
                case MonsterSoundCue.Death:
                    return Pick(s.Death, variant);
                default:
                    return null;
            }
        }

        static string Pick(string[] names, int variant)
        {
            if (names == null || names.Length == 0) return null;
            int i = variant % names.Length;
            if (i < 0) i += names.Length;
            return names[i];
        }

        // Keeps death-drop and corpse billboards off the same plane (z-fighting).
        const float DeathDropOffsetDoom = 12f;

        void SpawnDeathDrop()
        {
            if (dropSpawned) return;
            dropSpawned = true;
            if (!DeathDropTable.TryGet(doomEdNum, out int dropNum)) return;

            Vector3 facing = bb != null ? bb.FacingDirection : transform.forward;
            facing.y = 0f;
            if (facing.sqrMagnitude < 1e-6f) facing = Vector3.forward;
            else facing.Normalize();
            Vector3 dropPos = transform.position + facing * (DeathDropOffsetDoom * worldScale);

            PickupFactory.Spawn(cache, worldScale, dropNum, dropPos, transform.parent,
                                dropped: true);
        }

        Vector3 EyePos() => transform.position + Vector3.up * (heightM * 0.75f);
        Vector3 TargetCenter()
        {
            if (target == null) return transform.position;
            var cc = target.GetComponent<CharacterController>();
            if (cc != null)
                return target.position + Vector3.up * (cc.height * 0.5f);
            var cap = target.GetComponent<CapsuleCollider>();
            if (cap != null)
                return cap.bounds.center;
            return target.position + Vector3.up * (heightM * 0.5f);
        }

        bool CanSee(bool frontOnly)
        {
            if (target == null) return false;
            Vector3 to = TargetCenter() - EyePos();
            if (frontOnly)
            {
                // Передняя полусфера относительно DOOM-угла спавна (хранится в billboard).
                Vector3 facing = bb != null ? bb.FacingDirection : transform.forward;
                Vector3 flat = new Vector3(to.x, 0f, to.z);
                if (Vector3.Dot(facing, flat) < 0f) return false;
            }
            if (!Physics.Raycast(EyePos(), to.normalized, out var hit, SightRangeM,
                                 ~0, QueryTriggerInteraction.Ignore))
                return false;
            // Монстры луч зрения не блокируют (DOOM P_CheckSight игнорирует things):
            // если упёрлись в чужой EnemyHealth — пробуем сквозь него.
            var t = hit.transform;
            if (t == target || t.IsChildOf(target)) return true;
            if (hit.collider.GetComponent<EnemyHealth>() != null)
            {
                // Один повторный луч из-за спины блокера (достаточно для E1-плотности).
                Vector3 resume = hit.point + to.normalized * (hit.collider.bounds.size.magnitude);
                if (Physics.Raycast(resume, to.normalized, out var hit2,
                        SightRangeM, ~0, QueryTriggerInteraction.Ignore))
                    return hit2.transform == target || hit2.transform.IsChildOf(target);
            }
            return false;
        }

        float DistUnits()
            => target == null ? float.MaxValue
               : Vector3.Distance(FlatPos(transform.position), FlatPos(target.position)) / worldScale;

        static Vector3 FlatPos(Vector3 p) => new Vector3(p.x, 0f, p.z);

        float TargetRadius()
        {
            var cc = target != null ? target.GetComponent<CharacterController>() : null;
            if (cc != null) return cc.radius / worldScale;
            var cap = target != null ? target.GetComponent<CapsuleCollider>() : null;
            return cap != null ? cap.radius / worldScale : 16f;
        }

        void Delta(out float dx, out float dy)
        {
            if (target == null) { dx = dy = 0f; return; }
            Vector3 d = target.position - transform.position;
            dx = d.x / worldScale;
            dy = d.z / worldScale;   // DOOM north = Unity +z
        }

        void Face()
        {
            if (target == null || bb == null) return;
            Vector3 aim = target.position;
            if (target.GetComponent<PlayerHealth>() != null)
            {
                var cam = Camera.main;
                if (cam != null) aim = cam.transform.position;
            }
            Vector3 d = aim - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 1e-8f) return;
            bb.SetDoomAngle(NormAngle(Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg));
        }

        static readonly Vector3[] DirVectors =
        {
            new Vector3(1, 0, 0), new Vector3(1, 0, 1).normalized, new Vector3(0, 0, 1),
            new Vector3(-1, 0, 1).normalized, new Vector3(-1, 0, 0),
            new Vector3(-1, 0, -1).normalized, new Vector3(0, 0, -1),
            new Vector3(1, 0, -1).normalized
        };

        StepResult Step(Dir8 dir)
        {
            float stepM = def.Speed * worldScale;
            Vector3 move = DirVectors[(int)dir] * stepM;
            float stepUpM = StepUpUnits * worldScale;
            Vector3 from = transform.position;

            // Свип капсулой с высоты «плечи» — перешагиваемые ступени не мешают.
            Vector3 p1 = transform.position + Vector3.up * (radiusM + stepUpM);
            Vector3 p2 = transform.position + Vector3.up * (heightM - radiusM);
            if (Physics.CapsuleCast(p1, p2, radiusM * 0.95f, move.normalized,
                                    out var hit, stepM, ~0, QueryTriggerInteraction.Ignore))
            {
                var lineRef = hit.collider.GetComponentInParent<LineRef>();
                if (lineRef != null && LineActivator.IsUsableDoor(lineRef, hit.point))
                    return StepResult.BlockedByDoor;
                return StepResult.Blocked;
            }

            // Пол в точке назначения: перепад <= 24 юнитов вверх и вниз.
            Vector3 dest = transform.position + move + Vector3.up * (stepUpM + 0.05f);
            if (!Physics.Raycast(dest, Vector3.down, out var floorHit,
                                 stepUpM * 2f + 0.1f, ~0, QueryTriggerInteraction.Ignore))
                return StepResult.Blocked;   // впереди обрыв больше ступеньки

            transform.position = new Vector3(dest.x, floorHit.point.y, dest.z);
            LineActivator.MonsterCrossed(from, transform.position, transform);
            return StepResult.Moved;
        }

        void UseDoorAhead()
        {
            LineActivator.MonsterUseNearestDoor(transform.position, 64f * worldScale);
        }

        void Melee(int damage)
        {
            if (target == null) return;
            var ph = target.GetComponent<PlayerHealth>();
            if (ph != null) { ph.TakeDamage(damage); return; }
            var eh = target.GetComponent<EnemyHealth>();
            if (eh != null && !eh.IsDead)
                eh.TakeDamage(damage, DamageSource.Monster(health));
        }

        void Hitscan(int count)
        {
            if (target == null) return;
            Vector3 origin = EyePos();
            Vector3 baseDir = (TargetCenter() - origin).normalized;
            float rangeM = HitscanRules.HitscanRangeDoom * worldScale;
            for (int i = 0; i < count; i++)
            {
                int damage = MonsterRules.RollDamage(rng, 5, 3);
                float yaw = MonsterRules.SpreadOffsetDeg(rng);
                Vector3 dir = Quaternion.AngleAxis(yaw, Vector3.up) * baseDir;
                if (!Physics.Raycast(origin, dir, out var hit, rangeM,
                                     ~0, QueryTriggerInteraction.Ignore)) continue;
                var ph = hit.collider.GetComponent<PlayerHealth>();
                var eh = hit.collider.GetComponent<EnemyHealth>();
                if (ph != null)
                {
                    ph.TakeDamage(damage);
                    HitEffect.SpawnBlood(cache, worldScale, hit.point);
                }
                else if (eh != null && eh != health && !eh.IsDead)
                {
                    eh.TakeDamage(damage, DamageSource.Monster(health));
                    if (!eh.NoBlood)
                        HitEffect.SpawnBlood(cache, worldScale, hit.point);
                }
                else if (eh == null)
                    HitEffect.SpawnPuff(cache, worldScale, hit.point, hit.normal);
            }
        }

        void Missile()
        {
            if (target == null) return;
            Vector3 from = transform.position + Vector3.up * (def.MissileSpawnHeight * worldScale);
            // Launch SFX is owned by MonsterBrain (RangedAttack / DSFIRSHT).
            Projectile.Launch(cache, def, worldScale, rng, from, TargetCenter(), health, sound, doomEdNum);
        }
    }
}
