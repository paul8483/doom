# Этап 9. E1 compatibility polish — дизайн

**Дата:** 2026-07-12  
**Статус:** 🚧 automated verification green; interactive sign-off pending  
**Предыдущие этапы:** Stages 0–8 завершены; Stage 9 automated totals —
492 EditMode + 107 PlayMode (2026-07-12); Stage 8 baseline was
459 EditMode + 95 PlayMode  
**Контекст:** ограниченная compatibility-веха после Stage 8. Она закрывает
известные пробелы уже поддерживаемого E1, не расширяя проект до полного
source-port или Doom II.

## Цель

Довести текущий Freedoom/DOOM E1 runtime до более цельного поведения в семи
узких областях:

- linedef scrolling 48/85;
- crushers и crush damage;
- queued weapon switch и опускание оружия при смерти;
- WAD-driven анимация pickups;
- E1 extreme-death (`XDEATH`);
- компактная политика pitch/priority для SFX;
- единый проверяемый каталог pre-warm для всех runtime SFX.

Этап не меняет архитектурные основания проекта: WAD остаётся источником
контента, gameplay rules живут в pure C# слоях, Unity отвечает за runtime glue,
а BSP/REJECT/BLOCKMAP не возвращаются.

## Объём этапа

### 9a. Linedef scrolling 48/85

- `LineSpecialTable` продолжает быть источником классификации, но specials
  `48` и `85` становятся поддерживаемыми постоянными presentation effects.
  Они создаются при build карты и не проходят через one-shot `LineActivator`.
- Type 48 прокручивает middle wall texture влево, type 85 — вправо. Направление
  определяется ориентацией sidedef/UV, а не camera transform.
- Скорость задаётся в DOOM tics и преобразуется в UV через реальную ширину WAD
  texture. Поведение не зависит от frame rate.
- Scroll действует на конкретную сторону/секцию linedef, включая
  texture-grouped wall meshes. Нельзя сдвигать все стены одного material.
- Runtime использует `MaterialPropertyBlock`, выделенный material instance или
  отдельный scroll binding с явным ownership. Перестройка geometry каждый кадр
  запрещена.
- Classic и Enhanced показывают одинаковое направление и фазу. Enhanced
  material effects не заменяют linedef scroll.
- Scroll является детерминированным производным от `GameTic` и map data,
  поэтому отдельное поле savegame ему не требуется. После load фаза
  восстанавливается из сохранённого `WorldSnapshot.GameTic`.

### 9b. Crushers и crush damage

- Исполняется E1-relevant crusher family, уже классифицированная в
  `LineSpecialTable`: start/repeat/stop crusher specials и ceiling/floor actions
  с crush semantics. Перед реализацией обязательна инвентаризация E1M1–E1M9;
  расширение за реально нужный Doom 1 subset допускается только если оно
  использует тот же state machine.
- Crusher является отдельным типом sector mover, а не door cycle с другим
  target. Он хранит верхнюю/нижнюю границы, направление, скорость, режим
  silent/normal и active/stopped phase.
- Ceiling crusher движется к `floor + 8` DOOM units, при контакте наносит урон и
  продолжает либо разворачивается согласно типу special. Stop special
  останавливает tagged crushers без удаления состояния, нужного для
  корректного restore/retrigger.
- Контакт вычисляется по реальному зазору сектора и bounds actor, а не только по
  факту Unity collision callback. Один victim получает не более одного crush
  pulse за заданный gameplay tic.
- Базовая cadence: 10 damage каждые 4 tics для living shootable actors,
  синхронно с 35 Hz gameplay time. Player damage проходит через
  `PlayerHealth`; monsters/barrels — через существующий damage path, чтобы
  death, kill stats, drops, infighting policy и explosion hooks не обходились.
- Crusher не наносит урон pickups, projectiles, decorations без shootable
  health или уже неактивным объектам. Трупы не должны блокировать progression;
  визуальный gib трупов не входит отдельно от E1 `XDEATH`.
- Player riding floors, door/lift behavior, sector rebuild и collider identity
  не регрессируют. Geometry/collider обновляются существующим
  `SectorGeometry`, без создания нового Mesh на каждый tic.

### 9c. Save schema v6

- `SaveGame.SchemaVersion` повышается с 5 до 6;
  `FirstSupportedSchemaVersion` остаётся `1`.
