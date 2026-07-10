using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.Game;
using Doom.MapBuild;

namespace Doom.Stage3.PlayTests
{
    public class SoundPlayTests
    {
        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            MapLoader.MapNameOverride = null;
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
            MapLoader.MapNameOverride = null;
        }

        static IEnumerator LoadLevel(string mapName = null)
        {
            MapLoader.MapNameOverride = mapName;
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return null; yield return null;
            for (int i = 0; i < 90; i++) yield return null;
            Time.captureDeltaTime = 1f / 60f;
        }

        [UnityTest]
        public IEnumerator Sound_bootstrap_cache_and_playback()
        {
            yield return LoadLevel();

            var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            Assert.That(listeners.Length, Is.EqualTo(1), "exactly one AudioListener");

            var systems = Object.FindObjectsByType<SoundSystem>(FindObjectsSortMode.None);
            Assert.That(systems.Length, Is.EqualTo(1), "exactly one SoundSystem");
            var sound = systems[0];
            Assert.That(sound.Cache, Is.Not.Null);
            Assert.That(sound.Cache.IsCached("DSPISTOL"), Is.True);

            var local = sound.PlayLocal("DSPISTOL");
            Assert.That(local, Is.Not.Null);
            Assert.That(local.clip, Is.Not.Null);
            Assert.That(local.clip.name, Is.EqualTo("DSPISTOL"));
            Assert.That(local.spatialBlend, Is.EqualTo(0f));
            Assert.That(sound.WasPlayed("DSPISTOL"), Is.True);

            var world = sound.PlayAt("DSDOROPN", new Vector3(10f, 0f, 10f));
            Assert.That(world, Is.Not.Null);
            Assert.That(world.spatialBlend, Is.EqualTo(1f));
            Assert.That(world.transform.position, Is.EqualTo(new Vector3(10f, 0f, 10f)));
        }

        [UnityTest]
        public IEnumerator Weapon_pickup_pain_and_death_are_local()
        {
            yield return LoadLevel();
            var player = GameObject.Find("Player");
            var sound = Object.FindAnyObjectByType<SoundSystem>();
            var weapons = player.GetComponent<PlayerWeapons>();
            var inventory = player.GetComponent<PlayerInventory>();
            var health = player.GetComponent<PlayerHealth>();

            weapons.FireOnceForTest();
            Assert.That(sound.LastPlayedLump, Is.EqualTo("DSPISTOL"));
            Assert.That(sound.WasPlayed("DSPISTOL"), Is.True);

            health.TakeDamage(50);
            Assert.That(health.Health, Is.EqualTo(50));
            Assert.That(inventory.TryPickup(2011), Is.True);
            Assert.That(sound.LastPlayedLump, Is.EqualTo("DSITEMUP"));

            // Rejected stim at full HP stays silent (probe unchanged).
            while (health.Health < 100) health.GiveHealth(10, 100);
            string before = sound.LastPlayedLump;
            Assert.That(inventory.TryPickup(2011), Is.False);
            Assert.That(sound.LastPlayedLump, Is.EqualTo(before));

            health.TakeDamage(20);
            Assert.That(sound.LastPlayedLump, Is.EqualTo("DSPLPAIN"));
            Assert.That(health.IsDead, Is.False);

            health.TakeDamage(999);
            Assert.That(health.IsDead, Is.True);
            Assert.That(sound.LastPlayedLump, Is.EqualTo("DSPLDETH"));
            Assert.That(sound.WasPlayed("DSPLPAIN"), Is.True);
        }

