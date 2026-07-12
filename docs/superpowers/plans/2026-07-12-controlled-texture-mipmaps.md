# Controlled Texture Mipmaps — Implementation Plan

**Goal:** сохранить Scale2x 2× как резкий Enhanced LOD0 и включать
palette-aware mipmaps с anisotropic sampling только при минификации.

**Статус:** implementation/automation green (513 EditMode + 110 PlayMode,
Windows build 122.2 MB). Static close/mid/oblique captures reviewed;
moving-camera interactive shimmer sign-off pending.

**Architecture:** pure `PaletteMipGenerator` строит PLAYPAL-quantized chain.
`TextureCache` явно загружает albedo/normal mip levels. Enhanced shaders
принудительно читают nearest LOD0 вблизи и derivative-selected filtered levels
вдали. Classic использует прежний native Point object.

Спека:
`docs/superpowers/specs/2026-07-12-controlled-texture-mipmaps-design.md`.

## Инварианты

1. Classic texture object, pixels и sampling не меняются.
2. Enhanced LOD0 — текущий Scale2x output.
3. Unity не генерирует box-filter mips.
4. Каждый opaque mip texel принадлежит PLAYPAL.
5. Walls clamp Y/repeat X; flats repeat X/Y.
6. Albedo и normal имеют одинаковые mip dimensions.
7. Все animation frames используют одну mip policy.
8. Повторный switch не создаёт resources.

## Task 1: Документация и profile contract

**Files:**
- Create: design/plan pair
- Modify: `GraphicsProfile.cs`
- Modify: `GraphicsProfileTests.cs`

- [x] Зафиксировать scope, memory model и visual gate.
- [x] Добавить `ControlledWorldMipmaps`.
- [x] Закрепить Classic=false, Enhanced=true.

## Task 2: Pure palette mip generator

**Files:**
- Create: `Assets/Scripts/Graphics/PaletteMipGenerator.cs`
- Create: `Assets/Tests/EditMode/Graphics/PaletteMipGeneratorTests.cs`

- [x] Реализовать dimensions/validation и chain ownership.
- [x] Реализовать linear-light alpha-weighted downsample.
- [x] Реализовать nearest PLAYPAL quantization с stable tie-break.
- [x] Реализовать Clamp/RepeatX/RepeatXY.
- [x] Реализовать masked alpha coverage preservation.
- [x] Добавить synthetic и Freedoom tests.

## Task 3: Explicit albedo/normal mip upload

**Files:**
- Modify: `TextureCache.cs`
- Modify: `TextureUpscalePlayTests.cs`
- Modify: `EnhancedMaterialPlayTests.cs`

- [x] Создавать Enhanced textures с полной mip chain.
- [x] Загружать levels явно, `updateMipmaps:false`.
- [x] Настроить U/V wrap отдельно.
- [x] Построить согласованные normal levels.
- [x] Освободить CPU buffers после upload.
- [x] Обновить byte diagnostics и lifetime assertions.

## Task 4: Controlled shader sampling

**Files:**
- Create: `Assets/Shaders/Includes/DoomControlledSampling.hlsl`
- Modify: `DoomEnhancedWorld.shader`
- Modify: `DoomEnhancedCutout.shader`
- Modify: `DoomFluid.shader`
- Modify: `DoomMaterialFactory.cs`

- [x] Добавить nearest forced-LOD0 close path.
- [x] Добавить derivative-selected distant path и narrow transition.
- [x] Применить policy к opaque/cutout forward/depth/shadow.
- [x] Применить policy к обеим fluid frames.
- [x] Назначать Trilinear/aniso только Enhanced mip textures.

## Task 5: Verification

- [x] Запустить focused EditMode/PlayMode tests.
- [x] Проверить 20 switches и teardown.
- [x] Запустить full EditMode/PlayMode suites.
- [x] Запустить dual-profile E1M1–E1M9 smoke.
- [x] Собрать Windows standalone.
- [x] Снять E1M1/E1M3/E1M7 close/mid/oblique captures.
- [x] Записать memory/load/switch/frame measurements.
- [ ] Обновить roadmap/`CLAUDE.md` только после interactive sign-off.

## Stop conditions

- Classic capture или exact native restore изменился.
- Close Enhanced capture стал bilinear-soft.
- Masked walls теряют coverage или расходятся между passes.
- Wall/flat seams появились на mip levels.
- Resource counts растут после warm-up.
- Cost E1M7 неприемлем даже после освобождения CPU buffers.
- Новые XML/build evidence недоступны: записать `BLOCKED`, не объявлять PASS.

## Definition of Done

- [x] Pure mip fixtures и Freedoom integration зелёные.
- [x] Classic exact native Point restore.
- [x] Enhanced close LOD0 резкий, distant/oblique sampling стабильнее.
- [x] Palette, alpha, wrap, normal и animation contracts соблюдены.
- [x] Counts стабильны, full suites/build зелёные.
- [ ] Interactive visual gate и measurements записаны.
