# Stage 3: Игрок и коллизии — План реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Спавнить игрока в стартовой позиции из `THINGS`, дать управление от первого лица (WASD + полная мышь + Shift-бег), использовать `CharacterController` для столкновений с уже существующими `MeshCollider`-ами Stage 2. На выходе — Play mode по E1M1 от первого лица.

**Architecture:** Чистая часть (`Doom.Map`) расширяется парсером `THINGS` и параметром `worldScale = 1/32` в `MapGeometryBuilder` (32 DOOM-единицы = 1 Unity-метр, игрок ~1.75 м, gravity = -9.81 m/s² работает естественно). Unity-сторона (`Doom.MapBuild`) получает новый `PlayerController` MonoBehaviour с самостоятельно собранным `InputActionMap` (новый Input System, без asset-файла), плюс `MapLoader` инстанцирует Player с CharacterController + Camera в точке Player 1 Start.

**Tech Stack:** Unity 6000.4.8f1, C# / .NET, новый Input System package (`com.unity.inputsystem`), Unity Test Framework (NUnit EditMode + PlayMode), существующие `Doom.Wad` / `Doom.Map` / `Doom.MapBuild` сборки из Stage 1-2.

---

## Контекст

Stage 2 закрыт: пайплайн `MapData.Load` → `MapGeometryBuilder.Build` → `MapLoader` строит серую геометрию E1M1, `MeshCollider`-ы навешаны на каждый кусок. 62 EditMode-теста проходят. Сейчас в Play mode видна карта сверху (`autoFitCamera` в `MapLoader`), но игрока нет — это и есть пробел, который закрывает Stage 3.

Дизайн-решения, зафиксированные на брейншторме:

- **Scale:** `worldScale = 1f / 32f` пробрасывается **в MeshData generation** (внутри `Doom.Map`), не на уровне корневого `Transform`. Это значит — переделываем сигнатуры `MapGeometryBuilder.Build`, `SectorTriangulator.TriangulateFloor/Ceiling`, `WallMeshBuilder.BuildForSector`. Stage 2 тесты, вызывавшие эти методы без параметра, получают значение по умолчанию `1f`, чтобы не сломаться (а новые тесты явно проверяют масштабирование).
- **Input API:** новый Input System package `com.unity.inputsystem`. **БЕЗ** asset-файла `.inputactions` — собираем `InputActionMap` программно в `PlayerController.Awake()`. Так меньше зависимости от asset-импорта Unity, проще для headless-выполнения subagent-ами.
- **THINGS scope:** парсим ВСЕ записи (10-байтные структуры), но `MapLoader` использует только `Thing.Type == 1` (Player 1 start). Остальные типы (монстры, предметы) — Stage 5/6.
- **Movement feel:** modern FPS — полная мышь (yaw + pitch ±85°), WASD, Shift для бега (hold), без прыжка, без приседа. Скорости: walk = 200 DOOM/s × 1/32 ≈ 6.25 m/s; run = 440 DOOM/s × 1/32 ≈ 13.75 m/s.
- **CharacterController:** height = 56/32 = 1.75 m, radius = 16/32 = 0.5 m, stepOffset = 24/32 = 0.75 m (DOOM step-up auto-climb), slopeLimit = 45°.
- **Camera pivot:** дочерний `Transform` Player'а на Y = 41/32 ≈ 1.28 m (DOOM eye height).
- **Player spawn:** Player инстанцируется на высоте `bounds.max.y + 5` (высоко над крышей) — gravity роняет на пол. Так избегаем point-in-polygon для определения стартового сектора.
- **DOOM angle → Unity yaw:** DOOM angle 0 = east (+X), 90 = north (+Y DOOM → +Z Unity). Unity yaw 0 = +Z (forward). Конверсия: `unityYaw = 90f - doomAngle`.
- **Tests:** EditMode на THINGS parser + scale plumbing + Freedoom integration, плюс **новая PlayMode-сборка** `Doom.Stage3.PlayTests` с одним тестом «игрок падает на пол при старте E1M1».

## Формат лампа THINGS (краткая справка)

`THINGS` идёт первым лампом после маркера карты в каноническом порядке. Каждая запись — 10 байт:

| Смещение | Размер | Поле     | Тип |
|----------|--------|----------|-----|
| 0        | 2      | `x`      | int16 LE — DOOM X-координата |
| 2        | 2      | `y`      | int16 LE — DOOM Y-координата |
| 4        | 2      | `angle`  | uint16 LE — 0..359° (player starts обычно с шагом 45°) |
| 6        | 2      | `type`   | uint16 LE — `1` = Player 1 start, `2`/`3`/`4` = co-op starts, `11` = deathmatch start, тысячи = монстры/предметы |
| 8        | 2      | `flags`  | uint16 LE — bitmask (easy/medium/hard/multiplayer/deaf) |

В Stage 3 нас интересует исключительно `type == 1`.

## Файловая структура

После выполнения плана:

```
Packages/manifest.json                              ← + "com.unity.inputsystem": "1.11.2"
ProjectSettings/ProjectSettings.asset               ← activeInputHandler: 2 (Both)

Assets/Scripts/Map/MapData.cs                       ← + Thing struct, ParseThings, Things property,
                                                       THINGS reads in Load
Assets/Scripts/Map/MapGeometryBuilder.cs            ← + worldScale arg
Assets/Scripts/Map/SectorTriangulator.cs            ← × worldScale on vertices + heights
Assets/Scripts/Map/WallMeshBuilder.cs               ← × worldScale on vertices

Assets/Scripts/MapBuild/Doom.MapBuild.asmdef        ← + reference "Unity.InputSystem"
Assets/Scripts/MapBuild/MapLoader.cs                ← + worldScale field, spawn Player
Assets/Scripts/MapBuild/PlayerController.cs         ← NEW MonoBehaviour, hand-built InputActions

Assets/Tests/EditMode/Map/SyntheticMapBuilder.cs    ← + BuildThings, BuildMapWad accepts things
Assets/Tests/EditMode/Map/MapDataReaderTests.cs     ← + Parses_things + size warning test
Assets/Tests/EditMode/Map/MapDataLoadTests.cs       ← + "THINGS" to required-lump TestCase
Assets/Tests/EditMode/Map/MapGeometryScaleTests.cs  ← NEW (scale=0.5 halves vertices)
Assets/Tests/EditMode/Map/MapFreedoomTests.cs       ← + E1M1_has_player_start_in_things

Assets/Tests/PlayMode/                              ← NEW directory
├── Doom.Stage3.PlayTests.asmdef                    ← NEW
└── PlayerLandsOnFloorTests.cs                      ← NEW
```

## Тесты: где и как запускать

**EditMode** (быстро, как раньше):

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -nographics -projectPath "D:\Development\doom" `
    -runTests -testPlatform EditMode `
    -testResults "D:\Development\doom\Logs\test-results.xml" `
    -logFile -
```

**PlayMode** (новое, медленнее):

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -projectPath "D:\Development\doom" `
    -runTests -testPlatform PlayMode `
    -testResults "D:\Development\doom\Logs\playmode-results.xml" `
    -logFile -
```

**NB:** для PlayMode НЕ добавлять `-nographics` — это ломает PhysX (CharacterController перестаёт получать collision callbacks) и `Camera.Render`. `-batchmode` без `-nographics` использует Null GfxDevice для рендера, но физика работает. На Windows `-force-d3d11` тоже опционально, но не обязателен для PlayMode тестов без рендера.

Per-test PASS/FAIL пишутся в XML.

---

### Task 1: Установить Input System package + переключить ProjectSettings

**Files:**
- Modify: `Packages/manifest.json`
- Modify: `ProjectSettings/ProjectSettings.asset`

- [ ] **Step 1: Добавить пакет в манифест**

В `Packages/manifest.json`, в секции `dependencies`, добавить (по алфавиту):

```json
"com.unity.inputsystem": "1.11.2",
```

