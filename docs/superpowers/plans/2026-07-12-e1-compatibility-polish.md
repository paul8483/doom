# Stage 9: E1 Compatibility Polish — Implementation Plan

> **For agentic workers:** execute this plan task-by-task. Steps use checkbox
> (`- [ ]`) syntax for tracking. Do not mark a gate complete from an old
> XML/log, build or capture.

**Goal:** Закрыть ограниченный набор известных E1 compatibility gaps:
scroll 48/85, crushers/crush damage, queued weapon switch/death lower,
pickup animation, E1 XDEATH и компактную SFX pitch/priority/pre-warm policy.

**Architecture:** Pure state/rules остаются в `Doom.Specials`, `Doom.Game` и
`Doom.Things`; Unity runtime glue — в `Doom.MapBuild`. Scroll и pickup phase
выводятся из `GameTic`; crusher state является authoritative и поднимает save
schema до v6 с сохранением decode v1–v5. Спека:
`docs/superpowers/specs/2026-07-12-e1-compatibility-polish-design.md`.

**Статус:** 🚧 automated verification green; interactive sign-off pending.

**Implementation update (2026-07-12):** локально реализованы scroll 48/85,
crusher/crush damage + schema v6, queued switch/death lower, pickup animation,
E1 XDEATH и compact SFX policy/pre-warm; добавлены focused tests.

**Verification update (2026-07-12):** fresh gates recorded in
`Logs/stage9-e1-compatibility-notes.md`:

- focused EditMode **61/61**;
- full EditMode **492/492** (`Logs/stage9-t9-full-edit.xml`);
- full PlayMode **107/107** (`Logs/stage9-t9-full-play.xml`) after fog-boot
  test fix (`EnhancedAtmospherePlayTests` seeds Enhanced via settings store);
- Windows standalone **Success** (~128 MB exe report / ~122 MB folder);
- exclusions clean;
- interactive Stage 9 checklist **PENDING** — do not close `CLAUDE.md` yet.

**Tech Stack:** Unity 6000.4.8f1, C#/.NET profile Unity, Unity Test Framework,
URP 17.4.0, Input System 1.11.2, `freedoom1.wad`, существующие `Doom.Wad`,
`Doom.Graphics`, `Doom.Map`, `Doom.Things`, `Doom.Game`, `Doom.Specials`,
`Doom.Audio`, `Doom.MapBuild`.

**Базовая линия на дату старта Stage 9:** 459 EditMode + 95 PlayMode
(Stage 8 Task 14 close, 2026-07-12). Эти числа являются только baseline.

## Жёсткие границы scope

В Stage 9 входят только:

- linedef texture scroll 48/85;
- Doom 1 crusher family, stop/retrigger и crush damage (synthetic fixture,
  поскольку Freedoom E1 не содержит dedicated crusher linedefs);
- save schema v6 для crusher state с decode compatibility v1–v5;
- queued switch и weapon lower при смерти;
- WAD-driven pickup animation;
- XDEATH для поддерживаемого E1 roster;
- компактные SFX pitch/priority rules и полный catalog pre-warm;
- тестовые, build и интерактивные verification gates.

Не входят:

- sound occlusion;
- полная DMX channel/mixer emulation;
- automap;
- новые powerups;
- flying monsters;
- Doom II;
- общая PWAD/mod compatibility;
- finale/cast/ENDOOM.

Если задача требует одну из этих систем, остановиться и вынести её в отдельный
future design, не расширяя Stage 9.

## Запуск тестов

Для каждого запуска использовать уникальные XML/log. Открытый Unity Editor
блокирует batchmode; в таком случае закрыть Editor или отметить прогон
`BLOCKED`. Не добавлять `-quit` к `-runTests`.

```powershell
# EditMode
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -nographics -projectPath "C:\Development\doom" `
    -runTests -testPlatform EditMode -testFilter "<FILTER>" `
    -testResults "C:\Development\doom\Logs\stage9-<task>-edit.xml" `
    -logFile "C:\Development\doom\Logs\stage9-<task>-edit.log"

