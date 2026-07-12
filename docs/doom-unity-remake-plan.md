# План работ: ремейк DOOM на Unity

## Цель проекта

Создать собственный движок-ремейк оригинального DOOM на Unity и C#. Оригинальный контент (карты, текстуры, спрайты, звуки) берётся из WAD-файлов как источник данных. Вся отрисовка, физика и игровая логика пишутся с нуля средствами Unity. Карты, текстуры и ассеты самостоятельно не разрабатываются.

## Технологический стек

- **Движок:** Unity (последняя LTS-версия)
- **Язык:** C#
- **Источник данных:** WAD-файлы (для разработки и распространения — Freedoom, свободный и совместимый по формату с оригиналом)
- **Контроль версий:** Git

## Принципиальная архитектура

DOOM-уровень — двумерная карта, где высоту задаёт сектор (высота пола и потолка). Из WAD читаются данные геометрии, на их основе генерируются 3D-меши Unity. BSP-данные оригинала (`NODES`, `SSECTORS`, `SEGS`, `BLOCKMAP`) не используются — видимостью и физикой занимается сам Unity.

---

## Этап 0. Подготовка окружения ✅

Настроить рабочее место и убедиться, что инструменты на месте, прежде чем писать код.

- [x] Установить Unity Hub и последнюю LTS-версию Unity. → Unity 6000.4.8f1
- [x] Создать пустой 3D-проект. → в корне репозитория
- [x] Инициализировать Git-репозиторий, добавить `.gitignore` для Unity. → ветка `main`
- [x] Скачать тестовый WAD (Freedoom) и положить в проект. → Freedoom v0.13.0 Phase 1 в `Assets/StreamingAssets/wads/freedoom1.wad`
- [ ] Пройти короткий вводный туториал по Unity, если движок ещё не знаком (сцена, GameObject, компоненты, скрипты). _(опционально)_

**Результат этапа:** готовый проект, открывающийся в Unity, с тестовым WAD на диске.

---

## Этап 1. WAD-ридер ✅

Научиться читать структуру WAD-файла и доставать из него произвольные данные.

- [x] Реализовать чтение заголовка: сигнатура (`IWAD`/`PWAD`), число лампов, смещение таблицы.
- [x] Прочитать таблицу лампов (directory): для каждого лампа — смещение, размер, имя.
- [x] Реализовать метод «достать ламп по имени».
- [x] Все числа читаются как little-endian через `BinaryReader`.

**Результат этапа:** консольный вывод списка всех лампов и карт, содержащихся в WAD. Доступен через меню `Tools > Doom > Dump freedoom1.wad` (на Freedoom v0.13.0: 3163 ламп, 36 карт `E1M1..E4M9`). Дизайн — `docs/superpowers/specs/2026-05-28-wad-reader-design.md`; план реализации — `docs/superpowers/plans/2026-05-28-wad-reader.md`.

---

## Этап 2. Геометрия одной карты (серый блок-аут) ✅

Превратить данные одной карты в 3D-геометрию, по которой можно ходить. Текстуры пока не нужны.

- [x] Распарсить лампы карты: `VERTEXES`, `LINEDEFS`, `SIDEDEFS`, `SECTORS`.
- [x] Собрать контуры секторов и затриангулировать пол и потолок каждого сектора (через LibTessDotNet, undirected half-edge chaining).
- [x] Построить стены: для односторонних линий — квад от пола до потолка; для двусторонних — нижнюю и верхнюю перемычки там, где высоты соседних секторов различаются.
- [x] Сгенерировать меши Unity и разместить на сцене (`Stage2_MapPreview.unity`, runtime через `MapLoader`).

