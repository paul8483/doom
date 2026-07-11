using UnityEngine;
using Doom.Map;
using Doom.Specials;

namespace Doom.MapBuild
{
    /// Applies a successful TeleportRules selection to a player or monster body.
    public static class TeleportExecutor
    {
        const float TelefragDamage = 10000f;
        const float OccupancyRadiusDoom = 16f;

        public static bool TryTeleport(
            MapData map,
            TeleportLanding landing,
            Transform body,
            float worldScale,
            CharacterController playerCc = null,
            PlayerController playerLook = null,
            SoundSystem sound = null)
        {
            if (map == null || body == null) return false;

            float destX = landing.X * worldScale;
            float destZ = landing.Y * worldScale;
            float feetY = ResolveFloorY(destX, destZ, body.position.y);

            TelefragAt(destX, feetY, destZ, OccupancyRadiusDoom * worldScale, body);

            Vector3 dest = new Vector3(destX, feetY, destZ);
            if (playerCc != null)
            {
                playerCc.enabled = false;
                body.position = dest;
                playerCc.enabled = true;
            }
            else
            {
                body.position = dest;
            }

            float yaw = 90f - landing.Angle;
            body.rotation = Quaternion.Euler(0f, yaw, 0f);
            playerLook?.SetView(yaw, 0f);
            var bb = body.GetComponent<SpriteBillboard>();
            bb?.SetDoomAngle(landing.Angle);

            sound?.PlayAt("DSTELEPT", dest);
            return true;
        }

        /// Highest "Floor" MeshCollider under (x,z), or <paramref name="fallbackY"/>.
        /// Shared with player spawn so level starts do not drop from map-sky height.
        public static float ResolveFloorY(float x, float z, float fallbackY)
        {
            const float Far = 10000f;
            var hits = Physics.RaycastAll(
                new Vector3(x, fallbackY + Far, z), Vector3.down, 2f * Far,
                ~0, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            bool found = false;
            foreach (var h in hits)
            {
                if (h.collider.gameObject.name != "Floor") continue;
                if (h.point.y > best) { best = h.point.y; found = true; }
            }
            return found ? best : fallbackY;
        }

        static void TelefragAt(float x, float y, float z, float radius, Transform self)
        {
            var cols = Physics.OverlapSphere(
                new Vector3(x, y + radius, z), radius,
                ~0, QueryTriggerInteraction.Ignore);
            foreach (var c in cols)
            {
                if (c == null) continue;
                if (self != null && (c.transform == self || c.transform.IsChildOf(self)))
                    continue;

                var eh = c.GetComponentInParent<EnemyHealth>();
                if (eh != null && !eh.IsDead)
                {
                    eh.TakeDamage(Mathf.RoundToInt(TelefragDamage), DamageSource.Player());
                    continue;
                }

                var ph = c.GetComponentInParent<PlayerHealth>();
                if (ph != null)
                    ph.TakeDamage(Mathf.RoundToInt(TelefragDamage));
            }
        }
    }
}
