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
                    if (dmg > 0)
                        eh.TakeDamage(dmg, source);
                    continue;
                }

                var ph = c.GetComponentInParent<PlayerHealth>();
                if (ph != null && hitPlayers.Add(ph))
                {
                    float distDoom = HorizontalDistDoom(center, ph.transform.position, worldScale)
                                     - PlayerRadiusDoom;
                    int dmg = RadiusDamageRules.DamageAt(maxDamage, distDoom);
                    if (dmg > 0)
                        ph.TakeDamage(dmg);
                }
            }
        }

        static float HorizontalDistDoom(Vector3 a, Vector3 b, float worldScale)
        {
            float dx = (a.x - b.x) / worldScale;
            float dz = (a.z - b.z) / worldScale;
            // P_AproxDistance: max + min/2
            float ax = Mathf.Abs(dx);
            float az = Mathf.Abs(dz);
            return ax > az ? ax + az * 0.5f : az + ax * 0.5f;
        }

        static float CapsuleRadiusDoom(Collider col, float worldScale)
        {
            if (col is CapsuleCollider cap && worldScale > 0f)
                return cap.radius / worldScale;
            return 16f;
        }
    }
}
