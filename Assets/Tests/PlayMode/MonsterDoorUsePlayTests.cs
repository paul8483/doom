using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.MapBuild;

namespace Doom.Stage3.PlayTests
{
    /// Monster door use against vanilla p_spec.c on the real E1M4: a monster
    /// at a keyed door neither opens it nor triggers the player's key grunt
    /// (EV_VerticalDoor `if (!player) return 0`), while a manual door
    /// (special 1) still opens for it. Regression for the slot-0 report of
    /// 2026-09-04: twelve monsters parked at the blue door (line 1299) ran the
    /// PLAYER's key check and played the 2D DSNOWAY four times a second,
    /// audible everywhere on the map, for as long as they stood there.
    public class MonsterDoorUsePlayTests
    {
        const float WS = 1f / 32f;
        const int BlueDoorSector = 188;   // spec 26 lines 1295 / 1299
        const int BlueDoorLine = 1299;    // west face, front sector 189
        const int ManualDoorSector = 5;   // spec 1 lines 79 / 82
        const int ManualDoorLine = 79;

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            GameFlowController.ResetForTests();
            GameFlowController.AutoStartPlaying = true;
            // Order matters: ResetForTests goes through GameSessionHost, which
            // clears MapNameOverride — set the map after it.
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
            Assert.Fail("Stage2_MapPreview did not finish loading E1M4");
        }

        static Vector3 Doom(float x, float y, float floorDoom) =>
            new Vector3(x * WS, floorDoom * WS, y * WS);

        [UnityTest]
        public IEnumerator Monster_at_keyed_door_neither_opens_it_nor_plays_the_key_grunt()
        {
            yield return LoadLevel();
            var loader = Object.FindAnyObjectByType<MapLoader>();
            var activator = Object.FindAnyObjectByType<LineActivator>();
            var sound = loader.Sound;
            Assert.That(activator, Is.Not.Null);
            Assert.That(sound, Is.Not.Null);
            var inventory = GameObject.Find("Player").GetComponent<PlayerInventory>();
            Assert.That(inventory != null && !inventory.Keys.HasAny(),
                "a fresh E1M4 start must hold no keys");

            // Twelve monsters stood here in the slot-0 save: the west side of
            // the blue door, 30 units from its face.
            LineActivator.MonsterUseNearestDoor(Doom(-340, 1600, -96), 64f * WS);
            yield return null;

            Assert.That(activator.IsSectorMovingForTest(BlueDoorSector), Is.False,
                "a monster must not open a keyed door");
            Assert.That(sound.WasPlayed("DSNOWAY"), Is.False,
                "a monster's door use must not run the player's key grunt");
            Assert.That(sound.WasPlayed("DSOOF"), Is.False);

            // The player's own use of the same door is unchanged: denied, with the grunt.
            activator.ActivateLineForTest(BlueDoorLine);
            yield return null;
            Assert.That(activator.IsSectorMovingForTest(BlueDoorSector), Is.False);
            Assert.That(sound.WasPlayed("DSOOF"), Is.True,
                "the player without the key still hears the denial");
        }

        [UnityTest]
        public IEnumerator Monster_still_opens_a_manual_door()
        {
            yield return LoadLevel();
            var activator = Object.FindAnyObjectByType<LineActivator>();
            Assert.That(activator, Is.Not.Null);
            Assert.That(activator.IsSectorMovingForTest(ManualDoorSector), Is.False);

            // South side of the spec-1 door at (0..128, -616): line 79's front sector.
            LineActivator.MonsterUseNearestDoor(Doom(64, -640, 8), 64f * WS);
            yield return null;

            Assert.That(activator.IsSectorMovingForTest(ManualDoorSector), Is.True,
                "special 1 stays monster-usable (line " + ManualDoorLine + ")");
        }
    }
}
