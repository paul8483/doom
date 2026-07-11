using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.Game;
using Doom.MapBuild;
using Doom.MapBuild.Rendering;

namespace Doom.Stage3.PlayTests
{
    /// Stage 8 Task 14 — switch/reload must not grow runtime graphics resources
    /// after warm-up; owned assets are destroyed exactly once on context teardown.
    public class GraphicsResourceLifetimePlayTests
    {
        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
            MapLoader.MapNameOverride = null;
            GameSessionHost.ResetForTests();
        }

        [UnityTest]
        public IEnumerator Hot_switch_does_not_grow_resources_after_warmup()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            for (int i = 0; i < 90; i++) yield return null;

            var gfx = GraphicsModeController.Ensure();
            var loader = Object.FindFirstObjectByType<MapLoader>();
            Assert.IsNotNull(loader);
            Assert.IsNotNull(gfx.Context);

            gfx.Apply(GraphicsMode.Enhanced);
            yield return null;

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
            int doomMats = CountDoomMaterials();
            int doomTex = CountDoomTextures();
            int lights = CountSceneLights();

            for (int i = 0; i < 20; i++)
            {
                gfx.Apply(i % 2 == 0 ? GraphicsMode.Classic : GraphicsMode.Enhanced);
                yield return null;
            }

            Assert.AreEqual(tex, gfx.Context.TextureCount, "TextureCount grew after hot-switch");
            Assert.AreEqual(mats, gfx.Context.MaterialCount, "MaterialCount grew after hot-switch");
            Assert.AreEqual(normals, loader.WorldTextures.NormalMapCount,
                "NormalMapCount grew after hot-switch");
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
        public IEnumerator Map_reload_does_not_accumulate_doom_resources()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            // Keep GameSessionHost across reloads (level-transition style).
            yield return LoadMap("E1M1");
            var gfx = GraphicsModeController.Ensure();
            gfx.Apply(GraphicsMode.Enhanced);
            for (int i = 0; i < 5; i++) yield return null;

            int matsE1M1 = CountDoomMaterials();
            int texE1M1 = CountDoomTextures();
            int normalsE1M1 = CollectLiveNormals();
            Assert.That(matsE1M1, Is.GreaterThan(10));
            Assert.That(normalsE1M1, Is.GreaterThan(0));

            yield return LoadMap("E1M2");
            gfx = GraphicsModeController.Ensure();
            gfx.Apply(GraphicsMode.Enhanced);
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
            gfx.Apply(GraphicsMode.Enhanced);
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
        public IEnumerator ClearContext_destroys_owned_runtime_assets_once()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            for (int i = 0; i < 90; i++) yield return null;

            var gfx = GraphicsModeController.Ensure();
            gfx.Apply(GraphicsMode.Enhanced);
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

        static IEnumerator LoadMap(string map)
        {
            MapLoader.MapNameOverride = map;
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return null;
            yield return null;

            MapLoader loader = null;
            for (int i = 0; i < 180; i++)
            {
                loader = Object.FindAnyObjectByType<MapLoader>();
                if (loader != null && loader.LoadedMapName == map &&
                    Object.FindAnyObjectByType<LineActivator>() != null)
                    yield break;
                yield return null;
            }

            Assert.That(loader, Is.Not.Null, $"{map}: MapLoader missing");
            Assert.That(loader.LoadedMapName, Is.EqualTo(map));
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
