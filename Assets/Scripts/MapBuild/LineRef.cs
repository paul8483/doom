using UnityEngine;

namespace Doom.MapBuild
{
    /// Tags a wall GameObject with the sector it belongs to so the Use-raycast can
    /// resolve the underlying linedef. Wall meshes are texture-grouped (one collider
    /// can span several linedefs of the same sector+texture), so a single LineIndex
    /// per GameObject is not generally exact; LineActivator narrows by SectorIndex
    /// and then picks the linedef segment nearest the raycast hit point.
    ///
    /// Attached in MapLoader's shared PopulateSectorRoot path, so it is re-created
    /// automatically whenever C2's rebuild recreates a sector's wall GameObjects.
    public sealed class LineRef : MonoBehaviour
    {
        /// Sector index this wall belongs to (-1 if unknown).
        public int SectorIndex = -1;

        /// Optional exact linedef index when a wall maps 1:1 (-1 = resolve by geometry).
        public int LineIndex = -1;
    }
}