# PlayMode — physics/URP запускать без -nographics
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -projectPath "C:\Development\doom" `
    -runTests -testPlatform PlayMode -testFilter "<FILTER>" `
    -testResults "C:\Development\doom\Logs\stage9-<task>-play.xml" `
    -logFile "C:\Development\doom\Logs\stage9-<task>-play.log"
```

Per-test PASS/FAIL читать из XML. Compile-only, старый XML, test count в
документации и успешный запуск Editor не заменяют актуальный suite.

## Source-control gate для каждой Task

1. Проверить `git status --short` и diff до изменений. На старте документации
   известен untracked `ProjectSettings/SceneTemplateSettings.json`; не удалять и
   не присваивать его Stage 9 без отдельного решения.
2. Не перезаписывать пользовательские изменения wholesale.
3. Все новые Unity assets/scripts/tests под `Assets/` получают `.meta`.
4. Не commit'ить `Logs/`, `Library/`, `Temp/`, captures, build output или
   save-slot artifacts.
5. До checkpoint запускать узкий тест, затем релевантную assembly suite.
6. `Commit checkpoint` — только предлагаемая граница. Commit разрешён лишь по
   отдельной просьбе пользователя.
7. `CLAUDE.md` обновлять только в Task 9 после фактических totals/build/sign-off.

---

## Task 1: Зафиксировать E1 compatibility inventory и baseline

**Files:**
- Create: `Logs/stage9-e1-compatibility-notes.md`
- Create: `Assets/Tests/EditMode/Specials/E1CompatibilityInventoryTests.cs`
- Modify: `Assets/Tests/EditMode/Specials/Doom.Specials.Tests.asmdef`
  только при необходимости

- [ ] **Step 1: Зафиксировать исходное состояние.**

Записать branch/HEAD, Unity/package versions, текущий dirty status, Stage 8
baseline 459/95 и точные новые XML/log имена. Запустить полные suites до
изменений; записать фактические totals и failures/skips.

- [ ] **Step 2: Инвентаризировать E1M1–E1M9.**

Программно перечислить occurrences specials 48/85, crusher family
6/25/44/49/55/56/57/65/72/73/74/77/94/100/127/141 и любые связанные tagged
sectors. Отделить реально встречающийся E1 subset от table completeness.

- [ ] **Step 3: Инвентаризировать WAD frames и SFX.**

Проверить item animation frames, XDEATH frames для POSS/SPOS/TROO/SARG/Spectre/
Baron и все DS lumps, запрашиваемые существующими runtime call sites. Missing
optional data записать как fallback case, не как обязательный authored asset.

- [ ] **Step 4: Добавить runtime-support coverage assertions.**

Тест должен требовать `WallScrollRules` для E1 Scroll и `IsExecutable` +
`CrusherRules` для встреченных Crusher entries, но не требовать Doom II/PWAD
specials.

- [ ] **Step 5: Сохранить baseline notes.**

Записать test commands/results и inventory. Не объявлять Stage 9 завершённым.

**Commit checkpoint:** `Stage 9: inventory E1 compatibility gaps`

---

## Task 2: Реализовать linedef scrolling 48/85

**Files:**
- Modify: `Assets/Scripts/Specials/LineSpecialTable.cs`
- Create: `Assets/Scripts/Specials/WallScrollRules.cs`
- Create: `Assets/Scripts/MapBuild/Rendering/WallScrollSystem.cs`
- Modify: `Assets/Scripts/MapBuild/MapLoader.cs`
- Modify: `Assets/Scripts/MapBuild/SectorGeometry.cs` или wall binding registry
- Modify: `Assets/Scripts/MapBuild/Rendering/WorldRenderContext.cs`
  только если profile rebinding требует этого
- Create: `Assets/Tests/EditMode/Specials/WallScrollRulesTests.cs`
- Create: `Assets/Tests/PlayMode/WallScrollPlayTests.cs`

- [ ] **Step 1: Написать failing pure rules tests.**

Зафиксировать 48=left, 85=right, 35 Hz cadence, texture-width normalization,
wrap и отсутствие drift от variable frame delta. Rule API принимает tic,
direction и width, но не Unity material/time.

- [ ] **Step 2: Сделать 48/85 executable persistent effects.**