(Между `"com.unity.ide.visualstudio"` если есть, или просто перед `"com.unity.modules.accessibility"` — порядок ключей внутри `dependencies` не влияет на работу.)

- [ ] **Step 2: Переключить `activeInputHandler` на «Both»**

В `ProjectSettings/ProjectSettings.asset` найти строку `activeInputHandler:` и заменить значение на `2`:

```yaml
  activeInputHandler: 2
```

Значения: `0` = OldInputManagerOnly, `1` = NewInputSystemOnly, `2` = Both. Ставим `2`, чтобы Stage 2 / редакторное меню Stage 1 (использующие старый `Input` API из `UnityEditor`) не сломались.

Если ключ `activeInputHandler` отсутствует — добавить его в секцию `PlayerSettings` (рядом с другими `m_*` ключами). Если он уже `2` — ничего не делать.

- [ ] **Step 3: Прогнать тесты — компиляция должна пройти, 62 EditMode tests pass**

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -nographics -projectPath "D:\Development\doom" `
    -runTests -testPlatform EditMode `
    -testResults "D:\Development\doom\Logs\test-results.xml" `
    -logFile -
```

Ожидаемое: 62 теста pass, в `Library/PackageCache/com.unity.inputsystem@*` появилась папка с исходниками пакета. Timeout 300000 ms.

Если Unity при первом запуске жалуется на «active input handling has changed, please restart» в логе — это нормально, перезапуск headless-вызова не нужен; следующий запуск всё применит.

- [ ] **Step 4: Закоммитить**

```powershell
git add Packages/manifest.json Packages/packages-lock.json ProjectSettings/ProjectSettings.asset
git commit -m "Stage 3 setup: enable new Input System (Both) + add com.unity.inputsystem package"
```

(`packages-lock.json` Unity обновит автоматически после Step 3.)

---

### Task 2: `Thing` struct + `ParseThings`

**Files:**
- Modify: `Assets/Scripts/Map/MapData.cs`
- Modify: `Assets/Tests/EditMode/Map/SyntheticMapBuilder.cs`
- Modify: `Assets/Tests/EditMode/Map/MapDataReaderTests.cs`

- [ ] **Step 1: Дописать `BuildThings` в синтетический билдер**

В `Assets/Tests/EditMode/Map/SyntheticMapBuilder.cs`, после `BuildSectors`:

```csharp
        public static byte[] BuildThings(params (short x, short y,
                                                  ushort angle, ushort type, ushort flags)[] things)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            foreach (var t in things)
            {
                w.Write(t.x); w.Write(t.y);
                w.Write(t.angle); w.Write(t.type); w.Write(t.flags);
            }
            return ms.ToArray();
        }
```

Также модифицировать сигнатуру `BuildMapWad`, чтобы принимать `byte[] things = null`:

```csharp
        public static byte[] BuildMapWad(string mapName,
            byte[] vertexes = null, byte[] linedefs = null,
            byte[] sidedefs = null, byte[] sectors = null,
            byte[] things = null)
        {
            var lumps = new List<SyntheticWadBuilder.Lump>
            {
                new SyntheticWadBuilder.Lump(mapName, new byte[0]),
            };
            if (things   != null) lumps.Add(new SyntheticWadBuilder.Lump("THINGS",   things));
            if (linedefs != null) lumps.Add(new SyntheticWadBuilder.Lump("LINEDEFS", linedefs));
            if (sidedefs != null) lumps.Add(new SyntheticWadBuilder.Lump("SIDEDEFS", sidedefs));
            if (vertexes != null) lumps.Add(new SyntheticWadBuilder.Lump("VERTEXES", vertexes));
            if (sectors  != null) lumps.Add(new SyntheticWadBuilder.Lump("SECTORS",  sectors));
            return SyntheticWadBuilder.Build("IWAD",
                (System.Collections.Generic.IReadOnlyList<SyntheticWadBuilder.Lump>)lumps);
        }
```

(`things` ставится сразу после маркера, как в каноническом порядке.)

- [ ] **Step 2: Падающие тесты на `Thing`**

В `Assets/Tests/EditMode/Map/MapDataReaderTests.cs` дописать в класс:

```csharp
        [Test]
        public void Parses_things_into_records()
        {
            var bytes = SyntheticMapBuilder.BuildThings(
                (x: 100, y: 200, angle: 90, type: 1, flags: 0x07),
                (x: -50, y: 300, angle: 270, type: 3004, flags: 0x10));

            var things = MapData.ParseThings(bytes);

            Assert.That(things.Length, Is.EqualTo(2));
            Assert.That(things[0].X, Is.EqualTo((short)100));
            Assert.That(things[0].Y, Is.EqualTo((short)200));
            Assert.That(things[0].Angle, Is.EqualTo(90));
            Assert.That(things[0].Type, Is.EqualTo(1));
            Assert.That(things[0].Flags, Is.EqualTo(0x07));
            Assert.That(things[1].X, Is.EqualTo((short)-50));
            Assert.That(things[1].Type, Is.EqualTo(3004));
        }

        [Test]
        public void Things_lump_size_not_multiple_of_10_is_warning_not_throw()
        {
            // 10 байт = одна валидная запись, +3 «хвостовых» байта — игнорируются
            var bytes = new byte[13];
            // x=5, y=0, angle=0, type=1, flags=0
            bytes[0] = 5; bytes[1] = 0;
            bytes[2] = 0; bytes[3] = 0;
            bytes[4] = 0; bytes[5] = 0;
            bytes[6] = 1; bytes[7] = 0;
            bytes[8] = 0; bytes[9] = 0;
            bytes[10] = 0xAB; bytes[11] = 0xCD; bytes[12] = 0xEF;

            string captured = null;
            System.Action<string> handler = m => captured = m;
            MapLog.WarningHandler += handler;
            try
            {
                var things = MapData.ParseThings(bytes);
                Assert.That(captured, Does.Contain("THINGS"));
                Assert.That(things.Length, Is.EqualTo(1));
                Assert.That(things[0].X, Is.EqualTo((short)5));
                Assert.That(things[0].Type, Is.EqualTo(1));
            }
            finally { MapLog.WarningHandler -= handler; }
        }
```

- [ ] **Step 3: Прогнать — падает «`Thing` / `ParseThings` не определены»**

(Можно пропустить, high confidence.)

- [ ] **Step 4: Реализовать `Thing` + `ParseThings`**

В `Assets/Scripts/Map/MapData.cs` добавить struct `Thing` после `Sector` (до объявления `public sealed class MapData`):

```csharp
    public readonly struct Thing
    {
        public readonly short X;
        public readonly short Y;
        public readonly ushort Angle;
        public readonly ushort Type;
        public readonly ushort Flags;

        public Thing(short x, short y, ushort angle, ushort type, ushort flags)
        {
            X = x; Y = y; Angle = angle; Type = type; Flags = flags;
        }
    }
```

В `class MapData`, в секции size constants, добавить:

```csharp
        private const int ThingSize   = 10;
```

В секции static parsers (рядом с `ParseSectors`), добавить:

```csharp
        public static Thing[] ParseThings(byte[] bytes)
        {
            if (bytes == null) return Array.Empty<Thing>();
            int count = bytes.Length / ThingSize;
            if (bytes.Length % ThingSize != 0)
            {
                MapLog.Warning(
                    $"THINGS: размер {bytes.Length} не кратен {ThingSize}, " +
                    $"читаем первые {count} записей");
            }
            var things = new Thing[count];
            using var ms = new MemoryStream(bytes);
            using var r = new BinaryReader(ms);
            for (int i = 0; i < count; i++)
            {
                short x = r.ReadInt16();
                short y = r.ReadInt16();
                ushort angle = r.ReadUInt16();
                ushort type  = r.ReadUInt16();
                ushort flags = r.ReadUInt16();
                things[i] = new Thing(x, y, angle, type, flags);
            }
            return things;
        }
```

