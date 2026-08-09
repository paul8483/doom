# TRELLIS.2 pickups — Gate 0 status

**Дата:** 2026-08-07  
**Статус:** LIVE TEST SUCCESS — MEDIA0/BON1A0/weapons + COLUA0 floor lamp
+ BAR1A0 exploding barrel + TRE2A0/SMITA0 trees + ammo set
(CLIPA0/SBOXA0/AMMOA0/CELLA0/CELPA0/ROCKA0/SHELA0) wired in Enhanced.  
**Runtime:** Enhanced routes medikit (2012), health bonus (2014), six weapon
pickups (2001–2006), ammo pickups (2007 CLIPA0, 2048 AMMOA0, 2008 SHELA0,
2049 SBOXA0, 2010 ROCKA0, 2047 CELLA0, 17 CELPA0), COLU floor lamp (2028),
exploding barrel (2035), TRE2 large tree (54), and SMIT leafy tree (47) to
textured TRELLIS.2 meshes; Classic keeps native billboards. Pickup gameplay,
lamp sticky lights, and barrel explosion rules are unchanged. On barrel death
the 3D mesh reverts to the BEXP billboard sequence.  
**Baseline:** MEDIA0/BON1A0 interactive verdict SUCCESS on 2026-08-06. The
six-weapon Enhanced rollout passed automation, Windows build and interactive
scale/orientation/brightness/pickup/performance sign-off on 2026-08-07.
COLUA0 interactive standalone/editor verdict SUCCESS on 2026-08-07
(«результат отличный»). BAR1A0 interactive standalone verdict SUCCESS on
2026-08-07 («результат отличный»). TRE2A0/SMITA0 interactive standalone
verdict SUCCESS on 2026-08-07 after WAD palette albedo correction and
SectorFog support in `Doom/ExperimentalPickupUnlit`. Ammo
`AMMOA0`/`CELLA0`/`CELPA0`/`ROCKA0`/`SHELA0` interactive standalone gate
**SUCCESS** 2026-08-09 (3D mesh + 2D display-redraw; Windows build).

## Goal

Проверить, может ли официальный Microsoft TRELLIS.2 превратить native
Freedoom pickup sprite в узнаваемый полноценный 3D mesh быстрее, чем
отклонённый per-item parametric Blender loop.

Gate 0 выполняется только в официальном Hugging Face Space:

`https://huggingface.co/spaces/microsoft/TRELLIS.2`

Локальный официальный запуск не подходит: рабочая машина использует Intel Arc
B580, WSL не установлен, а официальный pipeline требует Linux, NVIDIA CUDA и
не менее 24 GB VRAM. Упомянутые в README `<10s Single CPU` относятся только к
конвертации готового textured mesh в O-Voxel, не к image-to-3D inference.
Официального CPU-only fallback нет.

## Input protocol

1. Декодировать native WAD patch через первый `PLAYPAL`.
2. Обрезать только полностью прозрачные поля.
3. Увеличить целым nearest-neighbor scale до максимума 416 px по большей оси.
4. Центрировать на прозрачном RGBA canvas 512×512.
5. Не дорисовывать неизвестные стороны и не добавлять фон.

Hugging Face settings:

- Resolution: `512`
- Decimation Target: `100000`
- Texture Size: `1024`
- для строгого A/B отключать Randomize Seed и повторять сохранённый seed

Локальные native Gate 0 inputs:

- `Logs/trellis2-gate0/MEDIA0-trellis.png`
- `Logs/trellis2-gate0/MEDIA0-edgemix8x-trellis.png`
- `Logs/trellis2-gate0/STIMA0-trellis.png`
- `Logs/trellis2-gate0/BON1A0-trellis.png`

`Logs/` gitignored; это локальные продолжительные artifacts, не runtime assets.
Durable conditioning images and exported GLBs are versioned under
`Textures/Trellis2/`.

## Interactive verdicts

### MEDIA0 native — SUCCESS

Trellis построил узнаваемую объёмную пиксельную аптечку: корректно прочитал
коробчатый корпус, выступающие боковые части, центральную зелёную панель и
крест. Результат заметно лучше uniform pixel extrusion и parametric MEDIA0
candidate. Позже GLB принят для Enhanced live runtime test.

### MEDIA0 EdgeMix 8× — REJECT

Текущий runtime `Scale8XContrastGated` был подан как conditioning image.
Результат стал размытым и плоским. EdgeMix сгладил важные shape cues, не
добавив новой информации о глубине. Решение: для TRELLIS.2 использовать native
WAD sprites, увеличенные только nearest-neighbor.

### STIMA0 native — REJECT, 3/3

