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
                                           ITextureSizeSource sizes = null)
        {
            sizes ??= new FallbackSizes();
            var polys = SectorPolygonBuilder.Build(map);
            var result = new SectorMeshes[map.Sectors.Length];
            for (int s = 0; s < map.Sectors.Length; s++)
            {
                var sec = map.Sectors[s];
                var floor   = SectorTriangulator.TriangulateFloor(map, polys[s], worldScale);
                var ceiling = SectorTriangulator.TriangulateCeiling(map, polys[s], worldScale);
                var walls   = WallMeshBuilder.BuildForSector(map, s, sizes, worldScale);
                result[s] = new SectorMeshes(s, floor, ceiling,
                                             sec.FloorFlat, sec.CeilingFlat, walls);
            }
            return result;
        }
    }
}
