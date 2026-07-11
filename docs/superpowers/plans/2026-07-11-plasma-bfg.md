# Stage 6c extension: Plasma Rifle and BFG9000 — Implementation Plan

> Выполнять после закрытия Stage 7e. План использует checkbox (`- [ ]`) для
> фактического прогресса. Строки commit checkpoint не являются разрешением
> создавать commits без отдельного запроса пользователя.

**Goal:** Добавить оставшийся арсенал Ultimate DOOM: cells, Plasma Rifle и
vanilla BFG9000 с WAD viewmodel/effects/sounds, pickups, HUD, carry-over,
save schema v4 и exactly-once восстановлением BFG spray.

**Architecture:** Ammo/weapon/projectile/tracer rules остаются pure-C# в
`Doom.Game`; Unity physics, projectile lifetime и visual effects — в
`Doom.MapBuild`. BFG не использует `RadiusDamageExecutor`: direct projectile
hit и 40 трассеров — разные damage events. Статическая карта и контент
по-прежнему приходят только из WAD.

**Design:** `docs/superpowers/specs/2026-07-11-plasma-bfg-design.md`

**Prerequisite:** Stage 7e Task 15 закрыта, полные baseline test totals
записаны. Если это условие не выполнено, не начинать код — сначала завершить
финальную приёмку 7e.

**Tech Stack:** Unity 6000.4.8f1, C#, Unity Test Framework, Input System,
`freedoom1.wad`, существующие assemblies `Doom.Game`, `Doom.Graphics`,
`Doom.MapBuild`.

## Test commands

```powershell
# EditMode
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -nographics -projectPath "C:\Development\doom" `
    -runTests -testPlatform EditMode -testFilter "<FILTER>" `
    -testResults "C:\Development\doom\Logs\plasma-bfg-edit.xml" `
    -logFile "C:\Development\doom\Logs\plasma-bfg-edit.log"

# PlayMode: PhysX требует запуск без -nographics
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -projectPath "C:\Development\doom" `
    -runTests -testPlatform PlayMode -testFilter "<FILTER>" `
    -testResults "C:\Development\doom\Logs\plasma-bfg-play.xml" `
    -logFile "C:\Development\doom\Logs\plasma-bfg-play.log"
```

Не добавлять `-quit` к `-runTests`. Результат брать из свежего XML, а не из
Editor log. Открытый Unity Editor блокирует batchmode: закрыть его либо
зафиксировать run как `BLOCKED`, но не использовать старые результаты.

---

## Task 1: Source/WAD audit и executable constants

**Files:**
- Create: `Assets/Tests/EditMode/Graphics/PlasmaBfgWadTests.cs`
- Create later from this audit:
  `Assets/Scripts/Game/PlasmaRules.cs`,
  `Assets/Scripts/Game/BfgRules.cs`

- [x] **Step 1: Зафиксировать source table.**

Сверить с `linuxdoom-1.10/p_inter.c`, `p_pspr.c`, `info.c`:

| Contract | Value |
|---|---|
| max cells / backpack | 300 / 600 |
| cell / cell pack / backpack grant | 20 / 100 / 20 |
| weapon pickup grant | 40 cells |
| plasma cost / speed / size | 1 / 25 per tic / radius 13, height 8 |
| plasma damage | `5 * (P_Random()%8+1)` |
| plasma fly / impact | `PLSS` A/B 6/6; `PLSE` A–E 4 each |
| BFG cost / speed / size | 40 / 25 per tic / radius 13, height 8 |
| BFG direct damage | `100 * (P_Random()%8+1)` |
| BFG tracer fan | 40 rays, 1024 units, −45° + `i*(90°/40)` |
| tracer damage | sum of 15 rolls `(P_Random()&7)+1` |
| BFG fly / impact | `BFS1` A/B 4/4; `BFE1` A–F 8 each |
| spray event | entry to `BFE1` frame C, after 16 impact tics |
| target effect | `BFE2` A–D 8 each |

- [x] **Step 2: Написать WAD integration tests.**

`PlasmaBfgWadTests` открывает `freedoom1.wad`, строит `SpriteSet` и проверяет
все необходимые кадры:

- `PLSG` A/B, `PLSF` A/B, `PLSS` A/B, `PLSE` A–E;
- `BFGG` A/B, `BFGF` A/B, `BFS1` A/B, `BFE1` A–F, `BFE2` A–D;
- pickup sprites `PLAS`, `BFUG`, `CELL`, `CELP`;
- SFX `DSPLASMA`, `DSBFG`, `DSFIRXPL`, `DSRXPLOD`, `DSWPNUP`, `DSITEMUP`.

Если Freedoom совместимый lump называется иначе, сначала подтвердить это
directory audit'ом. Допустимый fallback должен быть WAD-driven и явно
записан в design/plan; нельзя добавлять authored replacement.

- [x] **Step 3: Запустить audit test.**

Filter: `Doom.Graphics.Tests.PlasmaBfgWadTests`. До gameplay-кода тест обязан
проходить как проверка входных данных. Missing required lump — stop condition,
а не повод молча отключить оружие.

- [ ] **Step 4: Commit checkpoint (только по разрешению):**
`Stage 6c extension: pin plasma and BFG WAD contracts`

---

## Task 2: Cells и расширение weapon models

**Files:**
- Modify: `Assets/Scripts/Game/AmmoType.cs`
- Modify: `Assets/Scripts/Game/AmmoModel.cs`
- Modify: `Assets/Scripts/Game/WeaponId.cs`
- Modify: `Assets/Scripts/Game/WeaponDef.cs`
- Modify: `Assets/Scripts/Game/WeaponTable.cs`
- Modify: `Assets/Scripts/Game/WeaponLoadout.cs`
- Create: `Assets/Scripts/Game/PlasmaRules.cs`
- Create: `Assets/Scripts/Game/BfgRules.cs`
- Modify: `Assets/Tests/EditMode/Game/AmmoModelTests.cs`
- Modify: `Assets/Tests/EditMode/Game/AmmoModelBackpackTests.cs`
- Modify: `Assets/Tests/EditMode/Game/WeaponTableTests.cs`
- Modify: `Assets/Tests/EditMode/Game/WeaponLoadoutTests.cs`
- Create: `Assets/Tests/EditMode/Game/PlasmaRulesTests.cs`
- Create: `Assets/Tests/EditMode/Game/BfgRulesTests.cs`

- [ ] **Step 1: Написать failing ammo tests.**

Проверить `Cells` 0→300, backpack max 600, каждый backpack +20, расход 1/40,
negative/overflow restore clamp и capture/restore вместе с rockets.

- [ ] **Step 2: Написать failing weapon/loadout tests.**

- Plasma Rifle: slot 6, Cells, cost 1, `PLSG/PLSF`, `DSPLASMA`.
- BFG9000: slot 7, Cells, cost 40, `BFGG/BFGF`, `DSBFG`.
- `BestAvailable`: BFG доступен только при ≥40 cells; plasma при ≥1.
- Порядок: BFG → plasma → rocket → chaingun → shotgun → pistol →
  chainsaw/fist.
- `Restore`/`Capture` сохраняют ownership/current/pending новых enum values.
- Unknown `WeaponId` не должен fallback'иться на Fist в authoritative paths;
  invalid enum отвергается на границе restore/codec.

- [ ] **Step 3: Написать failing projectile rule tests.**

`PlasmaRulesTests` проверяет damage set 5..40, скорость/размер, кадры/тики и
snapshot type. `BfgRulesTests` проверяет direct damage 100..800, 40 точных
offsets (первый −45°, последний +42.75°), range 1024, spray frame/tic и
15 последовательных RNG rolls на один tracer.

- [ ] **Step 4: Реализовать enums и `AmmoModel`.**

Добавить cells во все `Get/GetMax/Add/TryConsume/Reset/Capture/Restore`,
backpack grant и comments. Старые overloads оставить для source compatibility
и делегировать новому полному overload с `cells = 0`.

- [ ] **Step 5: Реализовать weapon defs/rules.**

`WeaponDef` получает явные поля, необходимые scheduler:

- `ActionTic` — момент ammo commit/projectile spawn относительно начала;
- `RefireTics` — интервал удерживаемого огня (plasma = 3);
- при необходимости `RecoveryTics`/полная визуальная последовательность,
  чтобы release plasma доигрывал 20-tic B frame.

Для старых weapons defaults должны сохранить текущее поведение. Не кодировать
BFG special numbers только в `PlayerWeapons`: канонические числа принадлежат
`BfgRules`/`WeaponDef`.