**Результат этапа:** `MapLoader` на сцене `Stage2_MapPreview` строит карту E1M1 (или любой другой ExMy) в Play mode; видна серая геометрия с разделёнными по секторам полом, потолком и стенами. Pipeline покрыт 62 EditMode-тестами, включая интеграцию на freedoom1.wad (≥90% секторов замыкаются). Дизайн — `docs/superpowers/specs/2026-05-28-geometry-design.md`; план реализации — `docs/superpowers/plans/2026-05-28-geometry.md`.

---

## Этап 3. Игрок и коллизии ✅

Дать возможность реально перемещаться по уровню.

- [x] Прочитать стартовую позицию игрока из `THINGS`.
- [x] Настроить управление от первого лица (мышь + клавиши) через новый Input System.
- [x] Использовать `CharacterController` и меш-коллайдеры на геометрии для столкновений.

**Результат этапа:** `MapLoader` автоматически спавнит игрока в Player 1 Start (`Thing.Type == 1`) на сцене `Stage2_MapPreview`; WASD + мышь + Shift для бега. `CharacterController` (1.75 м × 0.5 м) использует `MeshCollider`-ы Stage 2 для столкновений; gravity = -9.81 m/s² работает естественно благодаря `worldScale = 1/32`. Покрыт 71 EditMode + 1 PlayMode тестом. Дизайн — `docs/superpowers/specs/2026-05-29-player-design.md`; план реализации — `docs/superpowers/plans/2026-05-29-player.md`.

---

## Этап 4. Палитра и текстуры ✅

Подтянуть оригинальную графику на геометрию.

- Прочитать палитру `PLAYPAL` (256 цветов).
- Реализовать флэты (текстуры пола/потолка, сырые 64×64 индекса в палитру).
- Реализовать стеновые текстуры: собрать их из патчей по таблицам `TEXTURE1`/`TEXTURE2` и списку `PNAMES`, учесть колоночный формат патчей и прозрачность.
- Прогонять индексные цвета через палитру и формировать `Texture2D`.
- Назначить текстуры на стены, полы и потолки.

**Результат этапа:** уровень E1M1 (и любой ExMy) с оригинальными текстурами — стены (с pegging), полы/потолки (флэты), прозрачные двусторонние решётки и яркость по секторам. Декодинг графики в `Doom.Graphics` (PLAYPAL/PNAMES/TEXTURE1/2/патчи/флэты → RGBA32); Unity-glue (`TextureCache`, Unlit-шейдеры) в `Doom.MapBuild`. Небо (F_SKY1) и анимация отложены. Подробный план реализации — `docs/superpowers/specs/2026-05-30-textures-design.md`.

---

## Этап 5. Объекты и враги (спрайты) ✅

Расставить на уровне персонажей и предметы.

- Прочитать `THINGS` и сопоставить типы объектов с их спрайтами.
- Реализовать спрайты-биллборды (плоскость, всегда повёрнутая к камере).
- Учесть нейминг кадров и углов обзора спрайтов.
- Разместить объекты в нужных координатах.

**Результат этапа:** на E1M1 (и любой ExMy) видны враги, предметы и декорации — спрайты-биллборды с выбором 1 из 8 ротаций по углу обзора, прозрачным фоном (alpha-cutout) и привязкой к полу/потолку; твёрдые объекты (флаг SOLID) блокируют игрока. Таблица типов `mobjinfo` перенесена в `Doom.Things`, каталог спрайтов `S_START/S_END` — в `Doom.Graphics`, биллборд/спавн — в `Doom.MapBuild`. Анимация кадров, ИИ и подбор предметов отложены на Этап 6. Дизайн — `docs/superpowers/specs/2026-05-30-sprites-design.md`.

---

## Этап 6. Игровая логика

Превратить «прогулку» в игру. Реализуется поэтапно, каждый пункт — отдельная веха. Этап разбит на под-этапы 6a–6f; **6a–6f завершены**. Stage 7: **7a–7e ✅**.

