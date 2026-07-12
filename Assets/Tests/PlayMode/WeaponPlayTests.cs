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
    public class WeaponPlayTests
    {
        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
        }

        static IEnumerator LoadLevel()
        {
            LogAssert.ignoreFailingMessages = true; // PhysX cook warnings
            Time.captureDeltaTime = 1f / 60f;
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return null; yield return null;
            // Let MapLoader.Build finish (geometry + Player + weapons wiring).
            for (int i = 0; i < 90; i++) yield return null;
        }

        // The player spawns at bounds.max.y + 5 and free-falls onto the floor; poll
        // rather than guess a frame count so the camera has actually settled before
        // we anchor a fixed-world-space target off it (PlayerDamagePlayTests pattern).
        static IEnumerator SettleOnFloor(CharacterController cc)
        {
            for (int i = 0; i < 300; i++)
            {
                if (cc != null && cc.isGrounded) break;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Pistol_kills_synthetic_enemy_and_drains_ammo()
        {
            yield return LoadLevel();
            var player = GameObject.Find("Player");
            var weapons = player.GetComponent<PlayerWeapons>();
            var cc = player.GetComponent<CharacterController>();
            yield return SettleOnFloor(cc); // camera must stop moving before we anchor a fixed target off it
            var cam = Camera.main.transform;

            // Synthetic target right in front of the camera: capsule + EnemyHealth 20 HP.
            var target = new GameObject("TestTarget");
            target.transform.position = cam.position + cam.forward * 3f - Vector3.up * 0.8f;
            var col = target.AddComponent<CapsuleCollider>();
            col.height = 1.75f; col.radius = 0.5f;
            col.center = new Vector3(0f, 0.875f, 0f);
            var eh = target.AddComponent<EnemyHealth>();
            eh.Init(20, corpseFrame: -1, billboard: null, capsule: col);

            int startAmmo = weapons.Ammo.Get(AmmoType.Bullets);
            // 20 HP / 5 min damage = at most 4 shots; fire with margin.
            for (int i = 0; i < 8 && !eh.IsDead; i++)
            {
                weapons.FireOnceForTest();
                // wait out the cooldown (~19 tics ≈ 0.54s at dt=1/60)
                for (int f = 0; f < 40; f++) { if (weapons.CooldownForTest <= 0f) break; yield return null; }
            }

            Assert.That(eh.IsDead, Is.True, "enemy died from the pistol");
            Assert.That(col.enabled, Is.False, "corpse collider is disabled");
            Assert.That(weapons.Ammo.Get(AmmoType.Bullets), Is.LessThan(startAmmo),
                "ammo was spent");
        }

        [UnityTest]
        public IEnumerator Empty_ammo_autoswitches_to_fist()
        {
            yield return LoadLevel();
            var weapons = GameObject.Find("Player").GetComponent<PlayerWeapons>();

            while (weapons.Ammo.TryConsume(AmmoType.Bullets, 1)) { }
            Assert.That(weapons.Loadout.Current, Is.EqualTo(WeaponId.Pistol));
            weapons.FireOnceForTest(); // out of ammo -> auto-switch
            Assert.That(weapons.Loadout.Current, Is.EqualTo(WeaponId.Fist));
        }

        [UnityTest]
        public IEnumerator Slot_requests_queue_until_action_end_and_last_valid_wins()
        {
            yield return LoadLevel();
            var weapons = GameObject.Find("Player").GetComponent<PlayerWeapons>();
            weapons.Loadout.Give(WeaponId.Shotgun);
            weapons.Loadout.TrySelect(WeaponId.Pistol);

            weapons.FireOnceForTest();
            Assert.That(weapons.Scheduler.IsRunning, Is.True);
            weapons.SelectSlotForTest(1); // fist
            weapons.SelectSlotForTest(7); // unowned BFG: must not replace fist
            Assert.That(weapons.Loadout.Current, Is.EqualTo(WeaponId.Pistol));
            Assert.That(weapons.Loadout.Pending, Is.EqualTo(WeaponId.Fist));
            weapons.SelectSlotForTest(3); // owned shotgun: last valid wins
            Assert.That(weapons.Loadout.Pending, Is.EqualTo(WeaponId.Shotgun));

            weapons.AdvanceTicsForTest(100);
            Assert.That(weapons.Scheduler.IsRunning, Is.False);
            Assert.That(weapons.Loadout.Current, Is.EqualTo(WeaponId.Shotgun));
            Assert.That(weapons.Loadout.HasPending, Is.False);
        }

        [UnityTest]
        public IEnumerator Shotgun_pickup_gives_weapon_and_shells()
        {
            yield return LoadLevel();
            var player = GameObject.Find("Player");
            var weapons = player.GetComponent<PlayerWeapons>();
            var cc = player.GetComponent<CharacterController>();

            // Find a shotgun (Thing_2001_*) on the map; if E1M1 has none, create one.
            var allPickups = GameObject.FindObjectsByType<ThingPickup>(FindObjectsSortMode.None);
            var pickupGo = allPickups.Select(p => p.gameObject)
                .FirstOrDefault(g => g.name.StartsWith("Thing_2001"));
            if (pickupGo == null)
            {
                pickupGo = new GameObject("Thing_2001_SHOT");
                pickupGo.transform.position = player.transform.position;
                pickupGo.AddComponent<ThingPickup>().Init(2001, 1f / 32f);
            }
            else
            {
                // E1M1 places several shotgun/ammo pickups near each other (e.g. a
                // shotgun with shell boxes only 32 DU away); deactivate every OTHER
                // pickup GO so teleporting next to one can't also sweep a neighbor's
                // trigger and add extra shells (non-deterministic ammo count).
                // NOTE: `p.enabled = false` would NOT work — Unity sends trigger
                // events to disabled MonoBehaviours (see OnTriggerEnter docs); only
                // deactivating the GameObject (or its collider) suppresses them.
                foreach (var p in allPickups)
                    if (p != null && p.gameObject != pickupGo)
                        p.gameObject.SetActive(false);
            }

            // Teleport onto the item + micro-move so the CharacterController
            // generates OnTriggerEnter.
            cc.enabled = false;
            player.transform.position = pickupGo.transform.position;
            cc.enabled = true;
            for (int i = 0; i < 10; i++) { cc.Move(new Vector3(0.01f, 0f, 0f)); yield return null; }

            Assert.That(weapons.Loadout.Has(WeaponId.Shotgun), Is.True, "shotgun picked up");
            Assert.That(weapons.Ammo.Get(AmmoType.Shells), Is.EqualTo(8), "+8 shells");
            Assert.That(pickupGo == null || !pickupGo, Is.True, "item GO destroyed");
        }

        [UnityTest]
        public IEnumerator Rocket_launcher_pickup_fires_projectile_and_spends_rocket()
        {
            yield return LoadLevel();
            var player = GameObject.Find("Player");
            var weapons = player.GetComponent<PlayerWeapons>();
            var cc = player.GetComponent<CharacterController>();

            foreach (var pickup in GameObject.FindObjectsByType<ThingPickup>(
                         FindObjectsSortMode.None))
                pickup.gameObject.SetActive(false);

            var pickupGo = new GameObject("Thing_2003_LAUN");
            pickupGo.transform.position = player.transform.position;
            pickupGo.AddComponent<ThingPickup>().Init(2003, 1f / 32f);

            cc.enabled = false;
            player.transform.position = pickupGo.transform.position;
            cc.enabled = true;
            for (int i = 0; i < 10; i++)
            {
                cc.Move(new Vector3(0.01f, 0f, 0f));
                yield return null;
            }
            Assert.That(weapons.Loadout.Has(WeaponId.RocketLauncher), Is.True);
            Assert.That(weapons.Ammo.Get(AmmoType.Rockets), Is.EqualTo(2));

            weapons.FireOnceForTest();
            Assert.That(weapons.Ammo.Get(AmmoType.Rockets), Is.EqualTo(1));
            Assert.That(
                Object.FindFirstObjectByType<PlayerRocketProjectile>(),
                Is.Not.Null, "player rocket was spawned");
        }

        [UnityTest]
        public IEnumerator Chainsaw_pickup_gives_weapon()
        {
            yield return LoadLevel();
            var player = GameObject.Find("Player");
            var weapons = player.GetComponent<PlayerWeapons>();
            var cc = player.GetComponent<CharacterController>();
            yield return SettleOnFloor(cc);

            foreach (var pickup in GameObject.FindObjectsByType<ThingPickup>(
                         FindObjectsSortMode.None))
                pickup.gameObject.SetActive(false);

            var pickupGo = new GameObject("Thing_2005_CSAW");
            pickupGo.transform.position = player.transform.position;
            pickupGo.AddComponent<ThingPickup>().Init(2005, 1f / 32f);

            cc.enabled = false;
            player.transform.position = pickupGo.transform.position;
            cc.enabled = true;
            for (int i = 0; i < 10; i++)
            {
                cc.Move(new Vector3(0.01f, 0f, 0f));
                yield return null;
            }
            Assert.That(weapons.Loadout.Has(WeaponId.Chainsaw), Is.True, "chainsaw picked up");
            Assert.That(weapons.Loadout.Current, Is.EqualTo(WeaponId.Chainsaw));
        }

        [UnityTest]
        public IEnumerator Rocket_direct_hit_kills_target_and_splash_hurts_player()
        {
            yield return LoadLevel();
            var player = GameObject.Find("Player");
            var weapons = player.GetComponent<PlayerWeapons>();
            var health = player.GetComponent<PlayerHealth>();
            yield return SettleOnFloor(player.GetComponent<CharacterController>());
            var cam = Camera.main.transform;

            Assert.That(weapons.Pickup(2003), Is.True);
            var target = new GameObject("RocketTarget");
            target.transform.position = cam.position + cam.forward * 3f - Vector3.up * 0.8f;
            var col = target.AddComponent<CapsuleCollider>();
            col.height = 1.75f;
            col.radius = 0.5f;
            col.center = new Vector3(0f, 0.875f, 0f);
            var enemy = target.AddComponent<EnemyHealth>();
            enemy.Init(20, corpseFrame: -1, billboard: null, capsule: col);

            weapons.FireOnceForTest();
            for (int i = 0; i < 90 && !enemy.IsDead; i++) yield return null;

            Assert.That(enemy.IsDead, Is.True, "rocket direct hit reached target");
            Assert.That(health.Health, Is.LessThan(HealthModel.MaxHealth),
                "nearby player received rocket splash self-damage");
        }
    }
}
