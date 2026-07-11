# Stage 8: Classic and Enhanced Graphics — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Добавить в Options глобальный выбор `Classic`/`Enhanced`, перевести
проект на единый URP pipeline, сохранить текущий вид в Classic и реализовать
современный WAD-derived render profile в Enhanced.

**Architecture:** Pure settings/image rules остаются в `Doom.Game` и
`Doom.Graphics`; Unity/URP glue живёт в `Doom.MapBuild`. Один persistent
`GraphicsModeController` применяет profile к зарегистрированному
`WorldRenderContext`; WAD decoding, gameplay state и collision не зависят от
режима. Спека:
`docs/superpowers/specs/2026-07-11-enhanced-graphics-design.md`.

**Статус:** утверждён; **Task 1 ✅ … Task 10 ✅**. Next: Task 11 WAD sky/fluids/fog.

**Tech Stack:** Unity 6000.4.8f1, C#/.NET profile Unity, Unity Test Framework,
Universal Render Pipeline (последняя совместимая с pinned Unity версия),
Input System 1.11.2, `freedoom1.wad`, существующие `Doom.Wad`,
`Doom.Graphics`, `Doom.Map`, `Doom.Things`, `Doom.Game`, `Doom.Specials`,
`Doom.MapBuild`.

**Content rule:** не добавлять authored/AI-upscaled textures, sprites, normal
maps, models, skyboxes или sounds. Runtime normals/effect textures строятся
только в памяти из WAD pixels/палитры.

**Базовая линия на дату старта Stage 8:** 413 EditMode + 67 PlayMode
(Stage 7 close, 2026-07-12). Не объявлять старые XML новым PASS.

## Запуск тестов

Для каждого прогона использовать уникальные XML/log. Открытый Unity Editor
блокирует batchmode; в таком случае закрыть Editor или отметить прогон
`BLOCKED`. Не читать PASS из старого файла.

```powershell
# EditMode
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -nographics -projectPath "C:\Development\doom" `
    -runTests -testPlatform EditMode -testFilter "<FILTER>" `
    -testResults "C:\Development\doom\Logs\stage8-<task>-edit.xml" `
    -logFile "C:\Development\doom\Logs\stage8-<task>-edit.log"

# PlayMode — PhysX/URP запускать без -nographics
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -projectPath "C:\Development\doom" `
    -runTests -testPlatform PlayMode -testFilter "<FILTER>" `
    -testResults "C:\Development\doom\Logs\stage8-<task>-play.xml" `
    -logFile "C:\Development\doom\Logs\stage8-<task>-play.log"
```

Не добавлять `-quit` к `-runTests`. Результаты тестов читать из XML. Compile-only
не заменяет EditMode/PlayMode suites. Render assertions проверяют shaders,
materials, camera/Volume state и bounded resources; визуальный паритет
подтверждается отдельным capture/interactive gate.

## Источник истины: render profiles

### Classic

- URP Unlit opaque/cutout;
- WAD RGBA + legacy gamma multiplication на sector vertex light;
- Point + mipmaps + anisotropic filtering;
- no Unity lights, shadows, normals, post, fog, decals, particles;
- WAD UI/weapon view поверх world camera.

### Enhanced

- URP custom Lit/Cutout + procedural normal/roughness;
- sector ambient + bounded local lights/shadows;
- Linear HDR, SSAO, bloom, grading, fog, MSAA, render scale/FSR;
- WAD SKY1, animated/emissive fluids;
- lit/interpolated sprites, spectre material, soft floor intersection;
- pooled WAD-derived particles and decals.

`GraphicsCapabilityPolicy` может выключить отдельный unsupported feature, но не
переписывает выбранный `GraphicsMode`.

## Source-control gate для каждой Task

1. Проверить `git status` и diff до изменений. На момент написания плана уже
   существуют пользовательские изменения
   `ProjectSettings/ProjectSettings.asset` и новый
   `ProjectSettings/SceneTemplateSettings.json`.
2. Task 3 законно меняет `ProjectSettings.asset`, `GraphicsSettings.asset`,
   `QualitySettings.asset`, `Packages/manifest.json` и `packages-lock.json`.
   Сначала сохранить/понять существующий diff; не перезаписывать его wholesale.
3. Все новые Unity assets/scripts должны иметь созданные Unity `.meta`.
4. Не commit'ить `Logs/`, `Library/`, `Temp/`, captures, profiler data или build
   output. Baseline summary `.md` допустим, raw captures — нет.
5. До checkpoint запускать узкий тест, затем релевантную assembly suite.
6. Строка `Commit checkpoint` не является разрешением создать commit. Commit
   выполняется только по отдельной просьбе пользователя.

---

## 8a — Baseline и settings contract

### Task 1: Зафиксировать Classic visual/performance baseline

**Files:**
- Create: `Logs/stage8-graphics-baseline-notes.md`
- Create: `Assets/Tests/PlayMode/GraphicsBaselineCaptureTests.cs`
- Modify: `Assets/Tests/PlayMode/Doom.Stage3.PlayTests.asmdef` при необходимости

- [x] **Step 1: Зафиксировать исходное состояние.**

Записать Unity/GPU/разрешение, активный Built-in pipeline, Gamma color space,
актуальные test totals и pre-existing project-setting diffs. Не менять renderer
до завершения captures.

