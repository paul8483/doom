# Enhanced Warm Performance — Implementation Plan

> Выполнять задачи последовательно. Checkbox (`- [ ]`) является журналом
> фактического выполнения; не отмечать шаг до зелёного теста или явно
> записанного blocker.

**Goal:** убрать ~85-секундный однопоточный прогрев Enhanced тремя
ступенями: (1) параллельные CPU-джобы + интеграция с бюджетом кадра,
(2) сессионный `EnhancedVariantStore` (переходы уровней без пересчёта),
(3) дисковый pack-кэш по SHA-256 WAD + `EnhancedPipelineVersion`.
Алгоритмы обработки и картинка не меняются; hot-switch сохраняется.

**Architecture:** pure `EnhancedJobRunner` (`Doom.Graphics`) выполняет
job→result без Unity; кэши получают split `TryCreateJob`/`Integrate`
(словари — только главный поток); единый `EnhancedWarmScheduler`
(`Doom.MapBuild`) обслуживает и load-фазы `MapLoader`, и hot-switch
`GraphicsModeController` (дублирующиеся warm-циклы удаляются).
Спека: `docs/superpowers/specs/2026-07-22-enhanced-warm-performance-design.md`.

**Статус:** not started.

**Ветка:** `texquality` (продолжение итерации; выполняется до её
Task 10 sign-off).

**Базовая линия (Task 9 texquality):** первый Enhanced warm E1M1 ~85.0 с
/ E1M7 ~85.7 с; повторный switch 11–19 мс; GPU ~370 МБ; managed
~500–550 МБ. EditMode **121** Graphics; lifetime PlayMode 5/5.

**Правило версии пайплайна:** любое изменение трансформов
(`DeditherFilter`, `SuperXbrUpscaler`, `SharpenFilter`,
`HeightMapGenerator`, `NormalMapGenerator`, `PaletteMipGenerator`,
`AlphaBleedGuard`) обязано инкрементировать `EnhancedPipelineVersion` —
иначе дисковый/сессионный кэш отдаст устаревшие данные.

## Запуск тестов

Как в texquality-плане (новый XML/log на прогон, без `-quit` при
`-runTests`, PASS/FAIL из XML; занятый Editor → `BLOCKED`).

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -nographics -projectPath "C:\Development\doom" `
    -runTests -testPlatform EditMode -testFilter "<FILTER>" `
    -testResults "C:\Development\doom\Logs\warmperf-<task>-edit.xml" `
    -logFile "C:\Development\doom\Logs\warmperf-<task>-edit.log"

& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -projectPath "C:\Development\doom" `
    -runTests -testPlatform PlayMode -testFilter "<FILTER>" `
    -testResults "C:\Development\doom\Logs\warmperf-<task>-play.xml" `
    -logFile "C:\Development\doom\Logs\warmperf-<task>-play.log"
