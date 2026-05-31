using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.Wad;
using Doom.Map;
using Doom.Specials;
using Doom.MapBuild;

namespace Doom.Stage3.PlayTests
{
    public class SectorRetriggerPlayTests
    {
        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true; // PhysX cook warnings
        }

        [TearDown]
        public void TearDown()
        {
            // Always release the deterministic clock so it never leaks to other fixtures.
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator Repeatable_door_can_be_triggered_again_after_it_finishes()
        {
            // Load + activate the scene with the NORMAL clock. Driving captureDeltaTime
            // across async scene activation deadlocks headless batchmode, so we only
            // switch to the fixed step once the level is fully built.
            SceneManager.LoadScene("Stage2_MapPreview");
            yield return null; yield return null;
            for (int i = 0; i < 90; i++) yield return null; // let MapLoader.Build finish

            var activator = Object.FindAnyObjectByType<LineActivator>();
            Assert.That(activator, Is.Not.Null);

            // Find a repeatable manual door (DR, tag 0) and its back sector.
            string path = Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = WadFile.Open(path);
            var map = MapData.Load(wad, "E1M1");
            int doorLine = -1, doorSector = -1;
            for (int i = 0; i < map.LineDefs.Length; i++)
            {
                var ld = map.LineDefs[i];
                if (ld.Special == 0 || ld.Tag != 0) continue;
                if (!LineSpecialTable.TryGet(ld.Special, out var sp)) continue;
                if (sp.Category != SpecialCategory.Door || !sp.Repeatable) continue;
                int back = ld.BackSideIdx >= 0 ? map.SideDefs[ld.BackSideIdx].SectorIdx : -1;
                if (back < 0) continue;
                doorLine = i; doorSector = back; break;
            }
            Assert.That(doorLine, Is.GreaterThanOrEqualTo(0), "E1M1 should have a repeatable manual door");

            // Deterministic stepping ONLY around the motion: 1/60s per frame so the
            // door's 4.3s wait elapses in a bounded number of frames.
            Time.captureDeltaTime = 1f / 60f;

            // First activation: a mover starts.
            activator.ActivateLineForTest(doorLine);
            Assert.That(activator.IsSectorMovingForTest(doorSector), Is.True, "mover should start");

            // Step until the full open→wait(4.3s)→close cycle finishes (cap generously).
            int frames = 0;
            while (activator.IsSectorMovingForTest(doorSector) && frames < 2000)
            { frames++; yield return null; }

            // THE REGRESSION: the flag must clear when the mover finishes.
            Assert.That(activator.IsSectorMovingForTest(doorSector), Is.False,
                "moving flag must clear after the mover completes (re-trigger fix)");

            // Second activation: it must move again.
            activator.ActivateLineForTest(doorLine);
            Assert.That(activator.IsSectorMovingForTest(doorSector), Is.True,
                "the door must be re-triggerable after finishing");

            Time.captureDeltaTime = 0f;
        }
    }
}
