# Enhanced Parametric Pickups — дизайн

**Дата:** 2026-08-05  
**Ветка:** `parametric-pickups` от `main` (`19e6895`)  
**Статус:** ❌ REJECT 2026-08-06 — Stage 0 ✅; Stage 1 MEDIA0 stopped before visual SUCCESS (loop too slow; not approaching needed result). No runtime routing.  
**Unity:** `6000.4.8f1`  
**Blender:** `4.5.12 LTS`  
**Предыдущий эксперимент:** Enhanced Pickup Pixel Extrusion — **REJECT** 2026-08-05  
**План:** `docs/superpowers/plans/2026-08-05-inventory-parametric-pickups.md`  
**Rejected predecessor:** `docs/superpowers/specs/2026-08-05-inventory-pixel-extrusion-design.md`

## Контекст и решение

Gate 1 uniform pixel extrusion подтвердил, что один алгоритм глубины не
восстанавливает разные предметные формы из фронтального WAD-спрайта:

- box-like `MEDIA0`, `AMMOA0`, `RKEYA0` выглядели приемлемо;
- `STIMA0` и `SHOTA0` требуют цилиндрических деталей;
- `BON1A0` требует тела вращения;
- `BON2A0` требует тонкой выпуклой пластины;
- `ARM1A0` требует передней и задней оболочек.

Эксперимент остановлен до runtime routing. Его offline builder, shader и
preview не являются фундаментом нового решения и не переносятся в новую
ветку.

Новый подход не пытается вывести скрытую форму универсальной эвристикой.
Каждый эталонный предмет получает:

1. утверждённый пользователем semantic brief;
2. собственную параметрическую конструкцию из Blender primitives, curves,
   modifiers и материалов;
3. детерминированный Python generator;
4. импортируемые Unity FBX/PNG outputs;
5. отдельный visual gate до перехода к следующему предмету.

После восьми эталонов утверждённые семейства распространяются на весь
`ItemRules.IsPickup` scope отдельным этапом и отдельным gate.

## Цель

В Enhanced заменить плоские world-pickup billboards осмысленными объёмными
моделями, которые:

- сохраняют узнаваемый фронтальный силуэт и цветовую композицию WAD sprite;
- имеют определённые brief-ом side, back и top;
- используют настоящие normals и реагируют на Enhanced lighting;
- остаются стилистически совместимыми с pixel-art окружением;
- детерминированно воспроизводятся из tracked source;
- не меняют gameplay, Classic rendering и save schema.

Это полная новая отрисовка presentation asset, а не depth effect поверх
исходного quad.

## Осознанное исключение из WAD-only

Архитектурное правило проекта «не создавать authored content» сохраняется
везде, кроме узко ограниченного scope этой работы.

Разрешённое исключение:

- только Enhanced world pickups;
- только geometry, UV и texture/material outputs, полученные из WAD reference,
  versioned user brief и tracked Blender Python;
- generated outputs должны иметь provenance manifest и воспроизводиться
  pinned toolchain;
- запрещено добавлять сторонние модели, textures или незафиксированную ручную
  Blender-доводку.

Classic, enemies, decorations, projectiles, effects, first-person weapons,
HUD, menus, world geometry и world textures остаются полностью WAD-driven.

## Scope

### Входит

Источником runtime scope остаётся `ItemRules.IsPickup`:

- health, armor и bonuses;
- ammo, backpack и cells;
- cards и skull keys;
- berserk, radiation suit и поддержанные powers;
- world weapon pickups;
- все `PickupAnimationTable` frames;
- `PickupFactory` death drops и save-restored runtime pickups.

Первая художественная фаза ограничена восемью эталонами:

1. `MEDIA0`;
2. `AMMOA0`;
3. `RKEYA0`;
4. `STIMA0`;
5. `BON2A0`;
6. `BON1A0`;
7. `ARM1A0`;
8. `SHOTA0`.

Они утверждаются строго последовательно. Остальные pickups не получают
runtime mesh до завершения family expansion.

### Не входит

- neural image-to-3D или generative texture synthesis;
- ручное редактирование vertices после generator export;
- физический mesh collider;
- изменение pickup touch radius/height;
- новый bobbing или gameplay animation;
- morph между animated frames;
- models для enemies, decorations, effects и FP weapon view;
- изменение EdgeMix, world texture pipeline или `EnhancedPipelineVersion`;
- сохранение spin/model state в save;
- поддержка Unity/Blender версий, отличных от pinned.

