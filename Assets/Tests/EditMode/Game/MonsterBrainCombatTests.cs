using NUnit.Framework;
using Doom.Game;
using Doom.Things;

namespace Doom.Game.Tests
{
    public class MonsterBrainCombatTests
    {
        static MonsterBrain New(int ed, FakeMonsterWorld w, int seed = 0)
        {
            MonsterTable.TryGet(ed, out var def);
            return new MonsterBrain(def, new DoomRandom(seed), w, ambush: false);
        }

        static void RunTics(MonsterBrain b, int n) { for (int i = 0; i < n; i++) b.Tick(); }

        static FakeMonsterWorld AwakeWorld(float dist)
        {
            return new FakeMonsterWorld { SeesFront = true, Dist = dist, Dx = dist, Dy = 0f };
        }

        [Test]
        public void Zombie_fires_hitscan_on_fire_frame_after_reaction()
        {
            var w = AwakeWorld(100f);
            var b = New(3004, w);
            b.NotifyNoise();
            RunTics(b, 35 * 8); // достаточно, чтобы реакция вышла и атака случилась
            Assert.That(b.State, Is.Not.EqualTo(MonsterState.Sleep));
            Assert.That(w.Log, Has.Some.Contains("hitscan:1"), "зомби стреляет");
            int fire = w.Log.FindIndex(s => s.StartsWith("hitscan"));
            int face = w.Log.FindIndex(s => s == "face");
            Assert.That(face, Is.LessThan(fire), "FaceTarget до выстрела");
        }

        [Test]
        public void Demon_bites_only_in_melee_range()
        {
            var far = AwakeWorld(300f);
            var bFar = New(3002, far);
            bFar.NotifyNoise();
            RunTics(bFar, 35 * 6);
            Assert.That(far.Log, Has.None.Contains("melee"), "демон издалека не кусает и не стреляет");
            Assert.That(far.Log, Has.Some.Contains("step"), "но проснулся и идёт — тест не вакуумный");

            var near = AwakeWorld(50f);
            var bNear = New(3002, near);
            bNear.NotifyNoise();
            RunTics(bNear, 35 * 4);
            Assert.That(near.Log, Has.Some.Contains("melee"), "в упор кусает");
            // Урон укуса в диапазоне ((r%10)+1)*4 = 4..40.
            foreach (var e in near.Log)
                if (e.StartsWith("melee:"))
                {
                    int d = int.Parse(e.Substring(6));
                    Assert.That(d, Is.InRange(4, 40));
                    Assert.That(d % 4, Is.EqualTo(0));
                }
        }

        [Test]
        public void Imp_prefers_melee_close_and_missile_far()
        {
            var near = AwakeWorld(50f);
            var bNear = New(3001, near);
            bNear.NotifyNoise();
            RunTics(bNear, 35 * 4);
            Assert.That(near.Log, Has.Some.Contains("melee"), "имп в упор царапается");
            Assert.That(near.Log, Has.None.Contains("missile"));

            var far = AwakeWorld(150f);
            var bFar = New(3001, far);
            bFar.NotifyNoise();
            RunTics(bFar, 35 * 10);
            Assert.That(far.Log, Has.Some.Contains("missile"), "имп на дистанции кидает фаербол");
        }

        [Test]
        public void Attack_returns_to_chase_and_moves_between_attacks()
        {
            var w = AwakeWorld(150f);
            var b = New(3004, w);
            b.NotifyNoise();
            RunTics(b, 35 * 12);
            int shots = w.Log.FindAll(s => s.StartsWith("hitscan")).Count;
            Assert.That(shots, Is.GreaterThan(1), "стреляет повторно");
            // MF_JUSTATTACKED: между залпами есть хотя бы один шаг.
            int first = w.Log.FindIndex(s => s.StartsWith("hitscan"));
            int second = w.Log.FindIndex(first + 1, s => s.StartsWith("hitscan"));
            Assert.That(w.Log.GetRange(first, second - first),
                Has.Some.Contains("step"), "между атаками монстр ходит");
        }