- [ ] **Step 6: RED→GREEN и regression.**

Filters:
`Doom.Game.Tests.AmmoModelTests,Doom.Game.Tests.AmmoModelBackpackTests,` +
`Doom.Game.Tests.WeaponTableTests,Doom.Game.Tests.WeaponLoadoutTests,` +
`Doom.Game.Tests.PlasmaRulesTests,Doom.Game.Tests.BfgRulesTests`.
Затем вся `Doom.Game.Tests`.

- [ ] **Step 7: Commit checkpoint (только по разрешению):**
`Stage 6c extension: model cells plasma and BFG`

---

## Task 3: Weapon action scheduler, input и viewmodel timing

**Files:**
- Modify: `Assets/Scripts/MapBuild/PlayerWeapons.cs`
- Modify: `Assets/Scripts/MapBuild/WeaponView.cs`
- Modify: `Assets/Scripts/MapBuild/PlayerSoundController.cs`
- Modify: `Assets/Scripts/MapBuild/NoiseAlertSystem.cs` only if event split requires it
- Create: `Assets/Tests/EditMode/Game/WeaponActionTests.cs` if scheduler is pure
- Modify/Create: `Assets/Tests/PlayMode/WeaponPlayTests.cs`

- [ ] **Step 1: Выделить testable attack timeline.**

Предпочтительно добавить pure `WeaponAction`/`WeaponActionState` в
`Doom.Game`, а не тестировать тики только через frames:

- start проверяет ammo;
- immediate weapons commit на tic 0;
- BFG публикует charge/start event, а ammo и projectile commit на tic 30;
- committed action выполняется ровно один раз;
- plasma допускает следующий held shot через 3 тика, но release view доигрывает
  B frame 20 тиков;
- death/reset отменяет ещё не committed BFG charge без расхода cells.

Если выбран Unity-side scheduler, тот же контракт покрыть PlayMode test hook
с явным `AdvanceTicsForTest`, не `WaitForSeconds`.

- [ ] **Step 2: Разделить события по смыслу.**

Не перегружать существующий `Fired` двумя моментами. Зафиксировать:

- attack-start/view event — запускает psprite sequence и `DSBFG` charge sound;
- action-commit event — расходует ammo, создаёт projectile и публикует
  gunfire noise;
- plasma оба события имеет на tic 0;
- existing weapons сохраняют прежний observable timing.

Подписчики `WeaponView`, `PlayerSoundController`, `NoiseAlertSystem` должны
получать подходящее событие без двойного sound/noise.

- [ ] **Step 3: Добавить slots 6/7.**

Input loop становится 1..7. Slot 1 по-прежнему выбирает chainsaw/fist; 6 и 7
выбирают только owned weapon. Переключение во время active action использует
существующий pending contract либо блокируется последовательно с остальным
арсеналом — поведение фиксируется тестом.

- [ ] **Step 4: Обновить `WeaponView`.**

View остаётся data-driven и воспроизводит точные BFG charge frames/flash.
Plasma flash выбирает A/B через тот же deterministic `DoomRandom` action
result, а не через Unity RNG. Не читать WAD из `OnGUI`.

- [ ] **Step 5: Tests и existing weapon regression.**

Проверить BFG commit ровно на tic 30, plasma held cadence, release recovery,
39-cell rejection, 40-cell one-time consume, death/reset cancellation,
slots 6/7 и отсутствие изменений pistol/rocket/chainsaw timings.

- [ ] **Step 6: Commit checkpoint (только по разрешению):**
`Stage 6c extension: schedule plasma and BFG fire actions`

---

## Task 4: Plasma projectile runtime

**Files:**
- Create: `Assets/Scripts/MapBuild/PlayerPlasmaProjectile.cs`
- Modify: `Assets/Scripts/MapBuild/PlayerWeapons.cs`
- Modify: `Assets/Scripts/MapBuild/WorldSnapshotRestore.cs`
- Create/Modify: `Assets/Tests/PlayMode/PlasmaBfgPlayTests.cs`

- [ ] **Step 1: Написать failing PlayMode tests.**

