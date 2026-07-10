using System.Collections.Generic;
using Doom.Graphics;

namespace Doom.Map
{
    /// Builds wall geometry for a sector, grouped into one WallSection per texture
    /// (so the glue assigns one material each), with UV (pegging-aware) and
    /// per-vertex sector light. Two-sided middle textures become masked sections.
    public static class WallMeshBuilder
    {
        private const ushort FlagLowerUnpegged = 0x0008;
        private const ushort FlagUpperUnpegged = 0x0010;

        // Accumulates geometry per (texture, masked) bucket.
        private sealed class Bucket
        {
            public readonly List<Float3> V = new();
            public readonly List<int> T = new();
            public readonly List<Float2> Uv = new();
            public readonly List<Float3> C = new();
        }

        public static IReadOnlyList<WallSection> BuildForSector(
            MapData map, int sectorIdx, ITextureSizeSource sizes, float worldScale = 1f,
            ISectorHeights h = null)
        {
            h ??= new StaticSectorHeights(map);
            var opaque = new Dictionary<string, Bucket>();
            // Masked middles split by ML_BLOCKING so passable curtains stay walk-through
            // while grates/fences get colliders.
            var maskedBlocking = new Dictionary<string, Bucket>();
            var maskedPassable = new Dictionary<string, Bucket>();
            var sec = map.Sectors[sectorIdx];
            float light = sec.LightLevel / 255f;
            int secFloor = h.FloorHeight(sectorIdx);
            int secCeil = h.CeilingHeight(sectorIdx);

            for (int i = 0; i < map.LineDefs.Length; i++)
            {
                var ld = map.LineDefs[i];
                if (!IsValidVertex(map, ld.V1) || !IsValidVertex(map, ld.V2)) continue;

                bool onFront = ld.FrontSideIdx >= 0 && ld.FrontSideIdx < map.SideDefs.Length &&
                               map.SideDefs[ld.FrontSideIdx].SectorIdx == sectorIdx;
                bool onBack  = ld.BackSideIdx >= 0 && ld.BackSideIdx < map.SideDefs.Length &&
                               map.SideDefs[ld.BackSideIdx].SectorIdx == sectorIdx;
                if (!onFront && !onBack) continue;

                int sideIdx = onFront ? ld.FrontSideIdx : ld.BackSideIdx;
                var side = map.SideDefs[sideIdx];
                var v1 = map.Vertexes[ld.V1];
                var v2 = map.Vertexes[ld.V2];

                if (!ld.IsTwoSided)
                {
                    // One-sided: middle texture spans floor..ceiling.
                    if (onFront)
                        EmitQuad(opaque, null, sizes, ld.Flags, side, light, worldScale,
                                 v1, v2, secFloor, secCeil,
                                 side.MiddleTexture, WallPart.OneSidedMiddle,
                                 secFloor, secCeil, facingFront: true, isMasked: false);
                    continue;
                }

                // Two-sided: find neighbour.
                int otherSec = -1;
                if (onFront && ld.BackSideIdx >= 0 && ld.BackSideIdx < map.SideDefs.Length)
                    otherSec = map.SideDefs[ld.BackSideIdx].SectorIdx;
                else if (onBack && ld.FrontSideIdx >= 0 && ld.FrontSideIdx < map.SideDefs.Length)
                    otherSec = map.SideDefs[ld.FrontSideIdx].SectorIdx;
                if (otherSec < 0 || otherSec >= map.Sectors.Length) continue;
                int otherFloor = h.FloorHeight(otherSec);
                int otherCeil = h.CeilingHeight(otherSec);

                // Lower step: neighbour floor higher than ours.
                if (otherFloor > secFloor && HasTex(side.LowerTexture))
                    EmitQuad(opaque, null, sizes, ld.Flags, side, light, worldScale,
                             v1, v2, secFloor, otherFloor,
                             side.LowerTexture, WallPart.Lower,
                             secFloor, secCeil, facingFront: onFront, isMasked: false);

                // Upper step: neighbour ceiling lower than ours.
                if (otherCeil < secCeil && HasTex(side.UpperTexture))
                    EmitQuad(opaque, null, sizes, ld.Flags, side, light, worldScale,
                             v1, v2, otherCeil, secCeil,
                             side.UpperTexture, WallPart.Upper,
                             secFloor, secCeil, facingFront: onFront, isMasked: false);

                // Middle (grating): clipped to the shared gap, not vertically tiled.
                if (HasTex(side.MiddleTexture))
                {
                    int gapLow = System.Math.Max(secFloor, otherFloor);
                    int gapHigh = System.Math.Min(secCeil, otherCeil);
                    if (gapHigh > gapLow)
                    {
                        bool blocks = (ld.Flags & WallSection.FlagBlocking) != 0;
                        var maskedBucket = blocks ? maskedBlocking : maskedPassable;
                        EmitQuad(opaque, maskedBucket, sizes, ld.Flags, side, light, worldScale,
                                 v1, v2, gapLow, gapHigh,
                                 side.MiddleTexture, WallPart.TwoSidedMiddle,
                                 gapLow, gapHigh, facingFront: onFront, isMasked: true);
                    }
                }
            }

            var result = new List<WallSection>();
            foreach (var kv in opaque)
                result.Add(ToSection(kv.Key, masked: false, blocks: true, kv.Value));
            foreach (var kv in maskedBlocking)
                result.Add(ToSection(kv.Key, masked: true, blocks: true, kv.Value));
            foreach (var kv in maskedPassable)
                result.Add(ToSection(kv.Key, masked: true, blocks: false, kv.Value));
            return result;
        }

