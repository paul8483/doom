# Этап 7. Шлифовка и расширение — дизайн

**Дата:** 2026-07-10
**Статус:** утверждён; **7a ✅, 7b ✅, 7c ✅, 7d ✅, 7e Task 14 ✅**; следующий — Task 15
**Предыдущие этапы:** 0–6 завершены, включая 6a–6f
**Контекст:** финальный этап master roadmap. Одна umbrella-спека разбита на
подэтапы 7a–7e: game flow → HUD → меню → сохранения → совместимость,
оптимизация и build.

## Цель

Превратить работающий E1M1 sandbox в законченную WAD-driven игру: игрок
начинает новую кампанию, проходит весь первый эпизод Freedoom, видит
DOOM-подобный HUD и intermission, может поставить игру на паузу, изменить
настройки, сохранить полный мир и продолжить его после перезапуска приложения.

Видимая веха: standalone Windows build запускается без Unity Editor; E1M1
заканчивается штатным exit switch, intermission переводит игрока в E1M2 с
сохранённым инвентарём, secret exit ведёт в E1M9 и возвращает в основной
маршрут. HUD, меню и face animation состоят только из WAD patches. Save/load
возвращает не только HP игрока, но и убитых монстров, подобранные предметы,
положение движущихся секторов и состояние одноразовых линий.

Гарантированный набор карт — `E1M1`–`E1M9` из
`Assets/StreamingAssets/wads/freedoom1.wad`. Остальные эпизоды проходят
автоматический smoke-test загрузки, но не входят в критерий полной
проходимости Stage 7.

## Объём этапа

### 7a. Game session и переходы уровней

- Pure-C# `CampaignRoute` валидирует имена `ExMy`, перечисляет доступные карты
  WAD и определяет normal/secret next map.
- Persistent `GameSession` хранит текущую карту, межуровневый snapshot игрока,
  статистику уровня и причину перехода.
- Линии `Exit` типов 11/51/52/124 становятся исполняемыми. Switch exit
  проигрывает доступный WAD SFX, блокирует управление и запускает intermission;
  walk exit использует тот же путь без дублирования логики.
- Sector special 11 сохраняет существующий урон и завершает уровень, когда
  здоровье игрока опускается до 10 или ниже.
- Переход перезагружает `Stage2_MapPreview` и передаёт следующую карту через
  session. Полная перезагрузка сцены является штатным teardown: объекты,
  runtime meshes, movers, audio и caches старой карты уничтожаются Unity.
- При normal exit инвентарь переносится в следующую карту; ключи и временные
  powers сбрасываются. New Game и restart after death начинают карту с
  определённым стартовым состоянием.
- E1M8 завершает кампанию и возвращает в main menu/episode-complete экран;
  E1M9 возвращает в маршрут эпизода согласно таблице, а не по арифметике
  `map + 1`.

### 7b. DOOM HUD и intermission

- Один WAD-only UI renderer работает в виртуальном пространстве 320×200 и
  использует nearest-neighbour scaling с letterbox/pillarbox.
- `STBAR`, цифровые glyph patches, key icons, weapon ownership и `STF*` face
  загружаются из WAD, пока `WadFile` открыт, затем живут в отдельном UI cache.
- Status bar показывает health, armor, ready ammo, четыре ammo counters,
  оружие, ключи и лицо. Отсутствующий необязательный patch имеет безопасный
  fallback; отсутствие базового `STBAR` отключает только декоративный фон, но
  не критические числа.
- `HudModel` — pure-C# projection текущих моделей игрока. Renderer не вычисляет
  gameplay rules и не ищет объекты сцены каждый кадр.
- Face controller реагирует на damage, pickup, rapid damage, низкое здоровье,
  смерть и короткий idle look. Приоритеты и длительности фиксируются тестами;
  renderer только выбирает уже рассчитанное имя patch.
- Существующий weapon view остаётся в верхних 168 строках виртуального экрана,
  но общий patch/layout helper устраняет дублирование формулы
  `R_DrawPSprite`.
- Intermission показывает завершённую/следующую карту, kills/items/secrets и
  время. Для Stage 7 достаточно статического WAD-only экрана с подтверждением;
  точная покадровая анимация оригинала не обязательна.
- Debug-текст `PlayerHud` удаляется или превращается в новый renderer; второй
  параллельный HUD не остаётся.

