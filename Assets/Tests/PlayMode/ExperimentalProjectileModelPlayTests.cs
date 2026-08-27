using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.Game;
using Doom.MapBuild;
using Doom.Things;

namespace Doom.Stage3.PlayTests
{
    /// The imp's fireball in Enhanced+3D: a voxel ball built from BAL1 itself.
    /// Coverage is fly frames only — impact must hand the sequence back to the
    /// billboard, because the explosion is a spray of loose pixels.
    public class ExperimentalProjectileModelPlayTests
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

        const float WorldScale = 1f / 32f;

        IEnumerator LoadLevel()
        {
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            // LoadScene lands at the end of the frame, and the previous test's
            // MapLoader is still findable until then — grabbing its SpriteCache
            // hands out a cache whose WAD is about to close.
            yield return null;
            for (int i = 0; i < 600; i++)
            {
                var flow = GameFlowController.Instance;
                var loader = Object.FindFirstObjectByType<MapLoader>();
                if (flow != null && flow.State == GameFlowState.Playing &&
                    loader != null && loader.Sprites != null &&
                    GameObject.Find("Player") != null)
                    yield break;
                yield return null;
            }
            Assert.Fail("Stage2_MapPreview did not finish loading");
        }

        static SpriteCache Sprites() =>
            Object.FindFirstObjectByType<MapLoader>().Sprites;

        static MonsterDef Imp()
        {
            Assert.That(MonsterTable.TryGet(3001, out var def), Is.True);
            return def;
        }

        /// A stand-in for the missile GameObject Projectile.LaunchInternal
        /// builds: same components, same billboard init, no flight.
        static GameObject NewMissile(SpriteCache cache, MonsterDef def,
                                     out SpriteBillboard bb)
        {
            var go = new GameObject("MissileTest",
                typeof(MeshFilter), typeof(MeshRenderer));
            bb = go.AddComponent<SpriteBillboard>();
            bb.Init(cache, def.MissileSprite, def.MissileFlyFrames[0], WorldScale,
                    doomAngleDeg: 0f, spawnCeiling: false, ceilingY: 0f);
            bb.SetStaticFrame(def.MissileFlyFrames[0]);
            return go;
        }

        [UnityTest]
        public IEnumerator Fireball_attaches_and_follows_toggle_cascade()
        {
            yield return LoadLevel();
            var def = Imp();
            var go = NewMissile(Sprites(), def, out var bb);
            var mr = go.GetComponent<MeshRenderer>();

            var model = ExperimentalProjectileModel.TryAttach(
                go, Sprites(), def.MissileSprite, def.MissileFlyFrames,
                WorldScale, bb);
            Assert.That(model, Is.Not.Null, "BAL1 ball and colour tables ship in Resources");

            var settings = SettingsController.Ensure();
            settings.ConfigureForTests(new SettingsStore(memory), display,
                new NoOpGraphicsModeAdapter());
            settings.SetGraphicsMode(GraphicsMode.Enhanced);
            yield return null;

            Assert.That(model.ModelVisible, Is.True, "Enhanced+3D On → ball");
            Assert.That(mr.enabled, Is.False);
            Assert.That(bb.enabled, Is.False);

            settings.SetGraphicsMode(GraphicsMode.Classic);
            yield return null;
            Assert.That(model.ModelVisible, Is.False, "Classic → billboard");
            Assert.That(mr.enabled, Is.True);
            Assert.That(bb.enabled, Is.True);

            settings.SetGraphicsMode(GraphicsMode.Enhanced);
            yield return null;
            Assert.That(model.ModelVisible, Is.True, "hot-switch back → ball");

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Ball_takes_its_size_and_place_from_the_wad_patch()
        {
            yield return LoadLevel();
            var def = Imp();
            var cache = Sprites();
            var go = NewMissile(cache, def, out var bb);
            var model = ExperimentalProjectileModel.TryAttach(
                go, cache, def.MissileSprite, def.MissileFlyFrames, WorldScale, bb);
            Assert.That(model, Is.Not.Null);

            var patch = cache.Get(def.MissileSprite, def.MissileFlyFrames[0], 0);
            Assert.That(patch.IsValid, Is.True);
            var pivot = model.PivotForTest;

            // The ball must cover exactly what the billboard quad covered: the
            // OBJ is normalized to a unit box, so scale IS the diameter, and
            // the sprite's own top offset puts the centre where it belongs.
            Assert.That(pivot.localScale.x,
                Is.EqualTo(patch.Width * WorldScale).Within(1e-5f));
            Assert.That(pivot.localPosition.y,
                Is.EqualTo((patch.TopOffset - patch.Height * 0.5f) * WorldScale)
                    .Within(1e-5f));

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Fly_frames_swap_the_colour_table()
        {
            yield return LoadLevel();
            var def = Imp();
            var go = NewMissile(Sprites(), def, out var bb);
            var model = ExperimentalProjectileModel.TryAttach(
                go, Sprites(), def.MissileSprite, def.MissileFlyFrames,
                WorldScale, bb);
            Assert.That(model, Is.Not.Null);
            model.SetEnhancedForTest(true);

            var first = model.CurrentProfileForTest;
            Assert.That(first, Is.Not.Null, "the ball must carry a colour table");

            model.NotifyFlyFrame(1);
            Assert.That(model.CurrentFrameForTest, Is.EqualTo(1));
            Assert.That(model.CurrentProfileForTest, Is.Not.SameAs(first),
                "BAL1 A and B are the same ball boiling differently — the " +
                "table has to change with the frame");

            model.NotifyFlyFrame(0);
            Assert.That(model.CurrentProfileForTest, Is.SameAs(first));

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Impact_hands_the_explosion_back_to_the_billboard()
        {
            yield return LoadLevel();
            var def = Imp();
            var go = NewMissile(Sprites(), def, out var bb);
            var mr = go.GetComponent<MeshRenderer>();
            var model = ExperimentalProjectileModel.TryAttach(
                go, Sprites(), def.MissileSprite, def.MissileFlyFrames,
                WorldScale, bb);
            Assert.That(model, Is.Not.Null);
            model.SetEnhancedForTest(true);
            Assert.That(model.ModelVisible, Is.True);

            model.RevertToBillboard();
            Assert.That(model.ModelVisible, Is.False, "explosion is native");
            Assert.That(mr.enabled, Is.True);
            Assert.That(bb.enabled, Is.True);

            // Sticky, like the barrel's BEXP: a toggle mid-explosion must not
            // bring the ball back over the spray.
            model.SetEnhancedForTest(true);
            Assert.That(model.ModelVisible, Is.False);
            model.NotifyFlyFrame(1);
            Assert.That(model.ModelVisible, Is.False);

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Unrouted_missile_sprite_does_not_attach()
        {
            yield return LoadLevel();
            var def = Imp();
            var go = NewMissile(Sprites(), def, out var bb);
            Assert.That(ExperimentalProjectileModel.TryAttach(
                    go, Sprites(), "BAL2", def.MissileFlyFrames, WorldScale, bb),
                Is.Null, "the cacodemon's ball has no accepted asset");

            Object.Destroy(go);
            yield return null;
        }
    }
}
