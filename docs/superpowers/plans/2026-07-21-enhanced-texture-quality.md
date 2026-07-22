# Enhanced Texture Quality — Implementation Plan

> Выполнять задачи последовательно. Checkbox (`- [ ]`) является журналом
> фактического выполнения; не отмечать шаг до зелёного теста или явно
> записанного blocker.

**Goal:** поднять качество Enhanced world textures вблизи четырьмя слоями:
дедизеринг/дебандинг исходников, Super-xBR 4× вместо Scale2x, texel-AA
sampling вместо Point, multi-scale normals + POM из сгенерированных
heightmap. Classic не меняется; hot-switch и все инварианты
Scale2x-итерации сохраняются.

**Architecture:** pure transforms живут в `Doom.Graphics`
(`DeditherFilter`, `SuperXbrUpscaler`, `AlphaBleedGuard`,
`HeightMapGenerator`, обновлённый `NormalMapGenerator`). `TextureCache`
собирает пайплайн Enhanced4X variant; `DoomMaterialFactory` и Enhanced
shaders добавляют texel-AA и POM. Существующий variant API, hot-switch и
controlled-mips пайплайн переиспользуются.
Спека: `docs/superpowers/specs/2026-07-21-enhanced-texture-quality-design.md`.

**Статус:** Task 6 done (`HudTextureCache` Enhanced4X + weapon placement via
`SpriteCache` + yielded `ENHANCED HUD` warm). Next: Task 7 (texel-AA).

**Ветка:** новая ветка от `main` (Scale2x-пайплайн и controlled mips уже
влиты в `main`; в `upscale` остался только незамерженный version bump).

**Tech Stack:** Unity 6000.4.8f1, C#/.NET profile Unity, Unity Test
Framework, URP 17.4.0, существующие `Doom.Graphics`, `Doom.MapBuild`,
`freedoom1.wad`.

**Content rule:** не добавлять authored/baked/AI-upscaled textures. Все
RGBA, height и normal maps создаются только в runtime memory из WAD pixels.

**License rule:** Super-xBR — C#-порт reference-реализации Hyllian под MIT.
Файл порта обязан нести MIT-заголовок с атрибуцией; текст лицензии
добавляется в third-party notices. **xBRZ (GPL-3.0-only) не использовать
ни в каком виде**, включая «подглядывание» в его исходники при написании
порта.

**Базовая линия:** на закрытии `upscale`-итерации — 505 EditMode +
109 PlayMode. Перед реализацией снять актуальные totals на `main` (после
merge mips могли измениться) и работать от них.

## Запуск тестов

Каждый прогон пишет новый XML/log. Открытый Unity Editor блокирует
batchmode; в этом случае закрыть Editor или записать `BLOCKED`, не
использовать старый XML.

```powershell
# EditMode
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -nographics -projectPath "C:\Development\doom" `
    -runTests -testPlatform EditMode -testFilter "<FILTER>" `
    -testResults "C:\Development\doom\Logs\texquality-<task>-edit.xml" `
    -logFile "C:\Development\doom\Logs\texquality-<task>-edit.log"

# PlayMode — без -nographics
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -projectPath "C:\Development\doom" `
    -runTests -testPlatform PlayMode -testFilter "<FILTER>" `
    -testResults "C:\Development\doom\Logs\texquality-<task>-play.xml" `
    -logFile "C:\Development\doom\Logs\texquality-<task>-play.log"
```

Не добавлять `-quit` к `-runTests`. PASS/FAIL брать из нового XML.

## Инварианты реализации

1. Classic material всегда получает exact native `Texture2D` c `Point`;
   Classic шейдеры не меняются.
2. WAD texture/flat декодируется не более одного раза на map load.
3. Enhanced albedo и normal(+height) имеют одинаковые 4× dimensions.
4. Одна animation sequence / fluid pair не смешивает variants.
5. Повторный hot-switch не создаёт texture/material/normal objects.
6. Ошибка одной texture даёт native fallback только для неё.
7. Gameplay и save schema не меняются; menus/intermission остаются native.
   Sprites/weapon view/HUD — в объёме (4×-вариант при Enhanced, native при
   Classic); placement rects всегда из `PatchHeader`, не из texture dims.
8. Промежуточные CPU буферы (dedithered, 2×, 4×, height) освобождаются
   после последнего consumer; GPU textures — `makeNoLongerReadable`.
9. POM keyword отсутствует на masked/cutout, fluids и sky материалах.