- [ ] **Step 5: Прогнать — оба новых теста pass + предыдущие**

Ожидаемое: 64 теста pass (62 + 2 новых).

- [ ] **Step 6: Закоммитить**

```powershell
git add Assets/Scripts/Map/MapData.cs `
        Assets/Tests/EditMode/Map/SyntheticMapBuilder.cs `
        Assets/Tests/EditMode/Map/MapDataReaderTests.cs
git commit -m "Stage 3: parse THINGS lump (Thing struct + ParseThings)"
```

---

### Task 3: `THINGS` как пятый обязательный лампа в `MapData.Load`

**Files:**
- Modify: `Assets/Scripts/Map/MapData.cs`
- Modify: `Assets/Tests/EditMode/Map/MapDataLoadTests.cs`

- [ ] **Step 1: Расширить параметризацию `Throws_when_required_lump_missing`**

В `MapDataLoadTests.cs` найти существующий `[Test]` (он сейчас тестирует только VERTEXES) и заменить на `[TestCase]`-параметризацию:

```csharp
        [TestCase("THINGS")]
        [TestCase("VERTEXES")]
        [TestCase("LINEDEFS")]
        [TestCase("SIDEDEFS")]
        [TestCase("SECTORS")]
        public void Throws_when_required_lump_missing(string missingLump)
        {
            byte[] thingsBytes   = SyntheticMapBuilder.BuildThings((0, 0, 0, 1, 0));
            byte[] vertexBytes   = SyntheticMapBuilder.BuildVertexes((0, 0), (64, 0), (64, 64), (0, 64));
            byte[] lineBytes     = SyntheticMapBuilder.BuildLineDefs((0, 1, 0, 0, 0, 0, 0xFFFF));
            byte[] sideBytes     = SyntheticMapBuilder.BuildSideDefs((0, 0, "-", "-", "W", 0));
            byte[] sectorBytes   = SyntheticMapBuilder.BuildSectors((0, 128, "F", "F", 0, 0, 0));

            // обнуляем именованный лампа
            switch (missingLump)
            {
                case "THINGS":   thingsBytes = null; break;
                case "VERTEXES": vertexBytes = null; break;
                case "LINEDEFS": lineBytes   = null; break;
                case "SIDEDEFS": sideBytes   = null; break;
                case "SECTORS":  sectorBytes = null; break;
            }

            var wadBytes = SyntheticMapBuilder.BuildMapWad("E1M1",
                vertexes: vertexBytes, linedefs: lineBytes,
                sidedefs: sideBytes, sectors: sectorBytes,
                things: thingsBytes);
            using var wad = new WadFile(new MemoryStream(wadBytes), ownsStream: true);

            var ex = Assert.Throws<InvalidDataException>(
                () => Doom.Map.MapData.Load(wad, "E1M1"));
            StringAssert.Contains(missingLump, ex.Message);
        }
```

Также добавить тест, что Load успешно возвращает `Things`:

```csharp
        [Test]
        public void Loaded_map_exposes_things_from_THINGS_lump()
        {
            var wadBytes = SyntheticMapBuilder.BuildMapWad("E1M1",
                things:   SyntheticMapBuilder.BuildThings((100, 200, 0, 1, 7)),
                vertexes: SyntheticMapBuilder.BuildVertexes((0, 0), (64, 0), (64, 64), (0, 64)),
                linedefs: SyntheticMapBuilder.BuildLineDefs(
                    (0, 1, 0, 0, 0, 0, 0xFFFF),
                    (1, 2, 0, 0, 0, 1, 0xFFFF),
                    (2, 3, 0, 0, 0, 2, 0xFFFF),
                    (3, 0, 0, 0, 0, 3, 0xFFFF)),
                sidedefs: SyntheticMapBuilder.BuildSideDefs(
                    (0, 0, "-", "-", "W", 0), (0, 0, "-", "-", "W", 0),
                    (0, 0, "-", "-", "W", 0), (0, 0, "-", "-", "W", 0)),
                sectors:  SyntheticMapBuilder.BuildSectors((0, 128, "F", "F", 0, 0, 0)));

            using var wad = new WadFile(new MemoryStream(wadBytes), ownsStream: true);
            var map = Doom.Map.MapData.Load(wad, "E1M1");

            Assert.That(map.Things.Length, Is.EqualTo(1));
            Assert.That(map.Things[0].Type, Is.EqualTo(1));
            Assert.That(map.Things[0].X, Is.EqualTo((short)100));
        }
```

- [ ] **Step 2: Прогнать — должны падать «Things property не существует» и т.д.**

(Можно пропустить.)

- [ ] **Step 3: Добавить `Things` property + чтение `THINGS` в `Load`**

В `MapData.cs`, в `public sealed class MapData`:

В блоке instance properties (рядом с `Vertexes/LineDefs/SideDefs/Sectors`):

```csharp
        public Thing[]   Things   { get; }
```

В public constructor — добавить `Thing[] things` параметр и присвоить:

```csharp
        public MapData(string name,
                       Vertex[] vertexes, LineDef[] linedefs,
                       SideDef[] sidedefs, Sector[] sectors,
                       Thing[] things)
        {
            Name     = name;
            Vertexes = vertexes;
            LineDefs = linedefs;
            SideDefs = sidedefs;
            Sectors  = sectors;
            Things   = things;
        }
```

В `Load`, расширить switch и RequireLump-вызовы:

```csharp
            byte[] vertexBytes = null, lineBytes = null, sideBytes = null,
                   sectorBytes = null, thingBytes = null;
            for (int i = markerIdx + 1; i <= end; i++)
            {
                if (Doom.Wad.WadMapNames.IsMapMarker(wad.Directory[i].Name)) break;
                switch (wad.Directory[i].Name)
                {
                    case "THINGS":   thingBytes  = wad.ReadLump(i); break;
                    case "VERTEXES": vertexBytes = wad.ReadLump(i); break;
                    case "LINEDEFS": lineBytes   = wad.ReadLump(i); break;
                    case "SIDEDEFS": sideBytes   = wad.ReadLump(i); break;
                    case "SECTORS":  sectorBytes = wad.ReadLump(i); break;
                }
            }

            RequireLump(mapName, "THINGS",   thingBytes);
            RequireLump(mapName, "VERTEXES", vertexBytes);
            RequireLump(mapName, "LINEDEFS", lineBytes);
            RequireLump(mapName, "SIDEDEFS", sideBytes);
            RequireLump(mapName, "SECTORS",  sectorBytes);

            return new MapData(
                mapName,
                ParseVertexes(vertexBytes),
                ParseLineDefs(lineBytes),
                ParseSideDefs(sideBytes),
                ParseSectors(sectorBytes),
                ParseThings(thingBytes));
        }
```

- [ ] **Step 4: Прогнать — все тесты pass**

Ожидаемое: 66 тестов pass (Stage 2 закрылся с 4 `[TestCase]` для VERTEXES/LINEDEFS/SIDEDEFS/SECTORS — мы добавили пятую THINGS = +1, плюс новый `Loaded_map_exposes_things_from_THINGS_lump` = +1, итого 64 + 2). NB: тест-body старой параметризации заменяется новой версией (использующей `BuildMapWad` с пятью лампами) — обрати внимание на чистую дельту в диффе.

Если падает «`MapData` constructor expects 5 args but got 6» — значит где-то в Stage 2 коде остался вызов `new MapData(name, v, l, s, sec)` без `things`. Найди и обнови.

Это касается **тестов в `Doom.Map.Tests`** — `SectorPolygonBuilderTests`, `SectorTriangulatorTests`, `WallMeshBuilderTests` где они руками собирают `new MapData("TEST", verts, lines, sides, sectors)`. Добавь к каждому такому вызову `, System.Array.Empty<Thing>()` в конец:

```csharp
var map = new MapData("TEST", verts, lines, sides, sectors, System.Array.Empty<Thing>());
```