- v6 сохраняет authoritative crusher/mover kind и crusher-specific state:
  phase/direction, верхнюю и нижнюю границы, speed, silent flag и состояние
  stopped/active. Точный DTO может быть компактнее, если эти данные однозначно
  восстанавливают тот же следующий tic.
- Новые поля добавляются version-gated в конец sector record либо другого
  существующего bounded record. Counts и enums валидируются до allocation/use.
- Декодеры v1–v5 остаются рабочими:
  - v1: базовые player/world fields;
  - v2: rockets/rocket launcher;
  - v3: chainsaw;
  - v4: cells/plasma/BFG и projectile phase;
  - v5: runtime sector light count;
  - v6: crusher state.
- Отсутствующие v6 fields в v1–v5 получают безопасные defaults
  (`MoverKind.Legacy`, crusher inactive) без изменения уже декодированных
  player, light, projectile и world данных.
- Encoder пишет только текущую v6. После загрузки старого save новый Save
  естественно записывает v6; in-place перезапись старого файла при одном Load
  не выполняется.
- Обязательны сохранённые binary fixtures или программно построенные payloads
  каждой версии 1–5. Нельзя доказывать compatibility, подменяя version byte у
  v6 payload.

### 9d. Weapon transition polish

- Slot input во время fire/cooldown не теряется. `PlayerWeapons` вызывает
  существующий `WeaponLoadout.TryQueuePending`; последний валидный запрос
  заменяет предыдущий.
- Pending weapon применяется ровно на первой legal ready boundary после
  завершения текущего action. Он не прерывает committed shot, не расходует ammo
  и не создаёт дополнительный `Fired`.
- Если pending weapon к моменту применения недоступен, запрос очищается и
  остаётся текущий/best available weapon. Auto-downgrade при нехватке ammo
  сохраняет приоритет gameplay safety над stale queued request.
- Slot 1 сохраняет выбор chainsaw/fist по текущей ownership policy.
- Existing `PlayerSnapshot.HasPendingWeapon/PendingWeapon` уже является
  save contract и продолжает round-trip без нового schema field.
- `WeaponView` получает явную presentation state machine
  ready/fire/lowering/hidden. При смерти текущее оружие опускается за
  фиксированное число tics до нижней границы экрана и остаётся скрытым.
- Death lower не меняет loadout, ammo или scheduler commit. Respawn возвращает
  ready view синхронно с существующим reset. Save/load живого игрока не
  восстанавливает transient OnGUI pixels; authoritative pending switch
  восстанавливается.

### 9e. Pickup animation

- Pure `PickupAnimationTable` (или эквивалент) описывает известные E1 item
  sprite sequences и tic cadence из DOOM states. Отсутствующий sequence имеет
  статический frame fallback.
- `ThingSpawner`/`PickupFactory` подключают лёгкий ticker только к pickups,
  у которых действительно больше одного валидного кадра.
- `SpriteBillboard.SetFrame` переиспользует существующий renderer/material cache;
  каждый frame не создаются quad, Texture2D или Material.
- Map pickup и runtime death drop используют одинаковую animation policy.
- Pickup animation является presentation state: touch radius, doomednum,
  inventory rules, item count, save identity и position не меняются.
- Фаза детерминирована от `GameTic` и stable spawn/map id. Save хранит наличие и
  позицию pickup, но не отдельный animation timer.
- Все нужные frames pre-warm'ятся до закрытия WAD. Missing optional frame
  завершает sequence безопасным fallback, а не вызывает runtime WAD access.

### 9f. E1 `XDEATH`

- `MonsterDef` получает optional `XDeath` sequence и final extreme corpse frame
  для поддерживаемого E1 roster: POSS, SPOS, TROO, SARG/Spectre и Baron, только
  если соответствующие WAD sprite frames существуют.
- Выбор normal death vs `XDEATH` использует overkill threshold на остаточном HP
  до clamp. `EnemyHealth` не должен терять эту информацию до передачи события
  в `MonsterController`.
- XDEATH использует ту же deterministic 35 Hz animation state machine, collider
  disable, kill counting, target cleanup и death-drop timing, что normal death.
  Kill регистрируется и drop создаётся ровно один раз.
- Crusher damage, rockets, BFG, barrels и hitscan могут вызвать XDEATH через
  общий damage path; тип источника сам по себе не форсирует gib.
- Missing optional extreme frames дают normal death. Никаких authored
  replacement sprites не добавляется.
