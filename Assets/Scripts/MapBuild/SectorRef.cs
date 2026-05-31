using UnityEngine;

namespace Doom.MapBuild
{
    /// Marks a sector's floor GameObject with its sector index, so a downward
    /// raycast can resolve which sector the player is standing on (for floor damage).
    public sealed class SectorRef : MonoBehaviour
    {
        public int SectorIndex = -1;
    }
}
