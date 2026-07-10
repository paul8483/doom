using System.Collections;
using System.IO;
using System.Linq;
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
    public class PickupPlayTests
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

        static IEnumerator SettleOnFloor(CharacterController cc)
        {
            for (int i = 0; i < 300; i++)
            {
                if (cc != null && cc.isGrounded) break;
                yield return null;
            }
        }

        static void TeleportOnto(GameObject player, CharacterController cc, Vector3 pos)
        {
            cc.enabled = false;
            player.transform.position = pos;
            cc.enabled = true;
        }

        [UnityTest]
        public IEnumerator Stim_heals_and_destroys()
        {
            yield return LoadLevel();
            var player = GameObject.Find("Player");
            var health = player.GetComponent<PlayerHealth>();
            var inv = player.GetComponent<PlayerInventory>();
            var cc = player.GetComponent<CharacterController>();
            yield return SettleOnFloor(cc);

            health.TakeDamage(50);
            Assert.That(health.Health, Is.EqualTo(50));

            var stim = new GameObject("Thing_2011_STIM");
            stim.transform.position = player.transform.position;
            stim.AddComponent<ThingPickup>().Init(2011, 1f / 32f);

            for (int i = 0; i < 10; i++) { cc.Move(new Vector3(0.01f, 0f, 0f)); yield return null; }

            Assert.That(health.Health, Is.EqualTo(60));
            Assert.That(stim == null || !stim, Is.True, "stim GO destroyed");

            // At full HP, stim is rejected and stays.
            health.GiveHealth(100, 100); // already 60; push toward 100 via damage undo
            while (health.Health < 100) health.GiveHealth(10, 100);

            var stim2 = new GameObject("Thing_2011_STIM2");
            stim2.transform.position = player.transform.position;
            stim2.AddComponent<ThingPickup>().Init(2011, 1f / 32f);
            for (int i = 0; i < 10; i++) { cc.Move(new Vector3(0.01f, 0f, 0f)); yield return null; }

            Assert.That(stim2 != null && stim2, Is.True, "stim remains at full HP");
            Assert.That(inv.TryPickup(2011), Is.False);
        }

        [UnityTest]
        public IEnumerator Green_armor_absorbs()
        {
            yield return LoadLevel();
            var player = GameObject.Find("Player");
            var health = player.GetComponent<PlayerHealth>();
            var inv = player.GetComponent<PlayerInventory>();

            Assert.That(inv.TryPickup(2018), Is.True);
            Assert.That(health.Armor, Is.EqualTo(100));
            Assert.That(health.ArmorType, Is.EqualTo(ArmorKind.Green));

            int hpBefore = health.Health;
            health.TakeDamage(30); // green saves 10 → HP -20
            Assert.That(health.Health, Is.EqualTo(hpBefore - 20));
            Assert.That(health.Armor, Is.EqualTo(90));
        }

        [UnityTest]
        public IEnumerator Key_gates_locked_door()
        {
            // E1M2 has locked doors; E1M1 often does not.
            yield return LoadLevel("E1M2");
            var activator = Object.FindAnyObjectByType<LineActivator>();
            var inv = Object.FindAnyObjectByType<PlayerInventory>();
            Assert.That(activator, Is.Not.Null);
            Assert.That(inv, Is.Not.Null);

            string path = Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = WadFile.Open(path);
            var map = MapData.Load(wad, "E1M2");

            int doorLine = -1, doorSector = -1;
            KeyKind need = KeyKind.None;
            for (int i = 0; i < map.LineDefs.Length; i++)
            {
                var ld = map.LineDefs[i];
                if (ld.Special == 0) continue;
                if (!LineSpecialTable.TryGet(ld.Special, out var sp)) continue;
                if (sp.Category != SpecialCategory.LockedDoor || sp.Key == KeyKind.None) continue;
                if (ld.Tag == 0)
                {
                    int back = ld.BackSideIdx >= 0 ? map.SideDefs[ld.BackSideIdx].SectorIdx : -1;
                    if (back < 0) continue;
                    doorLine = i; doorSector = back; need = sp.Key; break;
                }
                for (int s = 0; s < map.Sectors.Length; s++)
                {
                    if (map.Sectors[s].Tag != ld.Tag) continue;
                    doorLine = i; doorSector = s; need = sp.Key; break;
                }
                if (doorLine >= 0) break;
            }

            Assert.That(doorLine, Is.GreaterThanOrEqualTo(0), "E1M2 should have a locked door");
            float ceil0 = activator.GetSectorCeilForTest(doorSector);

            activator.ActivateLineForTest(doorLine);
            yield return null;
            Assert.That(activator.GetSectorCeilForTest(doorSector), Is.EqualTo(ceil0),
                "locked door must not move without key");

            Assert.That(KeyMapping.ToPlayerKey(need, out var pk), Is.True);
            inv.Keys.Give(pk);

            activator.ActivateLineForTest(doorLine);
            for (int i = 0; i < 30; i++) yield return null;
            Assert.That(activator.GetSectorCeilForTest(doorSector), Is.Not.EqualTo(ceil0),
                "locked door opens with key");
        }

        [UnityTest]
        public IEnumerator Poss_drops_clip()
        {
            yield return LoadLevel();
            var poss = Object.FindObjectsByType<MonsterController>(FindObjectsSortMode.None)
                .FirstOrDefault(m => m.gameObject.name.StartsWith("Thing_3004"));
            Assert.That(poss, Is.Not.Null, "E1M1 should have a zombie");

            var eh = poss.GetComponent<EnemyHealth>();
            Vector3 at = poss.transform.position;
            eh.TakeDamage(10_000);
            yield return null; yield return null;

            var clip = Object.FindObjectsByType<ThingPickup>(FindObjectsSortMode.None)
                .FirstOrDefault(p => p.DoomedNum == 2007 &&
                    (p.transform.position - at).sqrMagnitude < 1f);
            Assert.That(clip, Is.Not.Null, "POSS death should spawn CLIP pickup");
        }

        [UnityTest]
        public IEnumerator IronFeet_blocks_floor_damage()
        {
            yield return LoadLevel();
            var player = GameObject.Find("Player");
            var health = player.GetComponent<PlayerHealth>();
            var inv = player.GetComponent<PlayerInventory>();
            var floor = player.GetComponent<FloorDamageSystem>();
            var cc = player.GetComponent<CharacterController>();
            yield return SettleOnFloor(cc);

            string path = Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = WadFile.Open(path);
            var map = MapData.Load(wad, "E1M1");

            // Find a damaging floor SectorRef and stand on its collider surface.
            SectorRef hurtFloor = null;
            Collider hurtCol = null;
            foreach (var sref in Object.FindObjectsByType<SectorRef>(FindObjectsSortMode.None))
            {
                if (sref.SectorIndex < 0 || sref.SectorIndex >= map.Sectors.Length) continue;
                if (SectorDamageTable.DamagePerTick(map.Sectors[sref.SectorIndex].Special) <= 0)
                    continue;
                if (sref.gameObject.name != "Floor") continue;
                var col = sref.GetComponent<Collider>();
                if (col == null) continue;
                hurtFloor = sref;
                hurtCol = col;
                break;
            }
            Assert.That(hurtFloor, Is.Not.Null, "E1M1 should have a damaging floor");

            // Drop onto the mesh from above its bounds center so the feet land on
            // that Floor collider (transform.position alone can miss thin floors).
            Vector3 above = hurtCol.bounds.center + Vector3.up * 2f;
            Assert.That(Physics.Raycast(above, Vector3.down, out var hit, 10f,
                    ~0, QueryTriggerInteraction.Ignore), Is.True);
            TeleportOnto(player, cc, hit.point + Vector3.up * 0.02f);
            for (int i = 0; i < 120; i++)
            {
                cc.Move(Vector3.down * 0.05f);
                yield return null;
                if (cc.isGrounded &&
                    SectorDamageTable.DamagePerTick(floor.SectorSpecialUnderPlayer()) > 0)
                    break;
            }
            Assert.That(cc.isGrounded, Is.True, "player should be grounded on nukage");
            Assert.That(SectorDamageTable.DamagePerTick(floor.SectorSpecialUnderPlayer()),
                Is.GreaterThan(0), "raycast should see the damaging sector");

            int before = health.Health;
            int applied = floor.TryApplyFloorDamageOnce();
            Assert.That(applied, Is.GreaterThan(0), "nukage should damage without suit");
            Assert.That(health.Health, Is.LessThan(before));

            inv.Powers.GiveIronFeet(2100);
            before = health.Health;
            applied = floor.TryApplyFloorDamageOnce();
            Assert.That(applied, Is.EqualTo(0), "suit blocks floor damage");
            Assert.That(health.Health, Is.EqualTo(before));
        }
    }
}
