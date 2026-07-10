using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
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
    public class SaveLoadPlayTests
    {
        string tempRoot;
        Doom.MapBuild.SaveSlotStore testStore;

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            MapLoader.MapNameOverride = null;
            GameSessionHost.ResetForTests();
            tempRoot = Path.Combine(Path.GetTempPath(), "doom-saveload-" + System.Guid.NewGuid().ToString("N"));
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

        static IEnumerator WaitForPlayer(string map, int maxFrames = 180)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                var player = GameObject.Find("Player");
                var loader = Object.FindAnyObjectByType<MapLoader>();
                if (player != null && loader != null &&
                    string.Equals(loader.LoadedMapName, map, System.StringComparison.OrdinalIgnoreCase) &&
                    GameFlowController.Instance != null &&
                    GameFlowController.Instance.State == GameFlowState.Playing)
                    yield break;
                yield return null;
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
        public IEnumerator Save_load_round_trip_restores_complex_world()
        {
            yield return LoadLevel();
            WireTestStore();

            var registry = Object.FindAnyObjectByType<WorldStateRegistry>();
            Assert.That(registry, Is.Not.Null);
            var player = GameObject.Find("Player");
            Assert.That(player, Is.Not.Null);

            var health = player.GetComponent<PlayerHealth>();
            health.Model.ApplyDamage(40);
            Assert.That(health.Model.Health, Is.EqualTo(60));

            var zombie = Object.FindObjectsByType<MonsterController>(FindObjectsSortMode.None)
                .FirstOrDefault(m => m.gameObject.name.StartsWith("Thing_3004"));
            Assert.That(zombie, Is.Not.Null);
            var eh = zombie.GetComponent<EnemyHealth>();
            int zombieIndex = eh.MapThingIndex;
            eh.TakeDamage(10_000);
            for (int i = 0; i < 45; i++) yield return null;
            Assert.That(eh.IsDead, Is.True);

            var stim = Object.FindObjectsByType<ThingPickup>(FindObjectsSortMode.None)
                .FirstOrDefault(p => p.DoomedNum == 2011 && p.MapThingIndex >= 0);
            int stimIndex = -1;
            if (stim != null)
            {
                stimIndex = stim.MapThingIndex;
                var mapId = stim.GetComponent<MapThingIdentity>();
                Assert.That(player.GetComponent<PlayerInventory>().TryPickup(2011), Is.True);
                if (mapId != null)
                    registry.UnregisterMapThing(mapId.MapThingIndex);
                Object.Destroy(stim.gameObject);
                yield return null;
            }

            int expectedHp = health.Model.Health;

            string path = Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = WadFile.Open(path);
            var map = MapData.Load(wad, "E1M1");
            var activator = player.GetComponent<LineActivator>();
            int doorLine = -1, doorSector = -1;
            float beforeCeil = 0f;
            for (int i = 0; i < map.LineDefs.Length; i++)
            {
                var ld = map.LineDefs[i];
                if (ld.Special == 0) continue;
                if (!LineSpecialTable.TryGet(ld.Special, out var sp)) continue;
                if (sp.Category != SpecialCategory.Door) continue;
                if (ld.Tag != 0) continue;
                int back = ld.BackSideIdx >= 0 ? map.SideDefs[ld.BackSideIdx].SectorIdx : -1;
                if (back < 0) continue;
                doorLine = i;
                doorSector = back;
                break;
            }
            Assert.That(doorLine, Is.GreaterThanOrEqualTo(0));
            beforeCeil = activator.GetSectorCeilForTest(doorSector);
            activator.ActivateLineForTest(doorLine);
            for (int i = 0; i < 15; i++) yield return null;

            var imp = Object.FindObjectsByType<MonsterController>(FindObjectsSortMode.None)
                .FirstOrDefault(m => m.gameObject.name.StartsWith("Thing_3001"));
            if (imp != null)
            {
                var defField = typeof(MonsterController).GetField("def",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var cacheField = typeof(SpriteBillboard).GetField("cache",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var def = defField?.GetValue(imp);
                var cache = cacheField?.GetValue(imp.GetComponent<SpriteBillboard>());
                if (def != null && cache != null)
                {
                    typeof(Projectile).GetMethod("Launch").Invoke(null, new object[]
                    {
                        cache, def, 1f / 32f, new DoomRandom(7),
                        player.transform.position + Vector3.up * 1.2f + Vector3.forward,
                        player.transform.position + Vector3.up * 1.2f + Vector3.forward * 8f,
                        null, null, 3001
                    });
                }
            }
            yield return null;

            Assert.That(WorldSnapshotCapture.TryCapture(registry, out var before, out string capErr),
                Is.True, capErr);

            var flow = GameFlowController.Ensure();
            flow.RequestPause();
            yield return null;

            var saves = SaveGameController.Ensure();
            Assert.That(saves.TrySave(0), Is.True, saves.LastError);
            Assert.That(flow.State, Is.EqualTo(GameFlowState.Paused));

            Assert.That(saves.TryLoad(0), Is.True, saves.LastError);
            yield return WaitForPlayer("E1M1");

            var registry2 = Object.FindAnyObjectByType<WorldStateRegistry>();
            Assert.That(registry2, Is.Not.Null);
            Assert.That(Object.FindObjectsByType<GameSessionHost>(FindObjectsSortMode.None).Length,
                Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<WorldStateRegistry>(FindObjectsSortMode.None).Length,
                Is.EqualTo(1));

            Assert.That(WorldSnapshotCapture.TryCapture(registry2, out var after, out string afterErr),
                Is.True, afterErr);

            var player2 = GameObject.Find("Player");
            Assert.That(player2.GetComponent<PlayerHealth>().Model.Health, Is.EqualTo(expectedHp));

            var dead = after.Things.First(t => t.MapThingIndex == zombieIndex);
            Assert.That(dead.Present, Is.True);
            Assert.That(dead.Health, Is.EqualTo(0));

            if (stimIndex >= 0)
            {
                var picked = after.Things.First(t => t.MapThingIndex == stimIndex);
                Assert.That(picked.Present, Is.False);
            }

            var sector = after.Sectors[doorSector];
            bool doorChanged = !Mathf.Approximately(sector.CeilingHeight, beforeCeil)
                               || sector.HasMover;
            Assert.That(doorChanged, Is.True);

            Assert.That(after.KillIds, Does.Contain(zombieIndex));
            Assert.That(after.NextSpawnId, Is.EqualTo(before.NextSpawnId));
            Assert.That(GameFlowController.Instance.State, Is.EqualTo(GameFlowState.Playing));
        }

        [UnityTest]
        public IEnumerator Corrupt_or_wrong_wad_save_rejected_without_changing_level()
        {
            yield return LoadLevel();
            WireTestStore();

            var player = GameObject.Find("Player");
            var health = player.GetComponent<PlayerHealth>();
            health.Model.ApplyDamage(25);
            int hpBefore = health.Model.Health;

            var flow = GameFlowController.Ensure();
            flow.RequestPause();
            yield return null;

            var saves = SaveGameController.Ensure();
            Assert.That(saves.TrySave(1), Is.True, saves.LastError);

            // Corrupt the slot bytes.
            string slotPath = Path.Combine(tempRoot, "slot1.dsav");
            byte[] data = File.ReadAllBytes(slotPath);
            data[data.Length - 1] ^= 0xFF;
            File.WriteAllBytes(slotPath, data);

            Assert.That(saves.TryLoad(1), Is.False);
            Assert.That(saves.LastError, Is.Not.Null.And.Not.Empty);
            Assert.That(GameObject.Find("Player").GetComponent<PlayerHealth>().Model.Health,
                Is.EqualTo(hpBefore));
            Assert.That(flow.State, Is.EqualTo(GameFlowState.Paused));

            // Wrong WAD identity: write a valid save then rewrite with mismatched identity.
            Assert.That(saves.TrySave(2), Is.True, saves.LastError);
            Assert.That(testStore.TryRead("slot2", out SaveGame good, out _), Is.True);
            var wrong = new SaveGame(
                good.Version, good.MapName, "len=1;h=deadbeef", good.Player, good.World);
            testStore.Write("slot2", wrong);

            Assert.That(saves.TryLoad(2), Is.False);
            Assert.That(saves.LastError, Does.Contain("WAD").IgnoreCase);
            Assert.That(GameObject.Find("Player").GetComponent<PlayerHealth>().Model.Health,
                Is.EqualTo(hpBefore));
            Assert.That(Object.FindAnyObjectByType<MapLoader>().LoadedMapName,
                Is.EqualTo("E1M1").IgnoreCase);
        }

        [UnityTest]
        public IEnumerator Load_does_not_duplicate_host_or_registry()
        {
            yield return LoadLevel();
            WireTestStore();

            var flow = GameFlowController.Ensure();
            flow.RequestPause();
            yield return null;

            var saves = SaveGameController.Ensure();
            Assert.That(saves.TrySave(3), Is.True, saves.LastError);
            Assert.That(saves.TryLoad(3), Is.True, saves.LastError);
            yield return WaitForPlayer("E1M1");

            Assert.That(Object.FindObjectsByType<GameSessionHost>(FindObjectsSortMode.None).Length,
                Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<WorldStateRegistry>(FindObjectsSortMode.None).Length,
                Is.EqualTo(1));
            Assert.That(GameSessionHost.Instance, Is.Not.Null);
            Assert.That(WorldStateRegistry.Instance, Is.Not.Null);

            // Second load of the same slot.
            flow = GameFlowController.Ensure();
            flow.RequestPause();
            yield return null;
            WireTestStore();
            Assert.That(SaveGameController.Ensure().TryLoad(3), Is.True);
            yield return WaitForPlayer("E1M1");

            Assert.That(Object.FindObjectsByType<GameSessionHost>(FindObjectsSortMode.None).Length,
                Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<WorldStateRegistry>(FindObjectsSortMode.None).Length,
                Is.EqualTo(1));
        }
    }
}
