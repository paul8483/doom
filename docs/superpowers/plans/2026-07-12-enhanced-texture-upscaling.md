# Enhanced Texture Upscaling — Implementation Plan

> Выполнять задачи последовательно. Checkbox (`- [ ]`) является журналом
> фактического выполнения; не отмечать шаг до зелёного теста или явно
> записанного blocker.

**Goal:** добавить в Enhanced mode runtime 2× Scale2x upscale для WAD wall
textures, flats и `SKY1`, сохранив native Point textures в Classic и корректный
hot-switch без reload мира.

**Architecture:** pure image transform живёт в `Doom.Graphics`.
`TextureCache` один раз хранит decoded WAD image и lazy native/Enhanced
variants. `WorldRenderContext` и material/animation/sky systems переключают
variant по `GraphicsProfile`; normal maps строятся из Enhanced 2× source.
Спека:
`docs/superpowers/specs/2026-07-12-enhanced-texture-upscaling-design.md`.

**Статус:** Tasks 1–7 automation green (**505 EditMode + 109 PlayMode**,
Windows build ~128 MB). **Interactive: Scale2x 2× не даёт значимого
визуального эффекта** на E1 wall/flat albedo — доработка не закрывается как
visual success. Variant API оставляем; следующий шаг — спека/план на xBRZ
2× (опционально 4×). Notes:
`Logs/enhanced-texture-upscale-baseline-notes.md`.

**Tech Stack:** Unity 6000.4.8f1, C#/.NET profile Unity, Unity Test Framework,
URP 17.4.0, существующие `Doom.Graphics`, `Doom.Game`, `Doom.MapBuild`,
`freedoom1.wad`.

**Content rule:** не добавлять authored/baked/AI-upscaled textures. Все 2× RGBA
и normal maps создаются только в runtime memory из WAD pixels.

**Базовая линия:** Stage 8 закрылся на 459 EditMode + 95 PlayMode. Перед
реализацией проверить актуальные totals: рабочее дерево содержит большой набор
untracked source/test/build/generated файлов, поэтому не удалять, не
перезаписывать и не включать их автоматически в checkpoint.

## Запуск тестов

Каждый прогон пишет новый XML/log. Открытый Unity Editor блокирует batchmode; в
этом случае закрыть Editor или записать `BLOCKED`, не использовать старый XML.

```powershell
# EditMode
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -nographics -projectPath "C:\Development\doom" `
    -runTests -testPlatform EditMode -testFilter "<FILTER>" `
    -testResults "C:\Development\doom\Logs\texture-upscale-<task>-edit.xml" `
    -logFile "C:\Development\doom\Logs\texture-upscale-<task>-edit.log"

# PlayMode — без -nographics
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -projectPath "C:\Development\doom" `
    -runTests -testPlatform PlayMode -testFilter "<FILTER>" `
    -testResults "C:\Development\doom\Logs\texture-upscale-<task>-play.xml" `
    -logFile "C:\Development\doom\Logs\texture-upscale-<task>-play.log"
```

Не добавлять `-quit` к `-runTests`. PASS/FAIL брать из нового XML.

## Инварианты реализации

1. Classic material всегда получает native `Texture2D`, не 2× texture с другой
   фильтрацией.
2. WAD texture/flat декодируется не более одного раза на map load.
3. Enhanced albedo и normal имеют одинаковые 2× dimensions.
4. Одна animation sequence не смешивает native и Enhanced frames.
5. Повторный hot-switch не создаёт texture/material/normal objects.
6. Ошибка одной texture даёт native fallback только для неё.
7. HUD, sprites, weapon view, gameplay и save schema не меняются.

## Source-control gate для каждой Task

1. Перед изменениями проверить `git status --short` и scoped diff.
2. Не использовать bulk add: рабочее дерево уже содержит generated Unity/build
   output и пользовательские untracked файлы.
3. Для каждого нового Unity `.cs`/asset должен существовать корректный `.meta`.
   Предпочтительно дать Unity импортировать файл; если Editor недоступен,
   создать `.meta` в формате соседних scripts с новым GUID.
4. Не commit'ить `Library/`, `Temp/`, raw `Logs/`, captures или `Builds/`.
5. Сначала узкий failing test, затем implementation, затем assembly suite.
6. `Commit checkpoint` — название логического checkpoint, а не разрешение
   выполнять commit.

---

## Task 1: Зафиксировать baseline и variant contracts

