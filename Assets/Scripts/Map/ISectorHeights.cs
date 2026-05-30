namespace Doom.Map
{
    /// Current floor/ceiling height (DOOM units) per sector. Lets geometry be
    /// rebuilt at runtime heights without mutating the immutable Sector[] from the WAD.
    public interface ISectorHeights
    {
        int FloorHeight(int sectorIdx);
        int CeilingHeight(int sectorIdx);
    }

    /// Default: the static heights straight from the WAD (Stage 2–5 behavior).
    public sealed class StaticSectorHeights : ISectorHeights
    {
        private readonly MapData map;
        public StaticSectorHeights(MapData map) => this.map = map;
        public int FloorHeight(int s) => map.Sectors[s].FloorHeight;
        public int CeilingHeight(int s) => map.Sectors[s].CeilingHeight;
    }
}
