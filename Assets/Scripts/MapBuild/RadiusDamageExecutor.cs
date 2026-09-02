using UnityEngine;
using Doom.Game;
using System.Collections.Generic;

namespace Doom.MapBuild
{
    /// Unity glue for <see cref="RadiusDamageRules"/> — OverlapSphere + TakeDamage.
    public static class RadiusDamageExecutor
    {
        const float PlayerRadiusDoom = 16f;

        public static void ApplyBarrelBlast(
            Vector3 center, float worldScale, Transform self, DamageSource source)
            => ApplyBlast(center, worldScale, self, source,
                RadiusDamageRules.BarrelMaxDamage, RadiusDamageRules.BarrelRadiusDoom);

        public static void ApplyBlast(
            Vector3 center, float worldScale, Transform self, DamageSource source,
            int maxDamage, float radiusDoom)
        {
            float radius = radiusDoom * worldScale;
            var cols = Physics.OverlapSphere(
                center, radius, ~0, QueryTriggerInteraction.Ignore);
            var hitEnemies = new HashSet<EnemyHealth>();
            var hitPlayers = new HashSet<PlayerHealth>();
            foreach (var c in cols)
            {
                if (c == null) continue;
                if (self != null && (c.transform == self || c.transform.IsChildOf(self)))
                    continue;

                var eh = c.GetComponentInParent<EnemyHealth>();
                if (eh != null && !eh.IsDead && hitEnemies.Add(eh))
                {
                    float targetRadiusDoom = CapsuleRadiusDoom(c, worldScale);
                    float distDoom = HorizontalDistDoom(center, eh.transform.position, worldScale)
                                     - targetRadiusDoom;
                    int dmg = RadiusDamageRules.DamageAt(maxDamage, distDoom);
                    if (dmg > 0 && HasLineOfSight(center, c))
                        eh.TakeDamage(dmg, source);
                    continue;
                }

                var ph = c.GetComponentInParent<PlayerHealth>();
                if (ph != null && hitPlayers.Add(ph))
                {
                    float distDoom = HorizontalDistDoom(center, ph.transform.position, worldScale)
                                     - PlayerRadiusDoom;
                    int dmg = RadiusDamageRules.DamageAt(maxDamage, distDoom);
                    if (dmg > 0 && HasLineOfSight(center, c))
                        ph.TakeDamage(dmg);
                }
            }
        }

        /// PIT_RadiusAttack: `dist = max(|dx|, |dy|)` (Chebyshev), NOT
        /// P_AproxDistance — a diagonal target is closer in blast terms than
        /// the approximation says (dx = dy = 60 → 60, not 90).
        static float HorizontalDistDoom(Vector3 a, Vector3 b, float worldScale)
        {
            float dx = Mathf.Abs(a.x - b.x) / worldScale;
            float dz = Mathf.Abs(a.z - b.z) / worldScale;
            return Mathf.Max(dx, dz);
        }

        static readonly RaycastHit[] sightHits = new RaycastHit[32];

        /// PIT_RadiusAttack applies damage only when `P_CheckSight(thing,
        /// bombspot)` passes: level geometry (walls, floors) between the blast
        /// and the target blocks the splash; other things do not.
        static bool HasLineOfSight(Vector3 from, Collider target)
        {
            Vector3 to = target.bounds.center;
            Vector3 delta = to - from;
            float dist = delta.magnitude;
            if (dist <= 1e-4f) return true;
            int count = Physics.RaycastNonAlloc(
                from, delta / dist, sightHits, dist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var col = sightHits[i].collider;
                if (col == null || col == target) continue;
                if (IsLevelGeometry(col)) return false;
            }
            return true;
        }

        static bool IsLevelGeometry(Collider col) =>
            col.GetComponentInParent<LineRef>() != null ||
            col.GetComponentInParent<SectorRef>() != null;

        static float CapsuleRadiusDoom(Collider col, float worldScale)
        {
            if (col is CapsuleCollider cap && worldScale > 0f)
                return cap.radius / worldScale;
            return 16f;
        }
    }
}