**Files:**
- Create: `Logs/enhanced-texture-upscale-baseline-notes.md`
- Create: `Assets/Scripts/Graphics/PixelWrapMode.cs`
- Create: `Assets/Scripts/MapBuild/Rendering/WorldTextureVariant.cs`
- Modify: `Assets/Scripts/MapBuild/Rendering/GraphicsProfile.cs`
- Modify: `Assets/Tests/EditMode/Map/GraphicsProfileTests.cs`

- [ ] **Step 1: Снять исходный baseline.**

Зафиксировать commit/branch, Unity/GPU/API, разрешение, актуальные test totals и
активный Enhanced profile. На E1M1 и E1M7 записать:

- map build time;
- время Classic → Enhanced и Enhanced → Classic;
- `WorldRenderContext.TextureCount`/`MaterialCount`/normal count;
- managed memory и доступную Unity texture-memory metric;
- dimensions/filter mode representative wall, flat и `SKY1`.

Raw profiler/captures оставить локальными; в notes сохранить команды, camera
poses и числа.

- [ ] **Step 2: Добавить pure wrap contract.**

Определить `PixelWrapMode` с явными стабильными значениями:

- `Clamp`;
- `RepeatX`;
- `RepeatXY`.

Не использовать `UnityEngine.TextureWrapMode` в `Doom.Graphics`.

- [ ] **Step 3: Добавить runtime variant contract.**

Определить `WorldTextureVariant.Native` и `Enhanced2X`. Добавить в
`GraphicsProfile` declarative поле `UpscaleWorldTextures2X`: `false` для
Classic, `true` для Enhanced. Capability fallback пока не отключает CPU path.

- [ ] **Step 4: Написать/запустить profile tests.**

Закрепить exact flags и mapping profile → variant. Запустить:

```text
Doom.Map.Tests.GraphicsProfileTests
```

**Commit checkpoint:** `graphics: define enhanced texture variant contracts`

---

## Task 2: Реализовать pure Scale2x transform

**Files:**
- Create: `Assets/Scripts/Graphics/PixelArtUpscaler.cs`
- Create: `Assets/Tests/EditMode/Graphics/PixelArtUpscalerTests.cs`
- Modify: `Assets/Tests/EditMode/Graphics/Doom.Graphics.Tests.asmdef` только
  если существующий glob/import этого требует

- [ ] **Step 1: Написать failing core fixtures.**

Проверить:

- 1×1 и uniform image → exact duplicated 2× pixels;
- 3×3 horizontal/vertical line;
- 3×3 diagonal с exact ожидаемым 6×6 RGBA;
- input array не мутирует;
- output dimensions и byte length;
- invalid null/dimensions/RGBA length;
- checked overflow до allocation.

- [ ] **Step 2: Реализовать `Scale2X`.**

Для центрального `E` и соседей `B` (up), `D` (left), `F` (right), `H` (down):

```text
E0 = D == B && D != H && B != F ? D : E
E1 = B == F && B != D && F != H ? F : E
E2 = D == H && D != B && H != F ? D : E
E3 = H == F && D != H && B != F ? F : E
```

Записать `E0..E3` в 2×2 output. Вынести alpha-aware exact equality:
полностью прозрачные pixels равны независимо от RGB.

- [ ] **Step 3: Реализовать border sampling.**

- `Clamp`: clamp X/Y;
- `RepeatX`: modulo X, clamp Y;
- `RepeatXY`: modulo X/Y.

Не создавать временные pixel arrays/tuples во внутреннем цикле; работать с
RGBA offsets или compact value type без per-pixel allocation.

- [ ] **Step 4: Добавить alpha/border fixtures.**

Проверить transparent hidden RGB, left/right seam для `RepeatX` и flat corners
для `RepeatXY`.

- [ ] **Step 5: Запустить Graphics suite.**

```text
Doom.Graphics.Tests.PixelArtUpscalerTests
Doom.Graphics.Tests
```

**Commit checkpoint:** `graphics: add deterministic scale2x image transform`

---

## Task 3: Добавить native/Enhanced variants в `TextureCache`

**Files:**
- Modify: `Assets/Scripts/MapBuild/TextureCache.cs`
- Create: `Assets/Tests/PlayMode/TextureUpscalePlayTests.cs`
- Modify: `Assets/Tests/PlayMode/Doom.Stage3.PlayTests.asmdef` только при
  необходимости

- [ ] **Step 1: Написать failing cache-level PlayMode tests.**

На synthetic/runtime cache или загруженной E1M1 texture проверить:

- native dimensions совпадают с decoded WAD;
- Enhanced dimensions ровно 2×;
- оба albedo — Point;
- повторный запрос возвращает тот же object;
- native и Enhanced — разные objects;
- variant count не растёт после повторных запросов.