## Источники истины и asset layout

Source-of-truth:

```text
freedoom1.wad sprite
  + Tools/ParametricPickups/briefs/<LUMP>.md
  + Tools/ParametricPickups/parameters/<LUMP>.json
  + Tools/ParametricPickups/generators/*.py
  + pinned Blender version
  → generated FBX/PNG + manifest
```

Tracked source:

```text
Tools/ParametricPickups/
  generate.py
  validate.py
  common/
  generators/
  briefs/
  parameters/
  schemas/
```

Tracked generated output:

```text
Assets/Models/Pickups/Generated/
  <LUMP>.fbx
  <LUMP>.fbx.meta
  manifest.json
  manifest.json.meta

Assets/Textures/Pickups/Generated/
  <LUMP>_albedo.png
  <LUMP>_albedo.png.meta
  optional <LUMP>_mask.png
```

Temporary WAD reference export, Blender scenes, turntables и validation
renders не коммитятся:

```text
Logs/parametric-pickups/references/   # Unity WAD reference export
Logs/parametric-pickups/              # previews, synthetic, staging
```

Unity clears project `Temp/` on editor exit, therefore offline references and
generator staging live under `Logs/parametric-pickups/` (also gitignored).

`.blend` не является source-of-truth и не коммитится. Generated FBX/PNG
коммитятся, чтобы clean clone собирался без установленного Blender. Blender
нужен только для regeneration и authoring.

## Toolchain и воспроизводимость

Используется Blender `4.5.12 LTS`. Точный executable path задаётся локальной
переменной/CLI argument, но version mismatch завершает generation ошибкой.

Generator обязан:

1. работать в Blender background mode;
2. сбрасывать scene и random seeds;
3. задавать units, axes, transforms и color management явно;
4. применять modifiers в фиксированном порядке;
5. triangulate перед export;
6. сортировать objects/material slots детерминированно;
7. экспортировать FBX без animation и embedded media;
8. записывать normalized geometry/material metrics и hashes;
9. не включать timestamps/absolute paths в semantic manifest;
10. завершаться non-zero exit при validation failure.

Binary FBX byte identity не является обязательной из-за exporter metadata.
Детерминизм проверяется normalized hash по ordered positions, indices,
normals, UV, material assignments и canonical transforms.

Manifest содержит:

- schema и generator versions;
- Blender version;
- source WAD identity/hash;
- lump, sprite prefix и frame;
- brief/parameter hashes;
- generated file hashes;
- normalized geometry hash;
- vertex/triangle/material counts;
- local bounds, pivot и canonical render scale;
- model family и variation id.

## Semantic brief

До моделирования каждого эталона пользователь утверждает brief:

- что изображает предмет;
- основные составные части;
- тип базового объёма;
- front silhouette invariants;
- предполагаемые side/back/top;
- symmetry или intentional asymmetry;
- материалы, цвета, decals и emissive areas;
- желаемая толщина и пропорции;
- допустимые расхождения с пиксельным sprite;
- ось и характер presentation rotation.

Brief хранится в `Tools/ParametricPickups/briefs/<LUMP>.md`, получает revision
и после `BRIEF APPROVED` становится входом generator. Любое изменение смысла
формы требует новой brief revision и повторного gate.

## Параметрические семейства

Начальная taxonomy задаёт реализационные инструменты, но не подменяет brief:

- `hard_surface_box`: cuboids, bevels, panels, straps, hinges, decals;
- `thin_hard_surface`: plates, keys, controlled thickness, cutouts;
- `cylindrical`: cylinders, rings, caps, tubes, radial segments;
- `thin_plate`: profile extrusion, curvature, rim и relief;
- `lathe`: profile curve, solid of revolution, neck/cap;
- `dual_shell`: separate front/back shells, controlled cavity and rim;
- `compound_weapon`: barrels, receiver, grip и authored assembly hierarchy.

Один generator family принимает per-lump JSON parameters. Item-specific
geometry допускается только через tracked generator code/parameters, не через
ручное изменение exported FBX.

