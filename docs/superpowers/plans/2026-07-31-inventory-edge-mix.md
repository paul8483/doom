# Sprite EdgeMix 8× — implementation plan/status

**Статус:** 🟢 EdgeMix 8× для предметов, противников и first-person оружия
закрыт 2026-08-02 (код + full suites + Windows standalone + interactive
visual gates). Осталось: warm-perf baseline.  
**Этап 2 (2026-08-03):** 🔵 contrast-gated EdgeMix запланирован — задачи
в разделе «Этап 2» ниже.  
**Спека:** `docs/superpowers/specs/2026-07-31-inventory-edge-mix-design.md`.

## Выполнено

- [x] Реализовать первый scope для world pickup sprites.
- [x] Отклонить EdgeMix 4×: переход занимает слишком большую долю
  маленького спрайта и размывает детали.
- [x] Зафиксировать кандидат EdgeMix 8× с полосой перехода 2+2 pixels.
- [x] Реализовать deterministic premultiplied-alpha upscaler в
  `Doom.Graphics`.
- [x] Добавить отдельные variant/job kind и cache routing.
- [x] Пометить map pickups, animated pickups и runtime death drops.
- [x] Сохранить projectile/world-effect Super-xBR 4× и Classic native path.
- [x] Поднять Enhanced pipeline version.
- [x] Добавить unit, runner, codec и PlayMode routing tests.
- [x] Собрать Windows standalone.
- [x] Добавить отдельные enemy variant/job kind и cache routing.
- [x] Пометить и прогреть анимационные кадры противников, включая Spectre.
- [x] Перевести противников с Super-xBR 4× на EdgeMix 8× в Enhanced.
- [x] Интерактивно сравнить Classic/Enhanced для предметов и противников.
- [x] Зафиксировать visual verdict: результат лучше исходного благодаря
  сохранению оригинальных краёв; заметное размытие принято как известное
  ограничение для будущего улучшения алгоритма.
- [x] Добавить `EnhancedWeapon8X` и `EnhancedJobKind.WeaponSprite`.
- [x] Расширить session/disk cache codec и поднять
  `EnhancedPipelineVersion` до 3.
- [x] Добавить в `SpriteCache` отдельную регистрацию weapon lumps и
  `GetWeapon`, сохранив native Classic fallback.
- [x] Перевести `WeaponView.DrawPatch` на weapon routing.
- [x] Применить EdgeMix 8× к idle/fire кадрам и muzzle flash
  (`WarmNativeWeapon` в `MapLoader`).
- [x] Сохранить нативные header dimensions/offsets для placement /
  bob / lowering / STBAR clipping.
- [x] Добавить unit, runner, codec и PlayMode routing/placement tests.

## Автоматические результаты

- Focused EditMode runner + codec
  (`EnhancedJobRunnerTests|EnhancedCacheCodecTests`):
  **20/20 PASS** (2026-08-02, pipeline v3 + WeaponSprite).
- Focused PlayMode sprite/weapon routing + placement
  (`SpriteUpscalePlayTests|Weapon_placement_rect…`): **12/12 PASS**.
- Full EditMode: **610/610 PASS** (2026-08-02,
  `Logs/test-results.xml`).
- Full PlayMode: **149/149 PASS** (2026-08-02,
  `Logs/playmode-results.xml`; hot-switch weapon assert обновлён с 4×
  на EdgeMix 8× / `GetWeapon`).
- Windows build: **SUCCESS**, `Builds/Windows/DoomUnity.exe`, ~122 MB
  (`Logs/edge-mix-weapon-build-windows.log`).

## First-person оружие — visual gate

- [x] Прогнать focused EditMode/PlayMode suites.
- [x] Собрать Windows standalone.
- [x] Сравнить Classic/Enhanced для fist, pistol, shotgun, chaingun,
  chainsaw, rocket launcher, plasma и BFG.
- [x] Проверить детали оружия, анимацию и muzzle flash на halo/размытие.
- [x] Зафиксировать visual verdict: SUCCESS (см. спеку).

## Осталось для полного закрытия scope

- [x] Прогнать полный EditMode/PlayMode suite.
- [x] Снять warm-time и memory delta для 8× pickup/enemy/weapon
  textures (2026-08-03, pipeline v4): E1M1 first switch 9.47–9.59 с
  (cold ≈ disk-warm), repeat 7.6–21 мс; E1M7 9.86–9.97 с / ~9 мс;
  sprite bytes 213 МБ; managed ~1.1 ГБ; mitigation ladder не нужен.
  Паритет с до-v4 замерами. **Scope закрыт полностью.**
- [x] Обновить итоговые test результаты в спеке и project status.

## Этап 2: contrast-gated EdgeMix (запланирован 2026-08-03)

Мотивация и дизайн — в спеке, раздел «Этап 2». Кратко: безусловная
4-пиксельная полоса смешивания мылит намеренные контрастные детали
(белый крест аптечки, самоцвет брони); добавляем гейт по взвешенной
RGB-дистанции с плавной рампой (полоса 4→2→0), силуэтный alpha-переход
не трогаем.

