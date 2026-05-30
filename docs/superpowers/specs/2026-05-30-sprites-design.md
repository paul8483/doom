# Этап 5. Объекты и враги (спрайты) — дизайн

**Дата:** 2026-05-30
**Статус:** утверждён (brainstorming), готов к написанию плана реализации
**Предыдущие этапы:** 0–4 завершены (WAD-ридер, геометрия, игрок+коллизии, палитра+текстуры)

## Цель

Расставить на E1M1 (и любой карте `ExMy`) врагов, предметы и декорации как
камера-ориентированные спрайты-биллборды с корректной картинкой по углу обзора.
Видимый результат этапа: на уровне видны зомби, импы, бочки, аптечки, патроны,
броня, столбы/лампы и прочий декор — статично, но «как в DOOM» (правильная
ротация при обходе вокруг объекта, прозрачный фон, твёрдые объекты блокируют
игрока). Геометрия, текстуры и игрок из Stage 2–4 не затрагиваются.

## Объём этапа

**Входит:**
- Каталог спрайтов из маркеров `S_START`/`S_END`: парсинг имени лампа
  (`XXXX` + кадр + ротация, опционально вторая пара кадр+ротация = зеркальный
  лампа), lookup `(спрайт, кадр, ротация) → (lumpIndex, mirrored)`.
- Полная таблица `mobjinfo` из `info.c` (~130 типов): по `doomednum` отдаёт
  имя спрайта, стартовый кадр, `radius`, `height` (в DOOM-юнитах) и флаги
  (как минимум `SOLID`, `SPAWNCEILING`).
- Спрайты-биллборды: плоскость, всегда повёрнутая к камере вокруг оси Y
  (цилиндрический биллборд — классика DOOM).
- Выбор 1 из 8 ротаций в рантайме по углу между камерой и объектом с учётом
  угла объекта (`THINGS.angle`); зеркальные ротации.
- Спавн объектов из ламп `THINGS` в их координатах, привязка к полу
  (или к потолку для флага `SPAWNCEILING`).
- Прозрачный фон спрайтов: alpha-cutout (переиспользуем шейдер `Doom/UnlitCutout`
  из Stage 4).
- Твёрдые объекты: типы с флагом `SOLID` получают цилиндрический коллайдер,
  размер из `radius`/`height` таблицы × `worldScale`.

**Сознательно отложено (Stage 6 и далее):**
- Анимация кадров спрайтов (цикл состояний — `states`/`tics`).
- ИИ и движение врагов, реакция на урон, смерть.
- Подбор предметов (аптечки, патроны, ключи) и логика инвентаря.
- Спрайты для спавн-точек игрока (типы 1–4) и deathmatch-старта (11) — это
  точки появления, а не видимые объекты; они не порождают спрайтов.
- «Full bright» подсветка кадров (бит `0x8000` фрейма) — Unlit-шейдер и так
  без сценного света, яркость кадра визуально не нужна на этой стадии.

## Ключевые решения (из brainstorming)

1. **Объём таблицы типов:** переносим полную `mobjinfo` (~130 записей) из
   `info.c`, а не подмножество. Любая карта `ExMy` отрисуется корректно, и
   Stage 6 (ИИ/предметы) сразу получает нужную таблицу с `radius`/`height`/флагами.
2. **Ротации:** полный выбор из 8 в рантайме по углу обзора (+ зеркальные кадры
   формата `POSSA2A8`). Это ядро узнаваемого DOOM-вида.
3. **Коллизии:** твёрдость по флагу `SOLID` из таблицы; цилиндрический коллайдер
   по `radius`. Предметы и декор без флага проходимы. Подбор/триггеры — Stage 6.
4. **Где живёт код (ось 1 — вариант A):** декодинг спрайтов и каталог
   `S_START/S_END` → в существующий `Doom.Graphics` (спрайт = тот же DOOM picture
   format, переиспользуем `Patch.Decode`). Таблица `mobjinfo` → новый pure-C#
   asmdef `Doom.Things` (`noEngineReferences`) — это игровые данные, переиспользуются
   Stage 6. Биллборд, спавн и кэш материалов → `Doom.MapBuild`.