Не запускать их через one-shot `LineActivator.Activate`. На map build создать
bindings только для соответствующей linedef side/wall section. Не сдвигать
shared material всех стен с тем же texture.

- [ ] **Step 3: Реализовать renderer binding.**

Применять UV offset через `MaterialPropertyBlock` или owned material variant.
Система не перестраивает Mesh/MeshCollider и не создаёт material каждый frame.

- [ ] **Step 4: Связать фазу с `WorldSnapshot.GameTic`.**

New game начинает с tic 0; load вычисляет тот же offset из restored game tic.
Отдельное поле save не добавлять.

- [ ] **Step 5: Проверить оба graphics profiles.**

PlayMode: одинаковые direction/phase в Classic и Enhanced, profile switch не
сбрасывает offset, 1000 tics не увеличивают Mesh/Material counts.

- [ ] **Step 6: Прогнать gates.**

Узко: `WallScrollRulesTests`, `WallScrollPlayTests`,
`LineSpecialCoverageTests`. Затем `Doom.Specials.Tests` и graphics profile smoke.

**Commit checkpoint:** `Stage 9a: execute E1 wall scrolling`

---

## Task 3: Реализовать crusher state machine

**Files:**
- Modify: `Assets/Scripts/Specials/LineSpecial.cs`
- Modify: `Assets/Scripts/Specials/LineSpecialTable.cs`
- Create: `Assets/Scripts/Specials/CrusherRules.cs`
- Modify: `Assets/Scripts/Specials/SectorActions.cs`
- Modify: `Assets/Scripts/MapBuild/LineActivator.cs`
- Modify: `Assets/Scripts/MapBuild/SectorMover.cs` либо
  Create: `Assets/Scripts/MapBuild/CrusherMover.cs`
- Modify: `Assets/Scripts/MapBuild/SectorGeometry.cs`
- Create: `Assets/Tests/EditMode/Specials/CrusherRulesTests.cs`
- Create: `Assets/Tests/PlayMode/CrusherPlayTests.cs`

- [ ] **Step 1: Написать failing crusher rule tests.**

Зафиксировать target `floor + 8`, start direction, slow/fast speeds,
down/reverse cycle, silent flag, stop/restart и tagged-sector selection.
Включить только E1-required behavior из Task 1 plus shared rules, нужные для
уже классифицированных Doom 1 variants.

- [ ] **Step 2: Ввести явный mover kind/state.**

Crusher не маскируется под door `cycle`. State должен различать normal mover и
crusher, верхнюю/нижнюю границы, active/stopped, direction и silent.

- [ ] **Step 3: Подключить LineActivator.**

Start special создаёт crusher только если sector policy разрешает; repeat не
дублирует thinker; stop special останавливает правильные tagged sectors.
One-shot line отмечается fired только после успешной активации.

- [ ] **Step 4: Сохранить geometry invariants.**

Двигать существующий runtime ceiling/floor source и rebuild in place.
MeshCollider/Mesh identity должны соответствовать текущей mover policy; не
создавать новый GameObject каждый tic.

- [ ] **Step 5: Проверить interaction.**

PlayMode: start/reverse/stop/retrigger, door/lift coexistence, player floor
riding regression и cleanup на scene reload.

- [ ] **Step 6: Прогнать gates.**

Узко: `CrusherRulesTests`, `CrusherPlayTests`, `SectorActionPlayTests`,
`SectorRetriggerPlayTests`, `PlayerLiftRiderPlayTests`.

**Commit checkpoint:** `Stage 9b: add Doom 1 crusher movers`

---

## Task 4: Добавить crush damage и save schema v6

**Files:**
- Create: `Assets/Scripts/Specials/CrushDamageRules.cs`
- Create: `Assets/Scripts/MapBuild/CrushDamageSystem.cs`
- Modify: `Assets/Scripts/MapBuild/SectorMover.cs` или `CrusherMover.cs`
- Modify: `Assets/Scripts/MapBuild/PlayerHealth.cs`
  только если common damage API недостаточен