- [ ] **Step 2: Разделить logical source и GPU variants.**

Заменить один `texCache` на явные cache entries по
`(name, WorldTextureVariant)`. `sourceCache` остаётся единственным native
decoded source. Сохранять `isFlat`/wrap metadata вместе с source entry, чтобы
не классифицировать lump повторно.

- [ ] **Step 3: Добавить явный API.**

Реализовать:

- `GetTexture(string name, WorldTextureVariant variant)`;
- `GetTextureForProfile(string name, GraphicsProfile profile)`;
- diagnostics counters/estimated RGBA bytes.

Существующий `GetTexture(name)` оставить как совместимый native wrapper до
миграции всех consumers. Не делать его зависимым от mutable global mode: это
скрывает ошибки pre-warm.

- [ ] **Step 4: Реализовать lazy Enhanced source.**

При `Enhanced2X` вызвать `PixelArtUpscaler.Scale2X` на cached native
`DecodedImage`. Выбирать:

- flat → `RepeatXY`;
- wall/SKY1 → `RepeatX`;
- placeholder → `Clamp`.

Upload выполняется через существующий vertical flip. Albedo создаётся без
mipmaps, sRGB, Point, Repeat и текущего aniso level.

- [ ] **Step 5: Реализовать per-texture fallback.**

Если transform/allocation/upload Enhanced variant завершился исключением:
один раз записать warning и вернуть native object. Не кэшировать бесконечные
повторные попытки: entry должен хранить failed/fallback state.

- [ ] **Step 6: Запустить cache tests.**

```text
Doom.Stage3.PlayTests.TextureUpscalePlayTests
```

**Commit checkpoint:** `mapbuild: cache native and enhanced world textures`

---

## Task 4: Переключать material textures вместе с profile

**Files:**
- Modify: `Assets/Scripts/MapBuild/Rendering/DoomMaterialFactory.cs`
- Modify: `Assets/Scripts/MapBuild/Rendering/WorldRenderContext.cs`
- Modify: `Assets/Scripts/MapBuild/Rendering/GraphicsModeController.cs`
- Modify: `Assets/Scripts/MapBuild/MapLoader.cs`
- Modify: `Assets/Tests/PlayMode/GraphicsModePlayTests.cs`
- Modify: `Assets/Tests/PlayMode/GraphicsResourceLifetimePlayTests.cs`
- Modify: `Assets/Tests/PlayMode/TextureUpscalePlayTests.cs`

- [ ] **Step 1: Написать failing hot-switch assertions.**

Расширить тест:

- захватить logical texture name/native object/dimensions;
- после Enhanced material с тем же logical name использует 2× object;
- после Classic восстанавливается exact native object;
- HP/position/world state не меняются;
- после warm-up 20 switches не меняют counts.

- [ ] **Step 2: Сделать material registration logical.**

При регистрации world material хранить `textureName`, `masked` и material.
Не восстанавливать name из `Texture2D.name`, если logical name уже известен.
Sprite materials не включать в этот registry.

- [ ] **Step 3: Привязать `TextureCache` к context.**

`MapLoader` связывает map cache с `WorldRenderContext` до первого
`RegisterContext`. `ApplyProfile` получает target variant, подготавливает
textures для registered materials и только затем retarget'ит shaders/surface
properties.

- [ ] **Step 4: Обновить normal lookup.**

Mapping `albedo object → (name, variant)` должен позволять material factory
получить normal, соответствующую Enhanced2X. Classic не создаёт normal.

- [ ] **Step 5: Проверить rollback.**

Добавить test hook/failing provider или targeted unit seam: исключение при
одной Enhanced texture не оставляет material без texture; глобальное
исключение profile apply возвращает Classic native variants.

- [ ] **Step 6: Запустить hot-switch/lifetime tests.**

```text
Doom.Stage3.PlayTests.GraphicsModePlayTests
Doom.Stage3.PlayTests.GraphicsResourceLifetimePlayTests
Doom.Stage3.PlayTests.TextureUpscalePlayTests
```

**Commit checkpoint:** `rendering: hot-switch native and enhanced texture variants`

---

## Task 5: Согласовать normals, animations, fluids и sky

**Files:**
- Modify: `Assets/Scripts/MapBuild/TextureCache.cs`
- Modify: `Assets/Scripts/MapBuild/Rendering/AnimatedSurfaceSystem.cs`
- Modify: `Assets/Scripts/MapBuild/Rendering/WadSkyRenderer.cs`
- Modify: `Assets/Scripts/MapBuild/MapLoader.cs`
- Modify: `Assets/Tests/PlayMode/EnhancedMaterialPlayTests.cs`
- Modify: `Assets/Tests/PlayMode/EnhancedAtmospherePlayTests.cs`
- Modify: `Assets/Tests/PlayMode/TextureUpscalePlayTests.cs`