- Здоровье, броня, урон. ✅ (под-этап 6b)
- Оружие и стрельба. ✅ (под-этап 6c)
- ИИ врагов (преследование, атака, смерть). ✅ (под-этап 6d)
- Подбираемые предметы (аптечки, патроны, ключи). ✅ (под-этап 6e)
- Поведение секторов: двери, лифты, движущиеся платформы. ✅ (под-этап 6a)
- Звук: эффекты и музыка из WAD ✅ (под-этап 6f).
- Ракетница ✅ (расширение 6c): слот 5, Rockets, splash, save schema v2.
- Chainsaw ✅ (расширение 6c): melee, save schema v3.
- Plasma Rifle + BFG9000 + Cells ✅ (расширение 6c): slots 6/7, save schema v4.
  Дизайн — `docs/superpowers/specs/2026-07-11-plasma-bfg-design.md`.

**Под-этап 6a (двери и интерактивные секторы) ✅.** Перенесена таблица типов линий DOOM в новую чистую C#-сборку `Doom.Specials` (`LineSpecialTable`, `SectorActions`, `Neighbors`); `Doom.Map` получил `ISectorHeights` и перестроение геометрии сектора в рантайме по высотам пола/потолка; `Doom.MapBuild` — `RuntimeSectorHeights`, `SectorGeometry` (перестроение меша/коллайдера на месте), `SectorMover` (анимация высот) и `LineActivator` (триггеры Use/Walk/Switch) плюс ввод «Use» (клавиша E). Двери и лифты открываются/двигаются. Отложено: проверка ключей (запертые двери пока открываются), выходы с уровня, давильни (crusher), свет, телепорты, скроллинг текстур, звук, повторный запуск отработавшего мувера. Дизайн — `docs/superpowers/specs/2026-05-31-doors-design.md`; план реализации — `docs/superpowers/plans/2026-05-31-doors.md`.

**Под-этап 6b (урон игроку и HP) ✅.** Новая чистая C#-сборка `Doom.Game` (`HealthModel` — HP и броня с DOOM-поглощением: зелёная броня впитывает 1/3 урона, синяя 1/2); `SectorDamageTable` в `Doom.Specials` (урон-полы: нукаж 5, слизь 10, лава 20 HP за тик); компоненты `Doom.MapBuild` — `SectorRef` (индекс сектора на GO пола), `PlayerHealth` (обёртка модели + событие `Died`), `FloorDamageSystem` (периодический урон от пола, пока «ноги на полу», каждые ~0.9с, через луч вниз → `SectorRef`), `PlayerDeathHandler` (блокировка управления + оверлей «You died» + респавн по R), `PlayerHud` (отладочный показ HP/брони через OnGUI); всё подключено в `MapLoader.SpawnPlayer`. Отложено: радиокостюм, выход с уровня по спецэффекту 11, секретные секторы (9), подбор брони/аптечек, краш-урон, полноценный HUD. Дизайн — `docs/superpowers/specs/2026-05-31-player-damage-design.md`; план реализации — `docs/superpowers/plans/2026-05-31-player-damage.md`.

**Под-этап 6c (оружие и стрельба) ✅.** Hitscan (fist/pistol/shotgun/chaingun) + rocket + chainsaw + plasma/BFG. Чистые правила в `Doom.Game`; Unity-глю в `Doom.MapBuild`. Бочки (2035): HP 20, BEXP splash. Дизайн — `docs/superpowers/specs/2026-07-02-weapons-design.md`; plasma/BFG — `2026-07-11-plasma-bfg`.

