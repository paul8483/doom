# Этап 6b. Урон игроку и HP — дизайн

**Дата:** 2026-05-31
**Статус:** утверждён (brainstorming), готов к написанию плана реализации
**Предыдущие этапы:** 0–5 завершены; под-этап 6a (двери и интерактивные секторы) завершён
**Контекст:** второй под-этап Stage 6 (игровая логика). Stage 6 разрезан на под-этапы
6a–6f (двери/секторы → урон/HP → оружие → ИИ → предметы → звук); 6b — второй.

## Цель

Дать игроку здоровье, броню и урон. Завести чистый, тестируемый API нанесения
урона (`TakeDamage`), к которому позже подключатся враги (6d) и оружие (6c).
Единственный реальный источник урона прямо сейчас — **урон-полы** (секторные
спецэффекты нукаж/слизь/лава), плюс обработка **смерти** с респавном. На E1M1
видимая веха: зайти в урон-сектор → HP убывает → смерть → «You died» → респавн.

## Объём этапа

**Входит:**
- **Модель здоровья и брони** (чистая логика): HP (старт 100, кламп 0..max=100),
  броня (0..max=200, тип зелёная/синяя), `ApplyDamage(int)` с DOOM-поглощением
  брони, флаг `IsDead`, `Reset()`.
- **Классификация урон-секторов** (ported data): `Sector.Special → урон за тик`.
  Реализуются спецэффекты 5 (10), 7 (5), 4 (20), 16 (20), 11 (20). Прочие → 0.
- **Урон-полы в рантайме:** пока игрок стоит на полу урон-сектора (grounded),
  каждые 32 тика (≈0.914 с) применяется урон сектора.
- **Смерть и респавн:** при HP ≤ 0 — блок управления, оверлей «You died — press R»,
  по нажатию R — сброс модели (полное HP) + телепорт на Player start, разблок.
- **Минимальный экранный показ** HP и брони (временный OnGUI-текст, debug).

**Сознательно отложено:**
- **Радиокостюм (ironfeet / powers):** повербапов нет — урон-полы бьют всегда.
- **Спецэффект 11 «exit при HP ≤ 10%»:** переходов уровней нет до Этапа 7 —
  трактуется как 20 урона; завершение уровня логируется/no-op.
- **Спецэффект 9 (секретный сектор):** не урон — вне объёма (счётчик секретов позже).
- **Подбор брони/аптечек (предметы):** под-этап предметов (6e). Броня стартует с 0;
  модель поглощения готова и покрыта тестами, но в игре броня появится с предметами.
- **Полноценный HUD** (лицо, патроны, оружие): Этап 7. Сейчас только debug-текст.
- **Краш-урон** закрывающейся двери/потолка (отложен из 6a): нужен «движущийся
  потолок давит игрока» — отдельная связка с crusher’ами, вне 6b.
- **Урон от падения** (в DOOM его нет) — не делаем.

## Ключевые решения (из brainstorming)

1. **Объём 6b:** модель HP/брони + урон-полы + смерть/респавн. Враги/оружие —
   позже, но `TakeDamage` сразу проектируется как общая точка входа урона.
2. **Броня включена** (роадмап явно называет «броню»): поглощение в точке
   `TakeDamage`, чтобы позже не переделывать. Подбор брони — позже; модель готова.
3. **Определение текущего сектора игрока:** луч вниз → floor-коллайдер, помеченный
   индексом сектора (`SectorRef`, аналог `LineRef` из 6a). O(1), без BSP,
   переиспользует существующие per-sector floor-GO и паттерн raycast (Stage 5
   `ThingSpawner`). Отвергнуто: point-in-polygon по `SectorPolygon[]` (O(секторов),
   возня с невыпуклостью/дырками) и триггер-объёмы на сектор (тяжело, ломается при
   перестроении).
4. **Архитектура:** чистая логика (модель здоровья) — в новой pure-C# сборке
   `Doom.Game` (`noEngineReferences`); классификация урон-секторов — в существующей
   `Doom.Specials` (рядом с `LineSpecialTable`); рантайм (тик урона, смерть,
   показ) — в `Doom.MapBuild`. Согласуется с правилом «логику портируем, не движок».
5. **Смерть → респавн по клавише** (не авто): простой видимый цикл смерти.
6. **Каденция урона** как в DOOM: каждые 32 тика (32/35 с) пока «ноги на полу».

## Архитектура

### Сборки и их роли