Делаешь это во ВСЕХ местах, иначе тесты не скомпилируются. Скорее всего: ~5-8 мест.

- [ ] **Step 5: Закоммитить**

```powershell
git add Assets/Scripts/Map/MapData.cs `
        Assets/Tests/EditMode/Map/MapDataLoadTests.cs `
        Assets/Tests/EditMode/Map/SectorPolygonBuilderTests.cs `
        Assets/Tests/EditMode/Map/SectorTriangulatorTests.cs `
        Assets/Tests/EditMode/Map/WallMeshBuilderTests.cs
git commit -m "Stage 3: THINGS as fifth required lump in MapData.Load"
```

---

### Task 4: Плюмбинг `worldScale` через `MapGeometryBuilder` + `SectorTriangulator` + `WallMeshBuilder`

**Files:**
- Modify: `Assets/Scripts/Map/MapGeometryBuilder.cs`
- Modify: `Assets/Scripts/Map/SectorTriangulator.cs`
- Modify: `Assets/Scripts/Map/WallMeshBuilder.cs`
- Create: `Assets/Tests/EditMode/Map/MapGeometryScaleTests.cs`

- [ ] **Step 1: Падающий scale-тест**

`Assets/Tests/EditMode/Map/MapGeometryScaleTests.cs`:

```csharp
using NUnit.Framework;

namespace Doom.Map.Tests
{
    public class MapGeometryScaleTests
    {
        /// квадрат 64×64, sector с floor=0, ceiling=128
        private static MapData Square()
        {
            var verts = new[]
            {
                new Vertex(0, 0), new Vertex(64, 0),
                new Vertex(64, 64), new Vertex(0, 64),
            };
            var lines = new[]
            {
                new LineDef(0, 1, 0, 0, 0, 0, -1),
                new LineDef(1, 2, 0, 0, 0, 1, -1),
                new LineDef(2, 3, 0, 0, 0, 2, -1),
                new LineDef(3, 0, 0, 0, 0, 3, -1),
            };
            var sides = new[]
            {
                new SideDef(0,0,"-","-","W",0), new SideDef(0,0,"-","-","W",0),
                new SideDef(0,0,"-","-","W",0), new SideDef(0,0,"-","-","W",0),
            };
            var sectors = new[] { new Sector(0, 128, "F", "F", 0, 0, 0) };
            return new MapData("TEST", verts, lines, sides, sectors, System.Array.Empty<Thing>());
        }

        [Test]
        public void Floor_at_worldScale_half_halves_all_vertex_coordinates()
        {
            var polys = SectorPolygonBuilder.Build(Square());
            var floor = SectorTriangulator.TriangulateFloor(Square(), polys[0], worldScale: 0.5f);

            // Все X и Z должны быть умножены на 0.5: из {0,32,64} вершин получится {0,16,32}
            foreach (var v in floor.Vertices)
            {
                Assert.That(v.X, Is.InRange(0f, 32f));
                Assert.That(v.Z, Is.InRange(0f, 32f));
                Assert.That(v.Y, Is.EqualTo(0f));  // floor=0, anything * 0.5 = 0
            }
        }

        [Test]
        public void Ceiling_at_worldScale_one_thirtysecond_matches_doom_scale_meters()
        {
            var polys = SectorPolygonBuilder.Build(Square());
            var ceiling = SectorTriangulator.TriangulateCeiling(Square(), polys[0], worldScale: 1f / 32f);

            // ceiling=128 в DOOM → 128/32 = 4 метра в Unity
            foreach (var v in ceiling.Vertices)
            {
                Assert.That(v.Y, Is.EqualTo(4f).Within(0.001f));
                Assert.That(v.X, Is.InRange(0f, 2f));  // 64/32 = 2
                Assert.That(v.Z, Is.InRange(0f, 2f));
            }
        }

        [Test]
        public void Wall_at_worldScale_half_halves_vertex_coordinates()
        {
            var walls = WallMeshBuilder.BuildForSector(Square(), 0, worldScale: 0.5f);

            Assert.That(walls.Vertices.Length, Is.EqualTo(16));  // 4 квада × 4 вершины
            foreach (var v in walls.Vertices)
            {
                Assert.That(v.X, Is.InRange(0f, 32f));
                Assert.That(v.Y, Is.InRange(0f, 64f));  // ceiling=128 * 0.5 = 64
                Assert.That(v.Z, Is.InRange(0f, 32f));
            }
        }

        [Test]
        public void MapGeometryBuilder_passes_worldScale_through()
        {
            var meshes = MapGeometryBuilder.Build(Square(), worldScale: 0.25f);
            Assert.That(meshes.Length, Is.EqualTo(1));
            // floor=0; ceiling=128 * 0.25 = 32
            // вершины пола все на Y=0
            foreach (var v in meshes[0].Floor.Vertices)
                Assert.That(v.Y, Is.EqualTo(0f));
            // вершины потолка на Y=32
            foreach (var v in meshes[0].Ceiling.Vertices)
                Assert.That(v.Y, Is.EqualTo(32f).Within(0.001f));
        }
    }
}
```

- [ ] **Step 2: Расширить signatures `SectorTriangulator`**

В `Assets/Scripts/Map/SectorTriangulator.cs` модифицировать публичные методы — добавить параметр `worldScale = 1f`:

```csharp
        public static MeshData TriangulateFloor(MapData map, SectorPolygon poly, float worldScale = 1f)
            => Triangulate(map, poly,
                           map.Sectors[poly.SectorIdx].FloorHeight * worldScale,
                           worldScale, flipWinding: true);

        public static MeshData TriangulateCeiling(MapData map, SectorPolygon poly, float worldScale = 1f)
            => Triangulate(map, poly,
                           map.Sectors[poly.SectorIdx].CeilingHeight * worldScale,
                           worldScale, flipWinding: false);

        private static MeshData Triangulate(MapData map, SectorPolygon poly,
                                            float yHeight, float worldScale, bool flipWinding)
```

В теле `Triangulate`, заменить строку конверсии vertex coords:

```csharp
                    // DOOM (X, Y) -> Unity (X, Z), Y = pre-scaled height
                    verts[i] = new Float3(p.X * worldScale, yHeight, p.Y * worldScale);
```

(`yHeight` уже умножено на scale на уровне вызова.)

- [ ] **Step 3: Расширить signature `WallMeshBuilder`**

В `Assets/Scripts/Map/WallMeshBuilder.cs`:

```csharp
        public static MeshData BuildForSector(MapData map, int sectorIdx, float worldScale = 1f)
        {
```

В теле, заменить вызовы `EmitQuad(verts, tris, v1, v2, sec.FloorHeight, sec.CeilingHeight, ...)` на:

```csharp
                if (!ld.IsTwoSided)
                {
                    if (onFront)
                        EmitQuad(verts, tris,
                                 v1, v2,
                                 sec.FloorHeight * worldScale, sec.CeilingHeight * worldScale,
                                 worldScale,
                                 facingFront: true);
                    continue;
                }
                ...
                if (other.FloorHeight > sec.FloorHeight)
                {
                    EmitQuad(verts, tris,
                             v1, v2,
                             sec.FloorHeight * worldScale, other.FloorHeight * worldScale,
                             worldScale,
                             facingFront: onFront);
                }
                if (other.CeilingHeight < sec.CeilingHeight)
                {
                    EmitQuad(verts, tris,
                             v1, v2,
                             other.CeilingHeight * worldScale, sec.CeilingHeight * worldScale,
                             worldScale,
                             facingFront: onFront);
                }
```

И сама `EmitQuad`:

