# Enhanced Parametric Pickups — implementation plan/status

**Дата:** 2026-08-05  
**Ветка:** `parametric-pickups` от `main` (`19e6895`)  
**Статус:** ❌ REJECT 2026-08-06 — Stage 0 ✅; Stage 1 MEDIA0 stopped before visual SUCCESS. No Stage 2+/runtime.  
**Спека:** `docs/superpowers/specs/2026-08-05-inventory-parametric-pickups-design.md`  
**Rejected predecessor:** `docs/superpowers/plans/2026-08-05-inventory-pixel-extrusion.md`  
**Unity:** строго `6000.4.8f1`  
**Blender:** строго `4.5.12 LTS` (`D:\Tools\Blender-4.5.12\blender.exe` / `DOOM_BLENDER`)  
**Историческая baseline:** 616 EditMode + 149 PlayMode

## Branch boundary

`pixel-extrusion` закрыт как **REJECT/FAILED**. Его artifacts остаются только
на той ветке для archaeology.

Сделано перед Stage 0:

1. `pixel-extrusion` сохранён (`5142147`);
2. `parametric-pickups` создан от `main` (`19e6895`);
3. extrusion builder/shader/preview/tests не переносились;
4. перенесена только пара spec/plan (`37d95b8`);
5. Stage 0 toolchain/export/tests зелёные на branch HEAD.

Feature implementation идёт только на `parametric-pickups`.

## Goal

Создать deterministic offline Blender pipeline, который по native WAD
reference, versioned user brief и per-item parameters генерирует полноценные
FBX/PNG pickup assets. Сначала восемь эталонов проходят отдельные
пользовательские gates, затем их model families покрывают весь
`ItemRules.IsPickup` scope. Только после offline visual acceptance добавляется
Enhanced runtime routing.

Classic, non-pickup rendering, gameplay touch logic, stats, save/load и world
identity не меняются.

## Architecture

```text
freedoom1.wad
  → Unity Editor reference export
  → native sprite/header/inventory manifest in Temp

user brief + JSON parameters + Blender Python
  → Blender 4.5.12 background generation
  → FBX + palette-aware PNG + normalized metrics/hash
  → committed Assets + generated Unity catalog

runtime:
ItemRules.IsPickup + sprite/frame + GraphicsProfile
  ├─ Classic/non-pickup → existing billboard
  ├─ Enhanced + catalog hit → imported mesh presentation
  └─ Enhanced + catalog miss → existing flat EdgeMix fallback
```

## Planned asset layout

```text
Tools/ParametricPickups/
  README.md
  generate.ps1
  generate.py
  validate.py
  schemas/
    brief.schema.json
    parameters.schema.json
    manifest.schema.json
  common/
    scene.py
    geometry.py
    materials.py
    export.py
    preview.py
    metrics.py
    wad_reference.py
  generators/
    hard_surface_box.py
    thin_hard_surface.py
    cylindrical.py
    thin_plate.py
    lathe.py
    dual_shell.py
    compound_weapon.py
  briefs/
  parameters/
  tests/

Assets/Models/Pickups/Generated/
  *.fbx
  manifest.json

Assets/Textures/Pickups/Generated/
  *_albedo.png
  optional *_mask.png

Assets/Resources/Doom/
  ParametricPickupCatalog.asset
```

Unity `.meta` files для tracked outputs обязательны. `.blend`, temporary
reference exports, previews и test scenes остаются в `Temp/`/`Logs/`.

## Implementation invariants

1. Не начинать следующий item до `BRIEF APPROVED` и visual `SUCCESS`.
2. Не делать hidden geometry assumptions до пользовательского brief.
3. Geometry/texture меняются только через tracked Python/JSON regeneration.
4. Manual post-export FBX/PNG editing запрещён.
5. Clean clone Unity build не требует Blender.
6. Blender generation требует exact `4.5.12 LTS` и fail-fast на mismatch.
7. Imported mesh coordinates используют native sprite texel units; runtime
   применяет существующий `worldScale`.
8. `PatchHeader` остаётся источником width/height/left/top placement.
9. Runtime не строит model geometry и не запускает external process.
10. Classic profile никогда не выбирает parametric mesh.
11. Missing/invalid model всегда показывает flat Enhanced pickup.
12. Pickup collider/touch, inventory, stats и save schema не меняются.
13. Imported Mesh/Texture assets не уничтожаются runtime. Только созданные
    runtime Materials регистрируются как owned.