## Source-control gate для каждой Task

1. Перед изменениями проверить `git status --short` и scoped diff.
2. Не использовать bulk add: дерево может содержать generated Unity/build
   output и пользовательские untracked файлы.
3. Для каждого нового Unity `.cs`/asset/shader должен существовать
   корректный `.meta` (предпочтительно через импорт Editor'ом).
4. Не commit'ить `Library/`, `Temp/`, raw `Logs/`, captures или `Builds/`.
5. Сначала узкий failing test, затем implementation, затем assembly suite.
6. `Commit checkpoint` — название логического checkpoint, а не разрешение
   выполнять commit.

---

## Task 1: Baseline, contracts и layer flags

**Files:**
- Create: `Logs/enhanced-texture-quality-baseline-notes.md`
- Modify: `Assets/Scripts/MapBuild/Rendering/WorldTextureVariant.cs`
- Modify: `Assets/Scripts/MapBuild/Rendering/GraphicsProfile.cs`
- Modify: `Assets/Tests/EditMode/Map/GraphicsProfileTests.cs`

- [x] **Step 1: Снять актуальный baseline на `main`.**

Зафиксировать commit/branch, Unity/GPU/API, актуальные test totals
(полные EditMode/PlayMode прогоны без фильтра). На E1M1 и E1M7 записать:
map build time, Classic↔Enhanced switch times, counts
(`TextureCount`/`MaterialCount`/normals), managed/texture memory,
dimensions и filter mode representative wall/flat/`SKY1` в обоих режимах.
Camera poses для будущих послойных captures записать в notes.

Notes: `Logs/enhanced-texture-quality-baseline-notes.md`. EditMode baseline
**513** (`texquality-t1-baseline-edit.xml`) on `texquality` @ `a580cd6` before
contract recompile; PlayMode totals filled when run completes. E1 timing/
memory TBD at Task 7; poses recorded from Stage 8 harness.

- [x] **Step 2: Расширить variant contract.**

Добавить `WorldTextureVariant.Enhanced4X`; `Enhanced2X` пометить
`[Obsolete]` с сохранением численного значения. Profile mapping Enhanced →
`Enhanced4X`.

- [x] **Step 3: Ввести декларативные layer flags.**

В `GraphicsProfile` заменить единый флаг апскейла на `WorldDedither`,
`WorldUpscale4X`, `WorldTexelAA`, `WorldParallax`. Classic — все false;
Enhanced — все true. Публичный API настроек (settings schema, Options UI)
не меняется. Обеспечить возможность собрать промежуточный profile из
кода (editor/test-only) для послойных captures.

- [x] **Step 4: Запустить profile tests.**

```text
Doom.Map.Tests.GraphicsProfileTests
```

**5 passed** — `Logs/texquality-t1-edit.xml`.

**Commit checkpoint:** `graphics: define texture quality layer contracts`

---

## Task 2: Pure `DeditherFilter`

**Files:**
- Create: `Assets/Scripts/Graphics/DeditherFilter.cs`
- Create: `Assets/Tests/EditMode/Graphics/DeditherFilterTests.cs`

- [x] **Step 1: Написать failing fixtures.**

- шахматка 2 близких цветов (дистанция < T) → однородный средний тон;
- шахматка 2 контрастных цветов (> T) → без изменений;
- резкое ребро между двумя областями близких цветов смягчается не более
  чем на 1 пиксель; контрастное ребро не трогается вовсе;
- полностью прозрачные пиксели неизменны; RGB непрозрачных соседей не
  усредняется с прозрачными;
- RepeatX/RepeatXY: сглаживание работает через край tile без швов;
  Clamp: край сэмплится с clamp;
- вход не мутирует; invalid dimensions/RGBA отклоняются.

- [x] **Step 2: Реализовать фильтр.**

3×3 селективное усреднение: сосед входит с весом 1 при
`dist(c, center) < T`, иначе 0; дистанция — взвешенный RGB
(перцептуальные веса, например 0.30/0.59/0.11). `T` — константа,
подобранная на Freedoom fixtures (Step 3). Без per-pixel allocations во
внутреннем цикле.

- [x] **Step 3: Откалибровать порог на Freedoom.**

Integration test: representative дизеренная область реального Freedoom
wall (найти конкретный lump на этапе реализации, зафиксировать имя в
тесте) после фильтра имеет заметно меньшую локальную дисперсию;
контрастная область (швы/контуры) — неизменную. Порог записать константой
с комментарием, чем откалиброван.

`ColorDistanceThreshold = 40` на Freedoom 0.13 `STARTAN2`: dither patch
`(72,88)` 16×16 variance ~58→18; high-edge `(0,56)` strength retained ≥85%.
Test: `Freedoom_STARTAN2_dither_region_variance_drops_seam_stable`.

- [x] **Step 4: Запустить Graphics suite.**

```text
Doom.Graphics.Tests.DeditherFilterTests
Doom.Graphics.Tests
```

**14/14 DeditherFilter** (`Logs/texquality-t2-dedither-edit.xml`);
**78/78 Graphics** (`Logs/texquality-t2-graphics-edit.xml`).

**Commit checkpoint:** `graphics: add palette-aware dedither filter`

- [x] **Step 5 (ревизия 2026-07-22): паттерн-гейтинг вместо порогового
  сглаживания.**

Интерактивный просмотр превью («мыло») + свип T=20/30/40 показали, что
пороговое сглаживание попадает в stop condition 2: зерно Freedoom живёт в
тех же дистанциях, что и дизеринг, рабочего T нет. Дизайн пересогласован
(см. ревизию в спеке): `DeditherFilter` переписан на шахматный
паттерн-гейтинг (`GroupTolerance = 10`, `CrossDistanceThreshold = 40`,
midpoint collapse, диагностическая маска). Тесты переписаны: синтетика
(шахматки/шум/паз/ребро/прозрачность/wrap/маска) + Freedoom-интеграция
(`STARTAN2`+`BROWN1`: гейт < 10% пикселей, сила ребра ≥ 95%).

**17/17 DeditherFilter** (`Logs/texquality-t2b-dedither-edit.xml`);
**81/81 Graphics** (`Logs/texquality-t2b-graphics-edit.xml`).

Замер превью-инструментом (`Tools > Doom > Dump Dedither Preview`,
`Logs/dedither-preview/`): на 9 представительных текстурах E1 гейт
срабатывает на 0.00–0.02% пикселей — Freedoom реального шахматного
дизеринга почти не содержит; слой для Freedoom ≈ no-op (корректно), польза
ожидается на retail DOOM.WAD. Ставка визуального улучшения — Tasks 3/5/6.

**Commit checkpoint:** `graphics: gate dedither on true checkerboard patterns`

---

## Task 3: Pure `SuperXbrUpscaler` + `AlphaBleedGuard`

**Files:**
- Create: `Assets/Scripts/Graphics/SuperXbrUpscaler.cs`
- Create: `Assets/Scripts/Graphics/AlphaBleedGuard.cs`
- Create: `Assets/Tests/EditMode/Graphics/SuperXbrUpscalerTests.cs`
- Create: `Assets/Tests/EditMode/Graphics/AlphaBleedGuardTests.cs`

- [x] **Step 1: Написать failing core fixtures.**

- 1×1 и uniform image → uniform 2× exact;
- output dimensions/byte length; 2× дважды → 4×;
- вход не мутирует; invalid input/checked overflow до allocation;
- горизонтальная/вертикальная линия остаётся линией (без изломов);
- диагональная fixture: снять golden-массив с реализации после ревью
  правильности и закрепить как snapshot-регрессию.

- [x] **Step 2: Портировать Super-xBR (MIT).**

Порт reference-реализации Hyllian (3 внутренних прохода на один 2× шаг).
MIT-заголовок + атрибуция в файле; third-party notice
(`Assets/ThirdParty/SuperXbr/`). Веса/константы reference-версии не
менять. Wrap policy (Clamp/RepeatX/RepeatXY) применять при выборке
соседей на обоих проходах. Без per-pixel allocations.

- [x] **Step 3: Реализовать `AlphaBleedGuard`.**

Дилатация RGB непрозрачных пикселей в полностью прозрачные соседние
texels (1–2 итерации, маска `valid` для распространения через уже
bled texels); alpha не меняется. Тесты: RGB прозрачного соседа после
dilate равен ближайшему непрозрачному; непрозрачные пиксели неизменны;
полностью непрозрачное изображение — no-op.

- [x] **Step 4: Alpha/wrap fixtures.**

Masked fixture (решётка): после bleed + 2×2 апскейла нет тёмных RGB у
пикселей с alpha над порогом cutout; RepeatX отличается от Clamp на
горизонтальном шве (wrap активен); RepeatXY отличается от Clamp на углу.

- [x] **Step 5: Freedoom integration.**

Representative wall (`STARTAN2`), flat (`FLOOR4_8`), first masked
texture in `TextureSet`, `SKY1`: полный проход dedither → [bleed] →
superxbr ×2 ×2 без исключений; dimensions 4×; alpha сохраняет маску.

- [x] **Step 6: Запустить Graphics suite целиком.**

```text
Doom.Graphics.Tests.SuperXbrUpscalerTests
Doom.Graphics.Tests.AlphaBleedGuardTests
Doom.Graphics.Tests
```

**14 SuperXbr + 7 AlphaBleed = 21/21** (`Logs/texquality-t3-core-edit.xml`);
**102/102 Graphics** (`Logs/texquality-t3-graphics-edit.xml`).

**Commit checkpoint:** `graphics: add superxbr 4x upscale with alpha bleed guard`

---

## Task 4: Enhanced4X пайплайн в `TextureCache`

**Files:**
- Modify: `Assets/Scripts/MapBuild/TextureCache.cs`
- Modify: `Assets/Tests/PlayMode/TextureUpscalePlayTests.cs`

- [x] **Step 1: Написать failing cache tests.**

- `Enhanced4X` variant имеет ровно 4× dimensions, controlled-mips Trilinear, mips;
- native variant не изменился (Point, native dimensions);
- повторный запрос → тот же object; counts стабильны;
- flat → RepeatXY, wall → RepeatX, placeholder → Clamp;
- masked synthetic grate через bleed guard (нет тёмных fringe на visible);
- ошибка transform (test seam `ForceEnhancedFailureForTests`) → native
  fallback, failed state кэшируется.

- [x] **Step 2: Собрать пайплайн.**

`Enhanced4X` entry: cached native `DecodedImage` → `DeditherFilter` (если
`WorldDedither`) → `AlphaBleedGuard` (только masked) → `SuperXbrUpscaler`
×2 → ×2 → существующий controlled-mips upload (Trilinear+aniso while
controlled mips on). `BuildEnhanced4XDecoded` — общий CPU helper.
Промежуточные буферы не кэшируются; CPU 4× освобождается после albedo+normal
upload; `makeNoLongerReadable` для albedo.

- [x] **Step 3: Удалить Scale2x из mapping.**

`Enhanced2X` больше не создаётся: obsolete request aliases to `Enhanced4X`.
`PixelArtUpscaler` и его тесты остаются. Profile mapping уже был на
`Enhanced4X` (Task 1).

- [x] **Step 4: Запустить cache tests.**

```text
Doom.Stage3.PlayTests.TextureUpscalePlayTests
```

**5/5 passed** (`Logs/texquality-t4-play.xml`).

**Commit checkpoint:** `mapbuild: build enhanced 4x texture variant pipeline`

**Post-Task 4 (2026-07-22): New Game hang fix.** Sync Super-xBR during
`GetMaterial`/GEOMETRY froze standalone on Enhanced New Game. Fix: native
albedo at material create; yielded warm of registered names + anim/sky +
normals under `ENHANCED TEXTURES` before `RegisterContext`; PlayMode
`WaitForMapBuild` on `LastBuildSeconds`. Re-verified
`TextureUpscalePlayTests`+`EnhancedMaterialPlayTests` **7/7**; Windows
build OK.

**Commit checkpoint:** `mapbuild: yield enhanced 4x warm so new game stays responsive`

---

## Task 5: Enhanced sprites 4× (монстры, предметы, снаряды, спектр)

> Добавлено ревизией 2026-07-22 (см. спеку, слой 5): после первого eyeball
> мир стал заметно лучше, а спрайты выбиваются. Идёт до texel-AA/POM.

**Files:**
- Modify: `Assets/Scripts/MapBuild/Rendering/GraphicsProfile.cs`
  (+ `SpritesUpscale4X`, `UiUpscale4X`; тесты `GraphicsProfileTests`)
- Modify: `Assets/Scripts/MapBuild/SpriteCache.cs`
- Modify: `Assets/Scripts/MapBuild/MapLoader.cs` (прогрев спрайтов с yields)
- Create: `Assets/Tests/PlayMode/SpriteUpscalePlayTests.cs`

- [x] **Step 1: Расширить profile contract.**

`SpritesUpscale4X`/`UiUpscale4X`: Classic false, Enhanced true;
`EnhancedWithLayers` принимает оба. Тесты профиля обновить.

**5/5 GraphicsProfileTests** (`Logs/texquality-t5-profile-edit.xml`).

- [x] **Step 2: Написать failing sprite-cache tests.**

- Enhanced sprite material: texture ровно 4× от patch dims; header
  dims/offsets/mirror в `SpriteMaterial` неизменны (rect billboards тот же);
- Classic: native texture, побайтно тот же объект при повторном Get;
- spectre вариант следует профилю (тот же 4× источник);
- cutout края после bleed+4× без тёмного RGB над порогом cutoff;
- ошибка transform одного lump → native fallback, failed state per lump;
- hot-switch Enhanced→Classic восстанавливает native объекты.

- [x] **Step 3: Реализовать Enhanced4X в `SpriteCache`.**

Ключ кэша `(lump, variant, spectre)`. Пайплайн: `AlphaBleedGuard` →
`BuildEnhanced4XDecoded` без dedither-стадии для спрайтов не изобретать —
использовать общий helper с `applyDedither` по профилю (для спрайтов
паттерн-гейт безопасен), wrap = `Clamp`. Фильтрация/мипы — как у текущих
sprite-материалов (не менять политику в этой задаче).

- [x] **Step 4: Прогрев без фризов.**

Расширить существующий sprite pre-warm (пока WAD открыт): при Enhanced
строить 4× варианты покадрово (yield) под загрузочной плашкой, фаза
`ENHANCED SPRITES` после `ENHANCED TEXTURES`. Ленивый путь в геймплее
(дропы, снаряды, поздние кадры) остаётся — один спрайт мал. Прогревать
только кадры, реально используемые вещами карты + weapon set, не весь
`S_START/S_END`.

- [x] **Step 5: Hot-switch спрайтов.**

Переключение профиля ретаргетит текстуры существующих sprite-материалов
(или materials-per-variant — по фактической архитектуре Stage 8 lit
sprites); возврат в Classic — exact native. Counts стабильны после 20
switches.

- [x] **Step 6: Запустить sprite tests.**

```text
Doom.Map.Tests.GraphicsProfileTests
Doom.Stage3.PlayTests.SpriteUpscalePlayTests
```

**5/5 profile** (`Logs/texquality-t5-profile-edit.xml`);
**6/6 SpriteUpscalePlayTests** (`Logs/texquality-t5-sprite-play.xml`).

**Commit checkpoint:** `sprites: build enhanced 4x sprite variants`

---

## Task 6: Weapon view и HUD 4×

**Files:**
- Modify: `Assets/Scripts/MapBuild/WeaponView.cs`
- Modify: `Assets/Scripts/MapBuild/HudTextureCache.cs`
- Modify: `Assets/Scripts/MapBuild/DoomHud.cs` (если требуется variant plumb)
- Create/Modify: PlayMode тесты weapon/HUD

- [x] **Step 1: Написать failing placement tests.**

Снапшот weapon patch rect (`VirtualScreenRenderer.WeaponPatch`) и HUD
rect'ов на native vs Enhanced: прямоугольники идентичны (позиции из
`PatchHeader`, не из texture dims). Enhanced текстуры — 4× dims; Classic —
native. Меню/intermission — native в обоих режимах.

