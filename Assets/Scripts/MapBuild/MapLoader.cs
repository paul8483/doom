using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Doom.Wad;
using Doom.Map;

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

            var root = new GameObject(map.Name);
            root.transform.SetParent(transform, worldPositionStays: false);

            var meshes = MapGeometryBuilder.Build(map, worldScale);
            Bounds? bounds = null;
            int builtSectors = 0;
            foreach (var sm in meshes)
            {
                if (!sm.HasAnyGeometry) continue;
                var go = new GameObject($"Sector_{sm.SectorIdx}");
                go.transform.SetParent(root.transform, worldPositionStays: false);
                AddChild(go, "Floor",   sm.Floor,   floorMaterial,   ColliderMode.Render, ref bounds);
                AddChild(go, "Ceiling", sm.Ceiling, ceilingMaterial, ColliderMode.None,   ref bounds);
                for (int wi = 0; wi < sm.Walls.Count; wi++)
                    AddChild(go, $"Walls_{wi}", sm.Walls[wi].Mesh, wallMaterial, ColliderMode.ThickWall, ref bounds);
                builtSectors++;
            }
            Debug.Log($"MapLoader: built {builtSectors}/{meshes.Length} sectors");

            SpawnPlayer(map, bounds);
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
        }

        enum ColliderMode { None, Render, ThickWall }

        void AddChild(GameObject parent, string name, MeshData data,
                      Material material, ColliderMode collider, ref Bounds? bounds)
        {
            if (data == null || data.IsEmpty) return;

            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, worldPositionStays: false);

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
