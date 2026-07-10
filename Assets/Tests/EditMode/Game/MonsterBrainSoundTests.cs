using System.Linq;
using NUnit.Framework;
using Doom.Things;

namespace Doom.Game.Tests
{
    public class MonsterBrainSoundTests
    {
        static MonsterBrain New(int ed, FakeMonsterWorld w, int seed = 0)
        {
            Assert.That(MonsterTable.TryGet(ed, out var def), Is.True);
            return new MonsterBrain(def, new DoomRandom(seed), w, ambush: false);
        }

        [Test]
        public void Wake_emits_sight_once()
        {
            var w = new FakeMonsterWorld();
            var b = New(3004, w);
            b.NotifyNoise();
            Assert.That(w.Log.Count(s => s.StartsWith("sfx:Sight:")), Is.EqualTo(1));
            b.NotifyNoise(); // already awake
            Assert.That(w.Log.Count(s => s.StartsWith("sfx:Sight:")), Is.EqualTo(1));
        }

        [Test]
        public void Attack_emits_ranged_or_melee_on_fire_frame()
        {
            var w = new FakeMonsterWorld { SeesFront = true, Dist = 100f };
            var b = New(3004, w, seed: 0);
            b.NotifyNoise();
            for (int i = 0; i < 200; i++) b.Tick();
            Assert.That(w.Log, Has.Some.Contains("hitscan:1"));
            Assert.That(w.Log, Has.Some.Matches(@"sfx:RangedAttack:0"));

            var near = new FakeMonsterWorld { SeesFront = true, Dist = 40f };
            var demon = New(3002, near, seed: 0);
            demon.NotifyNoise();
            for (int i = 0; i < 80; i++) demon.Tick();
            Assert.That(near.Log, Has.Some.Contains("melee:"));
            Assert.That(near.Log, Has.Some.Matches(@"sfx:MeleeAttack:0"));
        }

        [Test]
        public void Pain_and_death_emit_once()
        {
            // seed 0: first Next after ctor stand = rndtable[1]=8 < 200 → pain
            var w = new FakeMonsterWorld();
            var b = New(3004, w, seed: 0);
            b.NotifyDamaged();
            Assert.That(b.State, Is.EqualTo(MonsterState.Pain));
            Assert.That(w.Log.Count(s => s == "sfx:Pain:0"), Is.EqualTo(1));
            Assert.That(w.Log.Count(s => s.StartsWith("sfx:Sight:")), Is.EqualTo(1));

            b.NotifyKilled();
            Assert.That(w.Log.Count(s => s.StartsWith("sfx:Death:")), Is.EqualTo(1));
            b.NotifyKilled();
            Assert.That(w.Log.Count(s => s.StartsWith("sfx:Death:")), Is.EqualTo(1));
        }

        [Test]
        public void Active_sound_is_rare_not_every_chase_entry()
        {
            var w = new FakeMonsterWorld();
            var b = New(3004, w, seed: 0);
            b.NotifyNoise();
            for (int i = 0; i < 2000; i++) b.Tick();
            int actives = w.Log.Count(s => s == "sfx:Active:0");
            int chaseEntries = w.Log.Count(s => s == "face");
            Assert.That(actives, Is.GreaterThan(0), "хотя бы один active за длинный chase");
            Assert.That(actives, Is.LessThan(chaseEntries / 4), "active << каждого chase entry");
        }
    }
}
