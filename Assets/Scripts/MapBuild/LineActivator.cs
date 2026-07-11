using System.Collections.Generic;
using System.IO;
using System.Text;
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
        PlayerInventory inventory;
        SoundSystem sound;
        float keyDenyCooldown;
        TeleportLanding[] teleportLandings;
        PlayerController playerLook;
        WalkLineIndex walkIndex;
        readonly List<int> walkQuery = new List<int>(32);
        readonly List<int> monsterDoorLines = new List<int>();
        readonly Dictionary<LineRef, int> resolvedLineRefs = new Dictionary<LineRef, int>();

        public void Init(MapData map, RuntimeSectorHeights heights, SectorGeometry geometry,
                         float worldScale, Transform cam, SoundSystem sound = null)
        {
            this.map = map; this.heights = heights; this.geometry = geometry;
            this.worldScale = worldScale; this.cam = cam; this.sound = sound;
            onceFired = new bool[map.LineDefs.Length];
            moving = new bool[map.Sectors.Length];
            lastPos = transform.position;
            teleportLandings = TeleportRules.CollectLandings(map);
            playerLook = GetComponent<PlayerController>();
            walkIndex = new WalkLineIndex(map, worldScale);
            BuildMonsterDoorLines();
            resolvedLineRefs.Clear();
            instance = this;
        }

        public void SetInventory(PlayerInventory inv) => inventory = inv;

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
                lineIndex = instance.ResolveLineCached(lineRef);
            if (lineIndex < 0) return false;
            return IsMonsterUsableDoorSpecial(instance.map.LineDefs[lineIndex].Special);
        }

        /// Activate the nearest push-door within range (monster door use).
        public static void MonsterUseNearestDoor(Vector3 pos, float radius)
        {
            if (instance == null) return;
            instance.UseNearestDoorAt(pos, radius);
        }

        /// Monster walk-over: fire MonsterActivatable Walk specials crossed from→to.
        public static void MonsterCrossed(Vector3 from, Vector3 to, Transform body)
        {
            if (instance == null || body == null) return;
            instance.pendingMonsterBody = body;
            try
            {
                instance.HandleActorWalk(from, to, TeleportActorKind.Monster);
            }
            finally
            {
                instance.pendingMonsterBody = null;
            }
        }

        Transform pendingMonsterBody;

        void UseNearestDoorAt(Vector3 pos, float radius)
        {
            Vector2 point = new Vector2(pos.x, pos.z);
            float radiusSq = radius * radius;
            float bestDistSq = float.MaxValue;
            int bestLine = -1;

            for (int i = 0; i < monsterDoorLines.Count; i++)
            {
                int lineIndex = monsterDoorLines[i];
                var ld = map.LineDefs[lineIndex];
                if (!IsValidVertex(ld.V1) || !IsValidVertex(ld.V2)) continue;

                var v1 = map.Vertexes[ld.V1];
                var v2 = map.Vertexes[ld.V2];
                Vector2 a = new Vector2(v1.X * worldScale, v1.Y * worldScale);
                Vector2 b = new Vector2(v2.X * worldScale, v2.Y * worldScale);
                float distanceSq = DistPointSegmentSq(point, a, b);
                if (distanceSq > radiusSq || distanceSq >= bestDistSq) continue;

                bestDistSq = distanceSq;
                bestLine = lineIndex;
            }

            if (bestLine >= 0)
                Activate(bestLine, TriggerKind.Push, alsoSwitch: true);
        }

        void BuildMonsterDoorLines()
        {
            monsterDoorLines.Clear();
            if (map == null) return;

            for (int i = 0; i < map.LineDefs.Length; i++)
            {
                if (IsMonsterUsableDoorSpecial(map.LineDefs[i].Special))
                    monsterDoorLines.Add(i);
            }
        }

        static bool IsMonsterUsableDoorSpecial(int special)
        {
            if (!LineSpecialTable.TryGet(special, out var sp)) return false;
            if (sp.Trigger != TriggerKind.Push) return false;
            return sp.Category == SpecialCategory.Door ||
                   sp.Category == SpecialCategory.LockedDoor;
        }

        int ResolveLineCached(LineRef lineRef)
        {
            if (resolvedLineRefs.TryGetValue(lineRef, out int lineIndex))
                return lineIndex;

            lineIndex = ResolveLine(lineRef, lineRef.transform.position);
            resolvedLineRefs[lineRef] = lineIndex;
            return lineIndex;
        }

        void Update()
        {
            if (map == null) return;
            if (keyDenyCooldown > 0f) keyDenyCooldown -= Time.deltaTime;
            HandleWalk();
            lastPos = transform.position;
        }

        /// Test hook: directly activate a linedef as if pushed (Stage 6a PlayMode tests).
        public void ActivateLineForTest(int lineIndex) =>
            Activate(lineIndex, TriggerKind.Push, alsoSwitch: true, TeleportActorKind.Player);

        /// Test hook: fire a Walk teleport linedef as the player (no geometric cross required).
        public bool ActivateTeleportForTest(int lineIndex)
        {
            if (map == null || lineIndex < 0 || lineIndex >= map.LineDefs.Length) return false;
            var before = transform.position;
            Activate(lineIndex, TriggerKind.Walk, alsoSwitch: false, TeleportActorKind.Player);
            return (transform.position - before).sqrMagnitude > 1e-6f;
        }
        /// Test hook: read the live (raw DOOM-unit) ceiling height of a sector.
        public float GetSectorCeilForTest(int sector) => heights.CeilRaw(sector);
        /// Test hook: is a mover currently active on this sector?
        public bool IsSectorMovingForTest(int sector) => moving[sector];

        /// Capture one-shot fired flags for save (defensive copy).
        public void CaptureFired(out bool[] fired)
        {
            if (onceFired == null)
            {
                fired = System.Array.Empty<bool>();
                return;
            }
            fired = new bool[onceFired.Length];
            System.Array.Copy(onceFired, fired, onceFired.Length);
        }

        /// Restore one-shot fired flags from a save (length must match map).
        public void RestoreFired(bool[] fired)
        {
            if (onceFired == null || fired == null) return;
            int n = System.Math.Min(onceFired.Length, fired.Length);
            for (int i = 0; i < n; i++) onceFired[i] = fired[i];
        }

        /// Mark a sector as having an active mover (blocks re-trigger until done).
        public void SetSectorMoving(int sector, bool isMoving)
        {
            if (moving == null || sector < 0 || sector >= moving.Length) return;
            moving[sector] = isMoving;
        }

        /// Called when the player presses Use. Raycasts forward into a wall; if that
        /// misses or resolves to a non-special linedef (common with texture-grouped
        /// walls / closed door faces), falls back to the nearest Push/Switch special
        /// in front of the camera — same idea as monster door-use.
        public void TryUse()
        {
            if (map == null || cam == null) return;
            float range = 64f * worldScale;
            // Ignore triggers: pickup spheres (ThingPickup) would otherwise eat the
            // ray before it reaches the wall behind them.
            if (Physics.Raycast(cam.position, cam.forward, out var hit, range,
                                ~0, QueryTriggerInteraction.Ignore))
            {
                var lineRef = hit.collider.GetComponentInParent<LineRef>();
                if (lineRef != null)
                {
                    int lineIndex = ResolveLine(lineRef, hit.point);
                    if (lineIndex >= 0 && IsPlayerUseSpecial(map.LineDefs[lineIndex].Special))
                    {
                        Activate(lineIndex, TriggerKind.Push, alsoSwitch: true);
                        return;
                    }
                }
            }

            UseNearestSpecialInFront(range);
        }

        /// Writes a structured snapshot of the player's map location and view.
        /// Intended for interactive bug reports: press T, then attach Player.log
        /// or doom-location-dumps.log from Application.persistentDataPath.
        public void DumpLocation()
        {
            if (map == null || cam == null) return;

            var sb = new StringBuilder(4096);
            Vector3 p = transform.position;
            Vector3 cp = cam.position;
            Vector3 fwd = cam.forward;
            sb.AppendLine("========== DOOM LOCATION DUMP ==========");
            sb.AppendLine($"utc={System.DateTime.UtcNow:O}");
            sb.AppendLine($"map={map.Name}");
            sb.AppendLine($"player.unity=({p.x:F4}, {p.y:F4}, {p.z:F4})");
            sb.AppendLine($"player.doom=({p.x / worldScale:F1}, {p.z / worldScale:F1}, y={p.y / worldScale:F1})");
            sb.AppendLine($"view.yaw={transform.eulerAngles.y:F2} pitch={(playerLook != null ? playerLook.PitchDegrees : 0f):F2}");
            sb.AppendLine($"camera.pos=({cp.x:F4}, {cp.y:F4}, {cp.z:F4}) forward=({fwd.x:F4}, {fwd.y:F4}, {fwd.z:F4})");

            AppendFloorInfo(sb);
            AppendViewHits(sb);
            AppendNearbyPickups(sb, 256f);
            AppendNearbyLines(sb, 128f);
            sb.AppendLine("========== END DOOM LOCATION DUMP ==========");

            string dump = sb.ToString();
            Debug.Log(dump);
            try
            {
                string path = Path.Combine(Application.persistentDataPath, "doom-location-dumps.log");
                File.AppendAllText(path, dump + System.Environment.NewLine);
                Debug.Log($"[LocationDump] saved to: {path}");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[LocationDump] could not write dedicated log: {ex.Message}");
            }
        }

        void AppendFloorInfo(StringBuilder sb)
        {
            Vector3 origin = transform.position + Vector3.up * (8f * worldScale);
            float range = 256f * worldScale;
            if (!Physics.Raycast(origin, Vector3.down, out var hit, range, ~0,
                                 QueryTriggerInteraction.Ignore))
            {
                sb.AppendLine("floor.hit=none");
                return;
            }

            var sectorRef = hit.collider.GetComponentInParent<SectorRef>();
            int s = sectorRef != null ? sectorRef.SectorIndex : -1;
            sb.AppendLine($"floor.hit={HierarchyPath(hit.collider.transform)} distanceDU={hit.distance / worldScale:F1} sector={s}");
            if (s >= 0 && s < map.Sectors.Length)
            {
                var sector = map.Sectors[s];
                sb.AppendLine(
                    $"sector[{s}]=floor:{heights.FloorRaw(s):F1} ceil:{heights.CeilRaw(s):F1} " +
                    $"staticFloor:{sector.FloorHeight} staticCeil:{sector.CeilingHeight} " +
                    $"special:{sector.Special} tag:{sector.Tag} moving:{moving[s]}");
            }
        }

        void AppendViewHits(StringBuilder sb)
        {
            float range = 2048f * worldScale;
            RaycastHit[] hits = Physics.RaycastAll(cam.position, cam.forward, range, ~0,
                                                   QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            sb.AppendLine($"view.hits={hits.Length} rangeDU=2048");
            int count = System.Math.Min(hits.Length, 12);
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                var lr = hit.collider.GetComponentInParent<LineRef>();
                int line = lr != null ? ResolveLine(lr, hit.point) : -1;
                string lineInfo = line >= 0 ? FormatLine(line) : "line:none";
                sb.AppendLine(
                    $"  hit[{i}] distanceDU={hit.distance / worldScale:F1} trigger={hit.collider.isTrigger} " +
                    $"object={HierarchyPath(hit.collider.transform)} {lineInfo}");
            }
        }

        void AppendNearbyPickups(StringBuilder sb, float radiusDU)
        {
            Vector3 p = transform.position;
            float radius = radiusDU * worldScale;
            float radiusSq = radius * radius;
            var pickups = FindObjectsByType<ThingPickup>(FindObjectsSortMode.None);
            var nearby = new List<(ThingPickup pickup, float distanceSq)>();
            for (int i = 0; i < pickups.Length; i++)
            {
                var pu = pickups[i];
                if (pu == null) continue;
                Vector3 d = pu.transform.position - p;
                float dSq = d.x * d.x + d.z * d.z;
                if (dSq <= radiusSq) nearby.Add((pu, dSq));
            }
            nearby.Sort((a, b) => a.distanceSq.CompareTo(b.distanceSq));

            sb.AppendLine($"nearby.pickups={nearby.Count} radiusDU={radiusDU:F0}");
            int count = System.Math.Min(nearby.Count, 12);
            for (int i = 0; i < count; i++)
            {
                var pu = nearby[i].pickup;
                Vector3 ip = pu.transform.position;
                sb.AppendLine(
                    $"  pickup[{i}] doomed={pu.DoomedNum} xyDU={Mathf.Sqrt(nearby[i].distanceSq) / worldScale:F1} " +
                    $"dyDU={(ip.y - p.y) / worldScale:F1} " +
                    $"pos=({ip.x / worldScale:F1}, {ip.z / worldScale:F1}, y={ip.y / worldScale:F1}) " +
                    $"object={HierarchyPath(pu.transform)}");
            }
        }

        void AppendNearbyLines(StringBuilder sb, float radiusDU)
        {
            Vector2 p = new(transform.position.x, transform.position.z);
            float radius = radiusDU * worldScale;
            float radiusSq = radius * radius;
            var nearby = new List<(int index, float distanceSq)>();
            for (int i = 0; i < map.LineDefs.Length; i++)
            {
                var ld = map.LineDefs[i];
                if (!IsValidVertex(ld.V1) || !IsValidVertex(ld.V2)) continue;
                var v1 = map.Vertexes[ld.V1];
                var v2 = map.Vertexes[ld.V2];
                Vector2 a = new(v1.X * worldScale, v1.Y * worldScale);
                Vector2 b = new(v2.X * worldScale, v2.Y * worldScale);
                float d = DistPointSegmentSq(p, a, b);
                if (d <= radiusSq) nearby.Add((i, d));
            }
            nearby.Sort((a, b) => a.distanceSq.CompareTo(b.distanceSq));

            sb.AppendLine($"nearby.lines={nearby.Count} radiusDU={radiusDU:F0}");
            int count = System.Math.Min(nearby.Count, 20);
            for (int i = 0; i < count; i++)
            {
                int line = nearby[i].index;
                sb.AppendLine(
                    $"  near[{i}] distanceDU={Mathf.Sqrt(nearby[i].distanceSq) / worldScale:F1} {FormatLine(line)}");
            }
        }

        string FormatLine(int lineIndex)
        {
            var ld = map.LineDefs[lineIndex];
            int front = SideSector(ld.FrontSideIdx);
            int back = SideSector(ld.BackSideIdx);
            string definition = LineSpecialTable.TryGet(ld.Special, out var sp)
                ? $"{sp.Trigger}/{sp.Category}/repeat:{sp.Repeatable}/exec:{sp.IsExecutable}"
                : "unclassified";
            return $"line:{lineIndex} special:{ld.Special}({definition}) tag:{ld.Tag} flags:0x{ld.Flags:X4} " +
                   $"frontSector:{front} backSector:{back} fired:{onceFired[lineIndex]}";
        }

        int SideSector(int sideIndex) =>
            sideIndex >= 0 && sideIndex < map.SideDefs.Length
                ? map.SideDefs[sideIndex].SectorIdx
                : -1;

        static string HierarchyPath(Transform t)
        {
            if (t == null) return "<null>";
            var names = new Stack<string>();
            while (t != null)
            {
                names.Push(t.name);
                t = t.parent;
            }
            return string.Join("/", names);
        }

        /// Walls are texture-grouped, so a collider may cover several linedefs of the
        /// same sector. Prefer an exact LineIndex if one was recorded; otherwise pick
        /// the linedef of LineRef.SectorIndex whose segment is closest to the hit point,
        /// preferring Use-activatable specials when nearly tied (door vs doortrack).
        int ResolveLine(LineRef lineRef, Vector3 hitPoint)
        {
            if (lineRef.LineIndex >= 0 && lineRef.LineIndex < map.LineDefs.Length)
                return lineRef.LineIndex;

            int sector = lineRef.SectorIndex;
            if (sector < 0) return -1;

            Vector2 p = new(hitPoint.x, hitPoint.z);
            int best = -1;
            float bestDist = float.MaxValue;
            int bestUse = -1;
            float bestUseDist = float.MaxValue;
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
                if (IsPlayerUseSpecial(ld.Special) && d < bestUseDist)
                { bestUseDist = d; bestUse = i; }
            }
            // 8 DU slack: grouped colliders often make a special=0 neighbor "closer".
            float slack = 8f * worldScale;
            slack *= slack;
            if (bestUse >= 0 && bestUseDist <= bestDist + slack)
                return bestUse;
            return best;
        }

        static bool IsPlayerUseSpecial(int special)
        {
            if (special == 0 || !LineSpecialTable.TryGet(special, out var sp)) return false;
            if (!sp.IsExecutable) return false;
            return sp.Trigger == TriggerKind.Push || sp.Trigger == TriggerKind.Switch;
        }

        /// Nearest Push/Switch executable linedef in front of the camera within range.
        void UseNearestSpecialInFront(float radius)
        {
            int best = FindNearestSpecialInFront(radius);
            if (best >= 0)
                Activate(best, TriggerKind.Push, alsoSwitch: true);
        }

        int FindNearestSpecialInFront(float radius)
        {
            if (cam == null || map == null) return -1;

            Vector3 origin = cam.position;
            Vector3 flatFwd = cam.forward;
            flatFwd.y = 0f;
            if (flatFwd.sqrMagnitude < 1e-8f) return -1;
            flatFwd.Normalize();

            int best = -1;
            float bestDistSq = float.MaxValue;
            float radiusSq = radius * radius;
            for (int i = 0; i < map.LineDefs.Length; i++)
            {
                var ld = map.LineDefs[i];
                if (!IsPlayerUseSpecial(ld.Special)) continue;
                if (!IsValidVertex(ld.V1) || !IsValidVertex(ld.V2)) continue;
                var v1 = map.Vertexes[ld.V1]; var v2 = map.Vertexes[ld.V2];
                Vector2 a = new(v1.X * worldScale, v1.Y * worldScale);
                Vector2 b = new(v2.X * worldScale, v2.Y * worldScale);
                Vector2 o = new(origin.x, origin.z);
                float dSq = DistPointSegmentSq(o, a, b);
                if (dSq > radiusSq) continue;
                Vector2 mid = (a + b) * 0.5f;
                Vector3 to = new Vector3(mid.x - origin.x, 0f, mid.y - origin.z);
                if (Vector3.Dot(flatFwd, to) <= 0f) continue;
                if (dSq < bestDistSq) { bestDistSq = dSq; best = i; }
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
            HandleActorWalk(a, b, TeleportActorKind.Player);
        }

        void HandleActorWalk(Vector3 from, Vector3 to, TeleportActorKind actor)
        {
            if (map == null) return;
            if ((to - from).sqrMagnitude < 1e-8f) return;
            Vector2 a = new(from.x, from.z);
            Vector2 b = new(to.x, to.z);
            if (walkIndex != null)
                walkIndex.QuerySegment(a, b, walkQuery);
            else
            {
                walkQuery.Clear();
                for (int i = 0; i < map.LineDefs.Length; i++) walkQuery.Add(i);
            }

            for (int qi = 0; qi < walkQuery.Count; qi++)
            {
                int i = walkQuery[qi];
                if (onceFired[i]) continue;
                var ld = map.LineDefs[i];
                if (ld.Special == 0) continue;
                if (!LineSpecialTable.TryGet(ld.Special, out var sp)) continue;
                if (sp.Trigger != TriggerKind.Walk) continue;
                if (actor == TeleportActorKind.Monster && !sp.MonsterActivatable) continue;
                if (!CrossesLine(from, to, ld)) continue;
                if (!CrossedFromFrontSide(from, ld)) continue;
                Activate(i, TriggerKind.Walk, alsoSwitch: false, actor);
            }
        }

        bool CrossedFromFrontSide(Vector3 from, LineDef ld)
        {
            if (!IsValidVertex(ld.V1) || !IsValidVertex(ld.V2)) return false;
            var v1 = map.Vertexes[ld.V1];
            var v2 = map.Vertexes[ld.V2];
            return TeleportRules.IsOnFrontSide(
                from.x / worldScale, from.z / worldScale,
                v1.X, v1.Y, v2.X, v2.Y);
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

        void Activate(int lineIndex, TriggerKind via, bool alsoSwitch,
                      TeleportActorKind actor = TeleportActorKind.Player)
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

            if (sp.Category == SpecialCategory.Exit)
            {
                var kind = LevelTransitionController.KindFromLinedefSpecial(ld.Special);
                var ctrl = LevelTransitionController.Ensure();
                if (!ctrl.TryRequestExit(new LevelExitRequest(kind, lineIndex)))
                    return; // already transitioning — do not mark fired

                if (sp.Trigger == TriggerKind.Switch)
                    sound?.PlayAt("DSSWTCHN", LineMidpoint(lineIndex));

                if (!sp.Repeatable) onceFired[lineIndex] = true;
                return;
            }

            if (sp.Category == SpecialCategory.Teleport)
            {
                if (!TeleportRules.CanActorUse(ld.Special, actor))
                    return;
                if (!TeleportRules.TrySelect(map, ld.Tag, teleportLandings, out var landing))
                {
                    Debug.LogWarning($"[7e] teleport line {lineIndex} tag {ld.Tag}: no landing");
                    return;
                }

                Transform body = actor == TeleportActorKind.Player
                    ? transform
                    : null;
                // Monster body is passed via pendingMonsterBody when activated from MonsterCrossed.
                if (actor == TeleportActorKind.Monster)
                    body = pendingMonsterBody;
                if (body == null) return;

                var cc = actor == TeleportActorKind.Player
                    ? GetComponent<CharacterController>()
                    : null;
                var look = actor == TeleportActorKind.Player ? playerLook : null;
                if (!TeleportExecutor.TryTeleport(map, landing, body, worldScale, cc, look, sound))
                    return;

                if (!sp.Repeatable) onceFired[lineIndex] = true;
                return;
            }

            if (sp.Key != KeyKind.None)
            {
                if (inventory == null || !KeyMapping.HasRequired(inventory.Keys, sp.Key))
                {
                    if (inventory != null)
                        Debug.Log($"need key {sp.Key}");
                    PlayKeyDenied();
                    return;
                }
            }

            System.Collections.Generic.IEnumerable<int> targets =
                ld.Tag == 0 ? SectorActions.FindManualDoorTarget(map, lineIndex)
                            : SectorActions.FindTaggedSectors(map, ld.Tag);

            bool any = false;
            foreach (int s in targets)
            {
                StartMover(s, sp);
                any = true;
            }

            if (any && sp.Trigger == TriggerKind.Switch)
                sound?.PlayAt("DSSWTCHN", LineMidpoint(lineIndex));

            if (!sp.Repeatable) onceFired[lineIndex] = true;
        }

        void PlayKeyDenied()
        {
            if (keyDenyCooldown > 0f) return;
            keyDenyCooldown = 0.25f;
            if (sound == null) return;
            if (sound.Cache != null && sound.Cache.Get("DSNOWAY") != null)
                sound.PlayLocal("DSNOWAY");
            else
                sound.PlayLocal("DSOOF");
        }

        Vector3 LineMidpoint(int lineIndex)
        {
            var ld = map.LineDefs[lineIndex];
            if (!IsValidVertex(ld.V1) || !IsValidVertex(ld.V2)) return transform.position;
            var v1 = map.Vertexes[ld.V1];
            var v2 = map.Vertexes[ld.V2];
            return new Vector3(
                (v1.X + v2.X) * 0.5f * worldScale,
                0f,
                (v1.Y + v2.Y) * 0.5f * worldScale);
        }

        Vector3 SectorSoundOrigin(int sector)
        {
            // Sector_* roots sit at map origin — use floor/ceiling mesh bounds center.
            if (geometry != null)
            {
                var root = geometry.GetSectorRoot(sector);
                if (root != null)
                {
                    Transform surface = root.Find("Floor") ?? root.Find("Ceiling");
                    if (surface != null)
                    {
                        var mf = surface.GetComponent<MeshFilter>();
                        if (mf != null && mf.sharedMesh != null)
                            return mf.sharedMesh.bounds.center;
                    }
                }
            }

            // Fallback: average linedef vertices touching the sector.
            float sx = 0f, sy = 0f;
            int n = 0;
            for (int i = 0; i < map.LineDefs.Length; i++)
            {
                var ld = map.LineDefs[i];
                if (!LineTouchesSector(ld, sector)) continue;
                if (!IsValidVertex(ld.V1) || !IsValidVertex(ld.V2)) continue;
                var v1 = map.Vertexes[ld.V1];
                var v2 = map.Vertexes[ld.V2];
                sx += v1.X + v2.X;
                sy += v1.Y + v2.Y;
                n += 2;
            }
            if (n == 0) return Vector3.zero;
            return new Vector3(sx / n * worldScale, 0f, sy / n * worldScale);
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
                            targetH, speed, cycle, waitSeconds: 4.3f, onDone: () => moving[sector] = false,
                            sound, MoverSoundProfile.Door, SectorSoundOrigin(sector));
            }
            else if (sp.Category == SpecialCategory.Plat)
            {
                int down = SectorActions.ComputeTargetHeight(map, heights, sector, TargetSpec.LowestNeighborFloor);
                mover.Begin(heights, geometry, sector, SectorMover.Surface.Floor,
                            down, speed, cycle: true, waitSeconds: 3f, onDone: () => moving[sector] = false,
                            sound, MoverSoundProfile.FloorOrLift, SectorSoundOrigin(sector));
            }
            else if (sp.Category == SpecialCategory.Floor)
            {
                int targetH = SectorActions.ComputeTargetHeight(map, heights, sector, sp.Target);
                mover.Begin(heights, geometry, sector, SectorMover.Surface.Floor,
                            targetH, speed, cycle: false, waitSeconds: 0f, onDone: () => moving[sector] = false,
                            sound, MoverSoundProfile.FloorOrLift, SectorSoundOrigin(sector));
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
                            onDone: () => moving[captured] = false,
                            sound, MoverSoundProfile.FloorOrLift, SectorSoundOrigin(sec));
                    moving[sec] = true;
                }
                return;
            }
            moving[sector] = true;
            // Cleared when the mover finishes (onDone), so the line can be re-triggered.
        }
    }
}