14. Animated frame swap атомарен и не показывает blank/stale frame.
15. Neural generation не входит в v1.

## Protocol каждого item stage

Каждый Stage 1–8 выполняется одинаково.

### Checkpoint A — BRIEF APPROVED

1. Извлечь native sprite и принятый Enhanced EdgeMix reference.
2. Приложить их пользователю.
3. Запросить:
   - semantic identity;
   - составные части;
   - front silhouette invariants;
   - side/back/top;
   - symmetry;
   - materials/colors/decals;
   - proportions/depth;
   - допустимые отклонения;
   - presentation rotation.
4. Создать/обновить `briefs/<LUMP>.md`.
5. Показать структурированное резюме и получить `BRIEF APPROVED`.

До approval запрещено писать item generator/parameters.

### Checkpoint B — VISUAL VERDICT

1. Сгенерировать FBX/PNG дважды и сравнить normalized hashes.
2. Прогнать focused generator validation.
3. Создать `было | front | 45° | side | back | top` в одном масштабе.
4. Создать настоящий turntable.
5. Приложить panels/turntable и краткие metrics.
6. Получить:
   - `SUCCESS`: заморозить brief/parameter revision;
   - `RETUNE`: повторить candidate внутри текущего brief;
   - `REJECT`: удалить/архивировать candidate и вернуться к Checkpoint A.

`RETUNE`/`REJECT` блокируют следующий stage. Verdict записывается в
`Logs/parametric-pickups/<NN>-<LUMP>/VERDICT.md` и в status section plan.

## Generator CLI

Планируемый вызов:

```powershell
& "C:\Program Files\Blender Foundation\Blender 4.5\blender.exe" `
  --background --factory-startup `
  --python "Tools/ParametricPickups/generate.py" -- `
  --project-root "D:\Development\doom-parametric-pickups" `
  --lump MEDIA0 `
  --reference-dir "Logs/parametric-pickups/references" `
  --output-dir "Assets/Models/Pickups/Generated" `
  --texture-dir "Assets/Textures/Pickups/Generated" `
  --preview-dir "Logs/parametric-pickups/01-MEDIA0"
```

После install фактический path уточняется, но version остаётся pinned.
Wrapper `generate.ps1` валидирует executable/version и не скачивает toolchain
неявно.

Generator tests:

```powershell
& "<blender-4.5.12>\blender.exe" `
  --background --factory-startup `
  --python "Tools/ParametricPickups/tests/run_tests.py"
```

## Unity test CLI

EditMode:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
  -batchmode -nographics `
  -projectPath "D:\Development\doom-parametric-pickups" `
  -runTests -testPlatform EditMode `
  -testResults "D:\Development\doom-parametric-pickups\Logs\parametric-edit.xml" `
  -logFile "D:\Development\doom-parametric-pickups\Logs\parametric-edit.log"
```

PlayMode — без `-nographics`:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
  -batchmode `
  -projectPath "D:\Development\doom-parametric-pickups" `
  -runTests -testPlatform PlayMode `
  -testResults "D:\Development\doom-parametric-pickups\Logs\parametric-play.xml" `
  -logFile "D:\Development\doom-parametric-pickups\Logs\parametric-play.log"
```

`-runTests` не сочетать с `-quit`.

---

## Stage 0 — Toolchain, schemas и reference export ✅

**Статус:** SUCCESS 2026-08-05  
**Цель:** воспроизводимый пустой pipeline до моделирования первого item.

**Prerequisites:**

- [x] clean feature branch `parametric-pickups` от `main`;
- [x] Blender `4.5.12 LTS` установлен (portable `D:\Tools\Blender-4.5.12`);
- [x] Unity `6000.4.8f1` batch export выполнен;
- [ ] full baseline suites на branch HEAD — deferred until Stage 11+; Stage 0
      gate is Blender tests + reference export only.

**Create:**

- `Tools/ParametricPickups/README.md`
- `Tools/ParametricPickups/generate.ps1`
- `Tools/ParametricPickups/generate.py`
- `Tools/ParametricPickups/validate.py`
- `Tools/ParametricPickups/schemas/*.schema.json`
- `Tools/ParametricPickups/common/{scene,geometry,materials,export,preview,metrics,wad_reference}.py`
- `Tools/ParametricPickups/tests/run_tests.py`
- `Assets/Scripts/MapBuild/Editor/ParametricPickupReferenceMenu.cs`
- corresponding `.meta`

