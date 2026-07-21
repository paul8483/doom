# Enhanced Texture Quality — дизайн

**Дата:** 2026-07-21
**Статус:** in progress — Task 1 done (`5315e8a`); next Task 2 `DeditherFilter`
**Предыдущий этап:** Enhanced Texture Upscaling (Scale2x 2×) — automation
green, interactive reject 2026-07-12; controlled palette-aware mipmaps
влиты в `main`.
**Контекст:** Enhanced mode выигрывает у Classic за счёт света, тумана и
сглаживания дальних поверхностей, но вблизи world textures остаются
низкокачественными. Scale2x 2× не дал видимого улучшения: он сглаживает
только диагональные лесенки, а арт Freedoom преимущественно осевой, и
вблизи проблема — огромный texel и палитровый шум, а не лесенки.

## Цель

Поднять воспринимаемое качество Enhanced world textures вблизи за счёт
четырёх взаимодополняющих слоёв, каждый из которых бьёт по своей причине
«плохо в упор»:

1. **Texel-AA sampling** — чистые края текселей при любом угле и дистанции
   (устраняет «грязь» Point и мыло bilinear).
2. **Дедизеринг/дебандинг** — превращает палитровый шахматный дизеринг и
   ступенчатые рампы в непрерывные градиенты до апскейла.
3. **Super-xBR 4×** — заменяет Scale2x как алгоритм runtime-апскейла
   (2 прохода по 2×); MIT-лицензия, в отличие от GPL-3.0 xBRZ.
4. **Multi-scale normals + parallax occlusion mapping** — рельеф на панелях,
   кирпичах и заклёпках из сгенерированных heightmap; работает там, где
   апскейл бессилен.

Инварианты предыдущей итерации сохраняются:

- никаких replacement assets в проекте или build — все данные синтезируются
  в runtime из WAD pixels;
- Classic mode, UV, геометрия, gameplay и save schema не меняются;
- Classic ↔ Enhanced hot-switch без reload карты;
- WAD не декодируется повторно при переключении;
- ошибка обработки отдельной texture деградирует её до native fallback.

Нейронный апскейл (ESRGAN/Sentis) сознательно отложен как отдельный
эксперимент после оценки слоёв 1–4.

## Почему это сработает там, где Scale2x не сработал

Scale2x был одиночной ставкой на один механизм (доводка диагоналей),
слепой к настоящим дефектам близкого плана. Здесь три независимых
механизма:

- дедизеринг чистит сам исходник — тени и градиенты перестают быть
  шахматкой ещё до апскейла, и апскейлер тратит правила на реальные
  контуры, а не на дизеринговый шум;
- Super-xBR агрессивнее в блендинге и реконструкции кривых, чем
  консервативный Scale2x, а 4× даёт в 4 раза плотнее сетку texels;
- texel-AA и POM меняют не пиксели, а *сэмплинг и освещение* — то, что
  видно именно в упор.

Критерий приёмки — интерактивный eyeball с послойными captures (см.
«Тестирование»), а не только зелёная автоматизация.

## Объём доработки

### 1. Дедизеринг/дебандинг (pure `Doom.Graphics`)

Новый детерминированный transform `DeditherFilter`, выполняемый над
decoded native `DecodedImage` **до** апскейла:

- Селективное сглаживание 3×3: сосед участвует в усреднении только если
  его цветовая дистанция до центрального пикселя ниже порога `T`.
  Дизеринговая шахматка двух близких ступеней рампы усредняется в
  промежуточный тон; граница двух соседних палитровых полос смягчается.
- Пиксели дальше порога (реальные рёбра: металл/кровь, панель/шов) не
  участвуют — контуры остаются резкими. Это отличает пасс от блюра.
- Дистанция считается во взвешенном RGB (перцептуальные веса каналов);
  порог консервативный, подбирается один раз на Freedoom fixtures и
  фиксируется константой. Никакой зависимости от конкретной раскладки
  PLAYPAL-рамп — только цветовая близость.
- Полностью прозрачные пиксели не участвуют в усреднении и не изменяются;
  RGB непрозрачных соседей не «перетекает» через границу прозрачности.
- Wrap policy та же, что у апскейла: flats — RepeatXY, walls/SKY — RepeatX,
  placeholder — Clamp; фильтр сэмплит соседей тороидально, швов на краях
  tile нет.
- Transform чистый: вход не мутирует, выход — новый `DecodedImage` тех же
  dimensions.

### 2. Super-xBR 4× вместо Scale2x

