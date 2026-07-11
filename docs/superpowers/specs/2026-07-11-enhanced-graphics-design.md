# Этап 8. Classic и Enhanced графика — дизайн

**Дата:** 2026-07-11  
**Статус:** в реализации — Tasks 1–14 ✅ (perf/full suites);
next Task 15 Windows build / interactive sign-off  
**Предыдущие этапы:** Stages 0–7 завершены (Stage 7e Task 15 closed 2026-07-12)  
**Контекст:** пост-релизная графическая веха после закрытия Stage 7. Продолжает
Stage 4 и закрывает отложенные небо, анимацию поверхностей и расширенный рендер.

## Цель

Добавить в Options одну настройку `Graphics Mode` с двумя значениями:

- **Classic** — сохраняет текущий WAD-driven вид игры;
- **Enhanced** — использует современный GPU-рендер Unity, не заменяя WAD-контент
  авторскими текстурами, моделями или звуками.

Оба режима используют одну геометрию, gameplay state, физику, WAD и сцену.
Переключение не перезапускает кампанию, не меняет баланс и не попадает в
savegame. Видимый результат: одну и ту же точку E1M1–E1M9 можно немедленно
сравнить в Classic и Enhanced через меню Options.

## Объём этапа

### 8a. Единый URP pipeline и настройка режима

- Проект целиком переводится с Built-in Render Pipeline на Universal Render
  Pipeline. Поддерживать два render pipeline и два набора сцен не требуется.
- `GameSettingsData` получает enum `GraphicsMode { Classic, Enhanced }`,
  schema settings повышается с v1 до v2. Старые настройки мигрируют с
  `Classic` по умолчанию.
- Options показывает одну строку `Graphics Mode: Classic|Enhanced`.
  Промежуточные quality sliders не входят в обязательный UI Stage 8:
  возможности Enhanced задаются одним проверенным профилем.
- `Apply` сохраняет и применяет режим; `Cancel` возвращает snapshot вместе со
  всеми runtime-эффектами.
- Переключение выполняется на месте: меняются материалы и фильтрация уже
  декодированных текстур, post-processing, lights и effect systems.
  Gameplay-мир, коллайдеры, AI, movers и session не пересоздаются.

### 8b. Classic profile

Classic является режимом по умолчанию и эталоном визуальной регрессии:

- WAD `PLAYPAL`, исходное разрешение textures/sprites и текущие UV;
- `FilterMode.Point`, текущие mipmaps и anisotropic filtering;
- opaque/cutout Unlit URP shaders;
- `albedo × sector vertex light`, без влияния Unity Lights;
- без HDR bloom, SSAO, fog, normal maps, decals, particles и real-time shadows;
- HUD, меню и weapon view сохраняют виртуальный экран 320×200 и nearest-neighbour
  scaling;
- sprite billboard rotation, cutout, offsets и размеры не меняются.

URP работает в Linear color space. Classic shader явно воспроизводит прежнее
gamma-space умножение WAD albedo на sector light, затем возвращает корректный
linear output. Переход на URP не является разрешением «примерно сохранить»
Classic: опорные кадры до миграции и интерактивное сравнение обязательны.

### 8c. Enhanced world materials

- Стены, полы и потолки используют custom URP Lit/Cutout shaders. Sector
  `LightLevel` остаётся базовым ambient, а Unity lights добавляют динамическую
  составляющую.
- Для каждой непрозрачной WAD texture/flat в памяти детерминированно строится
  normal map из яркости изображения (Sobel/central differences, wrap с учётом
  wall/flat semantics). Strength и roughness определяются безопасной таблицей
  material categories и имеют нейтральный fallback.
- Normal maps являются runtime cache entries. Они не записываются в `Assets`,
  StreamingAssets или savegames и не считаются replacement content.
- Enhanced использует bilinear/trilinear sampling, mipmaps и anisotropic
  filtering; HUD остаётся Point.
- Alpha-cutout middle textures не получают прозрачное смешивание и продолжают
  писать depth. Это сохраняет коллизии и ограничивает sorting regressions.

### 8d. Освещение и тени

- Sector light остаётся authoritative ambient. Runtime sector-light specials
  меняют отдельный `RuntimeSectorLights`, а renderer получает значение без
  декодирования texture и без перестроения gameplay geometry.
- Состояние sector light применяется при world restore из уже существующего
  `SectorSnapshot.LightLevel`.
