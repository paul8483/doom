using System.Collections.Generic;
using UnityEngine;

namespace Doom.MapBuild
{
    /// Keeps a floor-standing thing glued to its sector's floor when runtime
    /// heights change (lifts, lowering floors, stair builds). Vertical placement
    /// happens once at spawn (ThingSpawner raycast); vanilla DOOM re-clips things
    /// to the moved plane in P_ChangeSector, so without this pickups and corpses
    /// are left hanging in the air after the floor under them moves.
    public sealed class FloorAnchor : MonoBehaviour
    {
        static readonly List<FloorAnchor> Registry = new List<FloorAnchor>();

        const float Far = 10000f;

        void OnEnable() => Registry.Add(this);
        void OnDisable() => Registry.Remove(this);

        /// Called by SectorGeometry after rebuilding a sector whose floor height
        /// changed. Snaps every anchored thing standing in that sector onto the
        /// new floor. The floor child's world bounds give a cheap XZ reject
        /// before the per-thing raycast.
        public static void OnSectorFloorMoved(
            int sector, Transform floorChild, float floorWorldY)
        {
            if (Registry.Count == 0 || floorChild == null) return;
            var renderer = floorChild.GetComponent<Renderer>();
            if (renderer == null) return;
            Bounds b = renderer.bounds;
            const float Margin = 0.5f;

            for (int i = Registry.Count - 1; i >= 0; i--)
            {
                var anchor = Registry[i];
                if (anchor == null) continue;
                Vector3 p = anchor.transform.position;
                if (p.x < b.min.x - Margin || p.x > b.max.x + Margin ||
                    p.z < b.min.z - Margin || p.z > b.max.z + Margin)
                    continue;
                if (anchor.FindSectorBelow() != sector) continue;
                anchor.SetFeetY(floorWorldY);
            }
        }

        /// Post-restore pass: settle every anchored thing onto its sector's
        /// current floor. Heals saves captured before FloorAnchor existed, where
        /// things were recorded already hanging over a moved floor.
        public static void ReanchorAll(RuntimeSectorHeights heights, float worldScale)
        {
            if (heights == null) return;
            for (int i = Registry.Count - 1; i >= 0; i--)
            {
                var anchor = Registry[i];
                if (anchor == null) continue;
                int sector = anchor.FindSectorBelow();
                if (sector < 0) continue;
                anchor.SetFeetY(heights.FloorRaw(sector) * worldScale);
            }
        }

        void SetFeetY(float y)
        {
            Vector3 p = transform.position;
            if (Mathf.Approximately(p.y, y)) return;
            transform.position = new Vector3(p.x, y, p.z);
        }

        /// Same floor pick as ThingSpawner.ResolveVertical: all hits from far
        /// above, "Floor" colliders only, highest wins. Floor XZ footprints never
        /// overlap in a DOOM map and floors only ever move along Y, so the hit
        /// IDENTITY is stable even against not-yet-synced colliders; the target
        /// height comes from RuntimeSectorHeights, not from the hit point.
        int FindSectorBelow()
        {
            Vector3 p = transform.position;
            var hits = Physics.RaycastAll(
                new Vector3(p.x, p.y + Far, p.z), Vector3.down, 2f * Far);
            float bestY = float.NegativeInfinity;
            SectorRef best = null;
            foreach (var h in hits)
            {
                if (h.collider.gameObject.name != "Floor") continue;
                if (h.point.y <= bestY) continue;
                bestY = h.point.y;
                best = h.collider.GetComponent<SectorRef>();
            }
            return best != null ? best.SectorIndex : -1;
        }
    }
}