### Task 0.1 — Pinned runner

- [x] Проверить Blender version string exactly `4.5.12`.
- [x] Передать paths только как explicit args.
- [x] Очистить scene через factory startup и reset helper.
- [x] Установить units, axes, color management и seeds.
- [x] Fail non-zero на schema/generation/validation error.
- [x] Не менять committed output при failed run: писать во временную папку и
      atomic replace только после success.

### Task 0.2 — Reference exporter

- [x] `Tools > Doom > Export Parametric Pickup References` читает
      `freedoom1.wad`.
- [x] Экспортировать native RGBA, EdgeMix 8× (`Scale8XContrastGated`) и exact
      `PatchHeader`.
- [x] Экспортировать authoritative inventory JSON из `ItemRules.IsPickup`,
      `ThingTable`, `PickupAnimationTable` и death-drop mappings.
- [x] Записать WAD SHA-256 и palette identity.
- [x] Output в `Logs/parametric-pickups/references/` (Unity clears project
      `Temp/` on exit, so references cannot live there).
- [x] CLI entrypoint `-executeMethod ...ParametricPickupReferenceMenu.ExportCli -quit`.

**Export evidence (2026-08-05):** 30 pickups, 47 frames, 47 unique lumps;
`identity.json` + `inventory.json` + native/edgemix/headers.

### Task 0.3 — Empty-scene validation

- [x] Unit tests для transform/axis/pivot helpers.
- [x] Unit tests для normalized geometry hash, не зависящего от timestamps.
- [x] Preview marker test доказывает правильный order/orientation views.
- [x] Back camera transform отличается от front.
- [x] Один synthetic primitive экспортируется и проходит повторный hash.

**Gate 0:** SUCCESS — `generate.ps1 -Mode test` → `ALL STAGE 0 TESTS PASSED`;
synthetic repeat-hash + Unity reference export green. Пользовательский art
verdict не требуется.

**Hard stop lifted for Stage 1 brief:** pinned Blender and repeatable synthetic
export confirmed. Do not implement MEDIA0 generator until `BRIEF APPROVED`.

---

## Stage 1 — MEDIA0 ❌ REJECT 2026-08-06

**Family candidate:** `hard_surface_box`  
**Log:** `Logs/parametric-pickups/01-MEDIA0/`  
**Brief:** APPROVED 2026-08-06 (r1; top = latches protrude above body, not flat lid)  
**Visual gate:** REJECT — user stopped experiment (loop too slow; not approaching needed result). No AMMOA0 / further stages.

**Create after BRIEF APPROVED:**

- `Tools/ParametricPickups/briefs/MEDIA0.md`
- `Tools/ParametricPickups/parameters/MEDIA0.json`
- `Tools/ParametricPickups/generators/hard_surface_box.py`
- `Tools/ParametricPickups/tests/test_media0.py`
- `Assets/Models/Pickups/Generated/MEDIA0.fbx`
- `Assets/Textures/Pickups/Generated/MEDIA0_albedo.png`
- optional `Assets/Textures/Pickups/Generated/MEDIA0_mask.png`

### Tasks

- [x] Получить `BRIEF APPROVED`; не считать вид Gate 1 extrusion brief-ом.
- [x] Реализовать reusable box primitives, bevel/profile/panel/decal API.
- [x] Подогнать canonical front footprint и anchor.
- [x] Генерировать materials/texture только из brief/reference.
- [x] Проверить manifold geometry, normals, UV, no degenerates.
- [x] Зафиксировать initial triangle/material/texture budgets (244→332 tris, ≤4 mats).
- [x] Сгенерировать drawing/comparison/turntable — visual `SUCCESS` **не получен**.
- [x] **REJECT 2026-08-06:** эксперимент остановлен пользователем на MEDIA0.

**Focused tests:** `test_media0`, repeat hash, front silhouette, pivot, preview
views.

**Hard stop:** experiment closed; do not start `AMMOA0` or runtime routing.

---

## Stage 2 — AMMOA0