Во всех трёх random seeds модель построила медицинскую коробку/контейнер с
боковой втулкой вместо цилиндра. Почти прямоугольный фронтальный силуэт и
белый крест семантически перетягивают результат к аптечке.

### BON1A0 native — SHAPE REJECT 2/2; LIVE RUNTIME ACCEPTED

Оба результата превратили округлое тело вращения в кубический/voxel-флакон,
поэтому строгий Gate 0 на восстановление круглой формы не пройден. Однако один
результат позже проверен в реальной игре и принят как удачная стилизованная
3D-колба после настройки масштаба, emission и зелёного локального света.

### ARM1A0 native — CANDIDATE ACCEPTED

Последний native run сохранил узнаваемый силуэт брони, наплечники и центральную
эмблему. Объёма недостаточно, но результат признан пригодным для следующего
live runtime gate. Дополнительно подготовлен depth shape-hint для более
выраженных изгибов и толщины.

## Live runtime test — SUCCESS

`MEDIA0_2026-08-06T185331.895.glb` and
`BON1A0_2026-08-06T185552.641.glb` were converted to textured OBJ resources
without mesh simplification (about 94k triangles each) and routed only in
Enhanced mode. The original billboard remains the Classic presentation and
all collision, collection, stats and save identity remain on the existing
pickup root.

The first Lit pass made MEDIA0 too dark. Several brightness experiments exposed
that the standalone build had stripped a shader referenced only through
`Shader.Find`. Final accepted presentation:

- both models: `Doom/ExperimentalPickupUnlit`, loaded explicitly from Resources;
- original GLB albedo, no gamma rewrite;
- exposure `1.0`;
- MEDIA0: four-cross UV emission mask with smooth pulse; body stays constant;
- BON1A0: HDR emission `0.65` plus a bounded green sticky light
  (intensity `0.75`, range `64` DOOM units, low pool importance, no shadow).

Interactive standalone verdict: both objects have acceptable scale and
brightness; glowing flasks and the blinking white medikit cross preserve the
important original visual cues. Final user verdict: **«результат отличный»**.
This is not yet a performance acceptance: the raw meshes are deliberately
retained for visual evaluation and require a later decimation gate before
production rollout.

## Six weapon rollout — SUCCESS

The shape-hint TRELLIS.2 exports were converted to textured OBJ Resources
without simplification and routed through the same presentation-only component:

- 2001 shotgun → `SHOTA0` (94,029 triangles);
- 2002 chaingun → `MGUNA0` (98,400 triangles);
- 2003 rocket launcher → `LAUNA0` (95,396 triangles);
- 2004 plasma rifle → `PLASA0` (99,125 triangles);
- 2005 chainsaw → `CSAWA0` (99,198 triangles);
- 2006 BFG9000 → `BFUGA0` (96,578 triangles).

All six use `Doom/ExperimentalPickupUnlit` at exposure `1.0`, with no added
weapon emission or sticky lights. Enhanced shows the model; Classic restores
the existing EdgeMix billboard. Gameplay, collision, collection, inventory,
death-drop and save identity remain on the unchanged pickup root.

Automated baseline on Unity `6000.4.8f1`:

- model load/normalization/mode-swap PlayMode test: 1/1 passed;
- `WeaponPlayTests`: 7/7 passed;
- graphics hot-switch PlayMode test: 1/1 passed;
- full EditMode suite: 617/617 passed;
- full PlayMode suite: 155/155 passed;
- Windows standalone build: SUCCESS, 166,385,591 bytes.

Interactive standalone gate: SUCCESS. All six models were accepted for scale,
orientation and brightness; pickup removal and Classic fallback remained
correct, with no serious frame-rate regression observed. Raw meshes still
remain an explicit decimation/performance debt before a wider production
rollout.

## COLUA0 floor lamp — SUCCESS

`COLUA0_2026-08-06T235903.211.glb` (shape-hint export) converted to textured
OBJ Resources without simplification (98,283 triangles) and allowlisted as
doomednum `2028`. Same presentation component as pickups:

- `Doom/ExperimentalPickupUnlit` at exposure `1.0`;
- no model-side emission (existing `EnhancedEmissionTable` sticky lamp light
  remains the warm floor pool);
- Enhanced shows the mesh; Classic restores the billboard;
- collision / solidity stay on the original thing root.

`ThingSpawner` now attaches experimental models for any allowlisted doomednum
(not only `ItemRules` pickups), so decorations can share the same path.

Targeted model PlayMode test passed (height 48 DU → 1.5 m). Interactive
verdict: **«результат отличный»**. Windows standalone build SUCCESS
(171,514,599 bytes).