- Modify: `Assets/Scripts/MapBuild/EnemyHealth.cs`
- Modify: `Assets/Scripts/Game/SaveGame.cs`
- Modify: `Assets/Scripts/Game/WorldSnapshot.cs`
- Modify: `Assets/Scripts/Game/SaveGameCodec.cs`
- Modify: `Assets/Scripts/MapBuild/WorldSnapshotCapture.cs`
- Modify: `Assets/Scripts/MapBuild/WorldSnapshotRestore.cs`
- Modify: `Assets/Scripts/MapBuild/WorldStateRegistry.cs`
- Create: `Assets/Tests/EditMode/Specials/CrushDamageRulesTests.cs`
- Modify: `Assets/Tests/EditMode/Game/SnapshotModelTests.cs`
- Modify: `Assets/Tests/EditMode/Game/SaveGameCodecTests.cs`
- Create: `Assets/Tests/EditMode/Game/SaveCompatibilityFixtureTests.cs`
- Create: `Assets/Tests/PlayMode/CrushDamagePlayTests.cs`
- Modify: `Assets/Tests/PlayMode/SaveLoadPlayTests.cs`

- [ ] **Step 1: Написать failing damage cadence tests.**

10 damage каждые 4 gameplay tics, максимум один pulse/victim/tic, только living
shootable targets. Проверить player armor path, monster death и barrel common
damage path.

- [ ] **Step 2: Реализовать deterministic victim query.**

Определять пересечение actor bounds с текущим sector gap. Не полагаться на
частоту `OnCollisionStay`; не наносить урон pickups/decorations/projectiles.
Не добавлять scene-wide `FindObjects*` каждый tic.

- [ ] **Step 3: Спроектировать v6 snapshot fields.**

Добавить explicit `MoverKind`/crusher state, достаточный для восстановления
следующего tic: phase/direction, bounds, speed, active/stopped, silent.
Проверить enum/range validation и equality/hash.

- [ ] **Step 4: Поднять codec schema 5 → 6.**

Encoder пишет v6. Decoder принимает 1–6. v6 fields читать только при
`version >= 6`; v1–v5 получают legacy defaults. Не менять порядок старых
fields и не читать v6 bytes из старых records.

- [ ] **Step 5: Добавить настоящие compatibility fixtures.**

Построить/зафиксировать payloads по layout каждой версии 1–5. Для каждой
проверить header/checksum/decode и version-specific defaults. Простая замена
version integer в v6 byte array запрещена.

- [ ] **Step 6: Реализовать capture/restore order.**

Сначала применить heights и crusher state, затем geometry, victims/targets и
resume simulation. Mid-downstroke и stopped crusher после load продолжают
именно сохранённую phase.

- [ ] **Step 7: Прогнать gates.**

Узко: `CrushDamageRulesTests`, `SaveCompatibilityFixtureTests`,
`SaveGameCodecTests`, `CrushDamagePlayTests`, `SaveLoadPlayTests`. Затем полные
`Doom.Game.Tests`, `Doom.Specials.Tests` и save/mover PlayMode group.

**Commit checkpoint:** `Stage 9b: save and restore crusher damage state`

---

## Task 5: Подключить queued weapon switch и death lower

**Files:**
- Modify: `Assets/Scripts/Game/WeaponLoadout.cs`
- Modify: `Assets/Scripts/Game/WeaponActionScheduler.cs`
  только если нужен явный ready boundary
- Modify: `Assets/Scripts/MapBuild/PlayerWeapons.cs`
- Modify: `Assets/Scripts/MapBuild/WeaponView.cs`
- Modify: `Assets/Scripts/MapBuild/PlayerDeathHandler.cs`
- Modify: `Assets/Scripts/MapBuild/MapLoader.cs`
- Modify: `Assets/Tests/EditMode/Game/WeaponLoadoutTests.cs`
- Modify/Create: scheduler tests в `Assets/Tests/EditMode/Game/`
- Create: `Assets/Tests/PlayMode/WeaponTransitionPlayTests.cs`
- Modify: `Assets/Tests/PlayMode/PlayerDamagePlayTests.cs`
- Modify: `Assets/Tests/PlayMode/SaveLoadPlayTests.cs`

- [ ] **Step 1: Написать failing queue tests.**

