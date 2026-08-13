using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.Game;
using Doom.MapBuild;

namespace Doom.Stage3.PlayTests
{
    public class ExperimentalMonsterModelPlayTests
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

        static GameObject NewMonsterRoot(out SpriteBillboard bb)
        {
            var go = new GameObject("MonsterModelTest",
                typeof(MeshFilter), typeof(MeshRenderer));
            bb = go.AddComponent<SpriteBillboard>();
            return go;
        }

        [UnityTest]
        public IEnumerator Poss_attaches_and_follows_toggle_cascade()
        {
            var go = NewMonsterRoot(out var bb);
            var mr = go.GetComponent<MeshRenderer>();
            var model = ExperimentalMonsterModel.TryAttach(go, "POSS", 1f / 32f, bb);
            Assert.That(model, Is.Not.Null,
                "all 7 POSS frame meshes are in Resources — attach must succeed");

            var settings = SettingsController.Ensure();
            settings.ConfigureForTests(new SettingsStore(memory), display,
                new NoOpGraphicsModeAdapter());
            settings.SetGraphicsMode(GraphicsMode.Enhanced);
            settings.SetEnhanced3DObjects(true);
            yield return null;

            Assert.That(model.ModelVisible, Is.True, "Enhanced+3D On → mesh");
            Assert.That(mr.enabled, Is.False);
            Assert.That(bb.enabled, Is.False);

            settings.SetEnhanced3DObjects(false);
            yield return null;
            Assert.That(model.ModelVisible, Is.False, "3D Off → billboard");
            Assert.That(mr.enabled, Is.True);
            Assert.That(bb.enabled, Is.True);

            settings.SetGraphicsMode(GraphicsMode.Classic);
            yield return null;
            Assert.That(model.ModelVisible, Is.False, "Classic → billboard");

            settings.SetGraphicsMode(GraphicsMode.Enhanced);
            settings.SetEnhanced3DObjects(true);
            yield return null;
            Assert.That(model.ModelVisible, Is.True, "hot-switch back → mesh");

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Unknown_sprite_does_not_attach()
        {
            var go = NewMonsterRoot(out var bb);
            Assert.That(ExperimentalMonsterModel.TryAttach(go, "TROO", 1f / 32f, bb),
                Is.Null, "TROO has no accepted frame meshes yet");
            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Live_frames_swap_and_death_reverts_forever()
        {
            var go = NewMonsterRoot(out var bb);
            var mr = go.GetComponent<MeshRenderer>();
            var model = ExperimentalMonsterModel.TryAttach(go, "POSS", 1f / 32f, bb);
            Assert.That(model, Is.Not.Null);

            var settings = SettingsController.Ensure();
            settings.ConfigureForTests(new SettingsStore(memory), display,
                new NoOpGraphicsModeAdapter());
            settings.SetGraphicsMode(GraphicsMode.Enhanced);
            settings.SetEnhanced3DObjects(true);
            yield return null;
            Assert.That(model.ModelVisible, Is.True);

            // Walk/attack/pain frames stay on the mesh.
            foreach (int frame in new[] { 1, 2, 3, 4, 5, 6, 0 })
            {
                model.NotifyFrame(frame);
                Assert.That(model.CurrentFrameForTest, Is.EqualTo(frame));
                Assert.That(model.RevertedForTest, Is.False);
                Assert.That(model.ModelVisible, Is.True);
            }

            // First death frame (H = 7) → billboard, permanently.
            model.NotifyFrame(7);
            Assert.That(model.RevertedForTest, Is.True);
            Assert.That(model.ModelVisible, Is.False);
            Assert.That(mr.enabled, Is.True, "death shows billboard frames");
            Assert.That(bb.enabled, Is.True);

            // Toggle churn must not resurrect the mesh on a corpse.
            settings.SetEnhanced3DObjects(false);
            yield return null;
            settings.SetEnhanced3DObjects(true);
            yield return null;
            Assert.That(model.ModelVisible, Is.False, "corpse never returns to mesh");
            Assert.That(mr.enabled, Is.True);

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator E1m1_spawns_mesh_routed_poss_monster()
        {
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            for (int i = 0; i < 300; i++)
            {
                var flow = GameFlowController.Instance;
                if (flow != null && flow.State == GameFlowState.Playing &&
                    GameObject.Find("Player") != null)
                    break;
                yield return null;
            }

            var settings = SettingsController.Ensure();
            settings.ConfigureForTests(new SettingsStore(memory), display);
            settings.SetGraphicsMode(GraphicsMode.Enhanced);
            settings.SetEnhanced3DObjects(true);
            yield return null; yield return null;

            ExperimentalMonsterModel found = null;
            foreach (var m in Object.FindObjectsByType<ExperimentalMonsterModel>(
                         FindObjectsSortMode.None))
            {
                if (m != null && m.HasModel) { found = m; break; }
            }
            Assert.That(found, Is.Not.Null,
                "E1M1 must spawn at least one mesh-routed POSS zombieman");
            Assert.That(found.GetComponent<MonsterController>(), Is.Not.Null,
                "model rides the original monster root");
            Assert.That(found.ModelVisible, Is.True);

            settings.SetGraphicsMode(GraphicsMode.Classic);
            yield return null;
            Assert.That(found.ModelVisible, Is.False, "Classic → native billboard");
        }
    }
}
