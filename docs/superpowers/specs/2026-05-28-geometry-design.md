# Этап 2. Геометрия одной карты (серый блок-аут) — дизайн

**Дата:** 2026-05-28
**Статус:** выполнено
**План реализации:** docs/superpowers/plans/2026-05-28-geometry.md
**Предыдущий этап:** Stage 1 завершён (WAD-ридер `Doom.Wad`)

## Цель

Превратить лампы карты (`VERTEXES`, `LINEDEFS`, `SIDEDEFS`, `SECTORS`) в серую 3D-геометрию
Unity, видимую в Play mode на любой карте Freedoom Phase 1 (E1M1..E4M9). Видимый результат
этапа: серый блок-аут E1M1 — полы, потолки, стены, корректные высоты секторов — который
можно облететь камерой. Текстуры, игрок и объекты сознательно вынесены в следующие этапы.

## Объём этапа

**Входит:**
- Парсинг сырой геометрии карты: `VERTEXES`, `LINEDEFS`, `SIDEDEFS`, `SECTORS`
  (read-only слой `MapData` + `MapData.Load(wad, mapName)`).
- Восстановление замкнутых контуров каждого сектора (outer + holes) из linedef-ов
  через half-edge chaining (`SectorPolygonBuilder`) — без чтения BSP-сегментов.
- Триангуляция полов и потолков через LibTessDotNet (`SectorTriangulator`).
- Геометрия стен: one-sided квады + two-sided lower/upper ступени (`WallMeshBuilder`).
- Оркестрация всего в `MapGeometryBuilder.Build(map) → SectorMeshes[]`.
- Unity-glue: `MapLoader` MonoBehaviour превращает `MeshData` в `UnityEngine.Mesh`,
  строит иерархию `GameObject`-ов с `MeshFilter`/`MeshRenderer`/`MeshCollider`.
- Превью-сцена `Stage2_MapPreview.unity` + интеграционные тесты на `freedoom1.wad`.

**Сознательно отложено:**
- **Текстуры** — Stage 4. Сейчас только три серых материала (пол/потолок/стена).
  Имена текстур из сайддефов/секторов парсятся, но не используются.
- **Игрок / камера / коллизии хождения** — Stage 3. `MeshCollider` навешивается заранее.
- **THINGS (объекты, монстры, точки спавна)** — Stage 3+.
- **BSP / PVS / освещение DOOM** — не портируем вообще; видимостью и физикой
  занимается Unity (culling + lighting).
- **Анимация флэтов, двери, лифты** — Stage 6 (gameplay).
- **Скайбокс / F_SKY1** — Stage 4 (текстуры) или позже.

## Ключевые решения

Зафиксированы на брейншторме:

1. **Время сборки:** runtime, через MonoBehaviour `MapLoader` на `Start()`.
2. **Имя карты:** настраиваемое поле `MapName` (по умолчанию `E1M1`).
3. **Масштаб (на момент Stage 2):** 1 DOOM-единица = 1 Unity-метр, без масштабирования.
   (Позже, в Stage 3, через `MapGeometryBuilder` прокинут `worldScale = 1/32`.)
4. **Координаты:** DOOM (X, Y) → Unity (X, Z); высота сектора ложится на Unity-Y.
   То есть DOOM-овая плоскость XY поднимается в Unity-овую XZ.
5. **Триангуляция:** вендорим LibTessDotNet v1.1.15 (SGI Free Software License B 2.0)
   в `Assets/ThirdParty/` — свой триангулятор не пишем, но и не зависим от Unity-сборки.
6. **Коллайдеры:** `MeshCollider` навешивается уже сейчас — пригодится в Stage 3.
7. **Иерархия:** `GameObject` на сектор, под ним три ребёнка `Floor` / `Ceiling` / `Walls`.
8. **Двухслойная архитектура:** парсинг и геометрия — чистый C# (`noEngineReferences`),
   `UnityEngine` только в `Doom.MapBuild`.
9. **Логирование без Unity:** `Doom.Map` не может звать `Debug.LogWarning`
   (`noEngineReferences`), поэтому предупреждения/ошибки идут через статический канал
   `MapLog` (события `WarningHandler`/`ErrorHandler`), на который `MapLoader` подписывает
   `Debug.LogWarning`/`Debug.LogError`.
10. **Обработка ошибок:** «лог + skip» там, где это имеет смысл (кривой сектор, edge
    out-of-range). Жёсткий бросок (`KeyNotFoundException` / `InvalidDataException`) —
    только если карта целиком не парсится (нет маркера или обязательного лампа).

### Архитектурные правила проекта, важные для Stage 2

- **BSP-данные не используются.** Лампы `NODES`, `SSECTORS`, `SEGS`, `BLOCKMAP`,
  `REJECT` игнорируются полностью. Контуры секторов реконструируются сами, из linedef-ов.