**Family candidate:** `hard_surface_box/container`  
**Log:** `Logs/parametric-pickups/02-AMMOA0/`

**Create after BRIEF APPROVED:**

- `Tools/ParametricPickups/briefs/AMMOA0.md`
- `Tools/ParametricPickups/parameters/AMMOA0.json`
- `Tools/ParametricPickups/tests/test_ammoa0.py`
- `Assets/Models/Pickups/Generated/AMMOA0.fbx`
- `Assets/Textures/Pickups/Generated/AMMOA0_albedo.png`
- optional `Assets/Textures/Pickups/Generated/AMMOA0_mask.png`

**Modify:**

- `Tools/ParametricPickups/generators/hard_surface_box.py`

### Tasks

- [ ] Получить отдельный brief; не клонировать MEDIA proportions/materials.
- [ ] Расширить family API только reusable container concepts.
- [ ] Проверить lid/seam/label readability на gameplay scale.
- [ ] Не регрессировать frozen `MEDIA0` normalized hash и previews.
- [ ] Получить `AMMOA0 SUCCESS`.

**Hard stop:** любое изменение accepted MEDIA требует отдельного regression
explanation/gate; без AMMO success не начинать RKEY.

---

## Stage 3 — RKEYA0

**Family candidate:** `thin_hard_surface`  
**Log:** `Logs/parametric-pickups/03-RKEYA0/`

**Create after BRIEF APPROVED:**

- `Tools/ParametricPickups/briefs/RKEYA0.md`
- `Tools/ParametricPickups/parameters/RKEYA0.json`
- `Tools/ParametricPickups/generators/thin_hard_surface.py`
- `Tools/ParametricPickups/tests/test_rkeya0.py`
- `Assets/Models/Pickups/Generated/RKEYA0.fbx`
- `Assets/Textures/Pickups/Generated/RKEYA0_albedo.png`
- optional `Assets/Textures/Pickups/Generated/RKEYA0_mask.png`

### Tasks

- [ ] Получить brief о physical identity key/card/device, back и cutouts.
- [ ] Реализовать controlled thickness, profile holes и relief/decal.
- [ ] Проверить side readability без превращения предмета в slab.
- [ ] Проверить back не является случайным mirror.
- [ ] Получить `RKEYA0 SUCCESS`.

**Hard stop:** не начинать cylinder family до accepted thin-object language.

---

## Stage 4 — STIMA0

**Family candidate:** `cylindrical`  
**Log:** `Logs/parametric-pickups/04-STIMA0/`

**Create after BRIEF APPROVED:**

- `Tools/ParametricPickups/briefs/STIMA0.md`
- `Tools/ParametricPickups/parameters/STIMA0.json`
- `Tools/ParametricPickups/generators/cylindrical.py`
- `Tools/ParametricPickups/tests/test_stima0.py`
- `Assets/Models/Pickups/Generated/STIMA0.fbx`
- `Assets/Textures/Pickups/Generated/STIMA0_albedo.png`
- optional `Assets/Textures/Pickups/Generated/STIMA0_mask.png`

### Tasks

- [ ] Утвердить ось, caps/rings, transparent/opaque interpretation и cross.
- [ ] Реализовать radial segments с explicit low-poly/pixel-art control.
- [ ] Исключить smooth photorealistic shading.
- [ ] Проверить silhouette front/side и radial highlight stability.
- [ ] Получить `STIMA0 SUCCESS`.

**Hard stop:** cylinder form должна читаться на gameplay distance.

---

## Stage 5 — BON2A0

**Family candidate:** `thin_plate`  
**Log:** `Logs/parametric-pickups/05-BON2A0/`

**Create after BRIEF APPROVED:**

- `Tools/ParametricPickups/briefs/BON2A0.md`
- `Tools/ParametricPickups/parameters/BON2A0.json`
- `Tools/ParametricPickups/generators/thin_plate.py`
- `Tools/ParametricPickups/tests/test_bon2a0.py`
- `Assets/Models/Pickups/Generated/BON2A0.fbx`
- `Assets/Textures/Pickups/Generated/BON2A0_albedo.png`
- optional `Assets/Textures/Pickups/Generated/BON2A0_mask.png`

### Tasks