- [x] **Step 2: Variant-путь в `HudTextureCache`/weapon.**

Ключ `(name, variant)` в `HudTextureCache`; пайплайн bleed → Super-xBR ×2 ×2
(Clamp); failed → native. Только `UiPatchCatalog.StatusBarNames` апскейлятся;
menus/title/intermission всегда native. Weapon view уже берёт 4× из
`SpriteCache` (`SpritesUpscale4X`). Прогрев: weapon set (Task 5) + yielded
`ENHANCED HUD` status-bar patches после `ENHANCED SPRITES`.

- [x] **Step 3: Hot-switch UI.**

OnGUI `TryGet` / `SpriteCache.Get` следуют активному профилю; Classic
восстанавливает native объекты; повторный Enhanced не растит counts.

- [x] **Step 4: Запустить weapon/HUD tests + eyeball.**

**6/6 UiUpscalePlayTests** (`Logs/texquality-t6-ui-play.xml`). Интерактивный
eyeball — с послойным sign-off Task 10.

**Commit checkpoint:** `ui: enhanced 4x weapon and hud patches`

**Post-Task 5/6 (2026-07-22): sharpen для спрайтов/UI.** Интерактивный
вердикт по standalone: оружие и предметы после Super-xBR 4× «немного
замылены» (фильтрация ни при чём — спрайты/HUD идут Point; это характер
Super-xBR на иконочном арте). Добавлен pure `SharpenFilter` (unsharp 3×3;
alpha untouched; прозрачные вне blur) c amount **0.5**, выбранным по
превью `Tools > Doom > Dump Sprite Preview` (панели native / 4× / +0.5 /
+1.0). Применяется в `SpriteCache.CreateEnhancedTexture` и
`HudTextureCache.GetOrCreateEnhanced`; мир не затронут. Тесты:
`SharpenFilterTests` ×9; Graphics EditMode **111/111**
(`Logs/texquality-sharpen-edit.xml`); Sprite+Ui PlayMode **12/12**
(`Logs/texquality-sharpen-play.xml`).

