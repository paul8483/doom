# Расширение Stage 6c. Plasma Rifle, BFG9000 и ячейки — дизайн

**Дата:** 2026-07-11  
**Статус:** утверждён; реализация начинается после закрытия Stage 7e  
**Базовая линия:** Stage 6a–6f завершён; rocket launcher и chainsaw уже
добавлены расширениями 6c; Stage 7e Task 15 ожидает финальную приёмку  
**Связанный исходный этап:** `docs/superpowers/specs/2026-07-02-weapons-design.md`

## Цель

Закрыть оставшийся арсенал Ultimate DOOM: добавить Plasma Rifle и BFG9000,
общий боезапас Cells, их pickups, WAD-анимации и звук, HUD, межуровневый
carry-over и полное сохранение мира.

Видимая веха: игрок подбирает плазматор 2004, HUD показывает слот 6 и ячейки,
плазменные заряды быстро летят и наносят урон; после подбора BFG 2006 слот 7
тратит 40 ячеек, выпускает большой шар и при его взрыве выполняет vanilla
`A_BFGSpray` из 40 трассеров. Save/load во время полёта любого шара не теряет и
не дублирует будущий урон.

Это **отдельное расширение Stage 6c**, а не скрытая часть Stage 7 и не часть
графического Stage 8. Реализация начинается после закрытия 7e, чтобы не менять
критерий уже начатой финальной E1-приёмки.

## Источник истины

Числа и последовательности берутся из `linuxdoom-1.10`:

- `p_inter.c`: `maxammo`, `clipammo`, `P_GiveAmmo`, `P_GiveWeapon`;
- `p_pspr.c`: `A_FirePlasma`, `A_FireBFG`, `A_BFGSpray`;
- `info.c`: weapon psprite states, `MT_PLASMA`, `MT_BFG`, `MT_EXTRABFG`;
- WAD: реальные lumps Freedoom для sprites и `DS*`.

Нельзя заменять BFG «большой ракетой». У vanilla BFG нет кругового
`P_RadiusAttack`: шар наносит прямой missile damage, а основной room-clearing
эффект создают трассеры от игрока.

## Объём

### Входит

- `AmmoType.Cells`, максимум 300; с backpack — 600.
- Backpack каждый раз даёт один cell clip: +20.
- Energy cell 2047 (`CELL`) даёт +20; cell pack 17 (`CELP`) — +100.
- Plasma Rifle 2004 (`PLAS`) находится в слоте 6, выдаёт 40 cells при обычном
  map pickup и тратит 1 cell на заряд.
- BFG9000 2006 (`BFUG`) находится в слоте 7, выдаёт 40 cells и тратит 40 на
  выстрел.
- Клавиши 6/7, ownership slots в HUD, ready ammo и cells/max-cells counters.
- Projectile collision, direct damage, impact animation и WAD sound.
- Vanilla BFG tracer fan, visual `MT_EXTRABFG` effects и корректный
  `DamageSource.Player()` для wake/infighting/kill stats.
- Автовыбор при пустом боезапасе учитывает BFG только при `cells >= 40` и
  plasma при `cells >= 1`.
- Carry-over между уровнями, full-world save/load и backward-compatible save
  schema v4.
- Классификация 2004/2006/2047/17 в E1 matrix меняется с controlled
  `Unsupported` на `Implemented`.
- EditMode, PlayMode, save/load и интерактивная приёмка.

### Сознательно не входит

- Doom II super shotgun и Doom II-only оружие/боеприпасы.
- Вертикальный autoaim оригинального DOOM. Игрок целится камерой; BFG fan
  сохраняет исходный pitch и разворачивается по yaw.
- Полный общий порт psprite state machine. Для этих двух стволов фиксируются
  нужные fire events и тики поверх существующих `WeaponDef`/`WeaponView`.
- Skill-dependent double ammo (ITYTD/Nightmare), пока в игре нет модели
  сложности. Значения ниже соответствуют обычной сложности.
- DMX pitch shift/channel stealing и sector occlusion.
- Новые authored sprites, textures или sounds: только WAD.
- Изменение Stage 7/Stage 8 scope или заявление о завершённой реализации до
  прохождения всех gates.

## Канонические правила

### Cells и pickups

`p_inter.c` задаёт `maxammo[am_cell] = 300` и `clipammo[am_cell] = 20`.