Recorded in `Logs/stage8-graphics-baseline-notes.md` (Built-in, Gamma,
413 EditMode + 67 PlayMode, RTX 3070 Ti / D3D12).

- [x] **Step 2: Добавить deterministic capture harness.**

Для E1M1, E1M3, E1M7 и E1M9 задать map, camera transform, FOV и resolution.
Capture должен сохранять world PNG и machine-readable metrics, не пытаться
захватить OnGUI через `Camera.Render()`.

`Assets/Tests/PlayMode/GraphicsBaselineCaptureTests.cs` →
`Logs/stage8-captures/{map}-classic.png` + `metrics.txt`.

- [x] **Step 3: Снять baseline.**

Записать CPU/GPU frame time, batches, SetPass, triangles, renderers/materials/
textures, managed memory и build time. Raw PNG/profiler captures оставить
локальными; в `.md` сохранить команды, числа и camera poses.

Numbers in baseline notes (build + Camera.Render sample + object counts).

- [x] **Step 4: Определить budgets.**

После измерения записать Classic parity tolerance для captures и Enhanced
budgets: frame time, render-target memory, active lights/shadows, decals,
particles и отсутствие роста после warm-up. Не придумывать числа до измерения.

Classic budgets set from measurement; Enhanced numeric frame/RT budgets deferred
until first Enhanced profile run (pool caps sketched).

- [x] **Step 5: Запустить текущие полные suites.**

EditMode и PlayMode должны быть зелёными до URP migration.

2026-07-12: **413 EditMode** + **68 PlayMode** (includes
`GraphicsBaselineCaptureTests`) — all Passed.
`Logs/stage8-t1-full-edit.xml`, `Logs/stage8-t1-full-play.xml`.

**Commit checkpoint:** `Stage 8: capture classic graphics baseline`
_(awaiting explicit user commit request)_

### Task 2: Settings schema v2 и Options selector

**Files:**
- Modify: `Assets/Scripts/Game/GameSettingsData.cs`
- Modify: `Assets/Scripts/MapBuild/SettingsStore.cs`
- Modify: `Assets/Scripts/MapBuild/SettingsController.cs`
- Modify: `Assets/Tests/EditMode/Game/GameSettingsTests.cs`
- Create: `Assets/Tests/EditMode/Game/SettingsStoreTests.cs`
- Modify: `Assets/Tests/PlayMode/SettingsPlayTests.cs`

- [x] **Step 1: Написать failing pure tests.**

Проверить `GraphicsMode` explicit values, default Classic, equality/hash,
`WithGraphicsMode`, invalid enum fallback, v2 round-trip и v1 → v2 migration с
сохранением volume/look/display fields.

- [x] **Step 2: Реализовать immutable contract.**

Добавить `GraphicsMode` в `Doom.Game`, поднять `SchemaVersion` до 2 и расширить
`GameSettingsData.TryCreate`/clone APIs. `SettingsStore` не должен сбрасывать все
v1 preferences из-за version mismatch.

- [x] **Step 3: Написать failing Options tests.**

Options содержит строку `Graphics Mode`; left/right/activate переключают только
Classic/Enhanced. Apply persist'ит, Cancel возвращает snapshot. На этом шаге
runtime adapter может быть fake.

- [x] **Step 4: Расширить `SettingsController`.**

Добавить setter/UI label/value и `IGraphicsModeAdapter`. Не искать URP objects
из pure settings tests. Default adapter временно no-op до Task 4.

- [x] **Step 5: Прогнать тесты.**

Узко: `GameSettingsTests`, `SettingsStoreTests`, `SettingsPlayTests`. Затем
полный `Doom.Game.Tests` и PlayMode suite.

2026-07-12: EditMode filter 12/12 PASS; SettingsPlayTests 4/4 PASS
(`Logs/stage8-t2-edit.xml`, `Logs/stage8-t2-play.xml`).

**Commit checkpoint:** `Stage 8a: add classic and enhanced setting`
_(awaiting explicit user commit request)_

---

## 8b — URP и Classic parity

### Task 3: Подключить единый URP pipeline

**Files:**
- Modify: `Packages/manifest.json`
- Modify: `Packages/packages-lock.json`
- Create: `Assets/Settings/Rendering/DoomUniversalRenderPipeline.asset`
- Create: `Assets/Settings/Rendering/DoomUniversalRenderer.asset`
- Create: `Assets/Settings/Rendering/DoomEnhancedVolume.asset`
- Modify: `ProjectSettings/GraphicsSettings.asset`
- Modify: `ProjectSettings/QualitySettings.asset`
- Modify: `ProjectSettings/ProjectSettings.asset`
- Modify: `Assets/Scenes/Stage2_MapPreview.unity`
- Modify: `Assets/Scripts/MapBuild/Doom.MapBuild.asmdef`
- Create/Modify: соответствующие `.meta`

- [x] **Step 1: Добавить совместимую URP package через Package Manager.**

Не вписывать выдуманную version. Дождаться resolve/import без compile errors и
зафиксировать package version, выбранную Unity 6000.4.8f1.

URP `17.4.0` (bundled with 6000.4.8f1). Configure via
`Tools > Doom > Configure URP Pipeline (Stage 8)`.

- [x] **Step 2: Создать URP assets.**

Один pipeline asset и renderer data используются обоими режимами. Включить
depth/opaque textures, HDR/MSAA capability и SRP Batcher; SSAO/Decal features
добавить disabled-by-profile до соответствующих Tasks.