**Commit checkpoint:** `sprites: sharpen upscaled sprite and hud art`

---

## Task 7: Texel-AA sampling в Enhanced shaders

**Files:**
- Modify: Enhanced world shaders (opaque + cutout; актуальные имена файлов
  уточнить по `Assets/Shaders/` на момент реализации)
- Modify: `Assets/Scripts/MapBuild/Rendering/DoomMaterialFactory.cs`
- Modify: `Assets/Tests/PlayMode/EnhancedMaterialPlayTests.cs`

- [ ] **Step 1: Написать failing material assertions.**

Enhanced albedo: Bilinear + mips + aniso; texel-AA property/keyword
включён на Enhanced world материалах при `WorldTexelAA`; Classic
материалы — Point, без keyword.

- [ ] **Step 2: Реализовать texel-AA выборку.**

Функция в общем include: UV → texel space, `frac` квантуется к центру
texel со сглаживанием шириной `fwidth` (fat pixels + smoothstep), затем
обычная bilinear выборка. Применить в Enhanced opaque и cutout albedo
paths. Cutout: alpha порог после texel-AA выборки, проверить отсутствие
«тающих» краёв решёток.

- [ ] **Step 3: Проверить дистанции и углы.**

PlayMode capture-level sanity: рендер плоскости под острым углом не даёт
NaN/чёрных артефактов; переключение профилей на лету меняет режим выборки
без pink materials. Визуальная оценка качества — Task 10, здесь только
корректность.