**Под-этап 6d (ИИ врагов) ✅.** Четыре монстра E1 (POSS/SPOS/TROO/SARG) просыпаются от взгляда/урона/шума выстрела, преследуют DOOM-походкой, открывают двери, атакуют (hitscan/укус/фаербол), дерутся между собой и умирают с анимацией. FSM в `Doom.Game` (`MonsterBrain`, `MonsterRules`, `ChaseDir`), данные в `Doom.Things` (`MonsterDef`/`MonsterTable`), заливка шума `NoiseAlert` в `Doom.Specials`. Unity-глю: `SpriteBillboard.SetFrame`, `Projectile` (фаербол импа), `MonsterController` (движение, зрение, атаки, двери), `DamageSource`+infighting в `EnemyHealth`, навеска в `ThingSpawner`, `NoiseAlertSystem` (выстрелы будят сектор). **193 EditMode + 20 PlayMode** (включая 5× `MonsterAiPlayTests`, Task 11, commit `14ae50f`). **Facing fix (2026-07-10):** в `SpriteBillboard` оффсет ротации был `+202.5°` вместо дизайн-дока `+22.5°` — монстр лицом к камере всегда получал rot 4 (спина) вместо rot 0 (лицо); особенно заметно на hitscan POSS/SPOS. Исправлено на `+22.5°`; `Face()` каждый кадр в Chase/Attack/Pain. Визуальная приёмка (Task 12) подтверждена интерактивно на E1M1. Отложено: XDEATH, летающие монстры, звук (6f), A_Look chain-wake, ML_SOUNDBLOCK. Дизайн — `docs/superpowers/specs/2026-07-03-monster-ai-design.md`; план — `docs/superpowers/plans/2026-07-03-monster-ai.md`.

**Под-этап 6e (подбираемые предметы) ✅.** Полный E1-набор через чистый роутер `ItemRules`: здоровье (stim/medi/bonus/soul), броня (green/blue/bonus), шесть ключей, рюкзак (×2 max + clip-грант), берсерк (heal-to-100, кулак ×10), радиокостюм (2100 тиков). `HealthModel` — GiveHealth/GiveArmor/GiveArmorBonus; новые `KeyInventory`/`PlayerKey`, `PlayerPowers`, `DeathDropTable` (POSS→CLIP, SPOS→SHOTGUN); `AmmoModel` — рюкзак. Unity: `PlayerInventory`, `ThingPickup`/`ThingSpawner` на все E1-предметы, `KeyMapping` + проверка ключей в `LineActivator`, ironfeet в `FloorDamageSystem`, дроп через `PickupFactory`, HUD KEYS/BERSERK/SUIT. Респавн сохраняет ключи. **215 EditMode + 25 PlayMode** (включая 5× `PickupPlayTests`). В исходном scope были отложены rockets/cells/new weapons; rockets уже реализованы, Cells/Plasma/BFG запланированы отдельным расширением 6c. Также отложены invuln/invis/map/light-amp и анимация бонусов. Дизайн — `docs/superpowers/specs/2026-07-10-pickups-design.md`; план — `docs/superpowers/plans/2026-07-10-pickups.md`.

**Под-этап 6f (звук + музыка) ✅.** `Doom.Audio` декодирует DMX `DS*`, MUS и SMF MIDI (`MusicScore` — Freedoom `D_*` это Type-1 MIDI; retail DOOM — MUS), `GENMIDI`; `MusOplPlayer` (порт LittleMUS) + vendored Nuked OPL3 (`Assets/ThirdParty/NukedOpl/`, LGPL-2.1). `Doom.MapBuild`: `SoundCache`/`SoundSystem` (пул 2D/3D), `MusicPlayer` (streaming 44.1 kHz), table-driven pre-warm. Озвучены оружие, пикапы, игрок, монстры E1, фаербол, двери/лифты/switch/key-deny + зацикленная музыка карты. **253 EditMode + 34 PlayMode** (`SoundPlayTests` ×9). Дизайн — `docs/superpowers/specs/2026-07-10-sound-design.md`; план — `docs/superpowers/plans/2026-07-10-sound.md`.

**Результат этапа:** играбельный уровень с базовым геймплеем.

---

## Этап 7. Шлифовка и расширение

Довести проект до законченного вида. Разбит на 7a–7e; спека/план —
`docs/superpowers/specs/2026-07-10-polish-design.md`,
`docs/superpowers/plans/2026-07-10-polish.md`.

