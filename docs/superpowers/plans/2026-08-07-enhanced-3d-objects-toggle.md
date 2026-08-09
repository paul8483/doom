# Enhanced 3D Objects Toggle — план реализации

**Дата:** 2026-08-07  
**Ветка:** `3d-toggle` от `main`  
**Статус:** ✅ CLOSED 2026-08-07 — Gate 1 SUCCESS (standalone)  
**Спека:** `docs/superpowers/specs/2026-08-07-enhanced-3d-objects-toggle-design.md`

Правила процесса: каждый Task с визуальным эффектом закрывается только
интерактивным вердиктом пользователя; перед проверкой — PNG-панели
«было/стало». Unity строго `6000.4.8f1`. Classic не трогаем; gameplay/
collision/save identity остаются на оригинальных thing root.

## Task 1 — Gate 0: классификация display-grade redraws ✅

1. Editor-меню `Tools > Doom > Dump 3D Toggle Gate Preview`: для каждого
   кандидата (`ARM1A0`, `BAR1A0`, `BFUGA0`, `COLUA0`, `CSAWA0`, `LAUNA0`,
   `MGUNA0`, `PLASA0`, `SHOTA0`; `STIMA0-v3` снят) панель: native спрайт
   (nearest ×N) | кандидат-redraw | наложение silhouette-регистрации.
2. Варианты фильтрации (Point vs Bilinear) на 2–3 репрезентативных лумпах.
3. **Интерактивный вердикт пользователя:** утверждённый display-grade список
   + выбор фильтрации. Результат фиксируется здесь и в спеке.

Вердикт Gate 0 (2026-08-07) ✅:
- **Display-grade allowlist:** `ARM1A0`, `BAR1A0`, `BFUGA0`, `COLUA0`,
  `CSAWA0`, `LAUNA0`, `MGUNA0`, `PLASA0`, `SHOTA0`.
- ❌ `STIMA0-cylinder-shapehint-v3.png` — REJECT (не display-grade).
- **Фильтрация:** Point (default спрайтов проекта; Bilinear не выбран).
- **Runtime note:** `BAR1` имеет кадры A0+B0 → `isAnimated`; redraw для
  `BAR1A0` не применяется, пока нет полного покрытия кадров (анти-моргание).

Выход: утверждённый allowlist лумпов для 2D-уровня.

**Дополнение 2026-08-08 ✅ («результат отличный»):** `ARM1B0`/`BAR1B0`
приняты — allowlist 11 лумпов, гейт анимации пропускает полностью покрытые
лумпы; бочка на pickup/redraw-пути + ваниллный idle-blink S_BAR1/S_BAR2.
Suites: EditMode 614/614, PlayMode 161/161.

**Дополнение 2026-08-09 ✅ SUCCESS:** ammo `AMMOA0`/`CELLA0`/`CELPA0`/
`ROCKA0`/`SHELA0` — mesh + display-redraw в cascade 3D On/Off; interactive
standalone gate SUCCESS (вместе с ранее заведёнными `CLIPA0`/`SBOXA0`).
Display-redraw allowlist **21** лумп.

## Task 2 — Импорт redraws как runtime-ресурсов ✅

1. Скопировать утверждённые PNG в `Assets/Resources/EnhancedSprites/<LUMP>.png`
   с import-настройками (cutout alpha, mip, фильтрация из Task 1).
2. Регистрация на native-патч: пайплайн-код, отображающий 512×512 канву на
   прямоугольник + draw-offsets нативного патча (протокол «спрайт ≤416 px,
   центрирован» инвертируется детерминированно).
3. EditMode-тесты: ресурс находится для каждого утверждённого лумпа;
   silhouette bbox redraw ≈ native bbox после масштабирования (регрессия
   регистрации); мировой размер билборда равен нативному.

## Task 3 — Настройка и меню ✅

1. `GameSettingsData` + `Enhanced3DObjects` (bool, default true), schema bump;
   миграция старых файлов настроек (поле отсутствует → true).
2. EditMode-тесты настроек: roundtrip, миграция, default.
3. Пункт «3D Objects» в Options, видимый только при
   `Graphics Mode = Enhanced`; горячее применение через существующий
   settings-контроллер.
4. PlayMode-тест меню: пункт скрыт в Classic, виден в Enhanced, переключение
   меняет значение и применяется без перезагрузки.

## Task 4 — Резолюция представления и роутинг ✅

1. Чистая C#-функция `(mode, toggle3D, hasMesh, hasDisplayRedraw, isAnimated)
   → Presentation {Mesh | RedrawBillboard | EdgeMixBillboard | NativeBillboard}`
   по матрице спеки; EditMode-тесты всех ветвей (включая: Classic всегда
   native; анимированный лумп в 2D → EdgeMix; лумп без ассетов → EdgeMix).
2. `ExperimentalPickupModel`/`ThingSpawner`: третье состояние — при
   `3D Off` меш скрыт, билборд получает redraw-материал (утверждённые лумпы)
   либо остаётся EdgeMix.
3. Вариант спрайт-кэша: материал из redraw-PNG вместо EdgeMix для
   утверждённых лумпов; кэширование и корректная выгрузка.
4. Hot-switch: переключение toggle и Classic↔Enhanced в любом порядке без
   утечек/артефактов; взрыв бочки в обоих состояниях toggle.
5. PlayMode-тесты: toggle Off → у зароученного лумпа активен билборд с
   redraw-материалом; toggle On → меш; Classic не затронут; бочка после
   взрыва играет BEXP на билборде при обоих значениях toggle.

## Task 5 — Полные сьюты и билд ✅

1. Полный EditMode + PlayMode (базовая точка: 617 + 155 до деревьев;
   актуализировать).
2. `Tools > Doom > Build Windows Standalone` — SUCCESS.

## Task 6 — Gate 1: интерактивная приёмка (standalone) ✅

1. PNG-панели «было/стало» по протоколу (аптечка/броня/монстр/дробовик +
   лампа/бочка/дерево): EdgeMix vs redraw vs mesh → `Logs/3d-toggle-gate1/`.
2. Живая проверка: hot-switch в бою, Classic-регрессия глазами, отсутствие
   «моргания» и размерных скачков.
3. **Вердикт пользователя 2026-08-07: SUCCESS.**

Автоматизация на закрытии: EditMode **629/629**, PlayMode **160/160**,
Windows standalone SUCCESS (~182 MB). Settings schema **v3**
(`Enhanced3DObjects`, default On).
