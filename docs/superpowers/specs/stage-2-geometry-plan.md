# Stage 2: Геометрия одной карты (серый блок-аут) — План реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Превратить лампы карты (`VERTEXES`, `LINEDEFS`, `SIDEDEFS`, `SECTORS`) в серую 3D-геометрию Unity, видимую в Play mode на любой карте Freedoom Phase 1 (E1M1..E4M9). Текстуры, игрок, объекты — этапы 3–4.

**Architecture:** Двухслойная разбивка. `Doom.Map` — чистый C# (`noEngineReferences: true`), парсинг + геометрические алгоритмы, на выходе плоские `MeshData`-структуры. `Doom.MapBuild` — тонкий слой с `UnityEngine`: один `MapLoader` MonoBehaviour, который превращает `MeshData` в `UnityEngine.Mesh` и инстанцирует `GameObject`-ы. Триангуляция — через [LibTessDotNet](https://github.com/speps/LibTessDotNet) (исходники в `Assets/ThirdParty/`).

**Tech Stack:** Unity 6000.4.8f1, C# / .NET, Unity Test Framework (NUnit EditMode), LibTessDotNet v1.1.15 (SGI Free Software License B 2.0), `Doom.Wad` API из Stage 1.

---

## Контекст

Stage 1 закрыт: `Doom.Wad` умеет открыть WAD, перечислить directory, читать произвольные лампы по имени/индексу. Stage 2 надстраивает над этим уровень структурированных данных карты + генерацию Unity-мешей.

Архитектурные правила проекта (`CLAUDE.md`), важные для Stage 2:
- **BSP-данные не используются.** Лампы `NODES`, `SSECTORS`, `SEGS`, `BLOCKMAP`, `REJECT` игнорируются полностью. Видимостью и физикой занимается Unity. Мы парсим только сырую геометрию: `VERTEXES`, `LINEDEFS`, `SIDEDEFS`, `SECTORS`.
- **Числа в WAD little-endian** — `BinaryReader` подходит как есть.
- **Не портируем C-движок** — пишем алгоритмы с нуля (включая half-edge сборку контуров сектора).
- **Никакого собственного контента.** Если кажется, что нужна ручная карта/меш — это сигнал перечитать спеку WAD.

Дизайн-решения, зафиксированные на брейншторме:
- **Время сборки:** runtime, через MonoBehaviour `MapLoader` на `Start()`.
- **Имя карты:** настраиваемое поле `MapName` (по умолчанию `E1M1`).
- **Масштаб:** 1 DOOM-единица = 1 Unity-метр (без масштабирования).
- **Координаты:** DOOM (X, Y, Z) → Unity (X, Z, Y) — поднимаем DOOM-овую плоскость XY в Unity-овую XZ, сектор-героты ложатся на Y.
- **Триангуляция:** LibTessDotNet (исходники под `Assets/ThirdParty/`).
- **Коллайдеры:** `MeshCollider` навешивается уже сейчас — пригодится в Stage 3.
- **Иерархия:** `GameObject` на сектор, под ним три ребёнка `Floor` / `Ceiling` / `Walls`.
- **Тесты:** unit на каждый парсер + алгоритм через `SyntheticWadBuilder`, плюс интеграционный сьют на `freedoom1.wad`.
- **Ошибки:** «лог + skip» там, где это имеет смысл. Жёсткий бросок только если карта целиком не парсится.

## Формат лампов карты (краткая справка)

Карта в IWAD'е — это маркер-лампа (`E1M1` / `MAP01`, размер 0), за которой идут лампы геометрии в каноническом порядке:

```
[E1M1]      ← маркер, size = 0
[THINGS]    ← объекты (Stage 3, в этом этапе пропускаем)
[LINEDEFS]
[SIDEDEFS]
[VERTEXES]
[SEGS]      ← BSP, не используем
[SSECTORS]  ← BSP, не используем
[NODES]     ← BSP, не используем
[SECTORS]
[REJECT]    ← BSP, не используем
[BLOCKMAP]  ← BSP, не используем
```

**`VERTEXES` — массив из записей по 4 байта:**

| Смещение | Размер | Поле | Тип |
|----------|--------|------|-----|
| 0        | 2      | `x`  | int16 LE |
| 2        | 2      | `y`  | int16 LE |

**`LINEDEFS` — массив из записей по 14 байт:**

| Смещение | Размер | Поле          | Тип       |
|----------|--------|---------------|-----------|
| 0        | 2      | `v1`          | uint16 LE — индекс начала в `VERTEXES` |
| 2        | 2      | `v2`          | uint16 LE — индекс конца |
| 4        | 2      | `flags`       | uint16 LE — битмаска (impassable, two-sided, …) |
| 6        | 2      | `specialType` | uint16 LE — поведение (door, lift, …); в Stage 2 не используем |
| 8        | 2      | `sectorTag`   | uint16 LE — тег для скриптов; в Stage 2 не используем |
| 10       | 2      | `frontSide`   | uint16 LE — индекс в `SIDEDEFS` |
| 12       | 2      | `backSide`    | uint16 LE — индекс или `0xFFFF` (= нет задней стороны) |

Битовый флаг `0x0004` = two-sided (но опираться будем на `backSide != 0xFFFF`, флаг используется чаще для рендеринга, а не для топологии).

**`SIDEDEFS` — массив из записей по 30 байт:**

| Смещение | Размер | Поле           | Тип |
|----------|--------|----------------|-----|
| 0        | 2      | `textureXOffset` | int16 LE |
| 2        | 2      | `textureYOffset` | int16 LE |
| 4        | 8      | `upperTexture`   | ASCII 8, `\0`-padded |
| 12       | 8      | `lowerTexture`   | ASCII 8, `\0`-padded |
| 20       | 8      | `middleTexture`  | ASCII 8, `\0`-padded |
| 28       | 2      | `sectorIdx`      | uint16 LE — индекс в `SECTORS` |

Строка `"-"` означает «текстуры нет». В Stage 2 текстуры не накладываем, но имена парсим — пригодится в Stage 4.

**`SECTORS` — массив из записей по 26 байт:**

| Смещение | Размер | Поле          | Тип |
|----------|--------|---------------|-----|
| 0        | 2      | `floorHeight`   | int16 LE |
| 2        | 2      | `ceilingHeight` | int16 LE |
| 4        | 8      | `floorFlat`     | ASCII 8, `\0`-padded |
| 12       | 8      | `ceilingFlat`   | ASCII 8, `\0`-padded |
| 20       | 2      | `lightLevel`    | uint16 LE |
| 22       | 2      | `specialType`   | uint16 LE |
| 24       | 2      | `tag`           | uint16 LE |

В Stage 2 берём только `floorHeight` и `ceilingHeight`. Остальное парсим, в геометрии не используем.

## Конвенции сторон linedef-а

Ключевой момент для сборки контуров сектора:

- **Front sidedef** живёт **справа** от вектора `V1 → V2`.
- **Back sidedef** (если есть) — **слева**.

Для сектора, видимого из front sidedef-а: чтобы обходить контур сектора **CCW** (counter-clockwise, против часовой), edge нужно направить так, чтобы сектор был **слева**. Front-сектор справа от `V1→V2`, значит для CCW-обхода используем направление `V2→V1`. Back-сектор слева от `V1→V2`, для CCW-обхода — `V1→V2`.

Это правило — фундамент алгоритма `SectorPolygonBuilder` (Task 6).

## Координатная трансформация

DOOM коорды → Unity коорды:

```
unityX = doomX
unityY = doomZ  (sector floor/ceiling height)
unityZ = doomY
```

Намотка треугольников:
- Полу нормаль смотрит вверх (`+Y` Unity). LibTess по умолчанию выдаёт CCW в DOOM-овой XY-плоскости (= CCW из (+Z) в DOOM = CCW из (+Y) в Unity, после нашего свопа Y/Z). Это уже корректная намотка для пола.
- Потолку нормаль смотрит вниз (`-Y`). Для потолка реверсируем индексы (меняем местами V1 и V2 в каждом треугольнике).
- Стенам — две треугольника квада нумеруем так, чтобы нормаль смотрела в сторону сектора, которому стена принадлежит.

## Файловая структура

После выполнения плана:

```
Assets/Scripts/Map/
├── Doom.Map.asmdef              ← runtime assembly, без UnityEngine
├── MapData.cs                    ← Vertex/LineDef/SideDef/Sector структуры + MapData + Load(WadFile, mapName)
├── MeshData.cs                   ← Float3 struct + MeshData (vertices/indices в чистом C#)
├── SectorPolygon.cs              ← структура: outer + holes; вершинные индексы в MapData.Vertexes
├── SectorPolygonBuilder.cs       ← half-edge chaining; собирает SectorPolygon[] из MapData
├── SectorTriangulator.cs         ← обёртка над LibTessDotNet → floor/ceiling MeshData
├── WallMeshBuilder.cs            ← квады стен (one-sided, two-sided lower/upper) → MeshData
└── MapGeometryBuilder.cs         ← оркестратор; SectorMeshes[] для всей карты

Assets/Scripts/MapBuild/
├── Doom.MapBuild.asmdef          ← runtime, с UnityEngine
└── MapLoader.cs                  ← MonoBehaviour; единственное место, где MeshData → UnityEngine.Mesh

Assets/ThirdParty/LibTessDotNet/
├── LibTessDotNet.asmdef          ← noEngineReferences: true
├── LICENSE.txt                    ← SGI Free Software License B 2.0
├── Dict.cs
├── Geom.cs
├── Mesh.cs
├── MeshUtils.cs
├── PriorityHeap.cs
├── PriorityQueue.cs
├── Sweep.cs
└── Tess.cs

Assets/Materials/MapBlockOut/
├── Floor.mat                      ← Standard shader, #3A3A3A
├── Ceiling.mat                    ← Standard shader, #555555
└── Wall.mat                       ← Standard shader, #808080

Assets/Scenes/
└── Stage2_MapPreview.unity        ← Main Camera + Directional Light + MapLoader

Assets/Tests/EditMode/Map/
├── Doom.Map.Tests.asmdef
├── SyntheticMapBuilder.cs         ← билдер in-memory лампов карты для unit-тестов
├── MapDataReaderTests.cs          ← парсинг VERTEXES/LINEDEFS/SIDEDEFS/SECTORS
├── MapDataLoadTests.cs            ← MapData.Load: маркер, отсутствующие лампы, etc.
├── SectorPolygonBuilderTests.cs
├── SectorTriangulatorTests.cs
├── WallMeshBuilderTests.cs
└── MapFreedoomTests.cs            ← интеграция на freedoom1.wad / E1M1
```

## Тесты: где и как запускать

Как в Stage 1: либо через `Window > General > Test Runner` в редакторе, либо CLI:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -nographics -projectPath "D:\Development\doom" `
    -runTests -testPlatform EditMode `
    -testResults "D:\Development\doom\Logs\test-results.xml" `
    -logFile -
```

**NB:** `-quit` НЕ добавлять с `-runTests` — Unity выйдет до того, как запустит тесты (см. CLAUDE.md). Per-test PASS/FAIL уходит в XML, в editor-лог — только сводка.

---

### Task 1: Bootstrap — asmdef-ы, заглушки, пустая сцена

Каркас, чтобы Unity без ошибок компилировал новые сборки. Реальный код добавляется в следующих тасках.

**Files:**
- Create: `Assets/Scripts/Map/Doom.Map.asmdef`
- Create: `Assets/Scripts/Map/Placeholder.cs`
- Create: `Assets/Scripts/MapBuild/Doom.MapBuild.asmdef`
- Create: `Assets/Scripts/MapBuild/Placeholder.cs`
- Create: `Assets/Tests/EditMode/Map/Doom.Map.Tests.asmdef`
- Create: `Assets/Tests/EditMode/Map/MapTestsSmoke.cs`

- [ ] **Step 1: Создать `Doom.Map.asmdef`** (без `LibTessDotNet` — добавим ссылку в Task 2)

Файл `Assets/Scripts/Map/Doom.Map.asmdef`:

```json
{
    "name": "Doom.Map",
    "rootNamespace": "Doom.Map",
    "references": ["Doom.Wad"],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": true
}
```

`noEngineReferences: true` повторяет ключевое решение Stage 1: парсер не должен зависеть от Unity.

- [ ] **Step 2: Заглушка для `Doom.Map`, чтобы asmdef собрался**

`Assets/Scripts/Map/Placeholder.cs`:

```csharp
namespace Doom.Map
{
    internal static class Placeholder { }
}
```

- [ ] **Step 3: Создать `Doom.MapBuild.asmdef`** (с Unity-зависимостями)

Файл `Assets/Scripts/MapBuild/Doom.MapBuild.asmdef`:

