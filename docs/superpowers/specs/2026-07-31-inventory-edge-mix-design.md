# Inventory EdgeMix 8× — дизайн

**Дата:** 2026-07-31  
**Ветка:** `inventory-redraw` от `main` (`a11a35f`)  
**Статус:** 🟡 runtime-интеграция и Windows build готовы; интерактивный
visual gate в standalone ещё не закрыт.

## Цель и границы

Улучшить только спрайты предметов, лежащих в мире: здоровье, патроны,
броня, ключи, бонусы/power-up и world weapon pickups.

Не меняются:

- стены, полы, потолки и sky;
- монстры, projectiles и эффекты;
- first-person оружие;
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

- `ThingSpawner` и `PickupFactory` помечают только pickup billboards.
- `SpriteCache` регистрирует соответствующие lump indices.
- В Enhanced pickup получает `EnhancedPickup8X` и
  `EnhancedJobKind.PickupSprite`.
- Остальные sprite lumps продолжают использовать принятый
  Super-xBR 4× + Sharpen.
- В Classic pickup возвращается к native texture.
- Patch header width/height/offsets остаются нативными, поэтому размер
  и положение предмета в мире не меняются.
- Session/disk cache разделён по job kind; pipeline version поднята до 2.

## Проверки на 2026-07-31

- EdgeMix + Enhanced runner: **15/15 EditMode PASS**.
- Enhanced cache codec: **9/9 EditMode PASS**.
- Sprite upscale fixture: **8/8 PlayMode PASS**.
- Проверено: pickup = 8×, monster = 4×, native header placement,
  Classic fallback, hot switch и cache codec.
- Windows standalone: **SUCCESS**, около 128 MB:
  `Builds/Windows/DoomUnity.exe`.
- Build log: `Logs/edge-mix-build-windows.log`.

Это фокусные проверки, не полный прогон 608 EditMode / 146 PlayMode.

## Локальные visual artifacts

Каталоги в `Logs/` (gitignored):

- `inventory-redraw-gate0/` — shape-redraw V2;
- `inventory-edge-mix-gate0/` — EdgeMix 4× reject (слишком размыт);
- `inventory-edge-mix-8x-gate0/` — текущий кандидат.

## Открытый visual gate

В standalone сравнить Classic ↔ Enhanced на E1-картах:

- аптечки и health bonuses;
- clip/shells/ammo boxes;
- armor и armor bonuses;
- keys;
- animated pickups;
- dropped clip/shotgun.

Проверить читаемость на близкой и средней дистанции, отсутствие
тёмного halo, дрожания анимации и чрезмерной мягкости. До явного
пользовательского вердикта алгоритм остаётся экспериментальным.

## Риски

- 8× содержит в четыре раза больше texel, чем прежний 4× pickup.
- Полупрозрачная четырёхпиксельная граница проходит через cutout/MSAA
  тракт и должна быть оценена в реальном кадре.
- Фокусные тесты не заменяют полный suite и perf/memory baseline.