Mid-action request сохраняется; последний valid request заменяет предыдущий;
unowned request отклоняется; apply происходит один раз на ready boundary и
очищает pending.

- [ ] **Step 2: Заменить drop input на queue.**

Текущий `SelectSlot` не должен `return` и терять request во время action.
Immediate legal switch остаётся immediate; blocked switch вызывает
`TryQueuePending`.

- [ ] **Step 3: Определить boundary/auto-downgrade порядок.**

Committed shot завершается. Затем ammo safety/auto-downgrade и pending selection
применяются в документированном порядке без двойного switch/Fired event.
Slot 1 сохраняет chainsaw/fist semantics.

- [ ] **Step 4: Проверить существующий save contract.**

`HasPendingWeapon/PendingWeapon` уже есть в v1 player layout. Round-trip и load
mid-action не требуют нового v6 field. Invalid/unowned pending очищается
существующей restore validation.

- [ ] **Step 5: Реализовать death-lower presentation.**

`PlayerDeathHandler` уведомляет `WeaponView`; view переходит
ready/fire → lowering → hidden по 35 Hz cadence. Damage/weapon scheduler,
loadout и ammo не меняются. Respawn сбрасывает view в ready.

- [ ] **Step 6: Проверить OnGUI lifecycle.**

PlayMode assertions проверяют state/position probe, а интерактивный gate —
фактический IMGUI output. `Camera.Render()` не считается доказательством
weapon view.

- [ ] **Step 7: Прогнать gates.**

`WeaponLoadoutTests`, scheduler tests, `WeaponTransitionPlayTests`,
`WeaponPlayTests`, `PlayerDamagePlayTests`, `SaveLoadPlayTests`.

**Commit checkpoint:** `Stage 9c: polish weapon transitions`

---

## Task 6: Добавить WAD-driven pickup animation

**Files:**
- Create: `Assets/Scripts/Things/PickupAnimationTable.cs`
- Modify: `Assets/Scripts/MapBuild/ThingSpawner.cs`
- Modify: `Assets/Scripts/MapBuild/PickupFactory.cs`
- Create: `Assets/Scripts/MapBuild/PickupAnimator.cs`
- Modify: `Assets/Scripts/MapBuild/SpriteBillboard.cs`
  только если current frame API недостаточен
- Modify: `Assets/Scripts/MapBuild/MapLoader.cs`
- Create: `Assets/Tests/EditMode/Things/PickupAnimationTableTests.cs`
- Create: `Assets/Tests/PlayMode/PickupAnimationPlayTests.cs`
- Modify: `Assets/Tests/PlayMode/PickupPlayTests.cs`
- Modify: `Assets/Tests/PlayMode/SaveLoadPlayTests.cs`

- [ ] **Step 1: Написать failing table tests.**

Зафиксировать E1 item sequences/tics по state data, static fallback и
Freedoom frame resolution. Не считать любой sprite с A–D автоматически
анимированным.

- [ ] **Step 2: Реализовать shared animation policy.**

Map things и runtime drops получают один `PickupAnimator` only when sequence
имеет несколько valid frames. Phase = function of game tic + stable identity.

- [ ] **Step 3: Расширить pre-warm frames.**

До `NotifyWadClosed` запросить все frames из validated table. Missing optional
frame кэшируется и сокращает/отключает sequence без runtime WAD access.

- [ ] **Step 4: Сохранить gameplay neutrality.**

Animator не меняет transform/collider/touch bounds/doomednum. Collection,
item stats, keys/powers/ammo и death-drop registration остаются прежними.
Save хранит presence/position, не presentation timer.

- [ ] **Step 5: Проверить resource behavior.**

1000 animation tics не создают Mesh/Texture2D/Material/GameObject. Reload
освобождает компонент/cache обычным ownership path.

- [ ] **Step 6: Прогнать gates.**

`PickupAnimationTableTests`, `PickupAnimationPlayTests`, `PickupPlayTests`,
`SaveLoadPlayTests`, `ThingTableSpriteResolutionTests`.

**Commit checkpoint:** `Stage 9d: animate E1 pickups`

---

## Task 7: Реализовать E1 XDEATH

