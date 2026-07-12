# Enhanced Texture Upscaling — дизайн

**Дата:** 2026-07-12  
**Статус:** предложено  
**Предыдущий этап:** Stage 8 Classic/Enhanced graphics завершён 2026-07-12  
**Контекст:** пост-Stage 8 улучшение читаемости низкоразрешённых WAD wall
textures и flats в Enhanced mode.

## Цель

Добавить в Enhanced mode детерминированный runtime 2× upscale текстур мира,
который:

- делает диагонали и контуры WAD pixel art менее ступенчатыми;
- сохраняет палитровый характер изображения без bilinear blur;
- не добавляет replacement assets в проект или build;
- не меняет Classic mode, UV, геометрию, gameplay или savegame;
- поддерживает переключение Classic ↔ Enhanced без reload карты;
- не декодирует WAD повторно и не создаёт новые ресурсы при каждом
  переключении.

Это не восстановление отсутствующих деталей и не HD texture pack. Алгоритм
получает только RGBA pixels, декодированные из WAD, и синтезирует более плотную
сетку texels для устойчивого nearest-neighbour sampling.

## Объём доработки

### 1. 2× edge-aware upscale для world albedo

- `Doom.Graphics` получает pure-C# `PixelArtUpscaler`.
- Первая версия реализует только фиксированный scale factor 2.
- Базовый алгоритм — Scale2x/EPX с alpha-aware сравнением соседей.
- Для каждого входного пикселя строится блок 2×2. Диагональные границы
  продолжаются только при однозначном совпадении ортогональных соседей;
  неоднозначные участки сохраняют цвет центрального пикселя.
- Сравнение цветов выполняется по точному RGBA. Полностью прозрачные pixels
  считаются равными независимо от RGB, чтобы скрытый цвет patch не создавал
  fringe.
- Результат имеет `Width * 2`, `Height * 2`, RGBA32 и ту же top-to-bottom
  ориентацию, что `DecodedImage`.
- Wrap policy задаётся явно:
  - flats используют wrap по X/Y;
  - wall textures используют wrap по X и clamp по Y;
  - placeholder использует clamp.

Scale2x выбран для первой версии вместо полного xBRZ: он компактен,
детерминирован, не требует новой third-party зависимости и имеет достаточный
эффект на 64×64/64×128 palette art. Архитектура transform API не должна
препятствовать последующей замене на xBRZ 2× после отдельного visual/performance
сравнения.

### 2. Раздельные native и Enhanced варианты

Один `Texture2D` нельзя безопасно превратить из native в 2× на месте. Поэтому
`TextureCache` хранит для каждого имени:

- один decoded native `DecodedImage`;
- native albedo `Texture2D` для Classic;
- lazy 2× `DecodedImage` и 2× albedo `Texture2D` для Enhanced;
- lazy normal map, построенную из Enhanced 2× изображения;
- surface category и metadata, общие для обоих вариантов.

Classic material всегда получает native texture. Enhanced material получает
2× texture. Возврат в Classic восстанавливает исходный объект texture, а не
пытается имитировать native вид фильтрацией увеличенного изображения.

Ключ кэша включает texture name и presentation variant. Повторные
Classic ↔ Enhanced switches переиспользуют те же объекты.

### 3. Sampling policy

- Classic world albedo остаётся `FilterMode.Point`, без изменения текущего
  вида.
- Enhanced 2× world albedo также использует `FilterMode.Point`.
- Normal maps остаются `FilterMode.Bilinear`.
- UV и texture scale материалов не меняются: увеличенная texture покрывает ту
  же world surface.
- Mipmaps не входят в первую версию. Их генерация отдельным стандартным
  box-filter может снова размыть palette art и требует отдельной custom mip
  policy.
- Существующий `BilinearWorldFiltering` не используется как признак upscale.
  Upscale является отдельной presentation capability.

### 4. Hot-switch и orchestration

`TextureCache` становится участником применения render profile:

1. При первом запросе texture WAD декодируется один раз, создаётся native
   variant.
2. Если карта загружается при активном Enhanced, 2× variant создаётся до
   первого показа соответствующего материала.
3. При первом Classic → Enhanced switch недостающие 2× variants создаются из
   уже сохранённых `DecodedImage`.
4. `WorldRenderContext.ApplyProfile` сначала просит cache подготовить variant,
   затем меняет material textures/shaders и только после этого обновляет
   animated surfaces и sky.
5. При ошибке upscale конкретной texture используется её native variant;
   Enhanced mode и остальные textures продолжают работать.

