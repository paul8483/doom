using UnityEngine;
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
        EnemyHealth health;
        float tickAccum;
        float radiusM, heightM;
        int doomEdNum;
        bool dropSpawned;

        public MonsterBrain Brain => brain;
        public bool IsAmbush => ambush;

        public void Init(MonsterDef def, bool ambush, int corpseFrame,
                         SpriteCache cache, float worldScale,
                         Transform player, SpriteBillboard bb, CapsuleCollider capsule,
                         EnemyHealth health, DoomRandom rng, int doomEdNum = 0)
        {
            this.def = def; this.ambush = ambush; this.corpseFrame = corpseFrame;
            this.cache = cache; this.worldScale = worldScale;
            this.player = player; this.bb = bb; this.capsule = capsule;
            this.health = health; this.rng = rng; this.doomEdNum = doomEdNum;
            target = player;
            radiusM = capsule != null ? capsule.radius : 0.5f;
            heightM = capsule != null ? capsule.height : 56f * worldScale;
            brain = new MonsterBrain(def, rng, new WorldAdapter(this), ambush);
        }

        public void SetTarget(Transform t) => target = t != null ? t : player;
        public Transform TargetForTest => target;
        public void NotifyNoise() => brain.NotifyNoise();
        public void NotifyDamaged() => brain.NotifyDamaged();
        public void NotifyKilled() => brain.NotifyKilled();

        static float NormAngle(float deg)
        {
            deg %= 360f;
            return deg < 0f ? deg + 360f : deg;
        }

        void Update()
        {
            if (brain == null) return;
            // Цель умерла (монстр): назад на игрока.
            if (target != player && target == null) target = player;
            tickAccum += Time.deltaTime;
            while (tickAccum >= TicSeconds) { tickAccum -= TicSeconds; brain.Tick(); }
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
            public bool CanSeeTarget(bool frontOnly) => c.CanSee(frontOnly);
            public float DistanceToTarget() => c.DistUnits();
            public float TargetRadiusUnits() => c.TargetRadius();
            public void TargetDelta(out float dx, out float dy) => c.Delta(out dx, out dy);
            public void FaceTarget() => c.Face();
            public StepResult TryStep(Dir8 d) => c.Step(d);
            public void UseDoor() => c.UseDoorAhead();
            public void MeleeHit(int dmg) => c.Melee(dmg);
            public void FireHitscan(int n) => c.Hitscan(n);
            public void LaunchMissile() => c.Missile();
            public void SetFrame(int f) { if (c.bb != null) c.bb.SetFrame(f); }
            public void OnDeathStarted()
            {
                if (c.capsule != null) c.capsule.enabled = false;
                c.SpawnDeathDrop();
            }
            public void OnBecameCorpse()
            { if (c.bb != null && c.corpseFrame >= 0) c.bb.SetStaticFrame(c.corpseFrame); }
        }

        void SpawnDeathDrop()
        {
            if (dropSpawned) return;
            dropSpawned = true;
            if (!DeathDropTable.TryGet(doomEdNum, out int dropNum)) return;
            PickupFactory.Spawn(cache, worldScale, dropNum, transform.position, transform.parent);
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

            // Свип капсулой с высоты «плечи» — перешагиваемые ступени не мешают.
            Vector3 p1 = transform.position + Vector3.up * (radiusM + stepUpM);
            Vector3 p2 = transform.position + Vector3.up * (heightM - radiusM);
            if (Physics.CapsuleCast(p1, p2, radiusM * 0.95f, move.normalized,
                                    out var hit, stepM, ~0, QueryTriggerInteraction.Ignore))
            {
                var lineRef = hit.collider.GetComponentInParent<LineRef>();
                if (lineRef != null && LineActivator.IsUsableDoor(lineRef))
                    return StepResult.BlockedByDoor;
                return StepResult.Blocked;
            }

            // Пол в точке назначения: перепад <= 24 юнитов вверх и вниз.
            Vector3 dest = transform.position + move + Vector3.up * (stepUpM + 0.05f);
            if (!Physics.Raycast(dest, Vector3.down, out var floorHit,
                                 stepUpM * 2f + 0.1f, ~0, QueryTriggerInteraction.Ignore))
                return StepResult.Blocked;   // впереди обрыв больше ступеньки

            transform.position = new Vector3(dest.x, floorHit.point.y, dest.z);
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
            Projectile.Launch(cache, def, worldScale, rng, from, TargetCenter(), health);
        }
    }
}