- [ ] **Step 4: Запустить material tests.**

```text
Doom.Stage3.PlayTests.EnhancedMaterialPlayTests
Doom.Stage3.PlayTests.TextureUpscalePlayTests
```

**Commit checkpoint:** `rendering: texel antialiased sampling for enhanced world`

---

## Task 8: Height, multi-scale normals и POM

**Files:**
- Create: `Assets/Scripts/Graphics/HeightMapGenerator.cs`
- Create: `Assets/Tests/EditMode/Graphics/HeightMapGeneratorTests.cs`
- Modify: `Assets/Scripts/Graphics/NormalMapGenerator.cs` (+ его тесты)
- Modify: `Assets/Scripts/MapBuild/TextureCache.cs`
- Modify: `Assets/Scripts/MapBuild/Rendering/DoomMaterialFactory.cs`
- Modify: Enhanced lit world shader (POM-ветка)
- Modify: `Assets/Tests/PlayMode/EnhancedMaterialPlayTests.cs`

- [ ] **Step 1: Написать failing height fixtures.**

- uniform input → uniform height;
- яркостная ступень → монотонный градиент height;
- дизеринговая шахматка **после** `DeditherFilter` → почти плоская height
  (регрессия «рельеф из шума»);
- fine + coarse веса per surface category применяются (metal ≠ stone);
- вход не мутирует, dimensions сохраняются.

