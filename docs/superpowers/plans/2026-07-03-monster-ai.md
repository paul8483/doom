# Stage 6d: Monster AI — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Монстры E1 (POSS/SPOS/TROO/SARG) просыпаются от взгляда/урона/шума выстрела, преследуют игрока DOOM-походкой, открывают двери, атакуют (hitscan/укус/фаербол), дерутся между собой и умирают с анимацией.

**Architecture:** Данные монстров (`MonsterDef`/`MonsterTable`) — в `Doom.Things`; FSM (`MonsterBrain` + `IMonsterWorld`) и боевые формулы (`MonsterRules`) — в `Doom.Game` (получает ссылку на `Doom.Things`); заливка шума (`NoiseAlert`) — в `Doom.Specials`; Unity-глю (`MonsterController`, `Projectile`, `NoiseAlertSystem`, навеска) — в `Doom.MapBuild`. Спека: `docs/superpowers/specs/2026-07-03-monster-ai-design.md`.

**Уточнение против спеки (утверждено при написании плана):** тайминг ВСЕХ кадров монстра ведёт `MonsterBrain` (по тикам, тестируемо в EditMode) и выставляет текущий кадр через `IMonsterWorld.SetFrame`. `SpriteBillboard` получает только метод `SetFrame(int)` (кадр с живыми ротациями); отдельный «проигрыватель последовательностей» в билборде не нужен. `Projectile` крутит свои 2 кадра сам (как `HitEffect`).

**Tech Stack:** Unity 6000.4.8f1, Unity Test Framework (EditMode + PlayMode), freedoom1.wad.

**Запуск тестов** (фильтр меняется по задаче):

```powershell
# EditMode
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -nographics -projectPath "D:\Development\doom" `
    -runTests -testPlatform EditMode -testFilter "<FILTER>" `
    -testResults "D:\Development\doom\Logs\t.xml" -logFile "D:\Development\doom\Logs\t.log"
# PlayMode — то же, но БЕЗ -nographics и -testPlatform PlayMode
```

Готчи: не добавлять `-quit` к `-runTests`; результаты только в XML; компил-ошибки — `error CS` в логе; код 198 «no valid license» или «project already open» → BLOCKED (интерактивный редактор закрывается `CloseMainWindow()`, см. memory `unity-headless-license`); таймаут 600000 мс; на каждый прогон — своё имя XML/лога, цитировать в отчёте правильные файлы.

**Все DOOM-числа ниже выверены по linuxdoom-1.10 (info.c, p_enemy.c, p_map.c) — копировать из плана, не из исходников.** Точка старта: EditMode 162/162, PlayMode 15/15.

---

### Task 1: `MonsterDef` + `MonsterTable` (Doom.Things)

**Files:**
- Create: `Assets/Scripts/Things/MonsterDef.cs`, `Assets/Scripts/Things/MonsterTable.cs`
- Test: `Assets/Tests/EditMode/Things/MonsterTableTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Things;
using Doom.Wad;
using Doom.Graphics;

namespace Doom.Things.Tests
{
    public class MonsterTableTests
    {
        static readonly int[] Eds = { 3004, 9, 3001, 3002 };

        [Test]
        public void All_four_monsters_have_consistent_defs()
        {
            foreach (int ed in Eds)
            {
                Assert.That(MonsterTable.TryGet(ed, out var m), Is.True, $"doomednum {ed}");
                Assert.That(m.Speed, Is.GreaterThan(0), $"{ed} speed");
                Assert.That(m.PainChance, Is.InRange(1, 255), $"{ed} painChance");
                Assert.That(m.ReactionMoves, Is.EqualTo(8), $"{ed} reaction");
                foreach (var (seq, name) in new[] {
                    (m.Stand, "stand"), (m.Run, "run"), (m.Attack, "attack"),
                    (m.Pain, "pain"), (m.Death, "death") })
                {
                    Assert.That(seq.Frames.Length, Is.EqualTo(seq.Tics.Length), $"{ed} {name}");
                    Assert.That(seq.Frames.Length, Is.GreaterThan(0), $"{ed} {name} пустая");
                    foreach (int t in seq.Tics) Assert.That(t, Is.GreaterThan(0), $"{ed} {name} tics");
                }
                Assert.That(m.FireIndex, Is.InRange(0, m.Attack.Frames.Length - 1), $"{ed} fireIndex");
                // Хоть какая-то атака есть.
                Assert.That(m.MeleeMod > 0 || m.HitscanCount > 0 || m.HasMissile, $"{ed} атаки");
            }
        }

        [Test]
        public void Doom_data_values()
        {
            MonsterTable.TryGet(3004, out var poss);  // зомби
            Assert.That(poss.Speed, Is.EqualTo(8));
            Assert.That(poss.PainChance, Is.EqualTo(200));
            Assert.That(poss.HitscanCount, Is.EqualTo(1));
            Assert.That(poss.MeleeMod, Is.EqualTo(0));
            Assert.That(poss.HasMissile, Is.False);
            Assert.That(poss.Run.Tics[0], Is.EqualTo(4));       // AABBCCDD @4
            Assert.That(poss.Run.Frames, Is.EqualTo(new[] { 0, 0, 1, 1, 2, 2, 3, 3 }));
            Assert.That(poss.Attack.Frames, Is.EqualTo(new[] { 4, 5, 4 })); // E,F,E
            Assert.That(poss.Attack.Tics, Is.EqualTo(new[] { 10, 8, 8 }));
            Assert.That(poss.FireIndex, Is.EqualTo(1));          // огонь на F
            Assert.That(poss.Death.Frames, Is.EqualTo(new[] { 7, 8, 9, 10 })); // H..K, труп L=11 в ThingTable

            MonsterTable.TryGet(9, out var spos);     // сержант
            Assert.That(spos.PainChance, Is.EqualTo(170));
            Assert.That(spos.HitscanCount, Is.EqualTo(3));
            Assert.That(spos.Run.Tics[0], Is.EqualTo(3));
            Assert.That(spos.Attack.Tics, Is.EqualTo(new[] { 10, 10, 10 }));

            MonsterTable.TryGet(3001, out var troo);  // имп
            Assert.That(troo.PainChance, Is.EqualTo(200));
            Assert.That(troo.MeleeMod, Is.EqualTo(8));
            Assert.That(troo.MeleeMult, Is.EqualTo(3));
            Assert.That(troo.HasMissile, Is.True);
            Assert.That(troo.MissileSpeed, Is.EqualTo(10));      // юниты/тик
            Assert.That(troo.MissileSprite, Is.EqualTo("BAL1"));
            Assert.That(troo.Attack.Frames, Is.EqualTo(new[] { 4, 5, 6 })); // E,F,G
            Assert.That(troo.Attack.Tics, Is.EqualTo(new[] { 8, 8, 6 }));
            Assert.That(troo.FireIndex, Is.EqualTo(2));
            Assert.That(troo.Pain.Tics, Is.EqualTo(new[] { 2, 2 }));
            Assert.That(troo.Death.Frames, Is.EqualTo(new[] { 8, 9, 10, 11 })); // I..L, труп M=12

            MonsterTable.TryGet(3002, out var sarg);  // демон
            Assert.That(sarg.Speed, Is.EqualTo(10));
            Assert.That(sarg.PainChance, Is.EqualTo(180));
            Assert.That(sarg.MeleeMod, Is.EqualTo(10));
            Assert.That(sarg.MeleeMult, Is.EqualTo(4));
            Assert.That(sarg.HitscanCount, Is.EqualTo(0));
            Assert.That(sarg.HasMissile, Is.False);
            Assert.That(sarg.Run.Tics[0], Is.EqualTo(2));
            Assert.That(sarg.Death.Frames, Is.EqualTo(new[] { 8, 9, 10, 11, 12 })); // I..M, труп N=13
        }

        [Test]
        public void All_sequence_frames_resolve_in_freedoom()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = WadFile.Open(path);
            var sprites = SpriteSet.Load(wad);
            foreach (int ed in Eds)
            {
                MonsterTable.TryGet(ed, out var m);
                ThingTable.TryGet(ed, out var thing);
                foreach (var seq in new[] { m.Stand, m.Run, m.Attack, m.Pain, m.Death })
                    foreach (int f in seq.Frames)
                        Assert.That(sprites.TryGet(thing.Sprite, f, 0, out _), Is.True,
                            $"{thing.Sprite} кадр {f} (ed {ed})");
            }
            // Фаербол импа: полёт A,B + взрыв C,D,E.
            MonsterTable.TryGet(3001, out var imp);
            foreach (int f in imp.MissileFlyFrames) Assert.That(sprites.TryGet("BAL1", f, 0, out _), Is.True, $"BAL1 fly {f}");
            foreach (int f in imp.MissileExplodeFrames) Assert.That(sprites.TryGet("BAL1", f, 0, out _), Is.True, $"BAL1 boom {f}");
        }
    }
}
```

Если фактическое API `SpriteSet`/`ThingTable` отличается — подстроить вызовы по `MonsterDataTests.cs` рядом, смысл ассертов не менять.

- [ ] **Step 2: Run to verify FAIL** (filter `Doom.Things.Tests.MonsterTableTests`) — compile error (нет `MonsterTable`) = red.

- [ ] **Step 3: Implement**

`Assets/Scripts/Things/MonsterDef.cs`:

```csharp
namespace Doom.Things
{
    /// One animation sequence: sprite frame indices (0='A') + DOOM tics per entry.
    public sealed class MonsterSeq
    {
        public readonly int[] Frames;
        public readonly int[] Tics;
        public MonsterSeq(int[] frames, int[] tics) { Frames = frames; Tics = tics; }
    }

    /// Static combat/AI data for one monster (info.c + p_enemy.c, linuxdoom-1.10).
    /// Damage formulas are ((P_Random() % Mod) + 1) * Mult.
    public sealed class MonsterDef
    {
        public int Speed;            // DOOM units per A_Chase move turn
        public int PainChance;       // 0..255, roll on every damage
        public int ReactionMoves;    // move turns of delay after waking (reactiontime 8)

        public int MeleeMod;         // 0 = no melee attack
        public int MeleeMult;
        public int HitscanCount;     // bullets per volley (0 = none)
        public bool HasMissile;

        public int MissileSpeed;         // units/tic (imp fireball 10)
        public int MissileImpactMod;     // damage ((r%8)+1)*3
        public int MissileImpactMult;
        public int MissileRadius;        // units (6)
        public int MissileSpawnHeight;   // units above feet (32)
        public string MissileSprite;     // "BAL1"
        public int[] MissileFlyFrames;   // {0,1} loop @ MissileFlyTics
        public int[] MissileFlyTics;
        public int[] MissileExplodeFrames; // {2,3,4}
        public int[] MissileExplodeTics;

        public MonsterSeq Stand;     // loop, A_Look on each entry
        public MonsterSeq Run;       // loop, one move turn per entry
        public MonsterSeq Attack;    // one-shot; FaceTarget on entries before FireIndex
        public int FireIndex;        // damage/projectile happens entering this entry
        public MonsterSeq Pain;      // one-shot
        public MonsterSeq Death;     // one-shot; then ThingDef.CorpseFrame
    }
}
```

`Assets/Scripts/Things/MonsterTable.cs`:

```csharp
using System.Collections.Generic;

namespace Doom.Things
{
    /// doomednum -> MonsterDef for the E1 roster. Numbers from linuxdoom-1.10.
    public static class MonsterTable
    {
        static readonly Dictionary<int, MonsterDef> Defs = new Dictionary<int, MonsterDef>
        {
            [3004] = new MonsterDef // POSS, zombieman
            {
                Speed = 8, PainChance = 200, ReactionMoves = 8,
                MeleeMod = 0, MeleeMult = 0, HitscanCount = 1, HasMissile = false,
                Stand = new MonsterSeq(new[] { 0, 1 }, new[] { 10, 10 }),
                Run = new MonsterSeq(new[] { 0, 0, 1, 1, 2, 2, 3, 3 },
                                     new[] { 4, 4, 4, 4, 4, 4, 4, 4 }),
                Attack = new MonsterSeq(new[] { 4, 5, 4 }, new[] { 10, 8, 8 }),
                FireIndex = 1,
                Pain = new MonsterSeq(new[] { 6, 6 }, new[] { 3, 3 }),
                Death = new MonsterSeq(new[] { 7, 8, 9, 10 }, new[] { 5, 5, 5, 5 }),
            },
            [9] = new MonsterDef // SPOS, shotgun guy
            {
                Speed = 8, PainChance = 170, ReactionMoves = 8,
                MeleeMod = 0, MeleeMult = 0, HitscanCount = 3, HasMissile = false,
                Stand = new MonsterSeq(new[] { 0, 1 }, new[] { 10, 10 }),
                Run = new MonsterSeq(new[] { 0, 0, 1, 1, 2, 2, 3, 3 },
                                     new[] { 3, 3, 3, 3, 3, 3, 3, 3 }),
                Attack = new MonsterSeq(new[] { 4, 5, 4 }, new[] { 10, 10, 10 }),
                FireIndex = 1,
                Pain = new MonsterSeq(new[] { 6, 6 }, new[] { 3, 3 }),
                Death = new MonsterSeq(new[] { 7, 8, 9, 10 }, new[] { 5, 5, 5, 5 }),
            },
            [3001] = new MonsterDef // TROO, imp
            {
                Speed = 8, PainChance = 200, ReactionMoves = 8,
                MeleeMod = 8, MeleeMult = 3, HitscanCount = 0, HasMissile = true,
                MissileSpeed = 10, MissileImpactMod = 8, MissileImpactMult = 3,
                MissileRadius = 6, MissileSpawnHeight = 32, MissileSprite = "BAL1",
                MissileFlyFrames = new[] { 0, 1 }, MissileFlyTics = new[] { 4, 4 },
                MissileExplodeFrames = new[] { 2, 3, 4 }, MissileExplodeTics = new[] { 6, 6, 6 },
                Stand = new MonsterSeq(new[] { 0, 1 }, new[] { 10, 10 }),
                Run = new MonsterSeq(new[] { 0, 0, 1, 1, 2, 2, 3, 3 },
                                     new[] { 3, 3, 3, 3, 3, 3, 3, 3 }),
                Attack = new MonsterSeq(new[] { 4, 5, 6 }, new[] { 8, 8, 6 }),
                FireIndex = 2,
                Pain = new MonsterSeq(new[] { 7, 7 }, new[] { 2, 2 }),
                Death = new MonsterSeq(new[] { 8, 9, 10, 11 }, new[] { 8, 8, 6, 6 }),
            },
            [3002] = new MonsterDef // SARG, demon
            {
                Speed = 10, PainChance = 180, ReactionMoves = 8,
                MeleeMod = 10, MeleeMult = 4, HitscanCount = 0, HasMissile = false,
                Stand = new MonsterSeq(new[] { 0, 1 }, new[] { 10, 10 }),
                Run = new MonsterSeq(new[] { 0, 0, 1, 1, 2, 2, 3, 3 },
                                     new[] { 2, 2, 2, 2, 2, 2, 2, 2 }),
                Attack = new MonsterSeq(new[] { 4, 5, 6 }, new[] { 8, 8, 8 }),
                FireIndex = 2,
                Pain = new MonsterSeq(new[] { 7, 7 }, new[] { 2, 2 }),
                Death = new MonsterSeq(new[] { 8, 9, 10, 11, 12 }, new[] { 8, 8, 4, 4, 4 }),
            },
        };

        public static bool TryGet(int doomEdNum, out MonsterDef def)
            => Defs.TryGetValue(doomEdNum, out def);
    }
}
```

- [ ] **Step 4: Run to verify PASS** (3/3, включая WAD-интеграцию).
- [ ] **Step 5: Full EditMode** без фильтра — старые не сломаны.
- [ ] **Step 6: Commit** — `git commit -m "Stage 6d: MonsterDef/MonsterTable - E1 roster data from info.c"` (+ .meta).

---

### Task 2: `MonsterRules` — боевые формулы + ссылка Doom.Game → Doom.Things

**Files:**
- Modify: `Assets/Scripts/Game/Doom.Game.asmdef` (references: `["Doom.Things"]`)
- Create: `Assets/Scripts/Game/MonsterRules.cs`
- Test: `Assets/Tests/EditMode/Game/MonsterRulesTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using NUnit.Framework;
using Doom.Game;

namespace Doom.Game.Tests
{
    public class MonsterRulesTests
    {
        [Test]
        public void Bullet_damage_is_3_to_15_step_3()
        {
            var r = new DoomRandom();
            for (int i = 0; i < 300; i++)
            {
                int d = MonsterRules.RollDamage(r, 5, 3); // зомби: ((r%5)+1)*3
                Assert.That(d, Is.InRange(3, 15));
                Assert.That(d % 3, Is.EqualTo(0));
            }
        }

        [Test]
        public void Monster_spread_unit_is_4096th_of_circle()
        {
            // (P_Random()-P_Random())<<20 в BAM: 1 ед. = 360/4096 ≈ 0.088°, max ±22.4°.
            var r = new DoomRandom();
            bool any = false;
            for (int i = 0; i < 100; i++)
            {
                float deg = MonsterRules.SpreadOffsetDeg(r);
                Assert.That(deg, Is.InRange(-22.5f, 22.5f));
                if (deg != 0f) any = true;
            }
            Assert.That(any, Is.True);
        }

        [Test]
        public void Melee_range_uses_doom_formula()
        {
            // P_CheckMeleeRange: dist < MELEERANGE - 20 + targetRadius (64-20+16=60 к игроку).
            Assert.That(MonsterRules.InMeleeRange(59.9f, 16f), Is.True);
            Assert.That(MonsterRules.InMeleeRange(60.1f, 16f), Is.False);
        }

        [Test]
        public void Missile_range_check_follows_p_checkmissilerange()
        {
            // dist=264, есть melee: p = 264-64=200 → атака только если P_Random() >= 200.
            var r = new DoomRandom(seed: 0); // первые значения 8,109,220,...
            Assert.That(MonsterRules.CheckMissileRange(r, dist: 264f, hasMelee: true), Is.False);  // 8 < 200
            r = new DoomRandom(seed: 2);     // следующее значение 220
            Assert.That(MonsterRules.CheckMissileRange(r, dist: 264f, hasMelee: true), Is.True);   // 220 >= 200
            // Без melee-атаки дистанция штрафуется ещё на 128.
            r = new DoomRandom(seed: 0);
            Assert.That(MonsterRules.CheckMissileRange(r, dist: 100f, hasMelee: false), Is.True,
                "100-64-128 < 0 → порог 0, любой бросок проходит");
            // В упор с melee порог мал.
            r = new DoomRandom(seed: 0);
            Assert.That(MonsterRules.CheckMissileRange(r, dist: 70f, hasMelee: true), Is.True,
                "70-64=6, бросок 8 >= 6");
        }
    }
}
```

- [ ] **Step 2: Run to verify FAIL** (filter `Doom.Game.Tests.MonsterRulesTests`).

- [ ] **Step 3: Implement**

В `Doom.Game.asmdef` заменить `"references": []` на `"references": ["Doom.Things"]` (сама сборка `MonsterRules` от Doom.Things пока не зависит, но `MonsterBrain` из Task 4 будет; правим asmdef здесь, чтобы дальше не трогать).

`Assets/Scripts/Game/MonsterRules.cs`:

```csharp
namespace Doom.Game
{
    /// Monster combat formulas (p_enemy.c, linuxdoom-1.10) over DoomRandom.
    public static class MonsterRules
    {
        public const float MeleeRangeDoom = 64f;   // MELEERANGE
        // Monster hitscan jitter: (P_Random()-P_Random())<<20 in BAM.
        const float SpreadUnitDeg = 360f / 4096f;

        /// Damage = ((P_Random() % mod) + 1) * mult.
        public static int RollDamage(DoomRandom r, int mod, int mult)
            => (r.Next() % mod + 1) * mult;

        public static float SpreadOffsetDeg(DoomRandom r)
            => (r.Next() - r.Next()) * SpreadUnitDeg;

        /// P_CheckMeleeRange (distances in DOOM units, to target center).
        public static bool InMeleeRange(float dist, float targetRadius)
            => dist < MeleeRangeDoom - 20f + targetRadius;

        /// P_CheckMissileRange distance gate (sight/justHit/reaction are checked
        /// by the caller). Returns true when the monster decides to attack.
        public static bool CheckMissileRange(DoomRandom r, float dist, bool hasMelee)
        {
            dist -= 64f;
            if (!hasMelee) dist -= 128f;   // no melee attack -> keep shooting further out
            if (dist > 200f) dist = 200f;
            return r.Next() >= dist;
        }
    }
}
```