### 7c. Меню и настройки

- Main menu: New Game, Load Game, Options, Quit. Pause menu: Resume, Save Game,
  Load Game, Options, End Game (`M_ENDGAM` → Quit to Main). Resume has no IWAD
  patch and uses system-text fallback; other pause rows use WAD menu patches.
- `Escape` открывает pause menu только во время уровня. Gameplay input maps
  выключаются, курсор освобождается, `Time.timeScale` становится 0. Закрытие
  меню восстанавливает прежнее состояние ровно один раз.
- Смерть, intermission и pause являются взаимоисключающими UI states,
  управляемыми одним `GameFlowController`; компоненты не соревнуются за cursor,
  timescale и enable/disable игрока.
- Настройки Stage 7: SFX volume, music volume, mouse sensitivity, invert Y,
  fullscreen и разрешение. Значения применяются сразу и сохраняются отдельно
  от savegames.
- `SoundSystem` и `MusicPlayer` получают runtime volume/pause API. Пауза музыки
  не перезапускает sequencer; UI SFX может проигрываться при `timeScale == 0`.
- Input rebinding не входит в обязательный объём: текущие hand-built
  `InputActionMap` не имеют стабильных action IDs и control-scheme asset.
  Экран Controls показывает действующие клавиши. Полный interactive rebinding
  отложен до перехода на `.inputactions`.
- UI использует WAD menu patches, где они доступны, и системный текст только
  для технических сообщений (ошибка save, несовместимая версия). Новых
  изображений или аудиоклипов в `Assets` не создаётся.

### 7d. Полный snapshot мира

- Формат — versioned binary envelope с magic, schema version, WAD identity,
  map name, payload length и checksum. Кодек находится в pure-C# слое и
  использует явный little-endian порядок; Unity serialization не является
  контрактом save format.
- Unity storage пишет временный файл в `Application.persistentDataPath`, flush
  выполняется до атомарной замены целевого slot. Неудачная запись не уничтожает
  последний валидный save.
- Save разрешён в стабильном gameplay state после остановки simulation на
  границе тика. Не сохраняются main menu, intermission, процесс scene loading
  и уже мёртвый player.
- Snapshot содержит:
  - schema/WAD/map/session metadata и текущий gameplay tic;
  - transform игрока, view yaw/pitch, HP/armor/type;
  - ammo/max-ammo/backpack, owned/current/pending weapon, keys и powers;
  - состояние `DoomRandom` всех сохраняемых владельцев;
  - каждый map thing по стабильному исходному thing index: present/picked,
    transform, health, FSM/frame, target thing index и flags;
  - активные projectiles: type, owner, transform, velocity и remaining life;
  - floor/ceiling heights всех секторов и активный mover с phase, direction,
    target, speed и wait timer;
  - one-shot/repeatable line activation state и switch texture state;
  - kills/items/secrets/time для intermission.
- Stable IDs происходят только из map indices и snapshot-local spawned IDs.
  `GetInstanceID`, имена GameObject и порядок `FindObjectsByType` в save не
  записываются.
- Load сначала проверяет envelope, WAD identity, наличие map marker и bounds
  всех массивов. Затем сцена строит исходную карту, после чего restore
  применяется фазами: sectors/lines → things → references/targets/projectiles
  → player → simulation resume.
- Неизвестная будущая версия, повреждённый checksum или другой WAD отклоняются
  понятным сообщением и не меняют текущую игру. Миграция schema v1 не нужна;
  контракт должен позволять добавить migrator позже.
- Аудиоканалы, частицы, hit effects, UI timers и decoded texture/audio caches
  не сохраняются. Они безопасно пересоздаются из authoritative snapshot.

### 7e. Совместимость E1, оптимизация и player build

- До реализации новых specials автоматически строится compatibility matrix:
  все linedef/sector/thing types на E1M1–E1M9 и их статус
  `implemented / harmless visual / progression blocker / unsupported`.
- Обязательны exit/secret exit и teleport player/monster. Crusher, lighting,
  scrolling и прочие распознанные категории реализуются в точном объёме,
  который встречается в E1; progression blocker не может оставаться no-op.
- Телепорт выбирает destination thing type 14 по sector tag, сохраняет
  ориентацию destination, обнуляет momentum и использует telefrag/occupancy
  policy, зафиксированную тестами.