- Пул динамических lights обслуживает:
  - muzzle flash;
  - imp projectile и его impact;
  - взрыв бочки;
  - emissive fire/lamp decorations, если тип и sprite явно известны таблице.
- Lights не создаются без ограничения на каждый Thing. Пул выбирает ближайшие
  и наиболее значимые источники относительно player camera; остальные
  сохраняют emissive material без Unity Light.
- Тени разрешены только выбранному подмножеству активных lights. Мир и solid
  things могут отбрасывать тени; translucent/cutout sprites используют
  упрощённую shadow policy.
- Направленный «солнечный» свет не добавляется ко всем картам: он ломает
  секторную композицию DOOM. Основой остаются sector ambient и локальные lights.

### 8e. Post-processing и масштаб рендера

Enhanced включает один URP Volume profile:

- HDR camera buffer;
- SSAO;
- мягкий bloom только для emissive/HDR pixels;
- умеренный color grading без изменения читаемости палитры;
- distance fog и sector-aware fog factor без физической volumetric simulation;
- MSAA;
- render scale и URP FSR, если они поддерживаются текущей платформой.

`GraphicsCapabilityPolicy` проверяет API/format/feature support. Неподдерживаемый
эффект отключается отдельно; сохранённое значение `Enhanced` не переписывается
на `Classic`. Normal generation имеет CPU path и не зависит от compute shaders.

Post-processing применяется только к world camera. WAD HUD, меню, intermission и
OnGUI weapon view рисуются после world composite, остаются резкими и не
подсвечиваются bloom.

### 8f. Небо, жидкости и atmosphere

- `F_SKY1` по-прежнему обозначает sky opening. `SKY1` декодируется как WAD wall
  texture и отображается camera-centered cylindrical sky shader без authored
  skybox.
- Animated texture catalog строится из известных DOOM animation ranges и
  реально существующих lumps. Отсутствующий кадр завершает/отключает конкретную
  sequence, а не ломает загрузку карты.
- Nukage, lava и другие распознанные fluids получают animated frames, слабое UV
  distortion, emissive contribution и контролируемый scrolling. Gameplay
  damage продолжает определяться sector special, а не именем texture.
- Fog использует глубину и sector parameters; он не должен просачиваться через
  стены или скрывать switch/door на игровой дистанции.

### 8g. Enhanced sprites

- Sprite shader принимает sector ambient и локальные lights, используя
  camera-facing normal. Исходный patch, offsets, rotation и cutout остаются
  authoritative.
- Texture filtering/upscale выполняются GPU sampling/render scale; на диск не
  создаются увеличенные sprite textures.
- Render transform интерполируется между gameplay ticks. Для анимационных
  кадров допускается короткий двухкадровый cross-fade, но gameplay frame,
  hitbox и death state не интерполируются.
- Пересечение billboard с полом смягчается depth-based fade в узкой зоне.
- Spectre использует отдельный WAD-derived translucent/distortion material,
  сохраняя depth/visibility policy и не меняя AI Thing type 58.

### 8h. Частицы и decals

- Muzzle flash, projectile trail/impact и barrel explosion дополняются
  ограниченными ParticleSystem emitters.
- Цвета/текстуры частиц берутся из WAD patches или создаются в памяти как
  одноцветные palette-derived texels. Новые PNG/TGA/mesh content assets не
  добавляются.
- Попадания могут создавать pooled URP decals на render geometry. Blood decal
  использует WAD blood patch, bullet mark — WAD puff/impact patch с безопасным
  fallback без decal.
- Decals/particles имеют максимальное количество, lifetime и deterministic
  cleanup при scene reload. Они являются presentation state и не входят в save.

## Сознательно не входит

- HDRP, ray tracing, path tracing, DLSS/XeSS vendor SDK и обязательный compute.
- AI/ESRGAN/xBRZ baked replacements, authored textures, sprites, normal maps,
  models, skyboxes, sounds или внешние texture packs.
- Изменение map geometry, collision, AI, weapon damage, monster timing или
  campaign/save semantics.
- Полноценные volumetric fluids, reflections, tessellation и displacement,
  меняющий силуэт/коллизию поверхности.
- Точная эмуляция software renderer, `COLORMAP` distance lighting и
  320×200 world framebuffer в Classic. Classic означает текущий вид проекта,
  а не новый source-port compatibility mode.
- Отдельные пользовательские sliders для каждого Enhanced-эффекта. Их можно
  добавить после профилирования, не меняя контракт двух основных режимов.

## Ключевые решения

