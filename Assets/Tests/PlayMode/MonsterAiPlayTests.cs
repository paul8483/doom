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
    public class MonsterAiPlayTests
    {
        [SetUp]
        public void SetUp() => LogAssert.ignoreFailingMessages = true;

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
        }

        static IEnumerator LoadLevel()
        {
            // Load with the real clock first — captureDeltaTime during async scene
            // activation can deadlock headless batchmode (SectorRetriggerPlayTests).
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return null; yield return null;
            for (int i = 0; i < 90; i++) yield return null;
            // Headless batchmode runs thousands of fps; pin 1/60s per frame so monster
            // brains (35 tics/s) and CharacterController gravity stay deterministic.
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

        // Match ThingSpawner: only "Floor" colliders count (walls skew placement).
        static bool TryFloorY(Vector3 xz, out float y)
        {
            var hits = Physics.RaycastAll(xz + Vector3.up * 50f, Vector3.down, 200f,
                                         ~0, QueryTriggerInteraction.Ignore);
            y = float.NegativeInfinity;
            bool found = false;
            foreach (var h in hits)
            {
                if (h.collider.gameObject.name != "Floor") continue;
                if (h.point.y > y) { y = h.point.y; found = true; }
            }
            return found;
        }

        static void PlaceAt(Transform t, Vector3 xz, CharacterController blockCc = null)
        {
            Vector3 pos = xz;
            if (TryFloorY(xz, out float y))
                pos = new Vector3(xz.x, y, xz.z);

            bool block = blockCc != null && blockCc.enabled;
            if (block) blockCc.enabled = false;
            t.position = pos;
            if (block) blockCc.enabled = true;
        }

        static MonsterController FindZombie(bool nonAmbushOnly = true)
        {
            var q = Object.FindObjectsByType<MonsterController>(FindObjectsSortMode.None)
                .Where(m => m.gameObject.name.StartsWith("Thing_3004"));
            if (nonAmbushOnly) q = q.Where(m => !m.IsAmbush);
            return q.FirstOrDefault();
        }

        [UnityTest]
        public IEnumerator Monster_wakes_on_noise_and_approaches()
        {
            yield return LoadLevel();
            var player = GameObject.Find("Player");
            var weapons = player.GetComponent<PlayerWeapons>();
            var cc = player.GetComponent<CharacterController>();
            yield return SettleOnFloor(cc);

            var mc = FindZombie();
            if (mc == null) Assert.Ignore("на карте нет не-ambush зомби");

            // Open spot ahead on the floor — must share a heard sector with the player.
            PlaceAt(mc.transform, player.transform.position + player.transform.forward * 4f, cc);
            float d0 = (mc.transform.position - player.transform.position).magnitude;

            weapons.FireOnceForTest();
            bool approached = false;
            for (int i = 0; i < 60 * 8; i++)
            {
                yield return null;
                if (mc.Brain.State != MonsterState.Sleep &&
                    (mc.transform.position - player.transform.position).magnitude < d0 - 0.25f)
                {
                    approached = true;
                    break;
                }
            }

            Assert.That(mc.Brain.State, Is.Not.EqualTo(MonsterState.Sleep), "проснулся");
            Assert.That(approached, Is.True, "приближается к игроку");
        }

        [UnityTest]
        public IEnumerator Monster_attack_damages_player()
        {
            yield return LoadLevel();
            var player = GameObject.Find("Player");
            var health = player.GetComponent<PlayerHealth>();
            var cc = player.GetComponent<CharacterController>();
            yield return SettleOnFloor(cc);

            var mc = FindZombie();
            if (mc == null) Assert.Ignore("на карте нет не-ambush зомби");

            Vector3 forward = player.transform.forward;
            PlaceAt(mc.transform, player.transform.position + forward * 2f, cc);

            int hp0 = health.Health;
            mc.NotifyNoise();
            for (int i = 0; i < 60 * 8 && health.Health == hp0; i++) yield return null;
            Assert.That(health.Health, Is.LessThan(hp0), "зомби ранил игрока");
        }

        [UnityTest]
        public IEnumerator Imp_fireball_reaches_player()
        {
            yield return LoadLevel();
            var player = GameObject.Find("Player");
            var health = player.GetComponent<PlayerHealth>();
            var cc = player.GetComponent<CharacterController>();
            yield return SettleOnFloor(cc);

            var imp = Object.FindObjectsByType<MonsterController>(FindObjectsSortMode.None)
                .FirstOrDefault(m => m.gameObject.name.StartsWith("Thing_3001") && !m.IsAmbush);
            if (imp == null) Assert.Ignore("на карте нет импа");

            PlaceAt(imp.transform, player.transform.position + player.transform.forward * 6f, cc);

            int hp0 = health.Health;
            // Chip the imp: wakes it, clears reactiontime, and sets justHit so
            // P_CheckMissileRange doesn't stall on the distance RNG gate.
            imp.GetComponent<EnemyHealth>().TakeDamage(1, DamageSource.Player());
            bool projectileSeen = false;
            for (int i = 0; i < 60 * 12; i++)
            {
                if (Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length > 0)
                    projectileSeen = true;
                if (health.Health < hp0) break;
                yield return null;
            }
            Assert.That(projectileSeen, Is.True, "фаербол был выпущен");
            Assert.That(health.Health, Is.LessThan(hp0), "фаербол попал");
        }

        [UnityTest]
        public IEnumerator Death_animates_then_corpse()
        {
            yield return LoadLevel();
            var mc = Object.FindObjectsByType<MonsterController>(FindObjectsSortMode.None).First();
            var eh = mc.GetComponent<EnemyHealth>();
            var col = mc.GetComponent<CapsuleCollider>();
            eh.TakeDamage(10000);
            Assert.That(mc.Brain.State, Is.EqualTo(MonsterState.Die), "анимация смерти пошла");
            Assert.That(col.enabled, Is.False, "коллайдер выключен сразу");
            for (int i = 0; i < 60 * 2; i++) yield return null;
            Assert.That(mc.Brain.State, Is.EqualTo(MonsterState.Dead), "дошёл до трупа");
        }

        [UnityTest]
        public IEnumerator Supported_monster_overkill_uses_xdeath_and_one_final_corpse()
        {
            yield return LoadLevel();
            var mc = FindZombie(nonAmbushOnly: false);
            Assert.That(mc, Is.Not.Null);
            var eh = mc.GetComponent<EnemyHealth>();
            var billboard = mc.GetComponent<SpriteBillboard>();
            int healthEntitiesBefore = Object.FindObjectsByType<EnemyHealth>(
                FindObjectsSortMode.None).Length;

            eh.TakeDamage(41); // POSS 20 -> -21, strictly below -spawnHealth
            Assert.That(mc.Brain.IsExtremeDeath, Is.True);
            Assert.That(billboard.CurrentFrame, Is.EqualTo(12), "POSS xdeath starts at M");

            for (int i = 0; i < 120 && mc.Brain.State != MonsterState.Dead; i++)
                yield return null;

            Assert.That(mc.Brain.State, Is.EqualTo(MonsterState.Dead));
            Assert.That(billboard.CurrentFrame, Is.EqualTo(20), "final xdeath corpse is U");
            Assert.That(Object.FindObjectsByType<EnemyHealth>(
                FindObjectsSortMode.None).Length, Is.EqualTo(healthEntitiesBefore),
                "xdeath remains the original entity");
        }

        [UnityTest]
        public IEnumerator Monster_damage_from_monster_retargets()
        {
            yield return LoadLevel();
            var player = GameObject.Find("Player");
            var cc = player.GetComponent<CharacterController>();
            yield return SettleOnFloor(cc);

            var zombis = Object.FindObjectsByType<MonsterController>(FindObjectsSortMode.None)
                .Where(m => m.gameObject.name.StartsWith("Thing_3004") && !m.IsAmbush)
                .Take(2).ToArray();
            if (zombis.Length < 2) Assert.Ignore("на карте меньше двух не-ambush зомби");
            var victim = zombis[0];
            var attacker = zombis[1];

            Vector3 forward = player.transform.forward;
            Vector3 open = player.transform.position + forward * 4f;
            PlaceAt(attacker.transform, open, cc);
            PlaceAt(victim.transform, open + forward * 2f, cc);
            attacker.enabled = false; // stay put so the victim's chase is measurable

            var vEh = victim.GetComponent<EnemyHealth>();
            var aEh = attacker.GetComponent<EnemyHealth>();
            vEh.TakeDamage(5, DamageSource.Monster(aEh));
            yield return null;
            Assert.That(victim.TargetForTest, Is.EqualTo(attacker.transform), "перенацелился на обидчика");
            float d0 = (victim.transform.position - attacker.transform.position).magnitude;
            bool closed = false;
            for (int i = 0; i < 60 * 10; i++)
            {
                yield return null;
                if ((victim.transform.position - attacker.transform.position).magnitude < d0 - 0.25f)
                {
                    closed = true;
                    break;
                }
            }
            Assert.That(victim.Brain.State, Is.Not.EqualTo(MonsterState.Sleep));
            Assert.That(closed, Is.True, "идёт к обидчику, а не к игроку");
        }
    }
}