**Files:**
- Modify: `Assets/Scripts/Things/MonsterDef.cs`
- Modify: `Assets/Scripts/Things/MonsterTable.cs`
- Modify: `Assets/Scripts/Things/ThingDef.cs` только если нужен final XDEATH frame
- Modify: `Assets/Scripts/MapBuild/EnemyHealth.cs`
- Modify: `Assets/Scripts/MapBuild/MonsterController.cs`
- Modify: `Assets/Scripts/MapBuild/ThingSpawner.cs`
- Modify: `Assets/Scripts/MapBuild/MapLoader.cs` для pre-warm при необходимости
- Modify: `Assets/Tests/EditMode/Things/MonsterDataTests.cs`
- Create: `Assets/Tests/EditMode/Game/ExtremeDeathRulesTests.cs`
- Create: `Assets/Tests/PlayMode/ExtremeDeathPlayTests.cs`
- Modify: `Assets/Tests/PlayMode/MonsterAiPlayTests.cs`
- Modify: `Assets/Tests/PlayMode/SaveLoadPlayTests.cs`

- [ ] **Step 1: Написать failing data/rule tests.**

Для E1 roster определить optional XDEATH sequence/final frame и threshold.
Обычный lethal damage выбирает normal death; overkill ниже threshold —
XDEATH; missing sequence — normal fallback.

- [ ] **Step 2: Сохранить остаточный HP до clamp.**

`EnemyHealth` передаёт lethal health/overkill decision controller'у до
установки HP=0. Common path регистрирует kill один раз и отключает capsule.

- [ ] **Step 3: Проиграть extreme sequence.**

`MonsterController` использует ту же tic scheduler, billboard frame API и
terminal corpse handling. Spectre использует SARG data/material policy; Baron
использует BOSS frames.

- [ ] **Step 4: Сохранить drops/stats/source semantics.**

POSS/SPOS drop появляется ровно один раз в той же logical death boundary.
Crusher/rocket/BFG/barrel/hitscan используют общий threshold, а не hardcoded
source switch. Infighting target cleanup не регрессирует.

- [ ] **Step 5: Pre-warm и save fallback.**

Все optional frames проверяются до WAD close. Existing thing frame snapshot
восстанавливает final corpse. Полную mid-animation FSM serialization не
добавлять без failing E1 progression case.

- [ ] **Step 6: Прогнать gates.**

`MonsterDataTests`, `ExtremeDeathRulesTests`, `ExtremeDeathPlayTests`,
`MonsterAiPlayTests`, `PickupPlayTests` death-drop case и save smoke.

**Commit checkpoint:** `Stage 9e: add E1 extreme deaths`

---

## Task 8: Добавить compact SFX pitch/priority/pre-warm

**Files:**
- Create: `Assets/Scripts/Game/SfxPlaybackPolicy.cs`
- Create: `Assets/Scripts/Game/SfxCatalog.cs`
- Modify: `Assets/Scripts/MapBuild/SoundSystem.cs`
- Modify: `Assets/Scripts/MapBuild/SoundCache.cs`
- Modify: `Assets/Scripts/MapBuild/MapLoader.cs`
- Modify: SFX call sites только для передачи policy class/priority при
  необходимости
- Create: `Assets/Tests/EditMode/Game/SfxPlaybackPolicyTests.cs`
- Create: `Assets/Tests/EditMode/Game/SfxCatalogTests.cs`
- Modify: `Assets/Tests/EditMode/Audio/AudioFreedoomTests.cs`
- Modify: `Assets/Tests/PlayMode/SoundPlayTests.cs`
- Create: `Assets/Tests/PlayMode/SoundPriorityPlayTests.cs`

- [ ] **Step 1: Написать failing pure policy tests.**

Проверить priority ordering, bounded pitch ranges, fixed-pitch classes,
deterministic tie-break inputs и catalog uniqueness/completeness.

- [ ] **Step 2: Отделить audio variation RNG.**

Не использовать `PlayerWeapons.Rng`, monster brain RNG или общий
`DoomRandom`. Audio sequence не сериализуется и не меняет gameplay outcomes.

- [ ] **Step 3: Хранить per-source metadata.**

