using UnityEngine;
using Doom.Game;

namespace Doom.MapBuild
{
    /// Player BFG ball: BFS1 flight, direct impact, delayed A_BFGSpray on BFE1 frame C.
    public sealed class PlayerBfgProjectile : MonoBehaviour, IProjectileSnapshotSource
    {
        SpriteCache cache;
        float worldScale;
        Vector3 velocity;
        Vector3 shotDirection;
        DoomRandom rng;
        SpriteBillboard billboard;
        SoundSystem sound;
        Transform ownerRoot;
        float castRadius;
        int flyIndex;
        float frameLeft;
        ProjectilePhase phase = ProjectilePhase.Flying;
        int explodeIndex;
        bool sprayApplied;
        float impactElapsed;

        public static void Launch(
            SpriteCache cache, float worldScale, DoomRandom rng,
            Vector3 from, Vector3 direction, Transform ownerRoot,
            SoundSystem sound = null)
        {
            if (direction.sqrMagnitude < 1e-8f) direction = Vector3.forward;
            direction = direction.normalized;
            Vector3 velocity = direction * (BfgRules.SpeedDoomPerTic * 35f * worldScale);
            LaunchInternal(
                cache, worldScale, rng, from, velocity, direction, ownerRoot, sound,
                null, -1f, ProjectilePhase.Flying, 0, false, 0f);
        }

        public static void LaunchFromSnapshot(
            SpriteCache cache, float worldScale, DoomRandom rng,
            ProjectileSnapshot snapshot, Transform ownerRoot, SoundSystem sound = null)
        {
            if (snapshot == null || cache == null) return;
            var vel = new Vector3(snapshot.VelX, snapshot.VelY, snapshot.VelZ);
            var savedDir = new Vector3(
                snapshot.ShotDirX, snapshot.ShotDirY, snapshot.ShotDirZ);
            Vector3 dir = savedDir.sqrMagnitude > 1e-8f
                ? savedDir.normalized
                : (vel.sqrMagnitude > 1e-8f ? vel.normalized : Vector3.forward);
            float impactElapsed = snapshot.Phase == ProjectilePhase.Exploding
                ? ElapsedExplosionTime(snapshot.FrameIndex, snapshot.RemainingLife)
                : 0f;
            LaunchInternal(
                cache, worldScale, rng,
                new Vector3(snapshot.X, snapshot.Y, snapshot.Z),
                vel, dir, ownerRoot, sound,
                snapshot.SpawnId, snapshot.RemainingLife,
                snapshot.Phase, snapshot.FrameIndex, snapshot.SprayApplied, impactElapsed);
        }

