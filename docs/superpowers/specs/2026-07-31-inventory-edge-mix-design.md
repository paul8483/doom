# Sprite EdgeMix 8× — дизайн

**Дата:** 2026-07-31  
**Ветка:** реализация предметов начата в `inventory-redraw` от `main`
(`a11a35f`), затем перенесена в `main` и расширена на противников и
first-person оружие.  
**Статус:** 🟢 EdgeMix 8× для предметов, противников и FP-оружия закрыт
2026-08-02 (код + full suites + Windows standalone + interactive visual
gates). Warm-perf baseline — ещё не снят.  
**Этап 2 (contrast-gated EdgeMix):** ✅ закрыт 2026-08-03. Gate 0 по
PNG-свипу (native | EdgeMix 8× | 16→64 | 32→112 | 64→176) выбрал
агрессивную точку **16→64** («крест чёткий»); runtime переведён на
`Scale8XContrastGated`, pipeline **v4**; standalone visual gate —
SUCCESS («всё хорошо, оставляем как есть»). Suites: EditMode
**616/616**, PlayMode **149/149**. Warm-perf baseline по-прежнему
не снят (замеры 2026-08-03 в
`Logs/enhanced-texture-quality-baseline-notes.md` сделаны до v4).

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

## Этап 2: Contrast-gated EdgeMix (дизайн, 2026-08-03)

**Статус:** 🔵 запланирован. Реализация не начата.

### Диагноз остаточной смазанности

Принятый EdgeMix 8× смешивает **безусловно**: любая граница двух разных
texel'ей получает 4-пиксельную полосу `AAAAAA|MMMM|BBBBBB` независимо от
цветовой дистанции. Для близких цветов (тени, переходы оттенков внутри
одной поверхности) это работает как задумано. Но намеренные контрастные
рёбра детали получают половинчатый средний цвет, который глаз читает как
мыло. Наблюдаемые примеры (интерактивно, 1.0.2):

- белый крест аптечки (`MEDIA0`/`STIMA0`) на контрастном фоне — при
  увеличении разрешения крест должен оставаться белым с резкой границей;
- яркий рубин/самоцвет на спрайте брони — насыщенный цвет на тёмном фоне
  размывается полосой смешивания.

Чем контрастнее деталь, тем заметнее полоса — алгоритм сильнее всего
портит именно то, за что цепляется взгляд. Направление согласуется с
уроком ESRGAN/MMPX-экспериментов: выигрывает не «более сильная»
интерполяция, а селективность — знать, где сглаживать, а где обязан
остаться жёсткий пиксель.

### Алгоритм

`EdgeMixUpscaler` дополняется гейтом по цветовой дистанции на стыке
непрозрачных texel'ей:

1. Дистанция соседей — взвешенный RGB (0.30/0.59/0.11), те же константы,
   что в `DeditherFilter`.
2. **Близкие цвета** → текущая 4-пиксельная полоса смешивания (поведение
   принятого EdgeMix 8× сохраняется).
3. **Контрастные цвета** → полоса сужается вплоть до нуля; блок
   вырождается в чистый nearest 8× — ребро остаётся жёстким.
4. **Рампа вместо жёсткого порога.** Урок дедизеринга: на Freedoom у
   порогов часто нет рабочей середины. Жёсткий T даст рваный вид на
   градиентных рампах (соседние ступени по разные стороны порога).
   Вес смешивания и/или ширина полосы (4→2→0 output-пикселей) убывают
   плавной функцией дистанции; границы рампы — калибруемые константы.
5. **Силуэт не гейтится.** Alpha-переход в прозрачность (premultiplied
   смешивание) — главный принятый выигрыш EdgeMix 8× и остаётся без
   изменений. Гейт применяется только к стыку двух непрозрачных texel'ей;
   ветки alpha-перехода и цветового перехода разделяются в коде.
6. **Пересечения границ** (усреднение четырёх texel'ей в углах) гейтятся
   по максимальной попарной дистанции участников — иначе угол креста
   замылится даже при загейченных прямых рёбрах.

Алгоритм остаётся deterministic pure-C# в `Doom.Graphics`, без neural
inference и без изменения patch header placement.

### Gate 0 (offline-превью до интеграции)

По проектному паттерну (ESRGAN/MMPX): свип параметров рампы офлайн через
превью-инструментарий (`Tools > Doom > Dump Sprite Preview`-семейство) на
проблемных патчах **до** любого изменения runtime:

- `MEDIA0`/`STIMA0` (крест) — целевой кейс контрастного ребра;
- броня с самоцветом (`ARM1`/`ARM2`/`BON2`) — целевой кейс;
- ключи (мелкие контрастные детали);
- 1–2 кадра монстров и одно FP-оружие — контроль «не сломали то, что
  стало хорошо» (силуэты и мягкие внутренние переходы).

Формат — триптихи: Classic native / принятый EdgeMix 8× / gated EdgeMix.
Stop condition: если ни одна точка свипа не даёт глазами «крест резче,
силуэт и мягкие переходы не хуже» — этап останавливается без изменения
runtime, вердикт архивируется.

### Интеграция (только после успеха Gate 0)

- `EnhancedPipelineVersion` поднимается до **4** — session/disk cache
  ключуется версией; без бампа hot-switch покажет stale-результаты
  прежнего EdgeMix (риск уже проявлялся на weapon routing).
- Variants/job kinds (`EnhancedPickup8X`/`EnhancedEnemy8X`/
  `EnhancedWeapon8X`) и routing не меняются — меняется только transform.
- Обновляются golden-фикстуры EditMode (контрастное ребро → nearest,
  близкие цвета → полоса, угловой кейс, alpha-силуэт без изменений).
- Финальный интерактивный visual gate в standalone: аптечки, броня,
  ключи, монстры, FP-оружие — Classic ↔ Enhanced.

### Смежная мера (отдельно от алгоритма)

Часть воспринимаемого мыла — сэмплинг при отрисовке, не текстура. На
ветке `esrgan` остался невлитый коммит `4995f50` («stabilize crisp
Enhanced sprite rendering»): texel-aware sampling в
`DoomEnhancedSprite.shader` + снап UI placement к пиксельной сетке.
Оценить cherry-pick отдельным шагом — он бьёт по второй причине мыла
независимо от гейта и вместе с ним закрывает обе.

**Результат (2026-08-03):** влит в `main` как `bb7075e`; focused
PlayMode 25/25; интерактивный вердикт **нейтральный** («не сильно
лучше, но и не хуже»). Оставлен ради нефизуальных плюсов (фикс гонки
Enhanced-boot, снап placement, стабильные тесты). Важный вывод:
сэмплинг не был главным источником мыла — остаточная мягкость запечена
в самой EdgeMix-текстуре, что подтверждает ставку этапа 2 на
contrast-гейт.

### Риски этапа 2

- Рампа с неудачными границами даст «пятнистость»: часть внутренних
  границ мягкая, часть резкая на одном спрайте (парируется свипом Gate 0
  и контрольными спрайтами).
- Ожесточение внутренних рёбер может вернуть «лесенки» на диагональных
  контрастных деталях — оценивать на диагоналях (рубин, ключи).
- Изменение golden-фикстур должно быть осознанным: старые ожидания
  безусловного смешивания перестанут проходить по построению.