```json
{
    "name": "Doom.MapBuild",
    "rootNamespace": "Doom.MapBuild",
    "references": ["Doom.Wad", "Doom.Map"],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 4: Заглушка для `Doom.MapBuild`**

`Assets/Scripts/MapBuild/Placeholder.cs`:

```csharp
namespace Doom.MapBuild
{
    internal static class Placeholder { }
}
```

- [ ] **Step 5: Создать test asmdef для `Doom.Map`**

Файл `Assets/Tests/EditMode/Map/Doom.Map.Tests.asmdef`:

```json
{
    "name": "Doom.Map.Tests",
    "rootNamespace": "Doom.Map.Tests",
    "references": [
        "Doom.Wad",
        "Doom.Map",
        "Doom.Wad.Tests",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": ["Editor"],
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

Ссылку на `Doom.Wad.Tests` добавляем, чтобы переиспользовать `SyntheticWadBuilder` (он `internal`, но `internal`-видимость работает в пределах одной сборки; нам нужно делать его `public` или копировать). Простейший вариант: сделать `SyntheticWadBuilder` `public`, тогда из `Doom.Map.Tests` он будет доступен через `using Doom.Wad.Tests;`.

- [ ] **Step 6: Пробросить `SyntheticWadBuilder` в `public`**

Файл `Assets/Tests/EditMode/Wad/SyntheticWadBuilder.cs`:

Заменить `internal static class SyntheticWadBuilder` на `public static class SyntheticWadBuilder`. Аналогично — `public readonly struct Lump`.

- [ ] **Step 7: Smoke-тест что инфраструктура работает**

Файл `Assets/Tests/EditMode/Map/MapTestsSmoke.cs`:

```csharp
using NUnit.Framework;

namespace Doom.Map.Tests
{
    public class MapTestsSmoke
    {
        [Test]
        public void Map_test_assembly_is_wired_up()
        {
            Assert.That(2 + 2, Is.EqualTo(4));
        }

        [Test]
        public void Can_see_SyntheticWadBuilder_from_Wad_tests()
        {
            // Если эта ассерт-цепочка компилируется, значит ссылка на Doom.Wad.Tests работает.
            var bytes = Doom.Wad.Tests.SyntheticWadBuilder.Build("IWAD",
                new[] { new Doom.Wad.Tests.SyntheticWadBuilder.Lump("X", new byte[0]) });
            Assert.That(bytes.Length, Is.GreaterThan(12));
        }
    }
}
```

- [ ] **Step 8: Создать пустую сцену для Stage 2 preview**

Открыть Unity Hub, затем проект. В Unity: `File > New Scene > Basic (Built-in)`. Сохранить как `Assets/Scenes/Stage2_MapPreview.unity`. Сцена должна содержать дефолтные `Main Camera` и `Directional Light`. Закрыть редактор после сохранения, чтобы CLI-прогон не конфликтовал с открытым проектом.

Альтернатива без UI (если Unity ещё не запускается): пропустить этот шаг здесь и создать сцену в Task 11. Помечать как «опц.».

- [ ] **Step 9: Прогнать тесты — оба smoke-теста pass**

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -nographics -projectPath "D:\Development\doom" `
    -runTests -testPlatform EditMode `
    -testResults "D:\Development\doom\Logs\test-results.xml" `
    -logFile -
```

Ожидаемое: exit 0, в XML 32 теста pass (30 Stage 1 + 2 smoke Stage 2).

- [ ] **Step 10: Закоммитить**

```powershell
git add Assets/Scripts/Map Assets/Scripts/MapBuild Assets/Tests/EditMode/Map `
        Assets/Tests/EditMode/Wad/SyntheticWadBuilder.cs Assets/Scenes
git commit -m "Stage 2 setup: Doom.Map/MapBuild asmdefs + smoke tests + empty preview scene"
```

---

### Task 2: Vendor LibTessDotNet sources

Положить исходники LibTessDotNet (MIT-совместимая SGI Free B v2.0) в репо. Версия v1.1.15 с `github.com/speps/LibTessDotNet/tree/master/LibTessDotNet/Sources`.

**Files:**
- Create: `Assets/ThirdParty/LibTessDotNet/LibTessDotNet.asmdef`
- Create: `Assets/ThirdParty/LibTessDotNet/LICENSE.txt`
- Create: `Assets/ThirdParty/LibTessDotNet/Dict.cs`
- Create: `Assets/ThirdParty/LibTessDotNet/Geom.cs`
- Create: `Assets/ThirdParty/LibTessDotNet/Mesh.cs`
- Create: `Assets/ThirdParty/LibTessDotNet/MeshUtils.cs`
- Create: `Assets/ThirdParty/LibTessDotNet/PriorityHeap.cs`
- Create: `Assets/ThirdParty/LibTessDotNet/PriorityQueue.cs`
- Create: `Assets/ThirdParty/LibTessDotNet/Sweep.cs`
- Create: `Assets/ThirdParty/LibTessDotNet/Tess.cs`
- Modify: `Assets/Scripts/Map/Doom.Map.asmdef`

- [ ] **Step 1: Скачать 8 .cs файлов из upstream**

Каждый файл — из ветки `master`, raw-URL `https://raw.githubusercontent.com/speps/LibTessDotNet/master/LibTessDotNet/Sources/<file>.cs`. Также скачать `LICENSE.txt` с той же ветки (`https://raw.githubusercontent.com/speps/LibTessDotNet/master/LICENSE.txt`).

PowerShell-команда (одна на файл):

```powershell
$base = "https://raw.githubusercontent.com/speps/LibTessDotNet/master"
$dest = "D:\Development\doom\Assets\ThirdParty\LibTessDotNet"
New-Item -ItemType Directory -Force $dest | Out-Null

$files = @("Dict.cs","Geom.cs","Mesh.cs","MeshUtils.cs",
           "PriorityHeap.cs","PriorityQueue.cs","Sweep.cs","Tess.cs")
foreach ($f in $files) {
    Invoke-WebRequest "$base/LibTessDotNet/Sources/$f" -OutFile "$dest\$f"
}
Invoke-WebRequest "$base/LICENSE.txt" -OutFile "$dest\LICENSE.txt"
```

Если нет интернета у воркера — альтернатива: воркер сообщает, что LibTess нужно скачать вручную, дальнейшее выполнение блокируется.

- [ ] **Step 2: Создать asmdef для LibTessDotNet**

Файл `Assets/ThirdParty/LibTessDotNet/LibTessDotNet.asmdef`:

```json
{
    "name": "LibTessDotNet",
    "rootNamespace": "LibTessDotNet",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": true
}
```

- [ ] **Step 3: Добавить ссылку на LibTessDotNet в `Doom.Map.asmdef`**

В `Assets/Scripts/Map/Doom.Map.asmdef` заменить `"references": ["Doom.Wad"]` на `"references": ["Doom.Wad", "LibTessDotNet"]`.

- [ ] **Step 4: Проверка, что всё компилируется**

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -nographics -projectPath "D:\Development\doom" `
    -runTests -testPlatform EditMode `
    -testResults "D:\Development\doom\Logs\test-results.xml" `
    -logFile -
```

Ожидаемое: exit 0, 32 теста pass. Если LibTess не компилируется — частая причина: какой-то из его файлов использует `System.Collections.Generic` `unsafe` или зависит от другого: тогда проверить, что все 8 файлов на месте.

- [ ] **Step 5: Закоммитить**

```powershell
git add Assets/ThirdParty Assets/Scripts/Map/Doom.Map.asmdef
git commit -m "Stage 2: vendor LibTessDotNet v1.1.15 (SGI Free Software License B)"
```

---

### Task 3: `MapData` + парсинг VERTEXES, LINEDEFS

Минимальный read-only слой над лампами карты. Два самых простых лампа в этом таске; SIDEDEFS / SECTORS — следующий таск.

**Files:**
- Modify: `Assets/Scripts/Map/Placeholder.cs` → удалить
- Create: `Assets/Scripts/Map/MapData.cs`
- Create: `Assets/Tests/EditMode/Map/SyntheticMapBuilder.cs`
- Create: `Assets/Tests/EditMode/Map/MapDataReaderTests.cs`

- [ ] **Step 1: Удалить placeholder из `Doom.Map`**

```powershell
Remove-Item Assets/Scripts/Map/Placeholder.cs, Assets/Scripts/Map/Placeholder.cs.meta
```

- [ ] **Step 2: Создать helper для синтетических лампов карты**

`Assets/Tests/EditMode/Map/SyntheticMapBuilder.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Text;
using Doom.Wad.Tests;

namespace Doom.Map.Tests
{
    /// Билдер байтовых блобов для каждого типа лампа карты,
    /// плюс упаковка их в синтетический WAD с маркером карты.
    public static class SyntheticMapBuilder
    {
        public static byte[] BuildVertexes(params (short x, short y)[] verts)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            foreach (var v in verts) { w.Write(v.x); w.Write(v.y); }
            return ms.ToArray();
        }

        public static byte[] BuildLineDefs(params (ushort v1, ushort v2, ushort flags,
                                                   ushort special, ushort tag,
                                                   ushort front, ushort back)[] lines)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            foreach (var l in lines)
            {
                w.Write(l.v1); w.Write(l.v2); w.Write(l.flags);
                w.Write(l.special); w.Write(l.tag);
                w.Write(l.front); w.Write(l.back);
            }
            return ms.ToArray();
        }

        public static byte[] BuildSideDefs(params (short tx, short ty,
                                                   string upper, string lower, string middle,
                                                   ushort sector)[] sides)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            foreach (var s in sides)
            {
                w.Write(s.tx); w.Write(s.ty);
                w.Write(EncodeName8(s.upper));
                w.Write(EncodeName8(s.lower));
                w.Write(EncodeName8(s.middle));
                w.Write(s.sector);
            }
            return ms.ToArray();
        }

        public static byte[] BuildSectors(params (short floorH, short ceilH,
                                                  string floorFlat, string ceilFlat,
                                                  ushort light, ushort special, ushort tag)[] sectors)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            foreach (var s in sectors)
            {
                w.Write(s.floorH); w.Write(s.ceilH);
                w.Write(EncodeName8(s.floorFlat));
                w.Write(EncodeName8(s.ceilFlat));
                w.Write(s.light); w.Write(s.special); w.Write(s.tag);
            }
            return ms.ToArray();
        }

        /// Собрать WAD с маркером карты + любыми переданными лампами карты.
        /// Маркер должен идти первым; остальные лампы — после.
        public static byte[] BuildMapWad(string mapName,
            byte[] vertexes = null, byte[] linedefs = null,
            byte[] sidedefs = null, byte[] sectors = null)
        {
            var lumps = new List<SyntheticWadBuilder.Lump>
            {
                new SyntheticWadBuilder.Lump(mapName, new byte[0]),
            };
            if (linedefs != null) lumps.Add(new SyntheticWadBuilder.Lump("LINEDEFS", linedefs));
            if (sidedefs != null) lumps.Add(new SyntheticWadBuilder.Lump("SIDEDEFS", sidedefs));
            if (vertexes != null) lumps.Add(new SyntheticWadBuilder.Lump("VERTEXES", vertexes));
            if (sectors != null) lumps.Add(new SyntheticWadBuilder.Lump("SECTORS", sectors));
            return SyntheticWadBuilder.Build("IWAD", lumps);
        }

        private static byte[] EncodeName8(string name)
        {
            var buf = new byte[8];
            if (string.IsNullOrEmpty(name)) return buf;
            var ascii = Encoding.ASCII.GetBytes(name);
            System.Array.Copy(ascii, buf, System.Math.Min(ascii.Length, 8));
            return buf;
        }
    }
}
```

NB: `SyntheticWadBuilder.Build` принимает `IReadOnlyList<Lump>`; убедиться, что его сигнатура совместима с `List<Lump>`. Если в Stage 1 это был `IReadOnlyList<Lump>` — `List<T>` имплементит этот интерфейс, всё ок.

- [ ] **Step 3: Падающие тесты на `Vertex` и `LineDef`**

`Assets/Tests/EditMode/Map/MapDataReaderTests.cs`:

```csharp
using NUnit.Framework;

namespace Doom.Map.Tests
{
    public class MapDataReaderTests
    {
        [Test]
        public void Parses_vertexes_into_short_x_y()
        {
            var bytes = SyntheticMapBuilder.BuildVertexes(
                (10, 20), (-30, 40), (0, 0));

            var verts = MapData.ParseVertexes(bytes);

            Assert.That(verts.Length, Is.EqualTo(3));
            Assert.That(verts[0].X, Is.EqualTo((short)10));
            Assert.That(verts[0].Y, Is.EqualTo((short)20));
            Assert.That(verts[1].X, Is.EqualTo((short)-30));
            Assert.That(verts[1].Y, Is.EqualTo((short)40));
            Assert.That(verts[2].X, Is.EqualTo((short)0));
        }

        [Test]
        public void Vertexes_lump_size_not_multiple_of_4_is_warning_not_throw()
        {
            // 4 байта = одна валидная запись, +1 «хвостовой» байт — игнорируется
            var bytes = new byte[] { 1, 0, 2, 0, 0xFF };

            var verts = MapData.ParseVertexes(bytes);

            Assert.That(verts.Length, Is.EqualTo(1));
            Assert.That(verts[0].X, Is.EqualTo((short)1));
            Assert.That(verts[0].Y, Is.EqualTo((short)2));
        }

        [Test]
        public void Parses_linedefs_into_records()
        {
            var bytes = SyntheticMapBuilder.BuildLineDefs(
                (v1: 0, v2: 1, flags: 0x0001, special: 0, tag: 0, front: 0, back: 0xFFFF),
                (v1: 1, v2: 2, flags: 0x0004, special: 0, tag: 0, front: 1, back: 2));

            var lines = MapData.ParseLineDefs(bytes);

            Assert.That(lines.Length, Is.EqualTo(2));
            Assert.That(lines[0].V1, Is.EqualTo(0));
            Assert.That(lines[0].V2, Is.EqualTo(1));
            Assert.That(lines[0].Flags, Is.EqualTo(0x0001));
            Assert.That(lines[0].FrontSideIdx, Is.EqualTo(0));
            Assert.That(lines[0].BackSideIdx, Is.EqualTo(-1));
            Assert.That(lines[1].BackSideIdx, Is.EqualTo(2));
        }
    }
}
```

- [ ] **Step 4: Прогнать — должно падать «`MapData` не определён»**

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -nographics -projectPath "D:\Development\doom" `
    -runTests -testPlatform EditMode `
    -testResults "D:\Development\doom\Logs\test-results.xml" `
    -logFile -
```

Ожидаемое: ошибки компиляции на `MapData.ParseVertexes` / `MapData.ParseLineDefs`.

- [ ] **Step 5: Реализовать структуры + `ParseVertexes` + `ParseLineDefs`**

`Assets/Scripts/Map/MapData.cs`:

```csharp
using System;
using System.IO;
using UnityEngine; // НЕТ: noEngineReferences = true. Не использовать!
```

Стоп — `Doom.Map` имеет `noEngineReferences: true`. `UnityEngine.Debug.LogWarning` использовать нельзя. Для предупреждений будем использовать `System.Console.WriteLine` или собственный канал. Самый простой и Unity-дружественный вариант — статическое событие/делегат, на который из `Doom.MapBuild` подпишемся, чтобы пробросить в `Debug.LogWarning`:

```csharp
namespace Doom.Map
{
    public static class MapLog
    {
        public static event Action<string> WarningHandler;
        public static event Action<string> ErrorHandler;

        public static void Warning(string msg) => WarningHandler?.Invoke(msg);
        public static void Error(string msg) => ErrorHandler?.Invoke(msg);
    }
}
```

В Task 10 `MapLoader` подпишет `Debug.LogWarning` и `Debug.LogError` на эти события.

Финальный `Assets/Scripts/Map/MapData.cs`:

```csharp
using System;
using System.IO;
using System.Text;

namespace Doom.Map
{
    public static class MapLog
    {
        public static event Action<string> WarningHandler;
        public static event Action<string> ErrorHandler;

        public static void Warning(string msg) => WarningHandler?.Invoke(msg);
        public static void Error(string msg) => ErrorHandler?.Invoke(msg);
    }

    public readonly struct Vertex
    {
        public readonly short X;
        public readonly short Y;
        public Vertex(short x, short y) { X = x; Y = y; }
    }

    public readonly struct LineDef
    {
        public readonly int V1;
        public readonly int V2;
        public readonly ushort Flags;
        public readonly ushort Special;
        public readonly ushort Tag;
        public readonly int FrontSideIdx;
        public readonly int BackSideIdx;

        public LineDef(int v1, int v2, ushort flags, ushort special, ushort tag,
                       int front, int back)
        {
            V1 = v1; V2 = v2; Flags = flags; Special = special; Tag = tag;
            FrontSideIdx = front; BackSideIdx = back;
        }

        public bool IsTwoSided => BackSideIdx >= 0;
    }

    public static class MapData
    {
        private const int VertexSize  = 4;
        private const int LineDefSize = 14;

        public static Vertex[] ParseVertexes(byte[] bytes)
        {
            if (bytes == null) return Array.Empty<Vertex>();
            int count = bytes.Length / VertexSize;
            if (bytes.Length % VertexSize != 0)
            {
                MapLog.Warning(
                    $"VERTEXES: размер {bytes.Length} не кратен {VertexSize}, " +
                    $"читаем первые {count} записей");
            }
            var verts = new Vertex[count];
            using var ms = new MemoryStream(bytes);
            using var r = new BinaryReader(ms);
            for (int i = 0; i < count; i++)
            {
                short x = r.ReadInt16();
                short y = r.ReadInt16();
                verts[i] = new Vertex(x, y);
            }
            return verts;
        }

        public static LineDef[] ParseLineDefs(byte[] bytes)
        {
            if (bytes == null) return Array.Empty<LineDef>();
            int count = bytes.Length / LineDefSize;
            if (bytes.Length % LineDefSize != 0)
            {
                MapLog.Warning(
                    $"LINEDEFS: размер {bytes.Length} не кратен {LineDefSize}, " +
                    $"читаем первые {count} записей");
            }
            var lines = new LineDef[count];
            using var ms = new MemoryStream(bytes);
            using var r = new BinaryReader(ms);
            for (int i = 0; i < count; i++)
            {
                ushort v1 = r.ReadUInt16();
                ushort v2 = r.ReadUInt16();
                ushort flags = r.ReadUInt16();
                ushort special = r.ReadUInt16();
                ushort tag = r.ReadUInt16();
                ushort front = r.ReadUInt16();
                ushort back = r.ReadUInt16();
                lines[i] = new LineDef(
                    v1, v2, flags, special, tag,
                    front: front,
                    back: back == 0xFFFF ? -1 : back);
            }
            return lines;
        }
    }
}
```

- [ ] **Step 6: Прогнать — 3 теста pass + остальные**

Ожидаемое: 35 теста pass (32 уже было + 3 новых).

- [ ] **Step 7: Закоммитить**

```powershell
git add Assets/Scripts/Map/MapData.cs Assets/Scripts/Map/MapData.cs.meta `
        Assets/Tests/EditMode/Map/SyntheticMapBuilder.cs `
        Assets/Tests/EditMode/Map/SyntheticMapBuilder.cs.meta `
        Assets/Tests/EditMode/Map/MapDataReaderTests.cs `
        Assets/Tests/EditMode/Map/MapDataReaderTests.cs.meta
git rm Assets/Scripts/Map/Placeholder.cs Assets/Scripts/Map/Placeholder.cs.meta
git commit -m "Stage 2: parse VERTEXES and LINEDEFS lumps"
```

---

### Task 4: парсинг SIDEDEFS, SECTORS

Аналогичный таск для оставшихся двух лампов. Структура та же: тест → парсер → коммит.

**Files:**
- Modify: `Assets/Scripts/Map/MapData.cs`
- Modify: `Assets/Tests/EditMode/Map/MapDataReaderTests.cs`

- [ ] **Step 1: Падающие тесты на `SideDef` и `Sector`**

Дописать в `MapDataReaderTests.cs`:

```csharp
        [Test]
        public void Parses_sidedefs_with_texture_names_and_sector_idx()
        {
            var bytes = SyntheticMapBuilder.BuildSideDefs(
                (tx: 10, ty: 20, upper: "BRICK1", lower: "-", middle: "WALL01",
                 sector: 5));

            var sides = MapData.ParseSideDefs(bytes);

            Assert.That(sides.Length, Is.EqualTo(1));
            Assert.That(sides[0].TextureXOffset, Is.EqualTo((short)10));
            Assert.That(sides[0].TextureYOffset, Is.EqualTo((short)20));
            Assert.That(sides[0].UpperTexture, Is.EqualTo("BRICK1"));
            Assert.That(sides[0].LowerTexture, Is.EqualTo("-"));
            Assert.That(sides[0].MiddleTexture, Is.EqualTo("WALL01"));
            Assert.That(sides[0].SectorIdx, Is.EqualTo(5));
        }

        [Test]
        public void Parses_sectors_with_heights()
        {
            var bytes = SyntheticMapBuilder.BuildSectors(
                (floorH: 0, ceilH: 128, floorFlat: "FLAT1", ceilFlat: "F_SKY1",
                 light: 192, special: 0, tag: 0),
                (floorH: -16, ceilH: 64, floorFlat: "BLOOD1", ceilFlat: "CEIL1",
                 light: 96, special: 9, tag: 12));

            var sectors = MapData.ParseSectors(bytes);

            Assert.That(sectors.Length, Is.EqualTo(2));
            Assert.That(sectors[0].FloorHeight, Is.EqualTo((short)0));
            Assert.That(sectors[0].CeilingHeight, Is.EqualTo((short)128));
            Assert.That(sectors[0].FloorFlat, Is.EqualTo("FLAT1"));
            Assert.That(sectors[0].CeilingFlat, Is.EqualTo("F_SKY1"));
            Assert.That(sectors[0].LightLevel, Is.EqualTo(192));
            Assert.That(sectors[1].FloorHeight, Is.EqualTo((short)-16));
            Assert.That(sectors[1].Tag, Is.EqualTo(12));
        }
```

- [ ] **Step 2: Прогнать — компиляция падает**

Ожидаемое: ошибки про `SideDef`, `Sector`, `MapData.ParseSideDefs`, `MapData.ParseSectors`.

- [ ] **Step 3: Дописать структуры и парсеры в `MapData.cs`**

В namespace `Doom.Map`:

```csharp
    public readonly struct SideDef
    {
        public readonly short TextureXOffset;
        public readonly short TextureYOffset;
        public readonly string UpperTexture;
        public readonly string LowerTexture;
        public readonly string MiddleTexture;
        public readonly int SectorIdx;

        public SideDef(short tx, short ty, string upper, string lower, string middle,
                       int sectorIdx)
        {
            TextureXOffset = tx; TextureYOffset = ty;
            UpperTexture = upper; LowerTexture = lower; MiddleTexture = middle;
            SectorIdx = sectorIdx;
        }
    }

    public readonly struct Sector
    {
        public readonly short FloorHeight;
        public readonly short CeilingHeight;
        public readonly string FloorFlat;
        public readonly string CeilingFlat;
        public readonly ushort LightLevel;
        public readonly ushort Special;
        public readonly ushort Tag;

        public Sector(short floorH, short ceilH, string floorFlat, string ceilFlat,
                      ushort light, ushort special, ushort tag)
        {
            FloorHeight = floorH; CeilingHeight = ceilH;
            FloorFlat = floorFlat; CeilingFlat = ceilFlat;
            LightLevel = light; Special = special; Tag = tag;
        }
    }
```

В `class MapData`:

```csharp
        private const int SideDefSize = 30;
        private const int SectorSize  = 26;

        public static SideDef[] ParseSideDefs(byte[] bytes)
        {
            if (bytes == null) return Array.Empty<SideDef>();
            int count = bytes.Length / SideDefSize;
            if (bytes.Length % SideDefSize != 0)
                MapLog.Warning(
                    $"SIDEDEFS: размер {bytes.Length} не кратен {SideDefSize}, " +
                    $"читаем первые {count} записей");
            var sides = new SideDef[count];
            using var ms = new MemoryStream(bytes);
            using var r = new BinaryReader(ms);
            for (int i = 0; i < count; i++)
            {
                short tx = r.ReadInt16();
                short ty = r.ReadInt16();
                string upper = ReadName8(r);
                string lower = ReadName8(r);
                string middle = ReadName8(r);
                ushort sector = r.ReadUInt16();
                sides[i] = new SideDef(tx, ty, upper, lower, middle, sector);
            }
            return sides;
        }

        public static Sector[] ParseSectors(byte[] bytes)
        {
            if (bytes == null) return Array.Empty<Sector>();
            int count = bytes.Length / SectorSize;
            if (bytes.Length % SectorSize != 0)
                MapLog.Warning(
                    $"SECTORS: размер {bytes.Length} не кратен {SectorSize}, " +
                    $"читаем первые {count} записей");
            var sectors = new Sector[count];
            using var ms = new MemoryStream(bytes);
            using var r = new BinaryReader(ms);
            for (int i = 0; i < count; i++)
            {
                short floorH = r.ReadInt16();
                short ceilH = r.ReadInt16();
                string floorFlat = ReadName8(r);
                string ceilFlat = ReadName8(r);
                ushort light = r.ReadUInt16();
                ushort special = r.ReadUInt16();
                ushort tag = r.ReadUInt16();
                sectors[i] = new Sector(floorH, ceilH, floorFlat, ceilFlat,
                                        light, special, tag);
            }
            return sectors;
        }

        private static string ReadName8(BinaryReader r)
        {
            var raw = r.ReadBytes(8);
            int end = raw.Length;
            for (int i = 0; i < raw.Length; i++)
                if (raw[i] == 0) { end = i; break; }
            return Encoding.ASCII.GetString(raw, 0, end);
        }
```

- [ ] **Step 4: Прогнать — все тесты pass**

Ожидаемое: 37 теста pass (35 + 2 новых).

- [ ] **Step 5: Закоммитить**

```powershell
git add Assets/Scripts/Map/MapData.cs Assets/Tests/EditMode/Map/MapDataReaderTests.cs
git commit -m "Stage 2: parse SIDEDEFS and SECTORS lumps"
```

---

### Task 5: `MapData.Load` — поиск маркера + сборка карты

Высокоуровневый API: один вызов даёт всю карту. Бросаем при отсутствии маркера или обязательных лампов.

**Files:**
- Modify: `Assets/Scripts/Map/MapData.cs`
- Create: `Assets/Tests/EditMode/Map/MapDataLoadTests.cs`

- [ ] **Step 1: Падающие тесты**

`Assets/Tests/EditMode/Map/MapDataLoadTests.cs`:

```csharp
using System.IO;
using NUnit.Framework;
using Doom.Wad;

namespace Doom.Map.Tests
{
    public class MapDataLoadTests
    {
        [Test]
        public void Loads_map_with_all_four_lumps()
        {
            var wadBytes = SyntheticMapBuilder.BuildMapWad(
                "E1M1",
                vertexes: SyntheticMapBuilder.BuildVertexes((0, 0), (64, 0), (64, 64), (0, 64)),
                linedefs: SyntheticMapBuilder.BuildLineDefs(
                    (0, 1, 0, 0, 0, 0, 0xFFFF),
                    (1, 2, 0, 0, 0, 1, 0xFFFF),
                    (2, 3, 0, 0, 0, 2, 0xFFFF),
                    (3, 0, 0, 0, 0, 3, 0xFFFF)),
                sidedefs: SyntheticMapBuilder.BuildSideDefs(
                    (0, 0, "-", "-", "WALL01", 0),
                    (0, 0, "-", "-", "WALL01", 0),
                    (0, 0, "-", "-", "WALL01", 0),
                    (0, 0, "-", "-", "WALL01", 0)),
                sectors: SyntheticMapBuilder.BuildSectors(
                    (0, 128, "FLAT01", "F_SKY1", 192, 0, 0)));

            using var wad = new WadFile(new MemoryStream(wadBytes), ownsStream: true);
            var map = Doom.Map.MapData.Load(wad, "E1M1");

            Assert.That(map.Vertexes.Length, Is.EqualTo(4));
            Assert.That(map.LineDefs.Length, Is.EqualTo(4));
            Assert.That(map.SideDefs.Length, Is.EqualTo(4));
            Assert.That(map.Sectors.Length, Is.EqualTo(1));
            Assert.That(map.Name, Is.EqualTo("E1M1"));
        }

        [Test]
        public void Throws_when_map_marker_missing()
        {
            var wadBytes = Doom.Wad.Tests.SyntheticWadBuilder.Build("IWAD", new[]
            {
                new Doom.Wad.Tests.SyntheticWadBuilder.Lump("PLAYPAL", new byte[10]),
            });

            using var wad = new WadFile(new MemoryStream(wadBytes), ownsStream: true);

            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
                () => Doom.Map.MapData.Load(wad, "E1M1"));
        }

        [Test]
        public void Throws_when_marker_is_not_a_map_name()
        {
            var wadBytes = Doom.Wad.Tests.SyntheticWadBuilder.Build("IWAD", new[]
            {
                new Doom.Wad.Tests.SyntheticWadBuilder.Lump("PLAYPAL", new byte[10]),
            });

            using var wad = new WadFile(new MemoryStream(wadBytes), ownsStream: true);

            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
                () => Doom.Map.MapData.Load(wad, "PLAYPAL"));
        }

        [Test]
        public void Throws_when_required_lump_missing()
        {
            // маркер есть, VERTEXES — нет
            var wadBytes = SyntheticMapBuilder.BuildMapWad(
                "E1M1",
                vertexes: null,
                linedefs: SyntheticMapBuilder.BuildLineDefs((0, 1, 0, 0, 0, 0, 0xFFFF)),
                sidedefs: SyntheticMapBuilder.BuildSideDefs(
                    (0, 0, "-", "-", "W", 0)),
                sectors: SyntheticMapBuilder.BuildSectors(
                    (0, 128, "F", "F", 0, 0, 0)));

            using var wad = new WadFile(new MemoryStream(wadBytes), ownsStream: true);

            var ex = Assert.Throws<InvalidDataException>(
                () => Doom.Map.MapData.Load(wad, "E1M1"));
            StringAssert.Contains("VERTEXES", ex.Message);
        }
    }
}
```

- [ ] **Step 2: Прогнать — падает «`MapData.Load` не найден»**

- [ ] **Step 3: Реализовать `MapData.Load` + контейнер `MapData` (instance)**

Сейчас `MapData` — это `static class` с парсерами. Превращаем в hybrid: static-парсеры остаются, плюс instance-объект с массивами. Можно либо переименовать static-методы в `MapLumps.Parse*` и оставить `MapData` для instance, либо ввести вложенный класс. Я выберу: оставить static-парсеры, но добавить публичные поля экземпляра и фабрику `Load`. Структура `class MapData`:

В `MapData.cs` модифицировать:

```csharp
    public sealed class MapData
    {
        public string Name { get; }
        public Vertex[] Vertexes { get; }
        public LineDef[] LineDefs { get; }
        public SideDef[] SideDefs { get; }
        public Sector[] Sectors { get; }

        public MapData(string name,
                       Vertex[] vertexes, LineDef[] linedefs,
                       SideDef[] sidedefs, Sector[] sectors)
        {
            Name = name;
            Vertexes = vertexes;
            LineDefs = linedefs;
            SideDefs = sidedefs;
            Sectors = sectors;
        }

        public static MapData Load(Doom.Wad.WadFile wad, string mapName)
        {
            int markerIdx = wad.FindLump(mapName);
            if (markerIdx < 0 || !Doom.Wad.WadMapNames.IsMapMarker(mapName))
            {
                throw new System.Collections.Generic.KeyNotFoundException(
                    $"Map '{mapName}' not found in WAD (or not a valid map name)");
            }

            // Ищем 4 обязательных лампа в окне [markerIdx+1, markerIdx+10].
            // Канонический порядок: THINGS, LINEDEFS, SIDEDEFS, VERTEXES, SEGS,
            // SSECTORS, NODES, SECTORS, REJECT, BLOCKMAP.
            // Делаем поиск по имени, чтобы не зависеть от порядка строго.
            const int Window = 10;
            int end = System.Math.Min(markerIdx + Window, wad.Directory.Count - 1);

            byte[] vertexBytes = null, lineBytes = null, sideBytes = null, sectorBytes = null;
            for (int i = markerIdx + 1; i <= end; i++)
            {
                // Если встретили следующий маркер карты — окно карты закончилось
                if (Doom.Wad.WadMapNames.IsMapMarker(wad.Directory[i].Name)) break;
                switch (wad.Directory[i].Name)
                {
                    case "VERTEXES": vertexBytes = wad.ReadLump(i); break;
                    case "LINEDEFS": lineBytes   = wad.ReadLump(i); break;
                    case "SIDEDEFS": sideBytes   = wad.ReadLump(i); break;
                    case "SECTORS":  sectorBytes = wad.ReadLump(i); break;
                }
            }

            RequireLump(mapName, "VERTEXES", vertexBytes);
            RequireLump(mapName, "LINEDEFS", lineBytes);
            RequireLump(mapName, "SIDEDEFS", sideBytes);
            RequireLump(mapName, "SECTORS",  sectorBytes);

            return new MapData(
                mapName,
                ParseVertexes(vertexBytes),
                ParseLineDefs(lineBytes),
                ParseSideDefs(sideBytes),
                ParseSectors(sectorBytes));
        }

        private static void RequireLump(string mapName, string lumpName, byte[] bytes)
        {
            if (bytes == null)
                throw new InvalidDataException(
                    $"Map '{mapName}' missing required lump '{lumpName}'");
        }

        // ... static ParseVertexes / ParseLineDefs / ParseSideDefs / ParseSectors / ReadName8 / consts ...
    }
```

NB: чтобы static-парсеры остались доступными из вложенного контекста — переносим их внутрь того же `class MapData` (теперь не `static class`, а `sealed class` с public static методами).

- [ ] **Step 4: Прогнать — все 4 теста pass**

Ожидаемое: 41 теста pass.

- [ ] **Step 5: Закоммитить**

```powershell
git add Assets/Scripts/Map/MapData.cs Assets/Tests/EditMode/Map/MapDataLoadTests.cs
git commit -m "Stage 2: MapData.Load — find marker and assemble four lumps"
```

---

### Task 6: `SectorPolygonBuilder` — half-edge chaining

Самый сложный таск этапа: восстановить замкнутые контуры (outer + holes) каждого сектора из набора linedef-ов.

**Files:**
- Create: `Assets/Scripts/Map/SectorPolygon.cs`
- Create: `Assets/Scripts/Map/SectorPolygonBuilder.cs`
- Create: `Assets/Tests/EditMode/Map/SectorPolygonBuilderTests.cs`

- [ ] **Step 1: Падающие тесты на 4 топологии**

`Assets/Tests/EditMode/Map/SectorPolygonBuilderTests.cs`:

```csharp
using NUnit.Framework;

namespace Doom.Map.Tests
{
    public class SectorPolygonBuilderTests
    {
        /// квадрат 64×64, CCW, 1 sector
        private static MapData SquareRoom()
        {
            var verts = new[]
            {
                new Vertex(0, 0), new Vertex(64, 0),
                new Vertex(64, 64), new Vertex(0, 64),
            };
            // 4 linedef'а по периметру, front = 0,1,2,3, back = -1
            var lines = new[]
            {
                new LineDef(0, 1, 0, 0, 0, 0, -1),
                new LineDef(1, 2, 0, 0, 0, 1, -1),
                new LineDef(2, 3, 0, 0, 0, 2, -1),
                new LineDef(3, 0, 0, 0, 0, 3, -1),
            };
            var sides = new[]
            {
                new SideDef(0, 0, "-", "-", "W", 0),
                new SideDef(0, 0, "-", "-", "W", 0),
                new SideDef(0, 0, "-", "-", "W", 0),
                new SideDef(0, 0, "-", "-", "W", 0),
            };
            var sectors = new[] { new Sector(0, 128, "F", "F", 0, 0, 0) };
            return new MapData("TEST", verts, lines, sides, sectors);
        }

        [Test]
        public void Square_room_yields_one_outer_contour_of_4_vertices()
        {
            var polys = SectorPolygonBuilder.Build(SquareRoom());

            Assert.That(polys.Length, Is.EqualTo(1));
            Assert.That(polys[0].IsValid, Is.True);
            Assert.That(polys[0].Outer.Count, Is.EqualTo(4));
            Assert.That(polys[0].Holes.Count, Is.EqualTo(0));
        }

        [Test]
        public void Square_room_outer_contour_is_CCW_in_doom_xy()
        {
            var polys = SectorPolygonBuilder.Build(SquareRoom());
            // Сектор слева от front-стороны → CCW
            // Vertex order должен идти CCW в DOOM-овой XY (signed area > 0)
            double area = SignedArea(SquareRoom().Vertexes, polys[0].Outer);
            Assert.That(area, Is.GreaterThan(0));
        }

        [Test]
        public void Two_adjacent_rooms_share_one_twoSided_linedef()
        {
            // (0,0)-(64,0)-(128,0)-(128,64)-(64,64)-(0,64), 2 sector'а слева/справа
            // Общая линия (64,0)→(64,64) — twoSided
            var verts = new[]
            {
                new Vertex(0, 0), new Vertex(64, 0), new Vertex(128, 0),
                new Vertex(128, 64), new Vertex(64, 64), new Vertex(0, 64),
            };
            // sector 0 = левая комната, sector 1 = правая
            // SideDef[0..3] = sector 0 walls (CCW: 0→1, 1→4, 4→5, 5→0)
            // SideDef[4..7] = sector 1 walls (CCW: 1→2, 2→3, 3→4, 4→1)
            // SideDef[8] = front (sector 1) shared, SideDef[9] = back (sector 0) shared
            var sides = new[]
            {
                new SideDef(0,0,"-","-","W",0), new SideDef(0,0,"-","-","W",0),
                new SideDef(0,0,"-","-","W",0), new SideDef(0,0,"-","-","W",0),
                new SideDef(0,0,"-","-","W",1), new SideDef(0,0,"-","-","W",1),
                new SideDef(0,0,"-","-","W",1), new SideDef(0,0,"-","-","W",1),
                new SideDef(0,0,"-","-","W",1), new SideDef(0,0,"-","-","W",0),
            };
            var lines = new[]
            {
                // sector 0 boundary (front sidedef = 0..3, back = -1)
                new LineDef(0, 1, 0, 0, 0, 0, -1),
                new LineDef(4, 5, 0, 0, 0, 2, -1),
                new LineDef(5, 0, 0, 0, 0, 3, -1),
                // sector 1 boundary
                new LineDef(1, 2, 0, 0, 0, 4, -1),
                new LineDef(2, 3, 0, 0, 0, 5, -1),
                new LineDef(3, 4, 0, 0, 0, 6, -1),
                // shared linedef — V1=1, V2=4, front (right) = sector 1, back (left) = sector 0
                new LineDef(1, 4, 0, 0, 0, 8, 9),
            };
            var sectors = new[]
            {
                new Sector(0, 128, "F", "F", 0, 0, 0),
                new Sector(0, 128, "F", "F", 0, 0, 0),
            };
            var map = new MapData("TEST", verts, lines, sides, sectors);

            var polys = SectorPolygonBuilder.Build(map);

            Assert.That(polys.Length, Is.EqualTo(2));
            Assert.That(polys[0].IsValid, Is.True);
            Assert.That(polys[1].IsValid, Is.True);
            Assert.That(polys[0].Outer.Count, Is.EqualTo(4));
            Assert.That(polys[1].Outer.Count, Is.EqualTo(4));
        }

        [Test]
        public void Sector_with_pillar_has_outer_and_one_hole()
        {
            // Внешняя 128×128 комната + центральная 32×32 колонна (отдельный sector с потолок=пол=0 — «закрыт»)
            // Для теста полигон-билдера достаточно того, что у внешнего сектора будет
            // один outer + один hole контур.
            var verts = new[]
            {
                // outer (0..3)
                new Vertex(0, 0), new Vertex(128, 0),
                new Vertex(128, 128), new Vertex(0, 128),
                // inner pillar (4..7) — CCW в его собственной плоскости,
                // но в outer-секторе они обходятся CW (это «дырка»)
                new Vertex(48, 48), new Vertex(80, 48),
                new Vertex(80, 80), new Vertex(48, 80),
            };
            // 4 sidedefs для outer (sector 0), 4 для pillar's front (sector 1), 4 для pillar's back (sector 0)
            var sides = new[]
            {
                // outer walls — sector 0
                new SideDef(0,0,"-","-","W",0), new SideDef(0,0,"-","-","W",0),
                new SideDef(0,0,"-","-","W",0), new SideDef(0,0,"-","-","W",0),
                // pillar front (внутрь pillar'а смотрит): sector 1
                new SideDef(0,0,"-","-","W",1), new SideDef(0,0,"-","-","W",1),
                new SideDef(0,0,"-","-","W",1), new SideDef(0,0,"-","-","W",1),
                // pillar back (наружу, к sector 0): sector 0
                new SideDef(0,0,"-","-","W",0), new SideDef(0,0,"-","-","W",0),
                new SideDef(0,0,"-","-","W",0), new SideDef(0,0,"-","-","W",0),
            };
            var lines = new[]
            {
                // outer (CCW)
                new LineDef(0, 1, 0, 0, 0, 0, -1),
                new LineDef(1, 2, 0, 0, 0, 1, -1),
                new LineDef(2, 3, 0, 0, 0, 2, -1),
                new LineDef(3, 0, 0, 0, 0, 3, -1),
                // pillar — twoSided. V1→V2 идёт CW для outer (то есть CCW для pillar's interior).
                // front = sector 1 (pillar inside), back = sector 0 (outer room)
                new LineDef(4, 5, 0, 0, 0, 4, 8),
                new LineDef(5, 6, 0, 0, 0, 5, 9),
                new LineDef(6, 7, 0, 0, 0, 6, 10),
                new LineDef(7, 4, 0, 0, 0, 7, 11),
            };
            var sectors = new[]
            {
                new Sector(0, 128, "F", "F", 0, 0, 0),
                new Sector(0, 0, "F", "F", 0, 0, 0),  // pillar — потолок и пол совпадают
            };
            var map = new MapData("TEST", verts, lines, sides, sectors);

            var polys = SectorPolygonBuilder.Build(map);

            Assert.That(polys.Length, Is.EqualTo(2));
            // sector 0: outer (внешняя комната) + hole (контур колонны)
            Assert.That(polys[0].Outer.Count, Is.EqualTo(4));
            Assert.That(polys[0].Holes.Count, Is.EqualTo(1));
            Assert.That(polys[0].Holes[0].Count, Is.EqualTo(4));
            // sector 1: только outer (pillar)
            Assert.That(polys[1].Outer.Count, Is.EqualTo(4));
            Assert.That(polys[1].Holes.Count, Is.EqualTo(0));
        }

        [Test]
        public void Open_contour_is_reported_as_invalid_and_logged()
        {
            // Три linedef'а: 0→1, 1→2 (2→0 отсутствует, контур не замыкается)
            var verts = new[]
            {
                new Vertex(0, 0), new Vertex(64, 0), new Vertex(64, 64),
            };
            var lines = new[]
            {
                new LineDef(0, 1, 0, 0, 0, 0, -1),
                new LineDef(1, 2, 0, 0, 0, 1, -1),
            };
            var sides = new[]
            {
                new SideDef(0,0,"-","-","W",0), new SideDef(0,0,"-","-","W",0),
            };
            var sectors = new[] { new Sector(0, 128, "F", "F", 0, 0, 0) };
            var map = new MapData("TEST", verts, lines, sides, sectors);

            string warning = null;
            System.Action<string> handler = m => warning = m;
            Doom.Map.MapLog.WarningHandler += handler;
            try
            {
                var polys = SectorPolygonBuilder.Build(map);
                Assert.That(polys[0].IsValid, Is.False);
            }
            finally { Doom.Map.MapLog.WarningHandler -= handler; }

            Assert.That(warning, Is.Not.Null);
            StringAssert.Contains("sector 0", warning.ToLowerInvariant());
        }

        // ---- helpers ----
        private static double SignedArea(Vertex[] verts, System.Collections.Generic.IReadOnlyList<int> ring)
        {
            double a = 0;
            for (int i = 0; i < ring.Count; i++)
            {
                var p0 = verts[ring[i]];
                var p1 = verts[ring[(i + 1) % ring.Count]];
                a += (double)p0.X * p1.Y - (double)p1.X * p0.Y;
            }
            return 0.5 * a;
        }
    }
}
```

- [ ] **Step 2: Прогнать — падает «`SectorPolygon` / `SectorPolygonBuilder` не определены»**

- [ ] **Step 3: Реализовать `SectorPolygon`**

`Assets/Scripts/Map/SectorPolygon.cs`:

```csharp
using System.Collections.Generic;

namespace Doom.Map
{
    public sealed class SectorPolygon
    {
        public int SectorIdx { get; }
        public bool IsValid { get; }
        public IReadOnlyList<int> Outer { get; }
        public IReadOnlyList<IReadOnlyList<int>> Holes { get; }

        public SectorPolygon(int sectorIdx, bool isValid,
                             IReadOnlyList<int> outer,
                             IReadOnlyList<IReadOnlyList<int>> holes)
        {
            SectorIdx = sectorIdx;
            IsValid = isValid;
            Outer = outer;
            Holes = holes;
        }

        public static SectorPolygon Invalid(int sectorIdx)
            => new SectorPolygon(sectorIdx, false,
                                 System.Array.Empty<int>(),
                                 System.Array.Empty<IReadOnlyList<int>>());
    }
}
```

- [ ] **Step 4: Реализовать `SectorPolygonBuilder`**

`Assets/Scripts/Map/SectorPolygonBuilder.cs`:

```csharp
using System.Collections.Generic;

namespace Doom.Map
{
    /// Восстанавливает замкнутые контуры (outer + holes) каждого сектора
    /// из linedefs/sidedefs.
    ///
    /// Алгоритм:
    /// 1. Для каждого linedef'а формируем half-edges, ориентированные так,
    ///    чтобы сектор был ВНУТРИ (слева от направления edge).
    ///    - Front sidedef → сектор справа от V1→V2 → используем V2→V1.
    ///    - Back sidedef  → сектор слева  от V1→V2 → используем V1→V2.
    /// 2. Группируем half-edges по сектору.
    /// 3. Внутри сектора chain'им: следующий edge = тот, что начинается в конце текущего.
    ///    Каждый edge используется ровно один раз. Получаем набор замкнутых loops.
    /// 4. Классифицируем loops: максимальная по площади (= по модулю signed area, CCW>0)
    ///    — outer; остальные — holes. Для дырок ожидаем CW (signed area < 0).
    public static class SectorPolygonBuilder
    {
        public static SectorPolygon[] Build(MapData map)
        {
            // edges[sector] = список half-edges (from, to) принадлежащих сектору
            var edges = new Dictionary<int, List<(int from, int to)>>();

            for (int i = 0; i < map.LineDefs.Length; i++)
            {
                var ld = map.LineDefs[i];

                if (!IsValidVertex(map, ld.V1) || !IsValidVertex(map, ld.V2))
                {
                    MapLog.Warning($"LineDef {i}: vertex out of range, пропускаем");
                    continue;
                }

                if (ld.FrontSideIdx >= 0)
                {
                    int sec = SafeSectorOf(map, ld.FrontSideIdx, i);
                    if (sec >= 0) Push(edges, sec, ld.V2, ld.V1);
                }
                if (ld.BackSideIdx >= 0)
                {
                    int sec = SafeSectorOf(map, ld.BackSideIdx, i);
                    if (sec >= 0) Push(edges, sec, ld.V1, ld.V2);
                }
            }

            var result = new SectorPolygon[map.Sectors.Length];
            for (int s = 0; s < map.Sectors.Length; s++)
            {
                result[s] = edges.TryGetValue(s, out var list)
                    ? BuildForSector(map, s, list)
                    : SectorPolygon.Invalid(s);
                if (!result[s].IsValid && edges.ContainsKey(s))
                {
                    MapLog.Warning($"Sector {s}: открытый контур, не удалось замкнуть полигон");
                }
            }
            return result;
        }

        private static bool IsValidVertex(MapData map, int vIdx)
            => vIdx >= 0 && vIdx < map.Vertexes.Length;

        private static int SafeSectorOf(MapData map, int sideIdx, int lineIdx)
        {
            if (sideIdx < 0 || sideIdx >= map.SideDefs.Length)
            {
                MapLog.Warning($"LineDef {lineIdx}: sidedef {sideIdx} out of range");
                return -1;
            }
            int sec = map.SideDefs[sideIdx].SectorIdx;
            if (sec < 0 || sec >= map.Sectors.Length)
            {
                MapLog.Warning($"SideDef {sideIdx}: sector {sec} out of range");
                return -1;
            }
            return sec;
        }

        private static void Push(Dictionary<int, List<(int from, int to)>> edges,
                                 int sector, int from, int to)
        {
            if (!edges.TryGetValue(sector, out var list))
            {
                list = new List<(int from, int to)>();
                edges[sector] = list;
            }
            list.Add((from, to));
        }

        private static SectorPolygon BuildForSector(
            MapData map, int sectorIdx, List<(int from, int to)> all)
        {
            // Index edges by their "from" vertex; на каждом vertex может быть >1 outgoing,
            // если sector очень тонкий или встречаются T-junction'ы. Тогда ловим warning.
            var byFrom = new Dictionary<int, List<int>>();
            for (int i = 0; i < all.Count; i++)
            {
                if (!byFrom.TryGetValue(all[i].from, out var l))
                {
                    l = new List<int>();
                    byFrom[all[i].from] = l;
                }
                l.Add(i);
            }

            var used = new bool[all.Count];
            var loops = new List<List<int>>();
            for (int i = 0; i < all.Count; i++)
            {
                if (used[i]) continue;
                var loop = new List<int>();
                int current = i;
                bool closed = false;
                while (true)
                {
                    used[current] = true;
                    loop.Add(all[current].from);
                    int next = FindNext(all, byFrom, used, all[current].to);
                    if (next == -1)
                    {
                        // Тупик. Проверяем — может уже замкнулись на старт.
                        if (all[current].to == all[i].from) closed = true;
                        break;
                    }
                    current = next;
                    if (all[current].from == all[i].from && i != current)
                    {
                        // Прошли полный круг и снова в стартовой точке.
                        // Но добавлять стартовую второй раз не надо.
                        // Тут есть тонкость: loop[0] = all[i].from = all[current].from;
                        // следующий шаг привёл бы к повторному добавлению.
                        used[current] = true;
                        closed = true;
                        break;
                    }
                }
                if (closed) loops.Add(loop);
            }

            if (loops.Count == 0)
                return SectorPolygon.Invalid(sectorIdx);

            // Классификация: максимальный по |area| — outer; остальные — holes.
            // Также проверяем, что у outer area > 0 (CCW). Если < 0 — реверсим.
            int outerIdx = 0;
            double outerAbs = 0;
            var areas = new double[loops.Count];
            for (int k = 0; k < loops.Count; k++)
            {
                areas[k] = SignedArea(map.Vertexes, loops[k]);
                double abs = areas[k] < 0 ? -areas[k] : areas[k];
                if (abs > outerAbs) { outerAbs = abs; outerIdx = k; }
            }

            var outer = loops[outerIdx];
            if (areas[outerIdx] < 0) outer.Reverse();

            var holes = new List<IReadOnlyList<int>>();
            for (int k = 0; k < loops.Count; k++)
            {
                if (k == outerIdx) continue;
                var hole = loops[k];
                // Дырки должны идти в противоположном направлении от outer (CW).
                // Если signed area > 0 (CCW) — реверсим.
                double a = SignedArea(map.Vertexes, hole);
                if (a > 0) hole.Reverse();
                holes.Add(hole);
            }

            return new SectorPolygon(sectorIdx, true, outer, holes);
        }

        private static int FindNext(List<(int from, int to)> all,
                                    Dictionary<int, List<int>> byFrom, bool[] used,
                                    int fromVertex)
        {
            if (!byFrom.TryGetValue(fromVertex, out var candidates)) return -1;
            int picked = -1;
            for (int i = 0; i < candidates.Count; i++)
            {
                int idx = candidates[i];
                if (used[idx]) continue;
                if (picked >= 0) return -1; // ambiguity — log handled at caller
                picked = idx;
            }
            return picked;
        }

        private static double SignedArea(Vertex[] verts, IReadOnlyList<int> ring)
        {
            double a = 0;
            for (int i = 0; i < ring.Count; i++)
            {
                var p0 = verts[ring[i]];
                var p1 = verts[ring[(i + 1) % ring.Count]];
                a += (double)p0.X * p1.Y - (double)p1.X * p0.Y;
            }
            return 0.5 * a;
        }
    }
}
```

- [ ] **Step 5: Прогнать тесты — все 5 pass**

При первом прогоне могут отвалиться `Two_adjacent_rooms` или `Sector_with_pillar` — это нормальная итерация. Возможные причины:
- неверное направление half-edge для front/back (проверить тесты по `SignedArea`),
- `FindNext` возвращает -1 при ambiguity, но в наших корректных тестах ambiguity быть не должно,
- классификация outer/hole разворачивает loop, а тест проверяет конкретное число вершин — это всё ещё проходит.

Если падает — отладить через `Console.WriteLine` / `MapLog.Warning` (timer test перехватывает), запустить тест из Test Runner внутри Unity.

- [ ] **Step 6: Закоммитить**

```powershell
git add Assets/Scripts/Map/SectorPolygon.cs `
        Assets/Scripts/Map/SectorPolygon.cs.meta `
        Assets/Scripts/Map/SectorPolygonBuilder.cs `
        Assets/Scripts/Map/SectorPolygonBuilder.cs.meta `
        Assets/Tests/EditMode/Map/SectorPolygonBuilderTests.cs `
        Assets/Tests/EditMode/Map/SectorPolygonBuilderTests.cs.meta
git commit -m "Stage 2: SectorPolygonBuilder — half-edge chaining + hole classification"
```

---

### Task 7: `MeshData` + `SectorTriangulator` (LibTessDotNet wrapper)

Прокладка между `SectorPolygon` и LibTess. На выходе — два `MeshData`: для пола (нормаль вверх) и потолка (нормаль вниз, индексы реверсированы).

**Files:**
- Create: `Assets/Scripts/Map/MeshData.cs`
- Create: `Assets/Scripts/Map/SectorTriangulator.cs`
- Create: `Assets/Tests/EditMode/Map/SectorTriangulatorTests.cs`

- [ ] **Step 1: `MeshData` + `Float3`**

`Assets/Scripts/Map/MeshData.cs`:

```csharp
namespace Doom.Map
{
    public readonly struct Float3
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;
        public Float3(float x, float y, float z) { X = x; Y = y; Z = z; }
    }

    public sealed class MeshData
    {
        public Float3[] Vertices { get; }
        public int[] Triangles { get; }

        public MeshData(Float3[] vertices, int[] triangles)
        {
            Vertices = vertices;
            Triangles = triangles;
        }

        public bool IsEmpty => Vertices.Length == 0 || Triangles.Length == 0;
        public static MeshData Empty { get; } =
            new MeshData(System.Array.Empty<Float3>(), System.Array.Empty<int>());
    }
}
```

- [ ] **Step 2: Падающие тесты**

`Assets/Tests/EditMode/Map/SectorTriangulatorTests.cs`:

```csharp
using NUnit.Framework;

namespace Doom.Map.Tests
{
    public class SectorTriangulatorTests
    {
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
            return new MapData("TEST", verts, lines, sides, sectors);
        }

        [Test]
        public void Square_floor_has_2_triangles_normal_up()
        {
            var map = Square();
            var polys = SectorPolygonBuilder.Build(map);

            var floor = SectorTriangulator.TriangulateFloor(map, polys[0]);

            Assert.That(floor.Triangles.Length, Is.EqualTo(6));
            // Все Y у пола = floorHeight = 0
            foreach (var v in floor.Vertices)
                Assert.That(v.Y, Is.EqualTo(0f));
            // Намотка CCW из (+Y): cross product первого треугольника должен иметь +Y компонент
            var n = TriNormal(floor.Vertices, floor.Triangles, 0);
            Assert.That(n.Y, Is.GreaterThan(0));
        }

        [Test]
        public void Square_ceiling_normal_down_indices_reversed()
        {
            var map = Square();
            var polys = SectorPolygonBuilder.Build(map);

            var ceiling = SectorTriangulator.TriangulateCeiling(map, polys[0]);

            Assert.That(ceiling.Triangles.Length, Is.EqualTo(6));
            foreach (var v in ceiling.Vertices)
                Assert.That(v.Y, Is.EqualTo(128f));
            var n = TriNormal(ceiling.Vertices, ceiling.Triangles, 0);
            Assert.That(n.Y, Is.LessThan(0));
        }

        [Test]
        public void Invalid_polygon_returns_empty_mesh()
        {
            var floor = SectorTriangulator.TriangulateFloor(
                new MapData("T", System.Array.Empty<Vertex>(),
                            System.Array.Empty<LineDef>(),
                            System.Array.Empty<SideDef>(),
                            new[] { new Sector(0, 128, "F", "F", 0, 0, 0) }),
                SectorPolygon.Invalid(0));
            Assert.That(floor.IsEmpty, Is.True);
        }

        private static Float3 TriNormal(Float3[] v, int[] t, int triIdx)
        {
            var a = v[t[triIdx*3 + 0]];
            var b = v[t[triIdx*3 + 1]];
            var c = v[t[triIdx*3 + 2]];
            float ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z;
            float wx = c.X - a.X, wy = c.Y - a.Y, wz = c.Z - a.Z;
            return new Float3(uy*wz - uz*wy, uz*wx - ux*wz, ux*wy - uy*wx);
        }
    }
}
```

- [ ] **Step 3: Прогнать — падает «`SectorTriangulator` не определён»**

- [ ] **Step 4: Реализовать `SectorTriangulator`**

`Assets/Scripts/Map/SectorTriangulator.cs`:

```csharp
using LibTessDotNet;

