using NUnit.Framework;
using Doom.Things;

namespace Doom.Game.Tests
{
    public class MonsterBrainWakeTests
    {
        static MonsterBrain NewPoss(FakeMonsterWorld w, bool ambush = false)
        {
            MonsterTable.TryGet(3004, out var def);
            return new MonsterBrain(def, new DoomRandom(), w, ambush);
        }

        [Test]
        public void Sleeps_until_seen_in_front_half()
        {
            var w = new FakeMonsterWorld();
            var b = NewPoss(w);
            for (int i = 0; i < 100; i++) b.Tick();
            Assert.That(b.State, Is.EqualTo(MonsterState.Sleep));

            w.SeesFront = true;
            for (int i = 0; i < 11; i++) b.Tick(); // A_Look — на границе кадра (10 тиков)
            Assert.That(b.State, Is.EqualTo(MonsterState.Chase));
        }

        [Test]
        public void Behind_player_does_not_wake_by_sight()
        {
            var w = new FakeMonsterWorld { Sees360 = true, SeesFront = false };
            var b = NewPoss(w);
            for (int i = 0; i < 100; i++) b.Tick();
            Assert.That(b.State, Is.EqualTo(MonsterState.Sleep));
        }

        [Test]
        public void Noise_wakes_unless_ambush()
        {
            var w = new FakeMonsterWorld();
            var b = NewPoss(w);
            b.NotifyNoise();
            Assert.That(b.State, Is.EqualTo(MonsterState.Chase));

            var deaf = NewPoss(new FakeMonsterWorld(), ambush: true);
            deaf.NotifyNoise();
            Assert.That(deaf.State, Is.EqualTo(MonsterState.Sleep));
        }

        [Test]
        public void Damage_wakes_even_ambush()
        {
            var w = new FakeMonsterWorld();
            var b = NewPoss(w, ambush: true);
            b.NotifyDamaged();
            Assert.That(b.State, Is.Not.EqualTo(MonsterState.Sleep));
        }

        [Test]
        public void Reaction_delays_first_attack_but_not_walking()
        {
            var w = new FakeMonsterWorld { SeesFront = true, Dist = 100f };
            var b = NewPoss(w);
            b.NotifyNoise();
            // Реакция 8 ходов; ход POSS = 4 тика. За 24 тика проходит 6 ходов —
            // реакция ещё не вышла: шаги идут, атак нет. (На 8-м ходу реакция
            // достигает нуля и атака уже разрешена — поэтому НЕ 32 тика.)
            for (int i = 0; i < 24; i++) b.Tick();
            Assert.That(w.Log, Has.None.Contains("hitscan"));
            Assert.That(w.Log, Has.Some.Contains("step"));
        }

        [Test]
        public void Sleep_animates_stand_frames()
        {
            var w = new FakeMonsterWorld();
            var b = NewPoss(w);
            b.Tick();
            Assert.That(w.LastFrame, Is.EqualTo(0));
            for (int i = 0; i < 10; i++) b.Tick();
            Assert.That(w.LastFrame, Is.EqualTo(1), "второй кадр stand после 10 тиков");
        }
    }
}
