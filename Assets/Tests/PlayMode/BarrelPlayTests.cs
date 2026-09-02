using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.MapBuild;
using Doom.Things;

namespace Doom.Stage3.PlayTests
{
    public class BarrelPlayTests
    {
        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
        }

        static IEnumerator LoadLevel()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return null; yield return null;
            for (int i = 0; i < 90; i++) yield return null;
        }

        static IEnumerator SettleOnFloor(CharacterController cc)
        {
            for (int i = 0; i < 300; i++)
            {
                if (cc != null && cc.isGrounded) break;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Pistol_explodes_barrel_and_removes_it()
        {
            yield return LoadLevel();
            var player = GameObject.Find("Player");
            var weapons = player.GetComponent<PlayerWeapons>();
            var cc = player.GetComponent<CharacterController>();
            yield return SettleOnFloor(cc);
            var cam = Camera.main.transform;

            // Synthetic barrel in front of the camera (same wiring as ThingSpawner).
            var go = new GameObject("TestBarrel", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.position = cam.position + cam.forward * 3f - Vector3.up * 0.5f;
            var col = go.AddComponent<CapsuleCollider>();
            float r = 10f / 32f;
            float h = 42f / 32f;
            col.radius = r;
            col.height = h;
            col.center = new Vector3(0f, h * 0.5f, 0f);

            var bb = go.AddComponent<SpriteBillboard>();
            // Billboard needs a cache for LateUpdate; leave null-safe — explosion still runs.
            var eh = go.AddComponent<EnemyHealth>();
            eh.Init(BarrelRules.Health, -1, bb, col, countKill: false, noBlood: true);
            var be = go.AddComponent<BarrelExplosion>();
            be.Init(bb, col, cache: null, worldScale: 1f / 32f, sound: null);
            eh.SetBarrel(be);

            for (int i = 0; i < 12 && !eh.IsDead; i++)
            {
                weapons.FireOnceForTest();
                for (int f = 0; f < 40; f++)
                {
                    if (weapons.CooldownForTest <= 0f) break;
                    yield return null;
                }
            }

            Assert.That(eh.IsDead, Is.True, "barrel died from pistol");
            Assert.That(col.enabled, Is.False, "collider disabled on explode");

            // Wait for BEXP sequence (~25 tics ≈ 0.71s) + destroy.
            for (int i = 0; i < 90; i++)
            {
                if (go == null) break;
                yield return null;
            }
            Assert.That(go == null, Is.True, "barrel GameObject destroyed after BEXP");
        }

        [UnityTest]
        public IEnumerator Enhanced_barrel_model_reverts_to_billboard_on_explode()
        {
            var go = new GameObject("EnhancedBarrel", typeof(MeshFilter), typeof(MeshRenderer));
            var mr = go.GetComponent<MeshRenderer>();
            var bb = go.AddComponent<SpriteBillboard>();
            var presentation = ExperimentalPickupModel.TryAttach(
                go, BarrelRules.DoomEdNum, worldScale: 1f / 32f, billboard: bb);
            Assert.That(presentation, Is.Not.Null);
            Assert.That(presentation.HasModel, Is.True);

            presentation.SetEnhancedForTest(true);
            Assert.That(presentation.ModelVisible, Is.True);
            Assert.That(mr.enabled, Is.False);

            var col = go.AddComponent<CapsuleCollider>();
            var eh = go.AddComponent<EnemyHealth>();
            eh.Init(1, -1, bb, col, countKill: false, noBlood: true);
            var be = go.AddComponent<BarrelExplosion>();
            be.Init(bb, col, cache: null, worldScale: 1f / 32f, sound: null);
            eh.SetBarrel(be);

            eh.TakeDamage(1, DamageSource.Player());
            yield return null;

            Assert.That(eh.IsDead, Is.True);
            Assert.That(presentation.ModelVisible, Is.False,
                "3D intact barrel hidden for BEXP billboard");
            Assert.That(mr.enabled, Is.True, "billboard renderer re-enabled on explode");
            Assert.That(bb.enabled, Is.True);

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Barrel_splash_damages_nearby_enemy()
        {
            yield return LoadLevel();
            var player = GameObject.Find("Player");
            var cc = player.GetComponent<CharacterController>();
            yield return SettleOnFloor(cc);

            // Far from the player so splash does not kill them.
            var origin = player.transform.position + player.transform.forward * 8f;

            var victim = new GameObject("SplashVictim");
            victim.transform.position = origin + Vector3.right * (20f / 32f);
            var vCol = victim.AddComponent<CapsuleCollider>();
            vCol.height = 1.75f; vCol.radius = 0.5f;
            vCol.center = new Vector3(0f, 0.875f, 0f);
            var veh = victim.AddComponent<EnemyHealth>();
            veh.Init(20, -1, null, vCol);

            var barrel = new GameObject("SplashBarrel");
            barrel.transform.position = origin;
            var bCol = barrel.AddComponent<CapsuleCollider>();
            bCol.radius = 10f / 32f;
            bCol.height = 42f / 32f;
            bCol.center = new Vector3(0f, 21f / 32f, 0f);
            var beh = barrel.AddComponent<EnemyHealth>();
            beh.Init(1, -1, null, bCol, countKill: false, noBlood: true);
            var be = barrel.AddComponent<BarrelExplosion>();
            be.Init(null, bCol, null, 1f / 32f, null);
            beh.SetBarrel(be);

            beh.TakeDamage(1, DamageSource.Player());
            yield return null;

            Assert.That(beh.IsDead, Is.True);
            // A_Explode runs on BEXP frame D, 15 tics after death (info.c) —
            // not on the death tic, so barrel chains ripple.
            Assert.That(veh.Health, Is.EqualTo(20),
                "no splash on the death frame itself");
            for (int i = 0; i < 90 && veh.Health == 20; i++)
                yield return null;
            Assert.That(veh.IsDead || veh.Health < 20, Is.True,
                "nearby enemy took splash damage from barrel");
        }
    }
}