        private enum WallPart { OneSidedMiddle, Upper, Lower, TwoSidedMiddle }

        private static WallSection ToSection(string tex, bool masked, bool blocks, Bucket b)
            => new WallSection(tex, masked,
                   new MeshData(b.V.ToArray(), b.T.ToArray(), b.Uv.ToArray(), b.C.ToArray()),
                   blocks);

        private static bool HasTex(string t) => !string.IsNullOrEmpty(t) && t != "-";

        private static void EmitQuad(
            Dictionary<string, Bucket> opaque, Dictionary<string, Bucket> masked,
            ITextureSizeSource sizes, ushort flags, SideDef side, float light, float worldScale,
            Vertex a, Vertex b, int yLowDoom, int yHighDoom,
            string texture, WallPart part, int regionLowDoom, int regionHighDoom,
            bool facingFront, bool isMasked)
        {
            float dx = (b.X - a.X), dz = (b.Y - a.Y);
            float lenDoom = (float)System.Math.Sqrt(dx * dx + dz * dz);
            if (System.Math.Abs(yHighDoom - yLowDoom) < 1e-4f || lenDoom < 1e-4f) return; // degenerate

            sizes.TryGetSize(texture, out int texW, out int texH);
            if (texW <= 0) texW = 64;
            if (texH <= 0) texH = 128;

            // Horizontal U at the two endpoints (DOOM units), with X offset.
            float u0 = (0f + side.TextureXOffset) / texW;
            float u1 = (lenDoom + side.TextureXOffset) / texW;

            // Vertical: choose the DOOM Y that the texture's TOP row maps to.
            float texTopY = VerticalTopY(part, flags, yLowDoom, yHighDoom,
                                         regionLowDoom, regionHighDoom, texH);
            texTopY += side.TextureYOffset; // positive Y offset shifts texture down in DOOM space

            // DOOM-класс v считается СВЕРХУ текстуры: vDoom = (texTopY - y)/texH.
            // Unity-текстура (после переворота строк в TextureCache) хранит верх
            // изображения на v=1, поэтому конвертируем v = 1 - vDoom (аффинно —
            // вертикальный тайлинг через Repeat-wrap сохраняется). Без инверсии все
            // стены рисовались вверх ногами.
            float vLow = 1f - (texTopY - yLowDoom) / texH;
            float vHigh = 1f - (texTopY - yHighDoom) / texH;

            float yLow = yLowDoom * worldScale;
            float yHigh = yHighDoom * worldScale;
            float ax = a.X * worldScale, az = a.Y * worldScale;
            float bx = b.X * worldScale, bz = b.Y * worldScale;

            var target = isMasked ? masked : opaque;
            if (target == null) return;
            var bucket = GetBucket(target, texture);
            int baseIdx = bucket.V.Count;

            // Winding mirrors the Stage 2 convention (front sees CCW from +normal).
            if (facingFront)
            {
                bucket.V.Add(new Float3(bx, yLow, bz));  bucket.Uv.Add(new Float2(u1, vLow));
                bucket.V.Add(new Float3(ax, yLow, az));  bucket.Uv.Add(new Float2(u0, vLow));
                bucket.V.Add(new Float3(ax, yHigh, az)); bucket.Uv.Add(new Float2(u0, vHigh));
                bucket.V.Add(new Float3(bx, yHigh, bz)); bucket.Uv.Add(new Float2(u1, vHigh));
            }
            else
            {
                // Back-сайд рисуется как seg V2→V1: u=0(+offset) у b(V2), растёт к
                // a(V1) — иначе текстура зеркальна для зрителя из back-сектора.
                bucket.V.Add(new Float3(ax, yLow, az));  bucket.Uv.Add(new Float2(u1, vLow));
                bucket.V.Add(new Float3(bx, yLow, bz));  bucket.Uv.Add(new Float2(u0, vLow));
                bucket.V.Add(new Float3(bx, yHigh, bz)); bucket.Uv.Add(new Float2(u0, vHigh));
                bucket.V.Add(new Float3(ax, yHigh, az)); bucket.Uv.Add(new Float2(u1, vHigh));
            }
            for (int k = 0; k < 4; k++) bucket.C.Add(new Float3(light, light, light));
            // Порядок индексов (0,1,2),(0,2,3): Cross(p1-p0,p2-p0) смотрит В сектор-
            // владелец квада (Unity рисует сторону, куда указывает этот cross).
            // Прежний порядок (0,2,1),(0,3,2) выворачивал ВСЕ стены наизнанку: квад
            // был виден только с противоположной стороны линии, изнутри своего
            // сектора отсекался back-face culling'ом (стены-призраки, зеркальные
            // текстуры, синие дыры в открытых зонах E1M1).
            bucket.T.Add(baseIdx + 0); bucket.T.Add(baseIdx + 1); bucket.T.Add(baseIdx + 2);
            bucket.T.Add(baseIdx + 0); bucket.T.Add(baseIdx + 2); bucket.T.Add(baseIdx + 3);
        }

