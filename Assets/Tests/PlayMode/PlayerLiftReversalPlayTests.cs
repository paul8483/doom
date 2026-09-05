using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.MapBuild;

namespace Doom.Stage3.PlayTests
{
    /// The PLAYER blocks a rising lift the way a monster does: T_PlatRaise
    /// `crushed` → the plat heads back down with pstart, waits and retries,
    /// and completes once the clearance is back. E1M4 lift 33 (floor 0 →
    /// −256, ceiling 160) with the ceiling faked down to 40 so the player's
    /// 56 units stop fitting once the floor passes −16. The earlier "player
    /// does not hold the plate" note came from probing E1M4's pillar 63,
    /// which is a teleporter pad (lines 1933/1938, special 97): the probe's
    /// player was teleported off the lift, not passed through.
    public class PlayerLiftReversalPlayTests
    {
        const float WS = 1f / 32f;
        const int Lift = 33;
        const int LiftSwitchLine = 326;   // SR special 62, tag 1

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            GameFlowController.ResetForTests();
            GameFlowController.AutoStartPlaying = true;
            MapLoader.MapNameOverride = "E1M4";
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            Time.timeScale = 1f;
            MapLoader.MapNameOverride = null;
            GameSessionHost.ResetForTests();
            GameFlowController.ResetForTests();
            LogAssert.ignoreFailingMessages = false;
        }

        IEnumerator LoadLevel()
        {
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return null;
            for (int i = 0; i < 900; i++)
            {
                var flow = GameFlowController.Instance;
                var loader = Object.FindAnyObjectByType<MapLoader>();
                if (flow != null && flow.State == GameFlowState.Playing &&
                    loader != null && loader.LoadedMapName == "E1M4" &&
                    loader.LastBuildSeconds > 0f && loader.Sprites != null &&
                    GameObject.Find("Player") != null)
                    yield break;
                yield return null;
            }
            Assert.Fail("E1M4 did not load");
        }

        static Vector3 Doom(float x, float y, float floorDoom) =>
            new Vector3(x * WS, floorDoom * WS, y * WS);

        static void Teleport(GameObject player, Vector3 pos)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = pos;
            if (cc != null) cc.enabled = true;
        }

        [UnityTest]
        public IEnumerator Lift_rising_into_the_player_goes_back_down_retries_and_finishes_with_clearance()
        {
            yield return LoadLevel();
            var loader = Object.FindAnyObjectByType<MapLoader>();
            var activator = Object.FindAnyObjectByType<LineActivator>();
            var registry = Object.FindAnyObjectByType<WorldStateRegistry>();
            var sound = loader.Sound;
            var player = GameObject.Find("Player");
            Assert.That(activator, Is.Not.Null);
            Assert.That(registry, Is.Not.Null);

            Teleport(player, Doom(-704, -704, 0));
            yield return null;
            Time.captureDeltaTime = 1f / 60f;
            activator.ActivateLineForTest(LiftSwitchLine);
            int frames = 0;
            while (activator.GetSectorFloorForTest(Lift) > -255f && frames++ < 600) yield return null;
            Assert.That(activator.GetSectorFloorForTest(Lift), Is.LessThanOrEqualTo(-255f), "lift must reach the bottom");

            // Ride it: the player stands on the lowered lift under a faked low ceiling.
            Teleport(player, Doom(-640, -704, -255));
            registry.Heights.SetCeil(Lift, 40f);
            int startsBefore = sound.PlayCountForTest("DSPSTART");

            float peak = -256f;
            bool rose = false, reversed = false, retried = false;
            frames = 0;
            while (!retried && frames++ < 1200)
            {
                yield return null;
                float now = activator.GetSectorFloorForTest(Lift);
                if (!reversed)
                {
                    if (now > peak) { peak = now; rose = true; }
                    else if (rose && now < peak - 2f) reversed = true;
                }
                else if (now <= -255f)
                {
                    // Back at the bottom: wait for the retry.
                    for (int i = 0; i < 60 * 4 && activator.GetSectorFloorForTest(Lift) <= -255f; i++) yield return null;
                    retried = activator.GetSectorFloorForTest(Lift) > -255f;
                    break;
                }
                Assert.That(sound.ActiveLoopCount, Is.Zero, "no motor loop on a lift");
            }
            Assert.That(rose && reversed, Is.True, "a lift rising into the player heads back down");
            Assert.That(peak, Is.InRange(-24f, -12f),
                "it reverses as soon as the next step would leave less than the player's 56 units");
            Assert.That(retried, Is.True, "after the dwell it tries again");
            Assert.That(sound.PlayCountForTest("DSPSTART"), Is.GreaterThanOrEqualTo(startsBefore + 3),
                "pstart for the rise, the reversal and the retry");
            Assert.That(player.transform.position.x / WS, Is.EqualTo(-640f).Within(8f), "the player is still on the lift");

            // Give the clearance back: the lift completes with the player riding up.
            registry.Heights.SetCeil(Lift, 160f);
            frames = 0;
            while (activator.IsSectorMovingForTest(Lift) && frames++ < 3000) yield return null;
            Assert.That(activator.IsSectorMovingForTest(Lift), Is.False, "lift must finish once the player fits");
            Assert.That(activator.GetSectorFloorForTest(Lift), Is.EqualTo(0f).Within(0.01f));
            yield return null;
            Assert.That(player.transform.position.y / WS, Is.GreaterThan(-8f), "the player rode the lift to the top");
        }
    }
}