Для pooled source отслеживать priority и monotonically increasing start
sequence. Перед каждым playback сбрасывать pitch, loop, clip, spatialBlend,
distances и volume.

- [ ] **Step 4: Реализовать deterministic stealing.**

Сначала idle non-loop; затем lower-priority oldest; равный priority — oldest.
Более низкий priority не вытесняет высокий. Tracked loop не крадётся one-shot.
Stop/reuse очищает metadata.

- [ ] **Step 5: Сделать catalog единственным pre-warm source.**

Catalog включает weapons/player/E1 monsters/projectiles/pickups/switches/
doors/lifts/crushers/barrels. На startup каждый unique DS lump проходит
`SoundCache.Get` до WAD close. Runtime uncached request после close остаётся
warning/test failure probe.

- [ ] **Step 6: Проверить explicit exclusions.**

Не добавлять occlusion, REJECT/BSP propagation, full DMX channels,
source-port singularity или bit-exact mixer. Unity attenuation сохраняется,
если failing E1 test не требует узкой правки.

- [ ] **Step 7: Прогнать gates.**

`SfxPlaybackPolicyTests`, `SfxCatalogTests`, `AudioFreedoomTests`,
`SoundPlayTests`, `SoundPriorityPlayTests`; затем полные `Doom.Game.Tests`,
`Doom.Audio.Tests` и sound PlayMode group.

**Commit checkpoint:** `Stage 9f: prioritize and vary E1 sound effects`

---

## Task 9: Full regression, Windows build и приёмка

**Files:**
- Modify: `Assets/Tests/PlayMode/E1MapSmokePlayTests.cs`
- Create: `Assets/Tests/PlayMode/Stage9CompatibilityPlayTests.cs`
  если feature-specific tests не покрывают integrated gate
- Modify: `Logs/stage9-e1-compatibility-notes.md`
- Modify: `docs/doom-unity-remake-plan.md`
- Modify: `docs/superpowers/specs/2026-07-12-e1-compatibility-polish-design.md`
- Modify: `docs/superpowers/plans/2026-07-12-e1-compatibility-polish.md`
- Modify: `CLAUDE.md` только после фактического закрытия

- [x] **Step 1: Запустить integrated E1 smoke в обоих profiles.**

E1M1–E1M9 строятся в Classic/Enhanced; scroll systems bind without missing
wall, crushers register/restore, pickup/XDEATH frames и SFX pre-warm не дают
post-WAD cache miss. Progression blockers отсутствуют.
(`E1MapSmokePlayTests` green in `stage9-t9-full-play.xml`)

- [x] **Step 2: Запустить save compatibility gate.**

v6 current round-trip: active downstroke, upstroke и stopped crusher.
Fixtures v1–v5 декодируются с ожидаемыми defaults. Corrupt/unknown v7/invalid
enum/count отвергаются без partial restore.
(`SaveGameCodecTests` + `CrusherPlayTests` + `SaveLoadPlayTests` green)

- [x] **Step 3: Запустить resource/stress gate.**

Повторить map reload и Classic↔Enhanced switch. Scroll/pickup animation не
растят Mesh/Material; XDEATH не дублирует drops; SFX pool не растёт и защищает
loops; crusher components удаляются при teardown.
(covered by `WallScrollPlayTests`, `PickupPlayTests`, `SoundPlayTests`,
`MonsterAiPlayTests`, `CrusherPlayTests` in full PlayMode)

- [x] **Step 4: Запустить полные suites.**

Создать новые `stage9-t9-full-edit.xml` и `stage9-t9-full-play.xml`. Записать
totals/pass/fail/skip в notes. Любой failure/skip — blocker либо явно
обоснованное незавершённое состояние; не копировать 459/95.
**492 EditMode + 107 PlayMode**, 0 fail/skip. Notes:
`Logs/stage9-e1-compatibility-notes.md`.

- [x] **Step 5: Собрать Windows standalone.**

Использовать `Tools > Doom > Build Windows Standalone` /
`Stage7BuildMenu.BuildWindowsStandaloneCli`. Записать Unity version, command,
result, size и warnings. Build output не commit'ить.
Success → `Builds/Windows/DoomUnity.exe` (~128 MB reported / ~122 MB folder);
log `Logs/stage9-t9-windows-build.log`.