- `Doom.Graphics` получает `SuperXbrUpscaler.Scale2X(DecodedImage, PixelWrapMode)`
  — C#-порт reference-реализации Hyllian (MIT). Файл несёт MIT-заголовок с
  атрибуцией; лицензионный текст добавляется в третьи-party notices.
  Порт остаётся pure C#, deterministic, без Unity-зависимостей.
- 4× достигается двумя последовательными применениями 2×. Промежуточный
  2× буфер не кэшируется и не попадает на GPU.
- Alpha policy для masked textures: перед апскейлом RGB непрозрачных
  пикселей дилатируется в полностью прозрачные соседние texels (bleed
  guard), затем апскейлится RGBA целиком; cutout shader по-прежнему
  порогует alpha. Это предотвращает тёмные fringes на решётках.
- Wrap policy идентична Scale2x-версии (RepeatXY/RepeatX/Clamp) и
  применяется на обоих проходах.
- `PixelArtUpscaler.Scale2X` и его тесты остаются в кодовой базе как
  reference transform, но из profile mapping исключаются.
- Fallback chain per texture: ошибка Super-xBR → native variant (без
  промежуточной попытки Scale2x — меньше состояний).

Полный пайплайн Enhanced albedo:

```text
DecodedImage (native)
  -> DeditherFilter
  -> [alpha bleed guard, только masked]
  -> SuperXbr 2x
  -> SuperXbr 2x        (итого 4x)
  -> controlled palette-aware mips (существующий пайплайн)
  -> Texture2D (Enhanced4X)
```

### 3. Texel-AA sampling для Enhanced world albedo

- Enhanced world albedo переводится с `FilterMode.Point` на
  `FilterMode.Bilinear` + существующие controlled mips + aniso.
- В Enhanced world shaders (opaque и cutout) добавляется texel-AA выборка:
  UV квантуются к центрам texels с шириной перехода из `fwidth`
  (fat pixels + smoothstep). Вблизи тексели остаются «хрусткими» с
  антиалиасенными границами; вдали фильтрация вырождается в обычный
  bilinear+mips без шума.
- **Это сознательное изменение политики предыдущей спеки** («Enhanced 2×
  использует Point»): Point отвергнут вместе со Scale2x-итерацией, texel-AA
  — его замена без bilinear mush.
- Classic шейдеры и Classic albedo (`Point`, native) не меняются.
- Sprites, HUD, weapon view, menus не затрагиваются.

### 4. Multi-scale normals, heightmap и POM

- Height generation: из обработанного 4× albedo строится heightmap как
  взвешенная сумма fine luminance (детали: заклёпки, швы) и blurred coarse
  luminance (крупные формы: панели, кирпичи). Веса — константы per surface
  category (существующая классификация metal/stone/tech/…).
- `NormalMapGenerator` переходит на тот же multi-scale источник: нормали
  считаются из heightmap, а не напрямую из albedo luminance одной шкалы.
  Дедизеринг до апскейла дополнительно чистит нормали от шума, который
  сейчас генерируется из дизеринговых паттернов.
- Height пакуется в alpha-канал существующей normal texture (RGBA32
  linear) — отдельная texture не создаётся, память не растёт сверх normal.
- Enhanced lit world shader получает parallax occlusion mapping по height
  из `_BumpMap.a`: фиксированное небольшое число шагов, амплитуда —
  константа per surface category (консервативная, «рельеф», а не
  «стереограмма»).
- POM включается только для solid walls и flats. Masked/cutout поверхности,
  animated fluids и sky в первой версии POM не получают (артефакты
  силуэтов и cross-fade дороже пользы).
- Classic не имеет normals/POM — как и сейчас.

### 5. Presentation contracts и hot-switch

- `WorldTextureVariant` получает `Enhanced4X`; `Enhanced2X` удаляется из
  profile mapping (значение enum сохраняется для стабильности, помечается
  obsolete).
- `GraphicsProfile` вместо одного флага апскейла несёт декларативные
  флаги слоёв: `WorldDedither`, `WorldUpscale4X`, `WorldTexelAA`,
  `WorldParallax`. Classic — все false; Enhanced — все true. Новые
  user-facing настройки **не добавляются**: пользователь по-прежнему видит
  Classic/Enhanced.
- Флаги нужны для тестов и послойных captures: capture/test harness может
  собрать промежуточные профили (например, только дедизеринг без
  апскейла). Runtime-доступ к промежуточным профилям — editor/test-only.
- Hot-switch, транзакционность применения profile, rollback и fallback
  ведут себя как в Scale2x-итерации; меняется только содержимое Enhanced
  variant.