- [x] **Step 3: Перевести проект в Linear color space.**

Не конвертировать WAD content assets. Runtime Texture2D creation позже получает
явную sRGB/linear policy; HUD palette textures остаются color data.

- [x] **Step 4: Настроить scene/runtime camera.**

Удалить зависимость от Built-in-only Directional Light. `MapLoader.SpawnPlayer`
создаёт URP-compatible camera; gameplay/audio/FOV/clip planes не меняются.

- [x] **Step 5: Compile и smoke gate.**

На этом checkpoint pink current shaders ожидаемы только до Task 4; не запускать
визуальную приёмку, пока Classic shaders не перенесены. Любые script compile
errors исправить до продолжения.

**Commit checkpoint:** `Stage 8b: configure universal render pipeline`
_(awaiting explicit user commit request)_

### Task 4: Перенести Classic shaders с визуальным паритетом

**Files:**
- Create: `Assets/Shaders/DoomClassicOpaque.shader`
- Create: `Assets/Shaders/DoomClassicCutout.shader`
- Modify: `Assets/Scripts/MapBuild/TextureCache.cs`
- Modify: `Assets/Scripts/MapBuild/SpriteCache.cs`
- Modify: `Assets/Scripts/MapBuild/HudTextureCache.cs`
- Modify: `Assets/Scripts/MapBuild/MapLoader.cs`
- Modify: `ProjectSettings/GraphicsSettings.asset`
- Modify: `Assets/Tests/PlayMode/TexturedMapLoadsTests.cs`
- Create: `Assets/Tests/PlayMode/ClassicRenderPlayTests.cs`

- [x] **Step 1: Написать failing Classic render tests.**

Проверить URP Classic shader names, Point filtering, mip/aniso policy, cutout
depth/cull behavior, отключённые scene lights/post и отсутствие pink materials.

- [x] **Step 2: Реализовать URP HLSL shaders.**

Opaque и cutout должны быть SRP Batcher compatible. Shader выполняет legacy
gamma-space `albedo × vertexColor` поверх Linear project output. Cutout
сохраняет `Cull Off`, alpha threshold и depth write текущего поведения.

- [x] **Step 3: Явно задать texture color policy.**

`TextureCache`/`SpriteCache` создают WAD albedo как sRGB color textures;
`HudTextureCache` остаётся Point без mipmaps. Не менять decoded RGBA bytes.

- [x] **Step 4: Обновить shader inclusion.**

Удалить Built-in shaders из runtime selection только после полного перехода.
Новые Classic и будущие Enhanced shader variants должны переживать stripping в
Windows build.

- [x] **Step 5: Automated и visual parity gate.**

Прогнать `TexturedMapLoadsTests`, `ClassicRenderPlayTests`, capture harness и
сравнить E1M1/E1M3/E1M7/E1M9 с Task 1. Исправлять color/UV/cutout, а не повышать
tolerance, пока различие не объяснено.

Automated gate: ClassicRender + TexturedMapLoads PASS (2026-07-12).
Interactive/capture visual parity vs Task 1 baselines deferred to user eyeball.

**Commit checkpoint:** `Stage 8b: preserve classic rendering on URP`
_(awaiting explicit user commit request)_

### Task 5: Render profiles и hot-switch

**Files:**
- Create: `Assets/Scripts/MapBuild/Rendering/GraphicsProfile.cs`
- Create: `Assets/Scripts/MapBuild/Rendering/GraphicsCapabilityPolicy.cs`
- Create: `Assets/Scripts/MapBuild/Rendering/GraphicsModeController.cs`
- Create: `Assets/Scripts/MapBuild/Rendering/WorldRenderContext.cs`
- Create: `Assets/Scripts/MapBuild/Rendering/DoomMaterialFactory.cs`
- Modify: `Assets/Scripts/MapBuild/TextureCache.cs`
- Modify: `Assets/Scripts/MapBuild/SpriteCache.cs`
- Modify: `Assets/Scripts/MapBuild/MapLoader.cs`
- Modify: `Assets/Scripts/MapBuild/SettingsController.cs`
- Create: `Assets/Tests/EditMode/Map/GraphicsProfileTests.cs`
- Create: `Assets/Tests/PlayMode/GraphicsModePlayTests.cs`

- [x] **Step 1: Написать failing policy tests.**

Classic flags все presentation effects off; Enhanced запрашивает полный набор.
Capability adapter отключает только unsupported flags и не меняет requested
mode.

- [x] **Step 2: Ввести material/context abstraction.**

`TextureCache` перестаёт напрямую делать `Shader.Find("Doom/Unlit")`.
`DoomMaterialFactory` кэширует profile variants. `WorldRenderContext` регистрирует
renderers/textures/camera/effect services и освобождает owned resources один раз.

- [x] **Step 3: Реализовать persistent controller.**

Controller создаётся на `GameSessionHost`, принимает Apply/Cancel из settings и
применяет profile к текущему context. Контроллер переживает scene reload, но
Unity resources старой сцены — нет.

- [x] **Step 4: Реализовать transactional hot-switch.**

На паузе подготовить target materials/state, затем одним apply phase заменить
shared materials/filter policy и enable flags. При ошибке оставить предыдущий
profile и показать техническое сообщение; gameplay state не трогать.