- один выстрел расходует 1 cell;
- swept cast не туннелирует через стену/тонкую цель;
- direct damage соответствует injected deterministic RNG;
- owner collider игнорируется;
- impact проигрывает `PLSE` A–E/`DSFIRXPL`;
- рядом стоящая цель не получает splash.

- [ ] **Step 2: Реализовать `PlayerPlasmaProjectile`.**

Использовать паттерн `PlayerRocketProjectile`: velocity в
`SpeedDoomPerTic * 35 * worldScale`, owner exclusion, `SphereCastAll`,
stable runtime identity и `IProjectileSnapshotSource`. Не вызывать
`RadiusDamageExecutor`.

Damage source — `DamageSource.Player()`. `EnemyHealth`/`PlayerHealth` lookup
делать через parent так же, как rocket path. Impact идемпотентен.

- [ ] **Step 3: Подключить fire commit.**

`PlayerWeapons` на plasma action создаёт шар из camera position/forward и не
попадает в hitscan path (`Pellets = 0` не должен случайно означать «ничего»).

- [ ] **Step 4: Узкий PASS + rocket regression.**

Запустить plasma tests и существующие rocket/barrel/weapon PlayMode tests.

- [ ] **Step 5: Commit checkpoint (только по разрешению):**
`Stage 6c extension: add plasma projectile`

---

## Task 5: Vanilla BFG projectile и `A_BFGSpray`

**Files:**
- Create: `Assets/Scripts/MapBuild/PlayerBfgProjectile.cs`
- Create: `Assets/Scripts/MapBuild/BfgSprayExecutor.cs`
- Create: `Assets/Scripts/MapBuild/BfgTracerEffect.cs`
- Modify: `Assets/Scripts/MapBuild/PlayerWeapons.cs`
- Modify: `Assets/Tests/PlayMode/PlasmaBfgPlayTests.cs`

- [ ] **Step 1: Failing direct-hit tests.**

Проверить 100..800 direct damage, `BFS1` flight, `BFE1` impact, owner
exclusion, отсутствие `RadiusDamageExecutor`/self-damage и `DSRXPLOD`.

- [ ] **Step 2: Failing spray tests.**

Сделать test seam для raycast (`IBfgTraceWorld` pure/runtime adapter либо
injected delegate), чтобы доказать:

- ровно 40 ray attempts;
- origin берётся из текущей player/camera position на spray tic;
- direction использует сохранённый shot yaw/pitch, не текущий поворот;
- wall/first-live-target semantics;
- каждый hit получает собственные 15 RNG rolls;
- `BFE2` создаётся только на hit target;
- `sprayApplied` делает повторный tick no-op.

- [ ] **Step 3: Реализовать BFG flight/impact state.**

`PlayerBfgProjectile` хранит normalized shot direction, owner identity,
`Flying/Exploding`, frame index/timer и `sprayApplied`. При impact:

1. один direct damage;
2. `BFE1` frame A;
3. после 16 тиков, при входе в frame C — один `BfgSprayExecutor.Execute`;
4. доиграть A–F и уничтожить GO.

- [ ] **Step 4: Реализовать executor/effects.**

40 `Physics.Raycast` на 1024×worldScale с
`QueryTriggerInteraction.Ignore`. Исключить owner. Dead/non-shootable objects
не должны скрыто поглощать последующие rays. На hit вызвать
`EnemyHealth.TakeDamage(..., DamageSource.Player())`; поддержать player target,
если gameplay path это допускает. Visual `BFE2` — отдельный short-lived
billboard без authoritative state.

- [ ] **Step 5: Подключить BFG commit и tests.**

На action tic 30 создать BFG ball и списать 40 cells один раз. Проверить full
charge cadence, release, repeated hold и невозможность второго commit.

- [ ] **Step 6: Commit checkpoint (только по разрешению):**
`Stage 6c extension: implement vanilla BFG spray`

---

## Task 6: Pickups, HUD, audio, WAD prewarm и compatibility

**Files:**
- Modify: `Assets/Scripts/Game/ItemRules.cs`
- Modify: `Assets/Scripts/Game/PickupSoundTable.cs`
- Modify: `Assets/Scripts/Game/HudModel.cs`
- Modify: `Assets/Scripts/MapBuild/DoomHud.cs`
- Modify: `Assets/Scripts/MapBuild/MapLoader.cs`
- Modify: `Assets/Tests/EditMode/Game/ItemRulesTests.cs`
- Modify: `Assets/Tests/EditMode/Game/HudModelTests.cs`
- Modify: `Assets/Tests/EditMode/Map/E1CompatibilityTests.cs`
- Modify: `Assets/Tests/PlayMode/PickupPlayTests.cs`
- Modify: `Assets/Tests/PlayMode/SoundPlayTests.cs`

