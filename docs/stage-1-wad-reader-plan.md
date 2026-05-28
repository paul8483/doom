# Stage 1: WAD-ридер — План реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Прочитать структуру WAD-файла (заголовок + directory) и предоставить API «достать ламп по имени», верифицированный на `Assets/StreamingAssets/wads/freedoom1.wad`.

**Architecture:** Чистый C#-парсер в собственной asmdef `Doom.Wad` без зависимостей от `UnityEngine` (тестируется в EditMode без сцены). Поток файла открывается один раз и читается лениво (28 МБ в память не грузим). Поверх — Editor-меню `Tools > Doom > Dump freedoom1.wad` как ручная веха этапа.

**Tech Stack:** Unity 6000.4.8f1, C# / .NET (BinaryReader, FileStream), Unity Test Framework (NUnit-style EditMode tests).

---

## Контекст

Это первый этап после Stage 0 (см. `docs/doom-unity-remake-plan.md`). Stage 0 уже завёл Unity-проект, положил `freedoom1.wad` в `Assets/StreamingAssets/wads/`, инициализировал Git. Никакого C#-кода в `Assets/Scripts/` ещё нет, asmdef'ов тоже нет — этот план создаёт всё с нуля.

Архитектурные правила проекта (`CLAUDE.md`):
- BSP-данные оригинала (`NODES`, `SSECTORS`, `SEGS`, `BLOCKMAP`) **не используются** — но в Stage 1 мы и не пытаемся их интерпретировать, просто читаем directory как сырые записи.
- Числа в WAD — **little-endian**, читаются через `BinaryReader` (который и так LE по умолчанию в .NET).
- Не портируется C-код оригинала — пишем парсер с нуля по описанию формата.

## Формат WAD (краткая справка)

WAD-файл — это:

```
+---------+---------+-------------+
| Header  | Lump    | Directory   |
| 12 b    | data    | numLumps×16 |
+---------+---------+-------------+
```

**Header (12 байт):**

| Смещение | Размер | Поле           | Описание                                 |
|----------|--------|----------------|------------------------------------------|
| 0        | 4      | `identification` | ASCII: `"IWAD"` или `"PWAD"`           |
| 4        | 4      | `numlumps`     | int32 LE — число лампов                  |
| 8        | 4      | `infotableofs` | int32 LE — смещение directory от начала  |

**Directory entry (16 байт на запись, всего `numlumps` штук):**

| Смещение | Размер | Поле        | Описание                                          |
|----------|--------|-------------|---------------------------------------------------|
| 0        | 4      | `filepos`   | int32 LE — смещение данных лампа от начала файла  |
| 4        | 4      | `size`      | int32 LE — длина данных в байтах                  |
| 8        | 8      | `name`      | ASCII, до 8 символов, добивается `\0`; ALL CAPS   |

**Имена лампов:** строго до 8 байт, ASCII, с нулевым добиванием справа. `\0` — терминатор; всё после первого `\0` игнорируется. Имена в оригинале всегда верхним регистром.

**«Карты» в WAD** — это маркер-лампы с именами `ExMy` (DOOM 1: `E1M1`...`E4M9`) или `MAPxx` (DOOM 2: `MAP01`...`MAP32`), с размером `size = 0`, за которыми идут лампы геометрии (`THINGS`, `LINEDEFS`, `SIDEDEFS`, `VERTEXES`, `SEGS`, `SSECTORS`, `NODES`, `SECTORS`, `REJECT`, `BLOCKMAP`). В Stage 1 нам нужно лишь **найти маркеры** для распечатки списка карт; парсить связки лампов карты будем в Stage 2.

## Файловая структура

После выполнения плана проект будет содержать:

```
Assets/Scripts/Wad/
├── Doom.Wad.asmdef            ← runtime assembly, без UnityEngine
└── WadFile.cs                  ← парсер: WadFile + LumpInfo + WadHeader + WadMapNames

Assets/Scripts/Wad/Editor/
├── Doom.Wad.Editor.asmdef      ← editor-only assembly
└── WadInspectorMenu.cs         ← Tools > Doom > Dump freedoom1.wad

Assets/Tests/EditMode/Wad/
├── Doom.Wad.Tests.asmdef       ← EditMode test assembly
├── SyntheticWadBuilder.cs      ← билдер in-memory WAD'ов для unit-тестов
├── WadFileTests.cs             ← юнит-тесты на синтетике
└── WadFreedoomTests.cs         ← интеграционный тест на freedoom1.wad
```

Решение «всё в одном `WadFile.cs`» намеренное: header, directory entry, парсер, helper определения карт меняются вместе, дробить по технологическим слоям нет смысла.

## Тесты: как и где

