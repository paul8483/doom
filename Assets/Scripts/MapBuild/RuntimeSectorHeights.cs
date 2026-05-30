using Doom.Map;

namespace Doom.MapBuild
{
    /// Mutable per-sector heights for runtime movers. Interpolates in float, but
    /// exposes rounded ints so the geometry builder stays unit-quantized (DOOM
    /// moves sectors in integer units; rebuilds are skipped when the rounded
    /// height doesn't change — see SectorGeometry).
    public sealed class RuntimeSectorHeights : ISectorHeights
    {
        private readonly float[] floor;
        private readonly float[] ceil;

        public RuntimeSectorHeights(MapData map)
        {
            floor = new float[map.Sectors.Length];
            ceil = new float[map.Sectors.Length];
            for (int s = 0; s < map.Sectors.Length; s++)
            {
                floor[s] = map.Sectors[s].FloorHeight;
                ceil[s] = map.Sectors[s].CeilingHeight;
            }
        }

        public int FloorHeight(int s) => UnityEngine.Mathf.RoundToInt(floor[s]);
        public int CeilingHeight(int s) => UnityEngine.Mathf.RoundToInt(ceil[s]);

        public float FloorRaw(int s) => floor[s];
        public float CeilRaw(int s) => ceil[s];
        public void SetFloor(int s, float v) => floor[s] = v;
        public void SetCeil(int s, float v) => ceil[s] = v;
    }
}