## Геометрический и стилистический contract

### Coordinates и pivot

- Blender/FBX импортируется с явной Unity-compatible axis convention.
- Object transforms применены; runtime получает identity local rotation/scale.
- Floor pickup origin соответствует точке опоры на `Y=0`.
- Horizontal anchor выводится из native `PatchHeader.LeftOffset`.
- World width/height и visual offset продолжают вычисляться из native
  `PatchHeader`, а не из texture resolution или произвольного FBX scale.
- Model canonical bounds нормализуются относительно native sprite footprint.
- Visual depth не меняет gameplay touch volume.

### Silhouette

Canonical front camera ортографическая. Front alpha silhouette сравнивается с
native WAD patch после приведения к одной canvas/anchor системе.

Автоматическая метрика silhouette IoU служит regression signal, но threshold
устанавливается per-item после первого утверждённого result. Она не заменяет
пользовательский visual verdict: осмысленная новая геометрия может намеренно
отличаться от отдельных пикселей.

### Материалы

- Base colors берутся из sprite palette/composition и brief.
- Текстуры генерируются скриптом; ручная painting вне source pipeline
  запрещена.
- Point filtering и отсутствие texture streaming фиксируются importer-ом.
- Albedo не должен содержать baked directional lighting, кроме осознанных
  sprite-style highlights, записанных в brief.
- Metallic/roughness задаются material family parameters; обязательный v1
  output может использовать один packed mask.
- Decals вроде креста или надписи могут проецироваться из WAD reference или
  строиться процедурно, если это записано в brief.
- Palette-aware quantization и ограниченный texture resolution сохраняют
  pixel-art язык; фотореалистичный PBR запрещён.

## Preview contract

Каждый candidate рендерится одним reproducible Blender preview rig:

- orthographic projection;
- одинаковые camera distance, object footprint и output scale;
- фиксированные environment, key/fill light и transparent/background color;
- views: `было`, front, 45°, side, back, top;
- настоящий render каждой стороны; back нельзя клонировать из front;
- labels содержат lump, brief revision, generator version и view;
- turntable использует тот же model/material и не заменяет static views.

`было` — текущий принятый Enhanced flat EdgeMix 8× billboard, приведённый к
той же native footprint. PNG orientation проверяется marker-тестом, чтобы
порядок panels/rows не инвертировался.

Outputs:

```text
Logs/parametric-pickups/<NN>-<LUMP>/
  brief-rN.md
  comparison-rN.png
  turntable-rN.mp4
  metrics-rN.json
  VERDICT.md
```

## Пользовательский gate protocol

Каждый из восьми item stages имеет два hard stops.

### Brief gate

1. Показать native/Enhanced sprite.
2. Запросить комментарии по semantic brief.
3. Записать brief revision.
4. Показать структурированное резюме пользователю.
5. Получить явный `BRIEF APPROVED`.

До `BRIEF APPROVED` generator текущего item не реализуется.

### Visual gate

1. Focused generation/validation зелёные.
2. Приложить comparison PNG и turntable.
3. Показать metrics и известные расхождения.
4. Получить один verdict:
   - `SUCCESS` — заморозить revision и перейти к следующему item;
   - `RETUNE` — менять параметры/implementation внутри текущего brief;
   - `REJECT` — отменить текущую интерпретацию, вернуться к новому brief.

`REJECT` одного item не объявляет автоматически весь подход failed, но
полностью блокирует следующий item. Только пользователь может остановить весь
эксперимент или разрешить исключение.

## Последовательность восьми gates

Порядок уменьшает риск: сначала проверяются базовые hard-surface contracts,
затем curved/shell/compound families.

1. `MEDIA0` — первый hard-surface box contract.
2. `AMMOA0` — reusable container variation.
3. `RKEYA0` — thin hard-surface, cutouts и читаемость side.
4. `STIMA0` — cylinder, rings/caps и radial shading.
5. `BON2A0` — thin curved plate/shield.
6. `BON1A0` — lathe profile и animated-family contract.
7. `ARM1A0` — separate shells, cavity/rim.
8. `SHOTA0` — compound assembly и long-object pivot.

Предполагаемые family labels не утверждают скрытую форму. Реальная
конструкция каждого этапа определяется только пользовательским brief.