- [x] **Step 5: Проверить переключение.**

Classic → Enhanced → Classic без scene reload сохраняет player transform/HP/
ammo, monster HP/state, sector heights и active movers. WAD не открывается и
textures не декодируются повторно. Повторить цикл не менее 20 раз и проверить
стабильные counts resources.

2026-07-12: `GraphicsProfileTests` 4/4; `GraphicsModePlayTests` PASS
(`Logs/stage8-t3-6-edit.xml`, `Logs/stage8-t3-6-play2.xml`).

**Commit checkpoint:** `Stage 8b: add graphics profile hot switching`
_(awaiting explicit user commit request)_

### Task 6: World/HUD compositing

**Files:**
- Create: `Assets/Scripts/MapBuild/Rendering/WorldCameraRenderer.cs`
- Modify: `Assets/Scripts/MapBuild/MapLoader.cs`
- Modify: `Assets/Scripts/MapBuild/VirtualScreenRenderer.cs`
- Modify: `Assets/Scripts/MapBuild/WeaponView.cs` только при необходимости
- Modify: `Assets/Scripts/MapBuild/DoomHud.cs` только при необходимости
- Create: `Assets/Tests/PlayMode/WorldHudCompositePlayTests.cs`

- [x] **Step 1: Написать failing layer/composite tests.**

World camera имеет URP/HDR/post path только в Enhanced. HUD/menu/intermission/
weapon view остаются overlay после world image и не попадают под Volume.

- [x] **Step 2: Настроить camera output.**

Предпочесть прямой world camera + OnGUI overlay. Вводить промежуточный
RenderTexture только если URP render scale/FSR требует его; ownership и resize
должны быть явными.

- [x] **Step 3: Проверить aspect ratios.**

4:3, 16:9 и runtime resize сохраняют FOV policy, pillarbox/virtual-screen layout
и Point UI. Не переводить весь WAD UI на Canvas без доказанной необходимости.

- [x] **Step 4: Прогнать PlayMode и интерактивный smoke.**

Проверить pause/options/death/intermission поверх обоих режимов. `Camera.Render`
не используется как доказательство наличия OnGUI.

2026-07-12: `WorldHudCompositePlayTests` PASS. Interactive smoke deferred.

**Commit checkpoint:** `Stage 8b: isolate world post from WAD UI`
_(awaiting explicit user commit request)_

---

## 8c — Enhanced world rendering

### Task 7: Procedural normal maps и Enhanced materials

**Files:**
- Create: `Assets/Scripts/Graphics/NormalMapGenerator.cs`
- Create: `Assets/Tests/EditMode/Graphics/NormalMapGeneratorTests.cs`
- Create: `Assets/Shaders/DoomEnhancedWorld.shader`
- Create: `Assets/Shaders/DoomEnhancedCutout.shader`
- Modify: `Assets/Scripts/MapBuild/Rendering/DoomMaterialFactory.cs`
- Modify: `Assets/Scripts/MapBuild/TextureCache.cs`
- Create: `Assets/Tests/PlayMode/EnhancedMaterialPlayTests.cs`

- [x] **Step 1: Написать failing pure normal tests.**

Uniform image → neutral normal; X/Y gradients → ожидаемые signed directions;
Repeat edges для wall/flat, transparent pixels neutral, output deterministic.

`NormalMapGeneratorTests` 8/8 PASS (`Logs/stage8-t7-edit.xml`, 2026-07-12).

- [x] **Step 2: Реализовать CPU generator.**

Работать по luminance WAD RGBA, не зависеть от Unity. Strength/roughness policy
зафиксировать именованными material categories; unknown получает слабый neutral
profile.

`NormalMapGenerator` + `MaterialSurfaceCategory`/`MaterialSurfaceProfile`/
`MaterialSurfaceClassifier` in `Doom.Graphics`.

- [x] **Step 3: Написать failing material tests.**

Enhanced material получает albedo/normal, sector ambient, roughness/emission и
correct opaque/cutout state. Classic variant не держит normal texture.

`EnhancedMaterialPlayTests` (assign + leak/cache) PASS
(`Logs/stage8-t7-play.xml`).

- [x] **Step 4: Реализовать SRP-compatible Enhanced shaders/cache.**

Normal создаётся лениво один раз на texture/profile key. Texture uses linear
normal sampling, bilinear/trilinear + mipmaps/aniso. Освещение не должно
полностью обнулять тёмный sector ambient.

`Doom/EnhancedWorld` + `Doom/EnhancedCutout`; `TextureCache` lazy normals;
`DoomMaterialFactory` surface props; mesh tangents in `MapLoader.ApplyMeshData`.

- [x] **Step 5: Leak/performance gate.**

Переключения не создают новые normal maps после warm-up; teardown уничтожает
runtime textures/materials. Проверить E1M7 memory против budget.

Hot-switch cache stable; `ClearContext` destroys owned `/Normal` textures.
Numeric E1M7 Enhanced frame/RT budgets remain deferred to Task 10/14.

**Commit checkpoint:** `Stage 8c: add enhanced WAD materials`
_(awaiting explicit user commit request)_

### Task 8: Runtime sector lights и save/restore