```csharp
        private static void EmitQuad(List<Float3> verts, List<int> tris,
                                     Vertex a, Vertex b, float yLow, float yHigh,
                                     float worldScale,
                                     bool facingFront)
        {
            int baseIdx = verts.Count;
            float ax = a.X * worldScale, az = a.Y * worldScale;
            float bx = b.X * worldScale, bz = b.Y * worldScale;
            if (facingFront)
            {
                verts.Add(new Float3(bx, yLow,  bz));
                verts.Add(new Float3(ax, yLow,  az));
                verts.Add(new Float3(ax, yHigh, az));
                verts.Add(new Float3(bx, yHigh, bz));
            }
            else
            {
                verts.Add(new Float3(ax, yLow,  az));
                verts.Add(new Float3(bx, yLow,  bz));
                verts.Add(new Float3(bx, yHigh, bz));
                verts.Add(new Float3(ax, yHigh, az));
            }
            tris.Add(baseIdx + 0); tris.Add(baseIdx + 2); tris.Add(baseIdx + 1);
            tris.Add(baseIdx + 0); tris.Add(baseIdx + 3); tris.Add(baseIdx + 2);
        }
```

(`yLow` и `yHigh` приходят уже пре-масштабированными.)

- [ ] **Step 4: Расширить `MapGeometryBuilder.Build`**

В `Assets/Scripts/Map/MapGeometryBuilder.cs`:

```csharp
        public static SectorMeshes[] Build(MapData map, float worldScale = 1f)
        {
            var polys = SectorPolygonBuilder.Build(map);
            var result = new SectorMeshes[map.Sectors.Length];
            for (int s = 0; s < map.Sectors.Length; s++)
            {
                var floor   = SectorTriangulator.TriangulateFloor(map, polys[s], worldScale);
                var ceiling = SectorTriangulator.TriangulateCeiling(map, polys[s], worldScale);
                var walls   = WallMeshBuilder.BuildForSector(map, s, worldScale);
                result[s] = new SectorMeshes(s, floor, ceiling, walls);
            }
            return result;
        }
```

- [ ] **Step 5: Прогнать — 4 новых теста pass, Stage 2 тесты тоже pass**

Ожидаемое: 70 тестов pass (66 + 4 scale tests). Stage 2 SectorTriangulator/WallMeshBuilder тесты используют default `worldScale = 1f`, поведение не меняется.

- [ ] **Step 6: Закоммитить**

```powershell
git add Assets/Scripts/Map/MapGeometryBuilder.cs `
        Assets/Scripts/Map/SectorTriangulator.cs `
        Assets/Scripts/Map/WallMeshBuilder.cs `
        Assets/Tests/EditMode/Map/MapGeometryScaleTests.cs `
        Assets/Tests/EditMode/Map/MapGeometryScaleTests.cs.meta
git commit -m "Stage 3: plumb worldScale through MapGeometryBuilder pipeline"
```

---

### Task 5: Freedoom integration — E1M1 имеет player start

**Files:**
- Modify: `Assets/Tests/EditMode/Map/MapFreedoomTests.cs`

- [ ] **Step 1: Добавить тест**

Дописать в `MapFreedoomTests.cs`:

```csharp
        [Test]
        public void E1M1_has_player_start_in_things()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var map = MapData.Load(wad, "E1M1");

            Assert.That(map.Things.Length, Is.GreaterThan(0),
                "E1M1 должна содержать THINGS");
            int playerStartCount = 0;
            foreach (var t in map.Things)
            {
                if (t.Type == 1) playerStartCount++;
            }
            Assert.That(playerStartCount, Is.EqualTo(1),
                "E1M1 содержит ровно один Player 1 start");
        }
```

- [ ] **Step 2: Прогнать — pass**

Ожидаемое: 71 тест pass.

- [ ] **Step 3: Закоммитить**

```powershell
git add Assets/Tests/EditMode/Map/MapFreedoomTests.cs
git commit -m "Stage 3: integration test — E1M1 has a Player 1 start in THINGS"
```

---

### Task 6: `PlayerController` MonoBehaviour

**Files:**
- Modify: `Assets/Scripts/MapBuild/Doom.MapBuild.asmdef`
- Create: `Assets/Scripts/MapBuild/PlayerController.cs`

- [ ] **Step 1: Добавить ссылку на Input System в asmdef**

В `Assets/Scripts/MapBuild/Doom.MapBuild.asmdef`:

```json
{
    "name": "Doom.MapBuild",
    "rootNamespace": "Doom.MapBuild",
    "references": ["Doom.Wad", "Doom.Map", "Unity.InputSystem"],
    ...
}
```

(Добавить `"Unity.InputSystem"` к существующим references — это имя asmdef из Input System package.)

- [ ] **Step 2: Реализовать `PlayerController`**

`Assets/Scripts/MapBuild/PlayerController.cs`:

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

namespace Doom.MapBuild
{
    /// FPS-контроллер: WASD walk/strafe, мышь mouselook (yaw на root,
    /// pitch на cameraPivot, clamp ±85°), Shift run (hold), no jump, no crouch.
    /// Input Actions собираются программно — без asset-файла.
    [AddComponentMenu("Doom/Player Controller")]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("Movement (m/s)")]
        [SerializeField] float walkSpeed = 6.25f;   // 200 DOOM units/s × (1/32)
        [SerializeField] float runSpeed  = 13.75f;  // 440 DOOM units/s × (1/32)
        [SerializeField] float gravity   = -9.81f;
        [SerializeField] float groundStickSpeed = -2f;

        [Header("Look")]
        [SerializeField] float mouseSensitivity = 0.1f;  // degrees per pixel
        [SerializeField] float pitchLimit = 85f;
        [SerializeField] Transform cameraPivot;

        InputAction moveAction;
        InputAction lookAction;
        InputAction sprintAction;
        InputActionMap playerMap;

        CharacterController cc;
        float pitch;
        float verticalVelocity;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            BuildInputActions();
        }

        void OnEnable()
        {
            playerMap.Enable();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void OnDisable()
        {
            playerMap.Disable();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void OnDestroy()
        {
            playerMap?.Dispose();
        }

        void BuildInputActions()
        {
            playerMap = new InputActionMap("Player");

            moveAction = playerMap.AddAction("Move",
                InputActionType.Value, expectedControlType: "Vector2");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up",    "<Keyboard>/w")
                .With("Down",  "<Keyboard>/s")
                .With("Left",  "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            lookAction = playerMap.AddAction("Look",
                InputActionType.Value, "<Mouse>/delta", expectedControlType: "Vector2");

            sprintAction = playerMap.AddAction("Sprint",
                InputActionType.Button, "<Keyboard>/leftShift");
        }

        void Update()
        {
            ApplyLook();
            ApplyMovement();
        }

        void ApplyLook()
        {
            if (cameraPivot == null) return;
            Vector2 look = lookAction.ReadValue<Vector2>() * mouseSensitivity;
            // yaw на root
            transform.Rotate(0f, look.x, 0f);
            // pitch на cameraPivot (отрицательный знак: вверх мыши = look up)
            pitch = Mathf.Clamp(pitch - look.y, -pitchLimit, pitchLimit);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        void ApplyMovement()
        {
            Vector2 move = moveAction.ReadValue<Vector2>();
            bool sprint = sprintAction.IsPressed();
            float speed = sprint ? runSpeed : walkSpeed;

            Vector3 horizontal = (transform.forward * move.y + transform.right * move.x) * speed;

            if (cc.isGrounded && verticalVelocity < 0f)
                verticalVelocity = groundStickSpeed;
            verticalVelocity += gravity * Time.deltaTime;

            cc.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);
        }
    }
}
```

- [ ] **Step 3: Прогнать тесты — компиляция должна пройти**

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -nographics -projectPath "D:\Development\doom" `
    -runTests -testPlatform EditMode `
    -testResults "D:\Development\doom\Logs\test-results.xml" `
    -logFile -
```

Ожидаемое: 71 тест pass без новых (PlayerController сам не тестируется — он будет проверен PlayMode-тестом в Task 8). Главное — компилируется.

Если компиляция падает на «`Unity.InputSystem` not found» — Input System package не успел импортироваться; запустить Unity вручную один раз и/или проверить `Library/PackageCache/com.unity.inputsystem@*`.

- [ ] **Step 4: Закоммитить**

```powershell
git add Assets/Scripts/MapBuild/Doom.MapBuild.asmdef `
        Assets/Scripts/MapBuild/PlayerController.cs `
        Assets/Scripts/MapBuild/PlayerController.cs.meta