- [ ] **Step 4: Run to verify PASS** (4/4). Полный EditMode — asmdef-правка ничего не сломала.
- [ ] **Step 5: Commit** — `git commit -m "Stage 6d: MonsterRules - damage rolls, spread, range checks"` (+ asmdef).

---

### Task 3: `Dir8` + `ChaseDir` — порт P_NewChaseDir

**Files:**
- Create: `Assets/Scripts/Game/Dir8.cs`, `Assets/Scripts/Game/ChaseDir.cs`
- Test: `Assets/Tests/EditMode/Game/ChaseDirTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Doom.Game;

namespace Doom.Game.Tests
{
    public class ChaseDirTests
    {
        static ChaseDir.TryStepFn Allow(params Dir8[] allowed)
        {
            var set = new HashSet<Dir8>(allowed);
            return d => set.Contains(d);
        }

        [Test]
        public void Prefers_diagonal_when_both_axes_far()
        {
            var r = new DoomRandom();
            var dir = ChaseDir.NewChaseDir(dx: 100f, dy: 100f, current: Dir8.None,
                r, d => true, out _);
            Assert.That(dir, Is.EqualTo(Dir8.NorthEast));
        }

        [Test]
        public void Falls_back_to_axis_when_diagonal_blocked()
        {
            var r = new DoomRandom();
            var dir = ChaseDir.NewChaseDir(100f, 100f, Dir8.None,
                r, Allow(Dir8.East, Dir8.North), out _);
            Assert.That(dir, Is.AnyOf(Dir8.East, Dir8.North));
        }

        [Test]
        public void Never_picks_turnaround_unless_cornered()
        {
            var r = new DoomRandom();
            // Идём на восток; всё, кроме разворота, заблокировано → берёт разворот.
            var dir = ChaseDir.NewChaseDir(100f, 0f, Dir8.East, r, Allow(Dir8.West), out _);
            Assert.That(dir, Is.EqualTo(Dir8.West));
            // А если открыт хоть один другой путь — разворот не выбирается.
            for (int seed = 0; seed < 8; seed++)
            {
                dir = ChaseDir.NewChaseDir(100f, 0f, Dir8.East,
                    new DoomRandom(seed), Allow(Dir8.West, Dir8.North), out _);
                Assert.That(dir, Is.EqualTo(Dir8.North), $"seed {seed}");
            }
        }

        [Test]
        public void Fully_blocked_returns_none()
        {
            var r = new DoomRandom();
            var dir = ChaseDir.NewChaseDir(100f, 0f, Dir8.East, r, d => false, out int mc);
            Assert.That(dir, Is.EqualTo(Dir8.None));
        }

        [Test]
        public void Movecount_is_random_and_15_masked()
        {
            for (int seed = 0; seed < 32; seed++)
            {
                ChaseDir.NewChaseDir(100f, 100f, Dir8.None, new DoomRandom(seed),
                    d => true, out int mc);
                Assert.That(mc, Is.InRange(0, 15));
            }
        }

        [Test]
        public void Small_deltas_mean_no_axis_preference()
        {
            // |delta| <= 10 юнитов по оси — ось не считается направлением (порог из P_NewChaseDir).
            var r = new DoomRandom();
            var dir = ChaseDir.NewChaseDir(5f, 100f, Dir8.None, r, d => true, out _);
            Assert.That(dir, Is.EqualTo(Dir8.North));
        }
    }
}
```

- [ ] **Step 2: Run to verify FAIL.**

- [ ] **Step 3: Implement**

`Assets/Scripts/Game/Dir8.cs`:

```csharp
namespace Doom.Game
{
    /// DOOM dirtype_t order (m_enemy): E, NE, N, NW, W, SW, S, SE. Y is north.
    public enum Dir8 { East, NorthEast, North, NorthWest, West, SouthWest, South, SouthEast, None }
}
```

`Assets/Scripts/Game/ChaseDir.cs`:

```csharp
namespace Doom.Game
{
    /// Port of P_NewChaseDir (p_enemy.c). Deltas in DOOM units, +y = north.
    public static class ChaseDir
    {
        public delegate bool TryStepFn(Dir8 dir);

        static readonly Dir8[] Opposite =
        {
            Dir8.West, Dir8.SouthWest, Dir8.South, Dir8.SouthEast,
            Dir8.East, Dir8.NorthEast, Dir8.North, Dir8.NorthWest, Dir8.None
        };
        // diags[(deltay<0)*2 + (deltax>0)] as in the original.
        static readonly Dir8[] Diags =
            { Dir8.NorthWest, Dir8.NorthEast, Dir8.SouthWest, Dir8.SouthEast };

        /// Picks a new movement direction; returns Dir8.None when cornered.
        /// movecount = P_Random()&15 on success (moves before re-deciding).
        public static Dir8 NewChaseDir(float dx, float dy, Dir8 current,
                                       DoomRandom r, TryStepFn tryStep, out int movecount)
        {
            movecount = 0;
            Dir8 turnaround = Opposite[(int)current];

            Dir8 d1 = dx > 10f ? Dir8.East : dx < -10f ? Dir8.West : Dir8.None;
            Dir8 d2 = dy < -10f ? Dir8.South : dy > 10f ? Dir8.North : Dir8.None;

            // Try a direct diagonal first.
            if (d1 != Dir8.None && d2 != Dir8.None)
            {
                var diag = Diags[((dy < 0f ? 1 : 0) << 1) + (dx > 0f ? 1 : 0)];
                if (diag != turnaround && tryStep(diag)) return Ok(diag, r, out movecount);
            }

            // Randomly (or when |dy|>|dx|) swap axis priorities.
            if (r.Next() > 200 || System.Math.Abs(dy) > System.Math.Abs(dx))
                (d1, d2) = (d2, d1);
            if (d1 == turnaround) d1 = Dir8.None;
            if (d2 == turnaround) d2 = Dir8.None;

            if (d1 != Dir8.None && tryStep(d1)) return Ok(d1, r, out movecount);
            if (d2 != Dir8.None && tryStep(d2)) return Ok(d2, r, out movecount);

            // Keep the old direction if it still works.
            if (current != Dir8.None && tryStep(current)) return Ok(current, r, out movecount);

            // Random sweep over all eight, direction of sweep randomized.
            if ((r.Next() & 1) != 0)
            {
                for (var d = Dir8.East; d <= Dir8.SouthEast; d++)
                    if (d != turnaround && tryStep(d)) return Ok(d, r, out movecount);
            }
            else
            {
                for (var d = Dir8.SouthEast; d >= Dir8.East; d--)
                    if (d != turnaround && tryStep(d)) return Ok(d, r, out movecount);
            }

            // Cornered: take the turnaround as the last resort.
            if (turnaround != Dir8.None && tryStep(turnaround)) return Ok(turnaround, r, out movecount);
            return Dir8.None;
        }

        static Dir8 Ok(Dir8 d, DoomRandom r, out int movecount)
        {
            movecount = r.Next() & 15;
            return d;
        }
    }
}
```

- [ ] **Step 4: Run to verify PASS** (6/6).
- [ ] **Step 5: Commit** — `git commit -m "Stage 6d: ChaseDir - P_NewChaseDir port"`.

---

### Task 4: `IMonsterWorld` + `MonsterBrain` — Sleep/пробуждение/ReactionTime

**Files:**
- Create: `Assets/Scripts/Game/IMonsterWorld.cs`, `Assets/Scripts/Game/MonsterBrain.cs`
- Test: `Assets/Tests/EditMode/Game/MonsterBrainWakeTests.cs` (+ `FakeMonsterWorld.cs` в той же папке тестов)

- [ ] **Step 1: Write the failing test** (+ фейк, используется и в Task 5)

`Assets/Tests/EditMode/Game/FakeMonsterWorld.cs`:

```csharp
using System.Collections.Generic;
using Doom.Game;

namespace Doom.Game.Tests
{
    /// Сценарный мир: тесты выставляют поля, мозг читает/командует.
    public sealed class FakeMonsterWorld : IMonsterWorld
    {
        public bool SeesFront, Sees360;
        public float Dist = 1000f;
        public float TargetRadius = 16f;
        public float Dx, Dy;
        public HashSet<Dir8> Blocked = new HashSet<Dir8>();
        public bool BlockedByDoor;

        public List<string> Log = new List<string>();
        public int LastFrame = -1;

        public bool CanSeeTarget(bool frontOnly) => frontOnly ? SeesFront : (SeesFront || Sees360);
        public float DistanceToTarget() => Dist;
        public float TargetRadiusUnits() => TargetRadius;
        public void TargetDelta(out float dx, out float dy) { dx = Dx; dy = Dy; }
        public void FaceTarget() => Log.Add("face");

        public StepResult TryStep(Dir8 dir)
        {
            if (Blocked.Contains(dir))
                return BlockedByDoor ? StepResult.BlockedByDoor : StepResult.Blocked;
            Log.Add($"step:{dir}");
            return StepResult.Moved;
        }
        public void UseDoor() => Log.Add("door");

        public void MeleeHit(int damage) => Log.Add($"melee:{damage}");
        public void FireHitscan(int count) => Log.Add($"hitscan:{count}");
        public void LaunchMissile() => Log.Add("missile");

        public void SetFrame(int frame) { LastFrame = frame; }
        public void OnDeathStarted() => Log.Add("death-start");
        public void OnBecameCorpse() => Log.Add("corpse");
    }
}
```

`Assets/Tests/EditMode/Game/MonsterBrainWakeTests.cs`:

```csharp
using NUnit.Framework;
using Doom.Game;
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
```

