using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.MapBuild;

namespace Doom.Stage3.PlayTests
{
    /// A down-wait-up lift sounds like T_PlatRaise: pstart when it sets off,
    /// pstop at the bottom, pstart again after the wait, pstop at the top —
    /// and no stnmov motor loop anywhere in the cycle. Until 2026-09-04 lifts
    /// ran the floor profile (DSSTNMOV loop + one pstop at the top).
    public class LiftSoundPlayTests
    {
        const int LiftSwitchLine = 594;   // E1M1 SR special 62, tag 1
        const int LiftSector = 98;        // floor 12, lowers to its lowest neighbour

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            MapLoader.MapNameOverride = null;
            GameSessionHost.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
            GameSessionHost.ResetForTests();
        }

        [UnityTest]
        public IEnumerator Lift_cycle_plays_pstart_pstop_pstart_pstop_without_a_motor_loop()
        {
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return null; yield return null;
            for (int i = 0; i < 90; i++) yield return null;
            Time.captureDeltaTime = 1f / 60f;

            var activator = GameObject.Find("Player").GetComponent<LineActivator>();
            var sound = Object.FindAnyObjectByType<MapLoader>().Sound;
            Assert.That(activator, Is.Not.Null);
            Assert.That(sound, Is.Not.Null);
            Assert.That(sound.PlayCountForTest("DSPSTART"), Is.Zero);
            Assert.That(sound.PlayCountForTest("DSPSTOP"), Is.Zero);

            float top = activator.GetSectorFloorForTest(LiftSector);
            activator.ActivateLineForTest(LiftSwitchLine);
            Assert.That(activator.IsSectorMovingForTest(LiftSector), Is.True);
            Assert.That(sound.PlayCountForTest("DSPSTART"), Is.EqualTo(1), "pstart when the lift sets off");
            Assert.That(sound.ActiveLoopCount, Is.Zero, "no motor loop on a lift");

            float prev = activator.GetSectorFloorForTest(LiftSector);
            bool reachedBottom = false, startedUp = false;
            int frames = 0;
            while (activator.IsSectorMovingForTest(LiftSector) && frames++ < 3000)
            {
                yield return null;
                float now = activator.GetSectorFloorForTest(LiftSector);
                if (!reachedBottom && now < top && Mathf.Approximately(now, prev))
                {
                    reachedBottom = true;
                    Assert.That(sound.PlayCountForTest("DSPSTOP"), Is.EqualTo(1), "pstop at the bottom");
                    Assert.That(sound.PlayCountForTest("DSPSTART"), Is.EqualTo(1));
                }
                if (reachedBottom && !startedUp && now > prev)
                {
                    startedUp = true;
                    Assert.That(sound.PlayCountForTest("DSPSTART"), Is.EqualTo(2), "pstart again after the wait");
                    Assert.That(sound.PlayCountForTest("DSPSTOP"), Is.EqualTo(1));
                }
                Assert.That(sound.ActiveLoopCount, Is.Zero, "no motor loop at any point of the cycle");
                prev = now;
            }

            Assert.That(reachedBottom && startedUp, Is.True, "lift must go down, wait and come back up");
            Assert.That(activator.GetSectorFloorForTest(LiftSector), Is.EqualTo(top).Within(0.01f));
            Assert.That(sound.PlayCountForTest("DSPSTOP"), Is.EqualTo(2), "pstop at the top");
            Assert.That(sound.PlayCountForTest("DSPSTART"), Is.EqualTo(2));
            Assert.That(sound.PlayCountForTest("DSSTNMOV"), Is.Zero, "stnmov belongs to floors and ceilings, not plats");
        }
    }
}