git commit -m "Stage 3: PlayerController MonoBehaviour with hand-built Input Actions"
```

---

### Task 7: `MapLoader` — `worldScale` + спавн Player в стартовой позиции

**Files:**
- Modify: `Assets/Scripts/MapBuild/MapLoader.cs`

- [ ] **Step 1: Добавить `worldScale` поле и инстанциировать игрока**

Заменить тело `MapLoader.cs` на новую версию, ключевые изменения:

1. SerializeField `float worldScale = 1f / 32f;`
2. `MapGeometryBuilder.Build(map, worldScale)`
3. После постройки геометрии — найти `Thing.Type == 1`, инстанцировать Player GameObject с CharacterController + Camera + PlayerController, поставить в (start.X × scale, bounds.max.y + 5, start.Y × scale) с yaw = 90 − doomAngle.
4. Отключить `autoFitCamera`-кодпуть когда игрок есть (камера — у игрока).

Полный новый `MapLoader.cs`:

```csharp
using System.IO;
using UnityEngine;
using Doom.Wad;
using Doom.Map;

namespace Doom.MapBuild
{
    [AddComponentMenu("Doom/Map Loader")]
    public sealed class MapLoader : MonoBehaviour
    {
        [Tooltip("Путь к WAD относительно StreamingAssets")]
        [SerializeField] string wadRelativePath = "wads/freedoom1.wad";

        [Tooltip("Имя карты (ExMy для DOOM 1, MAPxx для DOOM 2)")]
        [SerializeField] string mapName = "E1M1";

        [Tooltip("Коэффициент: DOOM unit × worldScale = Unity meter. 1/32 → player ~1.75m")]
        [SerializeField] float worldScale = 1f / 32f;

        [SerializeField] Material floorMaterial;
        [SerializeField] Material ceilingMaterial;
        [SerializeField] Material wallMaterial;

        void Start()
        {
            MapLog.WarningHandler += OnWarning;
            MapLog.ErrorHandler += OnError;
            try { Build(); }
            finally
            {
                MapLog.WarningHandler -= OnWarning;
                MapLog.ErrorHandler -= OnError;
            }
        }

        void Build()
        {
            string path = Path.Combine(Application.streamingAssetsPath, wadRelativePath);
            if (!File.Exists(path))
            {
                Debug.LogError($"MapLoader: WAD not found at {path}");
                return;
            }

            using var wad = WadFile.Open(path);
            var map = MapData.Load(wad, mapName);
            Debug.Log($"MapLoader: loaded {map.Name} — " +
                      $"{map.Vertexes.Length} verts, {map.LineDefs.Length} lines, " +
                      $"{map.Sectors.Length} sectors, {map.Things.Length} things");

            var root = new GameObject(map.Name);
            root.transform.SetParent(transform, worldPositionStays: false);

            var meshes = MapGeometryBuilder.Build(map, worldScale);
            Bounds? bounds = null;
            int builtSectors = 0;
            foreach (var sm in meshes)
            {
                if (!sm.HasAnyGeometry) continue;
                var go = new GameObject($"Sector_{sm.SectorIdx}");
                go.transform.SetParent(root.transform, worldPositionStays: false);
                AddChild(go, "Floor",   sm.Floor,   floorMaterial,   ref bounds);
                AddChild(go, "Ceiling", sm.Ceiling, ceilingMaterial, ref bounds);
                AddChild(go, "Walls",   sm.Walls,   wallMaterial,    ref bounds);
                builtSectors++;
            }
            Debug.Log($"MapLoader: built {builtSectors}/{meshes.Length} sectors");

            SpawnPlayer(map, bounds);
        }

        void SpawnPlayer(MapData map, Bounds? bounds)
        {
            Thing? start = null;
            foreach (var t in map.Things)
            {
                if (t.Type == 1) { start = t; break; }
            }
            Vector3 pos;
            float yaw;
            if (start.HasValue)
            {
                pos = new Vector3(start.Value.X * worldScale,
                                  (bounds?.max.y ?? 0f) + 5f,
                                  start.Value.Y * worldScale);
                yaw = 90f - start.Value.Angle;
            }
            else
            {
                Debug.LogWarning("MapLoader: no Player 1 start in THINGS; spawning at (0, top, 0)");
                pos = new Vector3(0f, (bounds?.max.y ?? 0f) + 5f, 0f);
                yaw = 0f;
            }

            // Удалить дефолтную Main Camera (если есть в сцене) — у игрока своя
            var existingMain = Camera.main;
            if (existingMain != null && existingMain.gameObject.GetComponent<PlayerController>() == null)
            {
                Destroy(existingMain.gameObject);
            }

            var player = new GameObject("Player");
            player.transform.SetParent(transform, worldPositionStays: false);
            player.transform.position = pos;
            player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // CharacterController: DOOM player size × worldScale
            var cc = player.AddComponent<CharacterController>();
            cc.height = 56f * worldScale;
            cc.radius = 16f * worldScale;
            cc.stepOffset = 24f * worldScale;
            cc.slopeLimit = 45f;
            cc.center = new Vector3(0f, cc.height * 0.5f, 0f);

            // Камера на eye-height (41 DOOM unit от ног)
            var cameraGO = new GameObject("PlayerCamera");
            cameraGO.transform.SetParent(player.transform, worldPositionStays: false);
            cameraGO.transform.localPosition = new Vector3(0f, 41f * worldScale, 0f);
            cameraGO.tag = "MainCamera";
            var cam = cameraGO.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 2000f;
            cam.fieldOfView = 75f;
            cameraGO.AddComponent<AudioListener>();

            // Контроллер последним — он считывает CharacterController в Awake
            var pc = player.AddComponent<PlayerController>();
            // SerializeField cameraPivot — приватный, поэтому MapLoader зовёт
            // PlayerController.SetCameraPivot (добавим в Step 2 ниже).
            pc.SetCameraPivot(cameraGO.transform);
        }

        void AddChild(GameObject parent, string name, MeshData data,
                      Material material, ref Bounds? bounds)
        {
            if (data == null || data.IsEmpty) return;

            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, worldPositionStays: false);

            var mesh = new Mesh();
            mesh.name = $"{parent.name}/{name}";
            mesh.indexFormat = data.Vertices.Length > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            var unityVerts = new Vector3[data.Vertices.Length];
            for (int i = 0; i < unityVerts.Length; i++)
                unityVerts[i] = new Vector3(
                    data.Vertices[i].X,
                    data.Vertices[i].Y,
                    data.Vertices[i].Z);
            mesh.vertices  = unityVerts;
            mesh.triangles = data.Triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            child.AddComponent<MeshFilter>().sharedMesh   = mesh;
            child.AddComponent<MeshRenderer>().sharedMaterial = material;
            child.AddComponent<MeshCollider>().sharedMesh  = mesh;

            var b = mesh.bounds;
            bounds = bounds.HasValue ? Combine(bounds.Value, b) : b;
        }

        static Bounds Combine(Bounds a, Bounds b) { a.Encapsulate(b); return a; }

        void OnWarning(string msg) => Debug.LogWarning($"[Doom.Map] {msg}");
        void OnError(string msg)   => Debug.LogError  ($"[Doom.Map] {msg}");

        // ─── Auto-bootstrap ────────────────────────────────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBootstrap()
        {
            if (FindAnyObjectByType<MapLoader>() != null) return;
            var go = new GameObject("MapLoader (auto)");
            var loader = go.AddComponent<MapLoader>();
            loader.floorMaterial   = CreateBlockoutMaterial(new Color(0.227f, 0.227f, 0.227f));
            loader.ceilingMaterial = CreateBlockoutMaterial(new Color(0.333f, 0.333f, 0.333f));
            loader.wallMaterial    = CreateBlockoutMaterial(new Color(0.502f, 0.502f, 0.502f));
        }

