using UnityEngine;
using Doom.Game;

namespace Doom.MapBuild
{
    /// Player plasma bolt: PLSS flight, direct impact only, PLSE explosion (no splash).
    public sealed class PlayerPlasmaProjectile : MonoBehaviour, IProjectileSnapshotSource
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
                * (PlasmaRules.SpeedDoomPerTic * 35f * worldScale);
            LaunchInternal(
                cache, worldScale, rng, from, velocity, ownerRoot, sound, null, -1f, 0);
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
                ownerRoot, sound, snapshot.SpawnId, snapshot.RemainingLife,
                snapshot.FrameIndex);
        }

        static void LaunchInternal(
            SpriteCache cache, float worldScale, DoomRandom rng,
            Vector3 from, Vector3 velocity, Transform ownerRoot, SoundSystem sound,
            int? forcedSpawnId, float remainingLife, int frameIndex)
        {
            var go = new GameObject("Missile_PLSS", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.position = from;
            int flyIndex = Mathf.Clamp(frameIndex, 0, PlasmaRules.FlyFrames.Length - 1);

            var bb = go.AddComponent<SpriteBillboard>();
            bb.Init(cache, PlasmaRules.Sprite, PlasmaRules.FlyFrames[flyIndex], worldScale,
                doomAngleDeg: 0f, spawnCeiling: false, ceilingY: 0f);
            bb.SetStaticFrame(PlasmaRules.FlyFrames[flyIndex]);

            var bolt = go.AddComponent<PlayerPlasmaProjectile>();
            bolt.cache = cache;
            bolt.worldScale = worldScale;
            bolt.velocity = velocity;
            bolt.rng = rng ?? new DoomRandom();
            bolt.billboard = bb;
            bolt.sound = sound;
            bolt.ownerRoot = ownerRoot;
            bolt.castRadius = PlasmaRules.RadiusDoom * worldScale;
            bolt.flyIndex = flyIndex;
            bolt.frameLeft = remainingLife > 0f
                ? remainingLife
                : PlasmaRules.FlyTics[flyIndex] / 35f;

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
            if (exploding) return null;
            var p = transform.position;
            Vector3 direction = velocity.sqrMagnitude > 1e-8f
                ? velocity.normalized : Vector3.zero;
            return new ProjectileSnapshot(
                spawnId, PlasmaRules.SnapshotType, SaveEntityId.None,
                p.x, p.y, p.z,
                velocity.x, velocity.y, velocity.z,
                frameLeft,
                ProjectilePhase.Flying, flyIndex,
                direction.x, direction.y, direction.z,
                false);
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
                flyIndex = (flyIndex + 1) % PlasmaRules.FlyFrames.Length;
                billboard.SetStaticFrame(PlasmaRules.FlyFrames[flyIndex]);
                frameLeft = PlasmaRules.FlyTics[flyIndex] / 35f;
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
            if (exploding) return;
            int direct = PlasmaRules.RollDirectDamage(rng);
            var enemy = hitCollider != null ? hitCollider.GetComponentInParent<EnemyHealth>() : null;
            var player = hitCollider != null ? hitCollider.GetComponentInParent<PlayerHealth>() : null;
            if (enemy != null && !enemy.IsDead)
                enemy.TakeDamage(direct, DamageSource.Player());
            else if (player != null)
                player.TakeDamage(direct);

            sound?.PlayAt(PlasmaRules.ExplodeSound, transform.position);

            exploding = true;
            explodeIndex = 0;
            velocity = Vector3.zero;
            billboard.SetSprite(PlasmaRules.ExplodeSprite, PlasmaRules.ExplodeFrames[0]);
            frameLeft = PlasmaRules.ExplodeTics[0] / 35f;
        }

        void TickExplosion()
        {
            frameLeft -= Time.deltaTime;
            if (frameLeft > 0f) return;
            explodeIndex++;
            if (explodeIndex >= PlasmaRules.ExplodeFrames.Length)
            {
                Destroy(gameObject);
                return;
            }
            billboard.SetStaticFrame(PlasmaRules.ExplodeFrames[explodeIndex]);
            frameLeft = PlasmaRules.ExplodeTics[explodeIndex] / 35f;
        }
    }
}