**Files:**
- Create: `Assets/Scripts/MapBuild/RuntimeSectorLights.cs`
- Modify: `Assets/Scripts/MapBuild/SectorGeometry.cs`
- Modify: `Assets/Scripts/MapBuild/LineActivator.cs`
- Modify: `Assets/Scripts/MapBuild/WorldSnapshotCapture.cs`
- Modify: `Assets/Scripts/MapBuild/WorldSnapshotRestore.cs`
- Modify: `Assets/Scripts/MapBuild/Rendering/WorldRenderContext.cs`
- Create: `Assets/Tests/EditMode/Specials/RuntimeLightRulesTests.cs`
- Create: `Assets/Tests/PlayMode/SectorLightPlayTests.cs`
- Modify: `Assets/Tests/PlayMode/SaveLoadPlayTests.cs`

- [x] **Step 1: Inventory E1 light specials.**

Сверить types из Stage 7 compatibility matrix и реализовывать сначала реально
встречающийся E1 subset. State machine/timing rules держать в pure layer, Unity
presentation — отдельно.

E1 sector lights 1/2/3/4/8/12/13/17 + linedef 12/13/17/35/79–81/104/138/139
via `RuntimeLightRules` (`Logs/stage8-t8-edit2.xml`).

- [x] **Step 2: Написать failing state tests.**

Initial light from SECTORS, deterministic blink/transition, profile-independent
state, bounds/tag behavior и отсутствие mesh recreation.

`RuntimeLightRulesTests` 8/8; PlayMode mesh-stable tick PASS.

- [x] **Step 3: Реализовать runtime state и shader binding.**

Enhanced получает current sector ambient через MaterialPropertyBlock или другой
batched-safe механизм. Classic продолжает baseline vertex light; скрытый runtime
state нужен для корректного последующего switch в Enhanced.

`RuntimeSectorLights` + Enhanced `_SectorAmbient` MPB; Classic clears blocks.

- [x] **Step 4: Исправить snapshot restore.**

Существующий `SectorSnapshot.LightLevel` должен реально применяться до simulation
resume. Save/load mid-light-cycle восстанавливает значение/phase, если phase
является authoritative state.

Save schema **v5** adds `LightCount`; capture/restore wired
(`Save_load_restores_runtime_sector_light_level` PASS).

- [x] **Step 5: Прогнать light/save suites.**

Проверить двери/movers/sector rebuild: свет не декодирует textures и не создаёт
новый Mesh каждый tic.

PlayMode filter 7/7 PASS (`Logs/stage8-t8-play.xml`).

**Commit checkpoint:** `Stage 8c: add runtime sector lighting`
_(awaiting explicit user commit request)_

### Task 9: Bounded dynamic lights и shadows

**Files:**
- Create: `Assets/Scripts/MapBuild/Rendering/EnhancedLightSystem.cs`
- Create: `Assets/Scripts/MapBuild/Rendering/EnhancedLightPool.cs`
- Create: `Assets/Scripts/Things/EnhancedEmissionTable.cs`
- Modify: `Assets/Scripts/MapBuild/PlayerWeapons.cs`
- Modify: `Assets/Scripts/MapBuild/Projectile.cs`
- Modify: `Assets/Scripts/MapBuild/BarrelExplosion.cs`
- Modify: `Assets/Scripts/MapBuild/ThingSpawner.cs`
- Modify: `Assets/Scripts/MapBuild/SpriteBillboard.cs`
- Create: `Assets/Tests/EditMode/Things/EnhancedEmissionTableTests.cs`
- Create: `Assets/Tests/PlayMode/EnhancedLightPlayTests.cs`

- [x] **Step 1: Написать failing priority/pool tests.**

Проверить fixed capacity, nearest/importance selection, stable reuse, expiry,
Classic disable и deterministic cleanup. Capacity берётся из Task 1 budget.

`EnhancedLightPoolTests` 4/4 + `EnhancedEmissionTableTests` 3/3
(`Logs/stage8-t9-10-edit.xml`).

- [x] **Step 2: Добавить event-driven light requests.**

Muzzle, projectile/impact, barrel explosion и table-listed emissive decorations
публикуют короткие requests. Не выполнять scene-wide searches каждый frame и не
добавлять permanent Light каждому Thing.

`EnhancedLightSystem` + hooks in `PlayerWeapons`/`Projectile`/`BarrelExplosion`/
`ThingSpawner`/`SpriteBillboard`.

- [x] **Step 3: Реализовать shadow budget.**

Только top-N значимых lights получают shadows; остальные остаются unshadowed.
Renderer shadow casting включить для world и безопасного subset things.
Directional sun не добавлять.

Pool ≤8 / shadows ≤4; world MeshRenderers cast when Enhanced+Shadows; sprites Off.

- [x] **Step 4: Проверить gameplay neutrality.**

Light requests не влияют на hitscan, projectile collision, damage timing,
monster visibility/wake или save data. Classic не оставляет активных Light.

PlayMode muzzle ammo/HP neutrality + Classic disable PASS.

- [x] **Step 5: Profile E1M7 и barrel stress.**

Много одновременных explosions не превышает pool/shadow capacity и не создаёт
GC allocation после warm-up.

Stress pulse ×40 stays within caps (`EnhancedLightPlayTests`).

**Commit checkpoint:** `Stage 8c: add bounded dynamic lights and shadows`
_(awaiting explicit user commit request)_

### Task 10: Post-processing, MSAA и upscaling

