using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.Game;
using Doom.Graphics;
using Doom.MapBuild;
using Doom.MapBuild.Rendering;
using Doom.Wad;

namespace Doom.Stage3.PlayTests
{
    /// Enhanced presentation cascade (mesh -> display redraw -> native) per
    /// lump. The user-facing Enhanced 2D mode (3D Objects toggle) was removed
    /// 2026-08-28: Enhanced IS the 3D presentation, Classic stays untouched.
    public class EnhancedPresentationPlayTests
    {
        MemorySettingsStorage memory;
        FakeDisplayAdapter display;

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            MapLoader.MapNameOverride = null;
            GameFlowController.ResetForTests();
            GameFlowController.AutoStartPlaying = true;
            memory = new MemorySettingsStorage();
            display = new FakeDisplayAdapter();
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            Time.timeScale = 1f;
            MapLoader.MapNameOverride = null;
            GameFlowController.ResetForTests();
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator Enhanced_shows_mesh_classic_untouched()
        {
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForPlaying();

            var settings = SettingsController.Ensure();
            settings.ConfigureForTests(new SettingsStore(memory), display);
            settings.SetGraphicsMode(GraphicsMode.Enhanced);
            yield return null; yield return null;

            var presentation = FindMeshPresentation();
            Assert.That(presentation, Is.Not.Null, "E1M1 should spawn a TRELLIS-routed thing");
            Assert.That(presentation.ModelVisible, Is.True, "Enhanced → mesh");

            var mr = presentation.GetComponent<MeshRenderer>();
            var bb = presentation.GetComponent<SpriteBillboard>();
            Assert.That(mr.enabled, Is.False);
            Assert.That(bb == null || !bb.enabled, Is.True);

            settings.SetGraphicsMode(GraphicsMode.Classic);
            yield return null; yield return null;

            Assert.That(presentation.ModelVisible, Is.False, "Classic → no mesh");
            Assert.That(mr.enabled, Is.True, "Classic → billboard");
        }

