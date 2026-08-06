# TRELLIS.2 pickups — Gate 0 status

**Дата:** 2026-08-07  
**Статус:** LIVE TEST SUCCESS — MEDIA0/BON1A0/weapons + COLUA0 floor lamp
accepted in Enhanced.  
**Runtime:** Enhanced routes medikit (2012), health bonus (2014), six weapon
pickups (2001–2006), and COLU floor lamp (2028) to textured TRELLIS.2 meshes;
Classic keeps EdgeMix 8× billboards. Pickup gameplay and lamp sticky lights
are unchanged.  
**Baseline:** MEDIA0/BON1A0 interactive verdict SUCCESS on 2026-08-06. The
six-weapon Enhanced rollout passed automation, Windows build and interactive
scale/orientation/brightness/pickup/performance sign-off on 2026-08-07.
COLUA0 interactive standalone/editor verdict SUCCESS on 2026-08-07
(«результат отличный»).

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

Original TRELLIS.2 exports:

- `Textures/Trellis2/GLB/MEDIA0_2026-08-06T185331.895.glb`
- `Textures/Trellis2/GLB/BON1A0_2026-08-06T185552.641.glb`
- `Textures/Trellis2/GLB/ARM1A0_2026-08-06T194747.841.glb`
- `Textures/Trellis2/GLB/ARM1A0_2026-08-06T200929.245.glb`
- `Textures/Trellis2/GLB/CSAWA0_2026-08-07T003844.479.glb`
- `Textures/Trellis2/GLB/SHOTA0_2026-08-07T002730.162.glb`
- `Textures/Trellis2/GLB/MGUNA0_2026-08-07T003205.078.glb`
- `Textures/Trellis2/GLB/LAUNA0_2026-08-07T003640.657.glb`
- `Textures/Trellis2/GLB/PLASA0_2026-08-07T003038.870.glb`
- `Textures/Trellis2/GLB/BFUGA0_2026-08-07T004316.500.glb`
- `Textures/Trellis2/GLB/COLUA0_2026-08-06T235903.211.glb`