        /// DOOM Y that the texture's top row aligns to, per part + pegging flags.
        private static float VerticalTopY(WallPart part, ushort flags,
                                          int yLow, int yHigh, int regionLow, int regionHigh, int texH)
        {
            bool lowerUnpegged = (flags & FlagLowerUnpegged) != 0;
            bool upperUnpegged = (flags & FlagUpperUnpegged) != 0;
            switch (part)
            {
                case WallPart.OneSidedMiddle:
                    // default top-pegged at ceiling; lower-unpegged pins bottom to floor.
                    return lowerUnpegged ? (yLow + texH) : yHigh;
                case WallPart.Upper:
                    // default: texture top at upper section top (yHigh);
                    // upper-unpegged also pins to the top here (DOOM aligns upper to ceiling).
                    return upperUnpegged ? yHigh : (yLow + texH);
                case WallPart.Lower:
                    // default top-pegged at the step top (yHigh = neighbour floor);
                    // lower-unpegged continues from the sector ceiling (regionHigh).
                    return lowerUnpegged ? regionHigh : yHigh;
                case WallPart.TwoSidedMiddle:
                default:
                    // not tiled: texture top at the gap top.
                    return yHigh;
            }
        }

        private static Bucket GetBucket(Dictionary<string, Bucket> map, string tex)
        {
            if (!map.TryGetValue(tex, out var b)) { b = new Bucket(); map[tex] = b; }
            return b;
        }

        private static bool IsValidVertex(MapData map, int idx)
            => idx >= 0 && idx < map.Vertexes.Length;
    }
}