## Family expansion

После восьми `SUCCESS` создаётся полный inventory manifest:

- перечислить каждый doomednum, sprite и frame из `ItemRules.IsPickup`;
- добавить все `PickupAnimationTable` frames;
- добавить POSS→CLIP и SPOS→SHOT death drops;
- назначить каждому lump утверждённый family/variation;
- создать параметры и generated assets;
- доказать отсутствие unmapped entries автоматическим тестом.

Новые variations используют утверждённый визуальный язык. Если item не
укладывается ни в одно семейство без новой семантической конструкции, он
получает отдельный brief/gate, а не forced approximation.

До batch expansion `SUCCESS` runtime mesh routing не реализуется.

## Runtime routing

Existing authoritative path сохраняется:

```text
ItemRules.IsPickup
  → ThingSpawner / PickupFactory
  → SpriteBillboard.SetPickupUpscale(true)
  → PickupMeshCatalog resolve(sprite, frame)
```

Presentation:

```text
Classic or non-pickup
  → existing native/Enhanced billboard path

Enhanced pickup + valid catalog asset
  → imported shared mesh + generated material + controlled Y rotation

Enhanced pickup + missing/invalid asset
  → existing flat Enhanced EdgeMix billboard fallback
```

Runtime loader не запускает Blender и не строит model geometry. Он только
разрешает Unity-imported assets через generated catalog.

Animated pickup frame меняет mesh/material/header presentation атомарно через
существующий `PickupAnimator` → `SpriteBillboard.SetFrame`. Crossfade/morph не
требуется.

## Cache и lifetime

- Imported mesh/material assets являются shared и не мутируются per-instance.
- Per-instance billboard quad остаётся отдельным fallback resource.
- `SpriteBillboard.OnDestroy` не уничтожает imported/shared asset.
- Hot-switch не создаёт mesh/material/texture.
- Catalog resolution не читает WAD после map warm/close.
- Runtime drops и save restore используют тот же catalog.
- Missing entry логируется один раз на key и безопасно fallback.

## Shader и presentation

Новый pickup mesh shader должен сохранять Enhanced contracts:

- URP Forward, DepthOnly и ShadowCaster passes;
- geometric normals;
- sector ambient, additional lights и fog;
- Point-filtered palette-aware albedo;
- optional packed metallic/roughness mask;
- per-instance emission только если brief/manifest разрешает;
- MaterialPropertyBlock compatibility;
- build preflight/always-included shader registration.

Медленное presentation rotation включено только в Enhanced. Скорость и фаза
не входят в save schema; pause останавливает rotation. Bobbing не добавляется.

## Автоматические проверки

### Generator

- pinned Blender/version guard;
- schema validation briefs/parameters/manifest;
- repeat generation даёт тот же normalized geometry hash;
- no unapplied transforms, degenerate/non-manifold triangles или missing
  normals/UV;
- texture dimensions/format/filter contract;
- triangle, material и texture budgets;
- canonical bounds/pivot;
- front silhouette render и metric;
- preview views действительно используют разные camera transforms.

### EditMode

- generated catalog schema читается;
- каждый manifest entry находит imported mesh/material/texture;
- every `ItemRules.IsPickup` frame покрыт после expansion;
- animation/death-drop frames покрыты;
- bounds/pivot metadata соответствует imported mesh;
- Classic profile не включает parametric meshes;
- Enhanced profile допускает mesh path и fallback;
- shader присутствует в build preflight.

### PlayMode

- Enhanced pickup использует catalog mesh;
- Classic тот же pickup остаётся native flat billboard;
- non-pickup/enemy/FP weapon paths не меняются;
- `PatchHeader` placement и floor anchor сохраняются;
- pickup collection/touch/stats эквивалентны baseline;
- animation атомарно меняет mesh/material;
- death drops и save-restored pickups получают model;
- missing asset показывает flat Enhanced fallback;
- repeated Classic↔Enhanced switch не растит resources;
- map teardown не уничтожает shared imported assets.

Историческая baseline до эксперимента: **616 EditMode + 149 PlayMode**. Перед
реализацией totals проверяются на актуальном `main`; старые XML не являются
доказательством.

## Budgets и производительность

Начальные guardrails, уточняемые после первого accepted item:

