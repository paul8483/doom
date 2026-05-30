using UnityEngine;
using Doom.Map;
using Doom.Graphics;
using Doom.Specials;

namespace Doom.MapBuild
{
    /// Tracks each sector's floor/ceiling/wall GameObjects and rebuilds them in
    /// place when runtime heights change. Rebuild reuses MapLoader's shared mesh
    /// construction so rebuilt GameObjects match the initial build exactly.
    public sealed class SectorGeometry
    {
        private readonly MapData map;
        private readonly SectorPolygon[] polys;
        private readonly RuntimeSectorHeights heights;
        private readonly float worldScale;
        private readonly TextureCache textures;
        private readonly ITextureSizeSource sizes;
        private readonly Transform[] sectorRoots;
        private readonly int[] lastFloor, lastCeil;

        public SectorGeometry(MapData map, SectorPolygon[] polys, RuntimeSectorHeights heights,
                              float worldScale, TextureCache textures, ITextureSizeSource sizes,
                              Transform[] sectorRoots)
        {
            this.map = map; this.polys = polys; this.heights = heights;
            this.worldScale = worldScale; this.textures = textures; this.sizes = sizes;
            this.sectorRoots = sectorRoots;
            lastFloor = new int[map.Sectors.Length];
            lastCeil = new int[map.Sectors.Length];
            for (int s = 0; s < map.Sectors.Length; s++)
            { lastFloor[s] = heights.FloorHeight(s); lastCeil[s] = heights.CeilingHeight(s); }
        }

        /// Rebuild sector s and every neighbor sharing a line (wall heights depend
        /// on both sides). The per-frame skip is applied by the CALLER (SectorMover
        /// only calls this when the moving sector's rounded height changed).
        public void RebuildSectorAndNeighbors(int s)
        {
            RebuildIfChanged(s);
            foreach (var n in Neighbors.OfSector(map, s)) RebuildIfChanged(n);
        }

        private void RebuildIfChanged(int s)
        {
            int f = heights.FloorHeight(s), c = heights.CeilingHeight(s);
            // Always rebuild when asked; the per-frame skip lives in the caller.
            // lastFloor/lastCeil are retained for a future finer-grained skip.
            Rebuild(s);
            lastFloor[s] = f; lastCeil[s] = c;
        }

        private void Rebuild(int s)
        {
            var root = sectorRoots[s];
            if (root == null) return;
            var sm = MapGeometryBuilder.RebuildSector(map, s, polys[s], heights, worldScale, sizes);
            MapLoader.RebuildSectorGameObjects(root, sm, textures, worldScale);
        }
    }
}