- Crusher циклически двигает ceiling и наносит crush damage без создания нового
  mesh каждый кадр. Light specials меняют runtime sector light и material
  properties без декодирования новых textures. Scroll изменяет material UV
  offset, не перестраивает геометрию.
- Автоматический smoke-test строит каждую E1 карту, проверяет конечность meshes,
  player start, отсутствие неизвестных progression blockers и корректный
  normal/secret route. Интерактивный playthrough остаётся обязательным для
  утверждения фактической проходимости.
- Оптимизация начинается с baseline, а не с предположений. Измеряются время
  build, managed allocations, количество meshes/materials/textures/colliders и
  worst-frame runtime на E1M1, E1M3, E1M7 и E1M9.
- Приоритетные кандидаты после измерения: spatial index для walk triggers,
  кэш line references вместо `FindObjectsByType`, collider simplification,
  material reuse, patch/texture negative cache и устранение per-frame GUI
  allocations. Texture atlas или объединение sector meshes допускаются только
  если не ломают UV animation, sector movers и save restore.
- Performance acceptance на целевой машине фиксируется baseline Task:
  regression не хуже baseline; затем устанавливаются измеримые budgets для
  build time, peak managed memory и 95th-percentile frame time. Произвольные
  числа до профилирования не объявляются критерием.
- Создаётся воспроизводимый Windows standalone build с
  `Stage2_MapPreview.unity` и `freedoom1.wad` в StreamingAssets. Build
  стартует в main menu, а не сразу в E1M1.

## Сознательно отложено

- PWAD merge поверх IWAD, DEH/BEX, Doom II `MAPxx` campaign routing.
- Automap, multiplayer/networking, demos и deterministic net sync.
- Полный набор оружия/боеприпасов и монстров Doom II; chainsaw, rocket,
  plasma/BFG и flying monsters не становятся скрытой частью Stage 7, если
  compatibility audit не докажет, что без них E1 непроходим.
- Invulnerability/invisibility/computer-map/light-amp pickups и их visual FX.
- Точная анимация intermission, finale/cast screen и текстовые story screens.
- Полная эмуляция DMX channel stealing, pitch shift, sector audio occlusion.
- Универсальные saves между разными WAD и миграция будущих schema versions.
- Полный keyboard/gamepad rebind UI до перехода на Input Actions asset.
- AI-upscale или любые authored replacement textures, sprites и sounds.

## Ключевые решения

1. **Одна umbrella-веха, пять последовательных подэтапов.** Stage 7 слишком
   широк для одного неделимого изменения; 7a–7e дают видимый результат и
   отдельный regression gate, сохраняя одну spec/plan пару.
2. **Scene reload вместо ручного teardown.** Текущий `MapLoader.Build()` —
   one-shot pipeline без симметричного destroy. Reload preview scene проще,
   безопаснее для meshes/audio/coroutines и уже поддержан auto-bootstrap.
3. **Persistent session хранит данные, не Unity objects.** Между сценами
   переживает только DTO/state service; ссылки на `MonoBehaviour`, Mesh,
   Material и AudioSource никогда не переходят границу уровня.
4. **Полный snapshot, а не checkpoint.** Выбранная семантика save/load
   восстанавливает mutable world. Перезагрузка чистой карты с одним player
   snapshot была бы заметно иной функцией и не называется полным сохранением.
5. **Binary contract вместо `JsonUtility`.** Явные primitive fields дают
   bounds checks, versioning, checksum и pure-C# тесты; private runtime fields
   не зависят от Unity field serialization.
6. **Build base map, затем restore.** Snapshot хранит delta/state, а не meshes
   и decoded WAD assets. WAD остаётся источником статической геометрии и
   контента.
7. **WAD patches остаются UI-источником.** Общая виртуальная 320×200 система
   согласуется с `WeaponView` и исключает handcrafted UI art. Отдельный uGUI
   asset hierarchy не нужен для Stage 7.
8. **Pure rules отделены от Unity glue.** Campaign routing, HUD projection,
   save DTO/codec и settings validation тестируются без сцены; rendering,
   storage, pause и restore adapters живут в `Doom.MapBuild`.
9. **E1 compatibility определяется данными.** Не переносим весь special table
   оригинального движка «на всякий случай»; audit E1 задаёт обязательный
   runtime subset, но ни один blocker не маскируется warning-логом.