namespace Doom.Map
{
    public static class SectorTriangulator
    {
        public static MeshData TriangulateFloor(MapData map, SectorPolygon poly)
            => Triangulate(map, poly, map.Sectors[poly.SectorIdx].FloorHeight, flipWinding: false);

        public static MeshData TriangulateCeiling(MapData map, SectorPolygon poly)
            => Triangulate(map, poly, map.Sectors[poly.SectorIdx].CeilingHeight, flipWinding: true);

        private static MeshData Triangulate(MapData map, SectorPolygon poly,
                                            float yHeight, bool flipWinding)
        {
            if (!poly.IsValid) return MeshData.Empty;

            try
            {
                var tess = new Tess();

                AddContour(tess, map.Vertexes, poly.Outer);
                foreach (var hole in poly.Holes)
                    AddContour(tess, map.Vertexes, hole);

                tess.Tessellate(WindingRule.EvenOdd, ElementType.Polygons, 3);

                int vc = tess.VertexCount;
                int tc = tess.ElementCount;
                var verts = new Float3[vc];
                for (int i = 0; i < vc; i++)
                {
                    var p = tess.Vertices[i].Position;
                    // DOOM (X, Y) → Unity (X, Z), Y = высота
                    verts[i] = new Float3(p.X, yHeight, p.Y);
                }
                var tris = new int[tc * 3];
                for (int t = 0; t < tc; t++)
                {
                    int a = tess.Elements[t * 3 + 0];
                    int b = tess.Elements[t * 3 + 1];
                    int c = tess.Elements[t * 3 + 2];
                    if (flipWinding) { (a, c) = (c, a); }
                    tris[t * 3 + 0] = a;
                    tris[t * 3 + 1] = b;
                    tris[t * 3 + 2] = c;
                }
                return new MeshData(verts, tris);
            }
            catch (System.Exception ex)
            {
                MapLog.Error($"SectorTriangulator: sector {poly.SectorIdx} tess failed: {ex.Message}");
                return MeshData.Empty;
            }
        }