```

## Инварианты реализации

1. Словари/состояние кэшей мутируются только главным потоком; worker'ы
   видят только immutable `EnhancedJob`.
2. Параллельный результат побайтно равен последовательному
   (детерминизм — тест, не декларация).
3. Пайплайн обработки не меняется; golden-снапшоты остаются зелёными
   без правок.
4. Отмена (смена сцены/выход) не оставляет исключений, утечек и
   полусобранных состояний; опоздавшие результаты отбрасываются.
5. Ошибка одного элемента → failed state + native fallback только для
   него (существующая семантика).
6. Store/диск никогда не отдают данные под чужой wadIdentity,
   layerConfig или pipelineVersion.
7. Повреждённый pack-файл — это промах кэша, а не ошибка игры.
8. Classic-путь и повторный hot-switch не медленнее текущих.
9. В тестах дисковый кэш направлен во временную директорию (или
   выключен) — никакой зависимости от состояния машины.

## Source-control gate

Как в texquality-плане: scoped diff, `.meta` для новых файлов, без
bulk add, сначала failing test.

---

## Task 1: Контракты job/store и тест детерминизма

**Files:**
- Create: `Assets/Scripts/Graphics/EnhancedPipelineVersion.cs`
- Create: `Assets/Scripts/Graphics/EnhancedJob.cs` (job + result типы)
- Create: `Assets/Scripts/Graphics/EnhancedJobRunner.cs`
- Create: `Assets/Tests/EditMode/Graphics/EnhancedJobRunnerTests.cs`

- [ ] **Step 1: Определить контракты.**

`EnhancedJob`: kind (WorldAlbedo/WorldNormal/Sprite/Hud), immutable вход
(native `DecodedImage`, `PixelWrapMode`, layer-флаги, category, spectre —
по kind), ключ элемента. `EnhancedJobResult`: kind + CPU-буферы
(albedo mips / normal+height / RGBA) либо ошибка. `EnhancedPipelineVersion.Value`
(int, стартово 1) с правилом инкремента в комментарии.

- [ ] **Step 2: Реализовать `EnhancedJobRunner.Run(job)`.**

Чистая функция: повторяет текущие пайплайны кэшей (мир: dedither →
[bleed] → superxbr ×2 ×2 → palette mips; нормаль: height → normals;
спрайт/HUD: bleed → superxbr ×2 ×2 → sharpen). Ни одного вызова Unity
API. Существующие приватные пайплайны кэшей переводятся на вызов
runner'а (single-source-of-truth), поведение неизменно.

- [ ] **Step 3: Тест детерминизма.**

Для ~10 Freedoom элементов (стены/флет/masked/скай/спрайты/STBAR):
`Parallel.ForEach` по jobs × 4 повтора == последовательный прогон,
побайтно. Плюс regression: результаты runner'а == текущим методам кэшей.

- [ ] **Step 4: Запустить Graphics suite.**

```text
Doom.Graphics.Tests
```

**Commit checkpoint:** `graphics: extract pure enhanced job runner`

---

## Task 2: Параллельный прогрев через `EnhancedWarmScheduler`

**Files:**
- Create: `Assets/Scripts/MapBuild/Rendering/EnhancedWarmScheduler.cs`
- Modify: `Assets/Scripts/MapBuild/TextureCache.cs` (+`TryCreateJob`/`Integrate`)
- Modify: `Assets/Scripts/MapBuild/SpriteCache.cs` (то же)
- Modify: `Assets/Scripts/MapBuild/HudTextureCache.cs` (то же)
- Modify: `Assets/Scripts/MapBuild/MapLoader.cs` (load-фазы → scheduler)
- Modify: `Assets/Scripts/MapBuild/Rendering/GraphicsModeController.cs`
  (`WarmEnhancedAssets` → scheduler)
- Create: `Assets/Tests/PlayMode/EnhancedWarmSchedulerPlayTests.cs`

- [ ] **Step 1: Failing PlayMode tests.**

- warm завершает все элементы; counts/bytes равны последовательной
  версии (снятой до правки);
- прогресс монотонный, фазы TEXTURES/SPRITES/HUD сохраняются;
- смена сцены посреди warm → нет исключений/утечек, повторная загрузка
  корректна;
- ошибка одного job (test seam) → native fallback только для него.

- [ ] **Step 2: Split job/integrate в кэшах.**

`TryCreateJob(id)` возвращает `null` для уже готовых/failed; словари
и `Texture2D` — только в `Integrate` (главный поток). Существующий
синхронный ленивый путь переиспользует runner + Integrate.

- [ ] **Step 3: Реализовать scheduler.**

Work-list → `Parallel.ForEach`
(`MaxDegreeOfParallelism = Max(1, ProcessorCount - 1)`) на фоне;
`ConcurrentQueue<(id, result)>`; интеграция каждый кадр с бюджетом
6–8 мс; прогресс = integrated/total; `CancellationTokenSource` в
`ClearContext`/`RegisterContext`. Оба потребителя (MapLoader,
GraphicsModeController) переведены; дублирующиеся циклы удалены.

- [ ] **Step 4: Замерить.**

E1M1/E1M7 первая Enhanced-загрузка и первый hot-switch на 8C/16T —
записать в baseline notes. **Гейт: ≤ 15 с.** Если не достигнут —
профилировать (upload-бюджет? worker-голодание?) до/вместо перехода к
Task 3; ступень 3 не компенсирует провал ступени 1.

- [ ] **Step 5: Запустить suites.**

```text
Doom.Stage3.PlayTests.EnhancedWarmSchedulerPlayTests
Doom.Stage3.PlayTests.GraphicsResourceLifetimePlayTests
Doom.Stage3.PlayTests.TextureUpscalePlayTests
Doom.Stage3.PlayTests.SpriteUpscalePlayTests
Doom.Stage3.PlayTests.UiUpscalePlayTests
```

**Commit checkpoint:** `rendering: parallel enhanced warm scheduler`

---

## Task 3: Сессионный `EnhancedVariantStore`

**Files:**
- Create: `Assets/Scripts/MapBuild/Rendering/EnhancedVariantStore.cs`
- Modify: `EnhancedWarmScheduler` + три кэша (lookup/publish)
- Create/Modify: PlayMode тесты store

- [ ] **Step 1: Failing tests.**

- второй заход на карту (scene reload / переход уровня): **0**
  выполненных jobs, варианты корректны, время warm ≈ upload-only;
- lookup мимо wadIdentity/layerConfig/pipelineVersion — промах
  (EditMode юниты);
- Classic-загрузка store не трогает; повторный switch не медленнее.

- [ ] **Step 2: Реализовать store.**

`ConcurrentDictionary`, ключ
`(wadIdentity, kind, itemId, layerConfig, pipelineVersion)`; wadIdentity
из существующей Stage 7d идентичности; очистка при несоответствии.
Порядок в scheduler: store → compute; publish после Integrate.

- [ ] **Step 3: Замерить.**

Переход E1M1→E1M2 в Enhanced; пик managed памяти (store ~370 МБ —
записать фактический). Гейт: переход без пересчёта.

- [ ] **Step 4: Запустить suites** (те же, что Task 2, + E1 smoke).

**Commit checkpoint:** `rendering: session store for enhanced variants`

---

## Task 4: Дисковый pack-кэш

**Files:**
- Create: `Assets/Scripts/Graphics/EnhancedCacheCodec.cs` (pure формат)
- Create: `Assets/Scripts/MapBuild/Rendering/EnhancedDiskCache.cs`
- Create: `Assets/Tests/EditMode/Graphics/EnhancedCacheCodecTests.cs`
- Modify: scheduler (store → disk → compute; фоновая запись)
- Create/Modify: PlayMode тесты диска

- [ ] **Step 1: Failing codec tests (EditMode, pure).**

Roundtrip заголовка/индекса/блобов; обрезанный файл, битый magic,
чужой wadHash, чужой pipelineVersion → промах без исключений; запись
temp+rename атомарна (симуляция прерывания — старый файл цел).

- [ ] **Step 2: Реализовать codec + disk cache.**

Один pack-файл `EnhancedCache/<wadHash>-v<N>.bin` в
`persistentDataPath` (в тестах — временная директория через seam).
SHA-256 WAD один раз на сессию. Чтение фоновым потоком на старте warm;
инкрементальная фоновая запись новых результатов; ошибки диска — лог,
не фатально.

- [ ] **Step 3: Failing PlayMode tests.**

- «холодный процесс» (store очищен) с pack-файлом → 0 compute-jobs;
- несовпадение версии → полный пересчёт + перезапись файла;
- повреждённый файл → пересчёт без ошибок игрока.

- [ ] **Step 4: Замерить.**

Старт с диска до конца warm (гейт ≤ ~5 с); размер pack-файла записать.

- [ ] **Step 5: Запустить suites** (Task 2 набор + codec).

**Commit checkpoint:** `rendering: wad-hash disk cache for enhanced variants`

---

## Task 5: Полные сьюты, build, закрытие

**Files:**
- Modify: `Logs/enhanced-texture-quality-baseline-notes.md` (до/после)
- Modify: оба plan/spec (этот и texquality), `CLAUDE.md`

- [ ] **Step 1:** Полные EditMode+PlayMode без фильтра, новые XML;
  разобрать все failures (pre-existing `Hot_switch` +1 материал — по
  прежней записи).
- [ ] **Step 2:** Windows build; запуск standalone: первая загрузка,
  переход уровня, холодный/тёплый старт с диском — числа в notes.
- [ ] **Step 3:** Обновить статусы spec/plan, `CLAUDE.md`; texquality
  Task 10 (sign-off) проводить уже с этим прогревом.

**Commit checkpoint:** `rendering: close enhanced warm performance`

---

## Порядок и зависимости

```text
Task 1 job runner + determinism
        v
