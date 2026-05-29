using System.IO;
using UnityEngine;
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
        // Runs after scene load; creates a MapLoader if none exists in the scene,
        // so "hit Play" works even when the scene has no pre-wired MapLoader GO.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBootstrap()
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
                AddChild(go, "Floor",   sm.Floor,   floorMaterial,   ref bounds);
                AddChild(go, "Ceiling", sm.Ceiling, ceilingMaterial, ref bounds);
                AddChild(go, "Walls",   sm.Walls,   wallMaterial,    ref bounds);
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
            cc.height = 56f * worldScale;
            cc.radius = 16f * worldScale;
            cc.stepOffset = 24f * worldScale;
            cc.slopeLimit = 45f;
            cc.center = new Vector3(0f, cc.height * 0.5f, 0f);

            var cameraGO = new GameObject("PlayerCamera");
            cameraGO.transform.SetParent(player.transform, worldPositionStays: false);
            cameraGO.transform.localPosition = new Vector3(0f, 41f * worldScale, 0f);
            cameraGO.tag = "MainCamera";
            var cam = cameraGO.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 2000f;
            cam.fieldOfView = 75f;
            cameraGO.AddComponent<AudioListener>();

            var pc = player.AddComponent<PlayerController>();
            pc.SetCameraPivot(cameraGO.transform);
        }

        void AddChild(GameObject parent, string name, MeshData data,
                      Material material, ref Bounds? bounds)
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
            child.AddComponent<MeshCollider>().sharedMesh  = mesh;

            var b = mesh.bounds;
            bounds = bounds.HasValue ? Combine(bounds.Value, b) : b;
        }

        static Bounds Combine(Bounds a, Bounds b) { a.Encapsulate(b); return a; }

        void OnWarning(string msg) => Debug.LogWarning($"[Doom.Map] {msg}");
        void OnError(string msg)   => Debug.LogError  ($"[Doom.Map] {msg}");
    }
}
