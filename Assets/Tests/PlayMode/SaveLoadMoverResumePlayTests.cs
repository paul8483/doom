using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.Game;
using Doom.Map;
using Doom.MapBuild;
using Doom.Specials;
using Doom.Wad;

namespace Doom.Stage3.PlayTests
{
    /// Movers resumed from a save behave like vanilla thinkers after a load:
    /// a floor in motion grinds its motor loop at once, and a door restored
    /// mid-open dwells VDOORWAIT at the top before closing with its cue.
    /// Before 2026-09-04 `BeginFromSnapshot` got no SoundSystem (every restored
    /// mover ran silent) and a zero dwell (the door shut the moment it opened).
    public class SaveLoadMoverResumePlayTests
    {
        const int FloorSwitchLine = 753;        // E1M1 S1 special 23, tag 3
        static readonly int[] FloorSectors = { 76, 126, 129 };

        string tempRoot;
        Doom.MapBuild.SaveSlotStore testStore;

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            MapLoader.MapNameOverride = null;
            GameSessionHost.ResetForTests();
            tempRoot = Path.Combine(Path.GetTempPath(), "doom-moverresume-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            testStore = new Doom.MapBuild.SaveSlotStore(tempRoot, new SystemSaveFileSystem());
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
            MapLoader.MapNameOverride = null;
            GameSessionHost.ResetForTests();
            try
            {
                if (!string.IsNullOrEmpty(tempRoot) && Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
            catch { /* best-effort */ }
        }

        static IEnumerator LoadLevel()
        {
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return null; yield return null;
            for (int i = 0; i < 90; i++) yield return null;
            Time.captureDeltaTime = 1f / 60f;
        }

        static IEnumerator WaitForPlayer(string map)
        {
            float deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline)
            {
                var player = GameObject.Find("Player");
                var loader = Object.FindAnyObjectByType<MapLoader>();
                if (player != null && loader != null &&
                    string.Equals(loader.LoadedMapName, map, System.StringComparison.OrdinalIgnoreCase) &&
                    GameFlowController.Instance != null &&
                    GameFlowController.Instance.State == GameFlowState.Playing)
                    yield break;
                yield return new WaitForSecondsRealtime(0.01f);
            }
            Assert.Fail($"Timed out waiting for player on {map}");
        }

        void WireTestStore()
        {
            var saves = SaveGameController.Ensure();
            saves.SetStoreForTests(testStore);
            var host = GameSessionHost.Ensure();
            string wadPath = Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");
            host.EnsureWadIdentity(wadPath);
        }

        static IEnumerator SaveAndReload()
        {
            var flow = GameFlowController.Ensure();
            flow.RequestPause();
            yield return null;
            var saves = SaveGameController.Ensure();
            Assert.That(saves.TrySave(0), Is.True, saves.LastError);
            Assert.That(saves.TryLoad(0), Is.True, saves.LastError);
            yield return WaitForPlayer("E1M1");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Floor_in_motion_resumes_its_motor_loop_after_load()
        {
            yield return LoadLevel();
            WireTestStore();
            var activator = GameObject.Find("Player").GetComponent<LineActivator>();

            // Slow "lower floor to lowest" on three closed sectors — still moving
            // long after the save.
            activator.ActivateLineForTest(FloorSwitchLine);
            for (int i = 0; i < 15; i++) yield return null;
            foreach (int s in FloorSectors)
                Assert.That(activator.IsSectorMovingForTest(s), Is.True, "floor " + s + " must be moving before the save");

            yield return SaveAndReload();

            var activator2 = GameObject.Find("Player").GetComponent<LineActivator>();
            var sound = Object.FindAnyObjectByType<MapLoader>().Sound;
            Assert.That(sound, Is.Not.Null);
            foreach (int s in FloorSectors)
                Assert.That(activator2.IsSectorMovingForTest(s), Is.True, "floor " + s + " must resume after the load");
            Assert.That(sound.ActiveLoopCount, Is.EqualTo(FloorSectors.Length),
                "each restored floor in motion grinds its DSSTNMOV loop");
            Assert.That(sound.WasPlayed("DSSTNMOV"), Is.True);

            // Let them finish: the loops release and the stop cue plays.
            int frames = 0;
            while (frames++ < 3000 && (activator2.IsSectorMovingForTest(FloorSectors[0])
                   || activator2.IsSectorMovingForTest(FloorSectors[1])
                   || activator2.IsSectorMovingForTest(FloorSectors[2])))
                yield return null;
            Assert.That(sound.ActiveLoopCount, Is.EqualTo(0));
            Assert.That(sound.WasPlayed("DSPSTOP"), Is.True);
        }

        [UnityTest]
        public IEnumerator Door_restored_mid_open_dwells_at_the_top_and_closes_with_its_cue()
        {
            yield return LoadLevel();
            WireTestStore();
            var player = GameObject.Find("Player");
            var activator = player.GetComponent<LineActivator>();

            string path = Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");
            int doorLine = -1, doorSector = -1;
            using (var wad = WadFile.Open(path))
            {
                var map = MapData.Load(wad, "E1M1");
                for (int i = 0; i < map.LineDefs.Length; i++)
                {
                    var ld = map.LineDefs[i];
                    if (ld.Special != 1 || ld.Tag != 0) continue;
                    int back = ld.BackSideIdx >= 0 ? map.SideDefs[ld.BackSideIdx].SectorIdx : -1;
                    if (back < 0) continue;
                    doorLine = i; doorSector = back; break;
                }
            }
            Assert.That(doorLine, Is.GreaterThanOrEqualTo(0), "E1M1 needs a DR door (special 1)");

            float closedCeil = activator.GetSectorCeilForTest(doorSector);
            activator.ActivateLineForTest(doorLine);
            for (int i = 0; i < 15; i++) yield return null;
            Assert.That(activator.IsSectorMovingForTest(doorSector), Is.True);
            Assert.That(activator.GetSectorCeilForTest(doorSector), Is.GreaterThan(closedCeil),
                "the door must be caught while opening");

            yield return SaveAndReload();

            var activator2 = GameObject.Find("Player").GetComponent<LineActivator>();
            var sound = Object.FindAnyObjectByType<MapLoader>().Sound;
            Assert.That(activator2.IsSectorMovingForTest(doorSector), Is.True, "door must resume after the load");

            // Ride the door to the top: the ceiling stops rising.
            float last = activator2.GetSectorCeilForTest(doorSector);
            int frames = 0;
            while (frames++ < 600)
            {
                yield return null;
                float now = activator2.GetSectorCeilForTest(doorSector);
                if (Mathf.Approximately(now, last)) break;
                last = now;
            }
            float top = activator2.GetSectorCeilForTest(doorSector);
            Assert.That(top, Is.GreaterThan(closedCeil + 8f), "door should have opened after the load");

            // VDOORWAIT: a full second later it is still at the top (was: closing at once).
            for (int i = 0; i < 60; i++) yield return null;
            Assert.That(activator2.GetSectorCeilForTest(doorSector), Is.EqualTo(top).Within(0.01f),
                "a door restored mid-open must dwell at the top before closing");
            Assert.That(sound.WasPlayed("DSDORCLS"), Is.False, "the close cue is not due yet");

            // The rest of the dwell plus the close travel: the close cue plays through
            // the restored SoundSystem and the door comes back down.
            frames = 0;
            while (frames++ < 900 && activator2.IsSectorMovingForTest(doorSector)) yield return null;
            Assert.That(activator2.IsSectorMovingForTest(doorSector), Is.False, "door cycle must finish");
            Assert.That(activator2.GetSectorCeilForTest(doorSector), Is.EqualTo(closedCeil).Within(0.01f));
            Assert.That(sound.WasPlayed("DSDORCLS"), Is.True, "the restored door closes with its cue");
        }
    }
}