- [ ] **Step 6: Интерактивно проверить Stage 9 checklist.**

На E1:

- 48/85 visibly движутся в правильные стороны в Classic и Enhanced;
- crusher start/stop/retrigger, player/monster damage и save/load mid-cycle;
- slot press во время выстрела применяется после action;
- death view опускается и respawn возвращает weapon;
- map pickups и death drops анимируются и собираются;
- normal death и overkill XDEATH выглядят корректно;
- SFX pitch не затрагивает fixed cues, priority слышимо защищает важные sounds
  при stress.

- [x] **Step 7: Проверить exclusions.**

Diff не содержит automap/powerup/flying/Doom II/PWAD/finale systems,
REJECT/occlusion или full DMX mixer work. Любой incidental refactor должен быть
объяснён и покрыт тестом.

- [ ] **Step 8: Обновить документацию после факта.**

Только после Steps 1–7 (interactive Step 6 still pending):

- заменить in-progress на completed;
- записать фактические test totals, build и interactive sign-off;
- отметить Tasks 1–9;
- обновить roadmap и `CLAUDE.md`;
- перечислить реальные remaining limits, не обещания.

Automated totals already recorded in notes/plan header; full close waits on
Step 6.

**Commit checkpoint:** `Stage 9: complete E1 compatibility polish`

---

## Порядок и зависимости

```text
Task 1 inventory/baseline
  -> Task 2 scroll
  -> Task 3 crusher state
  -> Task 4 crush damage + save v6
  -> Task 5 weapon transitions
  -> Task 6 pickup animation
  -> Task 7 XDEATH
  -> Task 8 compact SFX policy
  -> Task 9 full suites/build/sign-off
```

Tasks 5–8 могут разрабатываться независимо после Task 1, но Task 7 должен
использовать common damage contract Task 4 при интеграции. Task 9 выполняется
только на объединённом результате всех предыдущих tasks.

## Stop conditions

- E1 inventory показывает неоднозначный special/frame/lump и реализация требует
  догадки вместо WAD/source data: записать blocker и уточнить scope.
- Crusher требует обхода common health/death/stats/drop path.
- v6 ломает decode настоящего v1–v5 fixture или меняет старый binary layout.
- Scroll/pickup animation создаёт Mesh/Material/Texture каждый frame/tic.
- Cosmetic audio pitch потребляет gameplay RNG.
- Runtime после закрытия WAD запрашивает непрогретый обязательный SFX/sprite.
- Изменение требует occlusion/full DMX/automap/powerups/flying/Doom II/PWAD/
  finale: остановить scope expansion.
- Full suite/build заблокирован открытым Editor: отметить `BLOCKED`, не
  использовать старые результаты.
- Интерактивный weapon-view gate пытаются заменить `Camera.Render()` capture.

## Definition of Done

- [ ] Specials 48/85 scroll correct wall side left/right в обоих profiles.
- [ ] Synthetic crusher start/stop/retrigger и crush damage работают через
      common gameplay paths.
- [ ] Save schema v6 round-trip'ит crusher state; v1–v5 fixtures декодируются.
- [ ] Mid-action weapon selection queue'ится и применяется на ready boundary.
- [ ] Weapon view опускается при смерти, скрывается и возвращается на respawn.
- [ ] E1 pickup sequences WAD-driven, pre-warmed и allocation-stable.
- [ ] E1 XDEATH выбирается по overkill, fallback normal death безопасен.
- [ ] SFX pitch/priority bounded и deterministic; gameplay RNG не меняется.
- [ ] Единый SFX catalog полностью pre-warm'ится до WAD close.
- [ ] Explicit exclusions не попали в diff.
- [ ] Dual-profile E1M1–E1M9 smoke зелёный.
- [ ] Полные EditMode/PlayMode suites зелёные с новыми XML и totals.
- [ ] Windows standalone собран и интерактивный Stage 9 checklist подписан.
- [ ] Roadmap/spec/plan/`CLAUDE.md` закрыты только после фактических результатов.
