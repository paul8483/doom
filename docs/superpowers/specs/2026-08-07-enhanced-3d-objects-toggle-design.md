# Enhanced 3D Objects Toggle — дизайн

**Дата:** 2026-08-07  
**Ветка:** `3d-toggle` от `main`  
**Статус:** ✅ CLOSED 2026-08-07 — Gate 0 + Gate 1 SUCCESS  
**Unity:** `6000.4.8f1`  
**Доктрина:** `docs/superpowers/specs/2026-08-07-enhanced-ai-derived-assets-design.md`
(секция «Три уровня представления»)  
**Состояние TRELLIS-роллаута:** `docs/superpowers/plans/2026-08-06-trellis2-pickups.md`  
**План:** `docs/superpowers/plans/2026-08-07-enhanced-3d-objects-toggle.md`

## Контекст и решение

Enhanced теперь показывает TRELLIS.2 3D-меши для 12 лумпов. Доктрина
зафиксировала три уровня представления объектов и переключатель, которым
игрок сам выбирает степень отхода от оригинала (митигация риска «Heroes 3
в 3D»):

- **Classic** — native билборд, бит-точно (не затрагивается этой работой);
- **Enhanced + 3D Off** — улучшенный 2D-билборд (display-grade redraw);
- **Enhanced + 3D On** (default) — 3D-меш.

Эта фича реализует переключатель **«3D Objects» On/Off** в настройках,
видимый только в Enhanced, с горячим применением без перезагрузки сцены.

**Ключевое ограничение scope:** далеко не у всех лумпов есть и 3D-модель, и
display-grade 2D. **Работаем с тем, что есть** — новая генерация контента для
этой фичи не требуется. Уровни разрешаются каскадом per-lump, поэтому фича
осмысленна при частичном покрытии, а качество растёт монотонно по мере
добавления ассетов.

## Матрица разрешения представления (per lump)

| Режим | Порядок выбора |
|-------|----------------|
| Classic | native билборд, всегда |
| Enhanced + 3D On | 3D-меш → display-redraw билборд → EdgeMix билборд |
| Enhanced + 3D Off | display-redraw билборд → EdgeMix билборд |

**Обновление 2026-08-08:** EdgeMix 8× удалён целиком (см. пересмотр в
доктрине `2026-08-07-enhanced-ai-derived-assets-design.md`); последняя
ступень обоих Enhanced-каскадов теперь **native билборд**.

Правила:

- **v2/geometry-only shape-hints в игре не показываются никогда** — только
  картинки, классифицированные как display-grade на Gate 0.
- **Анимированные лумпы** (например `BON1` A–D, BEXP бочки) в 2D-уровне
  остаются нативными, пока не перерисованы **все** кадры — иначе «моргание»
  улучшенных и нативных кадров.
- ~~EdgeMix — переходный fallback (решение 2026-08-07)~~ — удалён целиком
  2026-08-08, fallback теперь native.
- Взрыв бочки: в 3D On меш скрывается и BEXP-последовательность играет на
  билборде (существующее поведение); в 3D Off бочка и так билборд.

## Наличие ассетов (инвентаризация 2026-08-07)

- **3D зароучено (12):** `MEDIA0`, `BON1A0`, `SHOTA0`, `MGUNA0`, `LAUNA0`,
  `PLASA0`, `CSAWA0`, `BFUGA0`, `COLUA0`, `BAR1A0`, `TRE2A0`, `SMITA0`
  (`Assets/Resources/ExperimentalPickups/`).
- **GLB без роутинга:** `ARM1A0` (2 кандидата в `Textures/Trellis2/GLB/`) —
  роутинг ARM1A0 не входит в эту фичу (отдельная задача по накатанной колее).
- **Display-grade 2D allowlist (Gate 0 ✅ 2026-08-07):** `ARM1A0`,
  `BAR1A0`, `BFUGA0`, `COLUA0`, `CSAWA0`, `LAUNA0`, `MGUNA0`, `PLASA0`,
  `SHOTA0`. Фильтрация: **Point**.
  `STIMA0-cylinder-shapehint-v3.png` — Gate 0 REJECT, не display-grade.
- **Заведомо не display-grade:** все `-v2` и `-45deg` хинты (деревья,
  монстры) — geometry-only conditioning.
- **Нет 2D-перерисовки вовсе:** `MEDIA0`, `BON1A0` (кондиционились нативом) —
  в 3D Off падают на EdgeMix. Это принятое ограничение «работаем с тем, что
  есть».

## Пользовательский контракт

- Настройка `3D Objects` (On/Off), default **On**; видна в Options только при
  `Graphics Mode = Enhanced`.
- Хранится в `GameSettingsData` (schema bump с миграцией: старые файлы
  настроек читаются, поле получает default On). **Save-игры не затрагиваются**
  — представление не входит в save schema v5.
- Переключение применяется горячо, без перезагрузки сцены и потери состояния
  мира — как существующий Classic↔Enhanced hot-switch.
- Classic-путь кода и вид не меняются вообще.

## Технические требования

- **Регистрация redraw на native-патч.** Redraw-канва 512×512 (спрайт ≤416 px
  по большей оси, центрирован) должна детерминированно отображаться на
  прямоугольник и draw-offsets нативного патча, чтобы мировой размер и
  привязка к полу совпадали с нативным/EdgeMix билбордом пиксель-в-пиксель.
  Проверка регистрации — часть импорта (автотест: silhouette bounding box
  redraw ≈ native при масштабировании).
- Display-redraws становятся runtime-ресурсами (например
  `Assets/Resources/EnhancedSprites/<LUMP>.png`); conditioning-исходники
  остаются source-only в `Textures/Trellis2/`.
- Материал redraw-билборда — существующий cutout-пайплайн спрайтов
  (`AlphaToMask`, texel-AA правила как у EdgeMix-варианта); фильтрация —
  решение Gate 0 по панелям (Point vs Bilinear на хай-рез картинке).
- Резолюция уровня — чистая C#-функция
  `(mode, toggle3D, hasMesh, hasDisplayRedraw, isAnimated) → Presentation`
  с EditMode-тестами; Unity-глю только применяет результат.

## Вне scope

- Роутинг `ARM1A0` и любая новая генерация (TRELLIS/SOL).
- Оружие от первого лица (`WeaponView`), HUD, меню, intermission.
- Монстры, снаряды, эффекты (остаются EdgeMix во всех Enhanced-вариантах).
- Декимация мешей (волна финализации — отдельная веха).
- Удаление EdgeMix из кода.

## Гейты

- **Gate 0 (классификация) ✅ 2026-08-07:** allowlist из 9 лумпов + Point;
  `STIMA0-v3` REJECT.
- **Gate 1 (интерактивный, standalone) ✅ 2026-08-07 SUCCESS:** переключение
  3D Objects в бою: On — меши; Off — утверждённые redraw-билборды, остальное
  EdgeMix; Classic без изменений; hot-switch без артефактов.
- **Автоматизация ✅:** EditMode **629/629**, PlayMode **160/160**, Windows
  standalone SUCCESS (~182 MB).
