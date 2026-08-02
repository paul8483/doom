# Sprite EdgeMix 8× — дизайн

**Дата:** 2026-07-31  
**Ветка:** реализация предметов начата в `inventory-redraw` от `main`
(`a11a35f`), затем перенесена в `main` и расширена на противников.  
**Статус:** 🟢 EdgeMix 8× для предметов и противников реализован; standalone
visual gate закрыт успешно 2026-08-02. Следующая стадия — first-person оружие.

## Цель и границы

Улучшить спрайты с прозрачным силуэтом, сохранив исходный pixel-art характер
краёв:

- предметы, лежащие в мире: здоровье, патроны, броня, ключи,
  бонусы/power-up и world weapon pickups;
- противники, включая их анимационные кадры и Spectre;
- на следующей стадии — first-person оружие и muzzle flash.

Не меняются:

- стены, полы, потолки и sky;
- projectiles и world effects;
- first-person оружие до завершения следующей стадии;
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
- `SpriteCache` регистрирует pickup/enemy lump indices.
- В Enhanced pickup получает `EnhancedPickup8X` и
  `EnhancedJobKind.PickupSprite`.
- В Enhanced enemy получает `EnhancedEnemy8X` и
  `EnhancedJobKind.EnemySprite`.
- Остальные sprite lumps продолжают использовать принятый
  Super-xBR 4× + Sharpen.
- В Classic pickup и enemy возвращаются к native texture.
- Patch header width/height/offsets остаются нативными, поэтому размер
  и положение спрайта в мире не меняются.
- Session/disk cache разделён по job kind; pipeline version поднята до 2.

## Проверки на 2026-07-31

- EdgeMix + Enhanced runner: **15/15 EditMode PASS**.
- Enhanced cache codec: **9/9 EditMode PASS**.
- Sprite upscale fixture: **8/8 PlayMode PASS**.
- На исходной pickup-стадии проверено: pickup = 8×, monster = 4×,
  native header placement, Classic fallback, hot switch и cache codec.
- 2026-08-02 добавлены отдельные enemy variant/job kind, routing и тестовое
  покрытие; противники переведены с Super-xBR 4× на EdgeMix 8×.
- Windows standalone: **SUCCESS**, около 128 MB:
  `Builds/Windows/DoomUnity.exe`.
- Build log: `Logs/edge-mix-build-windows.log`.

Это фокусные проверки, не полный прогон 608 EditMode / 146 PlayMode.

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

## Следующая стадия: first-person оружие

Перевести first-person weapon patches с общего Enhanced sprite path
(Super-xBR 4× + Sharpen) на EdgeMix 8×. Стадия включает:

1. Отдельные `EnhancedWeapon8X` и `EnhancedJobKind.WeaponSprite`, чтобы
   weapon lumps не смешивались с pickup/enemy/cache entries.
2. Явный weapon routing в `SpriteCache` (`GetWeapon`/регистрация lump) вместо
   текущего общего `Get`, вызываемого из `WeaponView.DrawPatch`.
3. EdgeMix 8× для idle/fire кадров и muzzle flash; Classic сохраняет native.
4. Нативные patch header dimensions/offsets остаются источником placement,
   поэтому размер, bob, lowering, clipping над status bar и анимационные
   тайминги не меняются.
5. Hot-switch Classic ↔ Enhanced не должен менять положение оружия и не
   должен показывать stale Super-xBR cache entry.
6. Unit/codec/routing проверки и отдельный standalone visual gate для fist,
   pistol, shotgun, chaingun, chainsaw, rocket launcher, plasma и BFG.

Критерий visual gate: края и внутренние детали оружия выглядят лучше
Super-xBR 4×, muzzle flash не получает halo, а известная мягкость EdgeMix
не ухудшает читаемость мелких деталей и анимации. Если размытие окажется
заметнее выигрыша, weapon routing не принимается до улучшения алгоритма.

## Риски

- 8× содержит в четыре раза больше texel, чем прежний 4× sprite.
- Полупрозрачная четырёхпиксельная граница проходит через cutout/MSAA
  тракт и должна быть оценена в реальном кадре.
- Фокусные тесты не заменяют полный suite и perf/memory baseline.
- First-person оружие занимает большую экранную площадь, поэтому мягкость,
  допустимая для world sprites, может оказаться слишком заметной.
