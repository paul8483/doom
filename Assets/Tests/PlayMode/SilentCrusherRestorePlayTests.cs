using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.Game;
using Doom.Map;
using Doom.MapBuild;

namespace Doom.Stage3.PlayTests
{
    /// A crusher started by the silent special (141) carries its silence
    /// through capture (IsSilentCrusher → v8 MoverSilent) and back into the
    /// resumed thinker: no motor loop after a load, while a loud crusher
    /// restored beside it grinds at once. E1 has no crushers, so the first
    /// test drives the mover on a one-sector synthetic map against the
    /// level's SoundSystem, and the second rewrites a real E1M1 save so two
    /// sectors come back as crushers — one silent, one loud — through the
    /// whole store → codec → RestoreSectors path.
    public class SilentCrusherRestorePlayTests
    {
        int SilentSector = 0;
        int LoudSector = 1;

        /// Sector under the player, so the rewritten crushers land elsewhere.
        static int PlayerSector()
        {
            var player = GameObject.Find("Player");
            if (player != null && Physics.Raycast(player.transform.position + Vector3.up * 0.5f,
                    Vector3.down, out var hit, 50f, ~0, QueryTriggerInteraction.Ignore))
            {
                var sr = hit.collider.GetComponentInParent<SectorRef>();
                if (sr != null) return sr.SectorIndex;
            }
            return -1;
        }

        string tempRoot;
        Doom.MapBuild.SaveSlotStore testStore;

        static MapData OneSectorMap(short ceiling = 128) =>
            new MapData("TEST", System.Array.Empty<Vertex>(), System.Array.Empty<LineDef>(),
                System.Array.Empty<SideDef>(),
                new[] { new Sector(0, ceiling, "FLAT1", "FLAT1", 160, 0, 1) },
                System.Array.Empty<Thing>());

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            MapLoader.MapNameOverride = null;
            GameSessionHost.ResetForTests();
            tempRoot = Path.Combine(Path.GetTempPath(), "doom-silentcrusher-" + System.Guid.NewGuid().ToString("N"));
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

        [UnityTest]
        public IEnumerator Silent_crusher_stays_silent_across_snapshot_resume()
        {
            yield return LoadLevel();
            var sound = Object.FindAnyObjectByType<MapLoader>().Sound;
            Assert.That(sound, Is.Not.Null);
            int loopsBefore = sound.ActiveLoopCount;

            // Live start: the silent special produces a crusher that reports
            // itself silent and starts no loop.
            var goSilent = new GameObject("silent crusher");
            var hSilent = new RuntimeSectorHeights(OneSectorMap());
            var silent = goSilent.AddComponent<SectorMover>();
            silent.BeginCrusher(hSilent, null, 0, 8f, 350f, true, false, 1f / 32f,
                sound: sound, silent: true);
            Assert.That(silent.IsSilentCrusher, Is.True);
            Assert.That(sound.ActiveLoopCount, Is.EqualTo(loopsBefore), "silent crusher: no motor loop");

            // Resume from a snapshot that carried MoverSilent: still silent.
            var goResumed = new GameObject("resumed silent crusher");
            var hResumed = new RuntimeSectorHeights(OneSectorMap());
            hResumed.SetCeil(0, 40f);
            var resumed = goResumed.AddComponent<SectorMover>();
            resumed.BeginFromSnapshot(
                hResumed, null, 0, SectorMover.Surface.Ceiling,
                8f, 350f, MoverPhase.Returning, 0, 128f,
                MoverBehavior.Crusher, true,
                sound: sound, sfx: default, silentCrusher: true);
            Assert.That(resumed.IsSilentCrusher, Is.True, "MoverSilent survives the resume");
            Assert.That(sound.ActiveLoopCount, Is.EqualTo(loopsBefore), "resumed silent crusher: no motor loop");

            // Control: a loud crusher resumed the same way grinds immediately.
            var goLoud = new GameObject("resumed loud crusher");
            var hLoud = new RuntimeSectorHeights(OneSectorMap());
            hLoud.SetCeil(0, 40f);
            var loud = goLoud.AddComponent<SectorMover>();
            loud.BeginFromSnapshot(
                hLoud, null, 0, SectorMover.Surface.Ceiling,
                8f, 350f, MoverPhase.Returning, 0, 128f,
                MoverBehavior.Crusher, true,
                sound: sound, sfx: MoverSoundProfile.FloorOrLift, silentCrusher: false);
            Assert.That(loud.IsSilentCrusher, Is.False);
            Assert.That(sound.ActiveLoopCount, Is.EqualTo(loopsBefore + 1), "loud crusher: motor loop resumes");

            yield return null;
            Assert.That(hSilent.CeilRaw(0), Is.LessThan(128f), "silent crusher still moves");
            Assert.That(hResumed.CeilRaw(0), Is.GreaterThan(40f), "resumed silent crusher still moves");

            Object.Destroy(goSilent); Object.Destroy(goResumed); Object.Destroy(goLoud);
            yield return null;
            Assert.That(sound.ActiveLoopCount, Is.EqualTo(loopsBefore), "destroyed movers release their loops");
        }

