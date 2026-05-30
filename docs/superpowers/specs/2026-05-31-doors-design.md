# Этап 6a. Двери и интерактивные секторы — дизайн

**Дата:** 2026-05-31
**Статус:** утверждён (brainstorming), готов к написанию плана реализации
**Предыдущие этапы:** 0–5 завершены (WAD-ридер, геометрия, игрок+коллизии, текстуры, спрайты)
**Контекст:** первый под-этап Stage 6 (игровая логика). Stage 6 разрезан на под-этапы
6a–6f (двери/секторы → урон/HP → оружие → ИИ → предметы → звук); 6a — первый.

## Цель

Оживить E1M1 (и любую `ExMy`): двери открываются по Use, лифты/платформы и
подъёмы/опускания полов и потолков, лестницы — по line specials и тегам секторов.
Геометрия движущихся секторов меняется во времени с корректными стенами и
коллайдерами. Ходьба/коллизии/текстуры/спрайты из Stage 3–5 не ломаются.

## Объём этапа

**Входит:**
- Полная ported-классификация linedef-типов DOOM (~140): каждый тип распознаётся
  (триггер, категория, направление, скорость, цель по тегу, ключ, repeatable,
  monster-activatable). Типы секторов (`Sector.Special`) — по необходимости.
- Исполняемые категории действий: **двери** (движение потолка), **лифты/платформы**
  (пол вниз→пауза→вверх), **подъём/опускание полов и потолков** (one-shot до
  относительной/абсолютной цели), **лестницы** (цепочка секторов по алгоритму
  stair-builder).