1. **Один URP pipeline, два профиля.** Runtime-переключение pipeline assets
   хрупко, требует двух shader families и усложняет build stripping. Classic
   воспроизводится собственным URP Unlit shader.
2. **Classic — default и regression oracle.** Пользователь не получает
   Enhanced автоматически после обновления, а слабое GPU не оказывается в
   неподдерживаемом режиме без выбора.
3. **Один settings selector.** `GraphicsMode` — глобальное preference, не часть
   savegame. Внутренние capability fallbacks не раздувают Options.
4. **Hot-switch presentation, не rebuild мира.** Кэши хранят decoded WAD data,
   material variants и normal maps; controller переключает renderer resources
   и effect systems транзакционно.
5. **WAD остаётся источником контента.** Procedural normal, particles и decals
   производятся только в памяти из WAD pixels/палитры. Репозиторий не получает
   производные texture assets.
6. **Sector light не заменяется PBR-светом.** Он задаёт художественную
   композицию карты; локальный свет добавляется поверх него.
7. **Post обрабатывает только мир.** WAD UI и weapon view не должны размываться,
   менять цвет или попадать под bloom.
8. **Bounded effects.** Lights, shadow casters, particles и decals используют
   пулы и бюджеты, определённые измерением, а не неограниченный spawn.
9. **Fallback деградирует отдельную возможность.** Enhanced остаётся Enhanced,
   даже если конкретный GPU не поддерживает FSR path или decal format.

## Архитектура

### `Doom.Game` — settings contract

- `GraphicsMode` — stable enum с явными numeric values.
- `GameSettingsData` schema v2 — immutable field, validation, `WithGraphicsMode`.
- `SettingsStore` мигрирует v1 keys в v2, сохраняя существующие значения и
  добавляя `Classic`. Неизвестное enum value становится `Classic`.

### `Doom.Graphics` — pure image transforms

- `NormalMapGenerator` принимает `DecodedImage` и параметры wrapping/strength,
  возвращает normal RGBA bytes.
- `TextureAnimationCatalog` строит доступные frame sequences из имен lumps.
- Pure слой не знает Unity Texture2D, URP, Material или GPU capability.

### `Doom.MapBuild` — runtime orchestration

- `GraphicsModeController` — persistent current profile, capability report и
  регистрация активного `WorldRenderContext`.
- `WorldRenderContext` — materials, textures, renderers, world camera, Volume,
  light/effect pools текущей сцены. Он не владеет gameplay state.
- `DoomMaterialFactory` — Classic/Enhanced opaque, cutout, sprite, spectre,
  sky и fluid variants.
- `TextureCache`/`SpriteCache` передают decoded textures фабрике, сохраняют
  filter policy и освобождают оба material variant при teardown.
- `EnhancedLightSystem`, `ParticleEffectPool`, `DecalEffectPool`,
  `AnimatedSurfaceSystem` включаются только в Enhanced.
- `RuntimeSectorLights` хранит mutable значения независимо от активного режима;
  `WorldSnapshotRestore` применяет сохранённый light state.

### URP assets и shaders

- Один Universal Render Pipeline Asset и Renderer Data для Windows build.
- SSAO/Decal renderer features и Global Volume profile включаются controller'ом
  только в Enhanced.
- Custom HLSL shaders лежат в `Assets/Shaders/` и явно включаются в build.
- Shaders совместимы с SRP Batcher; material keywords ограничены фиксированным
  набором, чтобы не получить неконтролируемый variant explosion.

### Поток переключения

```text
Options.GraphicsMode
        |
        v
GameSettingsData(v2) -> SettingsController
        | Apply/Cancel
        v
GraphicsModeController -> GraphicsCapabilityPolicy
        |
        +-> DoomMaterialFactory -> registered Renderers
        +-> Texture/Sprite filter policy
        +-> URP Volume + camera settings
        +-> Light/Particle/Decal/Animation systems
        |
        v
World changes visually; gameplay objects and save state remain intact
```

## Производительность и ресурсы

- До URP migration фиксируются Classic screenshots и baseline на E1M1, E1M3,
  E1M7 и E1M9: CPU/GPU frame time, batches, SetPass, triangles, active lights,
  shadow casters и render-target memory.
- Финальные численные budgets задаются после baseline на целевой Windows-машине
  и записываются в `Logs/stage8-graphics-baseline-notes.md`.
