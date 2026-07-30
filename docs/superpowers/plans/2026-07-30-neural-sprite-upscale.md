# Neural Sprite Upscale (Real-ESRGAN) — Implementation Plan

> Выполнять задачи последовательно. Checkbox (`- [ ]`) является журналом
> фактического выполнения; не отмечать шаг до зелёного теста или явно
> записанного blocker.

**Goal:** заменить апскейл спрайтов/оружия/HUD в Enhanced с
Super-xBR 4× + Sharpen на Real-ESRGAN 4× (RGB через Unity Sentis,
alpha — существующим Super-xBR), с Gate 0 до интеграции, fallback chain
нейро → Super-xBR → native и разовой стоимостью инференса через
session store + дисковый pack-кэш. Мир не меняется.

**Architecture:** Sentis остаётся за границей `Doom.MapBuild`
(`NeuralUpscaler`); чистые части (tensor prep, merge RGB+alpha) — в
`Doom.Graphics` (`NeuralSpriteComposer`); `EnhancedWarmScheduler`
получает main-thread нейронную полосу с кадровым бюджетом; ключи
store/disk различают состав через `EnhancedLayerConfig` +
`EnhancedPipelineVersion` bump.
Спека: `docs/superpowers/specs/2026-07-30-neural-sprite-upscale-design.md`.

**Статус:** ❌ stopped — Real-ESRGAN Gate 0 FAIL и follow-up MMPX 4×
Gate 0 FAIL (2026-07-30). Tasks 2–6 не выполняются. MMPX изменил лишь
0,43–1,98% пикселей четырёх сравнений при средней разнице канала менее
0,4/255 и визуально неотличим от Classic nearest. Runtime должен
остаться на Super-xBR 4× + Sharpen; незакоммиченный MMPX-прототип
требует отдельного решения об откате.

**Ветка:** `esrgan` (от `main` после вливания `texquality`;
проверить фактическое состояние веток перед стартом).

**Базовая линия (warm-perf close, 2026-07-24):** EditMode 601/601,
PlayMode 144/144; E1M1 compute warm ~14 с, cold disk ~2.8–3.7 с,
переходы уровней 0 compute; повторный switch 11–19 мс; pack ~367 МБ;
build ~128 МБ.

**Правило версии пайплайна:** нейронная ветка меняет выходные байты
Sprite/Hud → `EnhancedPipelineVersion` инкрементируется при включении
нейро в profile mapping (Task 4). Смена ONNX-модели после этого — тоже
инкремент.

## Запуск тестов

Как в texquality/warm-perf планах (новый XML/log на прогон, без `-quit`
при `-runTests`, PASS/FAIL из XML; занятый Editor → `BLOCKED`).

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -nographics -projectPath "D:\Development\doom" `
    -runTests -testPlatform EditMode -testFilter "<FILTER>" `
    -testResults "D:\Development\doom\Logs\neural-<task>-edit.xml" `
    -logFile "D:\Development\doom\Logs\neural-<task>-edit.log"

& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -projectPath "D:\Development\doom" `
    -runTests -testPlatform PlayMode -testFilter "<FILTER>" `
    -testResults "D:\Development\doom\Logs\neural-<task>-play.xml" `
    -logFile "D:\Development\doom\Logs\neural-<task>-play.log"