## BAR1A0 exploding barrel — SUCCESS

`BAR1A0_2026-08-07T001149.817.glb` (shape-hint export) converted to textured
OBJ Resources without simplification (96,144 triangles) and allowlisted as
doomednum `2035`. Same presentation component:

- `Doom/ExperimentalPickupUnlit` at exposure `1.0`;
- no model-side emission;
- Enhanced shows the intact mesh; Classic restores the billboard;
- collision / HP / splash / DSBAREXP stay on the original thing root;
- on lethal hit `BarrelExplosion` calls `ExperimentalPickupModel.RevertToBillboard()`
  so the BEXP A–E sequence plays on the billboard (3D mesh stays hidden).

Targeted PlayMode: model load/scale/swap + explode revert + pistol explode
passed (3/3). Windows standalone build SUCCESS (176,774,743 bytes).
Interactive verdict: **«результат отличный»**.

## Monster and tree conditioning expansion — 2026-08-07

Prepared native 512×512 TRELLIS inputs for the five unique E1 monster
presentations: `POSSA1`, `SPOSA1`, `TROOA1`, `SARGA1` (shared by demon and
spectre), and `BOSSA1`. Each has a front depth shape-hint and a separate
front-right 45-degree variant. These are source-conditioning artifacts only;
no monster GLB or runtime routing has passed a gate yet.

Tree experiments used existing `TRE2A0` (dead tree) and `SMITA0` (leafy tree)
front hints. Simply redrawing either tree at 45 degrees did not reliably
communicate depth: TRELLIS still placed the silhouette in an almost planar
mesh. Comparison with TRELLIS's successful built-in palm example showed that
angle alone is not the useful signal. The important evidence is radial roots,
branches distributed around the trunk circumference, strong foreshortening,
front/rear overlap, exposed attachment points, layered foliage at different
depths, and coherent natural shading.

Front-facing v2 hints therefore prioritize explicit 3D topology rather than a
nominal camera rotation:

- `TRE2A0-depth-shapehint-v2.png` — **TRELLIS GEOMETRY SUCCESS**, then
  **LIVE RUNTIME SUCCESS**. Converted to Resources OBJ
  (`Assets/Resources/ExperimentalPickups/TRE2A0/`, 98,602 triangles) and
  allowlisted as doomednum `54`.
- initial `SMITA0` v2 — **REJECT**, still reconstructed as a flat card;
- replacement `SMITA0-depth-shapehint-v2.png` — **CONDITIONAL SUCCESS** on
  geometry, then **LIVE RUNTIME SUCCESS** after WAD palette albedo correction.
  Converted to Resources OBJ (`Assets/Resources/ExperimentalPickups/SMITA0/`,
  94,945 triangles) and allowlisted as doomednum `47`.

## TRE2A0 / SMITA0 tree runtime — SUCCESS 2026-08-07

`TRE2A0_2026-08-07T133155.814.glb` and `SMITA0_2026-08-07T133844.641.glb`
converted to textured OBJ Resources without simplification and allowlisted:

- 54 TRE2 → `TRE2A0` (98,602 triangles, height 64 DU → 2.0 m);
- 47 SMIT → `SMITA0` (94,945 triangles, height 64 DU → 2.0 m).

Same presentation component as decorations/pickups:

- `Doom/ExperimentalPickupUnlit` at exposure `1.0`;
- albedo remapped toward native WAD trunk/leaf palette;
- SectorFog globals applied so distant trees fade with world fog;
- no model-side emission;
- Enhanced shows the mesh; Classic restores the billboard;
- collision / solidity stay on the original thing root.

Interactive standalone verdict: **SUCCESS**.

## AMMOA0 / CELLA0 / CELPA0 / ROCKA0 / SHELA0 ammo runtime — SUCCESS 2026-08-09

Latest TRELLIS.2 GLBs converted to textured OBJ Resources (no decimation) and
allowlisted alongside display-grade redraws in `EnhancedSprites/`:

| Lump | DoomedNum | Mesh Resources | Redraw |
|------|-----------|----------------|--------|
| SHELA0 | 2008 | `ExperimentalPickups/SHELA0/` | yes |
| ROCKA0 | 2010 | `ExperimentalPickups/ROCKA0/` | yes |
| CELLA0 | 2047 | `ExperimentalPickups/CELLA0/` | yes |
| AMMOA0 | 2048 | `ExperimentalPickups/AMMOA0/` | yes |
| CELPA0 | 17 | `ExperimentalPickups/CELPA0/` | yes |