- **Числа в WAD little-endian** — `BinaryReader` подходит как есть.
- **Не портируем C-движок** — half-edge сборку контуров и триангуляцию пишем/вендорим
  с нуля, а не переносим из оригинала.
- **Никакого собственного контента.** Если кажется, что нужна ручная карта/меш — это
  сигнал перечитать спеку WAD.

## Архитектура

### Сборка `Doom.Map` (pure C#, `noEngineReferences: true`)

Зависит от `Doom.Wad` и `LibTessDotNet`. Не ссылается на `UnityEngine`. Содержит:

- **`MapData`** — структуры `Vertex` / `LineDef` / `SideDef` / `Sector`, статические
  парсеры лампов и фабрику `Load(WadFile, mapName)`, которая находит маркер карты,
  собирает 4 обязательных лампа в окне после маркера и возвращает заполненный объект.
- **`MapLog`** — статический канал предупреждений/ошибок (события-делегаты).
- **`SectorPolygon`** — результат реконструкции контура: outer-кольцо + список дырок
  (индексы в `MapData.Vertexes`) + флаг `IsValid`.
- **`SectorPolygonBuilder`** — half-edge chaining: из linedef-ов формирует ориентированные
  half-edges (сектор всегда слева), группирует по сектору, замыкает в loops и
  классифицирует их по знаковой площади (максимальный по модулю — outer, остальные — holes).
- **`MeshData`** + `Float3` — плоский результат геометрии (вершины + индексы) в чистом C#.
- **`SectorTriangulator`** — обёртка над LibTessDotNet: пол (нормаль вверх) и потолок
  (нормаль вниз, индексы реверсированы) → `MeshData`.
- **`WallMeshBuilder`** — квады стен, видимых из сектора: one-sided во весь объём,
  two-sided lower/upper ступени по разнице высот с соседом.
- **`MapGeometryBuilder`** — оркестратор: `Build(map) → SectorMeshes[]` (на сектор:
  Floor / Ceiling / Walls).

### Сборка `Doom.MapBuild` (runtime, с `UnityEngine`)

- **`MapLoader`** MonoBehaviour — единственное место, где `MeshData` превращается в
  `UnityEngine.Mesh`. Читает WAD из `StreamingAssets`, грузит карту, строит геометрию,
  инстанцирует иерархию `GameObject`-ов (`MeshFilter`/`MeshRenderer`/`MeshCollider`),
  подписывает `MapLog` на `Debug.Log*`. Серые материалы (Standard shader) пока заглушка
  под текстуры Stage 4.

### Поток данных

```
WadFile ─► MapData.Load (VERTEXES/LINEDEFS/SIDEDEFS/SECTORS)
                 │
                 ▼  MapData
MapGeometryBuilder
   ├─ SectorPolygonBuilder  (half-edge chaining → outer + holes)
   ├─ SectorTriangulator    (LibTessDotNet → floor up / ceiling down)
   └─ WallMeshBuilder       (one-sided + two-sided lower/upper quads)
                 │
                 ▼  SectorMeshes[] (Floor/Ceiling/Walls, чистый C# MeshData)
Doom.MapBuild: MapLoader → UnityEngine.Mesh + MeshCollider ─► сцена
```

### Координатная трансформация (справка)

```
unityX = doomX
unityY = doomZ  (высота пола/потолка сектора)
unityZ = doomY
```

Намотка треугольников:
- Пол: нормаль вверх (`+Y`). LibTess выдаёт CCW в DOOM-XY, что после свопа Y/Z уже
  корректно для пола.
- Потолок: нормаль вниз (`-Y`) — реверсируем индексы каждого треугольника.
- Стены: порядок вершин квада подбирается так, чтобы нормаль смотрела в сектор-владелец.

### Конвенции сторон linedef-а (справка)

- **Front sidedef** — справа от вектора `V1 → V2`; **back sidedef** (если есть) — слева.
- Для CCW-обхода контура сектора (сектор слева от направления edge): front-сектор берёт
  направление `V2→V1`, back-сектор — `V1→V2`. Это фундамент `SectorPolygonBuilder`.
- Двусторонность определяем по `backSide != 0xFFFF`, а не по флагу `0x0004` (флаг
  используется чаще для рендеринга, чем для топологии).

### Формат лампов карты (справка)

Карта в IWAD'е — маркер-лампа (`E1M1` / `MAP01`, size 0), за которой идут лампы геометрии.
Stage 2 читает только четыре из них; BSP-лампы (`SEGS`/`SSECTORS`/`NODES`/`REJECT`/`BLOCKMAP`)
пропускаются:

