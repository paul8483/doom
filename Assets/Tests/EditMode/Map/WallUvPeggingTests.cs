using System.Collections.Generic;
using NUnit.Framework;

namespace Doom.Map.Tests
{
    public class WallUvPeggingTests
    {
        private sealed class Sizes : Doom.Graphics.ITextureSizeSource
        {
            private readonly Dictionary<string, (int w, int h)> map = new();
            public Sizes Add(string n, int w, int h) { map[n] = (w, h); return this; }
            public bool TryGetSize(string name, out int width, out int height)
            {
                if (name != null && map.TryGetValue(name, out var s)) { width = s.w; height = s.h; return true; }
                width = 64; height = 128; return true;
            }
        }

        private static WallSection First(IReadOnlyList<WallSection> s) => s[0];

        [Test]
        public void OneSided_u_spans_wall_length_over_texture_width()
        {
            // Wall length 128, texture width 64 -> u runs 0..2 along the wall.
            var verts = new[] { new Vertex(0, 0), new Vertex(128, 0) };
            var lines = new[] { new LineDef(0, 1, 0, 0, 0, 0, -1) };
            var sides = new[] { new SideDef(0,0,"-","-","W",0) };
            var sectors = new[] { new Sector(0, 128, "F", "F", 255, 0, 0) };
            var map = new MapData("T", verts, lines, sides, sectors, System.Array.Empty<Thing>());

            var sec = First(WallMeshBuilder.BuildForSector(map, 0, new Sizes().Add("W", 64, 128)));
            float maxU = 0f, minU = 1e9f;
            foreach (var uv in sec.Mesh.Uv) { if (uv.X > maxU) maxU = uv.X; if (uv.X < minU) minU = uv.X; }
            Assert.That(minU, Is.EqualTo(0f).Within(0.001f));
            Assert.That(maxU, Is.EqualTo(2f).Within(0.001f));
        }

        [Test]
        public void OneSided_default_is_top_pegged_v_one_at_ceiling()
        {
            // height 128, texHeight 128. Default (no unpegged): texture top at ceiling.
            // DOOM считает v сверху вниз, но Unity-текстура (после переворота строк в
            // TextureCache) хранит верх изображения на v=1 — поэтому у потолка v=1,
            // у пола v=0. Инвертированный v рисовал ВСЕ стены вверх ногами (AGM-лого
            // на SHAWN1 читалось перевёрнутым).
            var verts = new[] { new Vertex(0, 0), new Vertex(64, 0) };
            var lines = new[] { new LineDef(0, 1, 0, 0, 0, 0, -1) };
            var sides = new[] { new SideDef(0,0,"-","-","W",0) };
            var sectors = new[] { new Sector(0, 128, "F", "F", 255, 0, 0) };
            var map = new MapData("T", verts, lines, sides, sectors, System.Array.Empty<Thing>());

            var sec = First(WallMeshBuilder.BuildForSector(map, 0, new Sizes().Add("W", 64, 128)));
            // Find v at the highest and lowest vertex.
            float vAtTop = 0, vAtBottom = 0; float topY = -1e9f, botY = 1e9f;
            for (int i = 0; i < sec.Mesh.Vertices.Length; i++)
            {
                float y = sec.Mesh.Vertices[i].Y;
                if (y > topY) { topY = y; vAtTop = sec.Mesh.Uv[i].Y; }
                if (y < botY) { botY = y; vAtBottom = sec.Mesh.Uv[i].Y; }
            }
            Assert.That(vAtTop, Is.EqualTo(1f).Within(0.001f), "texture top (Unity v=1) at ceiling");
            Assert.That(vAtBottom, Is.EqualTo(0f).Within(0.001f), "texture bottom (Unity v=0) one tile down");
        }