Same cascade as CLIPA0/SBOXA0: Enhanced + 3D On → mesh; Enhanced + 3D Off →
display-redraw; Classic stays native. Interactive standalone verdict:
**SUCCESS**.

## Current conclusion

TRELLIS.2 уже доказал ценность для явно коробчатых pickups. Для неоднозначных
single-view sprites принят двухступенчатый workflow: native WAD sprite служит
источником идентичности, затем отдельный shape-hint добавляет только понятные
depth cues для image-to-3D conditioning. Универсальное автоматическое решение
не доказано, но этот контролируемый per-item цикл достаточно быстрый для
продолжения pickup-набора.

## Accepted production workflow

1. Декодировать native WAD sprite и сохранить его силуэт, палитру и ключевую
   символику.
2. Подготовить shape-hint: добавить изгибы, толщину, radial shading и видимые
   боковые плоскости, не изобретая новый предмет.
3. Сгенерировать TRELLIS.2 mesh; для сравнений отключить Randomize Seed и
   сохранить seed.
4. Провести live Enhanced gate по масштабу, яркости, читаемости и стоимости
   mesh. Classic продолжает использовать оригинальный billboard.
5. Сохранить conditioning image и исходный GLB в Git до Unity-конвертации.

Shape-hint нужен только для geometry conditioning; generated PBR texture не
считается автоматически финальным WAD-derived материалом.

Не использовать снова EdgeMix/Super-xBR как TRELLIS conditioning без нового
Gate 0: чистый upscale уже показал, что не сообщает модели форму.

## Versioned source artifacts

Shape-hints:

- `Textures/Trellis2/ShapeHints/ARM1A0-depth-shapehint.png`
- `Textures/Trellis2/ShapeHints/STIMA0-cylinder-shapehint-v3.png`
- `Textures/Trellis2/ShapeHints/COLUA0-depth-shapehint.png`
- `Textures/Trellis2/ShapeHints/BAR1A0-depth-shapehint.png`
- `Textures/Trellis2/ShapeHints/TRE2A0-depth-shapehint-v2.png`
- `Textures/Trellis2/ShapeHints/SMITA0-depth-shapehint-v2.png`
- `Textures/Trellis2/ShapeHints/AMMOA0-depth-shapehint.png` (+ v2 conditioning)
- `Textures/Trellis2/ShapeHints/CELLA0-depth-shapehint.png` (+ v2 conditioning)
- `Textures/Trellis2/ShapeHints/CELPA0-depth-shapehint.png` (+ v2 conditioning)
- `Textures/Trellis2/ShapeHints/ROCKA0-depth-shapehint.png` (+ v2 conditioning)
- `Textures/Trellis2/ShapeHints/SHELA0-depth-shapehint.png` (+ v2 conditioning)
- monster front/45-degree sets under `Textures/Trellis2/ShapeHints/`

Original TRELLIS.2 exports:

- `Textures/Trellis2/GLB/MEDIA0_2026-08-06T185331.895.glb`
- `Textures/Trellis2/GLB/BON1A0_2026-08-06T185552.641.glb`
- `Textures/Trellis2/GLB/ARM1A0_2026-08-06T194747.841.glb`
- `Textures/Trellis2/GLB/ARM1A0_2026-08-06T200929.245.glb`
- `Textures/Trellis2/GLB/CSAWA0_2026-08-07T003844.479.glb`
- `Textures/Trellis2/GLB/SHOTA0_2026-08-07T084956.750.glb`
- `Textures/Trellis2/GLB/MGUNA0_2026-08-07T003205.078.glb`
- `Textures/Trellis2/GLB/LAUNA0_2026-08-07T003640.657.glb`
- `Textures/Trellis2/GLB/PLASA0_2026-08-07T003038.870.glb`
- `Textures/Trellis2/GLB/BFUGA0_2026-08-07T004316.500.glb`
- `Textures/Trellis2/GLB/COLUA0_2026-08-06T235903.211.glb`
- `Textures/Trellis2/GLB/BAR1A0_2026-08-07T001149.817.glb`
- `Textures/Trellis2/GLB/TRE2A0_2026-08-07T133155.814.glb`
- `Textures/Trellis2/GLB/SMITA0_2026-08-07T133844.641.glb`
- `Textures/Trellis2/GLB/CELLA0_2026-08-09T163603.176.glb`
- `Textures/Trellis2/GLB/CELPA0_2026-08-09T163826.095.glb`
- `Textures/Trellis2/GLB/SHELA0_2026-08-09T164327.179.glb`
- `Textures/Trellis2/GLB/ROCKA0_2026-08-09T165252.623.glb`
- `Textures/Trellis2/GLB/AMMOA0_2026-08-09T173214.969.glb`