- Триггеры: **Push (Use)**, **Walk (W1/WR)**, **Switch (S1/SR** со сменой текстуры
  `SW1*↔SW2*)`. Различение **once (1)** и **repeatable (R)**.
- Анимация секторов через runtime-высоты + пересборку мешей: пол/потолок/стены
  движущегося сектора и соседей по общим линиям, с обновлением коллайдеров.

**Сознательно инертно/отложено:**
- Категории **crusher / light / teleport / donut / scroll** — распознаются
  таблицей, но действие — no-op с логом «не реализовано в 6a».
- Триггеры **Gun (G)** и **monster-activatable** — распознаются, инертны (нужны
  оружие 6c / ИИ 6d).
- **Ключи:** запертые двери распознаются (несут `Key`), но проверка ключа
  застаблена — в 6a без инвентаря такая дверь **открывается** (лог). Реальная
  блокировка по ключам — Этап 6e.
- **Exit-линии:** распознаются; активация только логируется (прогресса уровней
  нет — Этап 7).
- **Звук** дверей/лифтов — Этап 6f.
- **Краш-урон** закрывающейся двери/потолка (нужен HP — 6b) и плавная «езда» на
  лифте (carry/parent) — отложены; в 6a минимальное поведение (см. «Краевые
  решения»).

## Ключевые решения (из brainstorming)

1. **Объём действий:** двери + лифты + полы/потолки + лестницы (широкий набор), а
   не только двери. Покрывает большинство интерактива E1M1.
2. **Анимация (крук):** runtime-высоты как изменяемое состояние + пересборка мешей
   движущегося сектора и соседей теми же примитивами билдера. Единый источник
   истины, универсально для дверей/лифтов/полов/лестниц. Отвергнуты: «сдвиг флэтов
   + пересборка стен» (больше спецслучаев) и «жёсткая плита-дверь» (не обобщается
   на лифты/лестницы/раскрытие текстур стен).
3. **Таблица типов:** полная классификация всех ~140 linedef-типов (ported data,
   как `mobjinfo` в Stage 5), но исполняются только выбранные категории; прочие
   инертны. Оформляется data-таблицей для роста.
4. **Архитектура:** чистая логика (таблица + модель «что сделать с какими
   секторами») — в новом pure-C# `Doom.Specials` (`noEngineReferences`); рантайм
   (анимация, пересборка, ввод Use, детект пересечений) — в `Doom.MapBuild`.
   Согласуется с правилом «логику портируем, не движок».
5. **Ключи/exit/звук** — отложены (см. объём); в 6a застаблены/инертны, чтобы E1M1
   оставался проходимым и этап был самодостаточным.

## Архитектура

### Сборки и их роли

**`Doom.Specials` (новый, pure-C#, `noEngineReferences: true`, ссылается на
`Doom.Map`):**
- `LineSpecial` — дескриптор типа: `{ int Type, TriggerKind Trigger, bool
  Repeatable, bool MonsterActivatable, SpecialCategory Category, MoveDirection
  Direction, MoveSpeed Speed, TargetSpec Target, KeyKind Key }`.
- `LineSpecialTable` — ported таблица `тип → LineSpecial`, лукап `TryGet(int type,
  out LineSpecial)`; `All`. Источник истины — таблица linedef-типов DOOM (как
  `ThingTable`). Нереализуемые в 6a категории помечены своей `SpecialCategory`,
  но исполнитель их пропускает.
- Перечисления: `TriggerKind { Push, Walk, Switch, Gun }`, `SpecialCategory {
  Door, LockedDoor, Floor, Ceiling, Plat, Stair, Crusher, Light, Teleport, Donut,
  Exit, Scroll }`, `MoveDirection { Up, Down }`, `MoveSpeed { Slow, Normal, Fast,
  Turbo }`, `KeyKind { None, RedCard, BlueCard, YellowCard, RedSkull, BlueSkull,
  YellowSkull, Any }`, `TargetSpec` (как считать целевую высоту: `LowestNeighbor
  Floor/Ceiling`, `NextHigher/LowerFloor`, `HighestNeighborFloor`, `ToCeiling`,
  `ToFloor`, `ByValue(n)`, `LowestNeighborCeilingMinus4` (двери), `StairStep`).
- `SectorActions` (чистые функции над `MapData`): `FindTargetSectors(map, tagOrSelf)`
  по тегу линии (tag 0 = сам сектор линии для ручных дверей); `ComputeTargetHeight
  (map, runtimeHeights, sectorIdx, TargetSpec)` — вычисление целевой высоты из
  высот соседей; `BuildStairChain(map, startSector, ...)` — список секторов
  лестницы по алгоритму DOOM. Без Unity, тестируется в EditMode.
- `Neighbors` — утилита: соседние сектора/линии для сектора (для поиска соседних
  высот и пометки «грязными» при пересборке).

**`Doom.Map` (есть, дополняется):**
- Билдер геометрии получает **оверрайд высот** секторов. Вариант реализации:
  `MapGeometryBuilder.RebuildSector(map, sectorIdx, ISectorHeights heights,
  worldScale, sizes)` → `SectorMeshes` для одного сектора, где `SectorTriangulator`
  и `WallMeshBuilder` читают высоты из `ISectorHeights` (интерфейс с
  `FloorHeight(idx)`/`CeilingHeight(idx)`), а не только из `map.Sectors[idx]`.
  Дефолтная реализация `ISectorHeights` возвращает статические WAD-высоты (текущее
  поведение сохраняется для Stage 2–5).

**`Doom.MapBuild` (есть, дополняется):**
- `RuntimeSectorHeights` (реализует `ISectorHeights`): изменяемые `float
  currentFloor[]/currentCeil[]` по секторам, инициализируются из WAD.
- `SectorGeometry` — реестр: для каждого сектора ссылки на его `Floor`/`Ceiling`/
  `Wall_*` GameObject’ы и метод `RebuildSector(idx)` (пересобрать `MeshFilter` +
  `MeshCollider` через `MapGeometryBuilder.RebuildSector` на текущих высотах) и
  `RebuildSectorAndNeighbors(idx)`.
- `SectorMover` (MonoBehaviour): анимирует высоту(ы) целевых секторов во времени
  (door/plat/floor/stair), на каждый шаг пишет в `RuntimeSectorHeights` и зовёт
  `RebuildSectorAndNeighbors`. Хранит фазы (open→wait→close и т.п.), скорость,
  цель. Один экземпляр на активное действие; самоуничтожается по завершении
  (или ждёт для repeatable/perpetual).
- `LineActivator` (MonoBehaviour): обрабатывает Use (raycast от камеры),
  Walk (детект пересечения триггер-линий по позиции игрока за кадр), Switch
  (Use по переключателю + смена текстуры). По типу линии берёт `LineSpecial` из
  `LineSpecialTable`, находит целевые секторы (`SectorActions`), запускает
  `SectorMover`. Гасит once-линии после срабатывания.
- `MapLoader` (есть): создаёт `RuntimeSectorHeights` и `SectorGeometry` при
  построении, навешивает `LineActivator` на игрока, регистрирует Use в Input.

### Файловая структура (ориентир, финализируется в плане)

Новое:
- `Assets/Scripts/Specials/Doom.Specials.asmdef`
- `Assets/Scripts/Specials/LineSpecial.cs` (дескриптор + перечисления)
- `Assets/Scripts/Specials/LineSpecialTable.cs` (ported таблица)
- `Assets/Scripts/Specials/SectorActions.cs` (поиск целей, расчёт высот, лестницы)
- `Assets/Scripts/Specials/Neighbors.cs`
- `Assets/Scripts/Map/ISectorHeights.cs` (в `Doom.Map`)
- `Assets/Scripts/MapBuild/RuntimeSectorHeights.cs`
- `Assets/Scripts/MapBuild/SectorGeometry.cs`
- `Assets/Scripts/MapBuild/SectorMover.cs`
- `Assets/Scripts/MapBuild/LineActivator.cs`
- Тесты: `Assets/Tests/EditMode/Specials/*`, дополнения в `Assets/Tests/EditMode/Map/`
  и `Assets/Tests/PlayMode/`.

Изменяемое:
- `Assets/Scripts/Map/MapGeometryBuilder.cs`, `SectorTriangulator.cs`,
  `WallMeshBuilder.cs` (чтение высот через `ISectorHeights`).
- `Assets/Scripts/MapBuild/Doom.MapBuild.asmdef` (ссылка `Doom.Specials`),
  `MapLoader.cs` (создание runtime-состояния + активатор).

## Поток данных

```
Игрок жмёт Use → LineActivator: raycast в стену → её LineDef.Special
   → LineSpecialTable.TryGet(special) → LineSpecial{trigger, category, target, ...}
     ├─ trigger несовместим с этим путём активации (напр. Walk на Use) → игнор
     ├─ once-линия уже сработала → игнор
     ├─ category инертна (crusher/light/…/exit/locked-без-ключей) → лог, no-op/stub
     └─ исполняемая категория:
          SectorActions.FindTargetSectors(map, line.tag|self)
          для каждого целевого сектора:
             target = SectorActions.ComputeTargetHeight(map, runtimeHeights, s, spec)
             запустить/обновить SectorMover(s, floorOrCeil, target, speed, phase)
   Walk-триггер: LineActivator каждый кадр проверяет пересечение игроком
                 триггер-линий (отрезок прошлой→текущей позиции × линия).
   Switch-триггер: как Use, плюс смена текстуры сайддефа SW1*↔SW2*.

SectorMover.Update: интерполирует высоту к цели (speed × dt),
   пишет в RuntimeSectorHeights, зовёт SectorGeometry.RebuildSectorAndNeighbors(s)
   → MapGeometryBuilder.RebuildSector(...) на текущих высотах → обновляет
     MeshFilter + MeshCollider затронутых секторов.
```

## Детали реализации

### Таблица типов и триггеры

- `LineSpecialTable` — ported из канонической таблицы linedef-типов DOOM
  (источник истины — оригинальные спец-номера 1..~141; сверяться с Doom Wiki
  «Linedef type» / исходником `p_spec`/`p_doors`/`p_plats`/`p_floor`). Это ported
  data, как `ThingTable`; объёмная, но механическая.
- Триггер выводится из класса типа: Push (ручные двери/переключатели по Use),
  Walk (W1/WR), Switch (S1/SR), Gun (G — инертно). Repeatable — из «1» vs «R».
- **Use:** `Physics.Raycast` от камеры вперёд (~64 DOOM-юнита × worldScale) по
  стенам; по имени/линку стены к её `LineDef` (нужна обратная связь wall-GO →
  linedef index — добавить при инстанцировании стен в `MapLoader`/`SectorGeometry`).
  Берётся передняя сторона линии.
- **Walk:** `LineActivator` помнит позицию игрока; за кадр строит отрезок
  (prev→cur) и проверяет пересечение с триггер-линиями (только те, у кого Walk-спец).
  При пересечении — фир.
- **Switch:** Use по линии-переключателю; находит сайддеф с текстурой `SW1*`,
  меняет на `SW2*` (и наоборот для repeatable), запускает действие; материал стены
  обновляется через тот же кэш текстур.

### Механизм пересборки геометрии

- `RuntimeSectorHeights` инициализируется из `map.Sectors[i].FloorHeight/Ceiling
  Height`. `SectorMover` меняет только текущие значения.
- `MapGeometryBuilder.RebuildSector(map, s, heights, worldScale, sizes)` строит
  `SectorMeshes` для сектора `s`, читая высоты `s` и соседей из `heights`.
- «Грязные» секторы при шаге мувера: сам `s` + все секторы, делящие с ним линию
  (`Neighbors`), т.к. высота стены зависит от обеих сторон. Их GO пересобираются:
  новый `Mesh` в `MeshFilter`, обновлённый `MeshCollider` (пол — Render-меш, стены
  — ThickWall). Флэты не меняются.
- Частота: каждый кадр, пока мувер активен. Активных секторов одновременно мало →
  стоимость приемлема (дверь — пара треугольников + горстка квадов). Перебейк
  MeshCollider — только у затронутых; при необходимости оптимизация — позже.

### Поведения муверов

- **Door (ceiling):** закрыто `ceil=floor`; открыто `ceil = min(сосед.ceil) − 4`.
  DR/DR-варианты: open → wait (~150 тиков ≈ 4.3 c) → close, с переоткрытием при
  блокировке. D1/SR-варианты — одноразовое открытие. Скорость двери (DOOM ~2
  юнита/тик) → перевод в м/с.
- **Plat/Lift:** пол вниз до `min(сосед.floor)` → wait (~105 тиков ≈ 3 c) → вверх.
  Есть perpetual и «down-stay». Скорости slow/fast.
- **Floor/Ceiling movers:** до цели по `TargetSpec` (next higher/lower, lowest/
  highest neighbor, absolute, by N), one-shot.
- **Stair-builder:** от стартового сектора поднять пол на step; распространять на
  соседние секторы, делящие линию и имеющие тот же floor-флэт, наращивая высоту
  (алгоритм DOOM). Скорость/шаг по типу.
- Все DOOM-скорости (юниты/тик, 35 тиков/с) и паузы (тики) переводятся в секунды;
  расстояния × worldScale → метры. Направление и целевые высоты считает
  `Doom.Specials` (чисто); `SectorMover` интерполирует во времени.

### Краевые решения

- **Ключи:** `LineActivator` для `LockedDoor`/двери с `Key != None` логирует
  «дверь заперта (ключи → Stage 6e)» и **всё равно открывает** (E1M1 проходим).
- **Exit:** при активации exit-линии — лог «exit (прогресс уровней → Stage 7)», без
  смены сцены.
- **Езда на лифте:** `CharacterController` не «едет» сам. Минимально: поднимающийся
  коллайдер пола выталкивает капсулу вверх через разрешение проникновения (грубо,
  для медленных лифтов приемлемо); вниз — гравитация держит игрока на полу. Плавный
  carry/parenting — отложен.
- **Краш:** закрывающаяся дверь/потолок — коллайдер просто блокирует; краш-урон
  (нужен HP — 6b) отложен. Ручная дверь, упёршаяся в игрока, переоткрывается
  (мелкая приятность DR) — иначе блок.

## Тестирование

- **EditMode `Doom.Specials.Tests`:**
  - `LineSpecialTable` лукапы: известная дверь (напр. type 1 — DR Door), лифт
    (напр. type 62 — SR Lift), лестница (напр. type 8 — W1 Stairs), переключатель
    → верные `Category/Trigger/Repeatable/Key`; неизвестный тип → нет записи.
  - `SectorActions.FindTargetSectors` по тегу на synthetic-карте (`SyntheticMap
    Builder`): tag → набор секторов; tag 0 → сам сектор линии.
  - `ComputeTargetHeight`: дверь → `min(сосед.ceil) − 4`; лифт → `min(сосед.floor)`;
    floor-raise → next higher neighbor; на собранных вручную высотах.
  - `BuildStairChain`: цепочка из 3–4 секторов с тем же флэтом → корректный список
    и приращения высот.
- **EditMode `Doom.Map`:** `RebuildSector` на оверрайд-высотах даёт пол/потолок на
  заданном Y и стены нужной высоты (геометрические ассерты на synthetic-карте).
  Дефолтный `ISectorHeights` воспроизводит текущую статическую геометрию (регрессия,
  что Stage 2–5 не сломан).
- **EditMode регрессия на `freedoom1.wad` E1M1:** известные линии (дверь, лифт)
  резолвятся в ожидаемую категорию и непустой набор целевых секторов.
- **PlayMode:** загрузить E1M1; найти линию-дверь, вызвать активацию (через
  `LineActivator` напрямую или сымитировать Use-raycast), прокрутить время →
  `RuntimeSectorHeights` потолка двери вырос к открытому, меш/коллайдер сектора
  обновились без исключений; лифт: пол опустился к `min(сосед.floor)`, затем
  вернулся.

## Открытые риски / на проверку при реализации

- **Точные формулы целевых высот и скорости по типам** — выверяются по таблице
  DOOM и визуально на E1M1 (двери открываются на нужную высоту, лифты совпадают с
  уровнями).
- **Детект пересечения линий при ходьбе** (Walk-триггеры) — устойчивость на быстром
  движении/углах; возможно понадобится несколько под-шагов за кадр.
- **Стоимость перебейка `MeshCollider`** при анимации — приемлема для малого числа
  секторов; если узко — оптимизировать (translate флэтов вместо полной пересборки,
  пул мешей).
- **Связь wall-GameObject → linedef index** для Use-raycast — добавить тег/реестр
  при инстанцировании стен.
- **Ощущение «езды» на лифте** — грубое выталкивание может дёргать камеру; при
  плохом ощущении вынести аккуратный carry в отдельную мелкую задачу.
- **Перенос полной linedef-таблицы** — объёмная ported data; реализованные
  категории покрыть тестами, инертные — хотя бы корректно классифицировать.

## Связанные документы

- Мастер-план: `docs/doom-unity-remake-plan.md` (Этап 6, под-этап 6a).
- План реализации (HOW): `docs/superpowers/plans/2026-05-31-doors.md` (будет создан
  skill’ом writing-plans).
- Предыдущий этап: `docs/superpowers/specs/2026-05-30-sprites-design.md`.
- Stage 6 в целом разрезан на 6a–6f; 6b–6f получат свои спеки/планы позже.
