using UnityEngine;
using Doom.Game;

namespace Doom.MapBuild
{
    /// Player rocket: straight MISL flight, direct impact damage and A_Explode splash.
    public sealed class PlayerRocketProjectile : MonoBehaviour, IProjectileSnapshotSource
    {
        SpriteCache cache;
        float worldScale;
        Vector3 velocity;
        DoomRandom rng;
        SpriteBillboard billboard;
        SoundSystem sound;
        Transform ownerRoot;
        float castRadius;
        int flyIndex;
        float frameLeft;
        bool exploding;
        int explodeIndex;

        public static void Launch(
            SpriteCache cache, float worldScale, DoomRandom rng,
            Vector3 from, Vector3 direction, Transform ownerRoot,
            SoundSystem sound = null)
        {
            if (direction.sqrMagnitude < 1e-8f) direction = Vector3.forward;
            Vector3 velocity = direction.normalized
                * (RocketRules.SpeedDoomPerTic * 35f * worldScale);
            LaunchInternal(
                cache, worldScale, rng, from, velocity, ownerRoot, sound, null, -1f);
        }

        public static void LaunchFromSnapshot(
            SpriteCache cache, float worldScale, DoomRandom rng,
            ProjectileSnapshot snapshot, Transform ownerRoot, SoundSystem sound = null)
        {
            if (snapshot == null || cache == null) return;
            LaunchInternal(
                cache, worldScale, rng,
                new Vector3(snapshot.X, snapshot.Y, snapshot.Z),
                new Vector3(snapshot.VelX, snapshot.VelY, snapshot.VelZ),
                ownerRoot, sound, snapshot.SpawnId, snapshot.RemainingLife);
        }

        static void LaunchInternal(
            SpriteCache cache, float worldScale, DoomRandom rng,
            Vector3 from, Vector3 velocity, Transform ownerRoot, SoundSystem sound,
            int? forcedSpawnId, float remainingLife)
        {
            var go = new GameObject("Missile_MISL", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.position = from;

            var bb = go.AddComponent<SpriteBillboard>();
            bb.Init(cache, RocketRules.Sprite, RocketRules.FlyFrames[0], worldScale,
                doomAngleDeg: 0f, spawnCeiling: false, ceilingY: 0f);
            bb.SetStaticFrame(RocketRules.FlyFrames[0]);

            var rocket = go.AddComponent<PlayerRocketProjectile>();
            rocket.cache = cache;
            rocket.worldScale = worldScale;
            rocket.velocity = velocity;
            rocket.rng = rng ?? new DoomRandom();
            rocket.billboard = bb;
            rocket.sound = sound;
            rocket.ownerRoot = ownerRoot;
            rocket.castRadius = RocketRules.RadiusDoom * worldScale;
            rocket.frameLeft = remainingLife > 0f
                ? remainingLife
                : RocketRules.FlyTics[0] / 35f;

            var registry = WorldStateRegistry.Instance;
            if (registry != null)
            {
                int spawnId = forcedSpawnId ?? registry.AllocateSpawnId();
                var identity = go.AddComponent<RuntimeEntityIdentity>();
                identity.Init(spawnId);
                registry.RegisterSpawned(identity);
            }
        }

        public ProjectileSnapshot CaptureSnapshot(int spawnId, WorldStateRegistry registry)
        {
            // Impact damage has already been applied. The remaining explosion is
            // visual-only, and ProjectileSnapshot has no animation-phase fields.
            if (exploding) return null;
            var p = transform.position;
            return new ProjectileSnapshot(
                spawnId, RocketRules.SnapshotType, SaveEntityId.None,
                p.x, p.y, p.z,
                velocity.x, velocity.y, velocity.z,
                frameLeft);
        }

        void OnDestroy()
        {
            var identity = GetComponent<RuntimeEntityIdentity>();
            if (identity != null && WorldStateRegistry.Instance != null)
                WorldStateRegistry.Instance.UnregisterSpawned(identity.SpawnId);
        }

        void Update()
        {
            if (exploding)
            {
                TickExplosion();
                return;
            }

            frameLeft -= Time.deltaTime;
            if (frameLeft <= 0f)
            {
                flyIndex = (flyIndex + 1) % RocketRules.FlyFrames.Length;
                billboard.SetStaticFrame(RocketRules.FlyFrames[flyIndex]);
                frameLeft = RocketRules.FlyTics[flyIndex] / 35f;
            }

            Vector3 delta = velocity * Time.deltaTime;
            float distance = delta.magnitude;
            if (distance > 1e-8f
                && TryCast(delta.normalized, distance, out var hit))
            {
                transform.position += delta.normalized * hit.distance;
                Impact(hit.collider);
                return;
            }
            transform.position += delta;
        }

        bool TryCast(Vector3 direction, float distance, out RaycastHit closest)
        {
            closest = default;
            float best = float.PositiveInfinity;
            bool found = false;
            foreach (var hit in Physics.SphereCastAll(
                         transform.position, castRadius, direction, distance,
                         ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider == null) continue;
                var t = hit.collider.transform;
                if (ownerRoot != null && (t == ownerRoot || t.IsChildOf(ownerRoot)))
                    continue;
                if (hit.distance >= best) continue;
                best = hit.distance;
                closest = hit;
                found = true;
            }
            return found;
        }

        void Impact(Collider hitCollider)
        {
            int direct = MonsterRules.RollDamage(
                rng, RocketRules.DirectDamageMod, RocketRules.DirectDamageMult);
            var enemy = hitCollider != null ? hitCollider.GetComponentInParent<EnemyHealth>() : null;
            var player = hitCollider != null ? hitCollider.GetComponentInParent<PlayerHealth>() : null;
            if (enemy != null && !enemy.IsDead)
                enemy.TakeDamage(direct, DamageSource.Player());
            else if (player != null)
                player.TakeDamage(direct);

            sound?.PlayAt(RocketRules.ExplodeSound, transform.position);
            RadiusDamageExecutor.ApplyBlast(
                transform.position, worldScale, null, DamageSource.Player(),
                RocketRules.SplashDamage, RocketRules.SplashRadiusDoom);

            exploding = true;
            explodeIndex = 0;
            velocity = Vector3.zero;
            billboard.SetStaticFrame(RocketRules.ExplodeFrames[0]);
            frameLeft = RocketRules.ExplodeTics[0] / 35f;
        }

        void TickExplosion()
        {
            frameLeft -= Time.deltaTime;
            if (frameLeft > 0f) return;
            explodeIndex++;
            if (explodeIndex >= RocketRules.ExplodeFrames.Length)
            {
                Destroy(gameObject);
                return;
            }
            billboard.SetStaticFrame(RocketRules.ExplodeFrames[explodeIndex]);
            frameLeft = RocketRules.ExplodeTics[explodeIndex] / 35f;
        }
    }
}
