namespace Doom.Map
{
    public sealed class SectorMeshes
    {
        public int SectorIdx { get; }
        public MeshData Floor { get; }
        public MeshData Ceiling { get; }
        public MeshData Walls { get; }

        public SectorMeshes(int sectorIdx, MeshData floor, MeshData ceiling, MeshData walls)
        {
            SectorIdx = sectorIdx;
            Floor = floor;
            Ceiling = ceiling;
            Walls = walls;
        }

        public bool HasAnyGeometry =>
            !Floor.IsEmpty || !Ceiling.IsEmpty || !Walls.IsEmpty;
    }

    public static class MapGeometryBuilder
    {
        public static SectorMeshes[] Build(MapData map, float worldScale = 1f)
        {
            var polys = SectorPolygonBuilder.Build(map);
            var result = new SectorMeshes[map.Sectors.Length];
            for (int s = 0; s < map.Sectors.Length; s++)
            {
                var floor   = SectorTriangulator.TriangulateFloor(map, polys[s], worldScale);
                var ceiling = SectorTriangulator.TriangulateCeiling(map, polys[s], worldScale);
                var walls   = WallMeshBuilder.BuildForSector(map, s, worldScale);
                result[s] = new SectorMeshes(s, floor, ceiling, walls);
            }
            return result;
        }
    }
}