**`Doom.Game` (новый, pure-C#, `noEngineReferences: true`, без ссылок):**
- `HealthModel` — состояние и правила здоровья/брони игрока:
  - Поля: `Health` (int, старт 100, `MaxHealth=100`), `Armor` (int, 0,
    `MaxArmor=200`), `ArmorType` (`enum ArmorKind { None, Green, Blue }`).
  - `ApplyDamage(int damage)` — DOOM-поглощение бронёй (см. «Детали»); уменьшает
    броню и HP; HP клампится в 0 снизу; не уходит в отрицательное.
  - `IsDead => Health <= 0`.
  - `Reset()` — вернуть HP=MaxHealth, броню=0, тип=None.
  - **YAGNI:** `Heal`/`AddArmor` (восстановление/выдача брони) **не делаем в 6b** —
    в игре нет источника (подбор предметов → 6e). Сейчас только `ApplyDamage`,
    `IsDead`, `Reset` и поля HP/брони. Тесты задают броню напрямую через конструктор/
    поля, чтобы проверить поглощение без механизма выдачи.
  - Чистый класс, без Unity, юнит-тестируется в EditMode.
- Будущий дом геймплейной логики (боеприпасы, правила оружия/ИИ) — растёт в 6c+.

**`Doom.Specials` (есть, дополняется):**
- `SectorDamageTable` — ported data `Sector.Special → урон за тик`:
  `TryGetDamage(int special, out int damagePerTick)` (или `DamagePerTick(special) →
  int`, 0 если не урон-сектор). Значения: 5→10, 7→5, 4→20, 16→20, 11→20. Источник
  истины — `P_PlayerInSpecialSector` (Doom Wiki «Sector»/«Linedef type» секторные
  спецэффекты). Сосед `LineSpecialTable`; pure, тест в EditMode.

**`Doom.MapBuild` (есть, дополняется):**
- `SectorRef : MonoBehaviour { int SectorIndex; }` — навешивается на floor-
  GameObject в общем `PopulateSectorRoot` (переживает перестроения секторов 6a,
  как `LineRef`).
- `PlayerHealth : MonoBehaviour` — владеет `HealthModel`; `TakeDamage(int)` →
  `model.ApplyDamage`; если стал мёртв — событие `event Action Died` (один раз).
  Свойства `Health`/`Armor` для показа. На игроке.
- `FloorDamageSystem : MonoBehaviour` — тикер: накапливает время; каждые 32/35 c,
  если игрок «на полу» (`CharacterController.isGrounded`), луч вниз → `SectorRef`
  → `SectorDamageTable.DamagePerTick(sector.Special)` → если >0,
  `PlayerHealth.TakeDamage`. Берёт `MapData` (для `Sectors[idx].Special`) и
  worldScale при инициализации. На игроке.
- `PlayerDeathHandler : MonoBehaviour` — подписан на `PlayerHealth.Died`: ставит
  игрока в «мёртв» (отключает `PlayerController` и `LineActivator`, останавливает
  `FloorDamageSystem`), **владеет оверлеем смерти** (`OnGUI` «You died — press R»)
  и обработкой клавиши R; по R — `HealthModel.Reset()`, телепорт на стартовую
  позицию, реактивация. На игроке.
- `PlayerHud : MonoBehaviour` — минимальный `OnGUI` **только** для «HEALTH n   ARMOR
  n» в углу (оверлей смерти — не здесь, см. `PlayerDeathHandler`). Debug, заменится
  в Этапе 7.
- `MapLoader` (есть): в `SpawnPlayer` навешивает `PlayerHealth`/`FloorDamageSystem`/
  `PlayerDeathHandler`/`PlayerHud` на игрока и инициализирует их (`MapData`,
  worldScale, стартовая позиция/поворот для респавна). В `PopulateSectorRoot`
  навешивает `SectorRef` на floor-child с индексом сектора.

### Файловая структура (ориентир, финализируется в плане)

Новое:
- `Assets/Scripts/Game/Doom.Game.asmdef`
- `Assets/Scripts/Game/HealthModel.cs`
- `Assets/Scripts/Specials/SectorDamageTable.cs` (в `Doom.Specials`)
- `Assets/Scripts/MapBuild/SectorRef.cs`
- `Assets/Scripts/MapBuild/PlayerHealth.cs`
- `Assets/Scripts/MapBuild/FloorDamageSystem.cs`
- `Assets/Scripts/MapBuild/PlayerDeathHandler.cs`
- `Assets/Scripts/MapBuild/PlayerHud.cs`
- Тесты: `Assets/Tests/EditMode/Game/Doom.Game.Tests.asmdef` + `HealthModelTests.cs`;
  дополнения в `Assets/Tests/EditMode/Specials/` (`SectorDamageTableTests.cs`);
  `Assets/Tests/PlayMode/PlayerDamagePlayTests.cs`.

Изменяемое:
- `Assets/Scripts/MapBuild/Doom.MapBuild.asmdef` (ссылка `Doom.Game`).
- `Assets/Scripts/MapBuild/MapLoader.cs` (навеска `SectorRef` на полы; навеска
  health/damage/death/hud на игрока + инициализация; сохранить стартовую позицию
  для респавна).

## Поток данных

```
FloorDamageSystem.Update: accumulate dt
   каждые 32/35 c, если CharacterController.isGrounded:
      Physics.Raycast(вниз от игрока) → hit.collider.GetComponentInParent<SectorRef>()
        → sectorIdx → map.Sectors[sectorIdx].Special
        → SectorDamageTable.DamagePerTick(special) = dmg
        если dmg > 0: PlayerHealth.TakeDamage(dmg)
              → HealthModel.ApplyDamage(dmg)  (броня поглощает, HP убывает, кламп 0)
              → если HealthModel.IsDead и ещё не мёртв: PlayerHealth.Died()

PlayerHealth.Died → PlayerDeathHandler:
   отключить PlayerController + LineActivator + FloorDamageSystem,
   показать оверлей «You died — press R».
   нажатие R → HealthModel.Reset(); телепорт игрока на (startPos, startYaw);
              включить контроллер/активатор/damage-систему обратно.

PlayerHud.OnGUI: «HEALTH {model.Health}   ARMOR {model.Armor}» (+ оверлей смерти).
```

## Детали реализации

### Модель здоровья/брони (`HealthModel`)

Порт `P_DamageMobj` (армейская часть) DOOM:
```
ApplyDamage(int damage):
  if (ArmorType != None && Armor > 0):
     saved = (ArmorType == Green) ? damage / 3 : damage / 2   // целочисленно
     if (Armor <= saved): saved = Armor; ArmorType = None      // броня кончилась
     Armor  -= saved
     damage -= saved
  Health -= damage
  if (Health < 0): Health = 0
```
- Зелёная броня поглощает 1/3, синяя — 1/2 урона (целочисленно), тратясь по 1 за
  поглощённую единицу. Когда брони не хватает — поглощает остаток и обнуляется.
- HP клампится в 0 снизу (без отрицательных значений; гиббинг не моделируем).
- `Reset()`: HP=100, Armor=0, ArmorType=None. Старт игрока — те же значения.

### Классификация урон-секторов (`SectorDamageTable`)

- Из `P_PlayerInSpecialSector`: `case 5 → 10`, `case 7 → 5`, `case 4 → 20`,
  `case 16 → 20`, `case 11 → 20`. Остальные спецэффекты урона не наносят → 0.
- `DamagePerTick(int special) → int` (0 для не-урон-секторов). Спецэффект 11
  дополнительно подразумевает завершение уровня при низком HP — **отложено**
  (Этап 7); в 6b только урон 20.

### Урон-полы в рантайме (`FloorDamageSystem`)

- Каденция: каждые `32f / 35f` секунд (≈0.914 с) — как `leveltime & 0x1f` в DOOM.
  Накопитель времени в `Update`; не зависит от частоты кадров.
- Условие урона: игрок «на полу» — `CharacterController.isGrounded` (в DOOM урон
  только когда `z == floorz`, т.е. не в воздухе/над полом на лифте).
- Поиск сектора: короткий `Physics.Raycast` вниз от позиции игрока по floor-
  коллайдерам; `hit.collider.GetComponentInParent<SectorRef>().SectorIndex`. Если
  луч не нашёл `SectorRef` — урона нет (безопасный no-op).
- Урон берётся из `SectorDamageTable.DamagePerTick(map.Sectors[idx].Special)`.

### Смерть и респавн (`PlayerDeathHandler`)

- На событие `Died`: выставить флаг «мёртв», отключить ввод/триггеры/урон-тик,
  показать оверлей. Камера остаётся (можно слегка опустить — необязательно).
- Респавн по R: `HealthModel.Reset()`; `CharacterController.enabled=false` →
  `transform.position/rotation = старт` → `enabled=true` (иначе CC не телепортируется);
  снять «мёртв», включить компоненты. Стартовые позицию/поворот передаёт `MapLoader`
  из `SpawnPlayer` (та же точка Player start + высота над полом, как при спавне).

### Показ (`PlayerHud`)

- `OnGUI`: верхний-левый угол «HEALTH {n}» и «ARMOR {n}». Оверлей смерти —
  центрированный «You died — press R». Явно временный (комментарий: заменить HUD’ом
  в Этапе 7).

## Краевые решения

- **Броня = 0 на старте** (нет подбора): в игре поглощение пока не проявляется;
  математика покрыта юнит-тестами. Появится с предметами (6e). *Опционально для
  демонстрации вехи: не давать стартовую броню (держим дефолт DOOM 0).*
- **Радиокостюм** не моделируется — урон-полы бьют всегда (повербапы → позже).
- **Спецэффект 11**: только урон 20; завершение уровня — лог/no-op (Этап 7).
- **Луч не нашёл `SectorRef`** (игрок над дыркой/в воздухе) — урона нет.
- **Игрок не на полу** (прыжок невозможен, но на поднявшемся лифте/в падении при
  спавне) — `isGrounded` ложно → урона нет, как в оригинале.
- **Множественные тики за кадр** не нужны: каденция ~0.9 с » кадр; одного тика на
  интервал достаточно.

## Тестирование

- **EditMode `Doom.Game.Tests` (`HealthModel`):**
  - Урон без брони: HP уменьшается на полный урон; кламп в 0 (не отрицательный).
  - Зелёная броня: поглощает 1/3 (целочисленно), HP теряет 2/3; броня убывает на
    поглощённое.
  - Синяя броня: поглощает 1/2; HP теряет 1/2.
  - Броня кончается: поглощает остаток, `ArmorType→None`, дальше полный урон в HP.
  - `IsDead` при HP ≤ 0; `Reset()` возвращает 100/0/None.
- **EditMode `Doom.Specials.Tests` (`SectorDamageTable`):**
  - 5→10, 7→5, 4→20, 16→20, 11→20; не-урон-спецэффекты (напр. 0, 9, 1) → 0.
  - (Опц.) интеграция: в `freedoom1.wad` E1M1 присутствует хотя бы один урон-сектор
    (если нет — тест помечает, и PlayMode-проверка использует синтетический/иной
    источник; решается в плане после проверки карты).
- **PlayMode `PlayerDamagePlayTests`:**
  - Загрузить E1M1; поставить игрока в урон-сектор (или напрямую вызвать
    `FloorDamageSystem`/`PlayerHealth.TakeDamage`), прокрутить время → HP убыл.
  - Смерть: довести HP до 0 → `PlayerController` отключён, флаг «мёртв»; респавн
    (вызвать хук) → HP=100 и позиция вернулась на старт.
  - Регрессия: существующие PlayMode-тексты (игрок приземляется, спрайты, двери)
    проходят (навеска новых компонентов не ломает спавн/сцену).

## Открытые риски / на проверку при реализации

- **Наличие урон-секторов в E1M1 freedoom1.wad** — проверить при реализации; если
  их нет, PlayMode-тест ставит игрока в синтетическую ситуацию или вызывает урон
  напрямую (не ослабляя проверку до тривиальной).
- **Луч вниз и слои коллайдеров** — убедиться, что луч попадает в floor-коллайдер
  сектора, а не в капсулу игрока/потолок; использовать паттерн привязки к полу из
  `ThingSpawner` (Stage 5).
- **Телепорт `CharacterController`** — обязательно отключать/включать `enabled`
  вокруг смены `transform.position`, иначе CC «съест» перенос.
- **`SectorRef` переживает перестроение секторов 6a** — навешивать в общем
  `PopulateSectorRoot`, как `LineRef`, чтобы при анимации пола тег не терялся.
- **Целочисленное деление брони** — повторить точную семантику DOOM (saved=damage/3
  или /2, истощение брони), сверять юнит-тестами на конкретных числах.

## Связанные документы

- Мастер-план: `docs/doom-unity-remake-plan.md` (Этап 6, под-этап 6b).
- План реализации (HOW): `docs/superpowers/plans/2026-05-31-player-damage.md`
  (будет создан skill’ом writing-plans).
- Предыдущий под-этап: `docs/superpowers/specs/2026-05-31-doors-design.md` (6a).
- Stage 6 в целом разрезан на 6a–6f; 6c–6f получат свои спеки/планы позже.