        [Test]
        public void Pain_roll_uses_painchance_and_interrupts()
        {
            // POSS painchance 200/256. Бросок боли — ПЕРВОЕ обращение к rng в
            // NotifyDamaged (до Wake, который тоже тратит rng на выбор направления),
            // поэтому seed задаёт исход детерминированно: rndtable[1]=8 (<200 → боль),
            // rndtable[3]=220 (>=200 → без боли). NotifyNoise НЕ вызывать — он
            // разбудил бы мозг и сжёг значения rng до броска. По той же причине мир
            // тут НЕ видит цель (не AwakeWorld): A_Look на первом кадре stand будит
            // мозг ещё в конструкторе и жжёт rng до NotifyDamaged.
            var w = new FakeMonsterWorld { Dist = 100f, Dx = 100f, Dy = 0f };
            var pain = New(3004, w, seed: 0);   // первый бросок 8 → боль (и пробуждение)
            pain.NotifyDamaged();
            Assert.That(pain.State, Is.EqualTo(MonsterState.Pain));
            RunTics(pain, 3 + 3 + 1);
            Assert.That(pain.State, Is.EqualTo(MonsterState.Chase), "из боли назад в погоню");

            var w2 = new FakeMonsterWorld { Dist = 100f, Dx = 100f, Dy = 0f };
            var noPain = New(3004, w2, seed: 2); // первый бросок 220 → без боли
            noPain.NotifyDamaged();
            Assert.That(noPain.State, Is.EqualTo(MonsterState.Chase), "проснулся без боли");
        }

        [Test]
        public void Death_plays_sequence_then_corpse()
        {
            var w = AwakeWorld(100f);
            var b = New(3004, w);
            b.NotifyNoise();
            b.NotifyKilled();
            Assert.That(b.State, Is.EqualTo(MonsterState.Die));
            Assert.That(w.Log, Has.Some.EqualTo("death-start"), "коллайдер гасится на первом кадре");
            RunTics(b, 5 + 5 + 5 + 5 + 1);
            Assert.That(b.State, Is.EqualTo(MonsterState.Dead));
            Assert.That(w.Log, Has.Some.EqualTo("corpse"));
            // Мёртвый мозг инертен.
            b.NotifyDamaged(); b.NotifyNoise(); RunTics(b, 10);
            Assert.That(b.State, Is.EqualTo(MonsterState.Dead));
        }

        static int Shots(FakeMonsterWorld w) => w.Log.FindAll(s => s.StartsWith("hitscan")).Count;

        [Test]
        public void JustHit_makes_monster_attack_back_sooner()
        {
            // justHit заставляет следующий missile-check пройти без дистанционного
            // броска. Ищем seed, где после первого залпа урон даёт второй быстрее,
            // чем у неидентичного контроля без урона (тот же seed, параллельный тик).
            bool found = false;
            for (int seed = 0; seed < 32 && !found; seed++)
            {
                var wd = AwakeWorld(400f);
                var damaged = New(3004, wd, seed);
                var wc = AwakeWorld(400f);
                var control = New(3004, wc, seed);

                for (int i = 0; i < 300 && Shots(wd) < 1; i++)
                {
                    damaged.Tick();
                    control.Tick();
                }
                if (Shots(wd) < 1 || Shots(wc) < 1) continue;

                for (int i = 0; i < 50 && damaged.State == MonsterState.Attack; i++)
                {
                    damaged.Tick();
                    control.Tick();
                }

                int before = Shots(wd);
                damaged.NotifyDamaged();

                int damagedAt = -1, controlAt = -1;
                for (int i = 0; i < 150; i++)
                {
                    damaged.Tick();
                    control.Tick();
                    if (damagedAt < 0 && Shots(wd) > before) damagedAt = i;
                    if (controlAt < 0 && Shots(wc) > 1) controlAt = i;
                }
                if (damagedAt >= 0 && (controlAt < 0 || damagedAt < controlAt))
                    found = true;
            }
            Assert.That(found, Is.True, "justHit ускоряет повторный залп хотя бы на одном seed");
        }

        [Test]
        public void Damage_zeroes_reaction_for_immediate_retaliation()
        {
            // P_DamageMobj обнуляет reactiontime: разбуженный уроном монстр не
            // ждёт 8 ходов. Ищем seed без боли, где при появлении цели залп
            // случается раньше, чем истекла бы полная реакция (8 ходов × 4 тика
            // + замах атаки ≈ 50+ тиков).
            bool found = false;
            for (int seed = 0; seed < 64 && !found; seed++)
            {
                var w = new FakeMonsterWorld { Dist = 100f, Dx = 100f, Dy = 0f };
                var b = New(3004, w, seed);
                if (b.State != MonsterState.Sleep) continue;
                b.NotifyDamaged();
                if (b.State != MonsterState.Chase) continue;
                w.SeesFront = true;
                RunTics(b, 24);
                if (w.Log.Exists(s => s.StartsWith("hitscan")))
                    found = true;
            }
            Assert.That(found, Is.True, "хотя бы один seed даёт быстрый ответ без реакции");
        }
    }
}
