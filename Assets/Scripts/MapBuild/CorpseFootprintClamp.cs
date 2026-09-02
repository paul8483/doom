using System;
using UnityEngine;
using Doom.Map;

namespace Doom.MapBuild
{
    /// Unity side of <see cref="SectorFootprintClamp"/>: resolves the sector
    /// under a thing the same way FloorAnchor does (highest "Floor" collider
    /// under the origin) and converts the presentation pivot's footprint to
    /// DOOM units. Returns the world-space XZ shift the pivot should take so
    /// a lying corpse mesh stays on its own sector's floor. Zero when there is
    /// no live map (unit tests with synthetic roots) or no floor below.
    public static class CorpseFootprintClamp
    {
        /// Half a flat tile: far enough for the widest corpse (BOSSO0 at
        /// 90 px needs 45 at most), close enough that the visual never leaves
        /// the neighbourhood of the collision/save origin.
        public const float MaxShiftDoomUnits = 32f;

        /// Test seam: map used instead of the live registry's.
        public static MapData MapOverrideForTest;

        /// <param name="worldPos">Thing origin (feet) in world space.</param>
        /// <param name="yawDeg">Unity yaw of the mesh pivot (rotation about Y).</param>
        /// <param name="halfXMeters">Half extent of the mesh along its local X.</param>
        /// <param name="halfZMeters">Half extent of the mesh along its local Z.</param>
        public static Vector3 Resolve(
            Vector3 worldPos, float yawDeg,
            float halfXMeters, float halfZMeters, float worldScale)
        {
            MapData map = MapOverrideForTest ?? WorldStateRegistry.Instance?.Map;
            if (map == null || worldScale <= 0f) return Vector3.zero;
            int sector = FloorAnchor.FindSectorBelow(worldPos);
            if (sector < 0) return Vector3.zero;

            // Unity's local X after a yaw of θ about Y is (cos θ, 0, −sin θ);
            // DOOM y is Unity z, so the map-space X axis is (cos θ, −sin θ).
            double rad = yawDeg * Math.PI / 180.0;
            double ax = Math.Cos(rad), ay = -Math.Sin(rad);

            if (!SectorFootprintClamp.TryClamp(
                    map, sector,
                    worldPos.x / worldScale, worldPos.z / worldScale,
                    ax, ay,
                    halfXMeters / worldScale, halfZMeters / worldScale,
                    MaxShiftDoomUnits,
                    out double dx, out double dy))
                return Vector3.zero;

            return new Vector3((float)dx * worldScale, 0f, (float)dy * worldScale);
        }
    }
}
