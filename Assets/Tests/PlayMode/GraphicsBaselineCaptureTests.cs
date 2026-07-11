using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.MapBuild;
using Doom.Game;

namespace Doom.Stage3.PlayTests
{
    /// <summary>
    /// Stage 8 Task 1 — deterministic Classic world captures + machine-readable
    /// metrics before URP migration. OnGUI/HUD is NOT captured (Camera.Render only).
    /// PNGs land under Logs/stage8-captures/ (gitignored).
    /// </summary>
    public class GraphicsBaselineCaptureTests
    {
        const int CaptureWidth = 1280;
        const int CaptureHeight = 720;
        const float CaptureFov = 75f;

        /// Fixed eye poses: map → (position, lookAt). Chosen near Player-1 starts so
        /// E1M1/E1M3/E1M7/E1M9 frames stay comparable across Classic→URP migrations.
        static readonly (string Map, Vector3 Pos, Vector3 LookAt)[] Poses =
        {
            ("E1M1", new Vector3(33.0f, 1.75f, -98.0f), new Vector3(40.0f, 1.5f, -90.0f)),
            ("E1M3", new Vector3(0.0f, 1.75f, 0.0f), new Vector3(8.0f, 1.5f, 8.0f)),
            ("E1M7", new Vector3(0.0f, 1.75f, 0.0f), new Vector3(10.0f, 1.5f, 0.0f)),
            ("E1M9", new Vector3(0.0f, 1.75f, 0.0f), new Vector3(8.0f, 1.5f, -8.0f)),
        };

        static string LogsDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
        static string CaptureDir => Path.Combine(LogsDir, "stage8-captures");
        static string MetricsPath => Path.Combine(CaptureDir, "metrics.txt");

        [SetUp]
        public void SetUp()
        {
            MapLoader.MapNameOverride = null;
            GameFlowController.ResetForTests();
            GameFlowController.AutoStartPlaying = true;
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
            MapLoader.MapNameOverride = null;
            GameFlowController.ResetForTests();
        }

        [UnityTest]
        public IEnumerator Capture_classic_baseline_E1_maps()
        {
            LogAssert.ignoreFailingMessages = true;
            Directory.CreateDirectory(CaptureDir);

            var sb = new StringBuilder();
            sb.AppendLine($"# Stage 8 Classic graphics baseline metrics");
            sb.AppendLine($"# utc={DateTime.UtcNow:o}");
            sb.AppendLine($"# unity={Application.unityVersion}");
            sb.AppendLine($"# platform={Application.platform}");
            sb.AppendLine($"# graphicsDevice={SystemInfo.graphicsDeviceName}");
            sb.AppendLine($"# graphicsApi={SystemInfo.graphicsDeviceType}");
            sb.AppendLine($"# colorSpace={QualitySettings.activeColorSpace}");
            sb.AppendLine($"# customRenderPipeline={(UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != null)}");
            sb.AppendLine($"# capture={CaptureWidth}x{CaptureHeight} fov={CaptureFov}");
            sb.AppendLine();

            foreach (var (map, fallbackPos, fallbackLook) in Poses)
            {
                GameFlowController.ResetForTests();
                GameFlowController.AutoStartPlaying = true;
                MapLoader.MapNameOverride = map;
                SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
                yield return null;
                yield return null;

                MapLoader loader = null;
                for (int i = 0; i < 180; i++)
                {
                    loader = UnityEngine.Object.FindAnyObjectByType<MapLoader>();
                    if (loader != null && loader.LoadedMapName == map &&
                        GameObject.Find("Player") != null)
                        break;
                    yield return null;
                }

                Assert.That(loader, Is.Not.Null, $"{map}: MapLoader missing");
                Assert.That(loader.LoadedMapName, Is.EqualTo(map));

                var player = GameObject.Find("Player");
                Assert.That(player, Is.Not.Null, $"{map}: Player missing");

                var pc = player.GetComponent<PlayerController>();
                if (pc != null) pc.enabled = false;

                var cam = Camera.main;
                Assert.That(cam, Is.Not.Null, $"{map}: Camera.main missing");

                // Prefer spawn pose (deterministic per map); fall back to table pose.
                Vector3 pos = player.transform.position + Vector3.up * (41f / 32f);
                Vector3 forward = player.transform.forward;
                Vector3 look = pos + forward * 8f;
                if (float.IsNaN(pos.x) || pos.y > 50f)
                {
                    pos = fallbackPos;
                    look = fallbackLook;
                }

                cam.fieldOfView = CaptureFov;
                cam.transform.position = pos;
                cam.transform.LookAt(look);
                yield return null;

                // Warm render + sample wall-clock of Camera.Render (batchmode-safe).
                CaptureTo(cam, Path.Combine(CaptureDir, $"{map}-warmup.png"),
                    CaptureWidth, CaptureHeight);
                float t0 = Time.realtimeSinceStartup;
                const int Samples = 8;
                for (int i = 0; i < Samples; i++)
                    CaptureTo(cam, null, CaptureWidth, CaptureHeight);
                float msPerFrame = (Time.realtimeSinceStartup - t0) * 1000f / Samples;

                string png = Path.Combine(CaptureDir, $"{map}-classic.png");
                CaptureTo(cam, png, CaptureWidth, CaptureHeight);
                AssertNonTrivialPng(png);

                long managedBytes = GC.GetTotalMemory(false);
                int meshFilters = UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Length;
                int renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Length;
                int colliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Length;
                int textures = CountLoadedTextures();

                sb.AppendLine(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0}: build={1:F3}s renderMs={2:F2} " +
                    "meshes={3} renderers={4} colliders={5} transforms={6} " +
                    "meshFilters={7} meshRenderers={8} liveColliders={9} " +
                    "textures={10} managedMB={11:F1} " +
                    "camPos={12} look={13} fov={14} " +
                    "png={15} bytes={16}",
                    map,
                    loader.LastBuildSeconds,
                    msPerFrame,
                    loader.LastMeshCount,
                    loader.LastMaterialCount,
                    loader.LastColliderCount,
                    loader.LastGameObjectCount,
                    meshFilters,
                    renderers,
                    colliders,
                    textures,
                    managedBytes / (1024f * 1024f),
                    Fmt(pos),
                    Fmt(look),
                    CaptureFov,
                    Path.GetFileName(png),
                    new FileInfo(png).Length));