Применение profile должно быть транзакционным на уровне каждой material:
material не должен ссылаться на уничтоженный или частично загруженный texture.
Rollback в `GraphicsModeController` возвращает native variants вместе с Classic
shaders.

Первое включение Enhanced может синхронно прогреть используемые 2× variants.
До реализации фонового main-thread upload не вводится обещание «без единого
кадрового hitch». Время первого switch измеряется и фиксируется; повторный
switch обязан быть allocation-stable.

### 5. Animated surfaces, fluids и sky

- Все frames одной animation sequence используют один presentation variant.
- `AnimatedSurfaceSystem` не сохраняет навсегда массив native textures.
  При `ApplyProfile` он разрешает frames через `TextureCache` для целевого
  variant и обновляет `_MainTex`/`_MainTexB`.
- Возврат в Classic очищает property blocks и восстанавливает native main
  texture.
- Fluid cross-fade не смешивает native и 2× frame.
- `WadSkyRenderer` получает Enhanced 2× `SKY1`, но sky geometry и UV не
  меняются.
- Missing animation frame и magenta placeholder следуют существующему fallback.

### 6. Procedural normal maps

- Enhanced normal map генерируется из 2× albedo source, а не увеличивается из
  native normal.
- Surface category, strength, roughness, emission и wrap semantics остаются
  прежними.
- Normal cache key соответствует Enhanced albedo variant.
- Для одного texture/profile key normal создаётся не более одного раза.

### 7. UI и пользовательская настройка

Upscale включён всегда внутри Enhanced profile. Новая строка Options и новая
settings schema не добавляются:

- `Classic` означает authoritative native WAD pixels;
- `Enhanced` означает существующий Stage 8 profile плюс 2× world upscale;
- Apply/Cancel продолжают работать через существующий `GraphicsMode`.

Отдельный selector `Texture Quality` имеет смысл только после появления
нескольких реально поддерживаемых алгоритмов или scale factors. Сейчас он
создал бы настройку с одним полезным Enhanced значением.

## Сознательно не входит

- Upscale sprites, HUD, menus, intermission и weapon view.
- AI/ESRGAN, neural inference, external HD packs или baked PNG/TGA assets.
- Full xBRZ, 3×/4× factors и пользовательский quality slider.
- Bilinear/trilinear world albedo по умолчанию.
- Custom mip-chain generation.
- Изменение WAD palette, UV density, world scale или geometry.
- Изменение save schema: presentation resources не входят в savegame.
- GPU compute path; CPU implementation является обязательной и portable.

## Архитектура

### `Doom.Graphics`

- `PixelArtUpscaler.Scale2X(DecodedImage source, PixelWrapMode wrap)` —
  deterministic pure transform.
- `PixelWrapMode` — explicit flags/policy без зависимости от Unity.
- Transform валидирует dimensions и RGBA length, не изменяет входной массив.
- Для checked overflow или невозможного размера выбрасывается понятное
  исключение до allocation.

### `Doom.MapBuild.TextureCache`

- Вводится явный variant API, например:
  - `GetTexture(name, WorldTextureVariant.Native)`;
  - `GetTexture(name, WorldTextureVariant.Enhanced2X)`;
  - `GetTextureForProfile(name, profile)`.
- Старый `GetTexture(name)` может остаться thin wrapper для активного profile,
  но новые render systems не должны полагаться на неявный режим при pre-warm.
- Cache регистрирует и освобождает оба `Texture2D` через
  `WorldRenderContext`.
- Mapping `albedo Texture2D → texture name/variant` позволяет material factory
  выбрать правильную normal map.
- `EnhancedVariantCount` и приблизительный `EnhancedTextureBytes` доступны для
  diagnostics/tests без включения в gameplay API.

### `DoomMaterialFactory` и `WorldRenderContext`

- Material registration хранит logical texture name или provider, а не только
  текущий `Texture2D`.
- При profile switch factory устанавливает целевой `mainTexture`, затем
  настраивает shader properties и normal.
- `WorldRenderContext` владеет ссылкой на map `TextureCache` либо получает
  texture-variant callback при регистрации.
- Dispose уничтожает каждый runtime object ровно один раз.

### Поток данных

```text
WAD texture/flat
      |
      v
DecodedImage (native, cached once)
      |                         |
      | Classic                 | Enhanced, lazy
      v                         v
Native Texture2D        PixelArtUpscaler.Scale2X
                                |
                                +--> Enhanced 2x Texture2D
                                |
                                +--> NormalMapGenerator
                                         |
                                         v
                                  Enhanced 2x normal

GraphicsProfile switch
      |
      +--> material mainTexture variant
      +--> animated frame variants
      +--> sky variant
      +--> existing shader/post/effect policy
```