| Объект | DoomEdNum | Результат на обычной сложности |
|---|---:|---:|
| Energy cell | 2047 | +20 cells |
| Cell pack | 17 | +100 cells |
| Plasma Rifle | 2004 | оружие +40 cells |
| BFG9000 | 2006 | оружие +40 cells |
| Backpack | 8 | max 300→600 и +20 cells |

Повторный weapon pickup принимается, только если добавились cells; при уже
имеющемся оружии и полном ammo предмет остаётся. Новое оружие подбирается даже
при полном ammo и становится текущим через существующую pending/select
семантику.

### Plasma Rifle

- Slot 6, `AmmoPerShot = 1`.
- Fire sound: `DSPLASMA`; impact sound: `DSFIRXPL`.
- Viewmodel: `PLSG`; две flash-вариации `PLSF`, выбранные через
  `P_Random() & 1`.
- Projectile: `PLSS` A/B по 6 тиков, скорость 25 DOOM units/tic, radius 13,
  height 8.
- Direct damage: `5 × (P_Random() % 8 + 1)` = 5..40.
- Impact: `PLSE` A–E, тики 4/4/4/4/4; без radius splash.
- `A_FirePlasma` происходит на fire state длительностью 3 тика. Следующий
  `A_ReFire` state имеет 20 тиков только при завершении очереди; при удержании
  огня state machine запускает новый выстрел с plasma cadence. Реализация не
  должна ошибочно превращать это в один заряд каждые 23 тика.

### BFG9000

- Slot 7, `AmmoPerShot = 40`.
- Fire sound: `DSBFG`; impact sound соответствует WAD `DSRXPLOD`.
- Viewmodel: `BFGG`; flash: `BFGF`.
- Fire states: 20 тиков `A_BFGsound`, 10 тиков charge/flash, на следующем
  10-тиковом state выполняются расход 40 cells и spawn шара; затем 20 тиков
  `A_ReFire`.
- Projectile: `BFS1` A/B, скорость 25 DOOM units/tic, radius 13, height 8.
- Direct impact damage: `100 × (P_Random() % 8 + 1)` = 100..800 одной цели.
- Impact animation: `BFE1` A–F по 8 тиков. `A_BFGSpray` выполняется при входе
  в третий impact frame, то есть после первых 16 тиков explosion.
- Generic rocket/barrel radius damage **не вызывается**.

`A_BFGSpray`:

1. Берёт текущую позицию игрока-владельца в момент spray, но исходное
   направление сохранено шаром с момента выстрела.
2. Строит 40 лучей на 1024 DOOM units в yaw fan от −45° с шагом 90°/40
   (`−45° .. +42.75°`), сохраняя исходный pitch свободного прицела.
3. Каждый луч повреждает первую живую shootable-цель.
4. Урон одного попадания — сумма 15 бросков
   `(P_Random() & 7) + 1`; алгоритм фиксируется буквально, без замены одним
   равномерным броском.
5. На цели создаётся visual-only `BFE2` A–D по 8 тиков.
6. Стены прекращают конкретный луч; мёртвая/уже не shootable цель не должна
   поглощать последующие лучи.

Поворот игрока после выстрела не меняет fan direction; перемещение меняет
origin spray. Если owner отсутствует после restore, snapshot считается
некорректным: нельзя тихо породить BFG с другой семантикой.

## Архитектура

### `Doom.Game` — pure-C# rules

- `AmmoType.Cells`; cells storage/max/backpack/capture/restore в `AmmoModel`.
- `WeaponId.PlasmaRifle`, `WeaponId.Bfg9000`.
- Новые `WeaponDef` в `WeaponTable`, включая slot/ammo/timing/sprites/sounds.
- `WeaponLoadout.BestAvailable` проверяет реальный `AmmoPerShot`; порядок:
  BFG → plasma → rocket → chaingun → shotgun → pistol → chainsaw/fist.
- `PlasmaRules`: snapshot type, sprites, speed/size, damage roll,
  fly/explosion frames/tics и sounds.
- `BfgRules`: projectile constants, direct roll, tracer count/range/fan,
  `RollTracerDamage`, frames/tics и момент spray.
- `BfgTracerRules` возвращает чистые `(yawOffset, damage)` данные для 40
  трассеров. Physics и поиск целей остаются в Unity glue.