        private static void AddContour(Tess tess, Vertex[] mapVerts, System.Collections.Generic.IReadOnlyList<int> ring)
        {
            var arr = new ContourVertex[ring.Count];
            for (int i = 0; i < ring.Count; i++)
            {
                var v = mapVerts[ring[i]];
                arr[i].Position = new Vec3 { X = v.X, Y = v.Y, Z = 0 };
            }
            tess.AddContour(arr);
        }
    }
}
```

NB: проверь точное API LibTessDotNet v1.1.15 — `Vec3` лежит в namespace `LibTessDotNet`, `Tess`, `ContourVertex`, `WindingRule`, `ElementType` тоже. Если что-то не совпадает (например `Tess<float>` или namespace `LibTessDotNet.Double`) — поправить по сообщениям компилятора. Конкретно: в upstream-репо есть `LibTessDotNet/Sources/` (float-версия) и `LibTessDotNet.Double/Sources/` — нам нужна **первая**.

- [ ] **Step 5: Прогнать тесты — 3 pass**

Ожидаемое: 49 теста pass (41 + 5 polygon-builder + 3 triangulator).

- [ ] **Step 6: Закоммитить**

```powershell
git add Assets/Scripts/Map/MeshData.cs Assets/Scripts/Map/MeshData.cs.meta `
        Assets/Scripts/Map/SectorTriangulator.cs `
        Assets/Scripts/Map/SectorTriangulator.cs.meta `
        Assets/Tests/EditMode/Map/SectorTriangulatorTests.cs `
        Assets/Tests/EditMode/Map/SectorTriangulatorTests.cs.meta
