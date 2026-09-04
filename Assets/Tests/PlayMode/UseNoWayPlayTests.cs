using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.Map;
using Doom.MapBuild;
using Doom.Wad;

namespace Doom.Stage3.PlayTests
{
    /// PTR_UseTraverse: a use that reaches a line without a special and
    /// without an opening (a bare one-sided wall) stops there with sfx_noway,
    /// played at the player. The port never played that cue before
    /// 2026-09-04 (it had spent DSNOWAY on key denials instead).
    public class UseNoWayPlayTests
    {
        const float WS = 1f / 32f;

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

        static void Teleport(GameObject player, Vector3 pos)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = pos;
            if (cc != null) cc.enabled = true;
        }

        [UnityTest]
        public IEnumerator Using_a_bare_wall_plays_noway_at_the_player()
        {
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return null; yield return null;
            for (int i = 0; i < 90; i++) yield return null;
            Time.captureDeltaTime = 1f / 60f;

            var player = GameObject.Find("Player");
            var activator = player.GetComponent<LineActivator>();
            var look = player.GetComponent<PlayerController>();
            var sound = Object.FindAnyObjectByType<MapLoader>().Sound;
            Assert.That(activator, Is.Not.Null);
            Assert.That(look, Is.Not.Null);
            Assert.That(sound, Is.Not.Null);

            // The longest one-sided line without a special on E1M1: a bare wall
            // with room in front of it.
            string path = Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");
            int wall = -1; float bestLen = 0f;
            Vector2 v1 = default, v2 = default; float floor = 0f;
            using (var wad = WadFile.Open(path))
            {
                var map = MapData.Load(wad, "E1M1");
                for (int i = 0; i < map.LineDefs.Length; i++)
                {
                    var ld = map.LineDefs[i];
                    if (ld.Special != 0 || ld.IsTwoSided || ld.FrontSideIdx < 0) continue;
                    var a = map.Vertexes[ld.V1]; var b = map.Vertexes[ld.V2];
                    float len = Vector2.Distance(new Vector2(a.X, a.Y), new Vector2(b.X, b.Y));
                    if (len <= bestLen) continue;
                    bestLen = len; wall = i;
                    v1 = new Vector2(a.X, a.Y); v2 = new Vector2(b.X, b.Y);
                    floor = map.Sectors[map.SideDefs[ld.FrontSideIdx].SectorIdx].FloorHeight;
                }
            }
            Assert.That(wall, Is.GreaterThanOrEqualTo(0));

            // Front side is to the right of v1→v2 (DOOM convention): stand 24
            // units off the middle on that side and face the wall.
            Vector2 d = (v2 - v1).normalized;
            Vector2 n = new Vector2(d.y, -d.x);
            Vector2 mid = (v1 + v2) * 0.5f;
            Vector2 stand = mid + n * 24f;
            Teleport(player, new Vector3(stand.x * WS, (floor + 1f) * WS, stand.y * WS));
            Vector2 face = -n;
            look.SetView(Mathf.Atan2(face.x, face.y) * Mathf.Rad2Deg, 0f);
            yield return null;

            Assert.That(sound.PlayCountForTest("DSNOWAY"), Is.Zero);
            activator.TryUse();
            Assert.That(sound.PlayCountForTest("DSNOWAY"), Is.EqualTo(1),
                "line " + wall + ": a use into a bare wall grunts noway");

            // No cooldown in vanilla: every press grunts again.
            activator.TryUse();
            Assert.That(sound.PlayCountForTest("DSNOWAY"), Is.EqualTo(2));
            Assert.That(sound.PlayCountForTest("DSOOF"), Is.Zero, "oof is the key denial, not this");
        }
    }
}
