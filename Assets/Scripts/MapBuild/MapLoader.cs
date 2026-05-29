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

        [SerializeField] Material floorMaterial;
        [SerializeField] Material ceilingMaterial;
        [SerializeField] Material wallMaterial;

        [Tooltip("After loading, move Main Camera to look down at map center")]
        [SerializeField] bool autoFitCamera = true;

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
                      $"{map.Sectors.Length} sectors");

            var root = new GameObject(map.Name);
            root.transform.SetParent(transform, worldPositionStays: false);

            var meshes = MapGeometryBuilder.Build(map);
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

            if (autoFitCamera && bounds.HasValue) FitCamera(bounds.Value);
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

        void FitCamera(Bounds b)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var center = b.center;
            float topY = b.max.y + Mathf.Max(b.size.x, b.size.z);
            cam.transform.position = new Vector3(center.x, topY, center.z);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.farClipPlane  = Mathf.Max(cam.farClipPlane, topY * 3f);
            cam.nearClipPlane = 0.1f;
        }

        void OnWarning(string msg) => Debug.LogWarning($"[Doom.Map] {msg}");
        void OnError(string msg)   => Debug.LogError  ($"[Doom.Map] {msg}");
    }
}