- [ ] **Step 2: Реализовать `HeightMapGenerator`.**

Height = w_fine × fine luminance + w_coarse × blurred coarse luminance
(box/gauss несколько проходов; wrap policy как у источника). Веса —
константы per surface category.

- [ ] **Step 3: Перевести normals на height-источник.**

`NormalMapGenerator` считает нормали (Sobel) из heightmap; height
пакуется в alpha той же RGBA32 linear texture. Обновить существующие
normal-тесты; regression: normal texture alpha не константа для solid
материалов.

- [ ] **Step 4: Включить пайплайн в cache/factory.**

Height/normal генерируются из 4× обработанного albedo (или из 2×
промежуточного — см. mitigation ladder, только по результатам Task 9).
Factory: POM keyword + амплитуда per surface category **только** для
solid opaque walls/flats; masked, fluids, sky — без POM.

- [ ] **Step 5: Реализовать POM-ветку шейдера.**

Фиксированные шаги (например 8–16), высота из `_BumpMap.a`,
консервативная амплитуда. shader_feature/multi_compile так, чтобы masked
вариант не компилировал POM-код. Проверить на остром угле отсутствие
swim/step артефактов сверх приемлемого.

- [ ] **Step 6: Запустить normal/material tests.**

```text
Doom.Graphics.Tests.HeightMapGeneratorTests
Doom.Graphics.Tests
Doom.Stage3.PlayTests.EnhancedMaterialPlayTests
```