- Переходы между уровнями / campaign session. ✅ (под-этап 7a)
- HUD: здоровье, патроны, оружие, лицо + intermission. ✅ (под-этап 7b)
- Меню, настройки. ✅ (под-этап 7c)
- Сохранения. ✅ (под-этап 7d)
- Оптимизация, E1 specials, standalone build. ✅ (под-этап 7e)
- Тестирование на разных картах WAD. ✅ (E1M1–E1M9 smoke + sign-off)

**Под-этап 7a (session и переходы) ✅.** `CampaignRoute`/`SessionState`,
`GameSessionHost`, исполнение exit (11/51/52/124 + sector 11), перезагрузка
сцены с carry-over инвентаря.

**Под-этап 7b (HUD и intermission) ✅.** WAD-only status bar (`DoomHud`),
`HudModel`/`FaceRules`, `LevelStats` + `IntermissionView`. Debug `PlayerHud`
удалён. Коллайдеры на ML_BLOCKING midtextures (решётки).

**Под-этап 7c (меню и настройки) ✅.** `GameFlowController` (Playing/Paused/
Dead/Intermission/MainMenu), WAD `MenuController`, `GameSettingsData` +
`SettingsStore`/`SettingsController` (SFX/music volume, sensitivity, invert Y,
fullscreen); runtime volume и music pause/resume без рестарта sequencer.

**Под-этап 7d (full-world saves) ✅.** Pure `SaveGame`/`PlayerSnapshot`/
`WorldSnapshot` + `SaveGameCodec`/`SaveSlotStore`; Unity `WorldStateRegistry`/
capture/restore/`SaveGameController`; Save из pause, Load из main/pause с
WAD-identity preflight; scene reload и phased restore. PlayMode
`WorldCapturePlayTests` + `SaveLoadPlayTests`.

**Под-этап 7e (E1 specials / perf / build) ✅.** Teleports 97/125/126,
spectre/baron AI, `WalkLineIndex` + LineRef cache, E1M1–E1M9 smoke,
Windows standalone (`Tools > Doom > Build Windows Standalone`), baseline
`Logs/stage7e-baseline-notes.md`. Музыка в player: `OnAudioFilterRead`
(streaming PCM callbacks молчали в standalone). **413 EditMode + 67 PlayMode**
(2026-07-12). Known limits: scroll 48 / spectre translucency (light 138 done in Stage 8 Task 8).

**Результат этапа:** завершённый ремейк с играбельным Freedoom E1 и Windows build.

---

## Этап 8. Режимы графики Classic и Enhanced

Пост-релизная графическая веха после завершения Stage 7. В Options появляется
одна настройка `Graphics Mode` с двумя значениями:

- **Classic** — текущий WAD-driven вид: Point filtering, секторная яркость,
  Unlit opaque/cutout, без современного света и post-processing.
- **Enhanced** — тот же WAD и gameplay world через URP: procedural normal maps,
  секторный ambient и ограниченные динамические lights/shadows, HDR/SSAO/bloom/
  fog, MSAA/render scale/FSR, WAD SKY1, анимированные emissive fluids,
  улучшенные sprites, pooled particles и decals.

Оба режима работают в одном URP pipeline и переключаются без отдельной сцены,
повторного WAD decode или изменения gameplay/save state. Производные normal/
effect textures существуют только в runtime memory; authored и AI-upscaled
replacement assets не добавляются. Classic остаётся режимом по умолчанию и
эталоном визуальной регрессии.

**Статус:** ✅ завершён на ветке `enhanced` (2026-07-12). **Tasks 1–15 ✅**:
8a–8e (baseline → URP Classic/Enhanced → sky/sprites/particles → perf →
Windows build + interactive eyeball). Full suites at Task 14:
**459 EditMode + 95 PlayMode**. Standalone ~128 MB. Interactive sign-off
closed the same day (Classic↔Enhanced, sky/fluids/fog/lamps/HUD/muzzle QA;
post-sign-off polish commits on `enhanced`).