        [Test]
        public void OneSided_lower_unpegged_shifts_v_so_bottom_sits_at_floor()
        {
            // Same wall but Lower-unpegged (flag 0x0008). With height==texHeight==128
            // the difference from default is zero, so use height 64, texHeight 128
            // (Unity-v: 1 - doomV):
            // default top-pegged: bottom v = 1 - 64/128 = 0.5
            // lower-unpegged: bottom v = 1 - 1.0 = 0.0 (texture bottom pinned to floor)
            var verts = new[] { new Vertex(0, 0), new Vertex(64, 0) };
            var linesDefault = new[] { new LineDef(0, 1, 0, 0, 0, 0, -1) };
            var linesUnpeg   = new[] { new LineDef(0, 1, 0x0008, 0, 0, 0, -1) };
            var sides = new[] { new SideDef(0,0,"-","-","W",0) };
            var sectors = new[] { new Sector(0, 64, "F", "F", 255, 0, 0) };
            var sizes = new Sizes().Add("W", 64, 128);

            var def = First(WallMeshBuilder.BuildForSector(
                new MapData("T", verts, linesDefault, sides, sectors, System.Array.Empty<Thing>()), 0, sizes));
            var unp = First(WallMeshBuilder.BuildForSector(
                new MapData("T", verts, linesUnpeg, sides, sectors, System.Array.Empty<Thing>()), 0, sizes));

            float defBottom = BottomV(def), unpBottom = BottomV(unp);
            Assert.That(defBottom, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(unpBottom, Is.EqualTo(0.0f).Within(0.001f));
        }

        [Test]
        public void TwoSided_middle_with_texture_emits_masked_section_in_gap()
        {
            // Both sectors floor 0 ceil 128, middle texture "GRATE" on front side.
            // Gap = max(floor)..min(ceiling) = 0..128. Section is masked (cutout).
            var verts = new[] { new Vertex(0, 0), new Vertex(64, 0) };
            var lines = new[] { new LineDef(0, 1, 0, 0, 0, 0, 1) };
            var sides = new[]
            {
                new SideDef(0,0,"-","-","GRATE",0),
                new SideDef(0,0,"-","-","-",1),
            };
            var sectors = new[]
            {
                new Sector(0, 128, "F", "F", 255, 0, 0),
                new Sector(0, 128, "F", "F", 255, 0, 0),
            };
            var map = new MapData("T", verts, lines, sides, sectors, System.Array.Empty<Thing>());

            var secs = WallMeshBuilder.BuildForSector(map, 0, new Sizes().Add("GRATE", 64, 128));
            WallSection grate = null;
            foreach (var s in secs) if (s.Texture == "GRATE") grate = s;
            Assert.That(grate, Is.Not.Null, "middle grate section must exist");
            Assert.That(grate.Masked, Is.True);
            Assert.That(grate.Blocks, Is.False, "no ML_BLOCKING → walk-through");
            Assert.That(grate.Mesh.Triangles.Length, Is.EqualTo(6)); // one quad
            float top = -1e9f, bot = 1e9f;
            foreach (var v in grate.Mesh.Vertices) { if (v.Y > top) top = v.Y; if (v.Y < bot) bot = v.Y; }
            Assert.That(top, Is.EqualTo(128f).Within(0.001f));
            Assert.That(bot, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void TwoSided_middle_with_ML_BLOCKING_marks_Blocks()
        {
            var verts = new[] { new Vertex(0, 0), new Vertex(64, 0) };
            var lines = new[] { new LineDef(0, 1, WallSection.FlagBlocking, 0, 0, 0, 1) };
            var sides = new[]
            {
                new SideDef(0,0,"-","-","GRATE",0),
                new SideDef(0,0,"-","-","-",1),
            };
            var sectors = new[]
            {
                new Sector(0, 128, "F", "F", 255, 0, 0),
                new Sector(0, 128, "F", "F", 255, 0, 0),
            };
            var map = new MapData("T", verts, lines, sides, sectors, System.Array.Empty<Thing>());

            var secs = WallMeshBuilder.BuildForSector(map, 0, new Sizes().Add("GRATE", 64, 128));
            WallSection grate = null;
            foreach (var s in secs) if (s.Texture == "GRATE") grate = s;
            Assert.That(grate, Is.Not.Null);
            Assert.That(grate.Masked, Is.True);
            Assert.That(grate.Blocks, Is.True);
        }

        [Test]
        public void Back_side_u_runs_from_V2_toward_V1()
        {
            // DOOM рисует back-сайд как seg V2→V1: колонка текстуры u=0 (+offset) у
            // V2 и растёт к V1. Если оставить u по направлению V1→V2, зритель из
            // back-сектора видит текстуру зеркально (надписи задом наперёд).
            // Линия (0,0)→(64,0), back-сектор 1 (север, пол ниже) владеет lower-квадом.
            var verts = new[] { new Vertex(0, 0), new Vertex(64, 0) };
            var lines = new[] { new LineDef(0, 1, 0, 0, 0, 0, 1) };
            var sides = new[]
            {
                new SideDef(0,0,"-","-","-",0),
                new SideDef(0,0,"-","LOW","-",1),
            };
            var sectors = new[]
            {
                new Sector(32, 128, "F", "F", 255, 0, 0),
                new Sector(0,  128, "F", "F", 255, 0, 0),
            };
            var map = new MapData("T", verts, lines, sides, sectors, System.Array.Empty<Thing>());

            var sec = First(WallMeshBuilder.BuildForSector(map, 1, new Sizes().Add("LOW", 64, 128)));
            // u у V2 (x=64) = 0, у V1 (x=0) = 64/64 = 1.
            for (int i = 0; i < sec.Mesh.Vertices.Length; i++)
            {
                float x = sec.Mesh.Vertices[i].X;
                float u = sec.Mesh.Uv[i].X;
                if (x > 63f) Assert.That(u, Is.EqualTo(0f).Within(0.001f), "u=0 у V2");
                if (x < 1f)  Assert.That(u, Is.EqualTo(1f).Within(0.001f), "u=1 у V1");
            }
        }

        private static float BottomV(WallSection s)
        {
            float botY = 1e9f, v = 0;
            for (int i = 0; i < s.Mesh.Vertices.Length; i++)
                if (s.Mesh.Vertices[i].Y < botY) { botY = s.Mesh.Vertices[i].Y; v = s.Mesh.Uv[i].Y; }
            return v;
        }
    }
}
