using LibTessDotNet;

namespace Doom.Map
{
    public static class SectorTriangulator
    {
        public const string SkyFlat = "F_SKY1";

        public static MeshData TriangulateFloor(MapData map, SectorPolygon poly,
                                                ISectorHeights h = null, float worldScale = 1f)
        {
            h ??= new StaticSectorHeights(map);
            var sec = map.Sectors[poly.SectorIdx];
            return Triangulate(map, poly, h.FloorHeight(poly.SectorIdx) * worldScale,
                               worldScale, faceUp: true, sec.LightLevel);
        }

        public static MeshData TriangulateCeiling(MapData map, SectorPolygon poly,
                                                  ISectorHeights h = null, float worldScale = 1f)
        {
            h ??= new StaticSectorHeights(map);
            var sec = map.Sectors[poly.SectorIdx];
            // Sky ceilings are not rendered (Stage 4 defers real sky).
            if (sec.CeilingFlat == SkyFlat) return MeshData.Empty;
            return Triangulate(map, poly, h.CeilingHeight(poly.SectorIdx) * worldScale,
                               worldScale, faceUp: false, sec.LightLevel);
        }

        private static MeshData Triangulate(MapData map, SectorPolygon poly,
                                            float yHeight, float worldScale,
                                            bool faceUp, ushort lightLevel)
        {
            if (!poly.IsValid) return MeshData.Empty;

            try
            {
                var tess = new Tess { NoEmptyPolygons = true };

                AddContour(tess, map.Vertexes, poly.Outer);
                foreach (var hole in poly.Holes)
                    AddContour(tess, map.Vertexes, hole);

                tess.Tessellate(WindingRule.EvenOdd, ElementType.Polygons, 3);

                int vc = tess.VertexCount;
                int tc = tess.ElementCount;
                var verts = new Float3[vc];
                var uv = new Float2[vc];
                var colors = new Float3[vc];
                float g = lightLevel / 255f;
                for (int i = 0; i < vc; i++)
                {
                    var p = tess.Vertices[i].Position; // DOOM X,Y in p.X,p.Y
                    verts[i] = new Float3(p.X * worldScale, yHeight, p.Y * worldScale);
                    // Flats tile on a fixed 64-unit world grid.
                    uv[i] = new Float2(p.X / 64f, p.Y / 64f);
                    colors[i] = new Float3(g, g, g);
                }
                var tris = new int[tc * 3];
                for (int t = 0; t < tc; t++)
                {
                    int a = tess.Elements[t * 3 + 0];
                    int b = tess.Elements[t * 3 + 1];
                    int c = tess.Elements[t * 3 + 2];
                    // Ориентация закрепляется АБСОЛЮТНО по знаку нормали, а не
                    // относительным разворотом выдачи LibTess: LibTess выбирает
                    // нормаль проекции по СУММЕ знаковых площадей контуров, и у
                    // секторов из нескольких несвязных колец (ступени лестницы под
                    // одним номером сектора) «дырки» перевешивают outer — весь
                    // сектор выходит в обратном winding'е (невидимые полы/потолки).
                    float ny = (verts[b].Z - verts[a].Z) * (verts[c].X - verts[a].X)
                             - (verts[b].X - verts[a].X) * (verts[c].Z - verts[a].Z);
                    if (ny != 0f && (ny > 0f) != faceUp) { var tmp = b; b = c; c = tmp; }
                    tris[t * 3 + 0] = a;
                    tris[t * 3 + 1] = b;
                    tris[t * 3 + 2] = c;
                }
                return new MeshData(verts, tris, uv, colors);
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
            if (ring == null || ring.Count < 3) return;
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