- Animated sequences и fluid pairs используют единый variant (native или
  Enhanced4X), sky получает Enhanced4X `SKY1`.

## Сознательно не входит

- Нейронный апскейл (ESRGAN/Sentis), дисковый кэш инференса — отдельный
  будущий эксперимент.
- xBRZ в любом виде (GPL-3.0-only несовместим с проприетарным Unity
  runtime).
- Upscale/texel-AA/POM для sprites, HUD, weapon view, menus, intermission.
- Пользовательский selector Texture Quality и новые строки Options.
- Изменение WAD palette, UV density, world scale, geometry, gameplay,
  save schema.
- GPU compute path для transforms: CPU реализация обязательна и portable.
- POM на masked/cutout, fluids и sky (первая версия).
- Self-shadowing POM и height-based blending между соседними поверхностями.

## Архитектура

### `Doom.Graphics` (pure)

- `DeditherFilter.Apply(DecodedImage, PixelWrapMode)` — селективное
  сглаживание, порог и веса — фиксированные константы.
- `SuperXbrUpscaler.Scale2X(DecodedImage, PixelWrapMode)` — MIT-порт,
  атрибуция в заголовке файла.
- `AlphaBleedGuard.Dilate(DecodedImage)` — RGB dilation в прозрачные
  texels (только для masked источников).
- `HeightMapGenerator.Generate(DecodedImage, SurfaceCategory)` — multi-scale
  luminance height; используется и для normals, и для POM alpha.
- `NormalMapGenerator` переходит на height-источник; сигнатура дополняется,
  старый путь удаляется.
- Все transforms: вход не мутирует, dimensions/RGBA валидируются, overflow
  отклоняется до allocation.

### `Doom.MapBuild`

- `TextureCache`: entry-пайплайн Enhanced4X (dedither → [bleed] → superxbr
  ×2 ×2 → mips), кэш по `(name, variant)`, per-texture fallback state,
  diagnostics (variant count, estimated bytes) — расширение существующего
  variant API, а не новая система.
- `DoomMaterialFactory`: назначение Bilinear+texel-AA параметров Enhanced
  albedo, normal+height привязка, POM keyword только для solid
  opaque/flat материалов.
- Shaders: Enhanced opaque/cutout получают texel-AA функцию выборки;
  Enhanced lit opaque получает POM-ветку (multi_compile/feature, чтобы
  masked вариант не платил за неиспользуемый код).
- `WorldRenderContext`/`GraphicsModeController`/`AnimatedSurfaceSystem`/
  `WadSkyRenderer` — без структурных изменений, целевой variant меняется
  на Enhanced4X.

### Поток данных

```text
WAD texture/flat
      |
      v
DecodedImage (native, cached once)
      |                       |
      | Classic               | Enhanced, lazy
      v                       v
Native Texture2D       DeditherFilter
                              |
                       [AlphaBleedGuard]  (masked)
                              |
                       SuperXbr 2x -> SuperXbr 2x
                              |
              +---------------+----------------+
              v                                v
   Enhanced 4x albedo Texture2D      HeightMapGenerator
   (Bilinear + controlled mips,               |
    texel-AA в shader)                        v
                                     NormalMapGenerator
                                              |
                                              v
                                Enhanced normal RGBA (height в alpha)
                                     -> POM в Enhanced lit shader
```

## Ресурсы и производительность

4× albedo — это **16× байт** native (против 4× у Scale2x-итерации):

```text
native bytes   = W × H × 4
enhanced 4x    = W×4 × H×4 × 4 = native × 16
normal+height  = native × 16
итого GPU payload ≈ native × 33 (native albedo + 4x albedo + 4x normal)
+ mip chain ≈ +33%
```

Для типичной E1-карты (~100 world textures 128×128) это порядка сотен МБ
GPU-памяти в Enhanced. Поэтому обязательны:

- lazy creation только реально запрошенных world textures;
- `makeNoLongerReadable` после upload; освобождение промежуточных CPU
  буферов (dedithered, 2×, 4×, height) сразу после последнего consumer;
- один объект на `(name, variant)`; повторные switches не создают объектов;
- измерение на E1M1 и E1M7: map load, первый/повторный switch, texture и
  managed memory, frame time;
- стабильность counts после 20 switches и scene reload.

Заранее объявленный mitigation ladder, если E1M7 неприемлем (применять по
одной ступени, с измерением):

1. height/normal генерируются из 2× промежуточного результата вместо 4×
   (визуально нормали менее требовательны к разрешению);