- [ ] **Step 2: Run to verify FAIL** (filter `Doom.Game.Tests.MonsterBrainWakeTests`).

- [ ] **Step 3: Implement**

`Assets/Scripts/Game/IMonsterWorld.cs`:

```csharp
namespace Doom.Game
{
    public enum StepResult { Moved, Blocked, BlockedByDoor }

    /// Everything the monster FSM asks of / commands in the world.
    /// Distances and deltas are in DOOM units; +y = north (Unity +z).
    public interface IMonsterWorld
    {
        bool CanSeeTarget(bool frontOnly);
        float DistanceToTarget();
        float TargetRadiusUnits();
        void TargetDelta(out float dx, out float dy);
        void FaceTarget();

        StepResult TryStep(Dir8 dir);
        void UseDoor();                 // blocked by a door: use it like the player's E

        void MeleeHit(int damage);
        void FireHitscan(int count);    // count bullets, spread/damage rolled by the world
        void LaunchMissile();

        void SetFrame(int frame);       // current sprite frame (rotations stay live)
        void OnDeathStarted();          // first death frame: collider off
        void OnBecameCorpse();          // death sequence over: static corpse frame
    }
}
```

`Assets/Scripts/Game/MonsterBrain.cs` (в этой задаче — Sleep/Chase-каркас; атаки/боль/смерть добьёт Task 5, но структура закладывается сразу):

```csharp
using Doom.Things;

namespace Doom.Game
{
    public enum MonsterState { Sleep, Chase, Attack, Pain, Die, Dead }

    /// Simplified DOOM monster FSM (A_Look/A_Chase rules, p_enemy.c) driven at
    /// 35 tics/s. All timing lives here; the world only executes commands.
    public sealed class MonsterBrain
    {
        readonly MonsterDef def;
        readonly DoomRandom rng;
        readonly IMonsterWorld world;
        readonly bool ambush;

        public MonsterState State { get; private set; } = MonsterState.Sleep;

        // Sequence playback.
        MonsterSeq seq;
        int seqIdx;
        int ticsLeft;
        bool seqLoop;

        // Chase bookkeeping.
        Dir8 moveDir = Dir8.None;
        int moveCount;
        int reaction;
        bool justAttacked;
        bool justHit;

        public MonsterBrain(MonsterDef def, DoomRandom rng, IMonsterWorld world, bool ambush)
        {
            this.def = def; this.rng = rng; this.world = world; this.ambush = ambush;
            StartSeq(def.Stand, loop: true);
        }

        public void Tick()
        {
            if (State == MonsterState.Dead) return;
            ticsLeft--;
            if (ticsLeft > 0) return;
            AdvanceSeq();
        }

        public void NotifyNoise()
        {
            if (State != MonsterState.Sleep || ambush) return;
            Wake();
        }

        /// Damage landed (world already applied HP). Task 5 adds the pain roll.
        public void NotifyDamaged()
        {
            justHit = true;
            if (State == MonsterState.Sleep) Wake();
        }

        public void NotifyKilled() { /* Task 5 */ }

        void Wake()
        {
            State = MonsterState.Chase;
            reaction = def.ReactionMoves;
            moveDir = Dir8.None;
            moveCount = 0;
            StartSeq(def.Run, loop: true);
        }

        void StartSeq(MonsterSeq s, bool loop)
        {
            seq = s; seqLoop = loop; seqIdx = 0;
            ticsLeft = s.Tics[0];
            world.SetFrame(s.Frames[0]);
            OnSeqEntry();
        }

        void AdvanceSeq()
        {
            seqIdx++;
            if (seqIdx >= seq.Frames.Length)
            {
                if (seqLoop) seqIdx = 0;
                else { OnSeqFinished(); return; }
            }
            ticsLeft = seq.Tics[seqIdx];
            world.SetFrame(seq.Frames[seqIdx]);
            OnSeqEntry();
        }

        void OnSeqEntry()
        {
            switch (State)
            {
                case MonsterState.Sleep: LookThink(); break;
                case MonsterState.Chase: ChaseThink(); break;
                // Attack/Pain/Die — Task 5.
            }
        }

        void OnSeqFinished() { /* one-shot последовательности — Task 5 */ }

        void LookThink()
        {
            if (world.CanSeeTarget(frontOnly: true)) Wake();
        }

        void ChaseThink()
        {
            if (reaction > 0) reaction--;

            if (justAttacked)
            {
                justAttacked = false;
                NewDir();
                return;
            }

            // Атаки — Task 5 (melee-проверка и CheckMissileRange). Пока только ходим.
            Move();
        }

        void Move()
        {
            if (moveDir != Dir8.None)
            {
                var res = world.TryStep(moveDir);
                if (res == StepResult.BlockedByDoor) { world.UseDoor(); return; }
                if (res == StepResult.Moved && --moveCount >= 0) return;
            }
            NewDir();
        }

        void NewDir()
        {
            world.TargetDelta(out float dx, out float dy);
            moveDir = ChaseDir.NewChaseDir(dx, dy, moveDir, rng,
                d => world.TryStep(d) == StepResult.Moved, out moveCount);
        }
    }
}
```

Замечание для имплементера: `ChaseDir.NewChaseDir` сам делает пробные шаги через
`tryStep` — которые в фейке/мире СОВЕРШАЮТ шаг. Это соответствует DOOM
(P_TryWalk двигает объект), т.е. «выбор направления» и «первый шаг» — одно
действие. Реализация выше это учитывает (после NewDir отдельного шага нет).

- [ ] **Step 4: Run to verify PASS** (6/6).
- [ ] **Step 5: Commit** — `git commit -m "Stage 6d: MonsterBrain skeleton - sleep, wake paths, chase walking"`.

---

### Task 5: `MonsterBrain` — атаки, боль, смерть

**Files:**
- Modify: `Assets/Scripts/Game/MonsterBrain.cs`
- Test: `Assets/Tests/EditMode/Game/MonsterBrainCombatTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
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
            // разбудил бы мозг и сжёг значения rng до броска.
            var w = AwakeWorld(100f);
            var pain = New(3004, w, seed: 0);   // первый бросок 8 → боль (и пробуждение)
            pain.NotifyDamaged();
            Assert.That(pain.State, Is.EqualTo(MonsterState.Pain));
            RunTics(pain, 3 + 3 + 1);
            Assert.That(pain.State, Is.EqualTo(MonsterState.Chase), "из боли назад в погоню");

            var w2 = AwakeWorld(100f);
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

        [Test]
        public void JustHit_makes_monster_attack_back_sooner()
        {
            // justHit → CheckMissileRange отвечает true немедленно (ответка после урона).
            var w = AwakeWorld(250f);
            var b = New(3004, w, seed: 2); // без боли (220), но justHit взводится
            b.NotifyNoise();
            RunTics(b, 35 * 2);
            int before = w.Log.FindAll(s => s.StartsWith("hitscan")).Count;
            b.NotifyDamaged();
            RunTics(b, 35 * 2);
            int after = w.Log.FindAll(s => s.StartsWith("hitscan")).Count;
            Assert.That(after, Is.GreaterThan(before), "после урона отвечает выстрелом");
        }
    }
}
```

- [ ] **Step 2: Run to verify FAIL.**

- [ ] **Step 3: Implement** — дополнить `MonsterBrain.cs`:

```csharp
// Заменить NotifyDamaged/NotifyKilled/ChaseThink/OnSeqEntry/OnSeqFinished:

public void NotifyDamaged()
{
    if (State == MonsterState.Die || State == MonsterState.Dead) return;
    justHit = true;
    // Бросок боли — ДО Wake: P_DamageMobj бросает PainChance на каждом уроне,
    // включая будящий; а Wake() тратит rng на выбор направления, что сломало бы
    // детерминизм тестов.
    bool pained = rng.Next() < def.PainChance;
    if (State == MonsterState.Sleep) Wake();
    if (pained)
    {
        State = MonsterState.Pain;
        StartSeq(def.Pain, loop: false);
    }
}

public void NotifyKilled()
{
    if (State == MonsterState.Die || State == MonsterState.Dead) return;
    State = MonsterState.Die;
    world.OnDeathStarted();
    StartSeq(def.Death, loop: false);
}

void OnSeqEntry()
{
    switch (State)
    {
        case MonsterState.Sleep: LookThink(); break;
        case MonsterState.Chase: ChaseThink(); break;
        case MonsterState.Attack: AttackEntry(); break;
        // Pain/Die: только тайминг кадров.
    }
}

void OnSeqFinished()
{
    switch (State)
    {
        case MonsterState.Attack:
        case MonsterState.Pain:
            State = MonsterState.Chase;
            StartSeq(def.Run, loop: true);
            break;
        case MonsterState.Die:
            State = MonsterState.Dead;
            world.OnBecameCorpse();
            break;
    }
}

void ChaseThink()
{
    if (reaction > 0) reaction--;

    if (justAttacked)
    {
        justAttacked = false;
        NewDir();
        return;
    }

    // Melee first (P_CheckMeleeRange включает видимость).
    if (def.MeleeMod > 0 && world.CanSeeTarget(false) &&
        MonsterRules.InMeleeRange(world.DistanceToTarget(), world.TargetRadiusUnits()))
    {
        EnterAttack();
        return;
    }

    // Ranged (P_CheckMissileRange: sight, justHit, reaction, дистанционный бросок).
    if ((def.HitscanCount > 0 || def.HasMissile) && reaction == 0 &&
        world.CanSeeTarget(false) && MissileRangeCheck())
    {
        justAttacked = true;
        EnterAttack();
        return;
    }

    Move();
}

bool MissileRangeCheck()
{
    if (justHit) { justHit = false; return true; }
    return MonsterRules.CheckMissileRange(rng, world.DistanceToTarget(), def.MeleeMod > 0);
}

void EnterAttack()
{
    State = MonsterState.Attack;
    StartSeq(def.Attack, loop: false);
}

void AttackEntry()
{
    if (seqIdx < def.FireIndex) { world.FaceTarget(); return; }
    if (seqIdx > def.FireIndex) return;
    world.FaceTarget();
    // Огонь: melee в упор приоритетнее (A_TroopAttack), иначе дальняя атака.
    if (def.MeleeMod > 0 && world.CanSeeTarget(false) &&
        MonsterRules.InMeleeRange(world.DistanceToTarget(), world.TargetRadiusUnits()))
    {
        world.MeleeHit(MonsterRules.RollDamage(rng, def.MeleeMod, def.MeleeMult));
    }
    else if (def.HitscanCount > 0) world.FireHitscan(def.HitscanCount);
    else if (def.HasMissile) world.LaunchMissile();
    // SARG без дальней атаки за пределами melee «промахивается» — ничего.
}
```

