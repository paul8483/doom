using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using Doom.Game;
using Doom.MapBuild;
using Doom.MapBuild.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Doom.Stage3.PlayTests
{
    /// Diagnostic captures for the controlled mip visual gate. PNGs are gitignored.
    public class ControlledMipCaptureTests
    {
        const int Width = 1280;
        const int Height = 720;
        static readonly string[] Maps = { "E1M1", "E1M3", "E1M7" };

        static string CaptureDir => Path.GetFullPath(Path.Combine(
            Application.dataPath, "..", "Logs", "controlled-mip-captures"));

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
            MapLoader.MapNameOverride = null;
            GameFlowController.ResetForTests();
        }

        [UnityTest]
        public IEnumerator Capture_enhanced_close_mid_and_oblique_E1_maps()
        {
            LogAssert.ignoreFailingMessages = true;
            Directory.CreateDirectory(CaptureDir);
            var metrics = new StringBuilder();
            metrics.AppendLine("# Controlled texture mip captures");
            metrics.AppendLine($"# utc={DateTime.UtcNow:o}");
            metrics.AppendLine($"# unity={Application.unityVersion}");
            metrics.AppendLine($"# gpu={SystemInfo.graphicsDeviceName}");

            foreach (string map in Maps)
            {
                GameFlowController.ResetForTests();
                GameFlowController.AutoStartPlaying = true;
                MapLoader.MapNameOverride = map;
                SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);

                MapLoader loader = null;
                for (int i = 0; i < 180; i++)
                {
                    loader = UnityEngine.Object.FindAnyObjectByType<MapLoader>();
                    if (loader != null && loader.LoadedMapName == map &&
                        GameObject.Find("Player") != null)
                        break;
                    yield return null;
                }

                Assert.IsNotNull(loader, $"{map}: MapLoader missing");
                var player = GameObject.Find("Player");
                Assert.IsNotNull(player, $"{map}: Player missing");
                var controller = player.GetComponent<PlayerController>();
                if (controller != null) controller.enabled = false;

                var graphics = GraphicsModeController.Ensure();
                graphics.Apply(GraphicsMode.Enhanced);
                yield return null;
                Assert.IsNull(graphics.LastError, graphics.LastError);

                // Apply can run pending session/profile callbacks; reacquire scene objects.
                player = GameObject.Find("Player");
                Assert.IsNotNull(player, $"{map}: Player missing after Enhanced apply");
                controller = player.GetComponent<PlayerController>();
                if (controller != null) controller.enabled = false;
                var camera = Camera.main;
                Assert.IsNotNull(camera, $"{map}: Camera.main missing");
                Vector3 origin = player.transform.position + Vector3.up * (41f / 32f);
                Vector3 forward = player.transform.forward;
                Vector3 target = origin + forward * 8f;
                Vector3 close = origin;
                Vector3 oblique = origin;

                if (Physics.Raycast(origin, forward, out RaycastHit hit, 20f,
                    ~0, QueryTriggerInteraction.Ignore))
                {
                    target = hit.point;
                    close = hit.point - forward * 0.8f;
                    oblique = close - player.transform.right * 1.25f - forward * 0.35f;
                }
                else
                {
                    oblique = origin;
                }

                float renderMs = CapturePose(camera, map, "mid", origin, target, 75f);
                CapturePose(camera, map, "close", close, target, 55f);
                if (oblique == origin)
                    target = origin + Quaternion.Euler(0f, 45f, 0f) * forward * 8f;
                CapturePose(camera, map, "oblique", oblique, target, 75f);

                metrics.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}: build={1:F3}s renderMs={2:F2} variants={3} bytes={4} normals={5} managedMB={6:F1}",
                    map, loader.LastBuildSeconds, renderMs,
                    loader.WorldTextures.EnhancedVariantCount,
                    loader.WorldTextures.EnhancedTextureBytes,
                    loader.WorldTextures.NormalMapCount,
                    GC.GetTotalMemory(false) / (1024f * 1024f)));
            }

            File.WriteAllText(Path.Combine(CaptureDir, "metrics.txt"), metrics.ToString());
        }

        static float CapturePose(
            Camera camera, string map, string pose, Vector3 position, Vector3 target, float fov)
        {
            camera.fieldOfView = fov;
            camera.transform.position = position;
            camera.transform.LookAt(target);

            string path = Path.Combine(CaptureDir, $"{map}-{pose}.png");
            float start = Time.realtimeSinceStartup;
            CaptureTo(camera, path);
            float elapsedMs = (Time.realtimeSinceStartup - start) * 1000f;
            Assert.That(new FileInfo(path).Length, Is.GreaterThan(4096), path);
            return elapsedMs;
        }

        static void CaptureTo(Camera camera, string path)
        {
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
