using System.Collections.Generic;
using Doom.Map;

namespace Doom.Specials
{
    /// Actor kinds that may cross a teleport linedef.
    public enum TeleportActorKind { Player, Monster }

    /// One MT_TELEPORTMAN (thing type 14) landing resolved to a sector.
    public readonly struct TeleportLanding
    {
        public readonly int ThingIndex;
        public readonly short X;
        public readonly short Y;
        public readonly ushort Angle;
        public readonly int SectorIndex;

        public TeleportLanding(int thingIndex, short x, short y, ushort angle, int sectorIndex)
        {
            ThingIndex = thingIndex;
            X = x;
            Y = y;
            Angle = angle;
            SectorIndex = sectorIndex;
        }
    }

    /// Pure EV_Teleport rules: destination selection, actor filter, telefrag policy.
    public static class TeleportRules
    {
        public const int DestinationThingType = 14;

        /// Linedef specials 125/126 are monster-only; 39/97 allow player and monster.
        public static bool IsMonsterOnly(int linedefSpecial) =>
            linedefSpecial == 125 || linedefSpecial == 126;

        public static bool CanActorUse(int linedefSpecial, TeleportActorKind actor)
        {
            if (IsMonsterOnly(linedefSpecial))
                return actor == TeleportActorKind.Monster;
            return true;
        }

        /// Collect every type-14 thing and assign its containing sector via polygons.
        public static TeleportLanding[] CollectLandings(MapData map)
        {
            if (map == null) return System.Array.Empty<TeleportLanding>();
            var polys = SectorPolygonBuilder.Build(map);
            var list = new List<TeleportLanding>();
            for (int i = 0; i < map.Things.Length; i++)
            {
                var t = map.Things[i];
                if (t.Type != DestinationThingType) continue;
                int sector = FindSectorContaining(map, polys, t.X, t.Y);
                if (sector < 0) continue;
                list.Add(new TeleportLanding(i, t.X, t.Y, t.Angle, sector));
            }
            return list.ToArray();
        }

        /// First landing in the lowest-index sector that carries <paramref name="tag"/>,
        /// preferring the lowest thing index when several landings share that sector.
        public static bool TrySelect(
            MapData map, int tag, TeleportLanding[] landings, out TeleportLanding chosen)
        {
            chosen = default;
            if (map == null || landings == null || landings.Length == 0 || tag == 0)
                return false;

            for (int s = 0; s < map.Sectors.Length; s++)
            {
                if (map.Sectors[s].Tag != tag) continue;
                bool found = false;
                TeleportLanding best = default;
                for (int i = 0; i < landings.Length; i++)
                {
                    if (landings[i].SectorIndex != s) continue;
                    if (!found || landings[i].ThingIndex < best.ThingIndex)
                    {
                        best = landings[i];
                        found = true;
                    }
                }
                if (found)
                {
                    chosen = best;
                    return true;
                }
            }
            return false;
        }

        /// Vanilla P_TeleportMove stomps anything blocking the destination.
        public static bool ShouldTelefrag(bool destinationOccupiedBySolid) =>
            destinationOccupiedBySolid;

        /// DOOM front side is the right side of the directed linedef V1→V2.
        public static bool IsOnFrontSide(float px, float py, float v1x, float v1y, float v2x, float v2y)
        {
            float cross = (v2x - v1x) * (py - v1y) - (v2y - v1y) * (px - v1x);
            return cross < 0f;
        }

        static int FindSectorContaining(MapData map, SectorPolygon[] polys, float x, float y)
        {
            for (int s = 0; s < polys.Length; s++)
            {
                if (PointInSector(map, polys[s], x, y))
                    return s;
            }
            return -1;
        }

        static bool PointInSector(MapData map, SectorPolygon poly, float x, float y)
        {
            if (poly == null || !poly.IsValid || poly.Outer == null || poly.Outer.Count < 3)
                return false;
            if (!RingContains(map, poly.Outer, x, y)) return false;
            if (poly.Holes != null)
            {
                for (int h = 0; h < poly.Holes.Count; h++)
                {
                    if (RingContains(map, poly.Holes[h], x, y))
                        return false;
                }
            }
            return true;
        }

        static bool RingContains(MapData map, IReadOnlyList<int> ring, float x, float y)
        {
            bool inside = false;
            int n = ring.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var vi = map.Vertexes[ring[i]];
                var vj = map.Vertexes[ring[j]];
                if ((vi.Y > y) == (vj.Y > y)) continue;
                float denom = vj.Y - vi.Y;
                if (denom == 0f) continue;
                float xCross = (vj.X - vi.X) * (y - vi.Y) / denom + vi.X;
                if (x < xCross) inside = !inside;
            }
            return inside;
        }
    }
}
