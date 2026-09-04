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
        public IEnumerator Audio_bootstrap_and_music()
        {
            yield return LoadLevel();

            var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            Assert.That(listeners.Length, Is.EqualTo(1), "exactly one AudioListener");

            var systems = Object.FindObjectsByType<SoundSystem>(FindObjectsSortMode.None);
            Assert.That(systems.Length, Is.EqualTo(1), "exactly one SoundSystem");
            var sound = systems[0];
            Assert.That(sound.Cache, Is.Not.Null);
            Assert.That(sound.Cache.IsCached("DSPISTOL"), Is.True);
            Assert.That(sound.Cache.IsCached("DSTELEPT"), Is.True);

            var musicPlayers = Object.FindObjectsByType<MusicPlayer>(FindObjectsSortMode.None);
            Assert.That(musicPlayers.Length, Is.EqualTo(1), "exactly one MusicPlayer");
            var music = musicPlayers[0];
            Assert.That(music.TrackName, Is.EqualTo("D_E1M1"));
            Assert.That(music.IsActive, Is.True);
            Assert.That(music.ClipName, Is.EqualTo("D_E1M1"));

            // Batchmode often skips PCM callbacks; pump the sequencer directly.
            Assert.That(music.RenderForTest(1024), Is.EqualTo(1024));
            Assert.That(music.RenderedFrames, Is.GreaterThan(0));

            var local = sound.PlayLocal("DSPISTOL");
            Assert.That(local, Is.Not.Null);
            Assert.That(local.clip.name, Is.EqualTo("DSPISTOL"));
            Assert.That(local.spatialBlend, Is.EqualTo(0f));

            var world = sound.PlayAt("DSDOROPN", new Vector3(10f, 0f, 10f));
            Assert.That(world.spatialBlend, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator Weapon_and_pickup_are_local()
        {
            yield return LoadLevel();
            var player = GameObject.Find("Player");
            var sound = Object.FindAnyObjectByType<SoundSystem>();
            var weapons = player.GetComponent<PlayerWeapons>();
            var inventory = player.GetComponent<PlayerInventory>();
            var health = player.GetComponent<PlayerHealth>();

            weapons.FireOnceForTest();
            Assert.That(sound.LastPlayedLump, Is.EqualTo("DSPISTOL"));
            var pistolSrc = sound.PlayLocal("DSPISTOL");
            Assert.That(pistolSrc.spatialBlend, Is.EqualTo(0f));

            health.TakeDamage(50);
            Assert.That(health.Health, Is.EqualTo(50));
            Assert.That(inventory.TryPickup(2011), Is.True);
            Assert.That(sound.LastPlayedLump, Is.EqualTo("DSITEMUP"));
        }

        [UnityTest]
        public IEnumerator Rejected_pickup_is_silent()
        {
            yield return LoadLevel();
            var player = GameObject.Find("Player");
            var sound = Object.FindAnyObjectByType<SoundSystem>();
            var inventory = player.GetComponent<PlayerInventory>();
            var health = player.GetComponent<PlayerHealth>();

            while (health.Health < 100) health.GiveHealth(10, 100);
            string before = sound.LastPlayedLump;
            Assert.That(inventory.TryPickup(2011), Is.False);
            Assert.That(sound.LastPlayedLump, Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator Monster_cues_are_spatial()
        {
            yield return LoadLevel();
            var sound = Object.FindAnyObjectByType<SoundSystem>();
            var monster = Object.FindObjectsByType<MonsterController>(FindObjectsSortMode.None)
                .FirstOrDefault(m => m.Brain.State == MonsterState.Sleep && !m.IsAmbush);
            Assert.That(monster, Is.Not.Null, "need a sleeping non-ambush monster");

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
        public IEnumerator Imp_impact_plays_explosion()
        {
            yield return LoadLevel();
            var sound = Object.FindAnyObjectByType<SoundSystem>();
            var src = sound.PlayAt("DSFIRXPL", new Vector3(3f, 1f, 3f));
            Assert.That(src, Is.Not.Null);
            Assert.That(src.clip.name, Is.EqualTo("DSFIRXPL"));
            Assert.That(sound.WasPlayed("DSFIRXPL"), Is.True);
        }

        [UnityTest]
        public IEnumerator Door_and_lift_manage_world_sources()
        {
            yield return LoadLevel();
            var sound = Object.FindAnyObjectByType<SoundSystem>();
            var activator = Object.FindAnyObjectByType<LineActivator>();
            Assert.That(activator, Is.Not.Null);

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
            // EV_VerticalDoor / EV_DoLockedDoor: sfx_oof, never sfx_noway (that is P_UseLines' "nothing usable").
            Assert.That(sound.LastPlayedLump, Is.EqualTo("DSOOF"));
            Assert.That(sound.WasPlayed("DSNOWAY"), Is.False);
            Assert.That(activator.IsSectorMovingForTest(doorSector), Is.False);
            Assert.That(activator.GetSectorCeilForTest(doorSector), Is.EqualTo(ceilBefore));
        }

        [UnityTest]
        public IEnumerator Closed_wad_prewarm_does_not_reread()
        {
            yield return LoadLevel();
            var sound = Object.FindAnyObjectByType<SoundSystem>();
            Assert.That(sound.Cache.IsCached("DSPISTOL"), Is.True);
            var clip = sound.Cache.Get("DSPISTOL");
            Assert.That(clip, Is.Not.Null);
            Assert.That(sound.Cache.Get("DSPISTOL"), Is.SameAs(clip));
            Assert.That(sound.Cache.Get("DSNEVERPREWARMEDXYZ"), Is.Null);
            Assert.That(sound.Cache.Get("DSNEVERPREWARMEDXYZ"), Is.Null);
        }

        [UnityTest]
        public IEnumerator Player_pain_and_death_cues()
        {
            yield return LoadLevel();
            var player = GameObject.Find("Player");
            var sound = Object.FindAnyObjectByType<SoundSystem>();
            var health = player.GetComponent<PlayerHealth>();

            health.TakeDamage(20);
            Assert.That(sound.LastPlayedLump, Is.EqualTo("DSPLPAIN"));
            Assert.That(health.IsDead, Is.False);

            health.TakeDamage(999);
            Assert.That(health.IsDead, Is.True);
            Assert.That(sound.LastPlayedLump, Is.EqualTo("DSPLDETH"));
        }

        [UnityTest]
        public IEnumerator Source_reuse_resets_pitch_and_local_state()
        {
            yield return LoadLevel();
            string path = System.IO.Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = Doom.Wad.WadFile.Open(path);
            var cache = new SoundCache(wad);
            cache.Get("DSDOROPN");
            cache.Get("DSPISTOL");

            var go = new GameObject("SoundPolicyReuseTest");
            var sound = go.AddComponent<SoundSystem>();
            sound.Init(cache, 1f / 32f, poolSize: 1, randomSeed: 0);

            var world = sound.PlayAt("DSDOROPN", Vector3.one);
            Assert.That(world, Is.Not.Null);
            Assert.That(world.pitch, Is.EqualTo(1.0625f).Within(0.0001f));
            Assert.That(world.spatialBlend, Is.EqualTo(1f));

            var local = sound.PlayLocal("DSPISTOL");
            Assert.That(local, Is.SameAs(world), "higher-priority local cue should reuse the channel");
            Assert.That(local.pitch, Is.EqualTo(1f));
            Assert.That(local.spatialBlend, Is.EqualTo(0f));
            Assert.That(local.loop, Is.False);

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Tracked_loop_is_not_stolen_when_pool_is_full()
        {
            yield return LoadLevel();
            string path = System.IO.Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = Doom.Wad.WadFile.Open(path);
            var cache = new SoundCache(wad);
            cache.Get("DSSTNMOV");
            cache.Get("DSPISTOL");

            var go = new GameObject("SoundPolicyLoopTest");
            var sound = go.AddComponent<SoundSystem>();
            sound.Init(cache, 1f / 32f, poolSize: 1);
            var owner = new object();

            sound.PlayLoop("DSSTNMOV", owner, Vector3.zero);
            Assert.That(sound.ActiveLoopCount, Is.EqualTo(1));
            Assert.That(sound.PlayLocal("DSPISTOL"), Is.Null);
            Assert.That(sound.ActiveLoopCount, Is.EqualTo(1));

            sound.StopLoop(owner);
            Assert.That(sound.ActiveLoopCount, Is.Zero);
            Assert.That(sound.PlayLocal("DSPISTOL"), Is.Not.Null);

            Object.Destroy(go);
            yield return null;
        }
    }
}