- [ ] Утвердить shield/plate identity, curvature, rim, back и relief.
- [ ] Строить front profile из controlled curve, не alpha extrusion.
- [ ] Добавить bounded curvature/thickness и deterministic rim.
- [ ] Проверить, что side тонкий, но не исчезает.
- [ ] Получить `BON2A0 SUCCESS`.

**Hard stop:** не принимать flat card с декоративной глубиной как volume
model без явного пользовательского решения.

---

## Stage 6 — BON1A0

**Family candidate:** `lathe`  
**Log:** `Logs/parametric-pickups/06-BON1A0/`

**Create after BRIEF APPROVED:**

- `Tools/ParametricPickups/briefs/BON1A0.md`
- `Tools/ParametricPickups/parameters/BON1A0.json`
- `Tools/ParametricPickups/generators/lathe.py`
- `Tools/ParametricPickups/tests/test_bon1a0.py`
- `Assets/Models/Pickups/Generated/BON1A0.fbx`
- `Assets/Textures/Pickups/Generated/BON1A0_albedo.png`
- optional `Assets/Textures/Pickups/Generated/BON1A0_mask.png`

### Tasks

- [ ] Утвердить profile, symmetry axis, neck, cap, body/material.
- [ ] Реализовать piecewise profile curve и deterministic revolution.
- [ ] Поддержать frame variation parameters без geometry morph.
- [ ] Экспортировать/проверить все существующие BON1 animation frames,
      если brief определяет их как один rotating/animated object.
- [ ] Проверить отсутствие frame scale/pivot jump.
- [ ] Получить `BON1A0 SUCCESS`.

**Hard stop:** animation contract должен быть определён до dual-shell stage.

---

## Stage 7 — ARM1A0

**Family candidate:** `dual_shell`  
**Log:** `Logs/parametric-pickups/07-ARM1A0/`

**Create after BRIEF APPROVED:**

- `Tools/ParametricPickups/briefs/ARM1A0.md`
- `Tools/ParametricPickups/parameters/ARM1A0.json`
- `Tools/ParametricPickups/generators/dual_shell.py`
- `Tools/ParametricPickups/tests/test_arm1a0.py`
- `Assets/Models/Pickups/Generated/ARM1A0.fbx`
- `Assets/Textures/Pickups/Generated/ARM1A0_albedo.png`
- optional `Assets/Textures/Pickups/Generated/ARM1A0_mask.png`

### Tasks

- [ ] Утвердить wearable/cast identity, cavity, shoulder parts, rim и back.
- [ ] Строить separate front/back surfaces с controlled separation.
- [ ] Проверить no self-intersection/non-manifold shell seams.
- [ ] Проверить view сверху/сзади, где front sprite не даёт данных.
- [ ] Определить animation-frame variation contract.
- [ ] Получить `ARM1A0 SUCCESS`.

**Hard stop:** back/cavity не выводятся автоматически из mirrored front.

---

## Stage 8 — SHOTA0

**Family candidate:** `compound_weapon`  
**Log:** `Logs/parametric-pickups/08-SHOTA0/`

**Create after BRIEF APPROVED:**

- `Tools/ParametricPickups/briefs/SHOTA0.md`
- `Tools/ParametricPickups/parameters/SHOTA0.json`
- `Tools/ParametricPickups/generators/compound_weapon.py`
- `Tools/ParametricPickups/tests/test_shota0.py`
- `Assets/Models/Pickups/Generated/SHOTA0.fbx`
- `Assets/Textures/Pickups/Generated/SHOTA0_albedo.png`
- optional `Assets/Textures/Pickups/Generated/SHOTA0_mask.png`

### Tasks

- [ ] Утвердить barrels, receiver, grip, hidden side/back и orientation.
- [ ] Собрать hierarchy из reusable hard-surface/cylinder helpers.
- [ ] Apply transforms и export одним combined presentation mesh либо
      bounded submeshes по material contract.
- [ ] Проверить long-object pivot/floor anchor и rotation envelope.
- [ ] Проверить front silhouette и recognizability на gameplay distance.
- [ ] Получить `SHOTA0 SUCCESS`.

**Hard stop:** family expansion запрещён, пока все восемь statuses не
`SUCCESS`.

---

## Stage 9 — Full pickup inventory и family expansion

**Зависимость:** Stages 1–8 = `SUCCESS`.

**Цель:** покрыть каждый pickup lump/frame утверждёнными model families до
runtime integration.

