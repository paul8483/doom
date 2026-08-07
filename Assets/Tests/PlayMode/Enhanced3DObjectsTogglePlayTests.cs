using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.Game;
using Doom.Graphics;
using Doom.MapBuild;

namespace Doom.Stage3.PlayTests
{
    public class Enhanced3DObjectsTogglePlayTests
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
        public IEnumerator Toggle_off_billboard_on_mesh_classic_untouched()
        {
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForPlaying();

            var settings = SettingsController.Ensure();
            settings.ConfigureForTests(new SettingsStore(memory), display);
            settings.SetGraphicsMode(GraphicsMode.Enhanced);
            settings.SetEnhanced3DObjects(true);
            yield return null; yield return null;

            var presentation = FindMeshPresentation();
            Assert.That(presentation, Is.Not.Null, "E1M1 should spawn a TRELLIS-routed thing");
            Assert.That(presentation.ModelVisible, Is.True, "3D On → mesh");

            var mr = presentation.GetComponent<MeshRenderer>();
            var bb = presentation.GetComponent<SpriteBillboard>();
            Assert.That(mr.enabled, Is.False);
            Assert.That(bb == null || !bb.enabled, Is.True);

            settings.SetEnhanced3DObjects(false);
            yield return null; yield return null;

            Assert.That(presentation.ModelVisible, Is.False, "3D Off → hide mesh");
            Assert.That(mr.enabled, Is.True, "3D Off → billboard renderer");
            if (bb != null) Assert.That(bb.enabled, Is.True);

            settings.SetGraphicsMode(GraphicsMode.Classic);
            yield return null; yield return null;

            Assert.That(presentation.ModelVisible, Is.False, "Classic → no mesh");
            Assert.That(mr.enabled, Is.True, "Classic → billboard");
        }

        [UnityTest]
        public IEnumerator Toggle_off_allowlisted_single_frame_uses_redraw_resource()
        {
            // Synthetic SHOTA0 pickup: single-frame, allowlisted, has mesh.
            var go = new GameObject("ToggleShot", typeof(MeshFilter), typeof(MeshRenderer));
            var bb = go.AddComponent<SpriteBillboard>();
            var presentation = ExperimentalPickupModel.TryAttach(go, 2001, 1f / 32f, bb);
            Assert.That(presentation, Is.Not.Null);

            var settings = SettingsController.Ensure();
            settings.ConfigureForTests(new SettingsStore(memory), display,
                new NoOpGraphicsModeAdapter());
            settings.SetGraphicsMode(GraphicsMode.Enhanced);
            settings.SetEnhanced3DObjects(false);
            yield return null;

            Assert.That(presentation.ModelVisible, Is.False);
            Assert.That(Resources.Load<Texture2D>(
                    DisplayRedrawAllowlist.ResourcesPath("SHOTA0")),
                Is.Not.Null);

            // Resolver: Enhanced + 3D Off + redraw + not animated → RedrawBillboard.
            Assert.That(ObjectPresentationResolver.Resolve(
                    GraphicsMode.Enhanced, false, true, true, false),
                Is.EqualTo(ObjectPresentation.RedrawBillboard));

            settings.SetEnhanced3DObjects(true);
            yield return null;
            Assert.That(presentation.ModelVisible, Is.True);

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Barrel_explode_uses_billboard_for_both_toggle_values()
        {
            foreach (bool toggle3D in new[] { true, false })
            {
                var go = new GameObject($"Barrel3D_{toggle3D}",
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
                settings.SetEnhanced3DObjects(toggle3D);
                yield return null;

                if (toggle3D)
                    Assert.That(presentation.ModelVisible, Is.True);
                else
                    Assert.That(presentation.ModelVisible, Is.False);

                var col = go.AddComponent<CapsuleCollider>();
                var eh = go.AddComponent<EnemyHealth>();
                eh.Init(1, -1, bb, col, countKill: false, noBlood: true);
                var be = go.AddComponent<BarrelExplosion>();
                be.Init(bb, col, cache: null, worldScale: 1f / 32f, sound: null);
                eh.SetBarrel(be);

                eh.TakeDamage(1, DamageSource.Player());
                yield return null;

                Assert.That(presentation.ModelVisible, Is.False,
                    $"toggle3D={toggle3D}: mesh hidden after explode");
                Assert.That(mr.enabled, Is.True,
                    $"toggle3D={toggle3D}: BEXP on billboard");
                Assert.That(bb.enabled, Is.True);

                Object.Destroy(go);
                yield return null;
            }
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
