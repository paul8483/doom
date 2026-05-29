using NUnit.Framework;

namespace Doom.Map.Tests
{
    public class SectorPolygonBuilderTests
    {
        /// квадрат 64×64, CCW, 1 sector
        private static MapData SquareRoom()
        {
            var verts = new[]
            {
                new Vertex(0, 0), new Vertex(64, 0),
                new Vertex(64, 64), new Vertex(0, 64),
            };
            // 4 linedef'а по периметру, front = 0,1,2,3, back = -1
            var lines = new[]
            {
                new LineDef(0, 1, 0, 0, 0, 0, -1),
                new LineDef(1, 2, 0, 0, 0, 1, -1),
                new LineDef(2, 3, 0, 0, 0, 2, -1),
                new LineDef(3, 0, 0, 0, 0, 3, -1),
            };
            var sides = new[]
            {
                new SideDef(0, 0, "-", "-", "W", 0),
                new SideDef(0, 0, "-", "-", "W", 0),
                new SideDef(0, 0, "-", "-", "W", 0),
                new SideDef(0, 0, "-", "-", "W", 0),
            };
            var sectors = new[] { new Sector(0, 128, "F", "F", 0, 0, 0) };
            return new MapData("TEST", verts, lines, sides, sectors);
        }

        [Test]
        public void Square_room_yields_one_outer_contour_of_4_vertices()
        {
            var polys = SectorPolygonBuilder.Build(SquareRoom());

            Assert.That(polys.Length, Is.EqualTo(1));
            Assert.That(polys[0].IsValid, Is.True);
            Assert.That(polys[0].Outer.Count, Is.EqualTo(4));
            Assert.That(polys[0].Holes.Count, Is.EqualTo(0));
        }

        [Test]
        public void Square_room_outer_contour_is_CCW_in_doom_xy()
        {
            var polys = SectorPolygonBuilder.Build(SquareRoom());
            // Сектор слева от front-стороны → CCW
            // Vertex order должен идти CCW в DOOM-овой XY (signed area > 0)
            double area = SignedArea(SquareRoom().Vertexes, polys[0].Outer);
            Assert.That(area, Is.GreaterThan(0));
        }

        [Test]
        public void Two_adjacent_rooms_share_one_twoSided_linedef()
        {
            // (0,0)-(64,0)-(128,0)-(128,64)-(64,64)-(0,64), 2 sector'а слева/справа
            // Общая линия (64,0)→(64,64) — twoSided
            var verts = new[]
            {
                new Vertex(0, 0), new Vertex(64, 0), new Vertex(128, 0),
                new Vertex(128, 64), new Vertex(64, 64), new Vertex(0, 64),
            };
            // sector 0 = левая комната, sector 1 = правая
            // SideDef[0..3] = sector 0 walls (CCW: 0→1, 1→4, 4→5, 5→0)
            // SideDef[4..7] = sector 1 walls (CCW: 1→2, 2→3, 3→4, 4→1)
            // SideDef[8] = front (sector 1) shared, SideDef[9] = back (sector 0) shared
            var sides = new[]
            {
                new SideDef(0,0,"-","-","W",0), new SideDef(0,0,"-","-","W",0),
                new SideDef(0,0,"-","-","W",0), new SideDef(0,0,"-","-","W",0),
                new SideDef(0,0,"-","-","W",1), new SideDef(0,0,"-","-","W",1),
                new SideDef(0,0,"-","-","W",1), new SideDef(0,0,"-","-","W",1),
                new SideDef(0,0,"-","-","W",1), new SideDef(0,0,"-","-","W",0),
            };
            var lines = new[]
            {
                // sector 0 boundary (front sidedef = 0..3, back = -1)
                new LineDef(0, 1, 0, 0, 0, 0, -1),
                new LineDef(4, 5, 0, 0, 0, 2, -1),
                new LineDef(5, 0, 0, 0, 0, 3, -1),
                // sector 1 boundary
                new LineDef(1, 2, 0, 0, 0, 4, -1),
                new LineDef(2, 3, 0, 0, 0, 5, -1),
                new LineDef(3, 4, 0, 0, 0, 6, -1),
                // shared linedef — V1=1, V2=4, front (right) = sector 1, back (left) = sector 0
                new LineDef(1, 4, 0, 0, 0, 8, 9),
            };
            var sectors = new[]
            {
                new Sector(0, 128, "F", "F", 0, 0, 0),
                new Sector(0, 128, "F", "F", 0, 0, 0),
            };
            var map = new MapData("TEST", verts, lines, sides, sectors);

            var polys = SectorPolygonBuilder.Build(map);

            Assert.That(polys.Length, Is.EqualTo(2));
            Assert.That(polys[0].IsValid, Is.True);
            Assert.That(polys[1].IsValid, Is.True);
            Assert.That(polys[0].Outer.Count, Is.EqualTo(4));
            Assert.That(polys[1].Outer.Count, Is.EqualTo(4));
        }