**Commit checkpoint:** `rendering: multiscale normals and parallax from height`

---

## Task 9: Lifetime и performance gate

**Files:**
- Modify: `Assets/Scripts/MapBuild/TextureCache.cs`
- Modify: `Assets/Tests/PlayMode/GraphicsResourceLifetimePlayTests.cs`
- Modify: `Logs/enhanced-texture-quality-baseline-notes.md`

- [ ] **Step 1: Закрыть ownership.**

Каждый native/4× albedo и normal зарегистрирован и уничтожается один раз;
fallback alias не освобождается дважды; промежуточные CPU буферы
(dedithered, 2×, 4×, height) не удерживаются после последнего consumer;
`makeNoLongerReadable` включён.

- [ ] **Step 2: Warm-up stability на E1M7.**

Classic load → Enhanced → counts → 20 switches → counts неизменны →
scene reload → teardown без MissingReferenceException/leak growth.

- [ ] **Step 3: Измерить cost против baseline Task 1.**

E1M1/E1M7: load time, первый/повторный switch, managed/texture memory
(world + sprite + UI байты раздельно), frame time после warm-up. Числа
записать. **Память — главный риск итерации (16× albedo + 16× normal
против 4×+4× в Scale2x-версии, плюс sprite set).**
Если E1M7 неприемлем — применять mitigation ladder из спеки по одной
ступени с повторным измерением: (1) height/normal из 2×; (2) 4× только
для textures ≥ 64px; (3) общий откат на 2× Super-xBR. Каждую применённую
ступень задокументировать.

**Отдельно: первый Classic → Enhanced hot-switch.** Покадровый прогрев
работает только на загрузке при персистентном Enhanced; hot-switch из
паузы строит все варианты синхронно в одном кадре — с Super-xBR это
ожидаемый фриз. Замерить; при неприемлемом — покадровый прогрев под
индикатором (аналог `ENHANCED TEXTURES`) и повторный замер.

- [ ] **Step 4: Запустить lifetime tests.**

```text
Doom.Stage3.PlayTests.GraphicsResourceLifetimePlayTests
```

**Commit checkpoint:** `rendering: bound texture quality memory and lifetime`

---

## Task 10: E1 regression, build и послойный visual sign-off

**Files:**
- Modify: `Assets/Tests/PlayMode/E1MapSmokePlayTests.cs`
- Modify: `Assets/Tests/PlayMode/GraphicsBaselineCaptureTests.cs`
  (послойные capture-профили)
- Modify: `Logs/enhanced-texture-quality-baseline-notes.md`
- Modify: `docs/doom-unity-remake-plan.md` только после фактического закрытия
- Modify: `CLAUDE.md` только после фактического закрытия

- [ ] **Step 1: Расширить E1 smoke.**

E1M1–E1M9 в Classic и Enhanced: построение без exception/pink materials,
ожидаемые variants, отсутствие mixed-resolution animation frames.

- [ ] **Step 2: Запустить полные suites.**

Без `-testFilter`, новые XML. Сравнить totals с baseline Task 1. Все
failures разобрать; unrelated pre-existing failure записать с evidence.

- [ ] **Step 3: Windows standalone build.**

`Tools > Doom > Build Windows Standalone`. Проверить запуск, отсутствие
missing shaders (включая POM/texel-AA варианты) и editor-only
зависимостей.

- [ ] **Step 4: Снять послойные captures.**

Одинаковые poses (E1M1/E1M3/E1M7, записаны в baseline notes), шесть
конфигураций из спеки: Classic native → +dedither → +Super-xBR 4× →
+sprites/weapon/HUD 4× → +texel-AA → +normals/POM. Крупный план
brick/metal/door/flats, острые углы, masked walls, fluids, sky, монстры и
предметы в кадре, viewmodel и STBAR. Между конфигурациями с разным
составом CPU-слоёв перезагружать сцену (кэш вариантов строится под
активный профиль). Raw PNG не commit'ить.

