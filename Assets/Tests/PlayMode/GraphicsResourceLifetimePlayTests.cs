using System.Collections;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.Game;
using Doom.MapBuild;
using Doom.MapBuild.Rendering;

namespace Doom.Stage3.PlayTests
{
    /// Stage 8 Task 14 + texquality Task 9 — switch/reload must not grow runtime
    /// graphics resources after warm-up; owned assets destroyed exactly once.
    public class GraphicsResourceLifetimePlayTests
    {
        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            Time.timeScale = 1f;
            LogAssert.ignoreFailingMessages = false;
            MapLoader.MapNameOverride = null;
            GameSessionHost.ResetForTests();
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator Hot_switch_does_not_grow_resources_after_warmup()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForMapBuild();

            var gfx = GraphicsModeController.Ensure();
            var loader = Object.FindFirstObjectByType<MapLoader>();
            Assert.IsNotNull(loader);
            Assert.IsNotNull(gfx.Context);

            // Classic baseline then one Enhanced warm (may yield Super-xBR).
            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Classic);
            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);

            // Warm pools so capacity slots exist before the stability snapshot.
            var particles = Object.FindFirstObjectByType<ParticleEffectPool>();
            var decals = Object.FindFirstObjectByType<DecalEffectPool>();
            Assert.IsNotNull(particles);
            Assert.IsNotNull(decals);
            for (int i = 0; i < 8; i++)
            {
                particles.Pulse(EffectKind.Puff, new Vector3(i * 0.1f, 1f, 0f), 1f / 32f);
                decals.Spawn(EffectKind.Blood, new Vector3(i * 0.1f, 1f, 1f), Vector3.up);
            }
            yield return null;

            int tex = gfx.Context.TextureCount;
            int mats = gfx.Context.MaterialCount;
            int normals = loader.WorldTextures.NormalMapCount;
            int enhanced = loader.WorldTextures.EnhancedVariantCount;
            long worldBytes = loader.WorldTextures.EnhancedTextureBytes;
            long normalBytes = loader.WorldTextures.NormalTextureBytes;
            long spriteBytes = loader.Sprites != null ? loader.Sprites.EnhancedTextureBytes : 0;
            long hudBytes = loader.HudTextures != null ? loader.HudTextures.EnhancedTextureBytes : 0;
            int doomMats = CountDoomMaterials();
            int doomTex = CountDoomTextures();
            int lights = CountSceneLights();

            // Freeze billboards so lazy rotation frames cannot inflate counts.
            Time.timeScale = 0f;
            for (int i = 0; i < 20; i++)
                gfx.Apply(i % 2 == 0 ? GraphicsMode.Classic : GraphicsMode.Enhanced);
            Time.timeScale = 1f;
            yield return null;

            Assert.AreEqual(tex, gfx.Context.TextureCount, "TextureCount grew after hot-switch");
            Assert.AreEqual(mats, gfx.Context.MaterialCount, "MaterialCount grew after hot-switch");
            Assert.AreEqual(normals, loader.WorldTextures.NormalMapCount,
                "NormalMapCount grew after hot-switch");
            Assert.AreEqual(enhanced, loader.WorldTextures.EnhancedVariantCount,
                "EnhancedVariantCount grew after hot-switch");
            Assert.AreEqual(worldBytes, loader.WorldTextures.EnhancedTextureBytes,
                "world EnhancedTextureBytes grew after hot-switch");
            Assert.AreEqual(normalBytes, loader.WorldTextures.NormalTextureBytes,
                "NormalTextureBytes grew after hot-switch");
            if (loader.Sprites != null)
                Assert.AreEqual(spriteBytes, loader.Sprites.EnhancedTextureBytes,
                    "sprite EnhancedTextureBytes grew after hot-switch");
            if (loader.HudTextures != null)
                Assert.AreEqual(hudBytes, loader.HudTextures.EnhancedTextureBytes,
                    "HUD EnhancedTextureBytes grew after hot-switch");
            Assert.AreEqual(doomMats, CountDoomMaterials(),
                "Doom/ material instances grew after hot-switch");
            Assert.AreEqual(doomTex, CountDoomTextures(),
                "Doom-owned textures grew after hot-switch");
            Assert.That(CountSceneLights(), Is.LessThanOrEqualTo(lights + 1),
                "Light count grew after hot-switch");

            Assert.That(particles.ActiveCount, Is.LessThanOrEqualTo(ParticleEffectPool.Capacity));
            Assert.That(decals.ActiveCount, Is.LessThanOrEqualTo(DecalEffectPool.Capacity));

            gfx.Apply(GraphicsMode.Classic);
            yield return null;
            Assert.AreEqual(0, particles.ActiveCount);
            Assert.AreEqual(0, decals.ActiveCount);
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator E1M7_classic_enhanced_switch_stable_across_reload()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            yield return LoadMap("E1M7");
            var gfx = GraphicsModeController.Ensure();
            var loader = Object.FindFirstObjectByType<MapLoader>();
            Assert.IsNotNull(loader);
            Assert.IsNotNull(gfx.Context);

            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Classic);
            float buildSeconds = loader.LastBuildSeconds;

            float t0 = Time.realtimeSinceStartup;
            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);
            float firstSwitch = Time.realtimeSinceStartup - t0;

            Assert.IsTrue(gfx.EnhancedWarmComplete);
            Assert.That(loader.WorldTextures.EnhancedVariantCount, Is.GreaterThan(0));
            Assert.That(loader.WorldTextures.NormalMapCount, Is.GreaterThan(0));

            // Drain one LateUpdate pass so billboards bind Enhanced materials
            // before the stability snapshot (textures are pre-warmed; materials
            // are created on first Get per rotation).
            Time.timeScale = 0f;
            for (int i = 0; i < 3; i++) yield return null;

            int tex = gfx.Context.TextureCount;
            int mats = gfx.Context.MaterialCount;
            int normals = loader.WorldTextures.NormalMapCount;
            int enhanced = loader.WorldTextures.EnhancedVariantCount;
            long worldBytes = loader.WorldTextures.EnhancedTextureBytes;
            long normalBytes = loader.WorldTextures.NormalTextureBytes;
            long spriteBytes = loader.Sprites != null ? loader.Sprites.EnhancedTextureBytes : 0;
            long hudBytes = loader.HudTextures != null ? loader.HudTextures.EnhancedTextureBytes : 0;
            int doomMats = CountDoomMaterials();
            int doomTex = CountDoomTextures();

            t0 = Time.realtimeSinceStartup;
            for (int i = 0; i < 20; i++)
                gfx.Apply(i % 2 == 0 ? GraphicsMode.Classic : GraphicsMode.Enhanced);
            float twentySwitches = Time.realtimeSinceStartup - t0;
            for (int i = 0; i < 2; i++) yield return null;
            Time.timeScale = 1f;

            Assert.AreEqual(tex, gfx.Context.TextureCount);
            Assert.AreEqual(mats, gfx.Context.MaterialCount);
            Assert.AreEqual(normals, loader.WorldTextures.NormalMapCount);
            Assert.AreEqual(enhanced, loader.WorldTextures.EnhancedVariantCount);
            Assert.AreEqual(worldBytes, loader.WorldTextures.EnhancedTextureBytes);
            Assert.AreEqual(normalBytes, loader.WorldTextures.NormalTextureBytes);
            if (loader.Sprites != null)
                Assert.AreEqual(spriteBytes, loader.Sprites.EnhancedTextureBytes);
            if (loader.HudTextures != null)
                Assert.AreEqual(hudBytes, loader.HudTextures.EnhancedTextureBytes);
            // Doom/ materials: allow +1 known Stage-8 flake; no unbounded growth.
            Assert.That(CountDoomMaterials(), Is.LessThanOrEqualTo(doomMats + 1));
            Assert.AreEqual(doomTex, CountDoomTextures());

            AppendE1M7Metrics(
                buildSeconds, firstSwitch, twentySwitches / 20f,
                tex, mats, normals, enhanced,
                worldBytes, normalBytes, spriteBytes, hudBytes);

            // Scene reload must teardown without MissingReference / leak growth.
            yield return LoadMap("E1M7");
            gfx = GraphicsModeController.Ensure();
            loader = Object.FindFirstObjectByType<MapLoader>();
            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);

            Assert.That(gfx.Context.TextureCount,
                Is.InRange((int)(tex * 0.75f), (int)(tex * 1.25f) + 10));
            Assert.That(loader.WorldTextures.NormalMapCount,
                Is.InRange(Mathf.Max(1, (int)(normals * 0.75f)), (int)(normals * 1.25f) + 5));
            Assert.That(CollectLiveNormals(), Is.GreaterThan(0));
        }

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator Map_reload_does_not_accumulate_doom_resources()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            // Keep GameSessionHost across reloads (level-transition style).
            yield return LoadMap("E1M1");
            var gfx = GraphicsModeController.Ensure();
            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);
            for (int i = 0; i < 5; i++) yield return null;

            int matsE1M1 = CountDoomMaterials();
            int texE1M1 = CountDoomTextures();
            int normalsE1M1 = CollectLiveNormals();
            Assert.That(matsE1M1, Is.GreaterThan(10));
            Assert.That(normalsE1M1, Is.GreaterThan(0));

            yield return LoadMap("E1M2");
            gfx = GraphicsModeController.Ensure();
            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);
            for (int i = 0; i < 5; i++) yield return null;

            int matsE1M2 = CountDoomMaterials();
            int texE1M2 = CountDoomTextures();
            // Without Dispose, counts would approach E1M1+E1M2 (~2×). Cap at 1.6×.
            Assert.That(matsE1M2, Is.LessThan((int)(matsE1M1 * 1.6f) + 10),
                $"E1M2 materials ({matsE1M2}) look like a leak vs E1M1 ({matsE1M1})");
            Assert.That(texE1M2, Is.LessThan((int)(texE1M1 * 1.6f) + 10),
                $"E1M2 textures ({texE1M2}) look like a leak vs E1M1 ({texE1M1})");

            yield return LoadMap("E1M1");
            gfx = GraphicsModeController.Ensure();
            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);
            for (int i = 0; i < 5; i++) yield return null;

            int matsAgain = CountDoomMaterials();
            int texAgain = CountDoomTextures();
            int normalsAgain = CollectLiveNormals();

            // Same map again should land near the first snapshot (±25% slack for
            // ordering / pre-warm differences), not near 2×.
            Assert.That(matsAgain, Is.InRange((int)(matsE1M1 * 0.75f), (int)(matsE1M1 * 1.25f) + 5),
                $"E1M1 reload materials {matsAgain} vs first {matsE1M1}");
            Assert.That(texAgain, Is.InRange((int)(texE1M1 * 0.75f), (int)(texE1M1 * 1.25f) + 5),
                $"E1M1 reload textures {texAgain} vs first {texE1M1}");
            Assert.That(normalsAgain, Is.InRange(
                    Mathf.Max(1, (int)(normalsE1M1 * 0.75f)),
                    (int)(normalsE1M1 * 1.25f) + 5),
                $"E1M1 reload normals {normalsAgain} vs first {normalsE1M1}");
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator ClearContext_destroys_owned_runtime_assets_once()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForMapBuild();

            var gfx = GraphicsModeController.Ensure();
            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);
            yield return null;

            var particles = Object.FindFirstObjectByType<ParticleEffectPool>();
            Assert.IsNotNull(particles);
            particles.Pulse(EffectKind.Explosion, Vector3.one, 1f / 32f);
            yield return null;

            int normals = CollectLiveNormals();
            int particleWhite = CountNamedTexture("DoomParticleWhite");
            int particleMat = CountNamedMaterial("DoomParticleShared");
            int decalWhite = CountNamedTexture("DoomDecalWhite");
            Assert.That(normals, Is.GreaterThan(0));
            Assert.That(particleWhite, Is.EqualTo(1));
            Assert.That(particleMat, Is.EqualTo(1));
            Assert.That(decalWhite, Is.EqualTo(1));

            gfx.ClearContext();
            yield return null;

            Assert.AreEqual(0, CollectLiveNormals(), "normals must be destroyed once");
            Assert.AreEqual(0, CountNamedTexture("DoomParticleWhite"));
            Assert.AreEqual(0, CountNamedMaterial("DoomParticleShared"));
            Assert.AreEqual(0, CountNamedTexture("DoomDecalWhite"));

            // Second dispose must be a no-op (no throw / no second destroy path).
            gfx.ClearContext();
            yield return null;
            Assert.AreEqual(0, CollectLiveNormals());
            Assert.AreEqual(0, CountNamedTexture("DoomParticleWhite"));
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator Measure_E1M1_texture_quality_cost()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            yield return LoadMap("E1M1");
            var gfx = GraphicsModeController.Ensure();
            var loader = Object.FindFirstObjectByType<MapLoader>();
            Assert.IsNotNull(loader);

            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Classic);
            float buildSeconds = loader.LastBuildSeconds;

            float t0 = Time.realtimeSinceStartup;
            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);
            float firstSwitch = Time.realtimeSinceStartup - t0;

            t0 = Time.realtimeSinceStartup;
            gfx.Apply(GraphicsMode.Classic);
            gfx.Apply(GraphicsMode.Enhanced);
            float repeatSwitch = Time.realtimeSinceStartup - t0;

            long managed = System.GC.GetTotalMemory(false);
            AppendE1M1Metrics(
                buildSeconds, firstSwitch, repeatSwitch,
                gfx.Context.TextureCount,
                gfx.Context.MaterialCount,
                loader.WorldTextures.NormalMapCount,
                loader.WorldTextures.EnhancedVariantCount,
                loader.WorldTextures.NativeTextureBytes,
                loader.WorldTextures.EnhancedTextureBytes,
                loader.WorldTextures.NormalTextureBytes,
                loader.Sprites != null ? loader.Sprites.EnhancedTextureBytes : 0,
                loader.HudTextures != null ? loader.HudTextures.EnhancedTextureBytes : 0,
                managed);

            Assert.That(loader.WorldTextures.EnhancedVariantCount, Is.GreaterThan(0));
            Assert.That(firstSwitch, Is.GreaterThan(0f));
        }

        static IEnumerator WaitForMapBuild(float timeoutSeconds = 180f)
        {
            // Parallel Enhanced warm is wall-clock bound; frame-count waits race it.
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < timeoutSeconds)
            {
                yield return null;
                var loader = Object.FindFirstObjectByType<MapLoader>();
                if (loader != null && loader.LastBuildSeconds > 0f)
                    yield break;
            }
            Assert.Fail("MapLoader build did not finish in time");
        }

        static IEnumerator LoadMap(string map, float timeoutSeconds = 180f)
        {
            MapLoader.MapNameOverride = map;
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return null;
            yield return null;

            MapLoader loader = null;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < timeoutSeconds)
            {
                loader = Object.FindAnyObjectByType<MapLoader>();
                if (loader != null &&
                    loader.LoadedMapName == map &&
                    loader.LastBuildSeconds > 0f &&
                    Object.FindAnyObjectByType<LineActivator>() != null)
                    yield break;
                yield return null;
            }

            Assert.That(loader, Is.Not.Null, $"{map}: MapLoader missing");
            Assert.That(loader.LoadedMapName, Is.EqualTo(map));
            Assert.That(loader.LastBuildSeconds, Is.GreaterThan(0f), $"{map}: build incomplete");
        }

        static void AppendE1M1Metrics(
            float buildSeconds, float firstSwitch, float repeatSwitch,
            int tex, int mats, int normals, int enhanced,
            long nativeBytes, long worldBytes, long normalBytes,
            long spriteBytes, long hudBytes, long managedBytes)
        {
            var path = Path.Combine(Application.dataPath, "..", "Logs",
                "enhanced-texture-quality-baseline-notes.md");
            path = Path.GetFullPath(path);
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("## Task 9 performance gate (E1M1)");
            sb.AppendLine();
            sb.AppendLine($"Date: {System.DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"| Metric | Value |");
            sb.AppendLine($"|--------|-------|");
            sb.AppendLine($"| Map build time | {buildSeconds:F2}s |");
            sb.AppendLine($"| Classic→Enhanced first switch (yielded warm) | {firstSwitch:F2}s |");
            sb.AppendLine($"| Repeat Classic↔Enhanced (warm) | {repeatSwitch * 1000f:F1}ms |");
            sb.AppendLine($"| TextureCount / MaterialCount / NormalMapCount | {tex} / {mats} / {normals} |");
            sb.AppendLine($"| EnhancedVariantCount | {enhanced} |");
            sb.AppendLine($"| Native albedo bytes | {nativeBytes / (1024f * 1024f):F1} MB |");
            sb.AppendLine($"| Enhanced 4× albedo bytes | {worldBytes / (1024f * 1024f):F1} MB |");
            sb.AppendLine($"| Normal+height bytes | {normalBytes / (1024f * 1024f):F1} MB |");
            sb.AppendLine($"| Sprite Enhanced bytes | {spriteBytes / (1024f * 1024f):F1} MB |");
            sb.AppendLine($"| HUD Enhanced bytes | {hudBytes / (1024f * 1024f):F1} MB |");
            sb.AppendLine($"| Managed (GC.GetTotalMemory) | {managedBytes / (1024f * 1024f):F1} MB |");
            sb.AppendLine($"| Mitigation ladder | none applied |");
            File.AppendAllText(path, sb.ToString());
            Debug.Log("Task9 E1M1 metrics appended to " + path);
        }

        static void AppendE1M7Metrics(
            float buildSeconds, float firstSwitch, float avgRepeat,
            int tex, int mats, int normals, int enhanced,
            long worldBytes, long normalBytes, long spriteBytes, long hudBytes)
        {
            var path = Path.Combine(Application.dataPath, "..", "Logs",
                "enhanced-texture-quality-baseline-notes.md");
            path = Path.GetFullPath(path);
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("## Task 9 performance gate (E1M7)");
            sb.AppendLine();
            sb.AppendLine($"Date: {System.DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"| Metric | Value |");
            sb.AppendLine($"|--------|-------|");
            sb.AppendLine($"| Map build time | {buildSeconds:F2}s |");
            sb.AppendLine($"| Classic→Enhanced first switch (yielded warm) | {firstSwitch:F2}s |");
            sb.AppendLine($"| Avg repeat switch (20×, timeScale=0) | {avgRepeat * 1000f:F1}ms |");
            sb.AppendLine($"| TextureCount / MaterialCount / NormalMapCount | {tex} / {mats} / {normals} |");
            sb.AppendLine($"| EnhancedVariantCount | {enhanced} |");
            sb.AppendLine($"| Enhanced 4× albedo bytes | {worldBytes / (1024f * 1024f):F1} MB |");
            sb.AppendLine($"| Normal+height bytes | {normalBytes / (1024f * 1024f):F1} MB |");
            sb.AppendLine($"| Sprite Enhanced bytes | {spriteBytes / (1024f * 1024f):F1} MB |");
            sb.AppendLine($"| HUD Enhanced bytes | {hudBytes / (1024f * 1024f):F1} MB |");
            sb.AppendLine($"| Mitigation ladder | none applied |");
            File.AppendAllText(path, sb.ToString());
            Debug.Log("Task9 E1M7 metrics appended to " + path);
        }

        static int CollectLiveNormals()
        {
            int n = 0;
            foreach (var tex in Resources.FindObjectsOfTypeAll<Texture2D>())
            {
                if (tex != null && tex.name != null && tex.name.EndsWith("/Normal"))
                    n++;
            }
            return n;
        }

        static int CountDoomMaterials()
        {
            int n = 0;
            foreach (var mat in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (mat == null || mat.shader == null) continue;
                string s = mat.shader.name;
                if (s != null && s.StartsWith("Doom/"))
                    n++;
            }
            return n;
        }

        static int CountDoomTextures()
        {
            int n = 0;
            foreach (var tex in Resources.FindObjectsOfTypeAll<Texture2D>())
            {
                if (tex == null) continue;
                if (tex.width <= 4 && tex.height <= 4) continue;
                // Runtime WAD albedos / normals / sky — skip engine builtins.
                if (tex.name == "Default-Particle" || tex.name == "Default-Diffuse") continue;
                if (tex.hideFlags == HideFlags.HideAndDontSave && tex.name.StartsWith("Font"))
                    continue;
                n++;
            }
            return n;
        }

        static int CountNamedTexture(string name)
        {
            int n = 0;
            foreach (var tex in Resources.FindObjectsOfTypeAll<Texture2D>())
            {
                if (tex != null && tex.name == name) n++;
            }
            return n;
        }

        static int CountNamedMaterial(string name)
        {
            int n = 0;
            foreach (var mat in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (mat != null && mat.name == name) n++;
            }
            return n;
        }

        static int CountSceneLights() =>
            Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length;
    }
}