**Create:**

- `Tools/ParametricPickups/parameters/catalog.json`
- briefs/parameters для новых semantic variations
- `Tools/ParametricPickups/tests/test_catalog_coverage.py`
- generated FBX/PNG для полного scope
- `Assets/Models/Pickups/Generated/manifest.json`

### Task 9.1 — Authoritative inventory

- [ ] Экспортировать authoritative inventory JSON заново на branch HEAD.
- [ ] Перечислить все `ItemRules.IsPickup` doomednums.
- [ ] Разрешить sprite/frame/rotation refs через `SpriteSet`.
- [ ] Добавить каждый `PickupAnimationTable` frame.
- [ ] Добавить POSS→CLIP и SPOS→SHOT runtime drops.
- [ ] Дедуплицировать aliases только при идентичном resolved lump.

### Task 9.2 — Family assignment

- [ ] Назначить каждому lump family + variation + brief revision.
- [ ] Не forcing item в неподходящее family.
- [ ] Для новой semantic family остановиться и провести дополнительный
      brief/visual gate.
- [ ] Замороженные восемь assets не менять без regression gate.

### Task 9.3 — Coverage validation

- [ ] Нет unmapped pickup/frame.
- [ ] Нет orphan generated assets.
- [ ] Все outputs проходят geometry/material/budget validation.
- [ ] Повторная full generation даёт те же normalized hashes.
- [ ] Manifest содержит provenance и metrics.

### Batch visual gate 9 — обязательный STOP

- [ ] Сгенерировать contact sheets по health/armor/ammo/keys/powers/weapons.
- [ ] Показать front/45°/side/back для каждой variation.
- [ ] Показать animated turntables/clips.
- [ ] Получить `SUCCESS`, `RETUNE` или `REJECT`.

Без `SUCCESS` запрещено создавать runtime catalog/routing.

---

## Stage 10 — Unity import и catalog

**Зависимость:** batch Gate 9 = `SUCCESS`.

**Create:**

- `Assets/Scripts/MapBuild/Rendering/ParametricPickupManifest.cs`
- `Assets/Scripts/MapBuild/Rendering/ParametricPickupCatalogAsset.cs`
- `Assets/Scripts/MapBuild/Rendering/ParametricPickupCatalog.cs`
- `Assets/Scripts/MapBuild/Editor/ParametricPickupCatalogBuilder.cs`
- `Assets/Resources/Doom/ParametricPickupCatalog.asset`
- `Assets/Tests/EditMode/Map/ParametricPickupCatalogTests.cs`
- corresponding `.meta`

**Modify:**

- `Assets/Scripts/MapBuild/Editor/Doom.MapBuild.Editor.asmdef`
- importer configuration only through tracked Editor code

### Task 10.1 — Import contract

- [ ] ModelImporter fixes scale, axes, normals/tangents, animation off,
      material import policy and mesh readability.
- [ ] TextureImporter fixes Point filtering, no streaming, wrap/clamp, alpha
      and color-space contracts.
- [ ] Postprocessor не трогает unrelated FBX/PNG.
- [ ] Import metrics match manifest within explicit tolerance.

### Task 10.2 — Catalog asset

- [ ] Builder validates JSON schema/provenance.
- [ ] Resolve key: sprite prefix + logical frame/resolved lump.
- [ ] Catalog entry references Mesh, albedo/mask and native metadata.
- [ ] Duplicate/missing key fails build, а не silently overwrites.
- [ ] Catalog ScriptableObject находится в Resources для build inclusion.
- [ ] Imported Mesh/Texture lifetime принадлежит Unity AssetDatabase/build.

### Focused EditMode tests

- manifest parse/version;
- full authoritative coverage;
- imported references non-null;
- canonical bounds/pivot;
- no duplicate keys;
- importer settings;
- catalog build is idempotent.

---

## Stage 11 — Enhanced runtime prototype

**Цель:** один `MEDIA0` использует runtime model; остальные временно остаются
flat для узкой проверки integration seam.

**Create:**

- `Assets/Scripts/MapBuild/ParametricPickupPresentation.cs`
- `Assets/Shaders/DoomEnhancedParametricPickup.shader`
- `Assets/Shaders/DoomEnhancedParametricPickup.shader.meta`
- `Assets/Tests/PlayMode/ParametricPickupPlayTests.cs`
- `.meta`