- `ItemRules`, `PickupSoundTable`, `HudModel`, carry/save DTO расширяются
  новыми authoritative fields.

`Doom.Game` не зависит от `UnityEngine`; направления в pure rules выражаются
угловыми смещениями и числами, не `Vector3`.

### `Doom.MapBuild` — runtime

- `PlayerWeapons` получает bindings 6/7 и отдельные projectile fire paths.
  Расход cells происходит в fire event: для BFG не в момент начала 30-тикового
  charge. Если charge начат, 40 cells резервируются/проверяются так, чтобы
  переключение/save/load не давали бесплатный или двойной выстрел.
- `PlayerPlasmaProjectile` использует swept sphere cast, исключает owner,
  наносит только direct damage и проигрывает `PLSE`.
- `PlayerBfgProjectile` хранит owner, исходный shot direction, phase,
  animation frame/timer и флаг `sprayApplied`; на нужном frame вызывает
  `BfgSprayExecutor`.
- `BfgSprayExecutor` raycast'ит 40 направлений из текущей позиции камеры/игрока,
  применяет `DamageSource.Player()` и создаёт `BFE2` эффекты.
- `WeaponView` остаётся data-driven; ему нужен только корректный момент события
  и WAD prewarm.
- `MapLoader` прогревает `PLSG/PLSF/PLSS/PLSE`,
  `BFGG/BFGF/BFS1/BFE1/BFE2` и все нужные `DS*` до закрытия WAD.
- `ThingSpawner` автоматически вешает `ThingPickup` через расширенный
  `ItemRules.IsPickup`.

Общий projectile helper допускается только если не смешивает различающиеся
правила rocket/plasma/BFG и не раздувает эту задачу рефакторингом всех missiles.

## Input, HUD и звук

- `PlayerWeapons` создаёт actions для `<Keyboard>/6` и `<Keyboard>/7`.
- Slot 6 выбирает Plasma Rifle, slot 7 — BFG9000.
- `HudModel` больше не подставляет cells/maxCells = 0; значения идут из
  `AmmoModel`.
- `DoomHud.DrawArms` показывает ownership цифр 6/7.
- Ready ammo автоматически показывает cells текущего energy weapon.
- Pickups 2004/2006 используют `DSWPNUP`; 2047/17 — `DSITEMUP`.
- `PlayerSoundController` использует `WeaponDef.FireSound`; impact sounds
  запускают projectile components.
- Каждый plasma/BFG shot публикует существующий noise event и будит монстров.

## Carry-over и save schema v4

### Player state

`PlayerCarryState` и `PlayerSnapshot` получают:

- `Cells`;
- `OwnsPlasmaRifle`;
- `OwnsBfg9000`;
- поддержку Plasma/BFG в current/pending weapon validation.

`SaveGame.SchemaVersion` становится 4. В конец player payload после v3
append-only добавляются:

1. `Cells` (`Int32`);
2. `OwnsPlasmaRifle` (`Boolean`, 1 byte);
3. `OwnsBfg9000` (`Boolean`, 1 byte).

v1–v3 читаются с defaults `0/false/false`; повторное сохранение пишет v4.
Будущая версия по-прежнему отвергается. Existing v1/v2/v3 compatibility tests
не удаляются.

### Projectile state

Plasma и BFG шары регистрируются как runtime entities и реализуют
`IProjectileSnapshotSource`.

Текущих position/velocity/remaining-life недостаточно для BFG, потому что
spray — будущий authoritative damage. В v4 `ProjectileSnapshot` и codec
получают version-gated поля:

- projectile phase (`Flying`/`Exploding`);
- animation frame index и remaining frame time;
- исходный shot yaw/pitch или нормализованное direction;
- `SprayApplied`.

`TryReadWorld` принимает schema version. Для v1–v3 отсутствующие поля получают
defaults, полностью сохраняющие старые rocket/monster projectile semantics.
Unknown projectile type не должен молча исчезать при restore: v4 restore
возвращает controlled error до resume.

Сохранение до BFG spray восстанавливает pending spray ровно один раз;
сохранение после spray не повторяет урон. Visual-only `BFE2` tracer effects
можно не сохранять.

## Краевые решения

- BFG не начинает charge при `cells < 40`; выбирается лучшее доступное оружие.
- Plasma прекращает очередь на последней доступной ячейке и выполняет обычный
  auto-downgrade.