5. **Механизм биллборда (ось 2 — вариант A):** MonoBehaviour `SpriteBillboard`
   в `Update` — поворот к камере вокруг Y, выбор ротации, подмена материала.
   Прозрачно и тестируемо; кастомный вершинный шейдер-биллборд отвергнут как
   сложнее в отладке на этой стадии.
6. **Вертикальная привязка (ось 3 — вариант A):** raycast вниз по XZ объекта на
   уже существующие коллайдеры секторов → пол; для `SPAWNCEILING` — raycast вверх
   к потолку. Без point-in-polygon математики; робастно и переиспользует
   коллайдеры из Stage 2/3.
7. **Формат пикселей и фильтрация:** как в Stage 4 — декодер выдаёт `RGBA32`
   (`DecodedImage`), прозрачные пиксели патча → `alpha=0`; `Texture2D` с
   `FilterMode.Point` + mipmaps + anisotropic.

## Архитектура

### Новые / изменяемые сборки

**`Doom.Graphics` (есть, дополняется):**
- `SpriteSet` — каталог спрайтов. На `Load(wad)` читает диапазон между маркерами
  `S_START`/`S_END` (учитывая возможные `SS_START`/`SS_END`), для каждого лампа
  разбирает имя:
  - символы 0–3: имя спрайта (напр. `TROO`);
  - символ 4: кадр, индекс = `char - 'A'`;
  - символ 5: ротация `'0'..'8'`;
  - символы 6–7 (если есть): вторая пара кадр+ротация — тот же лампа отображается
    зеркально для этой (кадр, ротация).
  Хранит lookup `(спрайт, кадр, ротация) → SpriteFrameRef{ lumpIndex, mirrored }`.
  Ротация `0` означает «один кадр на все 8 углов». Декод картинки — через
  существующий `Patch.Decode(lump, palette)`.