**Modify:**

- `Assets/Scripts/MapBuild/SpriteBillboard.cs`
- `Assets/Scripts/MapBuild/SpriteCache.cs`
- `Assets/Scripts/MapBuild/Rendering/GraphicsProfile.cs`
- `Assets/Scripts/MapBuild/Rendering/DoomMaterialFactory.cs`
- `Assets/Scripts/MapBuild/Rendering/WorldRenderContext.cs`
- `Assets/Scripts/MapBuild/Editor/ConfigureUrpPipeline.cs`
- `Assets/Scripts/MapBuild/Editor/Stage7BuildMenu.cs`

### Task 11.1 — Presentation seam

- [ ] Добавить `GraphicsProfile.ParametricPickups`, true только Enhanced.
- [ ] Сохранить owned per-instance flat quad отдельно от imported shared mesh.
- [ ] В prototype allowlist только `MEDIA0`.
- [ ] Enhanced catalog hit выбирает mesh/material и отключает camera-facing.
- [ ] Controlled Y rotation использует scaled game time и pause.
- [ ] Classic/hit failure возвращает quad, material и billboard facing.
- [ ] Placement применяет native header anchor + worldScale, не FBX guesses.

### Task 11.2 — Material/shader

- [ ] Real geometric normals.
- [ ] URP Forward/DepthOnly/ShadowCaster.
- [ ] Sector ambient, additional lights, fog, MPB contracts.
- [ ] Point albedo и optional mask.
- [ ] Добавить shader в preflight/always-included path.
- [ ] Runtime-created materials cached и registered owned; imported assets не
      уничтожаются.

### Focused PlayMode tests

- Enhanced MEDIA uses imported mesh/shader;
- Classic MEDIA uses native quad;
- non-allowlisted pickup remains flat;
- enemy/effect/FP weapon unchanged;
- header placement/floor anchor unchanged;
- missing catalog/material gives Enhanced flat fallback;
- collection and stats unchanged;
- two instances share Mesh/material.

### Visual gate 11

- [ ] Windows standalone.
- [ ] MEDIA front/side/back/top во вращении.
- [ ] Floor anchor, shadow, fog и sector/additional lights.
- [ ] Classic↔Enhanced switch рядом с предметом.
- [ ] Получить `SUCCESS`.

---

## Stage 12 — Full runtime routing, animation и drops

**Зависимость:** Gate 11 = `SUCCESS`.

**Modify:**

- `Assets/Scripts/MapBuild/ParametricPickupPresentation.cs`
- `Assets/Scripts/MapBuild/SpriteBillboard.cs`
- `Assets/Scripts/MapBuild/PickupAnimator.cs` только если `SetFrame`
  недостаточно
- `Assets/Scripts/MapBuild/PickupFactory.cs` только для catalog warm/resolve
- `Assets/Scripts/MapBuild/ThingSpawner.cs` только для catalog warm/resolve
- `Assets/Tests/PlayMode/ParametricPickupPlayTests.cs`
- `Assets/Tests/PlayMode/GraphicsResourceLifetimePlayTests.cs`

### Tasks

- [ ] Удалить MEDIA allowlist; source scope остаётся `ItemRules.IsPickup`.
- [ ] Все catalog hits используют mesh.
- [ ] Frame change атомарно меняет mesh/material/placement metadata.
- [ ] Animated pickup не даёт blank frame, stale material или floor jump.
- [ ] Death drops работают после закрытия WAD.
- [ ] Save-restored runtime pickups получают тот же derived presentation.
- [ ] 20 hot-switch cycles не растят runtime material/instance mesh counts.
- [ ] Missing/corrupt entry логируется один раз и fallback.
- [ ] Collection одного instance не влияет на shared imported assets.

### Focused filters

```text
ParametricPickupPlayTests
PickupPlayTests
SpriteUpscalePlayTests
GraphicsModePlayTests
GraphicsResourceLifetimePlayTests
SaveLoadPlayTests
```

### Visual gate 12

- [ ] E1M1/E1M2 representative clusters.
- [ ] Static и animated items.
- [ ] Dropped clip/shotgun.
- [ ] Repeated Classic↔Enhanced switch.
- [ ] Gameplay near/medium distance readability.
- [ ] Получить `SUCCESS`.

