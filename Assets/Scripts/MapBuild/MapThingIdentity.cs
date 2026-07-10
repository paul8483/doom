using UnityEngine;

namespace Doom.MapBuild
{
    /// Stable identity for a map THINGS entry. Index is the source lump index.
    public sealed class MapThingIdentity : MonoBehaviour
    {
        public int MapThingIndex { get; private set; } = -1;
        public int DoomEdNum { get; private set; }
        public int MapFlags { get; private set; }

        public void Init(int mapThingIndex, int doomEdNum, int mapFlags)
        {
            if (mapThingIndex < 0)
                throw new System.ArgumentOutOfRangeException(nameof(mapThingIndex));
            MapThingIndex = mapThingIndex;
            DoomEdNum = doomEdNum;
            MapFlags = mapFlags;
        }
    }
}