10. **Профилируем перед оптимизацией.** Архитектурно рискованные atlas/batching
    изменения принимаются только с измеримым выигрышем и multi-map regression.

## Архитектура

### `Doom.Game` — session, HUD и save contract

Новые pure-C# типы:

- `CampaignRoute` / `CampaignMap` — normal/secret progression E1.
- `SessionState` — current map, transition, carry-over player и level stats.
- `HudModel`, `FaceState`, `FaceRules` — immutable projection для renderer.
- `GameSettingsData` — validated values без `PlayerPrefs`.
- `SaveGame`, `PlayerSnapshot`, `WorldSnapshot`, `ThingSnapshot`,
  `SectorSnapshot`, `LineSnapshot`, `ProjectileSnapshot`.
- `SaveGameCodec` — little-endian binary read/write с лимитами и checksum.

Существующие модели (`HealthModel`, `AmmoModel`, `WeaponLoadout`,
`KeyInventory`, `PlayerPowers`, `DoomRandom`) получают явные
`Capture`/`Restore` либо DTO constructors. Reflection и доступ к private fields
из Unity glue запрещены.

### `Doom.Specials` — runtime-neutral special rules

- Exit kind и route intent отделяются от Unity scene loading.
- Teleport destination selection, crusher targets и light transition rules
  остаются pure-C#.
- Таблица `LineSpecialTable` продолжает быть единственным источником
  классификации; executable означает наличие runtime executor и теста.

### `Doom.Graphics` — UI patches

- UI catalog читает named patches (`ST*`, `M_*`, intermission lumps) через
  существующий `Patch`/`Palette`.
- Decoded UI images не знают о Unity textures.
- Отсутствующие optional lumps представлены явным miss, а не exception из
  render loop.

### `Doom.MapBuild` — Unity orchestration

- `GameSessionHost` (`DontDestroyOnLoad`) и `GameFlowController`.
- `LevelTransitionController` и `IntermissionView`.
- `HudTextureCache`, `DoomHud`, общий `VirtualScreenRenderer`; адаптация
  `WeaponView`.
- `MenuController`, `SettingsStore`, `SaveSlotStore`.
- `WorldStateRegistry`, capture/restore adapters с map-stable IDs.
- Executors для teleport/crusher/light/scroll.
- `MapLoader` принимает session map, регистрирует mutable runtime state,
  применяет pending restore после полного spawn и сообщает `LevelReady`.

## Потоки данных

### Переход уровня

```text
LineActivator / FloorDamageSystem
    -> LevelExitRequested(kind)
    -> GameFlowController freezes gameplay
    -> WorldStats finalized
    -> IntermissionView
    -> GameSession advances CampaignRoute
    -> SceneManager reloads Stage2_MapPreview
    -> MapLoader builds session.CurrentMap
    -> carry-over PlayerSnapshot applied
```

### Save/load

```text
Pause menu Save
    -> simulation stops on stable tick
    -> WorldStateRegistry captures indexed runtime state
    -> SaveGameCodec writes temp + checksum
    -> SaveSlotStore atomically replaces slot

Pause/main menu Load
    -> envelope and WAD identity validated
    -> GameSession stores PendingRestore
    -> scene reload builds static WAD map
    -> sectors/lines -> things/references -> player restore
    -> gameplay resumes
```

### HUD

```text
Player models/events + Session stats
    -> HudModel/FaceRules
    -> DoomHud selects WAD patch names
    -> VirtualScreenRenderer draws 320x200
```

## Стабильная идентичность мира

- Map things получают `MapThingId` из индекса `THINGS`, а не координат.
- Drops/projectiles и другие runtime spawns получают monotonically increasing
  `SpawnId`, сохранённый в session snapshot.
- Sector и linedef IDs равны индексам соответствующих WAD arrays.
- Ссылки monster target/owner сериализуются как typed entity IDs и
  разрешаются только после создания всех entities.
- Удалённый pickup остаётся записью `present = false`; отсутствие записи не
  трактуется как «удалён».
- Save capture сортирует arrays по ID, чтобы бинарный результат и тесты были
  стабильны независимо от Unity enumeration order.

## Краевые решения

- New Game всегда очищает pending save, session stats и static test override.
- Quick restart после смерти не является load: карта строится заново, а
  межуровневый carry-over не применяется.
- Pause не открывается поверх смерти/intermission/loading. Load из pause
  закрывает старую сцену только после успешной валидации файла.