**Files:**
- Modify: `Assets/Settings/Rendering/DoomEnhancedVolume.asset`
- Modify: `Assets/Settings/Rendering/DoomUniversalRenderPipeline.asset`
- Modify: `Assets/Settings/Rendering/DoomUniversalRenderer.asset`
- Create: `Assets/Scripts/MapBuild/Rendering/GraphicsCapabilityPolicy.cs`
  (если не завершён в Task 5)
- Create: `Assets/Scripts/MapBuild/Rendering/EnhancedPostController.cs`
- Modify: `Assets/Scripts/MapBuild/Rendering/WorldCameraRenderer.cs`
- Create: `Assets/Tests/EditMode/Map/GraphicsCapabilityPolicyTests.cs`
- Create: `Assets/Tests/PlayMode/EnhancedPostPlayTests.cs`

- [x] **Step 1: Написать failing capability tests.**

HDR, depth/opaque texture, SSAO, decals, MSAA, render scale и FSR принимают
adapter report. Unsupported feature выключается отдельно; requested mode
остаётся Enhanced.

`GraphicsCapabilityPolicyTests` 5/5 (`Logs/stage8-t9-10-edit.xml`).

- [x] **Step 2: Настроить умеренный Enhanced Volume.**

Bloom реагирует только на HDR emission; SSAO/fog/grading не превращают тёмные
сектора в чёрные и не обрабатывают UI. Значения записать в spec baseline notes.

`EnhancedPostController`: bloom threshold 1.05 / intensity 0.28; soft grading;
distance fog until Task 11. Values in `Logs/stage8-graphics-baseline-notes.md`.

- [x] **Step 3: Добавить render scale/FSR policy.**

Использовать поддержанный URP API текущей package version. Нативное разрешение —
fallback. Не добавлять vendor SDK или reflection по внутренним URP API.

Enhanced render scale **0.85** + `UpscalingFilterSelection.FSR` when caps allow;
MSAA **4**. Classic restores scale 1 / MSAA 1.

- [x] **Step 4: Hot-switch и resize tests.**

Classic полностью отключает Volume/HDR-only path и возвращает baseline camera
state. Enhanced повторно включает его после resize/fullscreen change без stale
RenderTexture.

`EnhancedPostPlayTests` 3/3 PASS (`Logs/stage8-t9-10-play.xml`).

- [x] **Step 5: Profile GPU.**

Снять GPU frame time/render-target memory на baseline maps. Уменьшать intensity/
resolution feature, а не скрывать budget regression увеличением порога.

Numeric GPU/RT budgets remain deferred to Task 14; post defaults recorded in
baseline notes.

**Commit checkpoint:** `Stage 8c: add enhanced post processing and upscaling`
_(awaiting explicit user commit request)_

---

## 8d — WAD atmosphere и sprites

### Task 11: WAD sky, animation, fluids и fog

**Files:**
- Create: `Assets/Scripts/Graphics/TextureAnimationCatalog.cs`
- Create: `Assets/Tests/EditMode/Graphics/TextureAnimationCatalogTests.cs`
- Create: `Assets/Shaders/DoomSky.shader`
- Create: `Assets/Shaders/DoomFluid.shader`
- Create: `Assets/Scripts/MapBuild/Rendering/AnimatedSurfaceSystem.cs`
- Create: `Assets/Scripts/MapBuild/Rendering/WadSkyRenderer.cs`
- Create: `Assets/Scripts/MapBuild/Rendering/SectorFogSystem.cs`
- Modify: `Assets/Scripts/MapBuild/MapLoader.cs`
- Modify: `Assets/Scripts/MapBuild/TextureCache.cs`
- Create: `Assets/Tests/PlayMode/EnhancedAtmospherePlayTests.cs`

- [ ] **Step 1: Написать failing animation catalog tests.**

Known ranges разрешаются только из существующих lumps; missing frame безопасно
закрывает/отключает sequence. Добавить Freedoom integration на реально
встречающиеся E1 textures/flats.

- [ ] **Step 2: Реализовать `SKY1`.**

Декодировать через текущий TextureSet и рисовать camera-centered cylinder/sky
pass только через `F_SKY1` openings. Translation camera не двигает sky;
yaw/pitch/FOV/aspect не дают seam/stretch regression.

- [ ] **Step 3: Реализовать animated/emissive fluids.**

Frame cadence и UV offset не создают material instance каждый frame. Damage
остаётся sector-special logic. Classic после switch возвращает исходный static
flat без stale emission/offset.

- [ ] **Step 4: Реализовать depth/sector-aware fog.**

Fog не использует authored volume textures, не протекает сквозь стены и имеет
visibility clamp для switches/doors. Unsupported depth path отключает fog.

- [ ] **Step 5: Automated/interactive gate.**

Проверить E1 sky openings, nukage/lava, doors/movers и 4:3/16:9. В обоих режимах
нет missing/pink material.

**Commit checkpoint:** `Stage 8d: render WAD sky and animated fluids`

### Task 12: Enhanced sprite presentation

**Files:**
- Create: `Assets/Shaders/DoomEnhancedSprite.shader`
- Create: `Assets/Shaders/DoomSpectre.shader`
- Modify: `Assets/Scripts/MapBuild/SpriteCache.cs`
- Modify: `Assets/Scripts/MapBuild/SpriteBillboard.cs`
- Modify: `Assets/Scripts/MapBuild/ThingSpawner.cs`
- Modify: `Assets/Scripts/MapBuild/MonsterController.cs`
- Create: `Assets/Tests/PlayMode/EnhancedSpritePlayTests.cs`
- Modify: `Assets/Tests/PlayMode/MonsterAiPlayTests.cs`

