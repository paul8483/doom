using System.Collections.Generic;
using Doom.Graphics;

namespace Doom.Map
{
    public sealed class SectorMeshes
    {
        public int SectorIdx { get; }
        public MeshData Floor { get; }
        public MeshData Ceiling { get; }
        public string FloorFlat { get; }
        public string CeilingFlat { get; }
        public IReadOnlyList<WallSection> Walls { get; }

        public SectorMeshes(int sectorIdx, MeshData floor, MeshData ceiling,
                            string floorFlat, string ceilingFlat,
                            IReadOnlyList<WallSection> walls)
        {
            SectorIdx = sectorIdx;
            Floor = floor;
            Ceiling = ceiling;
            FloorFlat = floorFlat;
            CeilingFlat = ceilingFlat;
            Walls = walls;
        }

        public bool HasAnyGeometry
        {
            get
            {
                if (!Floor.IsEmpty || !Ceiling.IsEmpty) return true;
                foreach (var w in Walls) if (!w.Mesh.IsEmpty) return true;
                return false;
            }
        }
    }

    public static class MapGeometryBuilder
    {
        // Fallback sizes when no real texture source is supplied (geometry-only tests).
        private sealed class FallbackSizes : ITextureSizeSource
        {
            public bool TryGetSize(string name, out int width, out int height)
            { width = 64; height = 128; return true; }
        }

        public static SectorMeshes[] Build(MapData map, float worldScale = 1f,
                                           ITextureSizeSource sizes = null,
                                           ISectorHeights heights = null)
        {
            sizes ??= new FallbackSizes();
            heights ??= new StaticSectorHeights(map);
            var polys = SectorPolygonBuilder.Build(map);
            var result = new SectorMeshes[map.Sectors.Length];
            for (int s = 0; s < map.Sectors.Length; s++)
                result[s] = BuildOne(map, s, polys[s], heights, worldScale, sizes);
            return result;
        }

        /// Rebuild a single sector's meshes at the given runtime heights.
        public static SectorMeshes RebuildSector(MapData map, int sectorIdx, ISectorHeights heights,
                                                 float worldScale, ITextureSizeSource sizes)
        {
            sizes ??= new FallbackSizes();
            heights ??= new StaticSectorHeights(map);
            var poly = SectorPolygonBuilder.Build(map)[sectorIdx];
            return BuildOne(map, sectorIdx, poly, heights, worldScale, sizes);
        }

        /// Rebuild a single sector's meshes at the given runtime heights, reusing a
        /// pre-built polygon (skips the per-call SectorPolygonBuilder pass).
        public static SectorMeshes RebuildSector(MapData map, int sectorIdx, SectorPolygon poly,
                                                 ISectorHeights heights, float worldScale,
                                                 ITextureSizeSource sizes)
        {
            sizes ??= new FallbackSizes();
            heights ??= new StaticSectorHeights(map);
            return BuildOne(map, sectorIdx, poly, heights, worldScale, sizes);
        }

        private static SectorMeshes BuildOne(MapData map, int s, SectorPolygon poly,
                                             ISectorHeights heights, float worldScale,
                                             ITextureSizeSource sizes)
        {
            var sec = map.Sectors[s];
            var floor   = SectorTriangulator.TriangulateFloor(map, poly, heights, worldScale);
            var ceiling = SectorTriangulator.TriangulateCeiling(map, poly, heights, worldScale);
            var walls   = WallMeshBuilder.BuildForSector(map, s, sizes, worldScale, heights);
            return new SectorMeshes(s, floor, ceiling, sec.FloorFlat, sec.CeilingFlat, walls);
        }
    }
}