        [UnityTest]
        public IEnumerator Monster_cues_are_spatial()
        {
            yield return LoadLevel();
            var sound = Object.FindAnyObjectByType<SoundSystem>();
            var monster = Object.FindObjectsByType<MonsterController>(FindObjectsSortMode.None)
                .FirstOrDefault(m => m.Brain.State == MonsterState.Sleep && !m.IsAmbush);
            Assert.That(monster, Is.Not.Null, "need a sleeping non-ambush monster");

            Assert.That(Object.FindAnyObjectByType<MapLoader>().Sound, Is.Not.Null);
            monster.NotifyNoise();
            Assert.That(monster.Brain.State, Is.EqualTo(MonsterState.Chase));
            string sight = sound.LastPlayedLump;
            Assert.That(sight, Is.Not.Null.And.Not.Empty);
            Assert.That(sight.StartsWith("DSPOSIT") || sight.StartsWith("DSBGSIT") || sight == "DSSGTSIT",
                Is.True, $"unexpected sight cue {sight}");

            var src = sound.PlayAt("DSPOPAIN", monster.transform.position);
            Assert.That(src.spatialBlend, Is.EqualTo(1f));
            Assert.That(src.transform.position, Is.EqualTo(monster.transform.position));

            monster.NotifyKilled();
            string death = sound.LastPlayedLump;
            Assert.That(
                death.StartsWith("DSPODTH") || death.StartsWith("DSBGDTH") || death == "DSSGTDTH",
                Is.True, $"unexpected death cue {death}");
        }

        [UnityTest]
        public IEnumerator Door_plays_open_at_sector_origin()
        {
            yield return LoadLevel();
            var sound = Object.FindAnyObjectByType<SoundSystem>();
            var activator = Object.FindAnyObjectByType<LineActivator>();
            Assert.That(activator, Is.Not.Null);

            // Find a push door on E1M1 (special 1 is DR door).
            string path = System.IO.Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = Doom.Wad.WadFile.Open(path);
            var map = Doom.Map.MapData.Load(wad, "E1M1");
            int doorLine = -1;
            for (int i = 0; i < map.LineDefs.Length; i++)
            {
                if (map.LineDefs[i].Special == 1) { doorLine = i; break; }
            }
            Assert.That(doorLine, Is.GreaterThanOrEqualTo(0), "E1M1 has a type-1 door");

            activator.ActivateLineForTest(doorLine);
            Assert.That(sound.WasPlayed("DSDOROPN"), Is.True);
            Assert.That(sound.LastPlayedLump, Is.EqualTo("DSDOROPN"));
        }

        [UnityTest]
        public IEnumerator Locked_door_denial_is_local()
        {
            yield return LoadLevel("E1M2");
            var sound = Object.FindAnyObjectByType<SoundSystem>();
            var activator = Object.FindAnyObjectByType<LineActivator>();
            string path = System.IO.Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = Doom.Wad.WadFile.Open(path);
            var map = Doom.Map.MapData.Load(wad, "E1M2");

            int doorLine = -1, doorSector = -1;
            for (int i = 0; i < map.LineDefs.Length; i++)
            {
                var ld = map.LineDefs[i];
                if (!Doom.Specials.LineSpecialTable.TryGet(ld.Special, out var sp)) continue;
                if (sp.Category != Doom.Specials.SpecialCategory.LockedDoor ||
                    sp.Key == Doom.Specials.KeyKind.None) continue;
                if (ld.Tag == 0)
                {
                    int back = ld.BackSideIdx >= 0 ? map.SideDefs[ld.BackSideIdx].SectorIdx : -1;
                    if (back < 0) continue;
                    doorLine = i; doorSector = back; break;
                }
                for (int s = 0; s < map.Sectors.Length; s++)
                {
                    if (map.Sectors[s].Tag != ld.Tag) continue;
                    doorLine = i; doorSector = s; break;
                }
                if (doorLine >= 0) break;
            }
            Assert.That(doorLine, Is.GreaterThanOrEqualTo(0));
            float ceilBefore = activator.GetSectorCeilForTest(doorSector);
            activator.ActivateLineForTest(doorLine);
            Assert.That(sound.LastPlayedLump, Is.EqualTo("DSNOWAY").Or.EqualTo("DSOOF"));
            Assert.That(activator.IsSectorMovingForTest(doorSector), Is.False);
            Assert.That(activator.GetSectorCeilForTest(doorSector), Is.EqualTo(ceilBefore));
        }
    }
}
