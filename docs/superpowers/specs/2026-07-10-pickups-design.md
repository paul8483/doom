# Этап 6e. Подбираемые предметы — дизайн

**Дата:** 2026-07-10
**Статус:** реализован; EditMode + PlayMode зелёные
**Предыдущие этапы:** 0–5 завершены; под-этапы 6a–6d завершены; 6e реализован
**Контекст:** пятый под-этап Stage 6 (игровая логика). Разрез 6a–6f:
двери/секторы → урон/HP → оружие → ИИ → **предметы** → звук.

## Цель

Сделать карту «живой» по предметам: аптечки и броня лечат/защищают, ключи
открывают запертые двери (заглушка 6a снимается), рюкзак удваивает максимумы
патронов, берсерк усиливает кулак, радиокостюм глушит урон-полы, зомби роняют
обойму/дробовик. Узкий оружейный подбор из 6c (`ThingPickup` → шесть
doomednum'ов) расширяется до полного E1-набора предметов. Видимая веха: на
E1M1 подобрать стимпак и зелёную броню, увидеть рост HP/брони в HUD; на
E1M2 (или любом уровне с locked door) — ключ не даёт открыть дверь без него и
открывает с ним; убить POSS — на земле обойма.

## Объём этапа

**Входит:**
- **Здоровье:** stimpack (2011, +10 ≤100), medikit (2012, +25 ≤100),
  health bonus (2014, +1 ≤200), soulsphere (2013, +100 ≤200).
- **Броня:** green armor (2018, 100 / `ArmorKind.Green`), blue armor (2019,
  200 / `Blue`), armor bonus (2015, +1 ≤200; если типа не было — ставит Green).
  Правила выдачи — порт `P_GiveArmor` / `P_GiveBody` из `p_inter.c`.
- **Ключи:** шесть типов (5/6/13/38/39/40) → инвентарь; `LineActivator`
  отказывает в открытии locked door без нужного ключа (точный `KeyKind`,
  card ≠ skull). Сообщение в debug-лог при отказе.
- **Рюкзак (8):** удваивает текущие максимумы `AmmoModel` (bullets 200→400,
  shells 50→100) и даёт «клип-грант» при каждом подборе (+10 пуль, +4 дроби),
  даже если рюкзак уже был. Ракеты/ячейки не заводим — оружия под них нет.
- **Берсерк (2023):** если HP &lt; 100 — поднять до 100; сила кулака ×10
  (как `pw_strength` в DOOM — **на весь уровень**, не тикает); принудительно
  выбрать кулак.
- **Радиокостюм (2025):** таймер ~60 с (60×35 тиков); пока активен —
  `FloorDamageSystem` не наносит урон-полы.
- **Расширение подбора:** один роутер поверх существующего `ThingPickup`
  (оружие/патроны 6c остаются, плюс новые doomednum'ы). Билборд исчезает
  только если подбор успешен (полные патроны / полная броня того же или
  лучшего уровня — вещь остаётся, как в DOOM).
- **Дроп с трупов:** POSS → CLIP (2007), SPOS → SHOTGUN (2001) в момент
  смерти (порт `P_KillMobj` drop table). Дроп — обычный `ThingPickup`-GO.
- **HUD:** debug-строки KEYS + индикатор костюма/берсерка (полноценный HUD —
  Этап 7).

**Сознательно отложено:**
- **Invulnerability / invisibility / light-amp / computer map** — визуальные
  и/или картографические эффекты; без палитры/automap малозаметны. Позже.
- **Megasphere, super shotgun и прочий Doom 2** — вне Freedoom Phase 1 E1.
- **Ракеты, ячейки, chainsaw / rocket / plasma / BFG** как подбираемое оружие —
  нет стволов в 6c; появятся с оружейным расширением.
- **Звук подбора** (`DSITEMUP` и т.п.) — 6f.
- **Полноценный HUD** (цифры, лицо, иконки ключей) — Этап 7.
- **Сохранение арсенала после респавна** — по-прежнему сброс оружия/патронов
  как в 6c; ключи **сохраняются** (иначе R на запертой двери ломает прогресс
  уровня). Повербапы и рюкзак при респавне сбрасываются вместе с патронами.
- **Анимация предметов** (вращение BON1 и т.п.) — статичный кадр A достаточен;
  крутилку можно добавить позже поверх `SpriteBillboard`.

> **Статус 2026-07-11:** ограничение по ракетам снято оружейным расширением.
> Реализованы `AmmoType.Rockets`, ракетница 2003, ракета 2010, коробка 2046
> и ракетный клип-грант рюкзака; ячейки и plasma/BFG остаются отложенными.

## Ключевые решения (из brainstorming)

1. **Один чистый роутер `ItemRules`** — doomednum → результат подбора
   (`PickupResult`: принят / отклонён). Unity только вызывает и при успехе
   уничтожает GO. Отвергнуто: размазать switch по `PlayerWeapons`/
   `PlayerHealth` (уже тесно в 6c; нетестируемо целиком).
2. **Ключи входят в 6e обязательно** — заглушка 6a («открываем anyway»)
   блокирует честный E1; данные `KeyKind` уже в `LineSpecialTable`.
3. **Берсерк и костюм — да, invuln/invis — нет** — оба напрямую бьют в
   существующие системы (кулак / урон-полы); визуальные повербапы без палитры
   — пустой объём.
4. **Дроп с POSS/SPOS входит** — дёшево (спавн того же `ThingPickup`) и
   закрывает отложенное из 6d; без дропа «ферма» патронов на E1M1 беднее
   оригинала.
5. **Рюкзак только для Bullets/Shells** — не заводим `AmmoType.Rockets/Cells`
   «на будущее» (YAGNI); удвоение и выдача только по существующим типам.
6. **Архитектура как 6a–6d:** правила в `Doom.Game`, ключи-enum уже в
   `Doom.Specials`, Unity-глю в `Doom.MapBuild`. `ThingTable` уже содержит
   все спрайты предметов — новых данных в `Doom.Things` почти не нужно
   (разве что константы doomednum'ов в таблице подбора).

## Архитектура

### Сборки и их роли

**`Doom.Game` (есть, дополняется; pure-C#, `noEngineReferences`):**
- `HealthModel` — добавить:
  - `bool GiveHealth(int amount, int cap)` — порт `P_GiveBody` (stim/medi
    cap=100, bonus/soul cap=200); false если уже на капе.
  - `bool GiveArmor(ArmorKind kind)` — порт `P_GiveArmor`: hits = kind×100
    (Green=100, Blue=200); false если `Armor >= hits` (уже не хуже).
  - `bool GiveArmorBonus(int amount)` — +N к броне ≤200; если `ArmorType==None`
    → Green.
- `KeyInventory` — биты/набор по внутреннему `PlayerKey` (без зависимости
  `Doom.Game` → `Doom.Specials`); `Give(PlayerKey)`, `Has(PlayerKey)`,
  `HasAny()`, `Reset()`. Маппинг `KeyKind` ↔ `PlayerKey` живёт в Unity-глю
  (`Doom.MapBuild.KeyMapping`).
- `AmmoModel` — флаг `HasBackpack`; `GetMax` возвращает ×2 при рюкзаке;
  `GiveBackpack()` удваивает максимумы (идемпотентно) и делает `Add` по
  clip-объёмам; `Reset()` снимает рюкзак (вместе с патронами на респавне).
- `PlayerPowers` (или поля на маленьком `PowerModel`) — `Berserk` (bool,
  уровень-permanent), `IronFeetTics` (int, тикает вниз); `Advance(tics)`,
  `GiveBerserk()`, `GiveIronFeet(durationTics)`.
- `ItemRules` — статический/табличный роутер: на вход doomednum + снимки
  моделей (health/ammo/keys/powers/loadout), на выход мутации + bool
  accepted. Оружейные 6c-кейсы переезжают сюда (или вызываются из него),
  чтобы был один путь.
- `HitscanRules` / кулак — множитель ×10 при berserk (формула A_Punch:
  `damage = (P_Random()%10+1)*2`, при strength ещё `*10`).

**`Doom.Specials` (без изменений API):** `KeyKind` и `LineSpecial.Key` уже есть.

**`Doom.Things`:** `ThingTable` уже знает все предметы. Возможно тонкая
таблица `DeathDropTable` (monster doomednum → drop doomednum) — либо
константы рядом с `ItemRules` в `Doom.Game`.

**`Doom.MapBuild` (Unity-глю):**
- `ThingPickup` — расширить список; `OnTriggerEnter` зовёт единый
  `PlayerInventory.TryPickup(doomedNum)` (новый тонкий фасад на игроке),
  а не только `PlayerWeapons.Pickup`.
- `PlayerInventory` (или расширение существующих компонент) — владеет/
  прокидывает `KeyInventory`, `PlayerPowers`, связывает `HealthModel` через
  `PlayerHealth`, `AmmoModel`/`WeaponLoadout` через `PlayerWeapons`.
- `PlayerHealth` — обёртки `GiveHealth` / `GiveArmor` / `GiveArmorBonus`.
- `PlayerWeapons` — `Pickup` делегирует в `ItemRules` или остаётся для
  оружия, но вызывается из фасада; кулак читает berserk.
- `LineActivator.Activate` — если `sp.Key != None` и `!keys.Has(sp.Key)` →
  return (лог «need key»); иначе открыть. Монстры по-прежнему открывают
  только не-locked (как сейчас через `IsUseableDoor` / monster path).
- `FloorDamageSystem` — пропуск тика урона при `IronFeetTics > 0`.
- `EnemyHealth` / смерть монстра — после перехода в Die заспавнить drop
  (если есть) рядом с трупом через тот же путь, что `ThingSpawner` для
  предметов.
- `ThingSpawner` — вешать `ThingPickup` на все pickup-doomednum'ы этапа
  (не только шесть из 6c).
- `PlayerHud` — KEYS (R/Y/B card/skull кратко), IRONFEET/BERSERK если активны.
- `MapLoader.SpawnPlayer` — создать/связать инвентарь; на респавне: HP/ammo/
  weapons/powers/backpack reset; **ключи оставить**.

### Файловая структура (ориентир, финализируется в плане)

Новое:
- `Assets/Scripts/Game/`: `KeyInventory.cs`, `PlayerPowers.cs`, `ItemRules.cs`
  (и при необходимости `DeathDropTable.cs`)
- `Assets/Scripts/MapBuild/`: `PlayerInventory.cs` (фасад)
- Тесты: `KeyInventoryTests`, `HealthModelGiveTests` (или расширение
  `HealthModelTests`), `ItemRulesTests`, `AmmoModelBackpackTests`,
  `PlayerPowersTests`; PlayMode `PickupPlayTests`

Изменяемое:
- `HealthModel.cs`, `AmmoModel.cs`, `HitscanRules.cs` (berserk)
- `ThingPickup.cs`, `ThingSpawner.cs`, `PlayerWeapons.cs`, `PlayerHealth.cs`,
  `LineActivator.cs`, `FloorDamageSystem.cs`, `EnemyHealth.cs` /
  `MonsterController.cs` (drop), `PlayerHud.cs`, `MapLoader.cs`,
  `PlayerDeathHandler.cs` (что сбрасывать)

## Поток данных

```
OnTriggerEnter(ThingPickup) → PlayerInventory.TryPickup(doomedNum):
  ItemRules.TryApply(doomedNum, models) → accepted?
    да  → Destroy(GO)
    нет → вещь остаётся

E / Walk / Switch → LineActivator.Activate(line):
  sp.Key != None && !KeyInventory.Has(sp.Key) → отказ
  иначе → StartMover(...)

FloorDamageSystem tick:
  IronFeetTics > 0 → skip ApplyDamage

Fist fire → HitscanRules.Punch(..., berserk):
  base 2×(1d10); if berserk → ×10

Monster Die (POSS/SPOS) → spawn ThingPickup(dropDoomedNum) at corpse
```

## Детали реализации

- **Числа** — из linuxdoom-1.10 `p_inter.c` / `p_mobj.c`; фиксируются в плане
  (не читать исходники заново при реализации).
- **Радиус подбора** — как в 6c: sphere 32 DOOM units × worldScale.
- **Stim/Medi при полном HP** — rejected (вещь лежит).
- **Green armor при Armor≥100** — rejected; blue при Armor≥200 — rejected.
- **Бонусы** при капе 200 — rejected.
- **Ключ повторно** — accepted в DOOM (сообщение), но у нас достаточно
  accepted + no-op give (идемпотентно); GO уничтожается.
- **Берсерк повторно** — heal-to-100 если нужно + снова pending fist;
  сила уже есть.
- **Тики костюма** — копить в `PlayerPowers.Advance` из того же 35 Гц
  накопителя, что оружие/монстры (или из `FloorDamageSystem`/игрока).
- **Дроп** — позиция трупа + небольшой вертикальный offset; без physics
  impulse (в DOOM item тоже просто появляется).

## Краевые решения

- **Запертая дверь без ключа** — не открывается; повторный Use безобиден.
- **Монстр и locked door** — монстры не открывают locked (уже не должны
  через monster-use path); не меняем.
- **Респавн (R):** HP=100, броня=0, ammo/weapons/backpack/powers сброс;
  ключи сохраняются; подобранные вещи и дропы на карте не респавнятся.
- **Подбор оружия при полном боезапасе** — как 6c: новое оружие берётся
  всегда; уже имеющееся + полные патроны → rejected.

## Тестирование

- **EditMode `Doom.Game.Tests`:**
  - `HealthModel`: GiveHealth/GiveArmor/bonus/soul — капы, reject, типы брони.
  - `KeyInventory`: give/has по цветам; card ≠ skull; Any.
  - `AmmoModel`: backpack удваивает max; повторный backpack идемпотентен;
    Reset снимает.
  - `PlayerPowers`: ironfeet тикает до 0; berserk не тикает.
  - `ItemRules`: таблица doomednum'ов — accepted/rejected на фикстурах
    (полный HP, полная броня, полный ammo); оружейные кейсы 6c не сломаны.
  - `HitscanRules`: berserk-кулак в 10 раз больше обычного (на том же rng).
- **PlayMode:**
  - Stimpack поднимает HP, GO уничтожен; при HP=100 — GO остаётся.
  - Green armor даёт 100 и тип Green; урон меньше без брони.
  - Ключ: locked door без ключа не двигается; после подбора — двигается.
  - POSS death → CLIP появляется и подбирается.
  - IronFeet: в урон-секторе HP не падает, пока тики &gt; 0.

## Критерий готовности

На E1M1: стимпаки/медикаменты/броня/бонусы подбираются с правильными капами;
зомби оставляют обоймы, сержанты — дробовики; рюкзак (если есть на карте)
удваивает максимумы. На уровне с запертой дверью ключ обязателен. Берсерк
переключает на кулак и сильно бьёт; костюм спасает в нукаже. EditMode +
PlayMode зелёные; CLAUDE.md / roadmap обновлены.

## Связанные документы

- Мастер-план: `docs/doom-unity-remake-plan.md` (Этап 6, под-этап 6e).
- План реализации (HOW): `docs/superpowers/plans/2026-07-10-pickups.md`.
- Предыдущие: `docs/superpowers/specs/2026-05-31-doors-design.md` (6a, ключи),
  `docs/superpowers/specs/2026-05-31-player-damage-design.md` (6b, Heal YAGNI),
  `docs/superpowers/specs/2026-07-02-weapons-design.md` (6c, узкий подбор),
  `docs/superpowers/specs/2026-07-03-monster-ai-design.md` (6d, дроп отложен).