- `ThingSnapshot.Frame` и существующий thing state продолжают сохранять
  отображаемый corpse frame. Расширение полной mid-death FSM save fidelity не
  является отдельной целью Stage 9.

### 9g. Compact SFX pitch, priority и pre-warm

- Вводится маленький pure catalog `SoundPlaybackPolicy`: lump/class,
  local/world, priority, допустимый pitch range и loop/one-shot policy.
- Pitch variation применяется только к перечисленным one-shot classes
  (например, monster voice/active и допустимые impact variants). UI, weapon
  timing cues, loops и звуки с фиксированным pitch остаются `1.0`.
- Pitch выбирается отдельным deterministic audio RNG/sequence. Он не потребляет
  gameplay `DoomRandom` и не меняет spread, damage, AI или save state.
- Каждый pooled source при reuse полностью получает pitch/priority/spatial
  defaults; старый pitch не протекает в следующий sound.
- При заполненном пуле новый one-shot может вытеснить только менее приоритетный
  либо равноприоритетный более старый one-shot. Tracked loops не крадутся
  one-shot запросом. Tie-break deterministic.
- `CollectSfxNames` заменяется или проверяется единым catalog-driven pre-warm:
  все lumps, которые способны запросить weapons, player, E1 monsters,
  projectiles, pickups, switches, movers, crushers и barrels, декодируются до
  закрытия WAD.
- Optional missing lump кэшируется как miss с одним warning. Runtime playback
  после `NotifyWadClosed` не обращается к WAD.
- Это намеренно компактная политика поверх Unity `AudioSource`, а не полный
  порт DMX mixer/channel semantics.

## Сознательно не входит

- Sector/portal sound occlusion, REJECT-based propagation и geometry raycast
  occlusion.
- Полная DMX channel emulation: оригинальные channel limits, singularity,
  exact attenuation curves, hardware pitch tables и bit-exact mixer.
- Automap.
- Новые powerups: invulnerability, partial invisibility, computer map и light
  amplification.
- Flying monsters и vertical monster navigation.
- Doom II roster, weapons, maps и specials.
- Общая PWAD/mod compatibility, DeHackEd/BEX и произвольные custom state tables.
- Episode finale/cast/ENDOOM и полная E1 finale sequence.
- Новые authored textures, sprites, sounds, models или replacement assets.
- Полный demo-sync/source-port compatibility.

## Ключевые решения

1. **Stage 9 ограничен E1.** Таблицы могут быть расширяемыми, но verification и
   обязательная реализация основаны на E1M1–E1M9.
2. **Presentation не становится save state без необходимости.** Scroll и item
   animation выводятся из `GameTic`; transient weapon lowering не сериализуется.
3. **Crusher state — authoritative.** Его нельзя восстановить только по текущей
   высоте потолка, поэтому schema повышается до v6.
4. **Один damage path.** Crush и overkill не обходят PlayerHealth/EnemyHealth,
   статистику, drops и barrel behavior.
5. **Queued switch использует уже существующий contract.** Pending weapon уже
   присутствует в model/save; Stage 9 подключает runtime boundary.
6. **Audio RNG отделён от gameplay RNG.** Косметический pitch не может менять
   исход боя или ломать deterministic gameplay tests.
7. **Pre-warm имеет один источник истины.** Runtime feature не может тихо
   добавить SFX, которого нет в startup catalog.
8. **Оба graphics profiles получают одинаковый gameplay.** Classic/Enhanced
   различаются материалами, не scroll/crusher/weapon/pickup/death semantics.

## Архитектура

### Pure assemblies

- `Doom.Specials`: crusher/scroll definitions, target/state transition rules,
  crush cadence.
- `Doom.Game`: save v6 DTO/codec validation, queued-switch boundary policy,
  overkill decision и SFX playback priority/pitch rules.
- `Doom.Things`: pickup sequences, E1 XDEATH sequences и optional frame data.

Pure assemblies не знают `MonoBehaviour`, `AudioSource`, `Material`,
`Physics` или `Time`.

### `Doom.MapBuild`

- `WallScrollSystem` регистрирует bindings конкретных linedef sides и применяет
  tic-derived UV offset к Classic/Enhanced renderers.
- `CrusherMover` либо расширенный `SectorMover` исполняет crusher state machine;
  `CrushDamageSystem` собирает victims в секторе и отправляет bounded pulses.