        static SectorSnapshot AsCrusher(SectorSnapshot s, bool silent) =>
            new SectorSnapshot(
                s.Index, s.FloorHeight, s.CeilingHeight, s.LightLevel,
                hasMover: true, MoverPlane.Ceiling, MoverPhase.Moving,
                -1, s.FloorHeight + 8f, 350f, 0, s.LightCount,
                MoverBehavior.Crusher, true, s.CeilingHeight, silent);

        [UnityTest]
        public IEnumerator Loaded_save_restores_a_silent_crusher_without_cues_and_a_loud_one_with_them()
        {
            yield return LoadLevel();
            WireTestStore();
            var registry = Object.FindAnyObjectByType<WorldStateRegistry>();
            Assert.That(registry, Is.Not.Null);

            // A real save of the fresh level, with two sectors rewritten as
            // crushers in the snapshot: one silent (141), one loud.
            var flow = GameFlowController.Ensure();
            flow.RequestPause();
            yield return null;
            var saves = SaveGameController.Ensure();
            Assert.That(saves.TrySave(0), Is.True, saves.LastError);
            string slot = SaveGameController.SlotName(0);
            Assert.That(testStore.TryRead(slot, out SaveGame save, out string readErr), Is.True, readErr);
            Assert.That(save.Version, Is.EqualTo(SaveGame.SchemaVersion));

            var sectors = (SectorSnapshot[])save.World.Sectors.Clone();
            int avoid = PlayerSector();
            SilentSector = avoid == 0 ? 2 : 0;
            LoudSector = avoid == 1 ? 3 : 1;
            sectors[SilentSector] = AsCrusher(sectors[SilentSector], silent: true);
            sectors[LoudSector] = AsCrusher(sectors[LoudSector], silent: false);
            var world = new WorldSnapshot(
                save.World.GameTic, save.World.NextSpawnId, save.World.Stats,
                save.World.KillIds, save.World.ItemIds, save.World.SecretIds,
                sectors, save.World.Lines, save.World.Things,
                save.World.Projectiles, save.World.SpawnedPickups);
            testStore.Write(slot, new SaveGame(
                save.Version, save.MapName, save.WadIdentity, save.Player, world));

            Assert.That(saves.TryLoad(0), Is.True, saves.LastError);
            yield return WaitForPlayer("E1M1");
            yield return null;

            var sound = Object.FindAnyObjectByType<MapLoader>().Sound;
            var activator = GameObject.Find("Player").GetComponent<LineActivator>();
            SectorMover silentMover = null, loudMover = null;
            foreach (var m in Object.FindObjectsByType<SectorMover>(FindObjectsSortMode.None))
            {
                if (m.SectorIndex == SilentSector) silentMover = m;
                else if (m.SectorIndex == LoudSector) loudMover = m;
            }
            Assert.That(silentMover, Is.Not.Null, "sector 0 must come back as a mover");
            Assert.That(loudMover, Is.Not.Null, "sector 1 must come back as a mover");
            Assert.That(silentMover.IsCrusher && silentMover.IsSilentCrusher, Is.True,
                "MoverSilent restores a silent crusher");
            Assert.That(loudMover.IsCrusher && !loudMover.IsSilentCrusher, Is.True);
            Assert.That(activator.IsSectorMovingForTest(SilentSector), Is.True);
            Assert.That(activator.IsSectorMovingForTest(LoudSector), Is.True);
            Assert.That(sound.ActiveLoopCount, Is.EqualTo(1),
                "only the loud crusher grinds its motor loop after the load");
        }
    }
}