**Протокол интерактивных проверок (договорённость 2026-08-03):** перед
каждым шагом, требующим вердикта пользователя, готовятся PNG-сравнения
**было/стало** на стандартном наборе: аптечка `MEDIA0`, броня
`ARM1A0`/`BON2A0`, монстр `SARGA1`, дробовик `SHTGA0` (+ `SHOTA0`
pickup при уместности). «Было» = текущий принятый runtime-результат,
«стало» = кандидат; Classic native слева как референс. Картинки
отдаются пользователю в чат + сохраняются в `Logs/<experiment>/`.
Генератор — расширение `SpritePreviewMenu` (в набор добавляется броня).

### Task 1 — Gate 0: offline-превью с параметрическим свипом

- [x] `EdgeMixUpscaler.Scale8XGated(source, rampStart, rampEnd)` —
  отдельный детерминированный метод (принятый `Scale8X` не тронут):
  вес смешивания = рампа по взвешенной RGB-дистанции; alpha-силуэт
  всегда полный вес; углы — по минимальному весу участвующих пар.
- [x] Превью-меню `Tools > Doom > Dump EdgeMix Gate Preview`
  (`EdgeMixGatePreviewMenu.cs`): панели native | принятый EdgeMix 8× |
  свип (16→64, 32→112, 64→176) → `Logs/edge-mix-contrast-gate0/`.
- [x] Патчи: `MEDIA0`/`STIMA0`, `ARM1A0`/`BON2A0`, `RKEYA0`, контроль
  `SARGA1`/`POSSA1`, `SHOTA0`/`SHTGA0`. PNG отправлены пользователю
  (2026-08-03).
- [x] Интерактивный вердикт Gate 0 (2026-08-03): **SUCCESS — выбрана
  агрессивная точка 16→64** («колонка 3 лучше всех, крест чёткий»).

### Task 2 — интеграция (только после успеха Gate 0)

- [x] Константы `GateRampStart=16`/`GateRampEnd=64` +
  `Scale8XContrastGated` в `EdgeMixUpscaler`; runtime-пути
  pickup/enemy/weapon в `EnhancedJobRunner` переведены на гейт.
  Принятый `Scale8X` сохранён как reference transform.
- [x] Гейт углов — по минимальному весу участвующих пар (min weight ≡
  max pairwise distance из спеки).
- [x] `EnhancedPipelineVersion` → 4.
- [x] Тесты: `EdgeMixUpscalerTests` +6 (близкие цвета ≡ ungated,
  контрастное ребро → nearest, середина рампы — частичный вес,
  alpha-силуэт ≡ ungated, контрастная диагональ исключена из угла,
  невалидная рампа бросает); `EnhancedJobRunnerTests` сверяют
  `Scale8XContrastGated`.
- [x] Focused suites: EditMode
  `EdgeMixUpscalerTests|EnhancedJobRunnerTests|EnhancedCacheCodecTests`
  **32/32 PASS**; PlayMode
  `SpriteUpscalePlayTests|UiUpscalePlayTests|EnhancedSpritePlayTests`
  **20/20 PASS** (2026-08-03).

### Task 3 — visual gate и закрытие

- [x] Windows standalone build: SUCCESS,
  `Builds/Windows/DoomUnity.exe` (~122 MB,
  `Logs/gate-build-windows.log`).
- [x] Интерактивный Classic ↔ Enhanced в standalone (2026-08-03):
  **SUCCESS — «всё хорошо, оставляем как есть»** (рампа 16→64).
- [x] Полный EditMode/PlayMode suite (2026-08-03): EditMode
  **616/616**, PlayMode **149/149** (`Logs/test-results.xml`,
  `Logs/playmode-results.xml`).
- [x] Спека и project status обновлены. **Этап 2 закрыт 2026-08-03.**

### Task 4 (отдельно, опционально) — texel-aware sprite sampling

- [x] Cherry-pick `4995f50` с ветки `esrgan` влит в `main` как `bb7075e`
  (2026-08-03): texel-aware sampling в `DoomEnhancedSprite.shader`
  (`DOOM_SPRITE_TEXEL_AA`, все три прохода) + `AlphaToMask On` + снап UI
  placement (`ToScreenSnapped`) + ранний пиннинг GraphicsMode в
  `MapLoader` + стабилизация тестовых ожиданий. Esrgan-only doc-файлы
  из коммита исключены.
- [x] Focused PlayMode по шести затронутым классам
  (`EnhancedSprite|EnhancedMaterial|EnhancedPost|EnhancedAtmosphere|
  UiUpscale|SaveLoad`): **25/25 PASS**
  (`Logs/playmode-crisp-results.xml`, 2026-08-03).
- [x] Интерактивный вердикт (2026-08-03): **нейтрально** — «не сильно
  лучше, но и не хуже». Коммит остаётся в main: визуально безвреден,
  даёт нефизуальные плюсы (фикс гонки Enhanced-boot в `MapLoader`,
  снап placement, стабильные тестовые ожидания). Вывод для этапа 2:
  остаточное мыло в основном **запечено в EdgeMix-текстуре**, а не в
  сэмплинге — главный рычаг именно contrast-гейт.
- [ ] Внимание: `AlphaToMask On` зависит от MSAA — согласовать с
  незакоммиченным `QualitySettings.asset` (antiAliasing 2→0); дизеринга
  краёв в вердикте не отмечено.
- [ ] Полные suites — на закрытии этапа 2 (Task 3).
