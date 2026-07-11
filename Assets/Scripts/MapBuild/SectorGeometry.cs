using UnityEngine;
using Unity.Profiling;
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
        static readonly ProfilerMarker RebuildMarker = new("Doom.SectorGeometry.Rebuild");
        static readonly ProfilerMarker BuildWallsMarker = new("Doom.SectorGeometry.BuildWallData");
        static readonly ProfilerMarker ApplyWallsMarker = new("Doom.SectorGeometry.ApplyWallMeshes");

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

        public Transform GetSectorRoot(int sector)
        {
            if (sector < 0 || sector >= sectorRoots.Length) return null;
            return sectorRoots[sector];
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
            using (RebuildMarker.Auto())
            {
                var root = sectorRoots[s];
                if (root == null) return;

                // Floor and ceiling meshes don't change SHAPE when a sector moves, only
                // their Y. Translate their GameObjects so the floor MeshCollider persists.
                var floorChild = root.Find("Floor");
                if (floorChild != null)
                    floorChild.localPosition = new Vector3(
                        0f, (heights.FloorRaw(s) - map.Sectors[s].FloorHeight) * worldScale, 0f);

                var ceilChild = root.Find("Ceiling");
                if (ceilChild != null)
                    ceilChild.localPosition = new Vector3(
                        0f, (heights.CeilRaw(s) - map.Sectors[s].CeilingHeight) * worldScale, 0f);

                SectorMeshes sm;
                using (BuildWallsMarker.Auto())
                    sm = MapGeometryBuilder.RebuildSector(
                        map, s, polys[s], heights, worldScale, sizes);
                using (ApplyWallsMarker.Auto())
                    MapLoader.RebuildSectorWalls(root, sm, textures, worldScale);
            }
        }
    }
}