2. albedo 4× только для textures ≥ 64px по меньшей стороне, остальные 2×;
3. общий откат Enhanced на 2× Super-xBR (сохранив дедизеринг/texel-AA/POM).

CPU-стоимость transforms (dedither + 2×superxbr + height) выше Scale2x;
первый Enhanced switch может синхронно прогреваться, время фиксируется.
Обещание «без единого кадрового hitch» не вводится, как и раньше.

## Тестирование

### EditMode (pure fixtures)

- `DeditherFilter`: шахматка двух близких цветов → однородный средний тон;
  контрастное ребро не размывается; прозрачность не участвует и не
  протекает; wrap/clamp дают ожидаемые border-результаты; вход не мутирует.
- `SuperXbrUpscaler`: dimensions ровно 2× за проход; uniform input →
  uniform output; известная diagonal fixture → устойчивый snapshot
  (закреплённый golden-массив); двойное применение даёт 4×; invalid
  input/overflow отклоняются; RepeatX/RepeatXY швов не создают.
- `AlphaBleedGuard`: RGB дилатируется только в полностью прозрачные texels,
  alpha не меняется.
- `HeightMapGenerator`: плоский input → плоская height; ступень яркости →
  градиент height; дизеринговая шахматка после dedither даёт почти плоскую
  height (регрессия «нормали из шума»).
- Freedoom integration: representative wall, flat, masked texture и `SKY1`
  проходят полный пайплайн без исключений, dimensions/alpha корректны.

### PlayMode

- Enhanced material: 4× dimensions, Bilinear, mips; Classic: native, Point
  (без изменений против Stage 8).
- Normal texture dimensions совпадают с Enhanced albedo; alpha-канал
  содержит height (не константу) для solid материалов.
- POM keyword включён только на solid opaque/flat материалах; masked,
  fluids и sky — без POM.
- Classic → Enhanced → Classic: exact native restore, world/player state
  не меняются; 20 switches не растят counts.
- Animated/fluid pairs и `SKY1` — единый variant, без mixed dimensions.
- Ошибка одного transform → native fallback без pink materials.
- E1M1–E1M9 smoke в обоих режимах.

### Capture и интерактивно

Ключевое отличие от Scale2x-раунда — **послойный** протокол на одинаковых
camera poses (E1M1, E1M3, E1M7; brick/metal/door/flats в упор и под острым
углом):

1. Classic native (reference);
2. Enhanced только dedither (без апскейла);
3. Enhanced dedither + Super-xBR 4× (Point, без texel-AA);
4. - texel-AA (полный albedo-стек);
5. - multi-scale normals + POM (полный стек).

Оценка каждого слоя отдельно: что даёт видимый вклад, что нет, что портит
стиль. Дополнительно: masked walls без fringes, animated nukage/lava без
mixed frames, sky, возврат в Classic побитово совпадает с reference,
Windows standalone — время switches и память.

## Критерий готовности

1. Enhanced world albedo проходит пайплайн dedither → Super-xBR 4× →
   controlled mips; внешние/baked assets отсутствуют; Classic не изменён.
2. Texel-AA работает в Enhanced opaque/cutout; вблизи нет ни грязи Point,
   ни bilinear mush.
3. Normals строятся multi-scale из height; POM активен на solid
   поверхностях с консервативной амплитудой.
4. Hot-switch/rollback/fallback сохраняют инварианты Scale2x-итерации.
5. EditMode, PlayMode, E1 smoke и Windows build зелёные; totals записаны.
6. Memory/load/switch числа E1M1/E1M7 записаны; при превышении — применён
   и задокументирован mitigation ladder.
7. **Visual gate:** послойные captures + интерактивный eyeball подтверждают
   значимое улучшение близкого плана без потери пиксель-арт стиля.
   Если полный стек его не даёт — доработка не закрывается как успех, и
   фиксируется вывод для решения о нейроапскейл-эксперименте.

## Связанные документы

- План реализации:
  `docs/superpowers/plans/2026-07-21-enhanced-texture-quality.md`
- Предыдущая итерация (Scale2x, отклонена визуально):
  `docs/superpowers/specs/2026-07-12-enhanced-texture-upscaling-design.md`
- Controlled mipmaps:
  `docs/superpowers/specs/2026-07-12-controlled-texture-mipmaps-design.md`
- Stage 8 design:
  `docs/superpowers/specs/2026-07-11-enhanced-graphics-design.md`
- Baseline notes: `Logs/enhanced-texture-upscale-baseline-notes.md`
