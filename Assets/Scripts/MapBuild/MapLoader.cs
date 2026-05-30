using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Doom.Wad;
using Doom.Map;
using Doom.Graphics;

namespace Doom.MapBuild
{
    [AddComponentMenu("Doom/Map Loader")]
    public sealed class MapLoader : MonoBehaviour
    {
        [Tooltip("Path to WAD relative to StreamingAssets")]
        [SerializeField] string wadRelativePath = "wads/freedoom1.wad";

        [Tooltip("Map name (ExMy for DOOM 1, MAPxx for DOOM 2)")]
        [SerializeField] string mapName = "E1M1";

        [Tooltip("DOOM unit × worldScale = Unity meter. 1/32 → player ~1.75m")]
        [SerializeField] float worldScale = 1f / 32f;

        [SerializeField] Material floorMaterial;
        [SerializeField] Material ceilingMaterial;
        [SerializeField] Material wallMaterial;

        // Runtime sector-geometry registry (in-place rebuild on height changes).
        // Set during Build(); consumed by SectorMover/LineActivator in later tasks.
        public SectorGeometry Geometry { get; private set; }
        public RuntimeSectorHeights RuntimeHeights { get; private set; }

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
            MapLog.WarningHandler += OnWarning;
            MapLog.ErrorHandler   += OnError;
            try   { Build(); }
            finally
            {
                MapLog.WarningHandler -= OnWarning;
                MapLog.ErrorHandler   -= OnError;
            }
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
            var map = MapData.Load(wad, mapName);
            Debug.Log($"MapLoader: loaded {map.Name} — " +
                      $"{map.Vertexes.Length} verts, {map.LineDefs.Length} lines, " +
                      $"{map.Sectors.Length} sectors, {map.Things.Length} things");

            var palette  = new Palette(wad.ReadLump("PLAYPAL"));
            var textures = TextureSet.Load(wad);
            var cache    = new TextureCache(wad, textures, palette);

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

            SpawnPlayer(map, bounds);

            // ── Sprites (Stage 5) ─────────────────────────────────────────────
            var spriteSet = SpriteSet.Load(wad);
            var spriteCache = new SpriteCache(wad, spriteSet, palette);
            var thingsRoot = new GameObject("Things");
            thingsRoot.transform.SetParent(root.transform, worldPositionStays: false);
            float fallbackY = bounds?.min.y ?? 0f;
            int spawned = new ThingSpawner(spriteCache, worldScale)
                .SpawnAll(map, thingsRoot.transform, fallbackY);
            Debug.Log($"MapLoader: spawned {spawned} sprite things");
        }

        // ── Player spawn ──────────────────────────────────────────────────────
        void SpawnPlayer(MapData map, Bounds? bounds)
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
            activator.Init(map, RuntimeHeights, Geometry, worldScale, cameraGO.transform);
        }

        enum ColliderMode { None, Render, ThickWall }

        // ── Shared sector-root population (initial build AND in-place rebuild) ─────

        /// Re-create the Floor/Ceiling/Wall child GameObjects under `sectorRoot`
        /// from `sm`, clearing any existing children first. Used by SectorGeometry
        /// to rebuild a sector in place when its runtime heights change. The static
        /// build path calls PopulateSectorRoot directly (children already empty).
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

        /// Build the Floor/Ceiling/Wall child GameObjects for one sector under
        /// `sectorRoot`. Shared by the initial Build() loop and RebuildSectorGameObjects
        /// so both paths produce identical GameObjects/meshes/colliders.
        static void PopulateSectorRoot(Transform sectorRoot, SectorMeshes sm,
                                       TextureCache cache, float worldScale, ref Bounds? bounds)
        {
            AddChild(sectorRoot, "Floor", sm.Floor, cache.GetMaterial(sm.FloorFlat, false),
                     ColliderMode.Render, worldScale, ref bounds);
            if (!sm.Ceiling.IsEmpty)
                AddChild(sectorRoot, "Ceiling", sm.Ceiling, cache.GetMaterial(sm.CeilingFlat, false),
                         ColliderMode.None, worldScale, ref bounds);

            int wi = 0;
            foreach (var ws in sm.Walls)
            {
                if (ws.Mesh.IsEmpty) continue;
                var wall = AddChild(sectorRoot, $"Wall_{wi++}_{ws.Texture}", ws.Mesh,
                         cache.GetMaterial(ws.Texture, ws.Masked),
                         ws.Masked ? ColliderMode.None : ColliderMode.ThickWall, worldScale, ref bounds);
                // Tag the wall with its sector so the Use-raycast can resolve the
                // linedef (LineActivator narrows by sector, then nearest segment).
                // Re-created on every rebuild because this is the shared build path.
                if (wall != null && !ws.Masked)
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