- Pickup на полном ammo остаётся; новое оружие всё равно принимается.
- Projectile, появившийся внутри препятствия, обрабатывает impact один раз без
  туннелирования и повторного damage.
- Прямой hit и BFG spray могут убить одну цель в разные моменты; dead-check
  каждого tracer позволяет следующим лучам пройти к следующей цели.
- BFG direct hit не наносит self-damage и не имеет кругового splash.
- Save запрещён только во время уже существующего capture barrier; charge,
  flight и pre-spray explosion являются обычным authoritative state и должны
  сохраняться.
- Death/pause отключают input; уже выпущенные projectiles замораживаются общей
  `timeScale` семантикой.

## Тестирование

### EditMode

- `AmmoModelTests` / `AmmoModelBackpackTests`: 300/600, pickup clamp,
  backpack +20, consume 1/40, capture/restore.
- `WeaponTableTests`: slots 6/7, costs, frames/tics, sounds.
- `WeaponLoadoutTests`: ownership/current/pending и ammo-aware order, BFG не
  выбирается при 39 cells.
- `PlasmaRulesTests`: speed/size, damage set 5..40, frames/tics.
- `BfgRulesTests`: 40 offsets, first/last angle, 1024 range, literal 15-roll
  damage, deterministic RNG.
- `ItemRulesTests`: 2004/2006/2047/17, full-ammo rejection, weapon acceptance.
- `HudModelTests`: cells/max cells и ownership 6/7 вместо старого unsupported
  контракта.
- `SnapshotModelTests` / `SessionStateTests`: cells и оба оружия round-trip.
- `SaveGameCodecTests`: v4 round-trip; v1/v2/v3 defaults; malformed projectile
  phase/direction rejected before allocation/restore.
- `SpriteSetFreedoomTests`: все перечисленные viewmodel/projectile/effect
  frames реально разрешаются в `freedoom1.wad`.
- `E1CompatibilityTests`: четыре thing type имеют `Implemented`.

### PlayMode

- Plasma pickup даёт оружие/+40; cell/cell pack дают +20/+100.
- Plasma projectile наносит deterministic direct damage, тратит 1 cell,
  взрывается на стене и не создаёт radius damage.
- BFG не стреляет при 39 cells; при 40 тратит ровно 40 после charge.
- BFG direct hit + spray: ровно 40 ray attempts, ближайшие цели в fan получают
  deterministic damage, цели вне fan/за стеной — нет.
- Перемещение игрока до impact меняет spray origin; поворот не меняет исходное
  направление.
- Save/load plasma в полёте сохраняет trajectory/damage.
- Save/load BFG до и после spray доказывает exactly-once damage.
- Межуровневый transition переносит cells/ownership/current weapon.
- Existing rocket, monster projectile, weapon, pickup, HUD и save tests не
  регрессируют.

### Интерактивно

- Проверить pickup/высоту plasma на карте, где был обнаружен исходный дефект.
- Проверить непрерывность plasma cadence, две flash-вариации и звук.
- Проверить BFG charge, launch, impact, tracer effects и room-clearing.
- HUD 4:3/16:9: slots 6/7, ready ammo и cells/max cells читаемы.
- Save/load во время plasma flight, BFG charge, BFG flight и pre-spray
  explosion.

## Критерий готовности

1. Plasma Rifle, BFG9000 и все cell pickups работают на E1-картах.
2. BFG реализует шар + direct hit + 40 vanilla tracers, без rocket splash.
3. HUD/input/sound используют WAD и существующие runtime systems.
4. Carry и save schema v4 сохраняют ammo, ownership и active projectiles;
   v1–v3 продолжают загружаться.
5. E1 matrix больше не отмечает 2004/2006/2047/17 unsupported.
6. Узкие и полные EditMode/PlayMode suites зелёные.
7. Интерактивная приёмка подписана; только после неё master status и
   `CLAUDE.md` получают отметку «реализовано».

## Связанные документы

- Исходный weapons design:
  `docs/superpowers/specs/2026-07-02-weapons-design.md`
- Pickups design:
  `docs/superpowers/specs/2026-07-10-pickups-design.md`
- Stage 7 design:
  `docs/superpowers/specs/2026-07-10-polish-design.md`
- План реализации:
  `docs/superpowers/plans/2026-07-11-plasma-bfg.md`
- Master roadmap: `docs/doom-unity-remake-plan.md`
