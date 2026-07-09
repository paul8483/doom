using UnityEngine;
using Doom.Map;
using Doom.Specials;

namespace Doom.MapBuild
{
    /// Player-side trigger handling: Use (raycast), Walk (line crossing), Switch.
    /// Resolves the linedef special via LineSpecialTable and starts a SectorMover.
    public sealed class LineActivator : MonoBehaviour
    {
        static LineActivator instance;

        MapData map;
        RuntimeSectorHeights heights;
        SectorGeometry geometry;
        float worldScale;
        Transform cam;
        Vector3 lastPos;
        bool[] onceFired;        // per linedef
        bool[] moving;           // per sector: a mover is active

        public void Init(MapData map, RuntimeSectorHeights heights, SectorGeometry geometry,
                         float worldScale, Transform cam)
        {
            this.map = map; this.heights = heights; this.geometry = geometry;
            this.worldScale = worldScale; this.cam = cam;
            onceFired = new bool[map.LineDefs.Length];
            moving = new bool[map.Sectors.Length];
            lastPos = transform.position;
            instance = this;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// True when the wall belongs to a push-activated door a monster can open.
        public static bool IsUsableDoor(LineRef lineRef)
        {
            if (instance == null || lineRef == null || instance.map == null) return false;
            int lineIndex = lineRef.LineIndex;
            if (lineIndex < 0)
                lineIndex = instance.ResolveLine(lineRef, lineRef.transform.position);
            if (lineIndex < 0) return false;
            var ld = instance.map.LineDefs[lineIndex];
            if (!LineSpecialTable.TryGet(ld.Special, out var sp)) return false;
            if (sp.Trigger != TriggerKind.Push) return false;
            return sp.Category == SpecialCategory.Door || sp.Category == SpecialCategory.LockedDoor;
        }

        /// Activate the nearest push-door within range (monster door use).
        public static void MonsterUseNearestDoor(Vector3 pos, float radius)
        {
            if (instance == null) return;
            instance.UseNearestDoorAt(pos, radius);
        }

        void UseNearestDoorAt(Vector3 pos, float radius)
        {
            LineRef best = null;
            float bestDist = float.MaxValue;
            foreach (var lr in FindObjectsByType<LineRef>(FindObjectsSortMode.None))
            {
                if (!IsUsableDoor(lr)) continue;
                float d = Vector3.Distance(pos, lr.transform.position);
                if (d > radius || d >= bestDist) continue;
                bestDist = d;
                best = lr;
            }
            if (best == null) return;
            int lineIndex = ResolveLine(best, pos);
            if (lineIndex >= 0) Activate(lineIndex, TriggerKind.Push, alsoSwitch: true);
        }

        void Update()
        {
            if (map == null) return;
            HandleWalk();
            lastPos = transform.position;
        }

        /// Test hook: directly activate a linedef as if pushed (Stage 6a PlayMode tests).
        public void ActivateLineForTest(int lineIndex) => Activate(lineIndex, TriggerKind.Push, alsoSwitch: true);
        /// Test hook: read the live (raw DOOM-unit) ceiling height of a sector.
        public float GetSectorCeilForTest(int sector) => heights.CeilRaw(sector);
        /// Test hook: is a mover currently active on this sector?
        public bool IsSectorMovingForTest(int sector) => moving[sector];

        /// Called when the player presses Use. Raycasts forward into a wall.
        public void TryUse()
        {
            if (map == null || cam == null) return;
            float range = 64f * worldScale;
            // Ignore triggers: pickup spheres (ThingPickup) would otherwise eat the
            // ray before it reaches the wall behind them.
            if (!Physics.Raycast(cam.position, cam.forward, out var hit, range,
                                 ~0, QueryTriggerInteraction.Ignore)) return;

            var lineRef = hit.collider.GetComponentInParent<LineRef>();
            if (lineRef == null) return;

            int lineIndex = ResolveLine(lineRef, hit.point);
            if (lineIndex < 0) return;
            Activate(lineIndex, TriggerKind.Push, alsoSwitch: true);
        }

        /// Walls are texture-grouped, so a collider may cover several linedefs of the
        /// same sector. Prefer an exact LineIndex if one was recorded; otherwise pick
        /// the linedef of LineRef.SectorIndex whose segment is closest to the hit point.
        int ResolveLine(LineRef lineRef, Vector3 hitPoint)
        {
            if (lineRef.LineIndex >= 0 && lineRef.LineIndex < map.LineDefs.Length)
                return lineRef.LineIndex;

            int sector = lineRef.SectorIndex;
            if (sector < 0) return -1;

            Vector2 p = new(hitPoint.x, hitPoint.z);
            int best = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < map.LineDefs.Length; i++)
            {
                var ld = map.LineDefs[i];
                if (!LineTouchesSector(ld, sector)) continue;
                if (!IsValidVertex(ld.V1) || !IsValidVertex(ld.V2)) continue;
                var v1 = map.Vertexes[ld.V1]; var v2 = map.Vertexes[ld.V2];
                Vector2 a = new(v1.X * worldScale, v1.Y * worldScale);
                Vector2 b = new(v2.X * worldScale, v2.Y * worldScale);
                float d = DistPointSegmentSq(p, a, b);
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best;
        }

        bool LineTouchesSector(LineDef ld, int sector)
        {
            if (ld.FrontSideIdx >= 0 && ld.FrontSideIdx < map.SideDefs.Length &&
                map.SideDefs[ld.FrontSideIdx].SectorIdx == sector) return true;
            if (ld.BackSideIdx >= 0 && ld.BackSideIdx < map.SideDefs.Length &&
                map.SideDefs[ld.BackSideIdx].SectorIdx == sector) return true;
            return false;
        }

        static float DistPointSegmentSq(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 < 1e-12f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            Vector2 proj = a + t * ab;
            return (p - proj).sqrMagnitude;
        }

        void HandleWalk()
        {
            Vector3 a = lastPos, b = transform.position;
            if ((b - a).sqrMagnitude < 1e-8f) return;
            for (int i = 0; i < map.LineDefs.Length; i++)
            {
                if (onceFired[i]) continue;
                var ld = map.LineDefs[i];
                if (ld.Special == 0) continue;
                if (!LineSpecialTable.TryGet(ld.Special, out var sp)) continue;
                if (sp.Trigger != TriggerKind.Walk) continue;
                if (CrossesLine(a, b, ld)) Activate(i, TriggerKind.Walk, alsoSwitch: false);
            }
        }

        bool CrossesLine(Vector3 a, Vector3 b, LineDef ld)
        {
            if (!IsValidVertex(ld.V1) || !IsValidVertex(ld.V2)) return false;
            var v1 = map.Vertexes[ld.V1]; var v2 = map.Vertexes[ld.V2];
            Vector2 p1 = new(v1.X * worldScale, v1.Y * worldScale);
            Vector2 p2 = new(v2.X * worldScale, v2.Y * worldScale);
            Vector2 pa = new(a.x, a.z), pb = new(b.x, b.z);
            return SegmentsIntersect(pa, pb, p1, p2);
        }

        static bool SegmentsIntersect(Vector2 p, Vector2 p2, Vector2 q, Vector2 q2)
        {
            float d1 = Cross(q2 - q, p - q), d2 = Cross(q2 - q, p2 - q);
            float d3 = Cross(p2 - p, q - p), d4 = Cross(p2 - p, q2 - p);
            return ((d1 > 0) != (d2 > 0)) && ((d3 > 0) != (d4 > 0));
        }
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        bool IsValidVertex(int idx) => idx >= 0 && idx < map.Vertexes.Length;

        void Activate(int lineIndex, TriggerKind via, bool alsoSwitch)
        {
            var ld = map.LineDefs[lineIndex];
            if (!LineSpecialTable.TryGet(ld.Special, out var sp)) return;
            if (sp.Trigger != via && !(via == TriggerKind.Push && sp.Trigger == TriggerKind.Switch && alsoSwitch))
                return;
            if (!sp.IsExecutable)
            {
                Debug.Log($"[6a] line {lineIndex} special {ld.Special} category {sp.Category} not executed in Stage 6a");
                if (!sp.Repeatable) onceFired[lineIndex] = true;
                return;
            }
            if (sp.Key != KeyKind.None)
                Debug.Log($"[6a] locked door (key {sp.Key}) — key check deferred to Stage 6e; opening anyway");

            System.Collections.Generic.IEnumerable<int> targets =
                ld.Tag == 0 ? SectorActions.FindManualDoorTarget(map, lineIndex)
                            : SectorActions.FindTaggedSectors(map, ld.Tag);

            foreach (int s in targets) StartMover(s, sp);

            if (!sp.Repeatable) onceFired[lineIndex] = true;
        }

        void StartMover(int sector, LineSpecial sp)
        {
            if (sector < 0 || sector >= map.Sectors.Length) return;
            if (moving[sector]) return; // one mover per sector at a time; cleared on mover completion

            float speed = SectorMover.SpeedUnitsPerSec(sp.Speed);
            var mover = gameObject.AddComponent<SectorMover>();

            if (sp.Category == SpecialCategory.Door || sp.Category == SpecialCategory.LockedDoor)
            {
                int targetH = SectorActions.ComputeTargetHeight(map, heights, sector, sp.Target);
                bool cycle = sp.Repeatable || sp.Type == 1 || sp.Type == 4 || sp.Type == 90 || sp.Type == 63;
                mover.Begin(heights, geometry, sector, SectorMover.Surface.Ceiling,
                            targetH, speed, cycle, waitSeconds: 4.3f, onDone: () => moving[sector] = false);
            }
            else if (sp.Category == SpecialCategory.Plat)
            {
                int down = SectorActions.ComputeTargetHeight(map, heights, sector, TargetSpec.LowestNeighborFloor);
                mover.Begin(heights, geometry, sector, SectorMover.Surface.Floor,
                            down, speed, cycle: true, waitSeconds: 3f, onDone: () => moving[sector] = false);
            }
            else if (sp.Category == SpecialCategory.Floor)
            {
                int targetH = SectorActions.ComputeTargetHeight(map, heights, sector, sp.Target);
                mover.Begin(heights, geometry, sector, SectorMover.Surface.Floor,
                            targetH, speed, cycle: false, waitSeconds: 0f, onDone: () => moving[sector] = false);
            }
            else if (sp.Category == SpecialCategory.Stair)
            {
                var chain = SectorActions.BuildStairChain(map, heights, sector, stepUnits: 8);
                foreach (var (sec, tgt) in chain)
                {
                    if (sec < 0 || sec >= map.Sectors.Length || moving[sec]) continue;
                    int captured = sec;
                    var m = gameObject.AddComponent<SectorMover>();
                    m.Begin(heights, geometry, sec, SectorMover.Surface.Floor, tgt, speed, false, 0f,
                            onDone: () => moving[captured] = false);
                    moving[sec] = true;
                }
                return;
            }
            moving[sector] = true;
            // Cleared when the mover finishes (onDone), so the line can be re-triggered.
        }
    }
}
