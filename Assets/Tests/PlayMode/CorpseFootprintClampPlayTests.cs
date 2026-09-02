using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.Game;
using Doom.MapBuild;

namespace Doom.Stage3.PlayTests
{
    /// The E1M3 lift sergeant (slot-0 save, 2026-09-02): a corpse whose centre
    /// sits 0.5 units inside the lift's south edge. Its 3D slab used to hang
    /// half over the pit and got sliced by the shaft as the lift rose. The
    /// runtime clamp reads the sector under the thing from the live scene and
    /// slides the mesh pivot north onto the lift; the thing itself stays put.
    public class CorpseFootprintClampPlayTests
    {
        const float WorldScale = 1f / 32f;
        // DOOM (-1374.2, 1696.5) on the lift's WAD floor (128), facing 252.2°.
        static readonly Vector3 CorpseOrigin =
            new Vector3(-1374.2f * WorldScale, 128f * WorldScale, 1696.5f * WorldScale);
        const float CorpseAngle = 252.2f;

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

        // Map override must be set AFTER ResetForTests (GameSessionHost clears it).
        static IEnumerator LoadLevel(string mapName)
        {
            MapLoader.MapNameOverride = mapName;
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return null; yield return null;
            for (int i = 0; i < 90; i++) yield return null;
            Time.captureDeltaTime = 1f / 60f;
        }

        [UnityTest]
        public IEnumerator Lift_edge_corpse_resolves_a_northward_shift_from_the_live_scene()
        {
            yield return LoadLevel("E1M3");
            Assert.That(WorldStateRegistry.Instance, Is.Not.Null);
            Assert.That(WorldStateRegistry.Instance.Map.Name, Is.EqualTo("E1M3"));

            float yaw = 90f - CorpseAngle;
            Vector3 shift = CorpseFootprintClamp.Resolve(
                CorpseOrigin, yaw, 25f * WorldScale, 23.5f * WorldScale, WorldScale);

            Assert.That(shift.z, Is.GreaterThan(20f * WorldScale),
                "the slab hangs over the lift's south edge (line 261) and must move north");
            Assert.That(shift.y, Is.EqualTo(0f));
            Assert.That(shift.magnitude,
                Is.LessThanOrEqualTo(CorpseFootprintClamp.MaxShiftDoomUnits * WorldScale + 1e-4f));
        }

        [UnityTest]
        public IEnumerator Restored_corpse_snaps_its_pivot_inside_the_lift()
        {
            yield return LoadLevel("E1M3");

            var go = new GameObject("LiftCorpse", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.position = CorpseOrigin;
            var bb = go.AddComponent<SpriteBillboard>();
            bb.SetDoomAngle(CorpseAngle);
            var model = ExperimentalMonsterModel.TryAttach(go, "SPOS", WorldScale, bb);
            Assert.That(model, Is.Not.Null, "SPOS ships every live and death mesh");

            var settings = SettingsController.Ensure();
            settings.ConfigureForTests(new SettingsStore(memory), display,
                new NoOpGraphicsModeAdapter());
            settings.SetGraphicsMode(GraphicsMode.Enhanced);
            yield return null;
            Assert.That(model.ModelVisible, Is.True);

            // Save restore hands the corpse frame straight in: no fall to
            // slide through, so the pivot lands at rest immediately.
            model.NotifyFrame(11);
            Assert.That(model.RestOffsetForTest.z, Is.GreaterThan(20f * WorldScale));
            Assert.That(model.RestFractionForTest, Is.EqualTo(1f));
            yield return null;

            Transform pivot = go.transform.Find("Enhanced3DMonster");
            Assert.That(pivot, Is.Not.Null);
            Vector3 delta = pivot.position - go.transform.position;
            Assert.That(delta.z, Is.EqualTo(model.RestOffsetForTest.z).Within(1e-4f),
                "the pivot, not the thing, carries the shift");
            Assert.That(go.transform.position, Is.EqualTo(CorpseOrigin),
                "gameplay origin untouched");

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Live_kill_slides_the_body_across_the_death_chain()
        {
            yield return LoadLevel("E1M3");

            var go = new GameObject("LiftKill", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.position = CorpseOrigin;
            var bb = go.AddComponent<SpriteBillboard>();
            bb.SetDoomAngle(CorpseAngle);
            var model = ExperimentalMonsterModel.TryAttach(go, "SPOS", WorldScale, bb);
            Assert.That(model, Is.Not.Null);

            var settings = SettingsController.Ensure();
            settings.ConfigureForTests(new SettingsStore(memory), display,
                new NoOpGraphicsModeAdapter());
            settings.SetGraphicsMode(GraphicsMode.Enhanced);
            yield return null;

            model.NotifyDeathStarted(extremeDeath: false);
            model.NotifyFrame(7); // H0: first fall frame
            Assert.That(model.RestOffsetForTest.z, Is.GreaterThan(0f),
                "resolved once, from the corpse frame's footprint");
            Assert.That(model.RestFractionForTest, Is.LessThan(1f),
                "the fall starts at the gameplay origin and slides in");

            for (int frame = 8; frame <= 11; frame++)
            {
                model.NotifyFrame(frame);
                yield return null;
            }
            for (int i = 0; i < 30; i++) yield return null;
            Assert.That(model.RestFractionForTest, Is.EqualTo(1f).Within(1e-4f),
                "by the corpse frame the body has settled fully inside the lift");

            Object.Destroy(go);
            yield return null;
        }
    }
}
