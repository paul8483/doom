using UnityEngine;

namespace Doom.MapBuild
{
    /// "Is something standing in this sector that a moving plane would
    /// squash?" — the T_MovePlane blocked-move test for doors, floors and
    /// lifts (crushers use CrusherDamageSystem, which also deals damage).
    /// Actors: the player (CharacterController) and every live monster
    /// (MonsterController.Active with an enabled capsule). "Over the sector"
    /// is the same floor-collider probe the crusher uses.
    public static class SectorOccupancy
    {
        static Transform player;

        /// True when a live actor over <paramref name="sector"/> is taller
        /// than <paramref name="clearanceDoom"/> (DOOM units).
        public static bool HasActorTallerThan(SectorGeometry geometry, int sector, float clearanceDoom)
        {
            if (geometry == null) return false;
            var root = geometry.GetSectorRoot(sector);
            var floor = root != null ? root.Find("Floor")?.GetComponent<Collider>() : null;
            if (floor == null) return false;
            float worldScale = geometry.WorldScale;
            float clearance = clearanceDoom * worldScale;

            if (player == null)
            {
                var go = GameObject.Find("Player");
                player = go != null ? go.transform : null;
            }
            if (player != null)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null && cc.enabled &&
                    cc.bounds.size.y > clearance && IsOver(floor, cc.bounds.center, worldScale))
                    return true;
            }

            var monsters = MonsterController.Active;
            for (int i = 0; i < monsters.Count; i++)
            {
                var mc = monsters[i];
                if (mc == null) continue;
                var col = mc.GetComponent<CapsuleCollider>();
                if (col == null || !col.enabled) continue;
                if (col.bounds.size.y > clearance && IsOver(floor, col.bounds.center, worldScale))
                    return true;
            }
            return false;
        }

        static bool IsOver(Collider floor, Vector3 point, float worldScale)
        {
            var ray = new Ray(new Vector3(point.x, point.y + 8192f * worldScale, point.z), Vector3.down);
            return floor.Raycast(ray, out _, 16384f * worldScale);
        }
    }
}