        static void LaunchInternal(
            SpriteCache cache, float worldScale, DoomRandom rng,
            Vector3 from, Vector3 velocity, Vector3 shotDirection, Transform ownerRoot,
            SoundSystem sound, int? forcedSpawnId, float remainingLife,
            ProjectilePhase phase, int frameIndex, bool sprayApplied, float impactElapsed)
        {
            var go = new GameObject("Missile_BFS1", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.position = from;

            int flyIndex = Mathf.Clamp(frameIndex, 0, BfgRules.FlyFrames.Length - 1);
            int explodeIndex = Mathf.Clamp(frameIndex, 0, BfgRules.ExplodeFrames.Length - 1);
            string sprite = phase == ProjectilePhase.Flying
                ? BfgRules.Sprite : BfgRules.ExplodeSprite;
            int frame = phase == ProjectilePhase.Flying
                ? BfgRules.FlyFrames[flyIndex]
                : BfgRules.ExplodeFrames[explodeIndex];

            var bb = go.AddComponent<SpriteBillboard>();
            bb.Init(cache, sprite, frame, worldScale,
                doomAngleDeg: 0f, spawnCeiling: false, ceilingY: 0f);
            bb.SetStaticFrame(frame);

            var ball = go.AddComponent<PlayerBfgProjectile>();
            ball.cache = cache;
            ball.worldScale = worldScale;
            ball.velocity = velocity;
            ball.shotDirection = shotDirection.sqrMagnitude > 1e-8f
                ? shotDirection.normalized : Vector3.forward;
            ball.rng = rng ?? new DoomRandom();
            ball.billboard = bb;
            ball.sound = sound;
            ball.ownerRoot = ownerRoot;
            ball.castRadius = BfgRules.RadiusDoom * worldScale;
            ball.phase = phase;
            ball.flyIndex = flyIndex;
            ball.explodeIndex = explodeIndex;
            ball.sprayApplied = sprayApplied;
            ball.impactElapsed = impactElapsed;
            ball.frameLeft = remainingLife > 0f
                ? remainingLife
                : (phase == ProjectilePhase.Flying
                    ? BfgRules.FlyTics[flyIndex]
                    : BfgRules.ExplodeTics[explodeIndex])
                  / 35f;

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
            var p = transform.position;
            int frameIndex = phase == ProjectilePhase.Flying ? flyIndex : explodeIndex;
            return new ProjectileSnapshot(
                spawnId, BfgRules.SnapshotType, SaveEntityId.None,
                p.x, p.y, p.z,
                velocity.x, velocity.y, velocity.z,
                frameLeft,
                phase, frameIndex,
                shotDirection.x, shotDirection.y, shotDirection.z,
                sprayApplied);
        }

        void OnDestroy()
        {
            var identity = GetComponent<RuntimeEntityIdentity>();
            if (identity != null && WorldStateRegistry.Instance != null)
                WorldStateRegistry.Instance.UnregisterSpawned(identity.SpawnId);
        }

        void Update()
        {
            if (phase == ProjectilePhase.Exploding)
            {
                TickExplosion();
                return;
            }

            frameLeft -= Time.deltaTime;
            if (frameLeft <= 0f)
            {
                flyIndex = (flyIndex + 1) % BfgRules.FlyFrames.Length;
                billboard.SetStaticFrame(BfgRules.FlyFrames[flyIndex]);
                frameLeft = BfgRules.FlyTics[flyIndex] / 35f;
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
            if (phase != ProjectilePhase.Flying) return;
            int direct = BfgRules.RollDirectDamage(rng);
            var enemy = hitCollider != null ? hitCollider.GetComponentInParent<EnemyHealth>() : null;
            var player = hitCollider != null ? hitCollider.GetComponentInParent<PlayerHealth>() : null;
            if (enemy != null && !enemy.IsDead)
                enemy.TakeDamage(direct, DamageSource.Player());
            else if (player != null)
                player.TakeDamage(direct);

            sound?.PlayAt(BfgRules.ExplodeSound, transform.position);

            phase = ProjectilePhase.Exploding;
            explodeIndex = 0;
            impactElapsed = 0f;
            sprayApplied = false;
            velocity = Vector3.zero;
            billboard.SetSprite(BfgRules.ExplodeSprite, BfgRules.ExplodeFrames[0]);
            frameLeft = BfgRules.ExplodeTics[0] / 35f;
        }

        void TickExplosion()
        {
            float dt = Time.deltaTime;
            impactElapsed += dt;
            frameLeft -= dt;

            if (!sprayApplied
                && impactElapsed >= BfgRules.SprayAfterImpactTics / 35f)
            {
                ApplySpray();
            }

            if (frameLeft > 0f) return;
            explodeIndex++;
            if (explodeIndex >= BfgRules.ExplodeFrames.Length)
            {
                Destroy(gameObject);
                return;
            }
            billboard.SetStaticFrame(BfgRules.ExplodeFrames[explodeIndex]);
            frameLeft = BfgRules.ExplodeTics[explodeIndex] / 35f;
        }

        void ApplySpray()
        {
            if (sprayApplied) return;
            sprayApplied = true;
            Vector3 origin = ownerRoot != null ? ownerRoot.position : transform.position;
            var cam = ownerRoot != null ? ownerRoot.GetComponentInChildren<Camera>() : null;
            if (cam != null) origin = cam.transform.position;

            BfgSprayExecutor.Execute(
                origin, shotDirection, worldScale, rng, cache, ownerRoot: ownerRoot);
        }

        static float ElapsedExplosionTime(int frameIndex, float remainingLife)
        {
            int index = Mathf.Clamp(frameIndex, 0, BfgRules.ExplodeTics.Length - 1);
            int elapsedTics = 0;
            for (int i = 0; i < index; i++)
                elapsedTics += BfgRules.ExplodeTics[i];
            float currentDuration = BfgRules.ExplodeTics[index] / 35f;
            return elapsedTics / 35f + Mathf.Max(0f, currentDuration - remainingLife);
        }
    }
}