- [ ] **Step 4: Run to verify PASS** (filter `Doom.Game.Tests` — все Game-тесты; 7 новых + прежние).
- [ ] **Step 5: Commit** — `git commit -m "Stage 6d: MonsterBrain combat - attacks, pain, death, justhit"`.

---

### Task 6: `NoiseAlert` (Doom.Specials)

**Files:**
- Create: `Assets/Scripts/Specials/NoiseAlert.cs`
- Test: `Assets/Tests/EditMode/Specials/NoiseAlertTests.cs`

Факты о доступном API: `Neighbors.OfSector(MapData map, int sectorIdx)` → `IEnumerable<int>`; `ISectorHeights` в `Doom.Map` (floor/ceiling по индексу — проверить точную сигнатуру в `Assets/Scripts/Map/ISectorHeights.cs` и подстроить вызовы). Существующие тесты `NeighborsTests`/`SectorActionsTests` показывают, как строится синтетическая карта (`SyntheticMapBuilder`) — скопировать паттерн оттуда.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Linq;
using NUnit.Framework;
using Doom.Specials;
// + using-и для MapData/SyntheticMapBuilder/StaticSectorHeights — как в NeighborsTests.

namespace Doom.Specials.Tests
{
    public class NoiseAlertTests
    {
        // Синтетика: три сектора в цепочку A-B-C через двусторонние линии
        // (постройка карты — скопировать хелпер из NeighborsTests).

        [Test]
        public void Sound_floods_through_open_sectors()
        {
            var map = BuildChainMap();                    // A(0)-B(1)-C(2), проёмы открыты
            var heights = OpenHeights(map);               // потолки выше полов везде
            var heard = NoiseAlert.Compute(map, heights, sourceSector: 0);
            Assert.That(heard, Is.SupersetOf(new[] { 0, 1, 2 }));
        }

        [Test]
        public void Closed_door_blocks_sound()
        {
            var map = BuildChainMap();
            var heights = OpenHeights(map);
            CloseSector(heights, 1);                      // B: потолок опущен до пола
            var heard = NoiseAlert.Compute(map, heights, sourceSector: 0);
            Assert.That(heard, Does.Contain(0));
            Assert.That(heard, Does.Not.Contain(2), "за закрытой дверью не слышно");
        }

        [Test]
        public void Freedoom_e1m1_closed_door_blocks()
        {
            // Интеграция: открыть freedoom1.wad E1M1 (паттерн — LineSpecialCoverageTests),
            // высоты StaticSectorHeights из карты. Взять сектор двери (потолок == полу,
            // найти перебором) и убедиться, что секторы за ней не слышат сектор
            // игрока-старта, а слышимое множество непусто и содержит сектор старта.
            // Точные индексы секторов не хардкодить — искать по свойствам.
        }
    }
}
```

Тесты выше — каркас: `BuildChainMap`/`OpenHeights`/`CloseSector` и интеграционный тест написать по фактическим хелперам соседних тестов (`NeighborsTests`, `SectorActionsTests`, `RuntimeHeightRebuildTests`), сохранив смысл ассертов. Если в синтетическом билдере нельзя сделать цепочку из трёх секторов — допустимо две (A-B) + ассерты на блокировку между ними.

- [ ] **Step 2: Run to verify FAIL.**

- [ ] **Step 3: Implement**

`Assets/Scripts/Specials/NoiseAlert.cs`:

```csharp
using System.Collections.Generic;
using Doom.Map;

namespace Doom.Specials
{
    /// Port of P_NoiseAlert (p_enemy.c), simplified to sector granularity:
    /// sound floods from the source sector across two-sided adjacency and is
    /// stopped by collapsed openings (min ceiling <= max floor). ML_SOUNDBLOCK
    /// half-attenuation is not modeled (deferred).
    public static class NoiseAlert
    {
        public static HashSet<int> Compute(MapData map, ISectorHeights heights, int sourceSector)
        {
            var heard = new HashSet<int>();
            if (sourceSector < 0) return heard;
            var queue = new Queue<int>();
            heard.Add(sourceSector);
            queue.Enqueue(sourceSector);
            while (queue.Count > 0)
            {
                int s = queue.Dequeue();
                foreach (int n in Neighbors.OfSector(map, s))
                {
                    if (heard.Contains(n)) continue;
                    if (!OpeningExists(heights, s, n)) continue;
                    heard.Add(n);
                    queue.Enqueue(n);
                }
            }
            return heard;
        }

        static bool OpeningExists(ISectorHeights h, int a, int b)
        {
            float ceil = System.Math.Min(h.CeilingOf(a), h.CeilingOf(b));
            float floor = System.Math.Max(h.FloorOf(a), h.FloorOf(b));
            return ceil > floor;
        }
    }
}
```

(Имена методов `CeilingOf`/`FloorOf` — подстроить под фактический `ISectorHeights`.)

- [ ] **Step 4: Run to verify PASS** (filter `Doom.Specials.Tests.NoiseAlertTests`, 3/3).
- [ ] **Step 5: Commit** — `git commit -m "Stage 6d: NoiseAlert - P_NoiseAlert flood over sector graph"`.

---

### Task 7: `SpriteBillboard.SetFrame` + `Projectile` (Unity)

**Files:**
- Modify: `Assets/Scripts/MapBuild/SpriteBillboard.cs` (метод `SetFrame`)
- Create: `Assets/Scripts/MapBuild/Projectile.cs`

Компиляционный гейт (PlayMode-проверка — Task 11). Прочитать фактический `SpriteBillboard.cs` перед правкой.

- [ ] **Step 1: `SpriteBillboard.SetFrame`** — рядом с `SetStaticFrame`:

```csharp
/// Switch the animation frame while keeping rotation selection live
/// (walking/attack/pain frames have 8 rotations; corpse uses SetStaticFrame).
public void SetFrame(int newFrame)
{
    frame = newFrame;
    lockRotation = false;
}
```

- [ ] **Step 2: `Projectile.cs`**

```csharp
using UnityEngine;
using Doom.Game;
using Doom.Things;

namespace Doom.MapBuild
{
    /// Monster projectile (imp fireball): straight flight at DOOM speed,
    /// sphere-cast collision, explosion frames, then Destroy. Fly/explode
    /// frames come from MonsterDef; damage is rolled on impact (p_inter.c).
    public sealed class Projectile : MonoBehaviour
    {
        SpriteCache cache;
        MonsterDef def;
        float worldScale;
        Vector3 velocity;         // m/s
        DoomRandom rng;
        SpriteBillboard bb;
        float castRadius;

        int flyIdx;
        float flyLeft;
        bool exploding;
        int boomIdx;
        float boomLeft;