---

## Stage 13 — Regression, performance и close-out

**Create:**

- `Logs/inventory-parametric-pickups-baseline-notes.md`
- optional capture tests only if existing harness cannot produce required
  evidence

**Modify after final verdict:**

- `docs/superpowers/specs/2026-08-05-inventory-parametric-pickups-design.md`
- `docs/superpowers/plans/2026-08-05-inventory-parametric-pickups.md`
- `CLAUDE.md`
- `docs/doom-unity-remake-plan.md` if roadmap records post-Stage-8 work

### Task 13.1 — Metrics

- [ ] Per asset: vertices, triangles, materials, texture bytes.
- [ ] Unique pickup assets/material instances E1M1/E1M7.
- [ ] Pickup-heavy draw calls/frame timing.
- [ ] Cold Enhanced boot и repeat switch.
- [ ] Managed/native memory.
- [ ] Validate initial budgets; optimize only measured bottlenecks.

Mitigation order:

1. remove unnecessary hidden/internal geometry in generator;
2. merge compatible materials/atlas;
3. simplify bevel/radial segments;
4. verify material cache;
5. restrict shadows if measured;
6. only then add distance flat-billboard LOD with separate visual gate.

### Task 13.2 — Full automation/build

- [ ] Generator full regeneration/validation зелёные.
- [ ] Full EditMode suite зелёный на current HEAD.
- [ ] Full PlayMode suite зелёный без `-nographics`.
- [ ] E1M1–E1M9 smoke зелёный.
- [ ] Save/load/world capture regressions зелёные.
- [ ] Windows standalone build через pinned Unity.
- [ ] Build log, size и baseline записаны.

### Final visual gate 13

- [ ] Финальные `было/стало` panels для полного scope.
- [ ] Standalone E1M1/E1M2/E1 representative animations/drops.
- [ ] Front/side/back, distance readability, lighting/shadows/fog.
- [ ] Classic regression spot-check.
- [ ] Получить `SUCCESS — оставляем` или `REJECT — runtime path не сливать`.

Только после `SUCCESS` обновить project status.

## Stop conditions

Работа не продвигается, если:

- feature branch не создан чисто от `main`;
- exact Blender/Unity version недоступна;
- текущий item не имеет `BRIEF APPROVED` и `SUCCESS`;
- generator требует manual FBX/PNG edits;
- repeat normalized hash нестабилен;
- preview back клонирован или view order/scale неверен;
- accepted item регрессировал без нового gate;
- family expansion имеет unmapped pickup/frame;
- imported bounds/pivot не соответствуют manifest/header contract;
- Classic/non-pickup rendering изменился;
- pickup gameplay/save regression появился;
- missing asset делает item невидимым;
- imported assets уничтожаются instance teardown;
- resource counts растут после repeated hot-switch;
- standalone отличается от accepted offline/runtime preview.

## Definition of Done

- [ ] `pixel-extrusion` остаётся документированным REJECT.
- [ ] Восемь item briefs имеют `BRIEF APPROVED`.
- [ ] Восемь item visual gates имеют датированный `SUCCESS`.
- [ ] Family expansion batch gate имеет `SUCCESS`.
- [ ] Manifest покрывает все pickups, animations и death drops.
- [ ] Python/JSON source, FBX/PNG outputs и provenance tracked.
- [ ] Clean clone Unity build не требует Blender.
- [ ] Exact-tool regeneration проходит validation.
- [ ] Enhanced runtime meshes и flat fallback доказаны.
- [ ] Classic/non-pickup/gameplay/save invariants зелёные.
- [ ] Full suites и E1 smoke зелёные.
- [ ] Perf/memory baseline и Windows build готовы.
- [ ] Финальный standalone visual gate имеет `SUCCESS`.
- [ ] Spec, plan, roadmap/project status отражают фактический результат.

## Связанные документы

- `docs/superpowers/specs/2026-08-05-inventory-parametric-pickups-design.md`
- `docs/superpowers/specs/2026-08-05-inventory-pixel-extrusion-design.md`
- `docs/superpowers/plans/2026-08-05-inventory-pixel-extrusion.md`
- `docs/superpowers/specs/2026-07-31-inventory-edge-mix-design.md`
- `docs/superpowers/plans/2026-07-31-inventory-edge-mix.md`
