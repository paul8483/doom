using UnityEngine;

namespace Doom.MapBuild
{
    /// Carries the player with a moving sector floor (lifts/raising floors). Each
    /// LateUpdate, if the player stands on a sector whose floor MOVED this frame,
    /// snaps the player's feet to the floor surface. The snap is absolute (to the
    /// raycast hit point), so it can't double-count the CharacterController's own
    /// depenetration regardless of update order. On static floors / normal walking
    /// (floor height unchanged) it does nothing.
    public sealed class PlayerLiftRider : MonoBehaviour
    {
        CharacterController cc;
        float worldScale;
        int lastSector = -1;
        float lastFloorY;

        public void Init(CharacterController cc, float worldScale)
        {
            this.cc = cc;
            this.worldScale = worldScale;
            lastSector = -1;
        }

        /// Test hook: the sector the rider currently tracks under the player (-1 if none).
        public int CurrentSectorForTest => lastSector;

        void LateUpdate()
        {
            if (cc == null) return;
            if (!TryFloorUnderPlayer(out int sector, out float floorY))
            {
                lastSector = -1;
                return;
            }

            // Only carry while THIS sector's floor is actively moving (height changed
            // since last frame). New sector or static floor → just track, no snap.
            if (sector == lastSector && !Mathf.Approximately(floorY, lastFloorY))
            {
                float feetY = transform.position.y;       // CC bottom = player feet
                float delta = floorY - feetY;             // close the gap to the floor
                if (Mathf.Abs(delta) > 1e-5f)
                    cc.Move(Vector3.up * delta);
            }

            lastSector = sector;
            lastFloorY = floorY;
        }

        bool TryFloorUnderPlayer(out int sector, out float floorY)
        {
            sector = -1; floorY = 0f;
            Vector3 origin = transform.position + Vector3.up * (16f * worldScale);
            float range = 48f * worldScale;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, RaycastScratch.Hits,
                                                range, ~0, QueryTriggerInteraction.Ignore);
            float bestDist = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                var h = RaycastScratch.Hits[i];
                if (h.collider == (Collider)(object)cc) continue;   // skip the player capsule
                var sref = h.collider.GetComponentInParent<SectorRef>();
                if (sref == null || sref.SectorIndex < 0) continue;
                if (h.distance < bestDist)
                {
                    bestDist = h.distance;
                    sector = sref.SectorIndex;
                    floorY = h.point.y;
                    found = true;
                }
            }
            return found;
        }
    }
}