Task 2 parallel scheduler   ← гейт ≤15 с (обязателен)
        v
Task 3 session store        ← гейт: переход без пересчёта
        v
Task 4 disk cache           ← гейт: старт ≤ ~5 с
        v
Task 5 suites/build/docs
```

Ступени сдаются по одной; после каждой игра полностью рабочая. Task 4
можно отложить решением по результатам Task 2–3 замеров (записать
решение явно).

## Stop conditions

1. Детерминизм недостижим (parallel ≠ sequential) — остановиться,
   искать разделяемое состояние; не сдавать «почти равно».
2. Невоспроизводимые гонки/крэши в warm — откат ступени, редизайн.
3. Гейт Task 2 (≤ 15 с) не достигнут после профилирования — пересмотр
   дизайна (не маскировать диском).
4. Пик памяти со store неприемлем на целевом железе — обсудить сжатие/
   ограничение store до перехода дальше.
5. Формат pack-файла требует нарушить content rule или класть данные в
   билд — стоп.
6. Editor занят / нет свежих XML — `BLOCKED`, не объявлять PASS.

## Definition of Done

- [ ] Детерминизм-тесты и codec-тесты зелёные (EditMode).
- [ ] Первая Enhanced-загрузка ≤ 15 с (8C/16T), числа записаны.
- [ ] Переход уровня в Enhanced: 0 compute-jobs.
- [ ] Холодный старт с pack-файлом ≤ ~5 с; повреждение/версия
      обрабатываются молча.
- [ ] Hot-switch, lifetime, upscale, E1 smoke сьюты зелёные; полные
      EditMode/PlayMode прогоны с новыми XML; Windows build.
- [ ] Baseline notes: до/после по всем гейтам; spec/plan/CLAUDE.md
      обновлены.