        static Material CreateBlockoutMaterial(Color color)
        {
            var m = new Material(Shader.Find("Standard"));
            m.color = color;
            return m;
        }
    }
}
```

Несколько ключевых изменений vs Stage 2:
- Удалён `autoFitCamera` и `FitCamera` — больше не нужен, камера у игрока.
- Передаётся `worldScale` в `MapGeometryBuilder.Build`.
- `AddChild` больше не использует `worldScale` напрямую (вершины уже умножены в MeshData), но `mesh.bounds` теперь в Unity-метрах, не в DOOM-единицах.
- Новый `SpawnPlayer` метод.

- [ ] **Step 2: Добавить `SetCameraPivot` в `PlayerController`**

В `Assets/Scripts/MapBuild/PlayerController.cs`, после `OnDestroy`, добавить:

```csharp
        public void SetCameraPivot(Transform pivot) => cameraPivot = pivot;
```

- [ ] **Step 3: Прогнать EditMode-тесты — должны pass (PlayerController + MapLoader компилируются, тесты не зависят от Play)**

Ожидаемое: 71 тест pass.

- [ ] **Step 4: Закоммитить**

```powershell
git add Assets/Scripts/MapBuild/MapLoader.cs `
        Assets/Scripts/MapBuild/PlayerController.cs
git commit -m "Stage 3: MapLoader spawns Player at THINGS Type 1 start with CharacterController + camera"
```

---

### Task 8: PlayMode тест — игрок приземляется на пол

**Files:**
- Create: `Assets/Tests/PlayMode/Doom.Stage3.PlayTests.asmdef`
- Create: `Assets/Tests/PlayMode/PlayerLandsOnFloorTests.cs`

- [ ] **Step 1: Создать PlayMode asmdef**

`Assets/Tests/PlayMode/Doom.Stage3.PlayTests.asmdef`:

```json
{
    "name": "Doom.Stage3.PlayTests",
    "rootNamespace": "Doom.Stage3.PlayTests",
    "references": [
        "Doom.Wad",
        "Doom.Map",
        "Doom.MapBuild",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "Unity.InputSystem"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"],
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "versionDefines": [],
    "noEngineReferences": false
}
```

`includePlatforms: []` без `["Editor"]` — это PlayMode, она компилируется и в редакторе, и в плеере. Если оставить только `["Editor"]`, PlayMode-runner может её не подхватить.

- [ ] **Step 2: Написать PlayMode тест**

`Assets/Tests/PlayMode/PlayerLandsOnFloorTests.cs`:

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Doom.MapBuild;

namespace Doom.Stage3.PlayTests
{
    public class PlayerLandsOnFloorTests
    {
        [UnityTest]
        public IEnumerator Player_lands_on_floor_at_E1M1_start()
        {
            // Пустая сцена — MapLoader.AutoBootstrap создаст GameObject сам
            // (в PlayMode AutoBootstrap фaйрит на AfterSceneLoad новой сцены)
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);

            // Ждём пока сцена грузится и MapLoader.Start выполнится
            // На моей машине билд E1M1 ≈ 100ms, дадим запас
            for (int i = 0; i < 60; i++) yield return null;

            var player = GameObject.Find("Player");
            Assert.That(player, Is.Not.Null, "Player GameObject должен существовать после MapLoader.Build");

            float initialY = player.transform.position.y;
            var cc = player.GetComponent<CharacterController>();
            Assert.That(cc, Is.Not.Null, "Player должен иметь CharacterController");

            // Ещё 90 кадров (≈1.5 сек) на падение и устаканивание на полу
            for (int i = 0; i < 90; i++) yield return null;

            float landedY = player.transform.position.y;

            Assert.That(landedY, Is.LessThan(initialY),
                "Player должен был упасть с высоты по gravity");
            Assert.That(landedY, Is.GreaterThan(-200f),
                "Player улетел в void — что-то с коллайдерами не так");
            Assert.That(cc.isGrounded, Is.True,
                "Player должен стоять на полу (cc.isGrounded)");
        }
    }
}
```

- [ ] **Step 3: Прогнать PlayMode тест**

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -projectPath "D:\Development\doom" `
    -runTests -testPlatform PlayMode `
    -testResults "D:\Development\doom\Logs\playmode-results.xml" `
    -logFile -
```

Ожидаемое: exit 0, 1 PlayMode test pass. Прогон занимает 30-90 сек (запуск Unity + загрузка сцены + игра 150 кадров).

NB: PlayMode CLI **без** `-nographics`. PhysX работает в batch-mode без gfx (на Windows), но `Camera.Render` не вызывается — для нашего теста не нужно (мы проверяем физику, не рендер).

Если падает «scene Stage2_MapPreview not found in build settings» — добавить сцену в BuildSettings вручную или через `EditorBuildSettings.scenes = ...` API. Подсказка: см. `ProjectSettings/EditorBuildSettings.asset`, скорее всего там сейчас пусто. Добавить:

```yaml
EditorBuildSettings:
  serializedVersion: 2
  m_Scenes:
  - enabled: 1
    path: Assets/Scenes/Stage2_MapPreview.unity
    guid: <GUID из Stage2_MapPreview.unity.meta>
  ...
```

(Это в файле `ProjectSettings/EditorBuildSettings.asset`. GUID — это значение `guid:` из `Assets/Scenes/Stage2_MapPreview.unity.meta`.)

- [ ] **Step 4: Закоммитить**

```powershell
git add Assets/Tests/PlayMode `
        ProjectSettings/EditorBuildSettings.asset
git commit -m "Stage 3: PlayMode test — player lands on floor at E1M1 start"
```

---

### Task 9: Ручная верификация + обновить план + CLAUDE.md

**Files:**
- Modify: `docs/doom-unity-remake-plan.md`
- Modify: `CLAUDE.md`

- [ ] **Step 1: Ручная верификация в Unity Editor**

1. Открыть Unity Editor: `Unity.exe -projectPath D:\Development\doom`.
2. Открыть `Assets/Scenes/Stage2_MapPreview.unity`.
3. Нажать Play.
4. Проверить:
   - В Console: `MapLoader: loaded E1M1 — 1196 verts, ... 182 sectors, N things`.
   - В Console: `MapLoader: built 182/182 sectors`.
   - В Game view: вид от первого лица, ноги на полу первой комнаты E1M1.
   - WASD двигает игрока вперёд/назад/влево/вправо.
   - Мышь крутит камеру (горизонталь + вертикаль).
   - Shift ускоряет до ~13 м/с (заметно быстрее walk).
   - Cursor залочен в центре окна; Esc возвращает курсор.
   - Стены не пропускают (попробовать вбежать в стену — игрок останавливается).
   - Двери/проходы в соседние сектора с разной высотой пола работают (CC.stepOffset = 0.75 м, DOOM step = 24 unit / 32 = 0.75 м — должен сам залезать на ступеньку).

Если что-то не так — починить **на месте**, прогнать заново. Записать в отчёте что было не так.

- [ ] **Step 2: Отметить Этап 3 в `docs/doom-unity-remake-plan.md`**

Заменить заголовок:

```markdown
## Этап 3. Игрок и коллизии
```

на:

```markdown
## Этап 3. Игрок и коллизии ✅
```

Все три bullet'а превратить в чекбоксы:

```markdown
- [x] Прочитать стартовую позицию игрока из `THINGS`.
- [x] Настроить управление от первого лица (мышь + клавиши) через новый Input System.
- [x] Использовать `CharacterController` и меш-коллайдеры на геометрии для столкновений.
```

Заменить «Результат этапа» на:

```markdown
**Результат этапа:** `MapLoader` автоматически спавнит игрока в Player 1 Start (`Thing.Type == 1`) на сцене `Stage2_MapPreview`; WASD + мышь + Shift для бега. `CharacterController` (1.75 м × 0.5 м) использует `MeshCollider`-ы Stage 2 для столкновений; gravity = -9.81 m/s² работает естественно благодаря `worldScale = 1/32`. Покрыт 71 EditMode + 1 PlayMode тестом. Подробный план реализации — `docs/stage-3-player-plan.md`.
```

- [ ] **Step 3: Обновить `CLAUDE.md`**

В секции **Project status**, заголовок «Stages 0, 1, and 2 of the plan are done:» → «Stages 0, 1, 2, and 3 of the plan are done:» (или формулировка по тексту). Добавить пункт:

```markdown
- **Stage 3:** Player + collisions. `MapLoader` spawns Player from `THINGS` Type 1 at scene start; `PlayerController` MonoBehaviour (`Doom.MapBuild`) uses the new Input System (`com.unity.inputsystem`, hand-built `InputActionMap`) for WASD + mouse-look + Shift-sprint. `worldScale = 1/32` plumbed through `MapGeometryBuilder` so player is ~1.75 m tall, gravity = -9.81 m/s² works natively. PlayMode test asserts player lands on E1M1's floor without falling through.
```

Обновить forward-pointer: «Stage 4 (palette and textures) is the next concrete work».

В секции **Project layout**:
- Add `Assets/Tests/PlayMode/` entry.

```markdown
- `Assets/Tests/PlayMode/` — Unity PlayMode tests (asmdef `Doom.Stage3.PlayTests`). Runs in Play context; required for verifying `CharacterController` + collider integration.
```

В секции **Build, run, test**: добавить PlayMode CLI команду (между EditMode и dump):

```powershell
# Headless: run PlayMode tests (slower; physics needs no -nographics)
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -projectPath "D:\Development\doom" `
    -runTests -testPlatform PlayMode `
    -testResults "D:\Development\doom\Logs\playmode-results.xml" `
    -logFile "D:\Development\doom\Logs\playmode-run.log"
```

Обновить test count:

```markdown
The current test suite is 71 EditMode + 1 PlayMode tests:
- 30 from Stage 1 (WAD reader): 4 integration tests against `freedoom1.wad`, the rest unit tests on `SyntheticWadBuilder`-built blobs.
- 32 from Stage 2 (Map pipeline): 5 integration tests against `freedoom1.wad`, the rest unit on `SyntheticMapBuilder`-built lumps.
- 9 from Stage 3 EditMode (Player): THINGS parsing (+2), THINGS in Load (+2), scale plumbing (+4), Freedoom Player 1 start (+1) + 1 PlayMode test asserting player lands on E1M1 floor.
```

В **Architectural ground rules**, дописать:

```markdown
- **World scale is `1 DOOM unit × worldScale = 1 Unity meter`**, with `worldScale = 1f / 32f` as the project default. Plumbed through `MapGeometryBuilder` so `Float3` outputs are already in meters; `MapLoader` does not re-scale. Player height (56 DOOM units) becomes ~1.75 m; gravity is -9.81 m/s² natively.
```

- [ ] **Step 4: Закоммитить**

```powershell
git add docs/doom-unity-remake-plan.md CLAUDE.md
git commit -m "Stage 3 done: mark plan + CLAUDE.md"
```

---

## Definition of done

Этап считается закрытым, когда выполнено всё ниже:

1. `git log --oneline` показывает 9 коммитов с префиксом `Stage 3:` / `Stage 3 setup:` / `Stage 3 done:`.
2. Прогон `-runTests -testPlatform EditMode` возвращает exit 0; в XML 71 тест pass.
3. Прогон `-runTests -testPlatform PlayMode` возвращает exit 0; в XML 1 PlayMode test pass.
4. В Unity, открыв `Assets/Scenes/Stage2_MapPreview.unity` и нажав Play:
   - Игрок спавнится в первой комнате E1M1, видна стартовая «hangar» комната Freedoom от первого лица.
   - WASD двигает по полу с расчётной DOOM-скоростью.
   - Мышь крутит камеру (полная мышь, pitch clamp).
   - Shift бежит.
   - Стены/коллайдеры работают.
5. `Doom.Map` asmdef всё ещё `noEngineReferences: true`; `THINGS` парсер живёт там.
6. Документы (`docs/doom-unity-remake-plan.md`, `CLAUDE.md`) отражают новое состояние.

## Что НЕ делается в этом этапе (и почему)

- **Visual placement в реальном секторе игрока (вместо «сверху уронить»)** — point-in-polygon на 182 секторах E1M1 — отдельная задача; gravity-drop работает и проще. Сделаем если в Stage 6 понадобится точное «начало мутации сектора».
- **Headbob, дыхание камеры, recoil** — игровые ощущения Stage 5/6/7.
- **Прыжок, присед** — изначальный DOOM не имел, не добавляем.
- **Аудио шагов, звук при ударе об стену** — Stage 6 (звуки из WAD).
- **Конфиг ребайнда** — Stage 7 (settings).
- **Менюшка escape с возвращением курсора** — частично сделано (Esc возвращает курсор через Unity автомат), полноценное Pause-menu — Stage 7.
- **Стрельба, инвентарь, здоровье** — Stage 6.
- **Парсинг THINGS-типов кроме `1`** — Stage 5/6.

## Граничные случаи и заметки для реализующего

- **Input System и старые `UnityEditor.Input` API:** мы переводим `activeInputHandler` на `Both` (значение 2), чтобы Stage 1's editor menu (`UnityEditor.MenuItem`) и любой код, использующий старый `Input.*` API, продолжал работать. Если subagent видит ошибку «old input handler is disabled» — значит ProjectSettings не применилось, перезапусти Unity.
- **`CharacterController` и gravity-drop:** при первом кадре после спавна `cc.isGrounded == false`, `verticalVelocity` начинает накапливаться. Если падение даёт `verticalVelocity * dt > stepOffset`, может проскочить пол. На 5 метрах падения с gravity -9.81 это 1 секунда падения, terminal velocity к моменту удара ~10 м/с, за один кадр 60Hz это 0.16 м — меньше `cc.height/2`, всё ок.
- **DOOM angle → Unity yaw:** конверсия `unityYaw = 90f - doomAngle`. Если игрок смотрит «не туда» при спавне — проверить знак и offset; возможно `+90` вместо `-90` в зависимости от того, как WAD интерпретирует angle.
- **PlayMode runner и `-nographics`:** не добавлять `-nographics` к PlayMode-команде. PhysX отказывается от collision callbacks без real gfx device — в результате `cc.isGrounded` всегда false, и тест падает. На Windows batch-mode без `-nographics` создаёт software/Null gfx device для рендера, но PhysX (CPU-bound) работает корректно.
- **Cursor lock:** `Cursor.lockState = CursorLockMode.Locked` залочит курсор в центре окна. В Game view при потере фокуса (Alt-Tab) Unity автоматически разлочит и потребует клик в окно. Это норма, не баг.
- **`MapLoader.SpawnPlayer` идёт после `Build` геометрии**, чтобы `bounds.max.y` уже был известен. Если у карты zero geometry (все сектора invalid), bounds.HasValue == false — спавним в (0, 5, 0) и логируем warning.
- **`MapData(name, v, l, s, sec, things)` constructor change ломает Stage 2 тесты** — в Task 3 Step 4 явно перечислены файлы которые требуют обновления (добавления `, Array.Empty<Thing>()`). Не пропусти ни один.
- **PlayMode тест без `Stage2_MapPreview` в Build Settings** — нужно добавить (Task 8 Step 3). Это однократная правка `EditorBuildSettings.asset`.
- **`com.unity.inputsystem` версия 1.11.2** — последняя стабильная под Unity 6 на момент написания плана. Если эта версия отсутствует — взять чуть свежее (1.x), `2.x` если доступна и совместима. Главное не понизить ниже 1.8 (там был API breaking change на `expectedControlType`).
