using LibTessDotNet;

namespace Doom.Map
{
    public static class SectorTriangulator
    {
        public static MeshData TriangulateFloor(MapData map, SectorPolygon poly)
            => Triangulate(map, poly, map.Sectors[poly.SectorIdx].FloorHeight, flipWinding: true);

        public static MeshData TriangulateCeiling(MapData map, SectorPolygon poly)
            => Triangulate(map, poly, map.Sectors[poly.SectorIdx].CeilingHeight, flipWinding: false);

        private static MeshData Triangulate(MapData map, SectorPolygon poly,
                                            float yHeight, bool flipWinding)
        {
            if (!poly.IsValid) return MeshData.Empty;

            try
            {
                var tess = new Tess();

                AddContour(tess, map.Vertexes, poly.Outer);
                foreach (var hole in poly.Holes)
                    AddContour(tess, map.Vertexes, hole);

                tess.Tessellate(WindingRule.EvenOdd, ElementType.Polygons, 3);

                int vc = tess.VertexCount;
                int tc = tess.ElementCount;
                var verts = new Float3[vc];
                for (int i = 0; i < vc; i++)
                {
                    var p = tess.Vertices[i].Position;
                    // DOOM (X, Y) -> Unity (X, Z), Y = height
                    verts[i] = new Float3(p.X, yHeight, p.Y);
                }
                var tris = new int[tc * 3];
                for (int t = 0; t < tc; t++)
                {
                    int a = tess.Elements[t * 3 + 0];
                    int b = tess.Elements[t * 3 + 1];
                    int c = tess.Elements[t * 3 + 2];
                    if (flipWinding) { var tmp = a; a = c; c = tmp; }
                    tris[t * 3 + 0] = a;
                    tris[t * 3 + 1] = b;
                    tris[t * 3 + 2] = c;
                }
                return new MeshData(verts, tris);
            }
            catch (System.Exception ex)
            {
                MapLog.Error($"SectorTriangulator: sector {poly.SectorIdx} tess failed: {ex.Message}");
                return MeshData.Empty;
            }
        }

        private static void AddContour(Tess tess, Vertex[] mapVerts,
                                       System.Collections.Generic.IReadOnlyList<int> ring)
        {
            var arr = new ContourVertex[ring.Count];
            for (int i = 0; i < ring.Count; i++)
            {
                var v = mapVerts[ring[i]];
                arr[i].Position = new Vec3 { X = v.X, Y = v.Y, Z = 0 };
            }
            tess.AddContour(arr);
        }
    }
}
