using UnityEngine;
using Doom.Game;

namespace Doom.MapBuild
{
    /// Unity glue for <see cref="RadiusDamageRules"/> — OverlapSphere + TakeDamage.
    public static class RadiusDamageExecutor
    {
        const float PlayerRadiusDoom = 16f;

        public static void ApplyBarrelBlast(
            Vector3 center, float worldScale, Transform self, DamageSource source)
        {
            float radius = RadiusDamageRules.BarrelRadiusDoom * worldScale;
            var cols = Physics.OverlapSphere(
                center, radius, ~0, QueryTriggerInteraction.Ignore);
            foreach (var c in cols)
            {
                if (c == null) continue;
                if (self != null && (c.transform == self || c.transform.IsChildOf(self)))
                    continue;

                var eh = c.GetComponentInParent<EnemyHealth>();
                if (eh != null && !eh.IsDead)
                {
                    float targetRadiusDoom = CapsuleRadiusDoom(c, worldScale);
                    float distDoom = HorizontalDistDoom(center, eh.transform.position, worldScale)
                                     - targetRadiusDoom;
                    int dmg = RadiusDamageRules.BarrelDamageAt(distDoom);
                    if (dmg > 0)
                        eh.TakeDamage(dmg, source);
                    continue;
                }

                var ph = c.GetComponentInParent<PlayerHealth>();
                if (ph != null)
                {
                    float distDoom = HorizontalDistDoom(center, ph.transform.position, worldScale)
                                     - PlayerRadiusDoom;
                    int dmg = RadiusDamageRules.BarrelDamageAt(distDoom);
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