- не более 10k triangles на один pickup LOD0 без отдельного обоснования;
- не более 4 material slots;
- предпочтительно один atlas/material на model;
- albedo/mask не выше 512×512 без visual evidence;
- никаких runtime mesh generation или texture uploads;
- все assets импортируются/build-time;
- optional distance billboard LOD вводится только по измеренному bottleneck и
  проходит отдельный visual gate.

Финальный baseline фиксирует:

- vertex/triangle/material/texture bytes по каждому family;
- unique rendered pickup assets на E1M1/E1M7;
- draw calls в pickup-heavy view;
- cold Enhanced boot и repeat mode switch;
- managed/native memory;
- standalone frame timing.

## Риски

- Один front sprite не определяет hidden geometry; brief может потребовать
  несколько итераций.
- Процедурная модель может быть геометрически верной, но потерять pixel-art
  язык из-за гладкого PBR.
- Front silhouette совпадёт, но модель станет неузнаваемой на gameplay
  distance.
- FBX importer/version может менять normals, scale или material assignment.
- Большое число unique materials увеличит draw calls.
- Animated frames могут получить model popping или floor jump.
- User-authored brief и generated outputs создают новый provenance/licensing
  класс assets, который нужно явно документировать.
- Восемь семейств могут не покрыть все Doom pickups; expansion обязан
  создавать новый gated family, а не искажать предмет.
- Отсутствие Blender на машине блокирует regeneration, но не Unity build из
  committed outputs.

## Инварианты

1. `pixel-extrusion` остаётся закрытым REJECT и не продолжается скрыто.
2. Каждый эталон имеет `BRIEF APPROVED` и `SUCCESS` до следующего.
3. Geometry/texture изменяются только через tracked source regeneration.
4. Clean clone Unity build не требует Blender.
5. Classic и non-pickup presentation не меняются.
6. Pickup gameplay, stats, save/load и world identity не меняются.
7. Placement выводится из native header contract.
8. Runtime не генерирует geometry.
9. Missing/invalid model всегда даёт видимый flat Enhanced fallback.
10. После expansion каждый pickup/frame имеет catalog coverage.
11. Neural generation не входит в deterministic v1.
12. Финальный visual verdict принадлежит пользователю, не автоматической
    silhouette metric.

## Закрытие эксперимента (2026-08-06)

Эксперимент остановлен на Stage 1 MEDIA0 до visual SUCCESS.

Причина (пользователь): итеративный цикл brief → чертёж → retune слишком
медленный для восьми эталонов + expansion, и текущий MEDIA0 candidate не
приблизился к нужному визуальному результату.

Следствия:

- Stages 2–13 не выполняются;
- runtime parametric mesh path не добавляется;
- Enhanced world pickups остаются flat EdgeMix 8× billboards;
- Stage 0 toolchain и MEDIA0 WIP на ветке — только archaeology.

## Критерий готовности

~~Scope закрыт только когда:~~ **N/A — experiment REJECTED before DoD.**

Исторический DoD (не достигнут):

1. Все восемь briefs и visual gates имеют датированный `SUCCESS`.
2. Family expansion имеет отдельный `SUCCESS`.
3. Manifest покрывает весь `ItemRules.IsPickup`, animations и death drops.
4. Generated source/outputs/provenance tracked и воспроизводимы.
5. Enhanced runtime использует models; fallback доказан.
6. Classic/non-pickup/gameplay/save invariants доказаны automation.
7. Focused и full EditMode/PlayMode suites зелёные на актуальном HEAD.
8. E1M1–E1M9 smoke зелёный.
9. Windows standalone собран Unity `6000.4.8f1`.
10. Perf/memory baseline записан.
11. Финальный standalone visual gate имеет `SUCCESS`.
12. Spec, plan и project status обновлены фактическими результатами.

## Связанные документы

- `docs/superpowers/plans/2026-08-05-inventory-parametric-pickups.md`
- `docs/superpowers/specs/2026-08-05-inventory-pixel-extrusion-design.md`
- `docs/superpowers/plans/2026-08-05-inventory-pixel-extrusion.md`
- `docs/superpowers/specs/2026-07-31-inventory-edge-mix-design.md`
- `docs/superpowers/plans/2026-07-31-inventory-edge-mix.md`