## Ресурсы и производительность

При RGBA32 один 2× albedo занимает в четыре раза больше памяти исходного:

```text
native bytes = width × height × 4
enhanced bytes = width × 2 × height × 2 × 4 = native × 4
```

2× normal добавляет ещё `native × 4`. Одновременное хранение native albedo,
Enhanced albedo и Enhanced normal даёт до `native × 9` GPU-side RGBA payload
для textures, которым нужна normal, без учёта Unity overhead и CPU
`DecodedImage`.

Поэтому обязательны:

- lazy creation только для реально запрошенных world textures;
- отсутствие upscale для sprites/HUD;
- один объект на name/variant;
- `makeNoLongerReadable: true` для загруженных GPU textures после завершения
  CPU transform, если ни один runtime path не читает `Texture2D` pixels;
- освобождение upscaled `DecodedImage` после normal generation, если повторное
  CPU использование не требуется;
- измерение peak managed memory, texture memory, map load и первого switch на
  E1M1 и E1M7;
- отсутствие роста resource counts после 20 переключений.

Численные budgets фиксируются после baseline. Реализация не считается готовой,
если E1M7 получает неконтролируемый рост памяти или первый Enhanced switch
создаёт длительную непредсказуемую паузу.

## Тестирование

### EditMode

- Размер результата ровно 2× по каждой оси.
- Одноцветный input создаёт одноцветный 2× output.
- Горизонтальные/вертикальные линии сохраняются.
- Диагональная fixture получает ожидаемые Scale2x pixels.
- Wrap/clamp дают разные и ожидаемые border pixels.
- Transparent RGB не создаёт fringe.
- Input `DecodedImage` не мутирует.
- Invalid dimensions/RGBA length и overflow отклоняются.
- Freedoom integration: representative wall и flat успешно масштабируются,
  alpha и dimensions корректны.

### PlayMode

- Classic material использует native dimensions и Point.
- Enhanced material использует тот же logical name, 2× dimensions и Point.
- Enhanced normal dimensions совпадают с Enhanced albedo.
- Classic → Enhanced → Classic меняет texture object и восстанавливает
  исходный native object без изменения player/world state.
- 20 switches не увеличивают variant/material/texture counts после warm-up.
- Animated wall/fluid frames и `SKY1` используют единый целевой variant.
- Ошибка одного transform использует native fallback без pink shader.
- E1M1–E1M9 строятся в обоих режимах без exceptions и mixed-resolution
  animation frames.

### Capture и интерактивно

- Снять одинаковые camera poses в E1M1, E1M3 и E1M7:
  native Classic, Enhanced до изменения и Enhanced 2×.
- Проверить крупным планом brick/metal/door textures и flats под острым углом.
- Убедиться, что нет alpha fringes на masked walls.
- Проверить animated nukage/lava, switches, doors и `SKY1`.
- Возврат в Classic должен совпадать с Stage 8 Classic reference.
- В Windows standalone измерить load time, первый switch, повторный switch и
  texture/managed memory.

## Критерий готовности

1. Enhanced world wall textures, flats и sky используют deterministic runtime
   2× variants; внешние или baked texture assets отсутствуют.
2. Classic использует исходные native textures и визуально не изменён.
3. Normal maps строятся из Enhanced 2× source и совпадают по dimensions.
4. Animated sequences и fluids не смешивают native/2× frames.
5. Hot-switch не reload'ит карту, не меняет gameplay и после warm-up не
   увеличивает resource counts.
6. Ошибка отдельной texture деградирует её до native, не отключая Enhanced.
7. EditMode, PlayMode, E1 smoke и Windows build проходят; interactive captures
   подтверждают улучшение контуров без bilinear blur и alpha artifacts.
8. Измеренные memory/load/switch показатели записаны до объявления
   доработки завершённой.

## Связанные документы

- План реализации:
  `docs/superpowers/plans/2026-07-12-enhanced-texture-upscaling.md`
- Stage 8 design:
  `docs/superpowers/specs/2026-07-11-enhanced-graphics-design.md`
- Stage 8 implementation plan:
  `docs/superpowers/plans/2026-07-11-enhanced-graphics.md`
- Исходный texture pipeline:
  `docs/superpowers/specs/2026-05-30-textures-design.md`
