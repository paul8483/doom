using System.Collections.Generic;
using UnityEngine;
using Doom.Game;

namespace Doom.MapBuild
{
    /// World adapter for BFG tracer raycasts (testable via injected delegate).
    public interface IBfgTraceWorld
    {
        /// Ordered hits along the ray (nearest first), excluding the owner.
        void RaycastAll(Vector3 origin, Vector3 direction, float maxDist, List<Collider> hits);
    }

    public sealed class PhysicsBfgTraceWorld : IBfgTraceWorld
    {
        readonly Transform ownerRoot;

        public PhysicsBfgTraceWorld(Transform ownerRoot) => this.ownerRoot = ownerRoot;

        public void RaycastAll(Vector3 origin, Vector3 direction, float maxDist, List<Collider> hits)
        {
            hits.Clear();
            var raw = Physics.RaycastAll(origin, direction, maxDist, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(raw, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var rh in raw)
            {
                if (rh.collider == null) continue;
                var t = rh.collider.transform;
                if (ownerRoot != null && (t == ownerRoot || t.IsChildOf(ownerRoot)))
                    continue;
                hits.Add(rh.collider);
            }
        }
    }

    /// Vanilla A_BFGSpray: 40 tracers from the owner origin along the saved shot fan.
    public static class BfgSprayExecutor
    {
        static readonly List<Collider> HitBuffer = new List<Collider>(16);

        public static int Execute(
            Vector3 origin,
            Vector3 shotDirection,
            float worldScale,
            DoomRandom rng,
            SpriteCache cache,
            IBfgTraceWorld world = null,
            Transform ownerRoot = null)
        {
            world ??= new PhysicsBfgTraceWorld(ownerRoot);
            if (shotDirection.sqrMagnitude < 1e-8f) shotDirection = Vector3.forward;
            shotDirection.Normalize();

            // Preserve free-aim pitch; fan spreads around world up (yaw).
            Vector3 forward = shotDirection;
            Vector3 yawAxis = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(forward, yawAxis)) > 0.999f)
                yawAxis = Vector3.right;

            var shots = new List<BfgTracerShot>(BfgRules.TracerCount);
            BfgRules.BuildTracers(rng, shots);
            float range = BfgRules.TracerRangeDoom * worldScale;
            int hits = 0;

            foreach (var shot in shots)
            {
                Vector3 dir = Quaternion.AngleAxis(shot.YawOffsetDeg, yawAxis) * forward;
                world.RaycastAll(origin, dir, range, HitBuffer);

                bool damaged = false;
                foreach (var col in HitBuffer)
                {
                    var enemy = col.GetComponentInParent<EnemyHealth>();
                    if (enemy != null)
                    {
                        if (enemy.IsDead) continue; // corpses do not absorb the ray
                        enemy.TakeDamage(shot.Damage, DamageSource.Player());
                        BfgTracerEffect.Spawn(cache, worldScale, enemy.transform.position);
                        damaged = true;
                        hits++;
                        break;
                    }

                    var player = col.GetComponentInParent<PlayerHealth>();
                    if (player != null)
                    {
                        player.TakeDamage(shot.Damage);
                        BfgTracerEffect.Spawn(cache, worldScale, player.transform.position);
                        damaged = true;
                        hits++;
                        break;
                    }

                    // Non-shootable solid (wall / prop) stops this ray.
                    break;
                }

                _ = damaged;
            }

            return hits;
        }
    }
}
