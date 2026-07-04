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
            // justHit → MF_JUSTHIT: P_CheckMissileRange отвечает true немедленно,
            // минуя дистанционный бросок. Близнецы с одним seed (0) на дистанции
            // 400: там бросок проходит только при r >= 200 (порог капится на 200),
            // так что различие создаёт именно justHit. Трасса (POSS, ход = 4 тика):
            //   оба:      wake в конструкторе → NewChaseDir (rnd[1]=8, rnd[2]=109 →
            //             movecount 13); окно атаки на тике 56: rnd[3]=220 >= 200 →
            //             залп 1, выстрел на тике 66; конец атаки (тик 82) → NewDir
            //             (rnd[4]=222 → swap, rnd[5]=241 → movecount 1).
            //   контроль: шаг на 86, окно на 90: rnd[6]=149 < 200 → мимо, NewDir
            //             (rnd[7]=107, rnd[8]=75 → movecount 11); следующее окно —
            //             тик 138 (rnd[9]=248) → залп 2 стреляет лишь на тике 148.
            //   раненый:  урон на тике 84 → боль (rnd[6]=149 < 200) до тика 90,
            //             шаг на 90 (movecount 1 → 0), окно на 94: justHit → залп 2
            //             уже на тике 104. Без justHit там выпало бы 107 (< 200) и
            //             выстрел уехал бы на тик 140 — за пределы окна проверки.
            var wc = AwakeWorld(400f);
            var control = New(3004, wc);
            var wd = AwakeWorld(400f);
            var damaged = New(3004, wd);
            RunTics(control, 84);
            RunTics(damaged, 84);
            Assert.That(Shots(wc), Is.EqualTo(1), "трасса: у контроля залп 1 к тику 84");
            Assert.That(Shots(wd), Is.EqualTo(1), "трасса: у раненого залп 1 к тику 84");

            damaged.NotifyDamaged();       // тик 84: боль + justHit (+ reaction=0, уже 0)
            RunTics(control, 30);
            RunTics(damaged, 30);          // до тика 114
            Assert.That(Shots(wd), Is.EqualTo(2), "после урона отвечает залпом в течение 30 тиков");
            Assert.That(Shots(wc), Is.EqualTo(1), "контроль без урона до залпа 2 ещё не дошёл");
        }

        [Test]
        public void Damage_zeroes_reaction_for_immediate_retaliation()
        {
            // P_DamageMobj: `target->reactiontime = 0; // we're awake now...` —
            // разбуженный уроном монстр НЕ выжидает 8 ходов реакции. Мир слепой
            // (паттерн Pain-теста), чтобы мозг спал до NotifyDamaged. seed 9:
            // будящий урон без боли (rnd[10]=254 >= 200), NewChaseDir при Wake
            // берёт rnd[11]=140 (без свапа) и rnd[12]=16 → movecount 0, так что
            // первый же ход погони (тик 4) — окно атаки: reaction уже обнулён,
            // justHit минует дистанционный бросок → выстрел на тике 14. Со
            // старым поведением (реакция 8 ходов) залп не случился бы раньше
            // ~тика 58 (реакция до тика 28 плюс movecount 10 от NewDir тика 4).
            var w = new FakeMonsterWorld { Dist = 100f, Dx = 100f, Dy = 0f };
            var b = New(3004, w, seed: 9);
            Assert.That(b.State, Is.EqualTo(MonsterState.Sleep), "мир слепой — до урона спит");
            b.NotifyDamaged();
            Assert.That(b.State, Is.EqualTo(MonsterState.Chase), "254 >= painchance 200 — без боли");
            w.SeesFront = true; // цель становится видимой для боевых проверок погони
            RunTics(b, 20);
            Assert.That(w.Log, Has.Some.Contains("hitscan"), "стреляет, не выжидая реакцию");
        }
    }
}