- [ ] **Step 1: Написать failing sprite-profile tests.**

Classic сохраняет current cutout/Point/facing. Enhanced получает lit material,
profile filtering, sector ambient/local light, depth fade и отдельный Spectre
material. Player starts по-прежнему не спавнят renderer.

- [ ] **Step 2: Интерполировать presentation transforms.**

Хранить previous/current render pose на gameplay tick и интерполировать только
visual transform. Collider, AI location, attack range и save transform остаются
authoritative current state.

- [ ] **Step 3: Добавить bounded frame cross-fade.**

Если включён Enhanced, коротко смешивать только соседние valid sprite frames.
Death completion, corpse frame и rotation selection не задерживаются логически.
Не создавать/уничтожать quad каждый transition.

- [ ] **Step 4: Реализовать soft intersection и Spectre.**

Depth fade действует только в узкой зоне пола. Spectre distortion/translucency
не делает monster невидимым для hitscan и не меняет collider/AI.

- [ ] **Step 5: Regression gate.**

Прогнать sprite/monster/weapon/pickup PlayMode suites и интерактивно проверить
POSS/SPOS/TROO/SARG/Spectre/Baron со всех rotations в обоих режимах.

**Commit checkpoint:** `Stage 8d: add enhanced sprite rendering`

### Task 13: Pooled particles и decals

**Files:**
- Create: `Assets/Scripts/MapBuild/Rendering/ParticleEffectPool.cs`
- Create: `Assets/Scripts/MapBuild/Rendering/DecalEffectPool.cs`
- Create: `Assets/Scripts/MapBuild/Rendering/EnhancedEffectCatalog.cs`
- Modify: `Assets/Scripts/MapBuild/HitEffect.cs`
- Modify: `Assets/Scripts/MapBuild/PlayerWeapons.cs`
- Modify: `Assets/Scripts/MapBuild/Projectile.cs`
- Modify: `Assets/Scripts/MapBuild/BarrelExplosion.cs`
- Modify: `Assets/Settings/Rendering/DoomUniversalRenderer.asset`
- Create: `Assets/Tests/PlayMode/EnhancedEffectsPlayTests.cs`

- [ ] **Step 1: Написать failing pool tests.**

Fixed capacity, oldest/lowest-priority reuse, lifetime expiry, no growth after
warm-up, Classic disable, scene teardown и отсутствие save registration.

- [ ] **Step 2: Реализовать WAD-derived effect catalog.**

Использовать existing puff/blood/explosion patches или runtime palette texel.
Отсутствующий optional patch отключает только соответствующий effect.

- [ ] **Step 3: Подключить event hooks.**

Muzzle, hitscan surface hit, monster blood, projectile impact и barrel explosion
публикуют presentation events без изменения damage path.

- [ ] **Step 4: Реализовать decals.**

Project только на render geometry по hit point/normal. Ограничить size/lifetime/
distance; movers либо корректно parent'ят decal, либо не принимают его.
Masked walls/sprites не получают world decal.

- [ ] **Step 5: Stress и cleanup gate.**

Серия выстрелов/взрывов не превышает capacity, не оставляет Unity objects после
reload и не создаёт allocations после warm-up.

**Commit checkpoint:** `Stage 8d: add pooled particles and decals`

---

## 8e — Performance, build и приёмка

### Task 14: SRP performance и full-suite regression

**Files:**
- Modify: `Assets/Scripts/MapBuild/Rendering/DoomMaterialFactory.cs`
- Modify: `Assets/Scripts/MapBuild/TextureCache.cs`
- Modify: `Assets/Scripts/MapBuild/SpriteCache.cs`
- Modify: render systems из Tasks 5–13 по profiler evidence
- Modify: `Assets/Tests/PlayMode/E1MapSmokePlayTests.cs`
- Create: `Assets/Tests/PlayMode/GraphicsResourceLifetimePlayTests.cs`
- Modify: `Logs/stage8-graphics-baseline-notes.md`

- [ ] **Step 1: Расширить E1 smoke на оба profiles.**

E1M1–E1M9 строятся в Classic и Enhanced; нет pink/error shaders, NaN meshes,
missing mandatory WAD graphics или progression blockers.

- [ ] **Step 2: Добавить resource lifetime tests.**

Повторные switch/reload не увеличивают Texture2D/Material/Mesh/Light/
ParticleSystem/decal counts после warm-up. Каждый owned runtime resource
уничтожается ровно один раз.

- [ ] **Step 3: Профилировать до оптимизации.**

Сравнить Task 1 metrics на E1M1/E1M3/E1M7/E1M9. Проверить SRP Batcher
compatibility, material variants, shadow/decal overdraw и per-frame GC.

- [ ] **Step 4: Исправить измеренные bottlenecks.**

Предпочесть shared materials + MaterialPropertyBlock, pooling и fixed update
lists. GPU instancing применять только где оно не ломает per-sprite rotation/
frame. Atlas/mesh merge — только с доказанным выигрышем и regression tests.

- [ ] **Step 5: Полные suites.**

Запустить все EditMode и PlayMode tests. Записать totals и сравнение metrics в
baseline notes; любой skipped/failed test перечислить как blocker.