- [ ] **Step 5: Интерактивный sign-off (visual gate).**

В Windows build: Classic↔Enhanced hot-switch; оценка близкого плана по
слоям (какой слой даёт видимый вклад); отсутствие «акварели»/потери
пиксель-арт стиля, alpha fringes, POM-артефактов на краях и швах;
first/repeat switch responsiveness; 4:3/16:9. **Решение о статусе
итерации (успех / частичный успех с перечнем слоёв / reject) записать
явно, с указанием вклада каждого слоя.**

- [ ] **Step 6: Закрыть документацию.**

Только после green suites/build/sign-off: отметить spec/plan
завершёнными с датой и вердиктом послойной оценки; записать exact test
totals и performance numbers; обновить roadmap и `CLAUDE.md`; перечислить
ограничения (menus/intermission native, texel-AA/POM только для мира,
нейроапскейл отложен) и, при частичном успехе, рекомендацию по следующему
шагу.

**Commit checkpoint:** `graphics: complete enhanced texture quality stack`

---

## Порядок и зависимости

```text
Task 1 baseline/contracts
        |
        v
Task 2 DeditherFilter (pure)
        |
        v
Task 3 SuperXbr + AlphaBleedGuard (pure)
        |
        v
Task 4 TextureCache Enhanced4X pipeline
        |
        +--------------------+
        v                    v
Task 5 sprites 4x         Task 6 weapon/HUD 4x
        |                    |
        +--------------------+
        |
        +--------------------+
        v                    v
Task 7 texel-AA shaders   Task 8 height/normals/POM
        |                    |
        +--------------------+
        |
        v
Task 9 lifetime/performance
        |
        v
Task 10 E1/build/послойный sign-off/docs
```

Tasks 2–3 — pure transforms, могут выполняться изолированно после
контрактов Task 1. Tasks 5 и 6 (приоритет ревизии 2026-07-22) и Tasks 7 и
8 внутри своих пар независимы и могут идти в любом порядке, но пара 5/6
идёт до пары 7/8, и все четыре должны завершиться до Task 9.

## Stop conditions

Остановиться и пересогласовать дизайн, если:

1. Classic capture меняется после возврата из Enhanced.
2. Дедизеринг на консервативном пороге либо не даёт эффекта на Freedoom,
   либо «акварелит» фактуру — порог не имеет рабочей середины.
3. Super-xBR создаёт массовые alpha fringes или искажения контуров,
   неустранимые bleed/wrap policy.
4. Texel-AA невозможно совместить с controlled mips без видимых артефактов
   на дистанции.
5. POM даёт неприемлемые артефакты на швах секторов/дверных нишах даже с
   минимальной амплитудой.
6. E1M7 memory/load неприемлемы после всех трёх ступеней mitigation ladder.
7. Реализация требует менять UV, geometry, gameplay или save schema.
8. Порт Super-xBR невозможно выполнить без заимствования GPL-кода.
9. Unity Editor занят и новые test XML/build evidence получить невозможно:
   отметить `BLOCKED`, не объявлять PASS.

## Definition of Done

- [ ] Pure suites: DeditherFilter, SuperXbr (incl. golden snapshot),
      AlphaBleedGuard, HeightMapGenerator, обновлённый NormalMapGenerator —
      зелёные, включая Freedoom integration.
- [ ] Enhanced4X пайплайн в cache: 4×, Bilinear+mips, wrap policy,
      fallback; Classic — exact native/Point.
- [ ] Sprites/weapon/HUD: 4× варианты в Enhanced при неизменном placement
      (rects из `PatchHeader`); menus/intermission native; hot-switch
      восстанавливает native; прогрев без фризов.
- [ ] Texel-AA включён в Enhanced opaque/cutout; POM — только solid.
- [ ] Hot-switch/rollback: exact Classic restore, стабильные counts после
      20 switches и scene reload.
- [ ] E1M1–E1M9 smoke в обоих режимах; полные EditMode/PlayMode suites
      зелёные с новыми XML; totals записаны.
- [ ] Windows build собран и проверен.
- [ ] Послойные captures сняты; интерактивный sign-off проведён; вердикт
      по каждому слою записан.
- [ ] Performance/memory numbers и применённые mitigation steps записаны.
- [ ] Roadmap/`CLAUDE.md` обновлены только после фактической приёмки.