git commit -m "Stage 2: SectorTriangulator via LibTessDotNet (floor up, ceiling down)"
```

---

### Task 8: `WallMeshBuilder`

Стены из linedef-ов. Один meta-mesh на сектор (все стены, принадлежащие данному сектору).

**Files:**
- Create: `Assets/Scripts/Map/WallMeshBuilder.cs`
- Create: `Assets/Tests/EditMode/Map/WallMeshBuilderTests.cs`

- [ ] **Step 1: Падающие тесты**

`Assets/Tests/EditMode/Map/WallMeshBuilderTests.cs`:

```csharp
using NUnit.Framework;

namespace Doom.Map.Tests
{
    public class WallMeshBuilderTests
    {
        [Test]
        public void OneSided_line_produces_one_quad_facing_sector()
        {
            var verts = new[] { new Vertex(0, 0), new Vertex(64, 0) };
            var lines = new[] { new LineDef(0, 1, 0, 0, 0, 0, -1) };
            var sides = new[] { new SideDef(0,0,"-","-","W",0) };
            var sectors = new[] { new Sector(0, 128, "F", "F", 0, 0, 0) };
            var map = new MapData("T", verts, lines, sides, sectors);

            var walls = WallMeshBuilder.BuildForSector(map, 0);

            // квад = 4 вершины, 6 индексов
            Assert.That(walls.Vertices.Length, Is.EqualTo(4));
            Assert.That(walls.Triangles.Length, Is.EqualTo(6));
            // все Y в диапазоне [0, 128]
            foreach (var v in walls.Vertices)
            {
                Assert.That(v.Y, Is.InRange(0f, 128f));
            }
        }

