# Sprite EdgeMix 8× — дизайн

**Дата:** 2026-07-31  
**Ветка:** реализация предметов начата в `inventory-redraw` от `main`
(`a11a35f`), затем перенесена в `main` и расширена на противников и
first-person оружие.  
**Статус:** 🟢 EdgeMix 8× для предметов, противников и FP-оружия закрыт
2026-08-02 (код + full suites + Windows standalone + interactive visual
gates). Warm-perf baseline — ещё не снят.

## Цель и границы

Улучшить спрайты с прозрачным силуэтом, сохранив исходный pixel-art характер
краёв:

- предметы, лежащие в мире: здоровье, патроны, броня, ключи,
  бонусы/power-up и world weapon pickups;
- противники, включая их анимационные кадры и Spectre;
- first-person оружие и muzzle flash.

Не меняются:

- стены, полы, потолки и sky;
- projectiles и world effects (PUFF/BLUD и т.п.);
- HUD, меню и intermission.

## Как найден алгоритм

Shape-redraw 4× дал более плавный силуэт, но деформировал мелкие
внутренние области. Прямой EdgeMix 4× по описанному правилу
`2 исходных + 4 средних + 2 исходных` оказался слишком размытым:
у маленьких pickup-патчей цветовая граница встречается почти в каждом
нативном пикселе, поэтому полосы смешивания перекрывали детали.

Рабочий кандидат увеличивает разрешение до 8×, но сохраняет ширину
перехода четыре output-пикселя:

```text
AAAAAA | MMMM | BBBBBB
```

`A` и `B` — соседние исходные цвета, `M` — их среднее. Внутри каждого
8×8 блока остаётся центр 4×4 исходного цвета; по два пикселя с каждой
стороны участвуют в переходе.

## EdgeMix 8×

`EdgeMixUpscaler` работает детерминированно в pure-C# `Doom.Graphics`:

1. Каждый исходный texel соответствует блоку 8×8.
2. У вертикальной/горизонтальной границы берутся два output-пикселя
   с каждой стороны.
3. Для двух соседей записывается арифметический средний цвет.
4. На пересечении границ усредняются четыре texel.
5. RGB усредняется в premultiplied-alpha пространстве, чтобы прозрачный
   hidden RGB не создавал тёмный ореол.
6. На внешнем краю патча wrap отсутствует; внутри прозрачные texel
   участвуют в alpha-переходе.

Алгоритм не использует neural inference, dedither, Super-xBR или
последующий sharpen.

## Runtime routing

- `ThingSpawner` и `PickupFactory` помечают pickup billboards, включая
  animated pickups и runtime death drops.
- Enemy spawn/warm path помечает кадры противников отдельно от остальных
  sprite lumps.
- `MapLoader` / `WeaponView` помечают first-person weapon и flash lumps
  через `WarmNativeWeapon` / `GetWeapon`.
- `SpriteCache` регистрирует pickup/enemy/weapon lump indices.
- В Enhanced pickup получает `EnhancedPickup8X` и
  `EnhancedJobKind.PickupSprite`.
- В Enhanced enemy получает `EnhancedEnemy8X` и
  `EnhancedJobKind.EnemySprite`.
- В Enhanced weapon получает `EnhancedWeapon8X` и
  `EnhancedJobKind.WeaponSprite`.
- Остальные sprite lumps (projectiles/effects) продолжают использовать
  принятый Super-xBR 4× + Sharpen.
- В Classic pickup, enemy и weapon возвращаются к native texture.
- Patch header width/height/offsets остаются нативными, поэтому размер
  и положение спрайта / viewmodel в мире не меняются.
- Session/disk cache разделён по job kind; pipeline version поднята до 3.

## Проверки

- EdgeMix + Enhanced runner / codec: **20/20 EditMode PASS**
  (`EnhancedJobRunnerTests|EnhancedCacheCodecTests`, pipeline v3).
- Sprite/weapon routing + placement: **12/12 PlayMode PASS**
  (`SpriteUpscalePlayTests|Weapon_placement_rect…`).
- Full suites 2026-08-02: EditMode **610/610**, PlayMode **149/149**
  (`Logs/test-results.xml`, `Logs/playmode-results.xml`).
- 2026-08-02: отдельные enemy variant/job kind; visual gate SUCCESS.
- 2026-08-02: weapon variant/job kind + `WeaponView` routing;
  interactive weapon visual gate SUCCESS.
- Windows standalone: **SUCCESS**, ~122 MB,
  `Builds/Windows/DoomUnity.exe`
  (`Logs/edge-mix-weapon-build-windows.log`).

## Локальные visual artifacts

Каталоги в `Logs/` (gitignored):

- `inventory-redraw-gate0/` — shape-redraw V2;
- `inventory-edge-mix-gate0/` — EdgeMix 4× reject (слишком размыт);
- `inventory-edge-mix-8x-gate0/` — текущий кандидат.

## Visual gate: предметы и противники

В standalone сравнены Classic ↔ Enhanced на E1-картах:

- аптечки и health bonuses;
- clip/shells/ammo boxes;
- armor и armor bonuses;
- keys;
- animated pickups;
- dropped clip/shotgun;
- противники в idle/chase/attack/death кадрах и на разной дистанции;
- Spectre.

**Вердикт 2026-08-02: SUCCESS.** Предметы и противники визуально стали лучше.
Главное преимущество — исходная форма и характер краёв текстур сохранены,
при этом качество изображения повысилось. Одновременно остаётся заметная
мягкость/размытие; это известное ограничение текущего EdgeMix 8×, которое
следует уменьшить при будущем улучшении алгоритма.

## Visual gate: first-person оружие

Перевести first-person weapon patches с общего Enhanced sprite path
(Super-xBR 4× + Sharpen) на EdgeMix 8×:

1. Отдельные `EnhancedWeapon8X` и `EnhancedJobKind.WeaponSprite` — сделано.
2. Явный weapon routing в `SpriteCache` (`GetWeapon`/регистрация lump) и
   `WeaponView.DrawPatch` — сделано.
3. EdgeMix 8× для idle/fire кадров и muzzle flash; Classic сохраняет native.
4. Нативные patch header dimensions/offsets остаются источником placement.
5. Hot-switch Classic ↔ Enhanced не должен менять положение оружия и не
   должен показывать stale Super-xBR cache entry (pipeline v3).
6. Standalone visual gate для fist, pistol, shotgun, chaingun, chainsaw,
   rocket launcher, plasma и BFG — закрыт.

**Вердикт 2026-08-02: SUCCESS.** First-person оружие и muzzle flash приняты
на EdgeMix 8×. Края и характер силуэта лучше Super-xBR 4×; известная
мягкость/размытие EdgeMix остаётся ограничением алгоритма, но не ломает
читаемость деталей и анимации.

## Риски

- 8× содержит в четыре раза больше texel, чем прежний 4× sprite.
- Полупрозрачная четырёхпиксельная граница проходит через cutout/MSAA
  тракт и должна быть оценена в реальном кадре.
- Фокусные тесты не заменяют полный suite и perf/memory baseline.
- First-person оружие занимает большую экранную площадь, поэтому мягкость,
  допустимая для world sprites, может оказаться слишком заметной.
