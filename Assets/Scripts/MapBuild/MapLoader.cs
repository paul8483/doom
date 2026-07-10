using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Doom.Wad;
using Doom.Map;
using Doom.Graphics;
using Doom.Audio;
using Doom.Game;
using Doom.Things;

namespace Doom.MapBuild
{
    [AddComponentMenu("Doom/Map Loader")]
    public sealed class MapLoader : MonoBehaviour
    {
        [Tooltip("Path to WAD relative to StreamingAssets")]
        [SerializeField] string wadRelativePath = "wads/freedoom1.wad";

        [Tooltip("Map name (ExMy for DOOM 1, MAPxx for DOOM 2)")]
        [SerializeField] string mapName = "E1M1";

        /// PlayMode test hook: when set AND no active production session, Build()
        /// loads this map instead of the serialized mapName. Cleared by test teardown.
        public static string MapNameOverride;

        /// Map name actually loaded by the most recent Build().
        public string LoadedMapName { get; private set; }

        [Tooltip("DOOM unit × worldScale = Unity meter. 1/32 → player ~1.75m")]
        [SerializeField] float worldScale = 1f / 32f;

        [SerializeField] [Range(0f, 1f)] float sfxVolume = 1f;
        [SerializeField] [Range(0f, 1f)] float musicVolume = 0.55f;

        [SerializeField] Material floorMaterial;
        [SerializeField] Material ceilingMaterial;
        [SerializeField] Material wallMaterial;

        // Runtime sector-geometry registry (in-place rebuild on height changes).
        // Set during Build(); consumed by SectorMover/LineActivator in later tasks.
        public SectorGeometry Geometry { get; private set; }
        public RuntimeSectorHeights RuntimeHeights { get; private set; }

        /// Stage 6f SFX service. Created during Build() while the WAD is open.
        public SoundSystem Sound { get; private set; }

        /// Stage 6f music service. Created during Build() while the WAD is open.
        public MusicPlayer Music { get; private set; }

        /// Stage 7b UI patch textures. Built while the WAD is open; never re-reads it.
        public HudTextureCache HudTextures { get; private set; }

        // ── Auto-bootstrap ────────────────────────────────────────────────────
        // Creates a MapLoader if none exists in the scene, so "hit Play" works
        // even when the scene has no pre-wired MapLoader GO. Runs once after the
        // initial scene load AND on every subsequent scene load — the latter so
        // that loading the preview scene at runtime (e.g. via SceneManager.LoadScene
        // in a PlayMode test) re-bootstraps, since RuntimeInitializeOnLoadMethod
        // fires only once and would otherwise miss runtime scene swaps.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureLoader();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureLoader();

        static void EnsureLoader()
        {
            if (FindAnyObjectByType<MapLoader>() != null) return;
            var go = new GameObject("MapLoader (auto)");
            var loader = go.AddComponent<MapLoader>();
            loader.floorMaterial   = CreateBlockoutMaterial(new Color(0.227f, 0.227f, 0.227f));
            loader.ceilingMaterial = CreateBlockoutMaterial(new Color(0.333f, 0.333f, 0.333f));
            loader.wallMaterial    = CreateBlockoutMaterial(new Color(0.502f, 0.502f, 0.502f));
        }

        static Material CreateBlockoutMaterial(Color color)
        {
            var m = new Material(Shader.Find("Standard"));
            m.color = color;
            return m;
        }

        // ── MonoBehaviour lifecycle ───────────────────────────────────────────
        void Start()
        {
            var flow = GameFlowController.Ensure();
            MapLog.WarningHandler += OnWarning;
            MapLog.ErrorHandler   += OnError;
            try
            {
                if (GameFlowController.ShouldBuildMap())
                {
                    Build();
                    SettingsController.Ensure().ApplyLoadedSettings();
                    flow.NotifyLevelReady();
                }
                else
                {
                    LoadUiOnly();
                    SettingsController.Ensure().ApplyLoadedSettings();
                    flow.EnterMainMenu();
                }
            }
            finally
            {
                MapLog.WarningHandler -= OnWarning;
                MapLog.ErrorHandler   -= OnError;
            }
        }