- **`VERTEXES`** — записи по 4 байта: `x` int16 LE, `y` int16 LE.
- **`LINEDEFS`** — записи по 14 байт: `v1`, `v2`, `flags`, `specialType`, `sectorTag`,
  `frontSide`, `backSide` (`0xFFFF` = нет задней стороны), все uint16 LE.
- **`SIDEDEFS`** — записи по 30 байт: `textureXOffset`/`textureYOffset` int16 LE,
  `upper`/`lower`/`middle` texture (ASCII 8, `\0`-padded; `"-"` = нет текстуры),
  `sectorIdx` uint16 LE.
- **`SECTORS`** — записи по 26 байт: `floorHeight`/`ceilingHeight` int16 LE,
  `floorFlat`/`ceilingFlat` (ASCII 8), `lightLevel`/`specialType`/`tag` uint16 LE.
  В Stage 2 используются только высоты пола и потолка; остальное парсится впрок.

## Тестирование

Подход тот же, что в Stage 1: unit-тесты на синтетических байтовых блобах через
`SyntheticMapBuilder` (поверх `SyntheticWadBuilder` из Stage 1) плюс интеграционный сьют
на реальном `freedoom1.wad`. Прогон через Unity Test Framework (NUnit, EditMode) —
из Test Runner в редакторе или headless через CLI.

Покрытие:
- Парсинг каждого лампа (`VERTEXES`/`LINEDEFS`/`SIDEDEFS`/`SECTORS`), включая
  некратный размер лампа (warning + skip хвоста, не бросок).
- `MapData.Load`: успешная загрузка всех четырёх лампов; броски при отсутствии маркера,
  невалидном имени карты, отсутствии обязательного лампа.
- `SectorPolygonBuilder` на четырёх топологиях: одиночная комната (один outer, CCW),
  две смежные комнаты через одну two-sided линию, комната с колонной (outer + один hole),
  и незамкнутый контур (помечается `IsValid = false` + warning).
- `SectorTriangulator`: пол даёт нормаль вверх, потолок — вниз с реверсом индексов,
  невалидный полигон → пустой меш.
- `WallMeshBuilder`: one-sided → один квад; two-sided без перепада высот → ноль квадов;
  two-sided со ступенью пола → lower-квад только у нижнего сектора.
- `MapGeometryBuilder`: квадратная комната даёт непустые Floor/Ceiling/Walls.
- Интеграция на `freedoom1.wad`: E1M1 проходит весь пайплайн без исключений,
  число `SectorMeshes` совпадает с числом секторов, хотя бы один сектор даёт непустой пол.

## Затронутые файлы

Новые:
- `Assets/Scripts/Map/` — `Doom.Map.asmdef`, `MapData.cs`, `MeshData.cs`,
  `SectorPolygon.cs`, `SectorPolygonBuilder.cs`, `SectorTriangulator.cs`,
  `WallMeshBuilder.cs`, `MapGeometryBuilder.cs`.
- `Assets/Scripts/MapBuild/` — `Doom.MapBuild.asmdef`, `MapLoader.cs`.
- `Assets/ThirdParty/LibTessDotNet/` — вендоренные исходники v1.1.15 + asmdef + LICENSE.
- `Assets/Materials/MapBlockOut/` — `Floor.mat`, `Ceiling.mat`, `Wall.mat`.
- `Assets/Scenes/Stage2_MapPreview.unity`.
- `Assets/Tests/EditMode/Map/` — `Doom.Map.Tests.asmdef`, `SyntheticMapBuilder.cs`,
  `MapDataReaderTests.cs`, `MapDataLoadTests.cs`, `SectorPolygonBuilderTests.cs`,
  `SectorTriangulatorTests.cs`, `WallMeshBuilderTests.cs`, `MapGeometryBuilderTests.cs`,
  `MapFreedoomTests.cs`.

Изменяемые:
- `Assets/Tests/EditMode/Wad/SyntheticWadBuilder.cs` — `internal` → `public`, чтобы
  переиспользовать билдер из `Doom.Map.Tests`.

## Definition of done

Этап считается завершённым, когда:

1. Все EditMode-тесты проходят (30 из Stage 1 + новые тесты Stage 2).
2. `Stage2_MapPreview.unity` на Play показывает серый блок-аут E1M1: полы, потолки,
   стены, корректные высоты секторов.
3. `MapData.Load` + `MapGeometryBuilder.Build` работают на любой карте Freedoom Phase 1
   (E1M1..E4M9) без исключений (отдельные кривые сектора логируются, но не валят процесс).
4. `Doom.Map` остаётся `noEngineReferences: true` — парсер и геометрия не зависят от Unity.
5. Архитектурные правила соблюдены: BSP-лампы не читаются; триангуляция — через
   LibTessDotNet; координаты DOOM→Unity по схеме (X, Z, Y).
