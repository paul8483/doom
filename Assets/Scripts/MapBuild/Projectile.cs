using UnityEngine;
using Doom.Game;
using Doom.Things;

namespace Doom.MapBuild
{
    /// Monster projectile (imp fireball): straight flight at DOOM speed,
    /// sphere-cast collision, explosion frames, then Destroy. Fly/explode
    /// frames come from MonsterDef; damage is rolled on impact (p_inter.c).
    public sealed class Projectile : MonoBehaviour, IProjectileSnapshotSource
    {
        SpriteCache cache;
        MonsterDef def;
        float worldScale;
        Vector3 velocity;         // m/s
        DoomRandom rng;
        SpriteBillboard bb;
        float castRadius;
        EnemyHealth owner;
        SoundSystem sound;
        int typeEdNum;

        int flyIdx;
        float flyLeft;
        bool exploding;
        int boomIdx;
        float boomLeft;

        public static void Launch(SpriteCache cache, MonsterDef def, float worldScale,
                                  DoomRandom rng, Vector3 from, Vector3 targetPoint,
                                  EnemyHealth owner = null, SoundSystem sound = null,
                                  int typeEdNum = 0)
        {
            Vector3 delta = targetPoint - from;
            if (delta.sqrMagnitude < 1e-8f) delta = Vector3.forward;
            float speed = def.MissileSpeed * 35f * worldScale;
            Vector3 velocity = delta.normalized * speed;
            LaunchInternal(cache, def, worldScale, rng, from, velocity,
                owner, sound, typeEdNum, forcedSpawnId: null, remainingLife: -1f);
        }

        /// Recreate a projectile from a save snapshot (forced SpawnId + velocity).
        public static void LaunchFromSnapshot(
            SpriteCache cache, MonsterDef def, float worldScale, DoomRandom rng,
            ProjectileSnapshot snap, EnemyHealth owner = null, SoundSystem sound = null)
        {
            if (snap == null || def == null || cache == null) return;
            var from = new Vector3(snap.X, snap.Y, snap.Z);
            var velocity = new Vector3(snap.VelX, snap.VelY, snap.VelZ);
            LaunchInternal(cache, def, worldScale, rng, from, velocity,
                owner, sound, snap.Type, snap.SpawnId, snap.RemainingLife);
        }

        static void LaunchInternal(
            SpriteCache cache, MonsterDef def, float worldScale, DoomRandom rng,
            Vector3 from, Vector3 velocity, EnemyHealth owner, SoundSystem sound,
            int typeEdNum, int? forcedSpawnId, float remainingLife)
        {
            var go = new GameObject($"Missile_{def.MissileSprite}",
                typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.position = from;
            var bb = go.AddComponent<SpriteBillboard>();
            bb.Init(cache, def.MissileSprite, def.MissileFlyFrames[0], worldScale,
                    doomAngleDeg: 0f, spawnCeiling: false, ceilingY: 0f);
            bb.SetStaticFrame(def.MissileFlyFrames[0]);

            var p = go.AddComponent<Projectile>();
            p.cache = cache; p.def = def; p.worldScale = worldScale; p.rng = rng;
            p.bb = bb; p.owner = owner; p.sound = sound; p.typeEdNum = typeEdNum;
            p.velocity = velocity;
            p.castRadius = def.MissileRadius * worldScale;
            p.flyIdx = 0;
            p.flyLeft = remainingLife > 0f
                ? remainingLife
                : def.MissileFlyTics[0] / 35f;

            Rendering.EnhancedLightSystem.Instance?.PulseProjectile(
                from, worldScale, impact: false);

            var registry = WorldStateRegistry.Instance;
            if (registry != null)
            {
                int spawnId = forcedSpawnId ?? registry.AllocateSpawnId();
                var id = go.AddComponent<RuntimeEntityIdentity>();
                id.Init(spawnId);
                registry.RegisterSpawned(id);
            }
        }

        public ProjectileSnapshot CaptureSnapshot(int spawnId, WorldStateRegistry registry)
        {
            var ownerId = SaveEntityId.None;
            if (owner != null && registry != null)
                ownerId = registry.ResolveEntity(owner.transform);

            float life = exploding ? boomLeft : flyLeft;
            var p = transform.position;
            return new ProjectileSnapshot(
                spawnId, typeEdNum, ownerId,
                p.x, p.y, p.z,
                velocity.x, velocity.y, velocity.z,
                life);
        }

        void OnDestroy()
        {
            var id = GetComponent<RuntimeEntityIdentity>();
            if (id != null && WorldStateRegistry.Instance != null)
                WorldStateRegistry.Instance.UnregisterSpawned(id.SpawnId);
        }

        void Update()
        {
            if (exploding) { TickExplosion(); return; }

            // Полётная анимация (2 кадра циклом).
            flyLeft -= Time.deltaTime;
            if (flyLeft <= 0f)
            {
                flyIdx = (flyIdx + 1) % def.MissileFlyFrames.Length;
                bb.SetStaticFrame(def.MissileFlyFrames[flyIdx]);
                flyLeft = def.MissileFlyTics[flyIdx] / 35f;
            }

            // Движение со сферокастом по отрезку кадра.
            Vector3 delta = velocity * Time.deltaTime;
            float dist = delta.magnitude;
            if (dist > 1e-8f && Physics.SphereCast(transform.position, castRadius, delta.normalized,
                                   out var hit, dist, ~0, QueryTriggerInteraction.Ignore))
            {
                transform.position += delta.normalized * hit.distance;
                OnImpact(hit.collider);
                return;
            }
            transform.position += delta;
        }

        void OnImpact(Collider hitCollider)
        {
            sound?.PlayAt("DSFIRXPL", transform.position);

            int damage = MonsterRules.RollDamage(rng, def.MissileImpactMod, def.MissileImpactMult);

            var player = hitCollider.GetComponent<PlayerHealth>();
            var enemy = hitCollider.GetComponent<EnemyHealth>();
            if (player != null) player.TakeDamage(damage);
            else if (enemy != null && !enemy.IsDead)
                enemy.TakeDamage(damage, owner != null ? DamageSource.Monster(owner) : DamageSource.Player());

            // Presentation only — after damage attribution.
            Rendering.EnhancedLightSystem.Instance?.PulseProjectile(
                transform.position, worldScale, impact: true);
            Rendering.ParticleEffectPool.Instance?.Pulse(
                Rendering.EffectKind.Explosion, transform.position, worldScale);

            exploding = true;
            boomIdx = 0;
            bb.SetStaticFrame(def.MissileExplodeFrames[0]);
            boomLeft = def.MissileExplodeTics[0] / 35f;
            velocity = Vector3.zero;
        }

        void TickExplosion()
        {
            boomLeft -= Time.deltaTime;
            if (boomLeft > 0f) return;
            boomIdx++;
            if (boomIdx >= def.MissileExplodeFrames.Length) { Destroy(gameObject); return; }
            bb.SetStaticFrame(def.MissileExplodeFrames[boomIdx]);
            boomLeft = def.MissileExplodeTics[boomIdx] / 35f;
        }
    }
}
