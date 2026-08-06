# TRELLIS.2 pickups — Gate 0 status

**Дата:** 2026-08-06  
**Статус:** LIVE TEST SUCCESS — MEDIA0 and BON1A0 accepted in-game; further
Gate 0 generation remains paused by ZeroGPU quota.  
**Runtime:** Enhanced routes medikit (2012) and health bonus (2014) to textured
TRELLIS.2 meshes; Classic keeps EdgeMix 8× billboards and pickup gameplay is unchanged.  
**Baseline:** targeted model PlayMode test passed before the final presentation
tuning; Windows standalone built and interactive brightness/scale/glow verdict
SUCCESS on 2026-08-06. Full suites intentionally not run after the final tuning.

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

Локальные Gate 0 inputs:

- `Logs/trellis2-gate0/MEDIA0-trellis.png`
- `Logs/trellis2-gate0/MEDIA0-edgemix8x-trellis.png`
- `Logs/trellis2-gate0/STIMA0-trellis.png`
- `Logs/trellis2-gate0/BON1A0-trellis.png`

`Logs/` gitignored; это локальные продолжительные artifacts, не runtime assets.

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

### ARM1A0 — PENDING

Не запущен: бесплатная квота закончилась.

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

## Current conclusion

TRELLIS.2 уже доказал ценность для явно коробчатых pickups, но native
single-view pixel art не даёт модели достаточно shape cues для цилиндрических
и округлых предметов. Универсальное автоматическое решение пока не доказано.
Платить за дополнительные runs до улучшения conditioning inputs не нужно.

## Resume point

После восстановления бесплатного доступа:

1. подготовить deterministic `STIMA0-shapehint.png`:
   сохранить native alpha silhouette и маркировку, но заменить пиксельную
   светотень гладкими цилиндрическими normals/lighting;
2. подготовить deterministic `BON1A0-shapehint.png` с гладкой светотенью тела
   вращения;
3. shape-hint изображения нужны только для geometry conditioning; generated
   PBR texture не считается финальным WAD-derived материалом;
4. выполнить fixed-seed A/B `native vs shape-hint`, максимум три seeds на lump;
5. отдельно проверить native `ARM1A0`, если останется квота;
6. только после visual SUCCESS решать вопрос GLB cleanup, WAD/PLAYPAL material
   projection и Unity runtime catalog.

Не использовать снова EdgeMix/Super-xBR как TRELLIS conditioning без нового
Gate 0: чистый upscale уже показал, что не сообщает модели форму.