- Обязательные инварианты:
  - pools не растут после достижения capacity;
  - normal map строится не более одного раза на texture/profile key;
  - переключение Classic/Enhanced не декодирует WAD повторно;
  - после возврата в Classic нет активных Volume effects, Unity lights,
    decals или particles;
  - E1M7 не получает material/render-object leak при повторных переключениях.
- Оптимизация рассматривает SRP Batcher, shared materials, MaterialPropertyBlock
  и GPU instancing. Atlas/mesh merge допускаются только после измерений и не
  должны ломать movers, animated UV, sector light и save restore.

## Тестирование

### EditMode

- `GameSettingsData` v2: default Classic, enum validation, equality/hash,
  `WithGraphicsMode`.
- `SettingsStore`: v2 round-trip, v1 migration, corrupt/unknown mode fallback.
- `GraphicsProfilePolicy`: полный набор Classic/Enhanced flags и capability
  degradation без изменения выбранного mode.
- `NormalMapGenerator`: flat image даёт neutral normal; gradient — ожидаемое
  направление; wrap и alpha deterministic.
- `TextureAnimationCatalog`: complete/missing ranges и Freedoom integration.
- Runtime sector light capture/restore применяет `SectorSnapshot.LightLevel`.

### PlayMode

- `GraphicsModePlayTests`: Apply, Cancel, persistence и hot-switch без scene
  reload/gameplay reset.
- `ClassicRenderPlayTests`: Doom Unlit shaders, Point textures, отключённые
  lights/Volume/effect systems.
- `EnhancedRenderPlayTests`: Lit material и normal assigned, Volume active,
  bounded light появляется на muzzle/projectile/explosion и исчезает.
- `WorldHudCompositePlayTests`: world camera получает post, HUD/weapon view
  остаются в overlay path.
- `EnhancedEffectsPlayTests`: fluid animation, sky, spectre material,
  particle/decal pool capacity и cleanup.
- Расширение `E1MapSmokePlayTests`: обе конфигурации строят E1M1–E1M9 без pink
  materials, NaN meshes и неизвестных progression blockers.
- Windows standalone build включает pipeline/renderer/shader assets.

PlayMode assertions проверяют state, material/shader assignment, pool bounds и
render errors. Они не объявляют визуальный паритет по одному факту загрузки.

### Capture и интерактивно

- До миграции сохранить опорные Classic captures для одинаковых camera
  transforms; после миграции сравнить геометрию, UV, palette, sector brightness,
  sprites и masked walls.
- На E1M1–E1M9 проверить переключение Classic → Enhanced → Classic в gameplay
  и pause menu без изменения HP, ammo, enemy state и mover positions.
- Проверить 4:3, 16:9 и resize: post относится только к миру, HUD и weapon view
  остаются резкими и правильно выровненными.
- Enhanced: muzzle/projectile/barrel lights, тени, sky openings, nukage/lava,
  fog, spectre, particles и decals.
- Проверить слабый/unsupported capability path на доступном адаптере или через
  test adapter.
- Сравнить profiler captures с зафиксированными Stage 8 budgets.

## Критерий готовности

1. Options содержит ровно два значения `Classic` и `Enhanced`; выбор сохраняется
   отдельно от savegames, Apply/Cancel работают предсказуемо.
2. Один URP pipeline обслуживает оба режима, Windows build не содержит pink
   материалов и отсутствующих shader variants.
3. Classic интерактивно принят как эквивалент текущего вида на E1M1–E1M9.
4. Enhanced реализует world lighting/normals, bounded dynamic lights/shadows,
   post-processing/upscale, WAD sky, fluids/fog, enhanced sprites,
   particles/decals и capability fallbacks.
5. Переключение не меняет gameplay/session state и не создаёт resource leaks.
6. Полные EditMode и PlayMode suites зелёные; E1 smoke проходит в обоих режимах.
7. Windows standalone проверен интерактивно; profiler не нарушает budgets,
   записанные после baseline.
8. Roadmap, `CLAUDE.md`, baseline notes и test totals обновляются только после
   фактической реализации и приёмки.

## Связанные документы

- План реализации:
  `docs/superpowers/plans/2026-07-11-enhanced-graphics.md`
- Master roadmap: `docs/doom-unity-remake-plan.md`
- Исходный texture pipeline:
  `docs/superpowers/specs/2026-05-30-textures-design.md`
- Settings/menu architecture:
  `docs/superpowers/specs/2026-07-10-polish-design.md`
- Stage 7 baseline: `Logs/stage7e-baseline-notes.md`
