using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Doom.Map;
using Doom.MapBuild.Rendering;
using Doom.Specials;
using Doom.Things;

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
        RuntimeSectorLights lights;
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

        // P_ChangeSwitchTexture buttons: a repeatable switch pops back after
        // BUTTONTIME (vanilla p_switch.c). One entry per press, like vanilla's
        // buttonlist — rapid re-presses toggle and stack their own restores.
        struct ActiveButton
        {
            public int Line;
            public int Side;
            public SwitchTextureRules.Slot Slot;
            public string Restore;
            public float Timer;
        }
        readonly List<ActiveButton> buttons = new List<ActiveButton>();
        readonly List<int> walkQuery = new List<int>(32);
        readonly List<int> monsterDoorLines = new List<int>();
        // True while a monster's door use runs through Activate (vanilla
        // EV_VerticalDoor lets only players slam an open door shut).
        bool monsterDoorUse;

        public void Init(MapData map, RuntimeSectorHeights heights, SectorGeometry geometry,
                         float worldScale, Transform cam, SoundSystem sound = null,
                         RuntimeSectorLights lights = null)
        {
            this.map = map; this.heights = heights; this.geometry = geometry;
            this.worldScale = worldScale; this.cam = cam; this.sound = sound;
            this.lights = lights;
            onceFired = new bool[map.LineDefs.Length];
            moving = new bool[map.Sectors.Length];
            lastPos = transform.position;
            teleportLandings = TeleportRules.CollectLandings(map);
            playerLook = GetComponent<PlayerController>();
            walkIndex = new WalkLineIndex(map, worldScale);
            BuildMonsterDoorLines();
            instance = this;
        }

        public void SetInventory(PlayerInventory inv) => inventory = inv;

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// True when the wall belongs to a push-activated door a monster can open.
        /// Monster step blocked by a wall collider: is the linedef at the sweep
        /// hit point a manual door? Wall GameObjects sit at the sector origin
        /// and group several linedefs, so the hit point (not the transform)
        /// picks the segment — the old transform-based cache resolved every
        /// wall to whichever line lay nearest the map origin.
        public static bool IsUsableDoor(LineRef lineRef, Vector3 hitPoint)
        {
            if (instance == null || lineRef == null || instance.map == null) return false;
            int lineIndex = lineRef.LineIndex;
            if (lineIndex < 0)
                lineIndex = instance.ResolveLine(lineRef, hitPoint);
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

            if (bestLine < 0) return;
            monsterDoorUse = true;
            try { Activate(bestLine, TriggerKind.Push, alsoSwitch: true); }
            finally { monsterDoorUse = false; }
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

        /// P_UseSpecialLine for a non-player actor: the whitelist is 1 / 32 /
        /// 33 / 34, and EV_VerticalDoor then rejects 32–34 with `if (!player)
        /// return 0`, so the only door a monster ever opens is special 1
        /// (manual raise). Every keyed door — and 31 / 117 / 118, which are
        /// not on the list at all — is a plain wall to a monster: it picks a
        /// new direction instead of standing at the door and "using" it.
        /// The old predicate (any Push door, keyed ones included) parked
        /// monster packs at locked doors, where each use ran the PLAYER's
        /// key check and its map-wide grunt (E1M4 blue door, slot 0).
        public static bool IsMonsterUsableDoorSpecial(int special) => special == 1;


        void Update()
        {
            if (map == null) return;
            if (keyDenyCooldown > 0f) keyDenyCooldown -= Time.deltaTime;
            TickButtons(Time.deltaTime);
            HandleWalk();
            lastPos = transform.position;
        }

        void TickButtons(float dt)
        {
            for (int i = buttons.Count - 1; i >= 0; i--)
            {
                var b = buttons[i];
                b.Timer -= dt;
                if (b.Timer > 0f)
                {
                    buttons[i] = b;
                    continue;
                }
                buttons.RemoveAt(i);
                map.SideDefs[b.Side] = SwitchTextureRules.WithSlot(
                    map.SideDefs[b.Side], b.Slot, b.Restore);
                geometry?.RebuildSectorAndNeighbors(map.SideDefs[b.Side].SectorIdx);
                // Vanilla P_UpdateSpecials plays swtchn again when the button pops.
                sound?.PlayAt("DSSWTCHN", LineMidpoint(b.Line));
            }
        }

        /// P_ChangeSwitchTexture: flip the front sidedef's switch texture
        /// (top -> mid -> bottom, first match). Repeatable switches queue a
        /// button that restores the pressed slot after BUTTONTIME.
        void ChangeSwitchTexture(int lineIndex, bool useAgain)
        {
            var ld = map.LineDefs[lineIndex];
            int sideIdx = ld.FrontSideIdx;
            if (sideIdx < 0 || sideIdx >= map.SideDefs.Length) return;

            var side = map.SideDefs[sideIdx];
            var slot = SwitchTextureRules.FindSlot(side, out string from, out string to);
            if (slot == SwitchTextureRules.Slot.None) return;

            map.SideDefs[sideIdx] = SwitchTextureRules.WithSlot(side, slot, to);
            geometry?.RebuildSectorAndNeighbors(side.SectorIdx);

            if (useAgain)
                buttons.Add(new ActiveButton
                {
                    Line = lineIndex,
                    Side = sideIdx,
                    Slot = slot,
                    Restore = from,
                    Timer = SwitchTextureRules.ButtonSeconds,
                });
        }

        /// Test hook: pending button-restore count.
        public int ActiveButtonCountForTest => buttons.Count;

        /// Test hook: the LIVE sidedef (switch swaps mutate the runtime map).
        public SideDef GetSideDefForTest(int sideIdx) => map.SideDefs[sideIdx];

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
        /// Re-derives pressed S1 switch faces from the fired flags: vanilla
        /// saves sidedef textures (P_ArchiveWorld); the port keeps the schema
        /// untouched because the scene rebuilds from the WAD's initial state,
        /// so one toggle per fired executable switch line reconstructs it.
        /// SR buttons pop back within a second and need nothing.
        public void RestoreFired(bool[] fired)
        {
            if (onceFired == null || fired == null) return;
            int n = System.Math.Min(onceFired.Length, fired.Length);
            for (int i = 0; i < n; i++)
            {
                onceFired[i] = fired[i];
                if (!fired[i]) continue;
                if (!LineSpecialTable.TryGet(map.LineDefs[i].Special, out var sp)) continue;
                if (sp.Trigger != TriggerKind.Switch || !sp.IsExecutable) continue;
                ChangeSwitchTexture(i, useAgain: false);
            }
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
            AppendGraphicsLightInfo(sb);
            AppendViewHits(sb);
            AppendNearbyPickups(sb, 256f);
            AppendNearbyEmissiveThings(sb, 512f);
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

        void AppendGraphicsLightInfo(StringBuilder sb)
        {
            var gfx = GraphicsModeController.Instance;
            var lights = EnhancedLightSystem.Instance;
            if (gfx == null)
            {
                sb.AppendLine("graphics=none");
                return;
            }

            sb.AppendLine(
                $"graphics.mode={gfx.Current} profileLights={gfx.ActiveProfile.DynamicLights} " +
                $"error={(gfx.LastError ?? "-")}");
            if (lights == null)
            {
                sb.AppendLine("lights=none");
                return;
            }

            sb.AppendLine(
                $"lights.enabled={lights.IsProfileEnabled} active={lights.ActiveLightCount}/" +
                $"{lights.PoolCapacity} shadows={lights.ShadowCasterCount}/{lights.ShadowCapacity} " +
                $"requests={lights.RequestCount}");
        }

        void AppendNearbyEmissiveThings(StringBuilder sb, float radiusDU)
        {
            Vector3 p = transform.position;
            float radius = radiusDU * worldScale;
            float radiusSq = radius * radius;
            var billboards = FindObjectsByType<SpriteBillboard>(FindObjectsSortMode.None);
            var nearby = new List<(string name, int doomed, bool emits, float distanceSq, Vector3 pos)>();
            for (int i = 0; i < billboards.Length; i++)
            {
                var bb = billboards[i];
                if (bb == null) continue;
                Vector3 ip = bb.transform.position;
                Vector3 d = ip - p;
                float dSq = d.x * d.x + d.z * d.z;
                if (dSq > radiusSq) continue;
                if (!TryParseThingDoomedNum(bb.gameObject.name, out int doomed)) continue;
                bool emits = EnhancedEmissionTable.Contains(doomed);
                // Keep emissive hits plus a few nearest decorations for context.
                nearby.Add((HierarchyPath(bb.transform), doomed, emits, dSq, ip));
            }

            nearby.Sort((a, b) =>
            {
                int emitCmp = b.emits.CompareTo(a.emits);
                return emitCmp != 0 ? emitCmp : a.distanceSq.CompareTo(b.distanceSq);
            });

            int emissiveCount = 0;
            for (int i = 0; i < nearby.Count; i++)
                if (nearby[i].emits) emissiveCount++;

            sb.AppendLine(
                $"nearby.emissive={emissiveCount} decorations={nearby.Count} radiusDU={radiusDU:F0} " +
                $"(dynamic lights attach only to EnhancedEmissionTable types)");
            int count = System.Math.Min(nearby.Count, 16);
            for (int i = 0; i < count; i++)
            {
                var n = nearby[i];
                sb.AppendLine(
                    $"  thing[{i}] doomed={n.doomed} emit={n.emits} " +
                    $"xyDU={Mathf.Sqrt(n.distanceSq) / worldScale:F1} " +
                    $"dyDU={(n.pos.y - p.y) / worldScale:F1} " +
                    $"pos=({n.pos.x / worldScale:F1}, {n.pos.z / worldScale:F1}, y={n.pos.y / worldScale:F1}) " +
                    $"object={n.name}");
            }
        }

        static bool TryParseThingDoomedNum(string goName, out int doomed)
        {
            doomed = 0;
            // ThingSpawner names: Thing_{type}_{SPRITE}
            if (string.IsNullOrEmpty(goName) || !goName.StartsWith("Thing_")) return false;
            int a = "Thing_".Length;
            int b = goName.IndexOf('_', a);
            if (b <= a) return false;
            return int.TryParse(goName.Substring(a, b - a), out doomed);
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
                // P_CrossSpecialLine ignores the crossing side for every walk
                // special except teleports (EV_Teleport: `if (side == 1) return`),
                // so W1/WR doors, lifts and floors fire from either side.
                if (sp.Category == SpecialCategory.Teleport &&
                    !CrossedFromFrontSide(from, ld)) continue;
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
                {
                    ChangeSwitchTexture(lineIndex, sp.Repeatable);
                    // Vanilla p_switch.c: the S1 exit (special 11) clicks with
                    // swtchx, every other switch with swtchn.
                    string cue = ld.Special == 11 &&
                                 sound?.Cache != null && sound.Cache.Get("DSSWTCHX") != null
                        ? "DSSWTCHX" : "DSSWTCHN";
                    sound?.PlayAt(cue, LineMidpoint(lineIndex));
                }

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

            if (sp.Category == SpecialCategory.Light)
            {
                System.Collections.Generic.IEnumerable<int> lightTargets =
                    ld.Tag == 0 ? SectorActions.FindManualDoorTarget(map, lineIndex)
                                : SectorActions.FindTaggedSectors(map, ld.Tag);
                lights?.ApplyLinedef(ld.Special, lightTargets);

                if (sp.Trigger == TriggerKind.Switch)
                {
                    ChangeSwitchTexture(lineIndex, sp.Repeatable);
                    sound?.PlayAt("DSSWTCHN", LineMidpoint(lineIndex));
                }

                if (!sp.Repeatable) onceFired[lineIndex] = true;
                return;
            }

            if (sp.Key != KeyKind.None)
            {
                // EV_VerticalDoor / EV_DoLockedDoor: `if (!player) return 0` —
                // a monster at a keyed door neither opens it nor grunts. The
                // key test and its 2D oof belong to the player alone.
                if (actor != TeleportActorKind.Player || monsterDoorUse) return;
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
            {
                ChangeSwitchTexture(lineIndex, sp.Repeatable);
                sound?.PlayAt("DSSWTCHN", LineMidpoint(lineIndex));
            }

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

        /// VDOORWAIT (150 tics) and PLATWAIT (3 × 35 tics) from p_doors.c / p_plats.c;
        /// shared with the save restore so a resumed cycle keeps the vanilla dwell.
        public const float DoorWaitSeconds = 4.3f;
        public const float LiftWaitSeconds = 3f;

        /// World position a mover's cues play from (floor / ceiling mesh centre).
        public Vector3 SectorSoundOrigin(int sector)
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
            if (CrusherRules.TryGet(sp.Type, out var crusher))
            {
                var existing = FindCrusher(sector);
                if (crusher.Behavior == CrusherBehavior.Stop)
                {
                    existing?.StopCrusher();
                    return;
                }
                if (existing != null)
                {
                    existing.ResumeCrusher();
                    moving[sector] = true;
                    return;
                }
                if (moving[sector]) return;
                float target = CrusherRules.TargetHeight(heights.FloorRaw(sector));
                var crusherMover = gameObject.AddComponent<SectorMover>();
                crusherMover.BeginCrusher(
                    heights, geometry, sector, target, crusher.SpeedUnitsPerSecond,
                    crusher.Cycles, crusher.SlowsWhenCrushing, worldScale,
                    onDone: () => moving[sector] = false,
                    sound: sound, silent: crusher.Silent,
                    soundOrigin: SectorSoundOrigin(sector));
                moving[sector] = true;
                return;
            }

            bool isDoor = sp.Category == SpecialCategory.Door ||
                          sp.Category == SpecialCategory.LockedDoor;
            if (moving[sector])
            {
                // EV_VerticalDoor on a manual (DR) door that already has a
                // thinker: closing → reopen; open/waiting → a PLAYER closes it
                // now (monsters only ever push doors open).
                if (isDoor && sp.Trigger == TriggerKind.Push)
                {
                    var door = FindDoorMover(sector);
                    if (door != null)
                    {
                        if (door.IsClosing) door.Reopen();
                        else if (!monsterDoorUse) door.CloseEarly();
                    }
                }
                return; // one mover per sector at a time; cleared on mover completion
            }

            float speed = SectorMover.SpeedUnitsPerSec(sp.Speed);
            var mover = gameObject.AddComponent<SectorMover>();

            if (isDoor)
            {
                int targetH = SectorActions.ComputeTargetHeight(map, heights, sector, sp.Target);
                bool cycle = DoorCycles(sp.Type);
                mover.Begin(heights, geometry, sector, SectorMover.Surface.Ceiling,
                            targetH, speed, cycle, waitSeconds: DoorWaitSeconds, onDone: () => moving[sector] = false,
                            sound, MoverSoundProfile.Door, SectorSoundOrigin(sector));
            }
            else if (sp.Category == SpecialCategory.Plat && sp.Direction == MoveDirection.Up)
            {
                // raiseAndChange / raiseToNearestAndChange: the floor rises once
                // and stays (the flat change is not modelled). These used to run
                // as down-wait-up lifts.
                int targetH = SectorActions.ComputeTargetHeight(map, heights, sector, sp.Target);
                mover.Begin(heights, geometry, sector, SectorMover.Surface.Floor,
                            targetH, speed, cycle: false, waitSeconds: 0f, onDone: () => moving[sector] = false,
                            sound, MoverSoundProfile.FloorOrLift, SectorSoundOrigin(sector));
            }
            else if (sp.Category == SpecialCategory.Plat)
            {
                int down = SectorActions.ComputeTargetHeight(map, heights, sector, TargetSpec.LowestNeighborFloor);
                mover.Begin(heights, geometry, sector, SectorMover.Surface.Floor,
                            down, speed, cycle: true, waitSeconds: LiftWaitSeconds, onDone: () => moving[sector] = false,
                            sound, MoverSoundProfile.FloorOrLift, SectorSoundOrigin(sector));
            }
            else if (sp.Category == SpecialCategory.Ceiling)
            {
                // EV_DoCeiling one-shots: to floor (41/43), 8 above floor (44/72),
                // to the highest neighbouring ceiling (40). Not crushers.
                int targetH = SectorActions.ComputeTargetHeight(map, heights, sector, sp.Target);
                mover.Begin(heights, geometry, sector, SectorMover.Surface.Ceiling,
                            targetH, speed, cycle: false, waitSeconds: 0f, onDone: () => moving[sector] = false,
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
                // build8 (7/8) climbs 8 units per step, turbo16 (100/127) 16.
                int stepUnits = sp.Type == 100 || sp.Type == 127 ? 16 : 8;
                var chain = SectorActions.BuildStairChain(map, heights, sector, stepUnits);
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

        /// p_doors.c door kinds that wait and close again (normal / blazeRaise,
        /// keyed DR included). Everything else — open-stay, close, close30 —
        /// is a one-shot move. `Repeatable` alone was wrong both ways: SR/WR
        /// open-stay doors (61, 86, 106, 113, 99, 134, 136) closed after the
        /// wait, while S1/W1 normal doors (29, 108, 111) never did.
        static bool DoorCycles(int type) => type switch
        {
            1 or 4 or 26 or 27 or 28 or 29 or 63 or 90 or 105 or 108 or 111 or 114 or 117 => true,
            _ => false,
        };

        SectorMover FindDoorMover(int sector)
        {
            foreach (var mover in GetComponents<SectorMover>())
                if (mover != null && mover.IsCycleDoor && mover.SectorIndex == sector)
                    return mover;
            return null;
        }

        SectorMover FindCrusher(int sector)
        {
            foreach (var mover in GetComponents<SectorMover>())
                if (mover != null && mover.IsCrusher && mover.SectorIndex == sector)
                    return mover;
            return null;
        }
    }
}
