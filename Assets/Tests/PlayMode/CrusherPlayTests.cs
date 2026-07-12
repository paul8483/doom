using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Doom.Game;
using Doom.Map;
using Doom.MapBuild;

namespace Doom.Stage3.PlayTests
{
    public class CrusherPlayTests
    {
        static MapData OneSectorMap(short ceiling = 128) =>
            new MapData("TEST", System.Array.Empty<Vertex>(), System.Array.Empty<LineDef>(),
                System.Array.Empty<SideDef>(),
                new[] { new Sector(0, ceiling, "FLAT1", "FLAT1", 160, 0, 1) },
                System.Array.Empty<Thing>());

        [UnityTest]
        public IEnumerator Crusher_cycles_and_stop_resume_preserves_phase()
        {
            var go = new GameObject("crusher");
            var heights = new RuntimeSectorHeights(OneSectorMap());
            var mover = go.AddComponent<SectorMover>();
            mover.BeginCrusher(heights, null, 0, 8f, 700f, true, false, 1f / 32f);

            yield return new WaitForSeconds(0.2f);
            Assert.That(heights.CeilRaw(0), Is.LessThan(128f));
            mover.StopCrusher();
            float stopped = heights.CeilRaw(0);
            yield return null;
            Assert.That(heights.CeilRaw(0), Is.EqualTo(stopped).Within(0.001f));

            Assert.That(mover.TryCapture(
                out _, out _, out var phase, out _, out float target, out _, out _,
                out _, out var behavior, out bool cycle, out float origin), Is.True);
            Assert.That(phase, Is.EqualTo(MoverPhase.Stopped));
            Assert.That(behavior, Is.EqualTo(MoverBehavior.Crusher));
            Assert.That(cycle, Is.True);
            Assert.That(target, Is.EqualTo(8f));
            Assert.That(origin, Is.EqualTo(128f));

            mover.ResumeCrusher();
            yield return null;
            Assert.That(mover.IsStopped, Is.False);
            Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator Crusher_snapshot_resume_keeps_return_origin_and_cycle()
        {
            var go = new GameObject("restored crusher");
            var heights = new RuntimeSectorHeights(OneSectorMap());
            heights.SetCeil(0, 40f);
            var mover = go.AddComponent<SectorMover>();
            mover.BeginFromSnapshot(
                heights, null, 0, SectorMover.Surface.Ceiling,
                8f, 350f, MoverPhase.Returning, 0, 128f,
                MoverBehavior.Crusher, true);

            yield return null;
            Assert.That(heights.CeilRaw(0), Is.GreaterThan(40f));
            Assert.That(mover.TryCapture(
                out _, out _, out var phase, out _, out float target, out _, out _,
                out _, out var behavior, out bool cycle, out float origin), Is.True);
            Assert.That(phase, Is.EqualTo(MoverPhase.Returning));
            Assert.That(target, Is.EqualTo(8f));
            Assert.That(origin, Is.EqualTo(128f));
            Assert.That(behavior, Is.EqualTo(MoverBehavior.Crusher));
            Assert.That(cycle, Is.True);
            Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator Crusher_damages_player_at_four_tic_cadence()
        {
            const float scale = 1f / 32f;
            var map = OneSectorMap(8);
            var heights = new RuntimeSectorHeights(map);
            var root = new GameObject("Sector_0").transform;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(root);
            floor.transform.localScale = new Vector3(8f, 0.01f, 8f);
            var geometry = new SectorGeometry(
                map, new SectorPolygon[1], heights, scale, null, null, new[] { root });

            var player = new GameObject("player");
            player.transform.position = new Vector3(0f, 0.9f, 0f);
            var capsule = player.AddComponent<CapsuleCollider>();
            capsule.height = 1.75f;
            var health = player.AddComponent<PlayerHealth>();

            var monster = new GameObject("monster");
            monster.transform.position = new Vector3(1f, 0.9f, 0f);
            var monsterCapsule = monster.AddComponent<CapsuleCollider>();
            monsterCapsule.height = 1.75f;
            var enemyHealth = monster.AddComponent<EnemyHealth>();
            enemyHealth.Init(30, -1, null, monsterCapsule);

            var host = new GameObject("crusher");
            var mover = host.AddComponent<SectorMover>();
            mover.BeginCrusher(heights, geometry, 0, 0f, 0f, true, true, scale);

            yield return new WaitForSeconds(0.15f);
            Assert.That(health.Health, Is.EqualTo(90));
            Assert.That(enemyHealth.Health, Is.EqualTo(20));

            Object.Destroy(host);
            Object.Destroy(player);
            Object.Destroy(monster);
            Object.Destroy(root.gameObject);
        }
    }
}