```

**Гоtcha:** EditMode с `-nographics` не имеет GPU compute — тесты,
касающиеся Sentis, либо PlayMode, либо CPU backend, либо мокают
`NeuralUpscaler`. Чистые тесты composer'а от backend не зависят.

## Инварианты реализации

1. Sentis API не попадает в `Doom.Graphics` (pure asmdef,
   `noEngineReferences` сохраняется).
2. Alpha-канал спрайта побайтно равен alpha существующей Super-xBR
   ветки; placement rects всегда из `PatchHeader`.
3. Fallback chain per item: нейро упало → Super-xBR+Sharpen → native;
   отсутствие/повреждение модели деградирует только нейронный слой.
4. Store/disk никогда не отдают Super-xBR байты под нейронным
   layerConfig и наоборот; pipelineVersion mismatch = полный пересчёт.
5. Словари кэшей — только главный поток; Sentis Worker — только
   главный поток; worker-пул продолжает считать чистые ветки.
6. Отмена (смена сцены/выход) освобождает Sentis-ресурсы, опоздавшие
   результаты отбрасываются.
7. Classic-путь, повторный hot-switch и cold-disk старт не медленнее
   текущих.
8. Golden-снапшоты CPU-веток остаются зелёными без правок.
9. Мир (WorldAlbedo/WorldNormal jobs) не затрагивается вообще.

## Source-control gate

Как в предыдущих планах: scoped diff, `.meta` для новых файлов
(включая ONNX и LICENSE), без bulk add, сначала failing test.
ONNX-модель коммитится отдельным коммитом с указанием источника,
SHA-256 и команды конвертации в `Assets/ThirdParty/RealEsrgan/README.md`.

---

## Task 1: Gate 0 — Sentis + модели + offline-превью

**Files:**
- Modify: `Packages/manifest.json` (+`com.unity.ai.inference` / Sentis 2.6.1)
- Create: `Assets/ThirdParty/RealEsrgan/` (ONNX кандидаты, LICENSE, README)
- Create: `Assets/Scripts/MapBuild/Editor/NeuralSpritePreviewMenu.cs`

- [x] **Step 1: Подключить Sentis.**

Добавить `com.unity.ai.inference` 2.6.1 (Sentis; verified для Unity 6000.4).
Убедиться, что проект компилируется и build не ломается.

- [x] **Step 2: Получить модели-кандидаты.**

`RealESRGAN_x4plus_anime_6B` (основной), `realesr-animevideov3`
(лёгкий). ONNX вендорен в `Assets/ThirdParty/RealEsrgan/` с SHA-256 и
BSD-3 LICENSE (torch локально не был — взяты готовые ONNX с указанным
provenance в README).

- [x] **Step 3: Превью-инструмент.**

`Tools > Doom > Dump Neural Sprite Preview`: PNG-триптихи
native / Super-xBR+Sharpen / ESRGAN в `Logs/neural-preview/<model>/`.
Прогон 2026-07-30: GPUCompute, по 18 PNG на модель (часть ротаций
отсутствует как отдельные lumps — DOOM dual-name `A2A8` и т.п.).

- [x] **Step 4: Интерактивный вердикт Gate 0.**

**FAIL.** Пользователь: нейро тоже смазанная, контуры рваные;
`animevideov3/POSSA6.png` — не лучше Super-xBR, рука неровная.
Обе модели отклонены → **стоп эксперимента**, Tasks 2–6 не стартуют.
Runtime на Super-xBR. Превью сохранены в
`docs/superpowers/artifacts/2026-07-30-neural-sprite-gate0/` (+ `NOTES.md`).
Sentis/ONNX/preview tooling — оставить или откатить (отдельное решение).

**Commit checkpoint:** `docs: gate 0 fail neural sprite upscale`
*(коммит — по запросу пользователя)*

---

## Task 2: Чистый composer + контракт нейронной ветки

**Files:**
- Create: `Assets/Scripts/Graphics/NeuralSpriteComposer.cs`
- Modify: `Assets/Scripts/Graphics/EnhancedJob.cs` (+`NeuralRgb` флаг)
- Modify: `Assets/Scripts/Graphics/EnhancedJobRunner.cs` (alpha-only путь)
- Create: `Assets/Tests/EditMode/Graphics/NeuralSpriteComposerTests.cs`

- [ ] **Step 1: Failing EditMode tests.**

Composer: подготовка входа (нормализация [0,1], порядок каналов,
размеры), merge мок-RGB 4× + Super-xBR alpha (alpha побайтно равна
референсной ветке), clamp значений, вход не мутирует, invalid input
отклоняется. Runner: `NeuralRgb=false` даёт побайтно текущий результат
(регрессия golden); alpha-only путь возвращает ровно alpha-канал
полного Super-xBR результата.

- [ ] **Step 2: Реализовать.**

`NeuralSpriteComposer` (pure): `PrepareInput(DecodedImage) → float[]`,
`Merge(rgbFloat4x, DecodedImage superXbrRgba4x) → DecodedImage`.
Runner: Sprite/Hud с `NeuralRgb` строит только Super-xBR ветку
(для alpha) и возвращает промежуточный результат для merge на
стороне scheduler'а — форма API по месту, инвариант чистоты
`Doom.Graphics` обязателен.

- [ ] **Step 3: Запустить Graphics suite.**

```text
Doom.Graphics.Tests
```

**Commit checkpoint:** `graphics: neural sprite composer + alpha-only lane`

---

## Task 3: `NeuralUpscaler` runtime + полоса в scheduler

**Files:**
- Create: `Assets/Scripts/MapBuild/Rendering/NeuralUpscaler.cs`
- Modify: `Assets/Scripts/MapBuild/Rendering/EnhancedWarmScheduler.cs`
- Modify: `Assets/Scripts/MapBuild/SpriteCache.cs`
- Modify: `Assets/Scripts/MapBuild/HudTextureCache.cs`
- Create: `Assets/Tests/PlayMode/NeuralUpscalePlayTests.cs`

- [ ] **Step 1: Failing PlayMode tests.**

×5 минимум: (a) Enhanced sprite material — 4× dims, header
dims/offsets/mirror неизменны; (b) модель недоступна (seam) →
Super-xBR fallback, счётчики корректны; (c) один item с
ForceNeuralFailure → fallback только для него; (d) отмена посреди
нейронного warm → нет исключений/утечек, повторная загрузка ок;
(e) hot-switch восстанавливает native.

- [ ] **Step 2: `NeuralUpscaler`.**

Загрузка ONNX (Sentis `ModelLoader`), backend GPUCompute → CPU
fallback, `TryUpscaleRgb`, dispose; ошибки → null + лог (без
исключений наружу). Тестовый seam: подмена/отключение модели.

- [ ] **Step 3: Нейронная полоса в scheduler.**

Sprite/Hud jobs при neural-флагах: ветка B (alpha) — worker-пул;
ветка A — main-thread инференс с кадровым бюджетом (аналог 7 мс
integrate-бюджета; N инференсов/кадр подобрать по факту); merge через
composer → `Integrate`. Прогресс плашки включает нейронные items.
Ленивый путь: как в warm-perf — до готовности native, биллборд
перетаргетируется после интеграции.

- [ ] **Step 4: Запустить suites.**

```text
Doom.Stage3.PlayTests.NeuralUpscalePlayTests
Doom.Stage3.PlayTests.SpriteUpscalePlayTests
Doom.Stage3.PlayTests.UiUpscalePlayTests
Doom.Stage3.PlayTests.EnhancedWarmSchedulerPlayTests
Doom.Stage3.PlayTests.GraphicsResourceLifetimePlayTests
```

**Commit checkpoint:** `rendering: real-esrgan lane in enhanced warm`

---

## Task 4: Профиль, ключи кэшей, версия пайплайна

**Files:**
- Modify: `GraphicsProfile` (+`SpritesNeural4X`, `UiNeural4X`)
- Modify: `Assets/Scripts/MapBuild/Rendering/EnhancedLayerConfig.cs`
- Modify: `Assets/Scripts/Graphics/EnhancedPipelineVersion.cs` (bump)
- Modify: EditMode тесты layerConfig/store; PlayMode disk тест

- [ ] **Step 1: Failing tests.**

EditMode: layerConfig с нейро-флагами не матчится на записи без них
(и наоборот); Classic — оба false. PlayMode: pack-файл со старой
версией → полный пересчёт + перезапись; повторная загрузка карты —
0 инференсов; переход E1M1→E1M2 переиспользует общие спрайты.

- [ ] **Step 2: Реализовать.**

Флаги в профиле (Enhanced = true при доступной модели — capability
через `NeuralUpscaler.IsAvailable`), включение в `EnhancedLayerConfig`,
bump `EnhancedPipelineVersion.Value`, комментарий о правиле смены
модели.

- [ ] **Step 3: Запустить suites** (store/disk/codec EditMode + disk PlayMode).

**Commit checkpoint:** `rendering: neural layer flags + pipeline version bump`

---

## Task 5: Замеры, полные сьюты, build

**Files:**
- Modify: `Logs/enhanced-texture-quality-baseline-notes.md` (раздел Neural)

- [ ] **Step 1: Замерить на E1M1 и E1M7.**

Первый полный прогрев (гейт ≤ 30 с), cold disk (гейт ≤ ~5 с), переход
уровня (0 инференсов), повторный switch (не хуже 11–19 мс), пик
managed/native памяти, размер pack-файла и build. Провал warm-гейта →
mitigation ladder из спеки (по одной ступени, с записью).

- [ ] **Step 2: Полные сьюты.**

EditMode и PlayMode целиком, новые XML; E1M1–E1M9 smoke в обоих
режимах.

- [ ] **Step 3: Windows build + standalone smoke.**

`Tools > Doom > Build Windows Standalone`; boot, загрузка E1M1 в
Enhanced, Player.log без ошибок; размер build записать (+модель).

**Commit checkpoint:** `rendering: neural upscale measurements + suites`

---

## Task 6: Финальный visual gate + закрытие

**Files:**
- Modify: спека (статус/вердикт), этот план, `CLAUDE.md`

- [ ] **Step 1: A/B captures.**

Одинаковые позы (E1M1, E1M3, E1M7): Super-xBR стек vs нейронный —
монстры в упор/средняя дистанция, оружие, предметы, STBAR; анимация
ходьбы/атаки (мерцание деталей); равномерность мир+объекты.

- [ ] **Step 2: Интерактивный eyeball пользователя.**

Вердикт записать дословно. **Успех** → закрытие: статусы в
спеке/плане/`CLAUDE.md`, тест-totals. **Неуспех** → зафиксировать
отрицательный результат эксперимента, решение об откате нейро-ветки
(runtime возвращается на Super-xBR-стек; инфраструктура и выводы
остаются в истории).

**Commit checkpoint:** `docs: close neural sprite upscale experiment`

---

## Порядок и зависимости

```text
Task 1 Gate 0 (Sentis + модели + превью)   ← интерактивный стоп-гейт
        v