        [UnityTest]
        public IEnumerator Resolver_serves_redraw_only_below_a_mesh()
        {
            // Synthetic SHOTA0 pickup: single-frame, allowlisted, has mesh.
            var go = new GameObject("CascadeShot", typeof(MeshFilter), typeof(MeshRenderer));
            var bb = go.AddComponent<SpriteBillboard>();
            var presentation = ExperimentalPickupModel.TryAttach(go, 2001, 1f / 32f, bb);
            Assert.That(presentation, Is.Not.Null);

            var settings = SettingsController.Ensure();
            settings.ConfigureForTests(new SettingsStore(memory), display,
                new NoOpGraphicsModeAdapter());
            settings.SetGraphicsMode(GraphicsMode.Enhanced);
            yield return null;

            Assert.That(presentation.ModelVisible, Is.True, "Enhanced → mesh");
            Assert.That(Resources.Load<Texture2D>(
                    DisplayRedrawAllowlist.ResourcesPath("SHOTA0")),
                Is.Not.Null);

            // Pure cascade: a mesh wins; without one the redraw serves; an
            // animated lump with partial coverage stays native.
            Assert.That(ObjectPresentationResolver.Resolve(
                    GraphicsMode.Enhanced, hasMesh: true,
                    hasDisplayRedraw: true, isAnimated: false),
                Is.EqualTo(ObjectPresentation.Mesh));
            Assert.That(ObjectPresentationResolver.Resolve(
                    GraphicsMode.Enhanced, hasMesh: false,
                    hasDisplayRedraw: true, isAnimated: false),
                Is.EqualTo(ObjectPresentation.RedrawBillboard));
            Assert.That(ObjectPresentationResolver.Resolve(
                    GraphicsMode.Enhanced, hasMesh: false,
                    hasDisplayRedraw: true, isAnimated: true),
                Is.EqualTo(ObjectPresentation.NativeBillboard));
            Assert.That(ObjectPresentationResolver.Resolve(
                    GraphicsMode.Classic, hasMesh: true,
                    hasDisplayRedraw: true, isAnimated: false),
                Is.EqualTo(ObjectPresentation.NativeBillboard));

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Barrel_explode_hands_over_to_the_billboard()
        {
            var go = new GameObject("BarrelCascade",
                typeof(MeshFilter), typeof(MeshRenderer));
            var mr = go.GetComponent<MeshRenderer>();
            var bb = go.AddComponent<SpriteBillboard>();
            var presentation = ExperimentalPickupModel.TryAttach(
                go, 2035, 1f / 32f, bb);
            Assert.That(presentation, Is.Not.Null);

            var settings = SettingsController.Ensure();
            settings.ConfigureForTests(new SettingsStore(memory), display,
                new NoOpGraphicsModeAdapter());
            settings.SetGraphicsMode(GraphicsMode.Enhanced);
            yield return null;

            Assert.That(presentation.ModelVisible, Is.True);

            var col = go.AddComponent<CapsuleCollider>();
            var eh = go.AddComponent<EnemyHealth>();
            eh.Init(1, -1, bb, col, countKill: false, noBlood: true);
            var be = go.AddComponent<BarrelExplosion>();
            be.Init(bb, col, cache: null, worldScale: 1f / 32f, sound: null);
            eh.SetBarrel(be);

            eh.TakeDamage(1, DamageSource.Player());
            yield return null;

            Assert.That(presentation.ModelVisible, Is.False,
                "mesh hidden after explode");
            Assert.That(mr.enabled, Is.True, "BEXP on billboard");
            Assert.That(bb.enabled, Is.True);

            Object.Destroy(go);
            yield return null;
        }

        /// The billboard under a mesh carries the display redraw (the model
        /// hides it while the mesh shows), so fully covered animated lumps
        /// serve per-frame redraw textures with native-header placement.
        [UnityTest]
        public IEnumerator Fully_covered_animated_lumps_use_per_frame_redraws()
        {
            var settings = SettingsController.Ensure();
            settings.ConfigureForTests(new SettingsStore(memory), display,
                new NoOpGraphicsModeAdapter());
            settings.SetGraphicsMode(GraphicsMode.Enhanced);
            yield return null;

            string wadPath = Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            if (!File.Exists(wadPath))
                Assert.Ignore("freedoom1.wad missing");

            using var wad = WadFile.Open(wadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));
            var sprites = SpriteSet.Load(wad);
            var factory = new DoomMaterialFactory();
            factory.SetActiveProfile(GraphicsProfile.Enhanced);
            var cache = new SpriteCache(wad, sprites, palette, factory);

            foreach (string sprite in new[] { "ARM1", "BAR1" })
            {
                var nativeA = cache.WarmNativePickup(sprite, 0, 0);
                var nativeB = cache.WarmNativePickup(sprite, 1, 0);
                Assert.IsTrue(nativeA.IsValid, sprite);
                Assert.IsTrue(nativeB.IsValid, sprite);

                var a = cache.GetPickup(sprite, 0, 0);
                var b = cache.GetPickup(sprite, 1, 0);
                Assert.IsTrue(a.IsValid, sprite);
                Assert.IsTrue(b.IsValid, sprite);

                // Both frames covered → per-frame redraw textures, not native.
                Assert.AreNotSame(nativeA.Material.mainTexture, a.Material.mainTexture,
                    $"{sprite} frame A should serve the display redraw");
                Assert.AreNotSame(nativeB.Material.mainTexture, b.Material.mainTexture,
                    $"{sprite} frame B should serve the display redraw");
                Assert.AreNotSame(a.Material.mainTexture, b.Material.mainTexture,
                    $"{sprite} A/B redraws must be distinct (blink)");

                // Placement stays native-header based.
                Assert.AreEqual(nativeA.Width, a.Width, sprite);
                Assert.AreEqual(nativeA.Height, a.Height, sprite);
                Assert.AreEqual(nativeB.Width, b.Width, sprite);
                Assert.AreEqual(nativeB.Height, b.Height, sprite);
            }

            settings.SetGraphicsMode(GraphicsMode.Classic);
            yield return null;
        }

        static ExperimentalPickupModel FindMeshPresentation()
        {
            foreach (var p in Object.FindObjectsByType<ExperimentalPickupModel>(
                         FindObjectsSortMode.None))
            {
                if (p != null && p.HasModel) return p;
            }
            return null;
        }

        static IEnumerator WaitForPlaying()
        {
            for (int i = 0; i < 300; i++)
            {
                var flow = GameFlowController.Instance;
                if (flow != null && flow.State == GameFlowState.Playing &&
                    GameObject.Find("Player") != null &&
                    FindMeshPresentation() != null)
                    yield break;
                yield return null;
            }

            Assert.Fail("Timed out waiting for Playing with TRELLIS presentation");
        }
    }
}