**`Doom.Things` (новый, pure-C#, `noEngineReferences: true`, ссылок на Unity нет):**
- `ThingFlags` — `[Flags]` enum: `Solid`, `SpawnCeiling` (минимально необходимое;
  место под остальные флаги `MF_*` зарезервировано).
- `ThingDef` — `{ int DoomEdNum, string Sprite, int Frame, int Radius, int Height,
  ThingFlags Flags }`.
- `ThingTable` — статическая ported-таблица из `info.c`: массив `ThingDef`,
  индекс `Dictionary<int, ThingDef>` по `DoomEdNum`. Записи с `doomednum == -1`
  (не спавнятся с карты) не включаются. Метод `TryGet(int type, out ThingDef)`.
  Спрайт и стартовый кадр берутся из `spawnstate` соответствующего `mobjinfo`.

**`Doom.MapBuild` (есть, дополняется):**
- `SpriteCache` — `(spriteName, frame, rotation) → Material`, кэш по имени лампа.
  Внутри: `Patch.Decode → DecodedImage → Texture2D(Point+mip+aniso) → Material`
  на базе `Doom/UnlitCutout`. Помнит размер/смещения спрайта (`width`, `height`,
  `leftOffset`, `topOffset`) для якоря.
- `SpriteBillboard` (MonoBehaviour) — в `Update`: (1) поворот трансформа к камере
  только вокруг Y; (2) вычисление индекса ротации; (3) запрос материала+зеркальности
  из `SpriteCache`/`SpriteSet`, подмена `MeshRenderer.material`; (4) флип
  зеркальности через знак `localScale.x`.
- `ThingSpawner` — читает `MapData.Things`, для каждого: `ThingTable.TryGet(type)`;
  пропускает спавн-точки игрока/DM; создаёт GameObject (quad mesh + `MeshFilter` +
  `MeshRenderer` + `SpriteBillboard`); ставит в `(x, ?, y) × worldScale`, Y по
  raycast; при `Solid` добавляет `CapsuleCollider` (radius/height × worldScale).
- `MapLoader` (есть) — после построения геометрии вызывает `ThingSpawner`.

### Файловая структура

Новое:
- `Assets/Scripts/Graphics/SpriteSet.cs`
- `Assets/Scripts/Things/Doom.Things.asmdef`
- `Assets/Scripts/Things/ThingDef.cs`
- `Assets/Scripts/Things/ThingTable.cs`
- `Assets/Scripts/MapBuild/SpriteCache.cs`
- `Assets/Scripts/MapBuild/SpriteBillboard.cs`
- `Assets/Scripts/MapBuild/ThingSpawner.cs`
- `Assets/Tests/EditMode/Graphics/SpriteSetTests.cs`
- `Assets/Tests/EditMode/Things/Doom.Things.Tests.asmdef`
- `Assets/Tests/EditMode/Things/ThingTableTests.cs`
- `Assets/Tests/PlayMode/SpriteSpawnPlayTests.cs` (или дополнение существующего PlayMode-набора)

Изменяемое:
- `Assets/Scripts/MapBuild/Doom.MapBuild.asmdef` — добавить ссылку `Doom.Things`.
- `Assets/Scripts/MapBuild/MapLoader.cs` — вызов спавнера.

## Поток данных

```
MapData.Things  (есть, парсится из THINGS)
   └─ для каждого Thing {x, y, angle, type}:
        ThingTable.TryGet(type) → ThingDef{sprite, frame, radius, height, flags}
           ├─ нет записи или спавн-точка игрока (1..4, 11) → пропустить
           └─ создать GameObject:
                pos.xz = (Thing.x, Thing.y) × worldScale
                pos.y  = raycast вниз на коллайдеры секторов (вверх при SpawnCeiling)
                SpriteBillboard ← { ThingDef.sprite, ThingDef.frame, SpriteSet, SpriteCache }
                если ThingFlags.Solid → CapsuleCollider(radius×ws, height×ws)
```

В `SpriteBillboard.Update`:
```
dirToCam = camera.pos − this.pos        (проекция на плоскость XZ)
transform.rotation = LookRotation вокруг Y к камере
rot = ВыборРотации(Thing.angle, угол(this → camera))
ref = SpriteSet.Get(sprite, frame, rot)   // (lumpIndex, mirrored)
renderer.material = SpriteCache.Material(ref)
localScale.x = baseScaleX × (ref.mirrored ? −1 : +1)
```

## Детали реализации

### Имена спрайтов

Маркеры `S_START`/`S_END` (в IWAD также `SS_START`/`SS_END`); ламповые имена в
этом диапазоне — спрайты. Имя до 8 символов, ASCII, upper-case. Разбор:
`name[0..3]` = спрайт, `name[4]` = кадр (`A`+index), `name[5]` = ротация
(`'0'`–`'8'`). Если `name.Length >= 8`: `name[6]` = второй кадр, `name[7]` =
вторая ротация, и этот лампа регистрируется зеркально для второй пары. Ротация
`'0'` регистрируется как «все углы».

### Выбор ротации (8 направлений)

DOOM-углы: 0° = восток, растут против часовой. Для объекта с углом `objAngle`
и направлением «от объекта к камере» `viewAngle`:
```
diff = viewAngle − objAngle           // нормализовать в [0, 360)
rotIndex = floor((diff + 22.5) / 45) mod 8   // 0..7
spriteRotation = rotIndex + 1                 // '1'..'8'
```
Если у кадра есть только ротация `0` — всегда берём её (картинка на все углы).
Конкретное соответствие индексов и знак направления выверяются на реальном WAD
в тестах/визуально (E1M1: импы/зомби с разных сторон). Перевод DOOM-координат и
углов в Unity — по конвенциям, уже принятым в Stage 3 (`MapLoader`/`PlayerController`).

### Биллборд и якорь спрайта

- Цилиндрический биллборд: трансформ смотрит на камеру, но наклон гасится —
  вращение только вокруг мировой оси Y (спрайт остаётся вертикальным).
- Размер квадрата: `W = patch.width × worldScale`, `H = patch.height × worldScale`.
- Горизонтальный якорь: origin спрайта в DOOM — на `leftOffset` пикселей от левого
  края; центр квадрата смещается на `(width/2 − leftOffset) × worldScale`, чтобы
  origin попал в XZ объекта.
- Вертикальный якорь: верх спрайта на `topOffset` пикселей выше origin (ног).
  Для стоящего на полу объекта низ квадрата ≈ уровень пола; центр по Y =
  `floorY + (topOffset − height/2) × worldScale`. Для `SpawnCeiling` верх квадрата
  привязывается к потолку.
- Зеркальность: флип `localScale.x` (шейдер `Doom/UnlitCutout` — `Cull Off`,
  инверсия намотки не вредит).

### Материалы и прозрачность

Один `Material` на отдельный лампа-спрайт (по факту на (спрайт,кадр,ротация),
с учётом переиспользования зеркальных лампов), кэш в `SpriteCache` по имени лампа.
Текстура — `RGBA32`, `FilterMode.Point`, mipmaps on, anisotropic; шейдер
`Doom/UnlitCutout` (alpha-test, `Cull Off`) из Stage 4.

### Твёрдость

Типы с `ThingFlags.Solid` получают `CapsuleCollider` высотой `height × worldScale`
и радиусом `radius × worldScale`, центр на половине высоты от пола. Триггеры,
урон от бочек и подбор предметов — Stage 6.

## Тестирование

- **EditMode `Doom.Graphics.Tests` (SpriteSetTests):** на `SyntheticWadBuilder`
  с маркерами `S_START/S_END` и синтетическими лампами — парсинг простого имени
  (`TROOA1`), ротации `0` (все углы), двойной зеркальной пары (`POSSA2A8` → обе
  (кадр,ротация), вторая `mirrored=true`); lookup отсутствующего → нет записи.
  Интеграция на `freedoom1.wad`: известный спрайт (напр. `TROO`, `BON1`, `POSS`)
  резолвится в существующий лампа и декодится в непустой `DecodedImage`.
- **EditMode `Doom.Things.Tests` (ThingTableTests):** `TryGet` отдаёт верный
  `ThingDef` для пары известных `doomednum` (напр. имп `3001`, бочка `2035`,
  бонус здоровья `2014`); проверка флага `Solid` (бочка/столб — true; бонус —
  false); неизвестный тип → `false`; спавн-точки игрока (1) присутствуют в WAD,
  но спавнером отфильтровываются (проверяется логикой спавнера, см. PlayMode).
- **PlayMode (SpriteSpawnPlayTests):** загрузка E1M1 → спавнится `N > 0` объектов
  с `MeshRenderer` и `SpriteBillboard`; хотя бы один объект с флагом `Solid` имеет
  `CapsuleCollider`; ни один заспавненный объект не соответствует типу игрока
  (1–4, 11). Запуск без `-nographics` (как PlayMode Stage 3/4).

## Открытые риски / на проверку при реализации

- **Знак и базовый индекс ротации.** Точное соответствие `rotIndex → '1'..'8'`
  и знак `diff` зависят от конвенции углов DOOM↔Unity, принятой в Stage 3.
  Выверяется визуально на E1M1 (обойти импа: спереди/сбоку/сзади разные кадры) и
  фиксируется тестом-регрессией, если найдётся стабильная проверка.
- **Маркеры спрайтов.** Во Freedoom возможны `SS_START/SS_END` или вложенные
  маркеры; `SpriteSet.Load` должен корректно определить диапазон (свериться через
  `Tools > Doom > Dump freedoom1.wad`).
- **Перенос `mobjinfo`/`states`.** Источник — `info.c`/`info.h` оригинального
  DOOM (формат и логика портируются по правилам проекта; рендер/BSP — нет).
  Это объёмная, но механическая таблица; стартовый спрайт+кадр берутся из
  `spawnstate`. Проверить покрытие типов, реально встречающихся в E1M1.
- **Якорь по offset.** Формула вертикального якоря выверяется на «висящих» и
  «стоящих» объектах (труп на полу vs подвешенный труп `SpawnCeiling`).

## Связанные документы

- Мастер-план: `docs/doom-unity-remake-plan.md` (Этап 5).
- План реализации (HOW, по задачам): `docs/superpowers/plans/2026-05-30-sprites.md`
  (будет создан skill'ом writing-plans).
- Предыдущий этап: `docs/superpowers/specs/2026-05-30-textures-design.md`.