- `WorldSnapshotCapture/Restore` round-trip'ят v6 crusher state до resume
  simulation.
- `PlayerWeapons` принимает/применяет pending switch; `WeaponView` визуализирует
  lowering.
- `PickupAnimator` меняет только billboard frame.
- `MonsterController` выбирает normal/XDEATH и завершает common death lifecycle.
- `SoundSystem` хранит per-source priority/start sequence/pitch, а
  `SoundCache`/startup catalog гарантируют pre-warm.

## Тестирование

### EditMode

- Scroll direction, tic-to-UV conversion, wrap и side isolation.
- Crusher targets, direction/reversal/stop/restart и 4-tic damage cadence.
- Save v6 round-trip, invalid enum/count rejection и настоящая decode
  compatibility fixtures v1–v5.
- Queued-switch replace/apply/invalid-clear policy.
- Pickup sequence cadence/fallback и Freedoom frame resolution.
- XDEATH threshold, sequence/fallback и one-kill/one-drop rules.
- SFX priority ordering, deterministic tie-break, bounded pitch и catalog
  completeness; audio RNG не меняет gameplay RNG.

### PlayMode

- Scroll 48/85 движется в правильные стороны в Classic и Enhanced, без material
  или mesh growth.
- Crushers двигаются, наносят player/monster damage с правильной cadence,
  останавливаются/retrigger'ятся и не ломают lift riding.
- Save/load mid-crusher продолжает ту же phase/direction; v1–v5 load smoke
  остаётся зелёным.
- Mid-fire slot press применяется после action; death опускает и скрывает
  weapon view; respawn возвращает его.
- Animated pickups сохраняют collection, stats, drop и save semantics.
- Overkill даёт XDEATH, обычный damage — normal death, missing frames —
  fallback.
- Saturated SFX pool соблюдает priority, pitch reset и loop protection; после
  WAD close нет uncached read.

### Full, build и интерактивно

- Полные EditMode и PlayMode suites запускаются с новыми XML/log; baseline
  459/95 не объявляется новым PASS.
- `E1MapSmokePlayTests` строит E1M1–E1M9 в Classic и Enhanced без unknown
  progression blockers, exceptions и runtime cache misses.
- Windows standalone собирается существующим build tool; WAD и все нужные SFX/
  sprite frames входят в runtime path.
- Интерактивно проверить реальные E1 scrolling walls, synthetic crusher fixture,
  queued switch, death lower, animated pickups, normal/XDEATH и SFX saturation
  на E1.
- Save gate: current v6 mid-crusher round-trip плюс decode fixtures v1–v5.
- Resource gate: повторные map reload/profile switch не увеличивают
  Mesh/Material/AudioClip/AudioSource/component counts после warm-up.

## Критерий готовности

1. Scroll 48/85 работает в обе стороны, tic-deterministic и одинаково в
   Classic/Enhanced.
2. E1 crushers/stop actions исполняются, crush damage bounded и проходит общий
   health/death path.
3. Save schema v6 восстанавливает crusher state; реальные v1–v5 payloads
   продолжают декодироваться.
4. Mid-action weapon selection queue'ится; смерть опускает weapon view без
   изменения loadout/ammo.
5. E1 pickups анимируются WAD frames без runtime allocations/cache misses и без
   изменения pickup/save semantics.
6. E1 overkill выбирает XDEATH с безопасным normal-death fallback.
7. SFX pool применяет bounded pitch/priority и полный catalog pre-warm, не
   затрагивая gameplay RNG.
8. Узкие тесты, полные suites, dual-profile E1 smoke, Windows build и
   интерактивный checklist пройдены после факта.
9. Roadmap, spec, plan и `CLAUDE.md` закрываются только после записи реальных
   totals, build result и sign-off; до этого Stage 9 остаётся in progress.

## Связанные документы

- План реализации:
  `docs/superpowers/plans/2026-07-12-e1-compatibility-polish.md`
- Master roadmap: `docs/doom-unity-remake-plan.md`
- Doors/movers:
  `docs/superpowers/specs/2026-05-31-doors-design.md`
- Weapons:
  `docs/superpowers/specs/2026-07-02-weapons-design.md`
- Monster AI:
  `docs/superpowers/specs/2026-07-03-monster-ai-design.md`
- Pickups:
  `docs/superpowers/specs/2026-07-10-pickups-design.md`
- Sound:
  `docs/superpowers/specs/2026-07-10-sound-design.md`
