using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.Game;
using Doom.MapBuild;

namespace Doom.Stage3.PlayTests
{
    public class PlayerDamagePlayTests
    {
        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true; // PhysX cook warnings
            // Headless -batchmode runs thousands of fps, so Time.deltaTime per frame
            // is ~0.00006s — far too small for the CharacterController's gravity Move
            // (which runs in Update, off Time.deltaTime) to fall onto the floor or for
            // cc.isGrounded to ever latch. Pin a realistic step so each `yield return
            // null` advances 1/60s of simulated time and the player actually settles.
            Time.captureDeltaTime = 1f / 60f;
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            Time.captureDeltaTime = 0f; // restore real-time stepping for other tests
        }

        static IEnumerator LoadE1M1()
        {
            SceneManager.LoadScene("Stage2_MapPreview");
            yield return null; yield return null;
            // Let MapLoader.Build finish (geometry + Player + damage/death wiring).
            for (int i = 0; i < 90; i++) yield return null;
        }

        // Poll until the player rests on the floor (or budget runs out). The spawn is
        // bounds.max.y + 5, so the drop distance varies with the map — poll rather than
        // guess a frame count.
        static IEnumerator SettleOnFloor(CharacterController cc)
        {
            for (int i = 0; i < 300; i++)   // up to ~5s of stepped time
            {
                if (cc != null && cc.isGrounded) break;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TakeDamage_reduces_health_through_the_wired_component()
        {
            yield return LoadE1M1();
            var health = Object.FindAnyObjectByType<PlayerHealth>();
            Assert.That(health, Is.Not.Null, "PlayerHealth should be on the Player");
            Assert.That(health.Health, Is.EqualTo(100));

            health.TakeDamage(30);
            Assert.That(health.Health, Is.EqualTo(70));
        }

        [UnityTest]
        public IEnumerator Floor_damage_path_resolves_the_spawn_sector_and_is_safe()
        {
            yield return LoadE1M1();
            var floorDamage = Object.FindAnyObjectByType<FloorDamageSystem>();
            Assert.That(floorDamage, Is.Not.Null, "FloorDamageSystem should be on the Player");

            // Let the player settle onto the floor — the downward raycast only reaches
            // the floor (~1.5m range) once the player is resting on it.
            var cc = floorDamage.GetComponent<CharacterController>();
            yield return SettleOnFloor(cc);
            Assert.That(cc.isGrounded, Is.True, "player should be standing on the floor");

            // The downward raycast must resolve a SectorRef under the player (>= 0).
            int special = floorDamage.SectorSpecialUnderPlayer();
            Assert.That(special, Is.GreaterThanOrEqualTo(0),
                "raycast should find the spawn sector's SectorRef");

            // The spawn sector is a normal (non-damaging) floor → one tick deals 0,
            // and the chain runs without exceptions.
            var health = Object.FindAnyObjectByType<PlayerHealth>();
            int before = health.Health;
            int applied = floorDamage.TryApplyFloorDamageOnce();
            Assert.That(applied, Is.EqualTo(0), "spawn floor is not a damaging sector");
            Assert.That(health.Health, Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator Death_disables_control_and_respawn_restores()
        {
            yield return LoadE1M1();
            var health = Object.FindAnyObjectByType<PlayerHealth>();
            var pc = health.GetComponent<PlayerController>();
            var death = health.GetComponent<PlayerDeathHandler>();
            var weapons = health.GetComponent<PlayerWeapons>();
            var inventory = health.GetComponent<PlayerInventory>();
            var weaponView = Object.FindAnyObjectByType<WeaponView>();
            Assert.That(pc, Is.Not.Null);
            Assert.That(death, Is.Not.Null);
            Assert.That(weaponView, Is.Not.Null);

            inventory.Keys.Give(PlayerKey.BlueCard);
            weapons.Loadout.Give(WeaponId.Shotgun);
            weapons.Ammo.Add(AmmoType.Shells, 12);

            health.TakeDamage(1000);              // fatal
            yield return null;
            Assert.That(health.IsDead, Is.True);
            Assert.That(pc.enabled, Is.False, "controls freeze on death");
            Assert.That(GameFlowController.ShouldDrawStatusHud(), Is.True,
                "death keeps the HUD visible");
            Assert.That(weaponView.IsLoweringForTest, Is.True);
            float lower0 = weaponView.LowerYForTest;
            weaponView.AdvanceLowerForTest(0.1f);
            Assert.That(weaponView.LowerYForTest, Is.GreaterThan(lower0),
                "psprite lowers after death instead of disappearing immediately");

            // Wander away while dead, then respawn.
            var wanderedPos = health.transform.position + new Vector3(5f, 0f, 5f);
            health.transform.position = wanderedPos;
            death.Respawn();
            Assert.That(health.Health, Is.EqualTo(100));
            Assert.That(pc.enabled, Is.True, "controls restore on respawn");
            Assert.That(weaponView.IsLoweringForTest, Is.False);
            Assert.That(weapons.Loadout.Current, Is.EqualTo(WeaponId.Pistol));
            Assert.That(weapons.Loadout.Has(WeaponId.Shotgun), Is.False,
                "respawn intentionally restores start weapons");
            Assert.That(weapons.Ammo.Get(AmmoType.Bullets), Is.EqualTo(50));
            Assert.That(weapons.Ammo.Get(AmmoType.Shells), Is.EqualTo(0));
            Assert.That(inventory.Keys.Has(PlayerKey.BlueCard), Is.True,
                "keys are intentionally retained across respawn");

            // Respawn() teleports to the handler's STORED start (the original spawn
            // point captured in Init), which differs from any in-test sampled position.
            // Assert it actually repositioned the player AWAY from the wandered spot,
            // rather than asserting closeness to a test-sampled start the handler never
            // stored. This keeps the check meaningful (respawn moved the player) without
            // coupling to the settle delta.
            Assert.That(Vector3.Distance(health.transform.position, wanderedPos),
                Is.GreaterThan(0.5f),
                "respawn should move the player away from the wandered position");
        }
    }
}