Task 2 pure composer + alpha lane
        v
Task 3 NeuralUpscaler + scheduler lane
        v
Task 4 profile flags + cache keys + version
        v
Task 5 замеры + полные сьюты + build
        v
Task 6 visual gate + закрытие              ← интерактивный стоп-гейт
```

После Task 1 возможен ранний выход без изменений runtime. После
каждой задачи игра полностью рабочая (нейро включается только в
Task 4 через профиль; до этого ветка тестируется изолированно).

## Stop conditions

1. **Gate 0 провален** (превью не лучше Super-xBR) — стоп, runtime не
   меняется, вывод в спеку.
2. ONNX-модель не импортируется в Sentis ни в одной конвертации —
   стоп, эскалация (не пытаться «почти совместимый» opset).
3. Инференс на целевом железе аномально медленный (warm-гейт
   недостижим после mitigation ladder) — стоп, обсуждение.
4. Sentis нестабилен в batchmode/standalone (крэши, утечки) — откат
   задачи, редизайн.
5. Alpha/placement инварианты нарушаются (спрайт «поплыл» в rect) —
   стоп, это ошибка дизайна, не тюнинг.
6. Editor занят / нет свежих XML — `BLOCKED`, не объявлять PASS.

## Definition of Done

- [ ] Gate 0 пройден с записанным вердиктом и выбранной моделью.
- [ ] Спрайты/оружие/HUD в Enhanced — нейро RGB + Super-xBR alpha;
      Classic и мир не изменены; меню/intermission native.
- [ ] Fallback chain и capability-деградация покрыты тестами.
- [ ] Store/disk отдают нейро-результаты; 0 инференсов на повторных
      загрузках; pipelineVersion bump.
- [ ] Warm ≤ 30 с, cold disk ≤ ~5 с (или задокументированный
      mitigation); числа в baseline notes.
- [ ] Полные EditMode/PlayMode + E1 smoke + Windows build зелёные.
- [ ] Финальный интерактивный visual gate: вердикт записан; успех →
      статусы обновлены, неуспех → эксперимент честно закрыт как
      отрицательный.
