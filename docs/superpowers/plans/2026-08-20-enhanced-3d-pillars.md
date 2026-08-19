# Enhanced 3D зелёные колонны — план

**Спека:** `docs/superpowers/specs/2026-08-20-enhanced-3d-pillars-design.md`  
**Ветка:** `3d-toggle`  
**Статус:** ✅ закрыт 2026-08-20 (гейт «выглядит хорошо»)

## Урок волны (случился до Task 1)

Первая сборка была сделана токаркой — «колонна же тело вращения» — без
согласования метода, и откатана целиком (ассеты, рантайм, тесты, билд, доки).
Пользователь: «уже не одобрили стойки факелов и опять ты налезаешь на те же
грабли». Зафиксировано правилом: **объекты делаем через TRELLIS, вычисленная
геометрия — редкое исключение, и выбирает его пользователь.** Только огонь
остаётся всегда вычисляемым (градиент с ядром внутри объёма не запекается —
волны файербола и факелов).

## Task 1 — conditioning ✅

- `dump_trellis_native_inputs.py COL1 A` / `COL2 A` → канвы; плюс нативные
  кропы `NativeInputs/<LUMP>-crop.png` под `--tone-image`.
- Промпт одним набором из двух, с явной привязкой «имя файла → результат»
  (правило [[hint-prompt-name-native-files]]); в тексте — объёмная подача,
  эллипс верха, каннелюры и черепа по всей окружности, единый материал/свет
  на обе картинки. Эскизы v2 пришли с сохранённой идентичностью с первого
  раза; Space вернул обе GLB без переролов.

## Task 2 — doomify ✅

```
python Tools/convert_trellis_glb_to_obj.py --glb Textures/Trellis2/GLB/<LUMP>_*.glb \
    --out-dir Assets/Resources/ExperimentalPickups/<LUMP> --lump <LUMP>
python Tools/doomify3d.py --lump <LUMP> --tone-image Textures/Trellis2/NativeInputs/<LUMP>-crop.png
```

- COL1 96 910 → 40 000 tris, COL2 97 059 → 39 999; альбедо 1024 → 256 px,
  18 цветов; тон в пределах пары единиц от спрайта (без `--tone-image` бейк
  приходит выцветшим — находка стоек 2026-08-19 подтвердилась).
- Doomify пишет в `Logs/doomify3d/<LUMP>/` — доумифицированные obj+albedo
  копируются в Resources ПОВЕРХ сырых; `mtllib` внутри OBJ он переписывает
  сам. Meta клонированы с `COLUA0` (Point + uncompressed), GUID из md5 пути.

## Task 3 — рантайм ✅

- `ExperimentalPickupModel`: кейсы 30/31 в `TryGetResource` + визуальные
  высоты в `SpriteHeightPx` (56/41 px — патч, не коллизия 48/36). Всё
  остальное (unlit-шейдер, каскад, hot-switch, событийная видимость)
  наследуется от маршрута лампы/деревьев; `ThingSpawner` не менялся.

## Task 4 — тесты ✅

- EditMode `PillarAssetTests` ×3: цепочка OBJ → MTL → albedo (ловушка белых
  трупов POSS 2026-08-16), загрузка через Resources, константы высот против
  WAD-патчей + спрайт вещи из `ThingTable`.
- PlayMode: колонны добавлены в табличный тест декораций
  (`PickupPlayTests.Experimental meshes…`) — аттач, высота/низ по патчу,
  переключение презентации.
- Полные сьюты: EditMode **651/651**, PlayMode **187/187**.

## Task 5 — билд и гейт ✅

- Windows build Success **326.1 MB** (+2 MB за два меша).
- Панель было/стало `Logs/pillar-gate/pillar-bylo-stalo.png` (натив |
  yaw 0 | yaw 35) отправлена перед проверкой (правило
  [[visual-verdict-workflow]]).
- Интерактивный гейт в standalone, слот 1: **SUCCESS** — «проверил в игре,
  выглядит хорошо».

## Не в волне

- Красная `COL3` (32, тот же зал E1M5): одна строка в allowlist + константа
  высоты (55 px) + свой conditioning-пит.
- `COL5` (36, E1M7, «сердце»): анимируется A/B — потребует правила покрытия
  «все кадры или ничего», как у факелов.