- **Где запускать в редакторе:** `Window > General > Test Runner`, вкладка `EditMode`, кнопка `Run All`.
- **Где запускать из CLI** (для CI и быстрой проверки из терминала):
  ```powershell
  & "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
      -batchmode -nographics -projectPath "D:\Development\doom" `
      -runTests -testPlatform EditMode `
      -testResults "D:\Development\doom\Logs\test-results.xml" `
      -logFile -
  ```
  Код возврата ≠ 0 при упавших тестах. **NB:** `-quit` НЕ добавлять — `-runTests` сам управляет завершением; флаги конфликтуют, и Unity выйдет ДО запуска тестов. Per-тест PASS/FAIL пишутся в XML (`-testResults`), в editor-лог попадает только сводка.
- **Доступ к freedoom1.wad из теста:** `System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, "wads", "freedoom1.wad")`. В EditMode это разрешается в обычный путь в `Assets/StreamingAssets/...`.

---

### Task 1: Bootstrap — assembly definitions, Test Framework, smoke test

**Files:**
- Create: `Assets/Scripts/Wad/Doom.Wad.asmdef`
- Create: `Assets/Scripts/Wad/Placeholder.cs`
- Create: `Assets/Tests/EditMode/Wad/Doom.Wad.Tests.asmdef`
- Create: `Assets/Tests/EditMode/Wad/SmokeTest.cs`
- Modify: `Packages/manifest.json` (добавить test framework)

- [ ] **Step 1: Добавить Unity Test Framework в манифест пакетов**

Открыть `Packages/manifest.json` и добавить запись в `dependencies` (после `com.unity.multiplayer.center`, до `com.unity.modules.accessibility`):

```json
"com.unity.test-framework": "1.5.1",
```

Запустить Unity (или просто переключиться на окно редактора — он сам подхватит изменение и установит пакет, см. `Library/PackageCache/com.unity.test-framework@*`).

Ожидаемый результат: пакет появляется в `Library/PackageCache/com.unity.test-framework@<hash>`. **NB:** Unity 6 ships test-framework как builtin-пакет, поэтому версия в манифесте — рекомендация, фактически Unity подставит свою (на Unity 6000.4.8f1 это 1.6.0). Это норма, парсер тестов работает одинаково.

- [ ] **Step 2: Создать asmdef для runtime-кода (без UnityEngine)**

Файл `Assets/Scripts/Wad/Doom.Wad.asmdef`:

```json
{
    "name": "Doom.Wad",
    "rootNamespace": "Doom.Wad",
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

`noEngineReferences: true` — это ключевая настройка: ассембли не ссылается на `UnityEngine.dll`, что заставляет компилятор ловить любые попытки протянуть Unity-зависимость в парсер.

- [ ] **Step 3: Создать placeholder, чтобы asmdef собрался**

Asmdef без хотя бы одного `.cs` иногда вызывает шум в Unity Console. Положить временный заглушечный файл `Assets/Scripts/Wad/Placeholder.cs`:

```csharp
namespace Doom.Wad
{
    internal static class Placeholder { }
}
```

(Файл удалим в Task 2, как только появится настоящий `WadFile.cs`.)

- [ ] **Step 4: Создать asmdef для EditMode-тестов**

Файл `Assets/Tests/EditMode/Wad/Doom.Wad.Tests.asmdef`:

```json
{
    "name": "Doom.Wad.Tests",
    "rootNamespace": "Doom.Wad.Tests",
    "references": [
        "Doom.Wad",
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

Ключевые пункты: `includePlatforms: ["Editor"]` — собирается только в редакторе, `defineConstraints: ["UNITY_INCLUDE_TESTS"]` — компилируется только при включённом Test Framework, `overrideReferences + precompiledReferences: ["nunit.framework.dll"]` — подтягивает NUnit.

- [ ] **Step 5: Написать smoke-test, что инфраструктура работает**

Файл `Assets/Tests/EditMode/Wad/SmokeTest.cs`:

```csharp
using NUnit.Framework;

namespace Doom.Wad.Tests
{
    public class SmokeTest
    {
        [Test]
        public void Test_framework_is_wired_up()
        {
            Assert.That(2 + 2, Is.EqualTo(4));
        }
    }
}
```

- [ ] **Step 6: Прогнать тесты через CLI и убедиться, что smoke проходит**

Запустить:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -nographics -projectPath "D:\Development\doom" `
    -runTests -testPlatform EditMode `
    -testResults "D:\Development\doom\Logs\test-results.xml" `
    -logFile -
```

Ожидаемое: exit code 0, в `Logs/test-results.xml` атрибут `result="Passed"`, `passed="1"`, `failed="0"`. Per-тест PASS-строки в editor-логе НЕ появляются — Unity 6 пишет их только в `-testResults` XML.

Если падает — проверить, что Unity успел импортировать пакет (открыть редактор вручную и закрыть), что asmdef'ы валидны (Unity Console покажет ошибку парсинга JSON).

- [ ] **Step 7: Закоммитить**

```powershell
git add Packages/manifest.json Packages/packages-lock.json `
        Assets/Scripts Assets/Tests
git commit -m "Stage 1 setup: Doom.Wad asmdef + Test Framework + smoke test"
```

(`packages-lock.json` Unity обновит автоматически после установки пакета.)

---

### Task 2: WAD header — parsing

**Files:**
- Create: `Assets/Scripts/Wad/WadFile.cs`
- Delete: `Assets/Scripts/Wad/Placeholder.cs`
- Create: `Assets/Tests/EditMode/Wad/SyntheticWadBuilder.cs`
- Create: `Assets/Tests/EditMode/Wad/WadFileTests.cs`

- [ ] **Step 1: Написать helper для конструирования синтетических WAD'ов**

Файл `Assets/Tests/EditMode/Wad/SyntheticWadBuilder.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Doom.Wad.Tests
{
    internal static class SyntheticWadBuilder
    {
        public readonly struct Lump
        {
            public readonly string Name;
            public readonly byte[] Data;
            public Lump(string name, byte[] data) { Name = name; Data = data; }
        }

        public static byte[] Build(string signature, IReadOnlyList<Lump> lumps)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);

            // Header: signature (4) + numLumps (4) + dirOffset (4)
            // dirOffset мы пока не знаем — запишем 0 и обновим в конце.
            w.Write(Encoding.ASCII.GetBytes(signature));
            w.Write(lumps.Count);
            long dirOffsetField = ms.Position;
            w.Write(0);

            // Тело лампов
            var entries = new (int Offset, int Size, string Name)[lumps.Count];
            for (int i = 0; i < lumps.Count; i++)
            {
                entries[i] = ((int)ms.Position, lumps[i].Data.Length, lumps[i].Name);
                w.Write(lumps[i].Data);
            }

            // Directory
            int dirOffset = (int)ms.Position;
            foreach (var e in entries)
            {
                w.Write(e.Offset);
                w.Write(e.Size);
                w.Write(EncodeName(e.Name));
            }

            // Обновить dirOffset в header
            ms.Position = dirOffsetField;
            w.Write(dirOffset);

            return ms.ToArray();
        }

        private static byte[] EncodeName(string name)
        {
            var buf = new byte[8];
            var ascii = Encoding.ASCII.GetBytes(name);
            System.Array.Copy(ascii, buf, System.Math.Min(ascii.Length, 8));
            return buf;
        }
    }
}
```

- [ ] **Step 2: Написать падающие тесты на чтение header**

Файл `Assets/Tests/EditMode/Wad/WadFileTests.cs`:

```csharp
using System.IO;
using NUnit.Framework;

namespace Doom.Wad.Tests
{
    public class WadFileTests
    {
        [Test]
        public void Reads_IWAD_signature_and_lump_count()
        {
            var bytes = SyntheticWadBuilder.Build("IWAD", new[]
            {
                new SyntheticWadBuilder.Lump("FIRST", new byte[] { 1, 2, 3 }),
                new SyntheticWadBuilder.Lump("SECOND", new byte[] { 4 }),
            });

            using var wad = new WadFile(new MemoryStream(bytes), ownsStream: true);

            Assert.That(wad.Header.Signature, Is.EqualTo("IWAD"));
            Assert.That(wad.Header.NumLumps, Is.EqualTo(2));
            Assert.That(wad.Header.DirOffset, Is.GreaterThan(0));
        }

        [Test]
        public void Accepts_PWAD_signature()
        {
            var bytes = SyntheticWadBuilder.Build(
                "PWAD",
                new[] { new SyntheticWadBuilder.Lump("X", new byte[0]) });

            using var wad = new WadFile(new MemoryStream(bytes), ownsStream: true);

            Assert.That(wad.Header.Signature, Is.EqualTo("PWAD"));
        }
    }
}
```

- [ ] **Step 3: Прогнать тесты — должны упасть на «WadFile не существует»**

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -nographics -projectPath "D:\Development\doom" `
    -runTests -testPlatform EditMode `
    -testResults "D:\Development\doom\Logs\test-results.xml" `
    -logFile -
```

Ожидаемое: компиляция падает — `error CS0246: The type or namespace name 'WadFile' could not be found`.

- [ ] **Step 4: Удалить placeholder и реализовать минимальный `WadFile` (только header)**

Удалить `Assets/Scripts/Wad/Placeholder.cs` (и его `.meta`).

Создать `Assets/Scripts/Wad/WadFile.cs`:

```csharp
using System;
using System.IO;
using System.Text;

namespace Doom.Wad
{
    public readonly struct WadHeader
    {
        public readonly string Signature;
        public readonly int NumLumps;
        public readonly int DirOffset;

        public WadHeader(string signature, int numLumps, int dirOffset)
        {
            Signature = signature;
            NumLumps = numLumps;
            DirOffset = dirOffset;
        }
    }

    public sealed class WadFile : IDisposable
    {
        private readonly Stream stream;
        private readonly bool ownsStream;
        private readonly BinaryReader reader;

        public WadHeader Header { get; }

        public WadFile(Stream stream, bool ownsStream = false)
        {
            this.stream = stream;
            this.ownsStream = ownsStream;
            this.reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

            stream.Position = 0;
            var sig = Encoding.ASCII.GetString(reader.ReadBytes(4));
            var numLumps = reader.ReadInt32();
            var dirOffset = reader.ReadInt32();
            Header = new WadHeader(sig, numLumps, dirOffset);
        }

        public void Dispose()
        {
            reader.Dispose();
            if (ownsStream) stream.Dispose();
        }
    }
}
```

- [ ] **Step 5: Прогнать тесты — должны пройти**

Команда та же. Ожидаемое: `Reads_IWAD_signature_and_lump_count ... PASS`, `Accepts_PWAD_signature ... PASS`.

- [ ] **Step 6: Закоммитить**

```powershell
git add Assets/Scripts/Wad/WadFile.cs Assets/Scripts/Wad/WadFile.cs.meta `
        Assets/Tests/EditMode/Wad/SyntheticWadBuilder.cs `
        Assets/Tests/EditMode/Wad/SyntheticWadBuilder.cs.meta `
        Assets/Tests/EditMode/Wad/WadFileTests.cs `
        Assets/Tests/EditMode/Wad/WadFileTests.cs.meta
git rm Assets/Scripts/Wad/Placeholder.cs Assets/Scripts/Wad/Placeholder.cs.meta
git commit -m "Stage 1: parse WAD header (signature, numLumps, dirOffset)"
```

---

### Task 3: Directory table

**Files:**
- Modify: `Assets/Scripts/Wad/WadFile.cs`
- Modify: `Assets/Tests/EditMode/Wad/WadFileTests.cs`

- [ ] **Step 1: Добавить падающий тест на directory**

Дописать в `WadFileTests.cs`:

```csharp
        [Test]
        public void Parses_directory_entries()
        {
            var bytes = SyntheticWadBuilder.Build("IWAD", new[]
            {
                new SyntheticWadBuilder.Lump("FIRST", new byte[] { 0xAA, 0xBB }),
                new SyntheticWadBuilder.Lump("E1M1", new byte[0]),
                new SyntheticWadBuilder.Lump("VERTEXES", new byte[] { 1, 2, 3, 4 }),
            });

            using var wad = new WadFile(new MemoryStream(bytes), ownsStream: true);

            Assert.That(wad.Directory.Count, Is.EqualTo(3));
            Assert.That(wad.Directory[0].Name, Is.EqualTo("FIRST"));
            Assert.That(wad.Directory[0].Size, Is.EqualTo(2));
            Assert.That(wad.Directory[1].Name, Is.EqualTo("E1M1"));
            Assert.That(wad.Directory[1].Size, Is.EqualTo(0));
            Assert.That(wad.Directory[2].Name, Is.EqualTo("VERTEXES"));
            Assert.That(wad.Directory[2].Size, Is.EqualTo(4));
        }

        [Test]
        public void Strips_null_padding_from_lump_names()
        {
            // "F" + 7 null bytes — должно стать "F", не "F\0\0\0\0\0\0\0"
            var bytes = SyntheticWadBuilder.Build("IWAD", new[]
            {
                new SyntheticWadBuilder.Lump("F", new byte[0]),
            });

            using var wad = new WadFile(new MemoryStream(bytes), ownsStream: true);

            Assert.That(wad.Directory[0].Name, Is.EqualTo("F"));
            Assert.That(wad.Directory[0].Name.Length, Is.EqualTo(1));
        }
```

- [ ] **Step 2: Прогнать тесты — должны упасть на отсутствии `Directory`**

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -nographics -projectPath "D:\Development\doom" `
    -runTests -testPlatform EditMode `
    -testResults "D:\Development\doom\Logs\test-results.xml" `
    -logFile -
```

Ожидаемое: ошибка компиляции — `'WadFile' does not contain a definition for 'Directory'`.

- [ ] **Step 3: Добавить `LumpInfo` и парсинг directory в `WadFile`**

Дописать в `WadFile.cs` (внутри namespace `Doom.Wad`):

```csharp
    public readonly struct LumpInfo
    {
        public readonly string Name;
        public readonly int Offset;
        public readonly int Size;

        public LumpInfo(string name, int offset, int size)
        {
            Name = name;
            Offset = offset;
            Size = size;
        }
    }
```

В классе `WadFile`:

```csharp
        public System.Collections.Generic.IReadOnlyList<LumpInfo> Directory { get; }
```

В конструкторе, после чтения header, добавить:

```csharp
            stream.Position = dirOffset;
            var entries = new LumpInfo[numLumps];
            for (int i = 0; i < numLumps; i++)
            {
                var filepos = reader.ReadInt32();
                var size = reader.ReadInt32();
                var nameBytes = reader.ReadBytes(8);
                var name = DecodeName(nameBytes);
                entries[i] = new LumpInfo(name, filepos, size);
            }
            Directory = entries;
```

И приватный метод декодирования имени (тоже внутри `WadFile`):

```csharp
        private static string DecodeName(byte[] raw)
        {
            int end = raw.Length;
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] == 0) { end = i; break; }
            }
            return Encoding.ASCII.GetString(raw, 0, end);
        }
```

- [ ] **Step 4: Прогнать тесты — все четыре должны пройти**

Ожидаемое: 4 PASS (включая два теста из Task 2).

- [ ] **Step 5: Закоммитить**

```powershell
git add Assets/Scripts/Wad/WadFile.cs Assets/Tests/EditMode/Wad/WadFileTests.cs
git commit -m "Stage 1: parse WAD directory, decode null-padded lump names"
```

---

### Task 4: Lookup by name + read lump bytes

**Files:**
- Modify: `Assets/Scripts/Wad/WadFile.cs`
- Modify: `Assets/Tests/EditMode/Wad/WadFileTests.cs`

- [ ] **Step 1: Добавить падающие тесты**

В `WadFileTests.cs`:

```csharp
        [Test]
        public void Reads_lump_data_by_name()
        {
            var payload = new byte[] { 10, 20, 30, 40 };
            var bytes = SyntheticWadBuilder.Build("IWAD", new[]
            {
                new SyntheticWadBuilder.Lump("OTHER", new byte[] { 99 }),
                new SyntheticWadBuilder.Lump("PLAYPAL", payload),
            });

            using var wad = new WadFile(new MemoryStream(bytes), ownsStream: true);
            var data = wad.ReadLump("PLAYPAL");

            Assert.That(data, Is.EqualTo(payload));
        }

        [Test]
        public void Reads_lump_data_by_index()
        {
            var bytes = SyntheticWadBuilder.Build("IWAD", new[]
            {
                new SyntheticWadBuilder.Lump("A", new byte[] { 1 }),
                new SyntheticWadBuilder.Lump("B", new byte[] { 2, 3 }),
            });

            using var wad = new WadFile(new MemoryStream(bytes), ownsStream: true);

            Assert.That(wad.ReadLump(0), Is.EqualTo(new byte[] { 1 }));
            Assert.That(wad.ReadLump(1), Is.EqualTo(new byte[] { 2, 3 }));
        }

        [Test]
        public void FindLump_returns_minus_one_for_missing()
        {
            var bytes = SyntheticWadBuilder.Build("IWAD", new[]
            {
                new SyntheticWadBuilder.Lump("A", new byte[0]),
            });

            using var wad = new WadFile(new MemoryStream(bytes), ownsStream: true);

            Assert.That(wad.FindLump("NOSUCH"), Is.EqualTo(-1));
        }

        [Test]
        public void ReadLump_by_name_throws_on_missing()
        {
            var bytes = SyntheticWadBuilder.Build("IWAD", new[]
            {
                new SyntheticWadBuilder.Lump("A", new byte[0]),
            });

            using var wad = new WadFile(new MemoryStream(bytes), ownsStream: true);

            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
                () => wad.ReadLump("NOSUCH"));
        }

        [Test]
        public void FindLump_returns_first_match_for_duplicate_names()
        {
            // В реальных WAD'ах одинаковые имена встречаются (маркеры F_START/F_END,
            // лампы карт). FindLump возвращает индекс первого вхождения.
            var bytes = SyntheticWadBuilder.Build("IWAD", new[]
            {
                new SyntheticWadBuilder.Lump("DUP", new byte[] { 1 }),
                new SyntheticWadBuilder.Lump("DUP", new byte[] { 2 }),
            });

            using var wad = new WadFile(new MemoryStream(bytes), ownsStream: true);

            Assert.That(wad.FindLump("DUP"), Is.EqualTo(0));
            Assert.That(wad.ReadLump("DUP"), Is.EqualTo(new byte[] { 1 }));
        }
```

- [ ] **Step 2: Прогнать тесты — должны упасть на отсутствии методов**

Ожидаемое: ошибки компиляции про `ReadLump` и `FindLump`.

- [ ] **Step 3: Реализовать `FindLump` и `ReadLump`**

Дописать в класс `WadFile`:

```csharp
        public int FindLump(string name)
        {
            for (int i = 0; i < Directory.Count; i++)
            {
                if (Directory[i].Name == name) return i;
            }
            return -1;
        }

        public byte[] ReadLump(string name)
        {
            int idx = FindLump(name);
            if (idx < 0)
            {
                throw new System.Collections.Generic.KeyNotFoundException(
                    $"Lump '{name}' not found in WAD");
            }
            return ReadLump(idx);
        }

        public byte[] ReadLump(int index)
        {
            var entry = Directory[index];
            if (entry.Size == 0) return System.Array.Empty<byte>();

            stream.Position = entry.Offset;
            var buf = new byte[entry.Size];
            int read = 0;
            while (read < buf.Length)
            {
                int n = stream.Read(buf, read, buf.Length - read);
                if (n <= 0) throw new EndOfStreamException(
                    $"Truncated lump '{entry.Name}': expected {buf.Length} bytes, got {read}");
                read += n;
            }
            return buf;
        }
```

(Цикл `while` нужен потому, что `Stream.Read` имеет право вернуть меньше, чем запрошено. Для `FileStream` на локальном диске это редко, но контракт такой.)

- [ ] **Step 4: Прогнать тесты — все девять должны пройти**

Ожидаемое: 9 PASS.

- [ ] **Step 5: Закоммитить**

```powershell
git add Assets/Scripts/Wad/WadFile.cs Assets/Tests/EditMode/Wad/WadFileTests.cs
git commit -m "Stage 1: lump lookup by name/index, ReadLump with bounds check"
```

---

### Task 5: Validation of malformed WADs

**Files:**
- Modify: `Assets/Scripts/Wad/WadFile.cs`
- Modify: `Assets/Tests/EditMode/Wad/WadFileTests.cs`

- [ ] **Step 1: Добавить падающие тесты на невалидные WAD'ы**

В `WadFileTests.cs`:

```csharp
        [Test]
        public void Rejects_unknown_signature()
        {
            // Заголовок с битой сигнатурой
            var bytes = new byte[12];
            System.Text.Encoding.ASCII.GetBytes("XXXX", 0, 4, bytes, 0);
            // numLumps = 0, dirOffset = 0 — остальное по нулям

            Assert.Throws<InvalidDataException>(
                () => new WadFile(new MemoryStream(bytes), ownsStream: true));
        }

        [Test]
        public void Rejects_file_too_short_for_header()
        {
            var bytes = new byte[8]; // < 12 байт

            Assert.Throws<EndOfStreamException>(
                () => new WadFile(new MemoryStream(bytes), ownsStream: true));
        }

        [Test]
        public void Rejects_negative_lump_count()
        {
            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            w.Write(System.Text.Encoding.ASCII.GetBytes("IWAD"));
            w.Write(-1);  // numLumps
            w.Write(12);  // dirOffset

            Assert.Throws<InvalidDataException>(
                () => new WadFile(ms, ownsStream: true));
        }
```

- [ ] **Step 2: Прогнать — должны падать (либо без `InvalidDataException`, либо с `IndexOutOfRangeException`)**

Ожидаемое: 3 fail (тесты выше); существующие 9 — pass.

- [ ] **Step 3: Добавить валидацию в конструктор `WadFile`**

В `WadFile.cs`, сразу после чтения header, до парсинга directory:

```csharp
            if (sig != "IWAD" && sig != "PWAD")
            {
                throw new InvalidDataException(
                    $"Not a WAD file: signature is '{sig}', expected 'IWAD' or 'PWAD'");
            }
            if (numLumps < 0)
            {
                throw new InvalidDataException(
                    $"Invalid WAD: negative lump count {numLumps}");
            }
            if (dirOffset < 12 || dirOffset > stream.Length)
            {
                throw new InvalidDataException(
                    $"Invalid WAD: directory offset {dirOffset} out of range");
            }
```

`BinaryReader.ReadInt32` для слишком короткого файла уже бросает `EndOfStreamException` сам — отдельной валидации не нужно.

- [ ] **Step 4: Прогнать — все 12 тестов pass**

- [ ] **Step 5: Закоммитить**

```powershell
git add Assets/Scripts/Wad/WadFile.cs Assets/Tests/EditMode/Wad/WadFileTests.cs
git commit -m "Stage 1: validate WAD signature, lump count, directory offset"
```

---

### Task 6: Map name detection

**Files:**
- Modify: `Assets/Scripts/Wad/WadFile.cs`
- Modify: `Assets/Tests/EditMode/Wad/WadFileTests.cs`

- [ ] **Step 1: Добавить падающие тесты на распознавание имени карты**

В `WadFileTests.cs`:

```csharp
        [TestCase("E1M1", true)]
        [TestCase("E4M9", true)]
        [TestCase("E2M5", true)]
        [TestCase("MAP01", true)]
        [TestCase("MAP32", true)]
        [TestCase("VERTEXES", false)]
        [TestCase("PLAYPAL", false)]
        [TestCase("E0M1", false)]     // эпизод 0 не бывает
        [TestCase("E5M1", false)]     // в оригинале только 4 эпизода
        [TestCase("E1M0", false)]     // карты с нуля не нумеруются
        [TestCase("MAP00", false)]
        [TestCase("MAP33", false)]
        [TestCase("", false)]
        [TestCase("THINGS", false)]
        public void Detects_map_marker_names(string name, bool expected)
        {
            Assert.That(WadMapNames.IsMapMarker(name), Is.EqualTo(expected));
        }
```

- [ ] **Step 2: Прогнать — падает на отсутствии `WadMapNames`**

- [ ] **Step 3: Реализовать `WadMapNames`**

В `WadFile.cs` (в том же файле, в том же namespace):

```csharp
    public static class WadMapNames
    {
        // ExMy: x ∈ 1..4, y ∈ 1..9
        // MAPxx: xx ∈ 01..32
        public static bool IsMapMarker(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            if (name.Length == 4 && name[0] == 'E' && name[2] == 'M')
            {
                int episode = name[1] - '0';
                int map = name[3] - '0';
                return episode >= 1 && episode <= 4 && map >= 1 && map <= 9;
            }

            if (name.Length == 5 && name[0] == 'M' && name[1] == 'A' && name[2] == 'P')
            {
                int hi = name[3] - '0';
                int lo = name[4] - '0';
                if (hi < 0 || hi > 9 || lo < 0 || lo > 9) return false;
                int n = hi * 10 + lo;
                return n >= 1 && n <= 32;
            }

            return false;
        }
    }
```

- [ ] **Step 4: Прогнать — все тесты pass**

- [ ] **Step 5: Закоммитить**

```powershell
git add Assets/Scripts/Wad/WadFile.cs Assets/Tests/EditMode/Wad/WadFileTests.cs
git commit -m "Stage 1: detect DOOM 1/2 map marker lump names"
```

---

### Task 7: Editor menu — Tools > Doom > Dump freedoom1.wad

**Files:**
- Create: `Assets/Scripts/Wad/Editor/Doom.Wad.Editor.asmdef`
- Create: `Assets/Scripts/Wad/Editor/WadInspectorMenu.cs`

Этот таск — единственный, который должен видеть `UnityEngine` / `UnityEditor`. Он даёт ручную проверку результата этапа: открыть Unity, нажать пункт меню, увидеть в Console распечатку.

- [ ] **Step 1: Создать editor-asmdef**

Файл `Assets/Scripts/Wad/Editor/Doom.Wad.Editor.asmdef`:

```json
{
    "name": "Doom.Wad.Editor",
    "rootNamespace": "Doom.Wad.Editor",
    "references": ["Doom.Wad"],
    "includePlatforms": ["Editor"],
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

- [ ] **Step 2: Реализовать пункт меню**

Файл `Assets/Scripts/Wad/Editor/WadInspectorMenu.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Doom.Wad.Editor
{
    public static class WadInspectorMenu
    {
        private const string WadRelativePath = "wads/freedoom1.wad";

        [MenuItem("Tools/Doom/Dump freedoom1.wad")]
        public static void DumpFreedoom1()
        {
            var path = Path.Combine(Application.streamingAssetsPath, WadRelativePath);
            if (!File.Exists(path))
            {
                Debug.LogError($"WAD not found at {path}");
                return;
            }

            using var wad = WadFile.Open(path);
            var sb = new StringBuilder();
            sb.AppendLine($"WAD: {path}");
            sb.AppendLine($"Signature: {wad.Header.Signature}");
            sb.AppendLine($"Lumps: {wad.Directory.Count}");

            var maps = new List<string>();
            for (int i = 0; i < wad.Directory.Count; i++)
            {
                if (WadMapNames.IsMapMarker(wad.Directory[i].Name))
                    maps.Add(wad.Directory[i].Name);
            }
            sb.AppendLine($"Maps ({maps.Count}): {string.Join(", ", maps)}");

            sb.AppendLine();
            sb.AppendLine("Directory:");
            for (int i = 0; i < wad.Directory.Count; i++)
            {
                var e = wad.Directory[i];
                sb.AppendLine($"  [{i,4}] {e.Name,-8}  offset={e.Offset,10}  size={e.Size,8}");
            }

            Debug.Log(sb.ToString());
        }
    }
}
```

- [ ] **Step 3: Добавить `WadFile.Open(string path)` (раньше его не было — `WadFile` принимал `Stream`)**

В `Assets/Scripts/Wad/WadFile.cs`, в классе `WadFile`:

```csharp
        public static WadFile Open(string path)
        {
            var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return new WadFile(fs, ownsStream: true);
        }
```

- [ ] **Step 4: Ручная верификация**

Открыть проект в Unity, дождаться компиляции, нажать `Tools > Doom > Dump freedoom1.wad`. В Console должно появиться сообщение вида:

```
WAD: D:/Development/doom/Assets/StreamingAssets/wads/freedoom1.wad
Signature: IWAD
Lumps: 2306
Maps (36): E1M1, E1M2, E1M3, ... E4M9, MAP01, ... MAP32

Directory:
  [   0] PLAYPAL   offset=        12  size=    10752
  ...
```

(Точные числа лампов и карт могут чуть отличаться от версии к версии Freedoom — главное, что Signature = IWAD, лампов несколько тысяч, карт около 36.)

- [ ] **Step 5: Закоммитить**

```powershell
git add Assets/Scripts/Wad/WadFile.cs Assets/Scripts/Wad/Editor
git commit -m "Stage 1: Tools > Doom > Dump menu for inspecting WAD contents"
```

---

### Task 8: Integration test against real freedoom1.wad

**Files:**
- Create: `Assets/Tests/EditMode/Wad/WadFreedoomTests.cs`

- [ ] **Step 1: Написать тест**

Файл `Assets/Tests/EditMode/Wad/WadFreedoomTests.cs`:

```csharp
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Doom.Wad.Tests
{
    public class WadFreedoomTests
    {
        private static string FreedoomPath =>
            Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");

        [Test]
        public void Opens_freedoom1_wad_as_IWAD()
        {
            Assert.That(File.Exists(FreedoomPath),
                $"freedoom1.wad missing at {FreedoomPath} — Stage 0 не доделан?");

            using var wad = WadFile.Open(FreedoomPath);

            Assert.That(wad.Header.Signature, Is.EqualTo("IWAD"));
            Assert.That(wad.Directory.Count, Is.GreaterThan(1000),
                "Freedoom Phase 1 содержит несколько тысяч лампов");
        }

        [Test]
        public void Contains_PLAYPAL_lump()
        {
            using var wad = WadFile.Open(FreedoomPath);
            int idx = wad.FindLump("PLAYPAL");

            Assert.That(idx, Is.GreaterThanOrEqualTo(0), "PLAYPAL должен быть");
            // PLAYPAL — 14 палитр по 256 цветов по 3 байта = 10752 байта
            Assert.That(wad.Directory[idx].Size, Is.EqualTo(14 * 256 * 3));
        }

        [Test]
        public void Contains_at_least_E1M1_map_marker()
        {
            using var wad = WadFile.Open(FreedoomPath);
            int idx = wad.FindLump("E1M1");

            Assert.That(idx, Is.GreaterThanOrEqualTo(0));
            Assert.That(wad.Directory[idx].Size, Is.EqualTo(0),
                "Маркер карты — лампа размера 0");
        }

        [Test]
        public void Lumps_after_E1M1_are_expected_map_components()
        {
            using var wad = WadFile.Open(FreedoomPath);
            int idx = wad.FindLump("E1M1");

            // Канонический порядок: THINGS, LINEDEFS, SIDEDEFS, VERTEXES, SEGS,
            // SSECTORS, NODES, SECTORS, REJECT, BLOCKMAP
            Assert.That(wad.Directory[idx + 1].Name, Is.EqualTo("THINGS"));
            Assert.That(wad.Directory[idx + 2].Name, Is.EqualTo("LINEDEFS"));
            Assert.That(wad.Directory[idx + 3].Name, Is.EqualTo("SIDEDEFS"));
            Assert.That(wad.Directory[idx + 4].Name, Is.EqualTo("VERTEXES"));
            Assert.That(wad.Directory[idx + 8].Name, Is.EqualTo("SECTORS"));
        }
    }
}
```

- [ ] **Step 2: Прогнать все тесты через CLI**

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -nographics -projectPath "D:\Development\doom" `
    -runTests -testPlatform EditMode `
    -testResults "D:\Development\doom\Logs\test-results.xml" `
    -logFile -
```

Ожидаемое: все тесты pass (юнит-тесты + интеграционные). Если интеграционные падают — скорее всего отличается версия Freedoom (см. ассерты на конкретные размеры/индексы — это самый хрупкий момент в этом тесте, но он важен как страховка от тихих регрессий).

- [ ] **Step 3: Удалить smoke-test** (он сделал своё дело — подтвердил, что тестовая инфра работает; держать его дальше нет смысла)

```powershell
Remove-Item Assets/Tests/EditMode/Wad/SmokeTest.cs, Assets/Tests/EditMode/Wad/SmokeTest.cs.meta
```

- [ ] **Step 4: Прогнать тесты ещё раз — smoke ушёл, остальные pass**

- [ ] **Step 5: Закоммитить**

```powershell
git add Assets/Tests/EditMode/Wad
git commit -m "Stage 1: integration tests against freedoom1.wad; drop smoke test"
```

---

### Task 9: Обновить план проекта

**Files:**
- Modify: `docs/doom-unity-remake-plan.md`
- Modify: `CLAUDE.md`

- [ ] **Step 1: Отметить Этап 1 как выполненный в `docs/doom-unity-remake-plan.md`**

Заменить заголовок Этапа 1 на:

```markdown
## Этап 1. WAD-ридер ✅
```

И добавить чекбоксы с галочками к списку шагов внутри этапа (аналогично Этапу 0).

- [ ] **Step 2: Обновить `CLAUDE.md` — раздел `Project status` и `Project layout`**

В `Project status` — отметить, что Stage 1 закрыт, доступен `WadFile` API и Editor-меню.

В `Project layout` — добавить запись:

```markdown
- `Assets/Scripts/Wad/` — WAD-парсер (pure C#, asmdef `Doom.Wad`, `noEngineReferences: true`).
- `Assets/Scripts/Wad/Editor/` — `Tools > Doom > Dump freedoom1.wad` для ручной проверки.
- `Assets/Tests/EditMode/Wad/` — NUnit-тесты под Unity Test Framework.
```

- [ ] **Step 3: Закоммитить**

```powershell
git add docs/doom-unity-remake-plan.md CLAUDE.md
git commit -m "Stage 1 done: mark plan + CLAUDE.md"
```

---

## Definition of done

Этап считается закрытым, когда выполнено всё ниже:

1. `git log --oneline` показывает 8 коммитов с префиксом `Stage 1:` (плюс финальный «mark plan»).
2. Из CLI прогон `-runTests -testPlatform EditMode` возвращает exit 0 и все тесты pass (включая интеграционные на `freedoom1.wad`).
3. В Unity Editor пункт меню `Tools > Doom > Dump freedoom1.wad` выводит в Console сигнатуру `IWAD`, несколько тысяч лампов, список карт `E1M1..E4M9`, и таблицу directory.
4. Парсер не зависит от `UnityEngine` (asmdef `Doom.Wad` собирается с `noEngineReferences: true`).
5. Документы (`docs/doom-unity-remake-plan.md`, `CLAUDE.md`) отражают новое состояние.

## Что НЕ делается в этом этапе (и почему)

- **Парсинг конкретных лампов** (`VERTEXES`, `LINEDEFS`, `PLAYPAL` как структуры) — это Stage 2/4. Здесь только сырые байты.
- **Кэширование лампов в памяти** — не нужно, пока нет проблемы. Лениво читаем со `Stream` на каждый запрос. Если профайлер позже покажет узкое место — добавим кэш.
- **Поддержка PWAD как мерджа поверх IWAD** — конструктор `WadFile` примет PWAD, но логика «PWAD заменяет одноимённые лампы базового IWAD» появится только когда понадобится (вряд ли раньше Stage 7).
- **Группы лампов между маркерами `F_START`/`F_END`, `S_START`/`S_END`** — относится к парсингу флэтов и спрайтов (Stage 4/5).
- **Юникод/нестандартные регистры в именах лампов** — оригинал и Freedoom всегда ASCII upper-case. Сравнения чувствительны к регистру. Если в будущем понадобится — поправим.