                Debug.Log($"[8 baseline] {map} renderMs={msPerFrame:F2} png={png}");
            }

            File.WriteAllText(MetricsPath, sb.ToString());
            Debug.Log($"[8 baseline] wrote metrics: {MetricsPath}\n{sb}");
            TestContext.WriteLine(sb.ToString());
            Assert.That(File.Exists(MetricsPath), Is.True);
        }

        static string Fmt(Vector3 v) =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "({0:0.###},{1:0.###},{2:0.###})", v.x, v.y, v.z);

        static int CountLoadedTextures()
        {
            var all = Resources.FindObjectsOfTypeAll<Texture2D>();
            int n = 0;
            foreach (var t in all)
            {
                if (t == null) continue;
                // Skip built-in editor/engine textures by size heuristic for a stable count.
                if (t.width <= 4 && t.height <= 4) continue;
                n++;
            }
            return n;
        }

        static void CaptureTo(Camera cam, string pngPath, int w, int h)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            Texture2D tex = null;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                if (string.IsNullOrEmpty(pngPath)) return;

                Directory.CreateDirectory(Path.GetDirectoryName(pngPath));
                RenderTexture.active = rt;
                tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                if (tex != null) UnityEngine.Object.Destroy(tex);
                rt.Release();
                UnityEngine.Object.Destroy(rt);
            }
        }

        static void AssertNonTrivialPng(string pngPath)
        {
            Assert.That(File.Exists(pngPath), Is.True, $"PNG missing: {pngPath}");
            var bytes = File.ReadAllBytes(pngPath);
            Assert.That(bytes.Length, Is.GreaterThan(3000),
                $"PNG too small: {pngPath} ({bytes.Length} bytes)");

            var tex = new Texture2D(2, 2);
            tex.LoadImage(bytes);
            var px = tex.GetPixels32();
            UnityEngine.Object.Destroy(tex);
            var first = px[0];
            bool varied = false;
            for (int i = 1; i < px.Length; i++)
            {
                if (px[i].r != first.r || px[i].g != first.g || px[i].b != first.b)
                {
                    varied = true;
                    break;
                }
            }
            Assert.That(varied, Is.True, $"PNG flat color: {pngPath}");
        }
    }
}
