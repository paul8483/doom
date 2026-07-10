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
    public class WorldCapturePlayTests
    {
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
            MapLoader.MapNameOverride = null;
            GameSessionHost.ResetForTests();
        }

        static IEnumerator LoadLevel()
        {
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return null; yield return null;
            for (int i = 0; i < 90; i++) yield return null;
            Time.captureDeltaTime = 1f / 60f;
        }

        [UnityTest]
        public IEnumerator Capture_complex_world_has_sorted_authoritative_changes()
        {
            yield return LoadLevel();

            var registry = Object.FindAnyObjectByType<WorldStateRegistry>();
            Assert.That(registry, Is.Not.Null, "MapLoader should create WorldStateRegistry");
            Assert.That(registry.MapThingCount, Is.GreaterThan(0));

            var player = GameObject.Find("Player");
            Assert.That(player, Is.Not.Null);
            var health = player.GetComponent<PlayerHealth>();
            health.Model.ApplyDamage(40);
            Assert.That(health.Model.Health, Is.EqualTo(60));

            // Kill a former human (may spawn a CLIP death drop → SpawnId).
            var zombie = Object.FindObjectsByType<MonsterController>(FindObjectsSortMode.None)
                .FirstOrDefault(m => m.gameObject.name.StartsWith("Thing_3004"));
            Assert.That(zombie, Is.Not.Null, "E1M1 should spawn a former human");
            var eh = zombie.GetComponent<EnemyHealth>();
            int zombieIndex = eh.MapThingIndex;
            Assert.That(zombieIndex, Is.GreaterThanOrEqualTo(0));
            eh.TakeDamage(10_000);
            for (int i = 0; i < 45; i++) yield return null;
            Assert.That(eh.IsDead, Is.True);

            // Pick up a map stimpack via inventory + unregister (simulates trigger pickup).
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

            // Open a manual door; capture while mover may still be active.
            string path = Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = WadFile.Open(path);
            var map = MapData.Load(wad, "E1M1");
            var activator = player.GetComponent<LineActivator>();
            int doorLine = -1, doorSector = -1;
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
            float beforeCeil = activator.GetSectorCeilForTest(doorSector);
            activator.ActivateLineForTest(doorLine);
            for (int i = 0; i < 15; i++) yield return null;

            // Activate a non-exit once Push/Switch special if present.
            int onceLine = -1;
            for (int i = 0; i < map.LineDefs.Length; i++)
            {
                var ld = map.LineDefs[i];
                if (ld.Special == 0) continue;
                if (!LineSpecialTable.TryGet(ld.Special, out var sp)) continue;
                if (sp.Repeatable) continue;
                if (sp.Category == SpecialCategory.Exit) continue;
                if (sp.Trigger != TriggerKind.Push && sp.Trigger != TriggerKind.Switch) continue;
                onceLine = i;
                break;
            }
            if (onceLine >= 0)
                activator.ActivateLineForTest(onceLine);

            // Launch an imp fireball using MonsterDef from a live imp controller.
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

            Assert.That(WorldSnapshotCapture.TryCapture(registry, out var snap, out string err),
                Is.True, err);

            for (int i = 1; i < snap.Things.Length; i++)
                Assert.That(snap.Things[i].MapThingIndex,
                    Is.GreaterThan(snap.Things[i - 1].MapThingIndex));

            var dead = snap.Things.First(t => t.MapThingIndex == zombieIndex);
            Assert.That(dead.Present, Is.True);
            Assert.That(dead.Health, Is.EqualTo(0));

            if (stimIndex >= 0)
            {
                var picked = snap.Things.First(t => t.MapThingIndex == stimIndex);
                Assert.That(picked.Present, Is.False);
            }

            var sector = snap.Sectors[doorSector];
            bool doorChanged = !Mathf.Approximately(sector.CeilingHeight, beforeCeil)
                               || sector.HasMover;
            Assert.That(doorChanged, Is.True,
                $"door sector {doorSector}: ceil {beforeCeil} -> {sector.CeilingHeight}, mover={sector.HasMover}");

            if (onceLine >= 0)
                Assert.That(snap.Lines.Any(l => l.Index == onceLine && l.Fired), Is.True);

            // Death drop and/or projectile should allocate SpawnIds.
            Assert.That(snap.NextSpawnId, Is.GreaterThan(0));
            Assert.That(snap.Projectiles.Length + snap.SpawnedPickups.Length,
                Is.GreaterThanOrEqualTo(1));

            Assert.That(snap.KillIds, Does.Contain(zombieIndex));
        }

        [UnityTest]
        public IEnumerator Registry_rejects_duplicate_map_thing_ids()
        {
            yield return LoadLevel();
            var registry = Object.FindAnyObjectByType<WorldStateRegistry>();
            Assert.That(registry, Is.Not.Null);

            int existing = registry.SortedMapThingIds().First();
            var go = new GameObject("dup");
            var id = go.AddComponent<MapThingIdentity>();
            id.Init(existing, 3004, 0);
            Assert.Throws<System.InvalidOperationException>(() => registry.RegisterMapThing(id));
            Object.Destroy(go);
        }
    }
}