- [ ] **Step 1: Написать failing normal tests.**

В Enhanced albedo и `_BumpMap` должны иметь одинаковые 2× dimensions.
Повторный material configure не создаёт вторую normal.

- [ ] **Step 2: Генерировать normal из 2× source.**

Для Enhanced variant передать upscaled `DecodedImage` в
`NormalMapGenerator.Generate`. Normal RGBA upload остаётся linear/Bilinear.
После успешной normal generation разрешить cache entry освободить CPU 2× RGBA,
если albedo уже uploaded и больше нет CPU consumer.

- [ ] **Step 3: Мигрировать animation frames.**

`AnimatedSurfaceSystem` хранит logical frame names. В `ApplyProfile` разрешает
массив target textures через cache и атомарно обновляет `_MainTex`/`_MainTexB`.
На Classic очищает property block и восстанавливает native shared material.

- [ ] **Step 4: Мигрировать sky.**

`WadSkyRenderer` хранит `TextureCache`, а не один immutable `skyTexture`.
`ApplyProfile` выбирает native или Enhanced2X `SKY1`, устанавливает
`mainTexture` и `_MainTex`. Classic остаётся Point/native.

- [ ] **Step 5: Исправить pre-warm order.**

`MapLoader` до закрытия WAD декодирует используемые logical names. Если
persisted mode Enhanced, разрешается сразу построить 2× variants; если Classic
— сохранить native decoded sources, достаточные для будущего hot-switch.

- [ ] **Step 6: Проверить animation consistency.**

Assertions:

- current/next fluid frames имеют одинаковый variant и dimensions;
- Classic frames native;
- Enhanced frames 2×;
- `SKY1` переключается вместе с profile;
- missing frame fallback не даёт mixed variant.

- [ ] **Step 7: Запустить material/atmosphere tests.**

```text
Doom.Stage3.PlayTests.EnhancedMaterialPlayTests
Doom.Stage3.PlayTests.EnhancedAtmospherePlayTests
Doom.Stage3.PlayTests.TextureUpscalePlayTests
```

**Commit checkpoint:** `rendering: upscale normals animations fluids and sky`

---

## Task 6: Resource lifetime и performance gate

**Files:**
- Modify: `Assets/Scripts/MapBuild/TextureCache.cs`
- Modify: `Assets/Scripts/MapBuild/Rendering/WorldRenderContext.cs`
- Modify: `Assets/Tests/PlayMode/GraphicsResourceLifetimePlayTests.cs`
- Modify: `Logs/enhanced-texture-upscale-baseline-notes.md`

- [ ] **Step 1: Закрыть CPU/GPU ownership.**

Проверить, что:

- все native/Enhanced albedo и normals зарегистрированы один раз;
- `Dispose` уничтожает каждый object один раз;
- fallback alias на native не регистрируется/уничтожается дважды;
- `makeNoLongerReadable` включён после upload там, где runtime не читает
  `Texture2D`;
- disposable 2× CPU buffers освобождаются после последнего consumer.

- [ ] **Step 2: Добавить warm-up stability test.**

На E1M7:

1. загрузить Classic;
2. включить Enhanced;
3. дождаться завершения synchronous apply;
4. записать counts;
5. выполнить 20 switches;
6. проверить неизменность texture/material/normal/variant counts;
7. reload scene и проверить teardown без MissingReferenceException/leak growth.

- [ ] **Step 3: Измерить cost.**

Сравнить baseline и implementation:

- E1M1/E1M7 load time;
- первый и повторный mode switch;
- managed/texture memory;
- native/upscaled/normal counts и estimated bytes;
- frame time после warm-up.

Не задавать искусственный бюджет задним числом. Записать реальные числа и
объяснить отклонения. Если cost неприемлем, остановиться перед full-suite и
выбрать одну оптимизацию: освобождение CPU 2× buffers, generation only for
bound materials либо incremental warm-up.

- [ ] **Step 4: Запустить lifetime test.**

```text
Doom.Stage3.PlayTests.GraphicsResourceLifetimePlayTests
```

**Commit checkpoint:** `rendering: bound enhanced texture memory and lifetime`

---

## Task 7: E1 regression, Windows build и visual sign-off

**Files:**
- Modify: `Assets/Tests/PlayMode/E1MapSmokePlayTests.cs`
- Modify: `Assets/Tests/PlayMode/GraphicsBaselineCaptureTests.cs` если нужен
  отдельный upscale capture mode