- Secret exit без доступной E1M9 выдаёт controlled fallback на normal route и
  warning; повреждённый route не загружает произвольную карту.
- Save, сделанный на mover wait phase, восстанавливает оставшееся время, а не
  запускает цикл сначала.
- Target на уже мёртвого/отсутствующего monster становится null; invalid
  required owner projectile отклоняет snapshot.
- Settings file/savegame I/O failure отображается в меню и не валит gameplay.
- При отсутствии HUD patch критические значения рисуются fallback glyphs из
  доступного WAD font; authored font texture не добавляется.
- Test hook `MapNameOverride` действует только при отсутствии активной
  production session и очищается teardown теста.

## Тестирование

### EditMode

- `CampaignRouteTests`: normal/secret routes, E1M8 completion, E1M9 return,
  invalid/missing maps.
- `HudModelTests` / `FaceRulesTests`: ammo mapping, keys, HP bands, damage
  priority, dead face и deterministic timers.
- `SaveGameCodecTests`: round-trip полного v1 snapshot, deterministic ordering,
  truncation, bad magic/version/checksum, count/string bounds и wrong WAD.
- Snapshot tests каждого существующего pure model, включая RNG index.
- `E1CompatibilityTests`: все E1 maps load; every progression-relevant special
  classified and executable.
- Teleport/crusher/light pure-rule tests по map indices и edge cases.

### PlayMode

- Exit switch на E1M1 приводит к E1M2 и переносит HP/ammo/weapons, но не keys
  или temporary powers.
- Secret route входит в E1M9 и возвращается в основной episode route.
- Pause выключает движение/use/fire, освобождает cursor и корректно
  восстанавливает music/gameplay.
- Settings применяются runtime и переживают scene/app-like reload.
- Save/load восстанавливает одновременно изменённые HP/ammo, мёртвого monster,
  подобранный item, открытый door, активный mover, fired one-shot line,
  projectile и RNG.
- Corrupt/incompatible save не меняет работающий уровень.
- Teleport и crusher работают на реальных E1 map fixtures.
- `E1MapSmokePlayTests` последовательно строит E1M1–E1M9 без NaN/degenerate
  runtime state и без progression-blocker warnings.

### Интерактивно

- Полное прохождение E1M1–E1M9 в standalone build, включая secret route.
- HUD проверяется при 4:3, 16:9 и resize: pixel aspect/layout стабилен, face и
  counters читаемы, weapon view не перекрывает status bar.
- Save/load проверяется в бою, после pickup, во время door/lift wait и после
  убийства; после перезапуска build мир совпадает.
- Main/pause/options navigation, volume extremes, fullscreen и quit.
- Profiler captures для baseline и финальной версии на выбранных E1 maps.

## Критерий готовности

Stage 7 завершён, когда одновременно выполнено следующее:

1. Windows standalone build стартует в main menu и не требует Unity Editor.
2. Freedoom E1M1–E1M9 полностью проходимы, normal и secret routes корректны.
3. HUD, weapon view, menus и intermission используют WAD data и работают при
   разных aspect ratios.
4. Full-world save/load переживает перезапуск build и восстанавливает весь
   перечисленный authoritative state.
5. Settings сохраняются отдельно и применяются runtime.
6. Compatibility matrix не содержит E1 progression blockers.
7. Установленные после baseline performance budgets соблюдены без регрессий
   geometry, movers, textures и saves.
8. Все существующие и новые EditMode/PlayMode тесты зелёные; интерактивный E1
   checklist подписан.
9. `CLAUDE.md`, master roadmap и implementation plan обновлены фактическими
   test counts, ограничениями и статусом Stage 7.

## Связанные документы

- Master roadmap: `docs/doom-unity-remake-plan.md`
- План реализации (HOW): `docs/superpowers/plans/2026-07-10-polish.md`
- Player/input: `docs/superpowers/specs/2026-05-29-player-design.md`
- Interactive sectors: `docs/superpowers/specs/2026-05-31-doors-design.md`
- Damage/HP: `docs/superpowers/specs/2026-05-31-player-damage-design.md`
- Weapons: `docs/superpowers/specs/2026-07-02-weapons-design.md`
- Pickups: `docs/superpowers/specs/2026-07-10-pickups-design.md`
- Sound/music: `docs/superpowers/specs/2026-07-10-sound-design.md`
