using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.MapBuild;

namespace Doom.Stage3.PlayTests
{
    /// T_PlatRaise going up into an actor: the plat is `crushed` without
    /// crush, so it heads back down with pstart, waits and tries again. Until
    /// 2026-09-04 the port held the floor in place until the actor left —
    /// with a monster stuck on a raised pillar that meant forever. E1M4
    /// sector 63 is the case: a 64×64 pillar whose floor (72) equals its
    /// ceiling, lowered to the room floor (−56) by the SR lift line 1954.
    public class LiftReversalPlayTests
    {
        const float WS = 1f / 32f;
        const int PillarSwitchLine = 1954;
        const int PillarSector = 63;

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
            Assert.Fail("Stage2_MapPreview did not finish loading E1M4");
        }

        static Vector3 Doom(float x, float y, float floorDoom) =>
            new Vector3(x * WS, floorDoom * WS, y * WS);

        [UnityTest]
        public IEnumerator Lift_blocked_by_a_monster_goes_back_down_and_finishes_once_it_is_gone()
        {
            yield return LoadLevel();
            var loader = Object.FindAnyObjectByType<MapLoader>();
            var activator = Object.FindAnyObjectByType<LineActivator>();
            var sound = loader.Sound;
            Assert.That(activator, Is.Not.Null);
            Assert.That(sound, Is.Not.Null);

            MonsterController monster = null;
            foreach (var mc in Object.FindObjectsByType<MonsterController>(FindObjectsSortMode.None))
            {
                var eh = mc.GetComponent<EnemyHealth>();
                if (eh != null && !eh.IsDead && mc.GetComponent<CapsuleCollider>() != null)
                { monster = mc; break; }
            }
            Assert.That(monster, Is.Not.Null, "E1M4 must have a live monster to park on the pillar");

            Time.captureDeltaTime = 1f / 60f;
            activator.ActivateLineForTest(PillarSwitchLine);
            Assert.That(activator.IsSectorMovingForTest(PillarSector), Is.True);
            int frames = 0;
            while (activator.GetSectorFloorForTest(PillarSector) > -55f && frames++ < 600) yield return null;
            Assert.That(activator.GetSectorFloorForTest(PillarSector), Is.LessThanOrEqualTo(-55f), "pillar must reach the room floor");

            // Park a (sleeping) monster on the lowered pillar.
            monster.transform.position = Doom(1920, 592, -56);
            int startsBefore = sound.PlayCountForTest("DSPSTART");

            // Wait through the dwell and the blocked rise: the floor must rise,
            // then come back down again instead of holding.
            float peak = -56f;
            bool rose = false, reversed = false;
            frames = 0;
            while (!reversed && frames++ < 900)
            {
                yield return null;
                float now = activator.GetSectorFloorForTest(PillarSector);
                if (now > peak) { peak = now; rose = true; }
                else if (rose && now < peak - 2f) reversed = true;
            }
            Assert.That(rose, Is.True, "lift must start back up after the dwell");
            Assert.That(reversed, Is.True, "a blocked lift heads back down (T_PlatRaise crushed → down)");
            Assert.That(peak, Is.LessThan(72f), "it never reaches the top through the monster");
            Assert.That(peak, Is.GreaterThan(0f), "it rises until the monster no longer fits under the ceiling");
            Assert.That(sound.PlayCountForTest("DSPSTART"), Is.GreaterThanOrEqualTo(startsBefore + 2),
                "pstart for the rise and again for the reversal");
            Assert.That(sound.ActiveLoopCount, Is.Zero);

            // Remove the blocker: the next attempt completes.
            Object.Destroy(monster.gameObject);
            yield return null;
            frames = 0;
            while (activator.IsSectorMovingForTest(PillarSector) && frames++ < 3000) yield return null;
            Assert.That(activator.IsSectorMovingForTest(PillarSector), Is.False, "lift must finish once the actor is gone");
            Assert.That(activator.GetSectorFloorForTest(PillarSector), Is.EqualTo(72f).Within(0.01f));
        }
    }
}
