# Controlled Texture Mipmaps — дизайн

**Дата:** 2026-07-12  
**Статус:** automated green; moving-camera interactive sign-off pending  
**Контекст:** Scale2x 2× pipeline работает, но сам по себе не прошёл visual
gate. Эта доработка сохраняет его как резкий Enhanced LOD0 и улучшает
стабильность world textures вдали и под острым углом без общего bilinear blur.

## Цель

- Вблизи Enhanced wall/flat/fluid albedo выглядит как текущий Scale2x Point.
- При минификации используются palette-aware mipmaps и anisotropic filtering.
- Classic возвращает тот же native Point object без mipmaps.
- WAD `PLAYPAL` остаётся единственным источником цветов mip-уровней.
- Hot-switch, animation frames, normals и lifetime остаются согласованными.

Это не добавляет деталей в исходный WAD и не заменяет отклонённый Scale2x
visual gate. Доработка устраняет shimmer/moiré на дистанции, не «лечит»
блочность текстуры вплотную.

## Объём

### LOD0

Enhanced сохраняет `WorldTextureVariant.Enhanced2X`: LOD0 строится текущим
alpha-aware Scale2x и загружается без изменения. Classic по-прежнему использует
отдельный native RGBA32 `Texture2D`, `FilterMode.Point`, `mipmapCount == 1`.

### Palette-aware mip chain

Pure-C# `PaletteMipGenerator` получает LOD0 `DecodedImage`, `Palette`,
`PixelWrapMode` и alpha policy. Каждый следующий уровень:

1. имеет Unity-совместимый размер `max(1, previousDimension >> 1)`;
2. усредняет соответствующий footprint в linear-light с alpha weighting;
3. квантует непрозрачный RGB к ближайшему `PLAYPAL` цвету;
4. при равном расстоянии выбирает меньший palette index;
5. канонизирует полностью прозрачный texel в RGBA `(0,0,0,0)`;
6. не мутирует предыдущий уровень.

Расстояние до палитры — детерминированное perceptual weighted RGB в linear
space. Таблица 256 linear palette colors строится один раз на chain.

Wrap policy:

- flat/fluid: `RepeatXY`;
- wall/cutout: `RepeatX` и clamp Y;
- placeholder: `Clamp`.

Для alpha-cutout mip alpha хранит усреднённое coverage. На каждом уровне
применяется детерминированная alpha rescale, сохраняющая долю texels выше
cutoff 0.5 максимально близко к предыдущему уровню. Скрытый RGB прозрачных
pixels не участвует в усреднении.

### Sampling

Enhanced mip-enabled textures имеют `FilterMode.Trilinear` и bounded
`anisoLevel`. Shader использует два пути:

- footprint не превышает примерно один source texel: nearest texel center,
  forced LOD0;
- footprint больше порога: обычный derivative-selected trilinear/anisotropic
  sample.

Между путями допускается узкая transition band, но близкий capture обязан
совпадать с текущим Scale2x Point. Общий bilinear sampling LOD0 запрещён.

Opaque, cutout forward/depth/shadow и обе fluid frames используют одну policy.
`SKY1` может разделять тот же cached mip object с world material, но sky shader
использует независимый forced Point LOD0 sampler. Sprites, HUD, menus и weapon
view в эту доработку не входят.

### Normal maps

Enhanced normal chain имеет те же dimensions и число уровней, что albedo.
Каждый normal mip строится из соответствующего albedo mip, либо downsample
выполняется vector-average + renormalization. Normal maps остаются linear.
Несогласованный albedo/normal LOD запрещён.

## Архитектура

- `Doom.Graphics`
  - `PaletteMipGenerator`;
  - immutable/read-only представление mip chain;
  - pure fixtures без Unity.
- `TextureCache`
  - создаёт Enhanced texture с `mipChain: true`;
  - явно загружает каждый уровень;
  - вызывает `Apply(updateMipmaps: false, makeNoLongerReadable: true)`;
  - освобождает CPU mip buffers после albedo/normal upload.
- `GraphicsProfile`
  - `ControlledWorldMipmaps=false` для Classic;
  - `ControlledWorldMipmaps=true` для Enhanced;
  - capability failure деградирует только sampling к текущему Point path.
- Enhanced shaders
  - общий `DoomControlledSampling.hlsl`;
  - одинаковая LOD policy для albedo, alpha и fluid frames.

## Ресурсы

Полная mip-chain добавляет не более примерно 33% к payload base level.
Поскольку Enhanced LOD0 уже 2×, верхняя оценка для albedo:

```text
Enhanced chain ~= native bytes × 4 × 4/3
```

Такая же оценка применяется к normal chain. Обязательны lazy generation,
single object per name/variant, освобождение CPU buffers и стабильные counts
после 20 hot-switches.

## Тестирование

### EditMode

- exact dimensions до 1×1, включая odd input;
- uniform, checker, diagonal и seam fixtures;
- каждый непрозрачный output RGB принадлежит `PLAYPAL`;
- RepeatX/RepeatXY/Clamp дают ожидаемые границы;
- transparent hidden RGB не создаёт fringe;
- masked coverage сохраняется в заданном допуске;
- determinism, input immutability, invalid input;
- Freedoom wall/flat integration.

### PlayMode

- Classic: native, Point, один mip;
- Enhanced: Scale2x dimensions, полная chain, Trilinear, bounded anisotropy;
- albedo/normal mip parity;
- fluid frames имеют одинаковую chain;
- cutout forward/depth/shadow не расходятся;
- Classic → Enhanced → Classic восстанавливает exact native object;
- 20 switches и scene teardown не меняют resource counts;
- capability fallback оставляет рабочий Point Enhanced.

### Visual gate

Одинаковые poses E1M1/E1M3/E1M7:

- вплотную: совпадение с текущим Scale2x Point;
- средняя дистанция: нет общего «мыла»;
- острый угол: меньше shimmer/moiré на walls и flats;
- нет palette drift, seams, alpha fringe и исчезновения решёток;
- animated fluids не меняют резкость между frames.

## Не входит

- xBRZ/GPL code, neural upscale и replacement assets;
- изменение Scale2x LOD0;
- mip sampling для Classic, sprites, HUD/menu/intermission/weapon view и sky;
- новый Options slider;
- gameplay, UV, geometry, save schema.

## Критерий готовности

1. Classic визуально и по object identity не изменён.
2. Enhanced LOD0 остаётся резким Scale2x Point.
3. Дальние/наклонные поверхности используют controlled mips + anisotropy.
4. Mip colors принадлежат PLAYPAL; masked coverage и seams сохранены.
5. Albedo/normals/fluids согласованы по mip count и dimensions.
6. Full suites, E1 smoke и Windows build зелёные.
7. Interactive captures подтверждают меньше shimmer без общего blur.
8. Измерения memory/load/switch записаны до закрытия.

## Связанные документы

- План: `docs/superpowers/plans/2026-07-12-controlled-texture-mipmaps.md`
- Scale2x design:
  `docs/superpowers/specs/2026-07-12-enhanced-texture-upscaling-design.md`
- Stage 8 design:
  `docs/superpowers/specs/2026-07-11-enhanced-graphics-design.md`