- [ ] **Step 1: Failing pickup tests.**

Проверить:

- 2004/2006: give weapon +40 cells;
- 2047: +20; 17: +100;
- new weapon accepted at full ammo;
- duplicate weapon/full ammo rejected and GO remains;
- backpack after cells support grants +20 и max 600;
- pickup sounds: weapons `DSWPNUP`, ammo `DSITEMUP`.

- [ ] **Step 2: Реализовать `ItemRules` и sound routing.**

Добавить четыре types в `TryPickup` и `IsPickup`. `ThingSpawner` после этого
подключит `ThingPickup` без отдельного hardcoded switch.

- [ ] **Step 3: Failing HUD tests и реализация.**

Заменить тест `Rockets_are_projected_while_cells_remain_unsupported` новым
контрактом. `HudModel.From` проецирует cells/max cells и ownership plasma/BFG.
`DoomHud.DrawArms` больше не подставляет `false` для slots 6/7.

- [ ] **Step 4: Prewarm.**

В `MapLoader` добавить все frames Task 1 и SFX. `CollectSfxNames` продолжает
собирать fire sound из `WeaponTable`; impact sounds добавить table-driven
через rules либо в explicit fixed set. После закрытия WAD ни один новый
projectile/view/effect не делает lazy read.

- [ ] **Step 5: Compatibility matrix.**

Убрать 2004/2006/2047/17 из `IsDeferredPickup`; классификация должна идти через
`ItemRules.IsPickup` и стать `Implemented`. Super shotgun 82 остаётся
`Unsupported`.

- [ ] **Step 6: EditMode/PlayMode PASS.**

Запустить ItemRules/HudModel/E1Compatibility, PickupPlayTests и SoundPlayTests.

- [ ] **Step 7: Commit checkpoint (только по разрешению):**
`Stage 6c extension: wire energy pickups HUD and sound`

---

## Task 7: Carry-over, player snapshot и save schema v4

**Files:**
- Modify: `Assets/Scripts/Game/PlayerCarryState.cs`
- Modify: `Assets/Scripts/Game/PlayerSnapshot.cs`
- Modify: `Assets/Scripts/Game/SaveGame.cs`
- Modify: `Assets/Scripts/Game/SaveGameCodec.cs`
- Modify: `Assets/Scripts/MapBuild/PlayerWeapons.cs`
- Modify: `Assets/Tests/EditMode/Game/SessionStateTests.cs`
- Modify: `Assets/Tests/EditMode/Game/SnapshotModelTests.cs`
- Modify: `Assets/Tests/EditMode/Game/SaveGameCodecTests.cs`
- Modify: `Assets/Tests/PlayMode/SessionHostPlayTests.cs`

- [ ] **Step 1: Failing carry/snapshot tests.**

Capture→reset→apply сохраняет cells, plasma/BFG ownership и current/pending.
Inter-level carry переносит их, но по-прежнему очищает keys/powers.

- [ ] **Step 2: Определить active BFG charge snapshot.**

Чтобы save в первые 30 тиков не отменял и не дублировал будущий выстрел,
`PlayerSnapshot` v4 дополнительно хранит optional active weapon action:

- action weapon;
- тики до commit/окончания;
- исходный shot direction;
- committed flag/phase, если это требуется выбранной scheduler model.

Предпочтительно создать immutable `WeaponActionSnapshot`, а не добавлять
несвязанные primitive parameters в каждый constructor. v1–v3 defaults =
отсутствие active action. Apply восстанавливает scheduler до resume.

- [ ] **Step 3: Расширить player models.**

Добавить `Cells`, `OwnsPlasmaRifle`, `OwnsBfg9000` и validation ownership для
current/pending. Старые constructor overloads делегируют новым с safe defaults.

- [ ] **Step 4: Bump и append-only codec.**

`SaveGame.SchemaVersion = 4`. После v3 player fields записать:

1. cells;
2. owns plasma;
3. owns BFG;
4. optional active-action payload.

`TryReadPlayer(version)` читает их только для v4. Encode принимает только
current schema; decode продолжает принимать v1–v4. Не менять порядок v1–v3
prefix.

- [ ] **Step 5: Backward compatibility tests.**

- v4 golden round-trip;
- synthetic v3 payload с удалённым v4 suffix → defaults cells=0/no weapons/no
  action;
- существующие synthetic v1/v2 tests остаются;
- invalid action weapon/phase/timer/direction отвергается;
- 39 cells + active BFG commit невозможно создать/restore;
- re-encode старого save пишет v4.

`SaveGameCodecTests` не должен опираться на один «магический player byte
count» без именованных per-version constants.

- [ ] **Step 6: Carry/save tests PASS.**

Запустить SessionState/SnapshotModel/SaveGameCodec и SessionHostPlayTests.

- [ ] **Step 7: Commit checkpoint (только по разрешению):**
`Stage 6c extension: persist cells plasma and BFG in save v4`

---

## Task 8: Projectile snapshot v4 и exactly-once BFG restore

**Files:**
- Modify: `Assets/Scripts/Game/WorldSnapshot.cs`
- Modify: `Assets/Scripts/Game/SaveGameCodec.cs`
- Modify: `Assets/Scripts/MapBuild/IProjectileSnapshotSource.cs` if needed
- Modify: `Assets/Scripts/MapBuild/WorldSnapshotCapture.cs`
- Modify: `Assets/Scripts/MapBuild/WorldSnapshotRestore.cs`
- Modify: `Assets/Scripts/MapBuild/PlayerPlasmaProjectile.cs`
- Modify: `Assets/Scripts/MapBuild/PlayerBfgProjectile.cs`
- Modify: `Assets/Tests/EditMode/Game/SnapshotModelTests.cs`
- Modify: `Assets/Tests/EditMode/Game/SaveGameCodecTests.cs`
- Modify: `Assets/Tests/PlayMode/WorldCapturePlayTests.cs`
- Modify: `Assets/Tests/PlayMode/SaveLoadPlayTests.cs`

- [ ] **Step 1: Failing projectile DTO/codec tests.**

v4 `ProjectileSnapshot` должен выражать:

- type/owner/position/velocity;
- phase `Flying/Exploding`;
- frame index + remaining frame tics/time;
- shot direction;
- `SprayApplied`.

v1–v3 projectile records декодируются по старому layout с defaults. Для этого
`TryReadWorld` и `TryReadProjectile` получают envelope version.

- [ ] **Step 2: Versioned codec без двусмысленного layout.**

В v4 append новые поля к **каждой** projectile record после старого prefix.
Reader знает version из envelope; count bounds проверяются до allocation.
Rocket/monster snapshots получают defaults `Flying`, frame 0 и не меняют
restore semantics.

- [ ] **Step 3: Capture plasma/BFG.**

Plasma сохраняет flight/impact visual phase; impact damage уже применён и не
повторяется. BFG обязательно сохраняет pre-spray exploding phase, потому что
будущий spray authoritative. `SprayApplied` отделяет post-spray state.

- [ ] **Step 4: Restore dispatch.**

`WorldSnapshotRestore` явно dispatch'ит:

- `RocketRules.SnapshotType`;
- `PlasmaRules.SnapshotType`;
- `BfgRules.SnapshotType`;
- monster projectile types.

Unknown v4 projectile type возвращает controlled restore error до gameplay
resume вместо текущего silent skip. BFG owner должен резолвиться как player;
иначе restore отклоняется.

- [ ] **Step 5: Exactly-once PlayMode tests.**

- plasma saved in flight продолжает trajectory и один раз наносит damage;
- BFG saved flying делает direct hit/spray;
- BFG saved on impact frame A/B выполняет spray один раз;
- BFG saved after spray не повторяет damage;
- existing rocket/monster projectile save-load не регрессирует.

- [ ] **Step 6: Codec/capture/save-load regression PASS.**

- [ ] **Step 7: Commit checkpoint (только по разрешению):**
`Stage 6c extension: restore plasma and BFG projectiles exactly once`

---

## Task 9: Full QA, интерактивная приёмка и фактическая документация