**Дизайн:** `docs/superpowers/specs/2026-07-11-enhanced-graphics-design.md`  
**План реализации:** `docs/superpowers/plans/2026-07-11-enhanced-graphics.md`

**Результат этапа:** пользователь может сравнить Classic и Enhanced на
E1M1–E1M9; Classic сохраняет текущий вид, Enhanced использует современные
возможности GPU без замены WAD-контента. Stage 8 закрыт.

### Пост-Stage 8: Enhanced Texture Upscaling (Scale2x) ⚠️

Ветка `upscale` (2026-07-12): runtime 2× Scale2x для Enhanced world
textures/flats/`SKY1`, normals из 2× source, hot-switch без reload.
Automation: **505 EditMode + 109 PlayMode**, Windows build OK.

**Интерактивный вывод:** Scale2x **не даёт значимого улучшения** читаемости
albedo на типичных Freedoom E1 текстурах (ортогональный pixel art почти не
меняется). Pipeline оставляем; для заметного эффекта нужен более сильный
алгоритм (xBRZ 2× / опционально 4×) — отдельного плана пока нет.

**Дизайн/план:**
`docs/superpowers/specs/2026-07-12-enhanced-texture-upscaling-design.md`,
`docs/superpowers/plans/2026-07-12-enhanced-texture-upscaling.md`  
**Notes:** `Logs/enhanced-texture-upscale-baseline-notes.md`

---

## Этап 9. E1 compatibility polish 🚧

Ограниченная compatibility-веха после Stage 8, закрывающая известные пробелы
уже поддерживаемого E1 без расширения проекта до полного source-port:

- linedef texture scroll 48/85;
- crushers, crush damage и save schema v6 с сохранением decode v1–v5;
- queued weapon switch и опускание weapon view при смерти;
- WAD-driven анимация pickups;
- XDEATH для поддерживаемого E1 roster;
- компактная SFX pitch/priority policy и единый полный pre-warm catalog;
- focused/full suites, dual-profile E1 smoke, Windows build и интерактивная
  приёмка.

**Статус:** 🚧 automated verification green (2026-07-12); interactive
sign-off pending. Fresh totals: **492 EditMode + 107 PlayMode**; Windows
standalone Success (~128 MB). Notes:
`Logs/stage9-e1-compatibility-notes.md`. `CLAUDE.md` закрывается только после
интерактивного checklist.

**Не входит:** sound occlusion, полная DMX channel emulation, automap, новые
powerups, flying monsters, Doom II, общая PWAD/mod compatibility и finale.

**Дизайн:**
`docs/superpowers/specs/2026-07-12-e1-compatibility-polish-design.md`  
**План реализации:**
`docs/superpowers/plans/2026-07-12-e1-compatibility-polish.md`

**Результат этапа:** E1M1–E1M9 сохраняют текущую архитектуру и контент, но
получают недостающие scrolling/crusher/weapon/item/death/audio compatibility
детали. До прохождения verification gates этап не считается завершённым.

---

## Рекомендации по ведению проекта

- **Маленькие вехи.** Первая цель — серая геометрия E1M1, по которой можно ходить. Большинство ремейков бросают именно на текстурах, спрайтах и ИИ — поэтому каждый шаг должен давать видимый результат.
- **Свободные WAD.** Использовать Freedoom как тестовые данные, чтобы не зависеть от коммерческого `DOOM.WAD` и иметь возможность распространять проект.
- **Документация формата.** Опираться на материалы сообщества по формату WAD (ключевые слова: «Doom WAD format», «linedefs», «sidedefs», «sectors»).
- **Не пытаться портировать C-код оригинала.** Переносятся данные и алгоритмы логики, а не движок рендеринга.