        public static void Launch(SpriteCache cache, MonsterDef def, float worldScale,
                                  DoomRandom rng, Vector3 from, Vector3 targetPoint)
        {
            var go = new GameObject($"Missile_{def.MissileSprite}",
                typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.position = from;
            var bb = go.AddComponent<SpriteBillboard>();
            bb.Init(cache, def.MissileSprite, def.MissileFlyFrames[0], worldScale,
                    doomAngleDeg: 0f, spawnCeiling: false, ceilingY: 0f);
            bb.SetStaticFrame(def.MissileFlyFrames[0]); // BAL1 без ротаций

            var p = go.AddComponent<Projectile>();
            p.cache = cache; p.def = def; p.worldScale = worldScale; p.rng = rng;
            p.bb = bb;
            float speed = def.MissileSpeed * 35f * worldScale;      // юниты/тик → м/с
            p.velocity = (targetPoint - from).normalized * speed;
            p.castRadius = def.MissileRadius * worldScale;
            p.flyIdx = 0;
            p.flyLeft = def.MissileFlyTics[0] / 35f;
        }

        void Update()
        {
            if (exploding) { TickExplosion(); return; }

            // Полётная анимация (2 кадра циклом).
            flyLeft -= Time.deltaTime;
            if (flyLeft <= 0f)
            {
                flyIdx = (flyIdx + 1) % def.MissileFlyFrames.Length;
                bb.SetStaticFrame(def.MissileFlyFrames[flyIdx]);
                flyLeft = def.MissileFlyTics[flyIdx] / 35f;
            }

            // Движение со сферокастом по отрезку кадра.
            Vector3 delta = velocity * Time.deltaTime;
            float dist = delta.magnitude;
            if (Physics.SphereCast(transform.position, castRadius, delta.normalized,
                                   out var hit, dist, ~0, QueryTriggerInteraction.Ignore))
            {
                transform.position += delta.normalized * hit.distance;
                OnImpact(hit.collider);
                return;
            }
            transform.position += delta;
        }

        void OnImpact(Collider hitCollider)
        {
            int damage = MonsterRules.RollDamage(rng, def.MissileImpactMod, def.MissileImpactMult);

            var player = hitCollider.GetComponent<PlayerHealth>();
            var enemy = hitCollider.GetComponent<EnemyHealth>();
            if (player != null) player.TakeDamage(damage);
            else if (enemy != null && !enemy.IsDead)
                enemy.TakeDamage(damage); // TODO(Task 9): DamageSource.Monster(owner) для infighting

            exploding = true;
            boomIdx = 0;
            bb.SetStaticFrame(def.MissileExplodeFrames[0]);
            boomLeft = def.MissileExplodeTics[0] / 35f;
            velocity = Vector3.zero;
        }

        void TickExplosion()
        {
            boomLeft -= Time.deltaTime;
            if (boomLeft > 0f) return;
            boomIdx++;
            if (boomIdx >= def.MissileExplodeFrames.Length) { Destroy(gameObject); return; }
            bb.SetStaticFrame(def.MissileExplodeFrames[boomIdx]);
            boomLeft = def.MissileExplodeTics[boomIdx] / 35f;
        }
    }
}
```

`DamageSource`/`EnemyHealth.TakeDamage(int, DamageSource)` появятся в Task 9; в
ЭТОЙ задаче, чтобы компилироваться, временно вызвать существующий
`enemy.TakeDamage(damage)` и оставить `// TODO(Task 9): передать источник для infighting` —
Task 9 заменит вызов. Атрибуция урона игроку монстру-стрелку не нужна.

Замечание: снаряд стартует ВНУТРИ капсулы стрелка — sphere cast от точки внутри
собственного коллайдера монстра его не заденет (PhysX не бьёт коллайдер,
содержащий начало каста), но каст может задеть СОСЕДНЕГО монстра — это
нормальный DOOM friendly fire.

- [ ] **Step 3: Компиляция** — полный EditMode: 0 ошибок, все зелёные.
- [ ] **Step 4: Commit** — `git commit -m "Stage 6d: SpriteBillboard.SetFrame + Projectile (imp fireball)"`.

---

### Task 8: `MonsterController` — реализация `IMonsterWorld`

**Files:**
- Create: `Assets/Scripts/MapBuild/MonsterController.cs`

Компиляционный гейт. Перед реализацией прочитать: `PlayerWeapons.cs` (паттерн hitscan-луча и эффектов), `LineActivator.cs` (как игрок «использует» стену: raycast → `LineRef` → активация — переиспользовать его механизм; если логика активации приватная, вынести в `LineActivator` публичный статический метод `TryUseLine(LineRef, ...)` минимальным рефакторингом), `HitEffect.cs`, `EnemyHealth.cs`, `SpriteBillboard.cs`.

- [ ] **Step 1: Implement**

```csharp
using UnityEngine;
using Doom.Game;
using Doom.Things;

namespace Doom.MapBuild
{
    /// Unity body for MonsterBrain: implements IMonsterWorld (movement sweeps,
    /// sight rays, attacks, door use) and pumps the brain at 35 tics/s.
    /// All brain-facing distances are in DOOM units (+y north = Unity +z).
    public sealed class MonsterController : MonoBehaviour
    {
        const float TicSeconds = 1f / 35f;
        const float StepUpUnits = 24f;    // монстр перешагивает <= 24 юнитов
        const float SightRangeM = 1000f;

        MonsterDef def;
        MonsterBrain brain;
        DoomRandom rng;
        SpriteCache cache;
        float worldScale;
        int corpseFrame;                  // финальный кадр (ThingDef.CorpseFrame)
        Transform target;                 // игрок или монстр-обидчик (infighting)
        Transform player;
        CapsuleCollider capsule;
        SpriteBillboard bb;
        EnemyHealth health;
        float tickAccum;
        float radiusM, heightM;

        public MonsterBrain Brain => brain;

        public void Init(MonsterDef def, bool ambush, int corpseFrame,
                         SpriteCache cache, float worldScale,
                         Transform player, SpriteBillboard bb, CapsuleCollider capsule,
                         EnemyHealth health, DoomRandom rng)
        {
            this.def = def; this.corpseFrame = corpseFrame;
            this.cache = cache; this.worldScale = worldScale;
            this.player = player; this.bb = bb; this.capsule = capsule;
            this.health = health; this.rng = rng;
            target = player;
            radiusM = capsule != null ? capsule.radius : 0.5f;
            heightM = capsule != null ? capsule.height : 56f * worldScale;
            brain = new MonsterBrain(def, rng, new WorldAdapter(this), ambush);
        }

        public void SetTarget(Transform t) => target = t != null ? t : player;
        public void NotifyNoise() => brain.NotifyNoise();
        public void NotifyDamaged() => brain.NotifyDamaged();
        public void NotifyKilled() => brain.NotifyKilled();

        void Update()
        {
            if (brain == null) return;
            // Цель умерла (монстр): назад на игрока.
            if (target != player && target == null) target = player;
            tickAccum += Time.deltaTime;
            while (tickAccum >= TicSeconds) { tickAccum -= TicSeconds; brain.Tick(); }
        }

        // ── IMonsterWorld через адаптер (метод на метод) ──────────────────────
        sealed class WorldAdapter : IMonsterWorld
        {
            readonly MonsterController c;
            public WorldAdapter(MonsterController c) { this.c = c; }
            public bool CanSeeTarget(bool frontOnly) => c.CanSee(frontOnly);
            public float DistanceToTarget() => c.DistUnits();
            public float TargetRadiusUnits() => c.TargetRadius();
            public void TargetDelta(out float dx, out float dy) => c.Delta(out dx, out dy);
            public void FaceTarget() => c.Face();
            public StepResult TryStep(Dir8 d) => c.Step(d);
            public void UseDoor() => c.UseDoorAhead();
            public void MeleeHit(int dmg) => c.Melee(dmg);
            public void FireHitscan(int n) => c.Hitscan(n);
            public void LaunchMissile() => c.Missile();
            public void SetFrame(int f) { if (c.bb != null) c.bb.SetFrame(f); }
            public void OnDeathStarted() { if (c.capsule != null) c.capsule.enabled = false; }
            public void OnBecameCorpse()
            { if (c.bb != null && c.corpseFrame >= 0) c.bb.SetStaticFrame(c.corpseFrame); }
        }

        Vector3 EyePos() => transform.position + Vector3.up * (heightM * 0.75f);
        Vector3 TargetCenter()
            => target.position + Vector3.up * (heightM * 0.5f);

        bool CanSee(bool frontOnly)
        {
            if (target == null) return false;
            Vector3 to = TargetCenter() - EyePos();
            if (frontOnly)
            {
                // Передняя полусфера относительно DOOM-угла спавна (хранится в billboard).
                Vector3 facing = bb != null ? bb.FacingDirection : transform.forward;
                Vector3 flat = new Vector3(to.x, 0f, to.z);
                if (Vector3.Dot(facing, flat) < 0f) return false;
            }
            if (!Physics.Raycast(EyePos(), to.normalized, out var hit, SightRangeM,
                                 ~0, QueryTriggerInteraction.Ignore))
                return false;
            // Монстры луч зрения не блокируют (DOOM P_CheckSight игнорирует things):
            // если упёрлись в чужой EnemyHealth — пробуем сквозь него.
            var t = hit.transform;
            if (t == target || t.IsChildOf(target)) return true;
            if (hit.collider.GetComponent<EnemyHealth>() != null)
            {
                // Один повторный луч из-за спины блокера (достаточно для E1-плотности).
                Vector3 resume = hit.point + to.normalized * (hit.collider.bounds.size.magnitude);
                if (Physics.Raycast(resume, to.normalized, out var hit2,
                        SightRangeM, ~0, QueryTriggerInteraction.Ignore))
                    return hit2.transform == target || hit2.transform.IsChildOf(target);
            }
            return false;
        }

        float DistUnits()
            => target == null ? float.MaxValue
               : Vector3.Distance(FlatPos(transform.position), FlatPos(target.position)) / worldScale;

        static Vector3 FlatPos(Vector3 p) => new Vector3(p.x, 0f, p.z);

        float TargetRadius()
        {
            var cc = target != null ? target.GetComponent<CharacterController>() : null;
            if (cc != null) return cc.radius / worldScale;
            var cap = target != null ? target.GetComponent<CapsuleCollider>() : null;
            return cap != null ? cap.radius / worldScale : 16f;
        }

        void Delta(out float dx, out float dy)
        {
            if (target == null) { dx = dy = 0f; return; }
            Vector3 d = target.position - transform.position;
            dx = d.x / worldScale;
            dy = d.z / worldScale;   // DOOM north = Unity +z
        }

        void Face()
        {
            if (target == null || bb == null) return;
            Vector3 d = target.position - transform.position;
            bb.SetDoomAngle(Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg);
        }

        static readonly Vector3[] DirVectors =
        {
            new Vector3(1, 0, 0), new Vector3(1, 0, 1).normalized, new Vector3(0, 0, 1),
            new Vector3(-1, 0, 1).normalized, new Vector3(-1, 0, 0),
            new Vector3(-1, 0, -1).normalized, new Vector3(0, 0, -1),
            new Vector3(1, 0, -1).normalized
        };

        StepResult Step(Dir8 dir)
        {
            float stepM = def.Speed * worldScale;
            Vector3 move = DirVectors[(int)dir] * stepM;
            float stepUpM = StepUpUnits * worldScale;

            // Свип капсулой с высоты «плечи» — перешагиваемые ступени не мешают.
            Vector3 p1 = transform.position + Vector3.up * (radiusM + stepUpM);
            Vector3 p2 = transform.position + Vector3.up * (heightM - radiusM);
            if (Physics.CapsuleCast(p1, p2, radiusM * 0.95f, move.normalized,
                                    out var hit, stepM, ~0, QueryTriggerInteraction.Ignore))
            {
                var lineRef = hit.collider.GetComponentInParent<LineRef>();
                if (lineRef != null && LineActivator.IsUsableDoor(lineRef))
                    return StepResult.BlockedByDoor;
                return StepResult.Blocked;
            }

            // Пол в точке назначения: перепад <= 24 юнитов вверх и вниз.
            Vector3 dest = transform.position + move + Vector3.up * (stepUpM + 0.05f);
            if (!Physics.Raycast(dest, Vector3.down, out var floorHit,
                                 stepUpM * 2f + 0.1f, ~0, QueryTriggerInteraction.Ignore))
                return StepResult.Blocked;   // впереди обрыв больше ступеньки

            transform.position = new Vector3(dest.x, floorHit.point.y, dest.z);
            return StepResult.Moved;
        }

        void UseDoorAhead()
        {
            // Свип уже нашёл дверь; повторить короткий луч вперёд по направлению
            // последнего шага сложно — проще активировать ближайшую дверную LineRef
            // в радиусе использования (64 юнита), как E-клавиша игрока.
            LineActivator.MonsterUseNearestDoor(transform.position, 64f * worldScale);
        }

        void Melee(int damage)
        {
            if (target == null) return;
            var ph = target.GetComponent<PlayerHealth>();
            if (ph != null) { ph.TakeDamage(damage); return; }
            var eh = target.GetComponent<EnemyHealth>();
            if (eh != null && !eh.IsDead)
                eh.TakeDamage(damage); // TODO(Task 9): DamageSource.Monster(health)
        }

        void Hitscan(int count)
        {
            if (target == null) return;
            Vector3 origin = EyePos();
            Vector3 baseDir = (TargetCenter() - origin).normalized;
            float rangeM = HitscanRules.HitscanRangeDoom * worldScale;
            for (int i = 0; i < count; i++)
            {
                int damage = MonsterRules.RollDamage(rng, 5, 3);
                float yaw = MonsterRules.SpreadOffsetDeg(rng);
                Vector3 dir = Quaternion.AngleAxis(yaw, Vector3.up) * baseDir;
                if (!Physics.Raycast(origin, dir, out var hit, rangeM,
                                     ~0, QueryTriggerInteraction.Ignore)) continue;
                var ph = hit.collider.GetComponent<PlayerHealth>();
                var eh = hit.collider.GetComponent<EnemyHealth>();
                if (ph != null)
                {
                    ph.TakeDamage(damage);
                    HitEffect.SpawnBlood(cache, worldScale, hit.point);
                }
                else if (eh != null && eh != health && !eh.IsDead)
                {
                    eh.TakeDamage(damage); // TODO(Task 9): DamageSource.Monster(health) → infighting
                    HitEffect.SpawnBlood(cache, worldScale, hit.point);
                }
                else if (eh == null)
                    HitEffect.SpawnPuff(cache, worldScale, hit.point, hit.normal);
            }
        }

        void Missile()
        {
            if (target == null) return;
            Vector3 from = transform.position + Vector3.up * (def.MissileSpawnHeight * worldScale);
            Projectile.Launch(cache, def, worldScale, rng, from, TargetCenter());
        }
    }
}
```

Адаптации, которые сделать по фактическому коду (не гадать):
- `SpriteBillboard.FacingDirection`/`SetDoomAngle` — добавить в `SpriteBillboard` (поле `doomAngleDeg` уже есть: геттер направления `new Vector3(cos, 0, sin)` и сеттер угла). Мелкая правка в этой же задаче.
- `LineActivator.IsUsableDoor(LineRef)` / `MonsterUseNearestDoor(Vector3, float)` — добавить в `LineActivator` минимальные публичные статические методы поверх его существующей таблицы Use-активации (двери = `LineSpecial` c Kind Door и триггером Use; повторно использовать существующий путь активации, не дублировать). Прочитать `LineActivator.cs` и встроиться в его API; если у него всё инстансное — сделать методы инстансными и найти инстанс через синглтон/статическую ссылку, которую он уже имеет или получит однострочно.
- `EnemyHealth.TakeDamage(int, DamageSource)` / `DamageSource.Monster(...)` появятся только в Task 9 — в ЭТОЙ задаче для компиляции звать существующий `eh.TakeDamage(damage)` с пометкой `// TODO(Task 9): DamageSource.Monster(health) для infighting` (в `Melee` и `Hitscan`); Task 9 заменит вызовы. Труп-кадр проблем не создаёт: он передаётся в `Init` параметром `corpseFrame` (Task 9 передаст `def.CorpseFrame` из ThingTable).

- [ ] **Step 2: Компиляция** — полный EditMode: 0 ошибок, все зелёные.
- [ ] **Step 3: Commit** — `git commit -m "Stage 6d: MonsterController - IMonsterWorld over Unity physics"`.

---

### Task 9: интеграция `EnemyHealth` (infighting) + навеска в `ThingSpawner`

**Files:**
- Modify: `Assets/Scripts/MapBuild/EnemyHealth.cs` (источник урона, делегирование мозгу)
- Create: `Assets/Scripts/MapBuild/DamageSource.cs`
- Modify: `Assets/Scripts/MapBuild/ThingSpawner.cs` (навеска MonsterController, AMBUSH)
- Modify: `Assets/Scripts/MapBuild/PlayerWeapons.cs` (передавать DamageSource.Player)
- Modify: `Assets/Scripts/MapBuild/Projectile.cs`, `Assets/Scripts/MapBuild/MonsterController.cs` (убрать TODO Task 9)

- [ ] **Step 1: `DamageSource.cs`**

```csharp
namespace Doom.MapBuild
{
    /// Who dealt the damage — for infighting retargeting.
    public readonly struct DamageSource
    {
        public readonly EnemyHealth MonsterAttacker; // null = игрок/среда
        DamageSource(EnemyHealth m) { MonsterAttacker = m; }
        public static DamageSource Player() => new DamageSource(null);
        public static DamageSource Monster(EnemyHealth attacker) => new DamageSource(attacker);
    }
}
```

- [ ] **Step 2: `EnemyHealth`** — добавить перегрузку и делегирование мозгу:

```csharp
// Новое поле (инициализируется в ThingSpawner):
MonsterController controller;   // null у стреляемых не-монстров

public void SetController(MonsterController c) => controller = c;

// Существующий TakeDamage(int) остаётся (среда/игрок без атрибуции) и зовёт новую перегрузку:
public void TakeDamage(int damage) => TakeDamage(damage, DamageSource.Player());

public void TakeDamage(int damage, DamageSource source)
{
    if (IsDead) return;
    hp -= damage;
    if (hp <= 0)
    {
        hp = 0;
        if (controller != null) controller.NotifyKilled();  // мозг играет смерть
        else Die();                                          // без ИИ — как раньше (труп сразу)
        return;
    }
    if (controller != null)
    {
        if (source.MonsterAttacker != null && source.MonsterAttacker != this)
            controller.SetTarget(source.MonsterAttacker.transform); // infighting
        controller.NotifyDamaged();
    }
}
```

Существующий приватный `Die()` (мгновенный труп) сохранить — он остаётся
путём смерти для стреляемых вещей БЕЗ контроллера; у монстров труп ставит
`MonsterController.OnBecameCorpse` (corpseFrame передан в `Init` ещё в Task 8).
Также заменить оба `// TODO(Task 9)` в `MonsterController` (Melee/Hitscan) и
один в `Projectile.OnImpact` на `TakeDamage(damage, DamageSource.Monster(...))`
(в `Projectile` для этого добавить поле `EnemyHealth owner`, заполняемое в
`Launch` параметром — стрелок снаряда). Проверить, что смерть игрока от
пола-урона не задета (это `PlayerHealth`, другой класс).

- [ ] **Step 3: `ThingSpawner`** — в блоке `Shootable && Health > 0` после `eh.Init(...)`:

```csharp
if (MonsterTable.TryGet(t.Type, out var mdef))
{
    bool ambush = (t.Flags & 0x0008) != 0;   // THINGS flag bit 3 = deaf/ambush
    var mc = go.AddComponent<MonsterController>();
    mc.Init(mdef, ambush, def.CorpseFrame, cache, worldScale, playerTransform,
            bb, col, eh, new DoomRandom(seedCounter++));
    eh.SetController(mc);
    // Прогрев кадров всех последовательностей (WAD ещё открыт).
    foreach (var seq in new[] { mdef.Stand, mdef.Run, mdef.Attack, mdef.Pain, mdef.Death })
        foreach (int f in seq.Frames)
            for (int rot = 0; rot < 8; rot++) cache.Get(def.Sprite, f, rot);
    if (mdef.HasMissile)
    {
        foreach (int f in mdef.MissileFlyFrames) cache.Get(mdef.MissileSprite, f, 0);
        foreach (int f in mdef.MissileExplodeFrames) cache.Get(mdef.MissileSprite, f, 0);
    }
}
```

`playerTransform` в `ThingSpawner` сейчас нет — `SpawnAll` вызывается из
`MapLoader` ПОСЛЕ `SpawnPlayer`; передать трансформ игрока параметром
(`SpawnAll(..., Transform player)`) и прокинуть из `MapLoader` (у него есть
ссылка на player GO). `seedCounter` — локальный `int`, стартующий с 0: у
каждого монстра свой детерминированный `DoomRandom(seed)` (разные фазы
случайностей, воспроизводимо в тестах). `t.Flags` — поле `Thing.Flags`
(уже парсится, `ushort`).

- [ ] **Step 4: `PlayerWeapons`** — в попадании по врагу заменить
  `enemy.TakeDamage(shot.Damage)` на `enemy.TakeDamage(shot.Damage, DamageSource.Player())`
  (семантика та же, явная атрибуция). `Projectile`/`MonsterController` — убрать
  временные TODO-вызовы, использовать перегрузку с `DamageSource.Monster(...)`.

- [ ] **Step 5: Компиляция + полный EditMode** — 0 ошибок, все зелёные (ожидаемо 191: 162 прежних + 29 новых из Task 1–6 = 3+4+6+6+7+3; сверить фактическое число с XML).
- [ ] **Step 6: Commit** — `git commit -m "Stage 6d: wire MonsterController - infighting, ambush, prewarm"`.

---

### Task 10: `NoiseAlertSystem` + провязка в `MapLoader`

**Files:**
- Create: `Assets/Scripts/MapBuild/NoiseAlertSystem.cs`
- Modify: `Assets/Scripts/MapBuild/MapLoader.cs` (создание системы после спавна)

- [ ] **Step 1: `NoiseAlertSystem.cs`**

```csharp
using System.Collections.Generic;
using UnityEngine;
using Doom.Game;
using Doom.Map;
using Doom.Specials;

namespace Doom.MapBuild
{
    /// Player gunfire wakes monsters: player's sector -> NoiseAlert flood ->
    /// NotifyNoise() on monsters standing in heard sectors. Fist is silent.
    public sealed class NoiseAlertSystem : MonoBehaviour
    {
        MapData map;
        ISectorHeights heights;
        PlayerWeapons weapons;
        Transform player;

        public void Init(MapData map, ISectorHeights heights,
                         PlayerWeapons weapons, Transform player)
        {
            this.map = map; this.heights = heights;
            this.weapons = weapons; this.player = player;
            weapons.Fired += OnFired;
        }

        void OnDestroy() { if (weapons != null) weapons.Fired -= OnFired; }

        void OnFired(WeaponDef def)
        {
            if (def.Ammo == AmmoType.None) return;   // кулак не шумит
            int source = SectorUnder(player.position);
            if (source < 0) return;
            var heard = NoiseAlert.Compute(map, heights, source);
            foreach (var mc in FindObjectsByType<MonsterController>(FindObjectsSortMode.None))
            {
                int s = SectorUnder(mc.transform.position);
                if (s >= 0 && heard.Contains(s)) mc.NotifyNoise();
            }
        }

        static int SectorUnder(Vector3 pos)
        {
            // Вниз с небольшим подъёмом — как FloorDamageSystem.
            if (Physics.Raycast(pos + Vector3.up * 0.1f, Vector3.down, out var hit,
                                100f, ~0, QueryTriggerInteraction.Ignore))
            {
                var sr = hit.collider.GetComponent<SectorRef>();
                if (sr != null) return sr.SectorIndex;
            }
            return -1;
        }
    }
}
```

(`SectorRef.SectorIndex` — проверить фактическое имя поля/свойства в
`SectorRef.cs`; паттерн raycast-вниз скопировать из `FloorDamageSystem`.
`ISectorHeights`-инстанс — это `RuntimeSectorHeights`, который `MapLoader`
уже создаёт; прокинуть его же.)

- [ ] **Step 2: `MapLoader`** — после `ThingSpawner.SpawnAll(...)` (монстры уже
  на сцене) добавить:

```csharp
// Звуковая тревога (Stage 6d): выстрелы будят комнату.
var noise = loaderGO.AddComponent<NoiseAlertSystem>();
noise.Init(map, runtimeHeights, weapons, player.transform);
```

(Имена локалов `loaderGO`/`runtimeHeights`/`weapons`/`player` подстроить по
фактическому `Build()`; `weapons` там уже есть с 6c.)

- [ ] **Step 3: Компиляция + полный EditMode** — 0 ошибок, все зелёные.
- [ ] **Step 4: Commit** — `git commit -m "Stage 6d: NoiseAlertSystem - gunfire wakes heard sectors"`.

---

### Task 11: PlayMode-тесты

**Files:**
- Create: `Assets/Tests/PlayMode/MonsterAiPlayTests.cs`

Паттерн загрузки/ожидания/SettleOnFloor — скопировать из `WeaponPlayTests.cs`
(включая `LogAssert.ignoreFailingMessages`, `captureDeltaTime = 1/60`,
`[TearDown]`). Хелпер поиска монстра: `FindObjectsByType<MonsterController>`
+ фильтр по имени GO (`Thing_3004_*` и т.п. — формат `Thing_{type}_{sprite}`).

- [ ] **Step 1: Write the tests**

```csharp
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Doom.Game;
using Doom.MapBuild;

namespace Doom.Stage3.PlayTests
{
    public class MonsterAiPlayTests
    {
        // LoadLevel()/SettleOnFloor()/[TearDown] — скопировать из WeaponPlayTests.

        [UnityTest]
        public IEnumerator Monster_wakes_on_noise_and_approaches()
        {
            yield return LoadLevel();
            var player = GameObject.Find("Player");
            var weapons = player.GetComponent<PlayerWeapons>();
            var mc = Object.FindObjectsByType<MonsterController>(FindObjectsSortMode.None)
                .OrderBy(m => (m.transform.position - player.transform.position).sqrMagnitude)
                .First();
            float d0 = (mc.transform.position - player.transform.position).magnitude;

            weapons.FireOnceForTest();   // шум
            for (int i = 0; i < 60 * 4; i++) yield return null;   // ~4 c

            Assert.That(mc.Brain.State, Is.Not.EqualTo(MonsterState.Sleep), "проснулся");
            float d1 = (mc.transform.position - player.transform.position).magnitude;
            Assert.That(d1, Is.LessThan(d0), "приближается к игроку");
        }

        [UnityTest]
        public IEnumerator Monster_attack_damages_player()
        {
            yield return LoadLevel();
            var player = GameObject.Find("Player");
            var health = player.GetComponent<PlayerHealth>();
            var mc = Object.FindObjectsByType<MonsterController>(FindObjectsSortMode.None)
                .First(m => m.gameObject.name.StartsWith("Thing_3004"));

            // Телепорт зомби вплотную-напротив игрока (виден, дистанция мала).
            var cc = player.GetComponent<CharacterController>();
            mc.transform.position = player.transform.position + cc.transform.forward * 2f;
            int hp0 = health.Current;   // имя свойства проверить по PlayerHealth
            mc.NotifyNoise();
            for (int i = 0; i < 60 * 6 && health.Current == hp0; i++) yield return null;
            Assert.That(health.Current, Is.LessThan(hp0), "зомби ранил игрока");
        }

        [UnityTest]
        public IEnumerator Imp_fireball_reaches_player()
        {
            yield return LoadLevel();
            var player = GameObject.Find("Player");
            var health = player.GetComponent<PlayerHealth>();
            var imp = Object.FindObjectsByType<MonsterController>(FindObjectsSortMode.None)
                .FirstOrDefault(m => m.gameObject.name.StartsWith("Thing_3001"));
            if (imp == null) Assert.Ignore("на карте нет импа");
            // 10 м перед игроком, прямая видимость — далеко для melee, годно для фаербола.
            imp.transform.position = player.transform.position
                + player.GetComponent<CharacterController>().transform.forward * 10f;
            int hp0 = health.Current;
            imp.NotifyNoise();
            bool projectileSeen = false;
            for (int i = 0; i < 60 * 10; i++)
            {
                if (Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length > 0)
                    projectileSeen = true;
                if (health.Current < hp0) break;
                yield return null;
            }
            Assert.That(projectileSeen, Is.True, "фаербол был выпущен");
            Assert.That(health.Current, Is.LessThan(hp0), "фаербол попал");
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
        public IEnumerator Monster_damage_from_monster_retargets()
        {
            yield return LoadLevel();
            var all = Object.FindObjectsByType<MonsterController>(FindObjectsSortMode.None);
            var victim = all[0];
            var attacker = all[1];
            var vEh = victim.GetComponent<EnemyHealth>();
            var aEh = attacker.GetComponent<EnemyHealth>();
            vEh.TakeDamage(1, DamageSource.Monster(aEh));
            // Проверка через приближение к обидчику: дистанция до атакера сокращается.
            float d0 = (victim.transform.position - attacker.transform.position).magnitude;
            for (int i = 0; i < 60 * 4; i++) yield return null;
            float d1 = (victim.transform.position - attacker.transform.position).magnitude;
            Assert.That(victim.Brain.State, Is.Not.EqualTo(MonsterState.Sleep));
            Assert.That(d1, Is.LessThan(d0), "идёт к обидчику, а не к игроку");
        }
    }
}
```

Замечания: тесты полагаются на детерминизм `captureDeltaTime`; если
конкретный монстр в конкретном месте E1M1 мешает (стена между ним и точкой
телепорта), телепортировать в открытую зону у спавна (как в WeaponPlayTests).
Тест «за закрытой дверью не будит» сознательно НЕ пишется как PlayMode —
он закрыт EditMode-интеграцией NoiseAlert на E1M1 (Task 6), а PlayMode-вариант
хрупок (зависит от геометрии конкретной двери).

- [ ] **Step 2: Run PlayMode** (filter `Doom.Stage3.PlayTests.MonsterAiPlayTests`) — 5/5. Хрупкие места чинить телепортом в открытую зону, НЕ ослаблением ассертов.
- [ ] **Step 3: Run ВСЁ** — полный EditMode + полный PlayMode: старые не сломаны (PlayMode ожидаемо 20 = 15 + 5).
- [ ] **Step 4: Commit** — `git commit -m "Stage 6d: PlayMode tests - wake, attack, fireball, death, infighting"`.

---

### Task 12: визуальная проверка + документация + финал

**Files:**
- Modify: `CLAUDE.md`, `docs/doom-unity-remake-plan.md`, `docs/superpowers/plans/2026-07-03-monster-ai.md` (чекбоксы)

- [ ] **Step 1: Глазная проверка.** Прогнать `StairCaptureTests` (харнесс видит мир, монстров и снаряды — не видит только OnGUI), посмотреть PNG: монстры в походных кадрах с ротациями. Затем попросить пользователя интерактивно: разбудить комнату выстрелом, увидеть погоню/атаки/фаербол/драку/смерти, монстра, открывшего дверь.
- [ ] **Step 2: `CLAUDE.md`** — абзац Stage 6d по образцу 6a–6c (сборки, компоненты, отложенное), статус, счётчики тестов из финальных XML.
- [ ] **Step 3: `docs/doom-unity-remake-plan.md`** — «ИИ врагов ✅ (под-этап 6d)» + абзац по образцу.
- [ ] **Step 4: Чекбоксы в этом плане.**
- [ ] **Step 5: Финальный прогон** полного EditMode + PlayMode; числа для CLAUDE.md — отсюда.
- [ ] **Step 6: Commit** — `git commit -m "Stage 6d done: monster AI - docs, plan checkboxes"`.

---

## Порядок и зависимости

Task 1–6 — чистый C# (Things/Game/Specials), строго TDD; Task 2 правит asmdef
(`Doom.Game` → `Doom.Things`). Task 7–10 — Unity-глю, компиляционные гейты;
Task 8 зависит от 7, Task 9 сшивает 7–8 с 6c-кодом, Task 10 — от 6 и 9.
Task 11 — PlayMode-верификация, Task 12 — глаза и документация. Выполнять по
порядку, каждый Task — отдельный коммит.

## Известные упрощения (зафиксированы в спеке)

- Тревога — по секторам, без ML_SOUNDBLOCK (полупоглощающих линий).
- Пробуждение криком (A_Look-звук будит соседей) — нет до 6f.
- Sleep-зрение проверяется на границе stand-кадров (каждые 10 тиков) — как A_Look.
- Зрение сквозь монстров — один повторный луч, не полный перебор.
- XDEATH, полёт (какодемоны), skill-флаги спавна, дроп обойм — отложены.
- SARG вне melee-дистанции в атаке «промахивается» (как A_SargAttack).