- Modify: `Logs/enhanced-texture-upscale-baseline-notes.md`
- Modify: `docs/doom-unity-remake-plan.md` только после фактического закрытия
- Modify: `CLAUDE.md` только после фактического закрытия

- [ ] **Step 1: Расширить E1 smoke.**

Для E1M1–E1M9 проверить Classic и Enhanced:

- карта построена без exception/pink materials;
- representative world materials имеют ожидаемый variant;
- animated sequence не mixed-resolution;
- отсутствуют NaN meshes и gameplay regressions.

- [ ] **Step 2: Запустить полные suites.**

Без `-testFilter` запустить новые EditMode и PlayMode XML. Сравнить totals с
актуальной baseline, а не только с историческими 459/95. Все failures
разобрать; unrelated pre-existing failure записать с evidence, не скрывать.

- [ ] **Step 3: Сделать Windows standalone build.**

Использовать существующий `Tools > Doom > Build Windows Standalone` или
соответствующий editor method. Проверить отсутствие missing shader/script и
запуск без Editor-only dependencies.

- [ ] **Step 4: Выполнить deterministic captures.**

Для одинаковых camera poses E1M1/E1M3/E1M7 сохранить локально:

- Classic native;
- Stage 8 Enhanced/native reference, если доступен;
- Enhanced 2×.

Проверить brick/metal/door/flats, oblique angles, masked middle textures,
fluid cross-fade и sky. Raw PNG не commit'ить.

- [ ] **Step 5: Провести интерактивный sign-off.**

В Windows build:

- переключить Classic → Enhanced → Classic в pause Options;
- проверить отсутствие заметного изменения gameplay state;
- принять улучшение диагоналей без bilinear mush;
- проверить отсутствие transparent fringes;
- проверить E1M1/E1M3/E1M7 и resize 4:3/16:9;
- записать first/repeat switch responsiveness.

- [ ] **Step 6: Закрыть документацию.**

Только после green suites/build/sign-off:

- отметить spec/plan завершёнными с датой;
- записать новые exact test totals и performance measurements;
- обновить roadmap и `CLAUDE.md`;
- перечислить известные ограничения: Scale2x не создаёт новых художественных
  деталей, sprites/HUD остаются native, mipmaps/xBRZ отложены.

**Commit checkpoint:** `graphics: complete enhanced texture upscaling`

---

## Порядок и зависимости

```text
Task 1 contracts/baseline
        |
        v
Task 2 pure Scale2x
        |
        v
Task 3 TextureCache variants
        |
        v
Task 4 material hot-switch
        |
        v
Task 5 normals/animations/sky
        |
        v
Task 6 lifetime/performance
        |
        v
Task 7 E1/build/sign-off/docs
```

Tasks 3–5 нельзя безопасно выполнять параллельно: cache API и ownership
определяют material, animation и sky integration. Pure Task 2 можно поручить
отдельному worker только после фиксации contracts Task 1.

## Stop conditions

Остановиться и пересогласовать дизайн, если:

1. Classic capture меняется после возврата из Enhanced.
2. Для hot-switch требуется повторно открыть уже закрытый WAD вместо
   использования decoded source cache.
3. Animated frames или fluid pair получают разные variants/dimensions.
4. Texture memory растёт при каждом переключении или scene reload.
5. Scale2x создаёт массовые alpha fringes/ложные диагонали на Freedoom
   textures, которые нельзя исправить alpha/wrap policy.
6. E1M7 memory/load cost неприемлем даже после освобождения CPU 2× buffers и
   generation only for bound materials.
7. Реализация требует менять UV, geometry, gameplay или save schema.
8. Unity Editor занят и новые test XML/build evidence получить невозможно:
   отметить `BLOCKED`, не объявлять PASS.

## Definition of Done

- [ ] Pure Scale2x fixtures и Freedoom integration зелёные.
- [ ] Classic использует exact native texture objects/Point.
- [ ] Enhanced world/fluids/sky используют cached 2× objects/Point.
- [ ] Enhanced normals построены из 2× source.
- [ ] Hot-switch и rollback сохраняют gameplay и exact Classic restore.
- [ ] Counts стабильны после 20 switches и scene reload.
- [ ] E1M1–E1M9 smoke проходит в обоих режимах.
- [ ] Полные EditMode/PlayMode suites зелёные с новыми XML.
- [ ] Windows build и interactive visual sign-off завершены.
- [ ] Performance/memory numbers и ограничения записаны.
- [ ] Roadmap/`CLAUDE.md` обновлены только после фактической приёмки.