        [Test]
        public void TwoSided_line_with_no_height_diff_produces_no_steps()
        {
            // Front и back сектора имеют одинаковые floor/ceiling — никаких step'ов
            var verts = new[] { new Vertex(0, 0), new Vertex(64, 0) };
            var lines = new[] { new LineDef(0, 1, 0, 0, 0, 0, 1) };
            var sides = new[]
            {
                new SideDef(0,0,"-","-","-",0),
                new SideDef(0,0,"-","-","-",1),
            };
            var sectors = new[]
            {
                new Sector(0, 128, "F", "F", 0, 0, 0),
                new Sector(0, 128, "F", "F", 0, 0, 0),
            };
            var map = new MapData("T", verts, lines, sides, sectors);

            var wallsA = WallMeshBuilder.BuildForSector(map, 0);
            var wallsB = WallMeshBuilder.BuildForSector(map, 1);

            Assert.That(wallsA.Triangles.Length, Is.EqualTo(0));
            Assert.That(wallsB.Triangles.Length, Is.EqualTo(0));
        }

        [Test]
        public void TwoSided_with_floor_step_emits_lower_quad_for_lower_sector()
        {
            // Sector 0: floor=0, ceil=128; sector 1: floor=32, ceil=128
            // Из sector 0 видна ступень (lower) высотой 32. Из sector 1 — ничего (его пол выше).
            var verts = new[] { new Vertex(0, 0), new Vertex(64, 0) };
            var lines = new[] { new LineDef(0, 1, 0, 0, 0, 0, 1) };
            var sides = new[]
            {
                new SideDef(0,0,"-","-","-",0),
                new SideDef(0,0,"-","-","-",1),
            };
            var sectors = new[]
            {
                new Sector(0, 128, "F", "F", 0, 0, 0),
                new Sector(32, 128, "F", "F", 0, 0, 0),
            };
            var map = new MapData("T", verts, lines, sides, sectors);

            var wallsA = WallMeshBuilder.BuildForSector(map, 0);
            var wallsB = WallMeshBuilder.BuildForSector(map, 1);

            // Sector 0 (нижний) видит lower-step: 1 квад
            Assert.That(wallsA.Triangles.Length, Is.EqualTo(6));
            // Sector 1 (верхний) свою сторону пола не видит (его пол выше) — 0 квадов
            Assert.That(wallsB.Triangles.Length, Is.EqualTo(0));
        }
    }
}
```

- [ ] **Step 2: Прогнать — падает на отсутствии `WallMeshBuilder`**

- [ ] **Step 3: Реализовать `WallMeshBuilder`**

`Assets/Scripts/Map/WallMeshBuilder.cs`:

```csharp
using System.Collections.Generic;