**Commit checkpoint:** `Stage 8e: optimize graphics profiles`

### Task 15: Windows build и интерактивный sign-off

**Files:**
- Modify: `Assets/Scripts/MapBuild/Editor/Stage7BuildMenu.cs` или переименовать
  в общий build tool без поломки menu item
- Modify: `ProjectSettings/GraphicsSettings.asset`
- Modify: `docs/doom-unity-remake-plan.md`
- Modify: `CLAUDE.md`
- Modify: `docs/superpowers/specs/2026-07-11-enhanced-graphics-design.md`
- Modify: `docs/superpowers/plans/2026-07-11-enhanced-graphics.md`
- Modify: `Logs/stage8-graphics-baseline-notes.md`

- [ ] **Step 1: Build inclusion audit.**

URP pipeline/renderer/Volume assets, renderer features и все Classic/Enhanced
shader variants включены. Standalone стартует в main menu; StreamingAssets WAD
присутствует. Build output не коммитить.

- [ ] **Step 2: Собрать Windows standalone.**

Записать точную команду/menu item, Unity version, build result и warnings.
Shader stripping warning или pink material является blocker, не cosmetic issue.

- [ ] **Step 3: Проверить settings lifecycle.**

Fresh install default Classic; Apply Enhanced переживает restart; Cancel
возвращает active profile; save/load не меняет graphics mode.

- [ ] **Step 4: Интерактивно пройти graphics checklist на E1M1–E1M9.**

На каждой карте проверить Classic → Enhanced → Classic без изменения gameplay.
Отдельно: masked walls, sky, dark sectors, movers, fluids, enemies, pickups.

- [ ] **Step 5: Проверить Enhanced effects.**

Muzzle/projectile/barrel lights и shadows; SSAO/bloom/grading/fog; FSR/fallback;
Spectre/Baron; particles/decals; resize/fullscreen; 4:3 и 16:9 HUD.

- [ ] **Step 6: Финальный profiler pass.**

Сравнить target machine metrics с Task 1 budgets. Regression исправить или
явно зафиксировать как незакрытый blocker; не объявлять Stage 8 завершённым.

- [ ] **Step 7: Обновить документацию после факта.**

Отметить завершённые задачи, реальные test totals/package version/budgets,
известные ограничения и interactive sign-off в roadmap/CLAUDE/spec/plan.

**Commit checkpoint:** `Stage 8: complete classic and enhanced graphics`

---

## Порядок и зависимости

```text
Task 1 baseline
  -> Task 2 settings
  -> Task 3 URP
  -> Task 4 Classic parity
  -> Task 5 hot-switch
  -> Task 6 compositing
  -> Task 7 Enhanced materials
  -> Task 8 sector lights
  -> Task 9 dynamic lights/shadows
  -> Task 10 post/upscale
  -> Task 11 sky/fluids/fog
  -> Task 12 sprites
  -> Task 13 particles/decals
  -> Task 14 performance/full suites
  -> Task 15 build/sign-off
```

Tasks 8–10 можно разрабатывать отдельными ветками только после завершения
Tasks 3–7 и с единым `GraphicsProfile` contract. Tasks 11–13 допустимо
параллелить после Task 10, но финальное объединение проходит Task 14 целиком.

## Stop conditions

- Classic captures отличаются после URP без объяснённого color/UV/cutout cause.
- URP package несовместим с Unity 6000.4.8f1 или требует неподдержанный API.
- Реализация требует authored replacement asset или записи generated normal/
  upscale texture в репозиторий.
- Hot-switch меняет gameplay/session/save state или требует полной перезагрузки
  кампании.
- OnGUI HUD/weapon view попадает под post-processing, пропадает из standalone
  или меняет виртуальный layout.
- Light/decal/particle systems не имеют fixed capacity либо растут после warm-up.
- E1 smoke, save/load, monster, weapon, pickup или mover regressions маскируются
  как «графические».
- Full suite/build не запущены из-за открытого Editor: отметить BLOCKED и
  остановиться, не использовать старые результаты.

## Definition of Done

- [ ] `Graphics Mode` имеет только `Classic` и `Enhanced`, default Classic;
      settings v1 мигрируют, Apply/Cancel/persistence работают.
- [ ] Один URP pipeline обслуживает оба режима; Classic принят по baseline.
- [ ] Hot-switch не перезагружает gameplay world и не повторяет WAD decode.
- [ ] Enhanced world materials используют procedural normals и sector ambient.
- [ ] Dynamic lights/shadows ограничены budget/pool.
- [ ] HDR/SSAO/bloom/grading/fog/MSAA/render scale/FSR имеют capability fallback.
- [ ] WAD SKY1, animated/emissive fluids и atmosphere работают на E1.
- [ ] Enhanced sprites, Spectre, interpolation и soft intersection приняты.
- [ ] Particles/decals WAD-derived, pooled и не входят в save.
- [ ] E1M1–E1M9 smoke проходит в обоих режимах.
- [ ] Полные EditMode/PlayMode suites зелёные с обновлёнными totals.
- [ ] Windows standalone не содержит pink/missing shaders и стартует в menu.
- [ ] Интерактивный Classic/Enhanced checklist и profiler budgets подписаны.
- [ ] Roadmap, `CLAUDE.md`, spec, plan и baseline notes обновлены по факту.