        /// Decode HUD/menu patches without building map geometry (main-menu boot).
        void LoadUiOnly()
        {
            string path = Path.Combine(Application.streamingAssetsPath, wadRelativePath);
            if (!File.Exists(path))
            {
                Debug.LogError($"MapLoader: WAD not found at {path}");
                return;
            }

            using var wad = WadFile.Open(path);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));
            var uiCatalog = UiPatchCatalog.LoadStandard(wad, palette);
            HudTextures = new HudTextureCache(uiCatalog);
            LoadedMapName = null;
            Debug.Log("MapLoader: UI-only load for main menu");
        }

        // ── Build ─────────────────────────────────────────────────────────────
        void Build()
        {
            string path = Path.Combine(Application.streamingAssetsPath, wadRelativePath);
            if (!File.Exists(path))
            {
                Debug.LogError($"MapLoader: WAD not found at {path}");
                return;
            }

            using var wad = WadFile.Open(path);
            string loadName = ResolveMapName();
            var map = MapData.Load(wad, loadName);
            LoadedMapName = map.Name;
            Debug.Log($"MapLoader: loaded {map.Name} — " +
                      $"{map.Vertexes.Length} verts, {map.LineDefs.Length} lines, " +
                      $"{map.Sectors.Length} sectors, {map.Things.Length} things");

            var palette  = new Palette(wad.ReadLump("PLAYPAL"));
            var textures = TextureSet.Load(wad);
            var cache    = new TextureCache(wad, textures, palette);

            // Stage 7b: decode HUD/menu/intermission patches while the WAD is open.
            var uiCatalog = UiPatchCatalog.LoadStandard(wad, palette);
            HudTextures = new HudTextureCache(uiCatalog);

            // Stage 6f: decode DS* and copy music lumps while the WAD is open.
            var soundCache = new SoundCache(wad);
            foreach (string sfx in CollectSfxNames())
                soundCache.Get(sfx);
            Sound = gameObject.GetComponent<SoundSystem>() ?? gameObject.AddComponent<SoundSystem>();
            Sound.Init(soundCache, worldScale, volume: sfxVolume);

            Music = gameObject.GetComponent<MusicPlayer>() ?? gameObject.AddComponent<MusicPlayer>();
            InitMusic(wad, loadName);

            var root = new GameObject(map.Name);
            root.transform.SetParent(transform, worldPositionStays: false);

            // Build geometry with the RUNTIME heights so the initial build and any
            // later in-place rebuilds share ONE height source. RuntimeSectorHeights
            // initializes to the WAD heights and round-trips them exactly, so the
            // initial output is identical to the static-heights build.
            var runtimeHeights = new RuntimeSectorHeights(map);
            var polys = SectorPolygonBuilder.Build(map);
            var meshes = MapGeometryBuilder.Build(map, worldScale, textures, runtimeHeights);

            var sectorRoots = new Transform[map.Sectors.Length];
            Bounds? bounds = null;
            int builtSectors = 0;
            foreach (var sm in meshes)
            {
                if (!sm.HasAnyGeometry) continue;
                var go = new GameObject($"Sector_{sm.SectorIdx}");
                go.transform.SetParent(root.transform, worldPositionStays: false);
                sectorRoots[sm.SectorIdx] = go.transform;
                PopulateSectorRoot(go.transform, sm, cache, worldScale, ref bounds);
                builtSectors++;
            }
            Debug.Log($"MapLoader: built {builtSectors}/{meshes.Length} sectors");

            RuntimeHeights = runtimeHeights;
            // `cache` (TextureCache) supplies materials; `textures` (TextureSet) is
            // the ITextureSizeSource for wall-UV sizing — same source used by Build().
            Geometry = new SectorGeometry(map, polys, runtimeHeights, worldScale,
                                          cache, textures, sectorRoots);

            // ── Sprites (Stage 5) ─────────────────────────────────────────────
            // Created BEFORE SpawnPlayer so the player's weapon view can share the
            // same SpriteCache instance (viewmodel/effect sprites are pre-warmed
            // below while the WAD is still open).
            var spriteSet = SpriteSet.Load(wad);
            var spriteCache = new SpriteCache(wad, spriteSet, palette);

            // Pre-warm weapon/flash/effect sprites: the WAD closes at the end of
            // Build(), and WeaponView/HitEffect fetch these lazily at runtime.
            foreach (var (spr, frames) in new (string, int[])[]
            {
                ("PUNG", new[] { 0, 1, 2, 3 }), ("PISG", new[] { 0, 1, 2 }), ("PISF", new[] { 0 }),
                ("SHTG", new[] { 0, 1, 2, 3 }), ("SHTF", new[] { 0, 1 }),
                ("CHGG", new[] { 0, 1 }), ("CHGF", new[] { 0, 1 }),
                ("PUFF", new[] { 0, 1, 2, 3 }), ("BLUD", new[] { 0, 1, 2 }),
            })
                foreach (int f in frames) spriteCache.Get(spr, f, 0);

            SpawnPlayer(map, bounds, spriteCache);

            var registry = gameObject.GetComponent<WorldStateRegistry>()
                ?? gameObject.AddComponent<WorldStateRegistry>();

            var thingsRoot = new GameObject("Things");
            thingsRoot.transform.SetParent(root.transform, worldPositionStays: false);
            float fallbackY = bounds?.min.y ?? 0f;
            var playerGo = GameObject.Find("Player");
            int spawned = new ThingSpawner(spriteCache, worldScale, Sound)
                .SpawnAll(map, thingsRoot.transform, fallbackY, playerGo.transform);
            Debug.Log($"MapLoader: spawned {spawned} sprite things");

            LineActivator lineActivator = null;
            LevelStatsTracker tracker = null;
            if (playerGo != null)
            {
                lineActivator = playerGo.GetComponent<LineActivator>();
                var weapons = playerGo.GetComponent<PlayerWeapons>();
                if (weapons != null)
                {
                    var noise = gameObject.AddComponent<NoiseAlertSystem>();
                    noise.Init(map, runtimeHeights, weapons, playerGo.transform);
                }

                var floor = playerGo.GetComponent<FloorDamageSystem>();
                tracker = gameObject.GetComponent<LevelStatsTracker>()
                    ?? gameObject.AddComponent<LevelStatsTracker>();
                tracker.Init(map, floor);
            }

            int startSpawnId = GameSessionHost.Instance != null
                ? GameSessionHost.Instance.NextSpawnId
                : 0;
            registry.Bind(map, runtimeHeights, lineActivator, tracker, startSpawnId);

            var host = GameSessionHost.Ensure();
            host.EnsureWadIdentity(path);
            if (host.TryConsumePendingRestore(LoadedMapName, out SaveGame pending))
            {
                if (!WorldSnapshotRestore.TryApply(
                        pending, registry, this, spriteCache, worldScale, playerGo, Sound,
                        out string restoreError))
                {
                    Debug.LogError("MapLoader: restore failed: " + restoreError);
                }
                else
                {
                    host.SetNextSpawnId(registry.NextSpawnId);
                    host.SyncSpawnIdFrom(registry);
                }
            }

            // WAD closes when this method returns; further SoundCache misses must
            // not touch the disposed stream.
            soundCache.NotifyWadClosed();
        }

        void InitMusic(WadFile wad, string loadName)
        {
            if (!MusicLumpName.TryForMap(loadName, out string track))
            {
                Debug.LogWarning($"MapLoader: no music lump mapping for '{loadName}'");
                return;
            }

            try
            {
                byte[] mus = wad.ReadLump(track);
                byte[] genMidi = wad.ReadLump("GENMIDI");
                // Copy so MusicPlayer owns independent buffers after WAD close.
                var musCopy = new byte[mus.Length];
                System.Buffer.BlockCopy(mus, 0, musCopy, 0, mus.Length);
                var genCopy = new byte[genMidi.Length];
                System.Buffer.BlockCopy(genMidi, 0, genCopy, 0, genMidi.Length);
                Music.Init(musCopy, genCopy, track, musicVolume);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"MapLoader: music disabled ({track}): {e.Message}");
            }
        }

        /// Collects every DS* name referenced by gameplay tables for pre-warm.
        public static IEnumerable<string> CollectSfxNames()
        {
            var set = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            void Add(string name)
            {
                if (!string.IsNullOrEmpty(name)) set.Add(name.ToUpperInvariant());
            }

            void AddAll(IEnumerable<string> names)
            {
                if (names == null) return;
                foreach (string n in names) Add(n);
            }

            foreach (WeaponId id in System.Enum.GetValues(typeof(WeaponId)))
                Add(WeaponTable.Get(id).FireSound);

            foreach (PickupSoundKind kind in System.Enum.GetValues(typeof(PickupSoundKind)))
                Add(PickupSoundTable.LumpName(kind));

            // Player / sector / projectile fixed set.
            foreach (string n in new[]
            {
                "DSPLPAIN", "DSPLDETH", "DSPDIEHI", "DSNOWAY", "DSOOF",
                "DSDOROPN", "DSDORCLS", "DSSTNMOV", "DSPSTOP", "DSSWTCHN",
                "DSFIRSHT", "DSFIRXPL", "DSCLAW",
            })
                Add(n);

            foreach (int doomed in new[] { 3004, 9, 3001, 3002 })
            {
                if (!MonsterTable.TryGet(doomed, out MonsterDef def) || def.Sounds == null)
                    continue;
                AddAll(def.Sounds.Sight);
                Add(def.Sounds.Active);
                Add(def.Sounds.RangedAttack);
                Add(def.Sounds.MeleeAttack);
                Add(def.Sounds.Pain);
                AddAll(def.Sounds.Death);
            }

            return set;
        }

        // ── Player spawn ──────────────────────────────────────────────────────
        void SpawnPlayer(MapData map, Bounds? bounds, SpriteCache spriteCache)
        {
            Thing? start = null;
            foreach (var t in map.Things)
            {
                if (t.Type == 1) { start = t; break; }
            }
            Vector3 pos;
            float yaw;
            if (start.HasValue)
            {
                pos = new Vector3(start.Value.X * worldScale,
                                  (bounds?.max.y ?? 0f) + 5f,
                                  start.Value.Y * worldScale);
                yaw = 90f - start.Value.Angle;
            }
            else
            {
                Debug.LogWarning("MapLoader: no Player 1 start in THINGS; spawning at (0, top, 0)");
                pos = new Vector3(0f, (bounds?.max.y ?? 0f) + 5f, 0f);
                yaw = 0f;
            }

            var existingMain = Camera.main;
            if (existingMain != null && existingMain.gameObject.GetComponent<PlayerController>() == null)
            {
                Destroy(existingMain.gameObject);
            }

            var player = new GameObject("Player");
            player.transform.SetParent(transform, worldPositionStays: false);
            player.transform.position = pos;
            player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var cc = player.AddComponent<CharacterController>();
            // Полная высота DOOM-игрока (56 юнитов). Заклинивание о потолок снято
            // тем, что потолкам не навешивается коллайдер (см. AddChild), а не
            // укорачиванием капсулы.
            cc.height = 56f * worldScale;
            cc.radius = 16f * worldScale;
            cc.stepOffset = 24f * worldScale;
            cc.slopeLimit = 45f;
            // 0 (а не дефолтные 0.001 м), иначе у угла стены остаточный сдвиг за
            // кадр падает ниже порога, обнуляется, и игрока «прилипает» к углу.
            cc.minMoveDistance = 0f;
            cc.center = new Vector3(0f, cc.height * 0.5f, 0f);

            var cameraGO = new GameObject("PlayerCamera");
            cameraGO.transform.SetParent(player.transform, worldPositionStays: false);
            cameraGO.transform.localPosition = new Vector3(0f, 41f * worldScale, 0f);  // DOOM eye height
            cameraGO.tag = "MainCamera";
            var cam = cameraGO.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 2000f;
            cam.fieldOfView = 75f;
            cameraGO.AddComponent<AudioListener>();

            var pc = player.AddComponent<PlayerController>();
            pc.SetCameraPivot(cameraGO.transform);

            // Trigger handling lives on the player so transform.position tracks it
            // for Walk detection. Init with the runtime height/geometry registries
            // (set just before SpawnPlayer) and the player's camera transform.
            var activator = player.AddComponent<LineActivator>();
            activator.Init(map, RuntimeHeights, Geometry, worldScale, cameraGO.transform, Sound);

            // Health, floor damage, death/respawn, and WAD status bar (Stage 7b).
            var health = player.AddComponent<PlayerHealth>();

            var floorDamage = player.AddComponent<FloorDamageSystem>();
            floorDamage.Init(map, worldScale, health, cc);

            var death = player.AddComponent<PlayerDeathHandler>();
            death.Init(health, pc, activator, floorDamage, cc, pos, Quaternion.Euler(0f, yaw, 0f));

            // Carry the player smoothly while standing on a moving sector floor
            // (rising lifts/floors). Acts only when the floor under the player moved.
            var rider = player.AddComponent<PlayerLiftRider>();
            rider.Init(cc, worldScale);

            // Weapons and shooting (Stage 6c) + inventory / pickups (Stage 6e).
            var weapons = player.AddComponent<PlayerWeapons>();
            weapons.Init(spriteCache, worldScale, cameraGO.transform);
            var weaponView = cameraGO.AddComponent<WeaponView>();
            weaponView.Init(weapons, spriteCache, worldScale, cc);

            var inventory = player.AddComponent<PlayerInventory>();
            inventory.Init(health, weapons);
            weapons.SetInventory(inventory);
            activator.SetInventory(inventory);
            floorDamage.SetInventory(inventory);

            var hud = player.AddComponent<DoomHud>();
            hud.Init(health, weapons, inventory, HudTextures);
            death.Respawned += hud.OnRespawn;

            death.Respawned += weapons.ResetToStart;
            death.Respawned += inventory.OnRespawn;
            death.SetWeapons(weapons);

            if (Sound != null)
            {
                var playerSound = player.AddComponent<PlayerSoundController>();
                playerSound.Init(Sound, weapons, inventory, health);
            }

            ApplySessionCarry(health, weapons, inventory);

            // Ensure flow exists; NotifyLevelReady is called from Start after Build.
            GameFlowController.Ensure();
        }

        /// Active production session wins; otherwise MapNameOverride; else inspector field.
        string ResolveMapName()
        {
            var host = GameSessionHost.Instance;
            if (host != null && host.Session != null && host.Session.IsActive &&
                !string.IsNullOrEmpty(host.Session.CurrentMap))
                return host.Session.CurrentMap;

            if (!string.IsNullOrEmpty(MapNameOverride))
                return MapNameOverride;

            return mapName;
        }

        void ApplySessionCarry(PlayerHealth health, PlayerWeapons weapons, PlayerInventory inventory)
        {
            var host = GameSessionHost.Instance;
            if (host == null || host.Session == null || !host.Session.IsActive)
                return;
            // Pending full-world restore replaces carry-over entirely.
            if (host.PendingRestore != null) return;

            var carry = host.Session.Carry;
            if (carry == null) return;

            carry.ApplyTo(health.Model, weapons.Ammo, weapons.Loadout);
            // Keys and powers stay at spawn defaults (cleared on level advance).
            inventory.Keys.Reset();
            inventory.Powers.Reset();
        }

        enum ColliderMode { None, Render, ThickWall }

        // ── Shared sector-root population (initial build AND in-place rebuild) ─────

        /// Re-create the Floor/Ceiling/Wall child GameObjects under `sectorRoot`
        /// from `sm`, clearing any existing children first. NOTE: no longer called by
        /// SectorGeometry (runtime rebuilds now translate Floor/Ceiling and use
        /// RebuildSectorWalls to avoid destroying the persistent floor collider). Kept
        /// for completeness / potential full-rebuild callers. The static build path
        /// calls PopulateSectorRoot directly (children already empty).
        public static void RebuildSectorGameObjects(Transform sectorRoot, SectorMeshes sm,
                                                    TextureCache cache, float worldScale)
        {
            if (sectorRoot == null) return;
            // Clear existing Floor/Ceiling/Wall children.
            for (int i = sectorRoot.childCount - 1; i >= 0; i--)
                Destroy(sectorRoot.GetChild(i).gameObject);
            Bounds? ignore = null;
            PopulateSectorRoot(sectorRoot, sm, cache, worldScale, ref ignore);
        }

        /// Rebuild ONLY the wall children of a sector in place (Floor/Ceiling persist
        /// and are repositioned by SectorGeometry via transform). Destroys existing
        /// "Wall_*" children and recreates them from sm.Walls, mirroring the wall
        /// logic in PopulateSectorRoot (wi naming, Masked→collider mapping, LineRef).
        public static void RebuildSectorWalls(Transform sectorRoot, SectorMeshes sm,
                                              TextureCache cache, float worldScale)
        {
            if (sectorRoot == null) return;
            for (int i = sectorRoot.childCount - 1; i >= 0; i--)
            {
                var child = sectorRoot.GetChild(i);
                if (child.name.StartsWith("Wall_")) Destroy(child.gameObject);
            }
            int wi = 0;
            Bounds? ignore = null;
            foreach (var ws in sm.Walls)
            {
                if (ws.Mesh.IsEmpty) continue;
                var wall = AddChild(sectorRoot, $"Wall_{wi++}_{ws.Texture}", ws.Mesh,
                         cache.GetMaterial(ws.Texture, ws.Masked),
                         ws.Blocks ? ColliderMode.ThickWall : ColliderMode.None, worldScale, ref ignore);
                if (wall != null && ws.Blocks)
                    wall.AddComponent<LineRef>().SectorIndex = sm.SectorIdx;
            }
        }

        /// Build the Floor/Ceiling/Wall child GameObjects for one sector under
        /// `sectorRoot`. Shared by the initial Build() loop and RebuildSectorGameObjects
        /// so both paths produce identical GameObjects/meshes/colliders.
        static void PopulateSectorRoot(Transform sectorRoot, SectorMeshes sm,
                                       TextureCache cache, float worldScale, ref Bounds? bounds)
        {
            var floorGo = AddChild(sectorRoot, "Floor", sm.Floor, cache.GetMaterial(sm.FloorFlat, false),
                     ColliderMode.Render, worldScale, ref bounds);
            if (floorGo != null)
                floorGo.AddComponent<SectorRef>().SectorIndex = sm.SectorIdx;
            if (!sm.Ceiling.IsEmpty)
                AddChild(sectorRoot, "Ceiling", sm.Ceiling, cache.GetMaterial(sm.CeilingFlat, false),
                         ColliderMode.None, worldScale, ref bounds);

            int wi = 0;
            foreach (var ws in sm.Walls)
            {
                if (ws.Mesh.IsEmpty) continue;
                var wall = AddChild(sectorRoot, $"Wall_{wi++}_{ws.Texture}", ws.Mesh,
                         cache.GetMaterial(ws.Texture, ws.Masked),
                         ws.Blocks ? ColliderMode.ThickWall : ColliderMode.None, worldScale, ref bounds);
                // Tag the wall with its sector so the Use-raycast can resolve the
                // linedef (LineActivator narrows by sector, then nearest segment).
                // Re-created on every rebuild because this is the shared build path.
                if (wall != null && ws.Blocks)
                    wall.AddComponent<LineRef>().SectorIndex = sm.SectorIdx;
            }
        }

        static GameObject AddChild(Transform parent, string name, MeshData data,
                             Material material, ColliderMode collider, float worldScale,
                             ref Bounds? bounds)
        {
            if (data == null || data.IsEmpty) return null;

            var child = new GameObject(name);
            child.transform.SetParent(parent, worldPositionStays: false);

            var mesh = new Mesh();
            mesh.name = $"{parent.name}/{name}";
            mesh.indexFormat = data.Vertices.Length > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            var unityVerts = new Vector3[data.Vertices.Length];
            for (int i = 0; i < unityVerts.Length; i++)
                unityVerts[i] = new Vector3(
                    data.Vertices[i].X,
                    data.Vertices[i].Y,
                    data.Vertices[i].Z);
            mesh.vertices  = unityVerts;
            mesh.triangles = data.Triangles;

            if (data.Uv.Length == data.Vertices.Length)
            {
                var uvs = new Vector2[data.Uv.Length];
                for (int i = 0; i < uvs.Length; i++)
                    uvs[i] = new Vector2(data.Uv[i].X, data.Uv[i].Y);
                mesh.uv = uvs;
            }

            if (data.Colors.Length == data.Vertices.Length)
            {
                var colors = new Color[data.Colors.Length];
                for (int i = 0; i < colors.Length; i++)
                    colors[i] = new Color(data.Colors[i].X, data.Colors[i].Y, data.Colors[i].Z, 1f);
                mesh.colors = colors;
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            child.AddComponent<MeshFilter>().sharedMesh   = mesh;
            child.AddComponent<MeshRenderer>().sharedMaterial = material;
            // Потолки (None): коллайдера нет — в DOOM коллизия 2D, потолок не
            //   блокирует ходьбу (краш-потолки — Stage 6); с 3D-капсулой потолок
            //   упирался бы в макушку и клинил игрока в низких секторах.
            // Пол (Render): плоский меш годится как коллайдер.
            // Стены (ThickWall): объёмный коллайдер. Тонкий одинарный квад
            //   позволяет капсуле протиснуться сквозь и застрять ВНУТРИ узкой
            //   колонны (капсула шире зазора → её зажимает между противоположными
            //   гранями, нет направления выхода). Толстая плита это исключает.
            if (collider == ColliderMode.Render)
                child.AddComponent<MeshCollider>().sharedMesh = mesh;
            else if (collider == ColliderMode.ThickWall)
                child.AddComponent<MeshCollider>().sharedMesh =
                    BuildThickColliderMesh(data, 4f * worldScale);

            var b = mesh.bounds;
            bounds = bounds.HasValue ? Combine(bounds.Value, b) : b;
            return child;
        }

        static Bounds Combine(Bounds a, Bounds b) { a.Encapsulate(b); return a; }

        // Объёмный коллайдер из тонкого стенового меша: каждый треугольник
        // выдавливается на ±thickness/2 вдоль своей нормали (центрировано — не
        // зависит от направления обхода). Капсула не может пройти сквозь плиту и
        // застрять внутри тонкой колонны. Рендер-меш остаётся плоским.
        static Mesh BuildThickColliderMesh(MeshData data, float thickness)
        {
            var v = data.Vertices;
            var t = data.Triangles;
            float h = thickness * 0.5f;
            var verts = new System.Collections.Generic.List<Vector3>(t.Length * 2);
            var tris  = new System.Collections.Generic.List<int>(t.Length * 8);
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = ToVec(v[t[i]]);
                Vector3 b = ToVec(v[t[i + 1]]);
                Vector3 c = ToVec(v[t[i + 2]]);
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-12f) continue; // вырожденный — пропускаем
                n = n.normalized * h;
                int bi = verts.Count;
                verts.Add(a - n); verts.Add(b - n); verts.Add(c - n);  // 0,1,2 (back)
                verts.Add(a + n); verts.Add(b + n); verts.Add(c + n);  // 3,4,5 (front)
                tris.Add(bi + 3); tris.Add(bi + 4); tris.Add(bi + 5);  // front
                tris.Add(bi + 0); tris.Add(bi + 2); tris.Add(bi + 1);  // back
                tris.Add(bi + 0); tris.Add(bi + 1); tris.Add(bi + 4); tris.Add(bi + 0); tris.Add(bi + 4); tris.Add(bi + 3);
                tris.Add(bi + 1); tris.Add(bi + 2); tris.Add(bi + 5); tris.Add(bi + 1); tris.Add(bi + 5); tris.Add(bi + 4);
                tris.Add(bi + 2); tris.Add(bi + 0); tris.Add(bi + 3); tris.Add(bi + 2); tris.Add(bi + 3); tris.Add(bi + 5);
            }
            var m = new Mesh();
            m.indexFormat = verts.Count > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            return m;
        }

        static Vector3 ToVec(Float3 p) => new Vector3(p.X, p.Y, p.Z);

        void OnWarning(string msg) => Debug.LogWarning($"[Doom.Map] {msg}");
        void OnError(string msg)   => Debug.LogError  ($"[Doom.Map] {msg}");
    }
}