namespace Doom.Map
{
    public static class WallMeshBuilder
    {
        /// Собирает все стены, видимые из данного сектора:
        /// - для каждой смежной линии смотрим, с какой стороны мы находимся (front/back)
        /// - one-sided: один квад от floor до ceiling нашего сектора
        /// - two-sided: lower-step (если соседский пол выше нашего)
        ///              + upper-step (если соседский потолок ниже нашего)
        ///              middle (текстуры в Stage 4 — пропускаем)
        public static MeshData BuildForSector(MapData map, int sectorIdx)
        {
            var verts = new List<Float3>();
            var tris  = new List<int>();
            var sec = map.Sectors[sectorIdx];

            for (int i = 0; i < map.LineDefs.Length; i++)
            {
                var ld = map.LineDefs[i];
                if (!IsValidVertex(map, ld.V1) || !IsValidVertex(map, ld.V2)) continue;

                bool onFront = ld.FrontSideIdx >= 0 &&
                               ld.FrontSideIdx < map.SideDefs.Length &&
                               map.SideDefs[ld.FrontSideIdx].SectorIdx == sectorIdx;
                bool onBack  = ld.BackSideIdx  >= 0 &&
                               ld.BackSideIdx  < map.SideDefs.Length &&
                               map.SideDefs[ld.BackSideIdx].SectorIdx == sectorIdx;

                if (!onFront && !onBack) continue;

                var v1 = map.Vertexes[ld.V1];
                var v2 = map.Vertexes[ld.V2];

                // One-sided: одна сторона = один квад во весь объём своего сектора
                if (!ld.IsTwoSided)
                {
                    if (onFront)
                        EmitQuad(verts, tris,
                                 v1, v2, sec.FloorHeight, sec.CeilingHeight,
                                 facingFront: true);
                    // Back-сторона на one-sided не бывает по дефиниции — игнорируем
                    continue;
                }

                // Two-sided: вычисляем сосед
                int otherSec = -1;
                if (onFront && ld.BackSideIdx >= 0)
                    otherSec = map.SideDefs[ld.BackSideIdx].SectorIdx;
                else if (onBack && ld.FrontSideIdx >= 0)
                    otherSec = map.SideDefs[ld.FrontSideIdx].SectorIdx;
                if (otherSec < 0 || otherSec >= map.Sectors.Length) continue;
                var other = map.Sectors[otherSec];

                // Lower step: соседский пол выше нашего → стена от sec.Floor до other.Floor
                if (other.FloorHeight > sec.FloorHeight)
                {
                    EmitQuad(verts, tris,
                             v1, v2, sec.FloorHeight, other.FloorHeight,
                             facingFront: onFront);
                }
                // Upper step: соседский потолок ниже нашего → стена от other.Ceiling до sec.Ceiling
                if (other.CeilingHeight < sec.CeilingHeight)
                {
                    EmitQuad(verts, tris,
                             v1, v2, other.CeilingHeight, sec.CeilingHeight,
                             facingFront: onFront);
                }
            }

            return new MeshData(verts.ToArray(), tris.ToArray());
        }

        private static void EmitQuad(List<Float3> verts, List<int> tris,
                                     Vertex a, Vertex b, float yLow, float yHigh,
                                     bool facingFront)
        {
            // a, b — DOOM XY. Unity: X = a.X, Z = a.Y.
            // Квад с углами (a, low), (b, low), (b, high), (a, high).
            // Нормаль: front sidedef справа от a→b. Чтобы нормаль смотрела в front-sector —
            // обходим против часовой при взгляде со стороны front'а.
            // Для facingFront=true: видна со стороны front (справа от a→b) — порядок CCW из (+normal):
            //   (b,low), (a,low), (a,high), (b,high)
            // Для facingFront=false (стена принадлежит back-сектору): противоположный порядок:
            //   (a,low), (b,low), (b,high), (a,high)
            int baseIdx = verts.Count;
            if (facingFront)
            {
                verts.Add(new Float3(b.X, yLow,  b.Y));
                verts.Add(new Float3(a.X, yLow,  a.Y));
                verts.Add(new Float3(a.X, yHigh, a.Y));
                verts.Add(new Float3(b.X, yHigh, b.Y));
            }
            else
            {
                verts.Add(new Float3(a.X, yLow,  a.Y));
                verts.Add(new Float3(b.X, yLow,  b.Y));
                verts.Add(new Float3(b.X, yHigh, b.Y));
                verts.Add(new Float3(a.X, yHigh, a.Y));
            }
            tris.Add(baseIdx + 0); tris.Add(baseIdx + 2); tris.Add(baseIdx + 1);
            tris.Add(baseIdx + 0); tris.Add(baseIdx + 3); tris.Add(baseIdx + 2);
        }

        private static bool IsValidVertex(MapData map, int idx)
            => idx >= 0 && idx < map.Vertexes.Length;
    }
}
```

- [ ] **Step 4: Прогнать — все 3 теста pass**

Ожидаемое: 52 теста pass.

- [ ] **Step 5: Закоммитить**

```powershell
git add Assets/Scripts/Map/WallMeshBuilder.cs `
        Assets/Scripts/Map/WallMeshBuilder.cs.meta `
        Assets/Tests/EditMode/Map/WallMeshBuilderTests.cs `
        Assets/Tests/EditMode/Map/WallMeshBuilderTests.cs.meta
git commit -m "Stage 2: WallMeshBuilder — one-sided + two-sided lower/upper steps"
```

---

### Task 9: `MapGeometryBuilder` — оркестратор

Связывает `SectorPolygonBuilder` + `SectorTriangulator` + `WallMeshBuilder` в один проход. Возвращает массив `SectorMeshes`, индексированный sector idx.

**Files:**
- Create: `Assets/Scripts/Map/MapGeometryBuilder.cs`

- [ ] **Step 1: Реализовать**

`Assets/Scripts/Map/MapGeometryBuilder.cs`:

```csharp
namespace Doom.Map
{
    public sealed class SectorMeshes
    {
        public int SectorIdx { get; }
        public MeshData Floor { get; }
        public MeshData Ceiling { get; }
        public MeshData Walls { get; }

        public SectorMeshes(int sectorIdx, MeshData floor, MeshData ceiling, MeshData walls)
        {
            SectorIdx = sectorIdx;
            Floor = floor;
            Ceiling = ceiling;
            Walls = walls;
        }

        public bool HasAnyGeometry =>
            !Floor.IsEmpty || !Ceiling.IsEmpty || !Walls.IsEmpty;
    }

    public static class MapGeometryBuilder
    {
        public static SectorMeshes[] Build(MapData map)
        {
            var polys = SectorPolygonBuilder.Build(map);
            var result = new SectorMeshes[map.Sectors.Length];
            for (int s = 0; s < map.Sectors.Length; s++)
            {
                var floor   = SectorTriangulator.TriangulateFloor(map, polys[s]);
                var ceiling = SectorTriangulator.TriangulateCeiling(map, polys[s]);
                var walls   = WallMeshBuilder.BuildForSector(map, s);
                result[s] = new SectorMeshes(s, floor, ceiling, walls);
            }
            return result;
        }
    }
}
```

Тестов отдельно не пишем — это тонкий wrapper. Покрытие даст интеграционный тест в Task 11.

- [ ] **Step 2: Прогнать — компиляция должна пройти, тесты pass**

Ожидаемое: 52 теста pass (без изменений).

- [ ] **Step 3: Закоммитить**

```powershell
git add Assets/Scripts/Map/MapGeometryBuilder.cs `
        Assets/Scripts/Map/MapGeometryBuilder.cs.meta
git commit -m "Stage 2: MapGeometryBuilder — combine sector polygons, tess, walls"
```

---

### Task 10: `MapLoader` MonoBehaviour + материалы + сцена

Единственный таск с Unity-зависимостями. Превращает `MeshData` в `UnityEngine.Mesh`, развешивает по `GameObject`-ам, подписывает `Debug.Log*` на `MapLog`.

**Files:**
- Modify: `Assets/Scripts/MapBuild/Placeholder.cs` → удалить
- Create: `Assets/Scripts/MapBuild/MapLoader.cs`
- Create: `Assets/Materials/MapBlockOut/Floor.mat`
- Create: `Assets/Materials/MapBlockOut/Ceiling.mat`
- Create: `Assets/Materials/MapBlockOut/Wall.mat`
- Modify: `Assets/Scenes/Stage2_MapPreview.unity`

- [ ] **Step 1: Удалить placeholder из `Doom.MapBuild`**

```powershell
Remove-Item Assets/Scripts/MapBuild/Placeholder.cs, Assets/Scripts/MapBuild/Placeholder.cs.meta
```

- [ ] **Step 2: Реализовать `MapLoader`**

`Assets/Scripts/MapBuild/MapLoader.cs`:

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

        [SerializeField] Material floorMaterial;
        [SerializeField] Material ceilingMaterial;
        [SerializeField] Material wallMaterial;

        [Tooltip("После загрузки переместить Main Camera в центр карты сверху")]
        [SerializeField] bool autoFitCamera = true;

        void Start()
        {
            MapLog.WarningHandler += OnWarning;
            MapLog.ErrorHandler += OnError;
            try
            {
                Build();
            }
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
                      $"{map.Sectors.Length} sectors");

            var root = new GameObject(map.Name);
            root.transform.SetParent(transform, worldPositionStays: false);

            var meshes = MapGeometryBuilder.Build(map);
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

            if (autoFitCamera && bounds.HasValue) FitCamera(bounds.Value);
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
                unityVerts[i] = new Vector3(data.Vertices[i].X, data.Vertices[i].Y, data.Vertices[i].Z);
            mesh.vertices = unityVerts;
            mesh.triangles = data.Triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            child.AddComponent<MeshRenderer>().sharedMaterial = material;
            child.AddComponent<MeshCollider>().sharedMesh = mesh;

            var b = mesh.bounds;
            bounds = bounds.HasValue ? Combine(bounds.Value, b) : b;
        }

        static Bounds Combine(Bounds a, Bounds b) { a.Encapsulate(b); return a; }

        void FitCamera(Bounds b)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var center = b.center;
            float topY = b.max.y + Mathf.Max(b.size.x, b.size.z); // высота над крышей ~ диаметр карты
            cam.transform.position = new Vector3(center.x, topY, center.z);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, topY * 3f);
            cam.nearClipPlane = 0.1f;
        }

        void OnWarning(string msg) => Debug.LogWarning($"[Doom.Map] {msg}");
        void OnError(string msg)   => Debug.LogError  ($"[Doom.Map] {msg}");
    }
}
```

- [ ] **Step 3: Создать три материала**

В Unity Editor:
1. `Assets > Create > Material` → переименовать в `Floor`, переместить в `Assets/Materials/MapBlockOut/`, выставить Albedo `#3A3A3A`.
2. То же для `Ceiling` → `#555555`.
3. То же для `Wall` → `#808080`.

Все три используют дефолтный `Standard` шейдер (Built-in pipeline).

Альтернатива без UI — сгенерировать `.mat`-файлы как YAML вручную:

```powershell
$mats = "D:\Development\doom\Assets\Materials\MapBlockOut"
New-Item -ItemType Directory -Force $mats | Out-Null
@'
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: Floor
  m_Shader: {fileID: 46, guid: 0000000000000000f000000000000000, type: 0}
  m_Parent: {fileID: 0}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {}
  disabledShaderPasses: []
  m_LockedProperties:
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs: []
    m_Ints: []
    m_Floats: []
    m_Colors:
    - _Color: {r: 0.227, g: 0.227, b: 0.227, a: 1}
  m_BuildTextureStacks: []
  m_AllowLocking: 1
'@ | Set-Content -Encoding UTF8 "$mats\Floor.mat"
```

(Аналогично для Ceiling: `0.333,0.333,0.333`; Wall: `0.502,0.502,0.502`. На каждый материал — отдельный файл и его `.meta` Unity сгенерирует при следующем импорте.)

Открыть Unity, дождаться компиляции, проверить что материалы видны в Project window.

- [ ] **Step 4: Подготовить сцену `Stage2_MapPreview.unity`**