**Files:**
- Modify: `docs/superpowers/specs/2026-07-11-plasma-bfg-design.md`
- Modify: `docs/superpowers/plans/2026-07-11-plasma-bfg.md`
- Modify: `docs/superpowers/specs/2026-07-02-weapons-design.md`
- Modify: `docs/superpowers/specs/2026-07-10-pickups-design.md`
- Modify: `docs/doom-unity-remake-plan.md`
- Modify after sign-off: `CLAUDE.md`

- [ ] **Step 1: Полный EditMode.**

Запустить без фильтра; записать фактический total из свежего XML. Никаких
compile warnings/errors от obsolete constructor overloads.

- [ ] **Step 2: Полный PlayMode.**

Запустить без `-nographics`, без фильтра; записать total. Отдельно подтвердить
rocket/barrel/monster missiles, HUD, session, pickup, world capture и
save/load regressions.

- [ ] **Step 3: Интерактивная приёмка.**

- pickup plasma в исходной проблемной нише;
- continuous plasma fire, release recovery, damage/effects/sound;
- BFG charge, projectile direct hit, delayed 40-ray room clear;
- перемещение до impact меняет tracer origin, поворот не меняет направление;
- HUD slots 6/7 и cells при 4:3/16:9;
- carry E1M?→next map;
- save/load во время BFG charge, flight, pre-spray и post-spray.

- [ ] **Step 4: Обновить статус только по факту.**

После PASS/sign-off:

- новая spec → `реализован`;
- roadmap bullet → ✅;
- старые weapons/pickups deferred notes → implemented dated amendment;
- `CLAUDE.md` → фактические компоненты, schema v4, totals и remaining limits;
- этот plan → выполненные checkboxes.

До этого шага документы остаются в статусе «запланирован».

- [ ] **Step 5: Проверить repository hygiene.**

Не включать `Library/`, `Logs/`, `Builds/`, save slots или player binaries.
Новые `.meta` добавлять вместе с scripts/tests после импорта Unity.

- [ ] **Step 6: Commit checkpoint (только по разрешению):**
`Stage 6c extension: finish plasma and vanilla BFG`

---

## Порядок зависимостей

```text
Task 1 WAD/source audit
  -> Task 2 pure models/rules
    -> Task 3 weapon scheduler
      -> Task 4 plasma runtime
      -> Task 5 BFG runtime
        -> Task 6 pickups/HUD/audio
          -> Task 7 player carry/save v4
            -> Task 8 projectile restore
              -> Task 9 full QA/docs
```

Task 4 и pure часть Task 5 можно делать параллельно после Task 3. Task 6 tests
можно начать после Task 2, но final prewarm зависит от полного списка runtime
frames/sounds. Task 7 должен определить schema до Task 8 codec changes.

## Stop conditions

- Stage 7e ещё не закрыт: не начинать реализацию расширения.
- Required WAD lump отсутствует: остановиться и зафиксировать controlled
  WAD-driven fallback; не создавать replacement asset.
- BFG реализуется через generic radius splash: остановиться — это нарушает
  утверждённый vanilla design.
- Save до BFG spray теряет или повторяет damage: не принимать visual-only
  workaround, расширить authoritative snapshot.
- Новый codec ломает v1–v3: исправить migration, не удалять старые tests.
- Unknown projectile silently disappears on v4 restore: вернуть controlled
  validation error до resume.
- Unity batchmode заблокирован Editor: отметить run `BLOCKED`, не объявлять
  PASS по старому XML.

## Definition of Done

- [ ] Cells 300/600, четыре pickups и backpack grant работают.
- [ ] Plasma Rifle: slot 6, 1 cell, projectile 5..40, WAD view/effects/sound.
- [ ] BFG9000: slot 7, 40 cells, delayed ball, direct 100..800 и 40 vanilla
  tracers без rocket splash.
- [ ] HUD/input/noise/pickup sound интегрированы.
- [ ] Carry и save schema v4 сохраняют cells/ownership/current/pending/action.
- [ ] Plasma/BFG projectile save/load имеет exactly-once damage; v1–v3 читаются.
- [ ] E1 matrix классифицирует 2004/2006/2047/17 как `Implemented`.
- [ ] Полные EditMode/PlayMode suites зелёные.
- [ ] Интерактивная приёмка подписана, документы обновлены фактическими totals.