        [Test]
        public void Sector_with_pillar_has_outer_and_one_hole()
        {
            // Внешняя 128×128 комната + центральная 32×32 колонна (отдельный sector с потолок=пол=0 — «закрыт»)
            // Для теста полигон-билдера достаточно того, что у внешнего сектора будет
            // один outer + один hole контур.
            var verts = new[]
            {
                // outer (0..3)
                new Vertex(0, 0), new Vertex(128, 0),
                new Vertex(128, 128), new Vertex(0, 128),
                // inner pillar (4..7) — CCW в его собственной плоскости,
                // но в outer-секторе они обходятся CW (это «дырка»)
                new Vertex(48, 48), new Vertex(80, 48),
                new Vertex(80, 80), new Vertex(48, 80),
            };
            // 4 sidedefs для outer (sector 0), 4 для pillar's front (sector 1), 4 для pillar's back (sector 0)
            var sides = new[]
            {
                // outer walls — sector 0
                new SideDef(0,0,"-","-","W",0), new SideDef(0,0,"-","-","W",0),
                new SideDef(0,0,"-","-","W",0), new SideDef(0,0,"-","-","W",0),
                // pillar front (внутрь pillar'а смотрит): sector 1
                new SideDef(0,0,"-","-","W",1), new SideDef(0,0,"-","-","W",1),
                new SideDef(0,0,"-","-","W",1), new SideDef(0,0,"-","-","W",1),
                // pillar back (наружу, к sector 0): sector 0
                new SideDef(0,0,"-","-","W",0), new SideDef(0,0,"-","-","W",0),
                new SideDef(0,0,"-","-","W",0), new SideDef(0,0,"-","-","W",0),
            };
            var lines = new[]
            {
                // outer (CCW)
                new LineDef(0, 1, 0, 0, 0, 0, -1),
                new LineDef(1, 2, 0, 0, 0, 1, -1),
                new LineDef(2, 3, 0, 0, 0, 2, -1),
                new LineDef(3, 0, 0, 0, 0, 3, -1),
                // pillar — twoSided. V1→V2 идёт CW для outer (то есть CCW для pillar's interior).
                // front = sector 1 (pillar inside), back = sector 0 (outer room)
                new LineDef(4, 5, 0, 0, 0, 4, 8),
                new LineDef(5, 6, 0, 0, 0, 5, 9),
                new LineDef(6, 7, 0, 0, 0, 6, 10),
                new LineDef(7, 4, 0, 0, 0, 7, 11),
            };
            var sectors = new[]
            {
                new Sector(0, 128, "F", "F", 0, 0, 0),
                new Sector(0, 0, "F", "F", 0, 0, 0),  // pillar — потолок и пол совпадают
            };
            var map = new MapData("TEST", verts, lines, sides, sectors);

            var polys = SectorPolygonBuilder.Build(map);

            Assert.That(polys.Length, Is.EqualTo(2));
            // sector 0: outer (внешняя комната) + hole (контур колонны)
            Assert.That(polys[0].Outer.Count, Is.EqualTo(4));
            Assert.That(polys[0].Holes.Count, Is.EqualTo(1));
            Assert.That(polys[0].Holes[0].Count, Is.EqualTo(4));
            // sector 1: только outer (pillar)
            Assert.That(polys[1].Outer.Count, Is.EqualTo(4));
            Assert.That(polys[1].Holes.Count, Is.EqualTo(0));
        }

        [Test]
        public void Open_contour_is_reported_as_invalid_and_logged()
        {
            // Три linedef'а: 0→1, 1→2 (2→0 отсутствует, контур не замыкается)
            var verts = new[]
            {
                new Vertex(0, 0), new Vertex(64, 0), new Vertex(64, 64),
            };
            var lines = new[]
            {
                new LineDef(0, 1, 0, 0, 0, 0, -1),
                new LineDef(1, 2, 0, 0, 0, 1, -1),
            };
            var sides = new[]
            {
                new SideDef(0,0,"-","-","W",0), new SideDef(0,0,"-","-","W",0),
            };
            var sectors = new[] { new Sector(0, 128, "F", "F", 0, 0, 0) };
            var map = new MapData("TEST", verts, lines, sides, sectors);

            string warning = null;
            System.Action<string> handler = m => warning = m;
            Doom.Map.MapLog.WarningHandler += handler;
            try
            {
                var polys = SectorPolygonBuilder.Build(map);
                Assert.That(polys[0].IsValid, Is.False);
            }
            finally { Doom.Map.MapLog.WarningHandler -= handler; }

            Assert.That(warning, Is.Not.Null);
            StringAssert.Contains("sector 0", warning.ToLowerInvariant());
        }

        // ---- helpers ----
        private static double SignedArea(Vertex[] verts, System.Collections.Generic.IReadOnlyList<int> ring)
        {
            double a = 0;
            for (int i = 0; i < ring.Count; i++)
            {
                var p0 = verts[ring[i]];
                var p1 = verts[ring[(i + 1) % ring.Count]];
                a += (double)p0.X * p1.Y - (double)p1.X * p0.Y;
            }
            return 0.5 * a;
        }
    }
}