В Unity:
1. Открыть `Assets/Scenes/Stage2_MapPreview.unity` (создана в Task 1).
2. В сцене: `Main Camera` (по умолчанию), `Directional Light` (по умолчанию).
3. Создать пустой `GameObject` → переименовать в `MapLoader` → `Add Component > Doom > Map Loader`.
4. В инспекторе перетащить три материала в соответствующие поля. `WadRelativePath` = `wads/freedoom1.wad`, `MapName` = `E1M1`, `AutoFitCamera` = true.
5. Сохранить сцену.
6. Файл `EditorBuildSettings.asset` обновить, чтобы `Stage2_MapPreview` была в списке: `File > Build Profiles > Add Open Scenes` (или вручную).

- [ ] **Step 5: Ручная верификация — нажать Play**

Открыть сцену, нажать Play. Ожидаемое:
- В Console: `MapLoader: loaded E1M1 — ~480 verts, ~640 lines, ~80 sectors`, затем `MapLoader: built X/N sectors` (X должно быть близко к N; если сильно меньше — слишком много секторов с открытыми контурами).
- В Scene view / Game view (сверху): силуэт карты E1M1, серые поверхности пол/потолок/стены.
- Свободно навигировать Scene view, проверить что стены закрывают сектора, пол и потолок не дырявые.

Если ничего не видно — проверить:
- камера на правильной позиции (autoFitCamera должен сработать),
- материалы заданы (mesh без material = розовый),
- размер карты — порядка сотен/тысяч юнитов; near/far планы камеры должны это вмещать.

- [ ] **Step 6: Закоммитить**

```powershell
git add Assets/Scripts/MapBuild Assets/Materials Assets/Scenes/Stage2_MapPreview.unity
git rm Assets/Scripts/MapBuild/Placeholder.cs Assets/Scripts/MapBuild/Placeholder.cs.meta
git commit -m "Stage 2: MapLoader MonoBehaviour + block-out materials + preview scene"
```

---

### Task 11: Интеграционные тесты на `freedoom1.wad`

Сьют, проверяющий что весь pipeline переваривает реальный WAD без исключений и даёт разумные числа.

**Files:**
- Create: `Assets/Tests/EditMode/Map/MapFreedoomTests.cs`

- [ ] **Step 1: Написать тесты**

`Assets/Tests/EditMode/Map/MapFreedoomTests.cs`:

```csharp
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Wad;
using Doom.Map;

namespace Doom.Map.Tests
{
    public class MapFreedoomTests
    {
        private static string FreedoomPath =>
            Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");

        [Test]
        public void Loads_E1M1_with_expected_lump_counts()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var map = MapData.Load(wad, "E1M1");

            Assert.That(map.Name, Is.EqualTo("E1M1"));
            Assert.That(map.Vertexes.Length, Is.GreaterThan(100),
                "E1M1 содержит сотни вершин");
            Assert.That(map.LineDefs.Length, Is.GreaterThan(100));
            Assert.That(map.SideDefs.Length, Is.GreaterThan(100));
            Assert.That(map.Sectors.Length, Is.GreaterThan(10));
        }

        [Test]
        public void Throws_for_nonexistent_map()
        {
            using var wad = WadFile.Open(FreedoomPath);
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
                () => MapData.Load(wad, "E9M9"));
        }

        [Test]
        public void SectorPolygonBuilder_closes_most_E1M1_sectors()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var map = MapData.Load(wad, "E1M1");

            var polys = SectorPolygonBuilder.Build(map);

            int valid = 0;
            foreach (var p in polys) if (p.IsValid) valid++;
            double ratio = (double)valid / polys.Length;
            // Если меньше 90% секторов замкнулись — что-то очень не так с алгоритмом
            Assert.That(ratio, Is.GreaterThan(0.9),
                $"Замкнуто {valid}/{polys.Length} секторов ({ratio:P0})");
        }

        [Test]
        public void Builds_geometry_for_E1M1_without_throwing()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var map = MapData.Load(wad, "E1M1");

            SectorMeshes[] meshes = null;
            Assert.DoesNotThrow(() => meshes = MapGeometryBuilder.Build(map));

            int totalTris = 0;
            foreach (var sm in meshes)
            {
                totalTris += sm.Floor.Triangles.Length / 3;
                totalTris += sm.Ceiling.Triangles.Length / 3;
                totalTris += sm.Walls.Triangles.Length / 3;
            }
            Assert.That(totalTris, Is.GreaterThan(1000),
                "E1M1 должна давать тысячи треугольников");
        }

        [Test]
        public void Loads_other_E1Mx_maps_without_throwing()
        {
            using var wad = WadFile.Open(FreedoomPath);
            foreach (var name in new[] { "E1M2", "E1M3", "E2M1", "E3M1" })
            {
                Assert.DoesNotThrow(() => {
                    var map = MapData.Load(wad, name);
                    var meshes = MapGeometryBuilder.Build(map);
                    Assert.That(meshes.Length, Is.GreaterThan(0));
                }, $"Failed for {name}");
            }
        }
    }
}
```

- [ ] **Step 2: Прогнать тесты**

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -nographics -projectPath "D:\Development\doom" `
    -runTests -testPlatform EditMode `
    -testResults "D:\Development\doom\Logs\test-results.xml" `
    -logFile -
```

Ожидаемое: 57 тестов pass (52 + 5 интеграционных).

Если упало на `SectorPolygonBuilder_closes_most_E1M1_sectors` (< 90%) — это сигнал что алгоритм где-то некорректен на реальных данных. Проверить через `Tools > Doom > Dump freedoom1.wad` структуру первых пары секторов, прогнать `SectorPolygonBuilder` локально на этих данных и сравнить с ожидаемым контуром. Возможные причины:
- ambiguity на T-junction (sector делит вершину с 3+ другими) — log пишет «open contour» хотя на деле просто чейнинг неоднозначен,
- знак `SignedArea` не совпадает с CCW из-за свопа координат — проверить тест `Square_room_outer_contour_is_CCW_in_doom_xy`.

- [ ] **Step 3: Закоммитить**

```powershell
git add Assets/Tests/EditMode/Map/MapFreedoomTests.cs `
        Assets/Tests/EditMode/Map/MapFreedoomTests.cs.meta
git commit -m "Stage 2: integration tests on freedoom1.wad maps"
```

---

### Task 12: Обновить план и `CLAUDE.md`

**Files:**
- Modify: `docs/doom-unity-remake-plan.md`
- Modify: `CLAUDE.md`

- [ ] **Step 1: Отметить Этап 2 как выполненный в `docs/doom-unity-remake-plan.md`**

В заголовке Этапа 2 заменить `## Этап 2. Геометрия одной карты (серый блок-аут)` на `## Этап 2. Геометрия одной карты (серый блок-аут) ✅`.

Под bullet'ами добавить чекбоксы:

```markdown
- [x] Распарсить лампы карты: `VERTEXES`, `LINEDEFS`, `SIDEDEFS`, `SECTORS`.
- [x] Собрать контуры секторов и затриангулировать пол и потолок каждого сектора (через LibTessDotNet).
- [x] Построить стены: для односторонних линий — квад от пола до потолка; для двусторонних — нижнюю и верхнюю перемычки там, где высоты соседних секторов различаются.
- [x] Сгенерировать меши Unity и разместить на сцене (`Stage2_MapPreview.unity`, runtime через `MapLoader`).
```

В разделе **«Результат этапа»** добавить кратко: «`MapLoader` на сцене `Stage2_MapPreview` строит карту E1M1 (или любой другой ExMy) в Play mode; видна серая геометрия с разделёнными по секторам полом, потолком и стенами».

- [ ] **Step 2: Обновить `CLAUDE.md`**

В разделе **Project status** добавить пункт про Stage 2: `MapData`, `MapGeometryBuilder`, `MapLoader`, LibTessDotNet, новая сцена.

В разделе **Project layout** добавить:

```markdown
- `Assets/Scripts/Map/` — парсер геометрии карты + сборщик мешей (asmdef `Doom.Map`, `noEngineReferences: true`). Зависит от `Doom.Wad` и `LibTessDotNet`.
- `Assets/Scripts/MapBuild/` — `MapLoader` MonoBehaviour, единственное место, где `MeshData` превращается в `UnityEngine.Mesh` (asmdef `Doom.MapBuild`).
- `Assets/ThirdParty/LibTessDotNet/` — vendored sources LibTessDotNet v1.1.15 (SGI Free Software License B 2.0).
- `Assets/Materials/MapBlockOut/` — три материала для серого блок-аута: пол, потолок, стены.
- `Assets/Scenes/Stage2_MapPreview.unity` — preview-сцена с `MapLoader` для Stage 2.
- `Assets/Tests/EditMode/Map/` — NUnit-тесты парсеров, алгоритмов сборки контуров, триангулятора, стен, интеграция на `freedoom1.wad`.
```

В разделе **Architectural ground rules** уточнить пункт про BSP:
- BSP-данные оригинала **по-прежнему не используются**. `SectorPolygonBuilder` восстанавливает полигоны из linedef-ов сам.

- [ ] **Step 3: Закоммитить**

```powershell
git add docs/doom-unity-remake-plan.md CLAUDE.md
git commit -m "Stage 2 done: mark plan + CLAUDE.md"
```

---

## Definition of done

Этап считается закрытым, когда выполнено всё ниже:

1. `git log --oneline` показывает 12 коммитов с префиксом `Stage 2:`/`Stage 2 setup:`/`Stage 2 done:`.
2. Прогон `-runTests -testPlatform EditMode` возвращает exit 0; в XML 57 тестов pass (32 после Task 1 + 3 vertex/linedef + 2 sidedef/sector + 4 load + 5 polygon + 3 tess + 3 walls + 5 freedoom).
3. В Unity, открыв `Assets/Scenes/Stage2_MapPreview.unity` и нажав Play:
   - В Console: одна строка про загрузку (`loaded E1M1 — N verts, M lines, K sectors`) и одна про сборку (`built X/K sectors`).
   - В Game view: вид сверху на E1M1, серая геометрия.
   - В Scene view: можно навигировать вокруг карты, секторы различимы по цветам пол/потолок/стены.
4. `Doom.Map` asmdef не зависит от `UnityEngine` (`noEngineReferences: true`); `LibTessDotNet` тоже.
5. Документы (`docs/doom-unity-remake-plan.md`, `CLAUDE.md`) отражают новое состояние.

## Что НЕ делается в этом этапе (и почему)

- **`THINGS`** — Stage 3 (старт игрока, монстры, объекты). Может парситься попутно, но никакого спрайтового рендера ещё не будет.
- **Текстуры (`PLAYPAL`, `TEXTURE1`/`TEXTURE2`, `PNAMES`, `FLATS`)** — Stage 4. В Stage 2 однотонные материалы.
- **Освещение из `Sector.LightLevel`** — Stage 4/5. Сейчас полагаемся на один `Directional Light` в сцене.
- **Двусторонние middle-текстуры (заборы, решётки)** — Stage 4. В Stage 2 отрисовываем только lower/upper steps между секторами.
- **Двери, лифты, спецсектора (`Sector.Special`, `LineDef.Special`)** — Stage 6. Высоты пола/потолка зафиксированы статически.
- **Skybox, F_SKY1** — Stage 4 (флэт `F_SKY1` — особый случай, ему соответствует skybox).
- **Оптимизация мешей** (объединение, batching, occlusion) — Stage 7. В Stage 2 ленивый «по `GameObject` на сектор» — приемлемо при ~100 секторах E1M1.
- **PWAD merge** (наложение пользовательских WAD'ов поверх IWAD) — позже Stage 7.
- **T-junction-аккуратный chaining** (когда вершина имеет 3+ outgoing edges одного сектора) — log + skip. Если на freedoom1.wad доля «открытых» секторов окажется заметной, Step 2 интеграционных тестов это поймает.

## Граничные случаи и заметки для реализующего

- **Производительность парсинга:** карта E1M1 — это ~10 КБ суммарно лампов карты. Парсинг и сборка занимают миллисекунды. Никаких NativeArray/Burst/Jobs пока не нужно.
- **Меш-индексы:** по умолчанию Unity Mesh использует UInt16, лимит 65535 вершин на меш. На стенах целого сектора это редко критично, но `MapLoader` всё равно переключает на UInt32, если перевалит.
- **`Mesh.RecalculateNormals`:** дёшево, корректно для flat-shaded блок-аута. Для Stage 4 (текстуры) понадобится явный расчёт UV и, возможно, smoothing groups — пока не нужно.
- **`MeshCollider`:** при пустом меше Unity ругается. `AddChild` проверяет `data.IsEmpty` и не создаёт `GameObject` в этом случае.
- **`autoFitCamera` математика:** простая, не учитывает соотношение сторон. Достаточно для проверки, что геометрия в принципе видна. Подкрутка камеры — Stage 3 при добавлении игрока.
- **LibTessDotNet API:** если в Tess.cs другая структура `Vec3` (например `LibTessDotNet.Float` namespace) — поправить `using` и тип `Vec3` в `SectorTriangulator`. См. `Tess.cs` upstream.
