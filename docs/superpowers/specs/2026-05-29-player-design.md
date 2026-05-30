# Этап 3. Игрок и коллизии — дизайн

**Дата:** 2026-05-29
**Статус:** выполнено
**План реализации:** docs/superpowers/plans/2026-05-29-player.md

## Цель

Поставить игрока на карту E1M1 и дать ему ходить от первого лица с коллизиями.
Конкретно: спавнить игрока в стартовой позиции из `THINGS`, дать управление от
первого лица (WASD + полная мышь + Shift-бег), использовать `CharacterController`
для столкновений с уже существующими `MeshCollider`-ами Stage 2. Видимый результат
этапа — Play mode по E1M1 от первого лица: игрок стоит на полу первой комнаты,
ходит, упирается в стены, падает под гравитацией.

## Контекст

Stage 2 закрыт: пайплайн `MapData.Load` → `MapGeometryBuilder.Build` →
`MapLoader` строит серую геометрию E1M1, `MeshCollider`-ы навешаны на каждый
кусок. 62 EditMode-теста проходят. Сейчас в Play mode видна карта сверху
(`autoFitCamera` в `MapLoader`), но игрока нет — это и есть пробел, который
закрывает Stage 3.

## Объём этапа

**Входит:**
- Парсинг lump'а `THINGS` в `Doom.Map` (все 10-байтные записи).
- Спавн игрока из `Thing.Type == 1` (Player 1 start) с учётом угла.
- Управление от первого лица: WASD (walk/strafe), полная мышь (yaw + pitch),
  Shift (sprint, hold).
- Коллизии через `CharacterController`: стены, пол, гравитация, авто-подъём по
  ступенькам (step offset).
- Масштаб мира `worldScale = 1/32`, проброшенный через `MapGeometryBuilder` так,
  что `MeshData` уже выходит в метрах.
- `MapLoader` инстанцирует игрока с `CharacterController` + камерой от первого
  лица в точке Player 1 Start.

**Сознательно отложено:**
- Точное размещение игрока в реальном секторе (point-in-polygon) — вместо этого
  игрок «роняется сверху» гравитацией. Точное определение стартового сектора —
  при необходимости в Stage 6.
- Headbob, дыхание камеры, recoil — игровые ощущения Stage 5/6/7.
- Прыжок и присед — оригинальный DOOM их не имел, не добавляем.
- Аудио шагов / удара об стену — Stage 6 (звуки из WAD).
- Конфиг ребайнда и полноценное Pause-меню — Stage 7.
- Стрельба, инвентарь, здоровье — Stage 6.
- Парсинг THINGS-типов кроме `1` (монстры, предметы) — Stage 5/6.
- Текстуры (пол/стены остаются серыми) — Stage 4.

## Ключевые решения (зафиксированы на брейншторме)

1. **`CharacterController`, не Rigidbody.** DOOM-игроку не нужна физика твёрдого
   тела (вращения, отскоки); ему нужно ходить, упираться в стены, падать под
   гравитацией и подниматься по ступенькам. Это ровно профиль
   `CharacterController`.
2. **`worldScale = 1f / 32f` пробрасывается в генерацию `MeshData`** (внутри
   `Doom.Map`), а не на уровне корневого `Transform`. Переделываются сигнатуры
   `MapGeometryBuilder.Build`, `SectorTriangulator.TriangulateFloor/Ceiling`,
   `WallMeshBuilder.BuildForSector`. Значение по умолчанию `1f` сохраняет
   совместимость со Stage 2 тестами. 32 DOOM-юнита = 1 метр; игрок ~1.75 м;
   gravity = -9.81 m/s² работает естественно.
3. **Новый Input System (`com.unity.inputsystem`), `InputActionMap` собирается в
   коде** в `PlayerController.Awake()` — БЕЗ `.inputactions`-ассета. Меньше
   зависимости от asset-импорта Unity, проще для headless-исполнения.
   `activeInputHandler` переводится на «Both» (значение 2), чтобы старый
   `Input` API (редакторное меню Stage 1) не сломался.
4. **THINGS scope:** парсятся ВСЕ записи, но `MapLoader` использует только
   `Thing.Type == 1`.
5. **Movement feel — modern FPS:** полная мышь (yaw + pitch, clamp ±85°), WASD,
   Shift для бега. Скорости: walk = 200 DOOM/s × 1/32 ≈ 6.25 m/s;
   run = 440 DOOM/s × 1/32 ≈ 13.75 m/s.
6. **Размеры `CharacterController`:** height = 56/32 = 1.75 м, radius = 16/32 =
   0.5 м, stepOffset = 24/32 = 0.75 м (DOOM step-up auto-climb), slopeLimit = 45°.
   Камера-пивот — дочерний `Transform` на Y = 41/32 ≈ 1.28 м (DOOM eye height).
7. **Спавн «уронить сверху»:** игрок инстанцируется на высоте `bounds.max.y + 5`,
   гравитация роняет его на пол. Так избегается point-in-polygon для определения
   стартового сектора.
8. **DOOM angle → Unity yaw:** DOOM angle 0 = east (+X), 90 = north (+Y DOOM →
   +Z Unity); Unity yaw 0 = +Z (forward). Конверсия: `unityYaw = 90f - doomAngle`.

## Архитектура

Чистая часть (`Doom.Map`, `noEngineReferences`) расширяется парсером `THINGS` и
параметром `worldScale` в пайплайне геометрии. Unity-сторона (`Doom.MapBuild`)
получает новый `PlayerController` MonoBehaviour и логику спавна в `MapLoader`.

```
WAD → MapData.Load → Things (THINGS lump → Thing[])
MapGeometryBuilder.Build(map, worldScale = 1/32) → MeshData уже в метрах
MapLoader → находит Thing.Type == 1 → инстанцирует Player
Player: CharacterController + дочерняя камера (eye height) + PlayerController
PlayerController (Doom.MapBuild) → InputActionMap (Move/Look/Sprint) собран в коде
```

THINGS парсятся в чистом `Doom.Map`; спавн и управление — в Unity-связанном
`Doom.MapBuild`. `MapLoader.SpawnPlayer` идёт после построения геометрии, чтобы
`bounds.max.y` был известен. `autoFitCamera`/«вид сверху» из Stage 2 убирается —
камера теперь у игрока.

### Формат лампа THINGS (краткая справка)

`THINGS` идёт первым лампом после маркера карты в каноническом порядке. Каждая
запись — 10 байт:

| Смещение | Размер | Поле    | Тип |
|----------|--------|---------|-----|
| 0        | 2      | `x`     | int16 LE — DOOM X-координата |
| 2        | 2      | `y`     | int16 LE — DOOM Y-координата |
| 4        | 2      | `angle` | uint16 LE — 0..359° (player starts обычно с шагом 45°) |
| 6        | 2      | `type`  | uint16 LE — `1` = Player 1 start, `2`/`3`/`4` = co-op, `11` = deathmatch, тысячи = монстры/предметы |
| 8        | 2      | `flags` | uint16 LE — bitmask (easy/medium/hard/multiplayer/deaf) |

В Stage 3 интересует исключительно `type == 1`.

## Тестирование

- **EditMode `Doom.Map`** (синтетика через `SyntheticMapBuilder`):
  - парсинг `THINGS` в записи (координаты / угол / тип / флаги);
  - размер лампа не кратен 10 → предупреждение, а не исключение;
  - `THINGS` как пятый обязательный лампа в `MapData.Load`;
  - проброс `worldScale` через `SectorTriangulator` / `WallMeshBuilder` /
    `MapGeometryBuilder` (масштаб вершин и высот).
- **EditMode интеграция на `freedoom1.wad`:** E1M1 содержит ровно один
  Player 1 start (`Thing.Type == 1`).
- **PlayMode** (новая сборка `Doom.Stage3.PlayTests`): игрок спавнится, падает
  под гравитацией и приземляется на пол E1M1, не проваливаясь. Запускать БЕЗ
  `-nographics` — PhysX не отдаёт collision callbacks без реального/нулевого
  gfx-устройства, и `cc.isGrounded` остаётся false.
- Существующие тесты Stage 1–2 не сломаны (default `worldScale = 1f` сохраняет
  поведение).

## Затронутые файлы (высокоуровнево)

- `Packages/manifest.json` — добавить `com.unity.inputsystem`.
- `ProjectSettings/ProjectSettings.asset` — `activeInputHandler: 2` (Both).
- `Assets/Scripts/Map/MapData.cs` — struct `Thing`, `ParseThings`, `Things`
  property, чтение `THINGS` в `Load`.
- `Assets/Scripts/Map/MapGeometryBuilder.cs`, `SectorTriangulator.cs`,
  `WallMeshBuilder.cs` — параметр и проброс `worldScale`.
- `Assets/Scripts/MapBuild/Doom.MapBuild.asmdef` — ссылка `Unity.InputSystem`.
- `Assets/Scripts/MapBuild/PlayerController.cs` — новый MonoBehaviour
  (управление от первого лица, Input Actions в коде).
- `Assets/Scripts/MapBuild/MapLoader.cs` — поле `worldScale`, спавн игрока.
- `Assets/Tests/EditMode/Map/` — тесты THINGS, worldScale, Freedoom-интеграция;
  обновление вызовов `new MapData(...)` под новый конструктор.
- `Assets/Tests/PlayMode/` — новая сборка `Doom.Stage3.PlayTests` и тест
  приземления на пол.
- `docs/doom-unity-remake-plan.md`, `CLAUDE.md` — отметить этап завершённым.

## Definition of done / результат

- Игрок спавнится на E1M1 в точке Player 1 Start; ходит WASD, смотрит мышью,
  бегает Shift; упирается в стены, стоит на полу, падает под гравитацией.
- `worldScale = 1/32` делает игрока ~1.75 м, gravity = -9.81 m/s² работает
  естественно; камера от первого лица на eye height.
- EditMode-тесты на THINGS + worldScale + Freedoom-интеграцию проходят;
  PlayMode-тест подтверждает приземление на пол.
- `Doom.Map` остаётся `noEngineReferences: true`; `THINGS`-парсер живёт там.
- Документы (`docs/doom-unity-remake-plan.md`, `CLAUDE.md`) отражают новое
  состояние.

## Граничные случаи

- Нет `Thing.Type == 1` → лог-предупреждение, игрок спавнится в (0, top, 0).
- Несколько `type == 1` (не должно быть) → берётся первый.
- Карта без геометрии (`bounds.HasValue == false`) → спавн в (0, 5, 0) +
  предупреждение.
- Изменение конструктора `MapData(..., things)` ломает Stage 2 тесты —
  все вызовы `new MapData(...)` нужно обновить (см. план).
- Gravity-drop: за один кадр 60 Hz при terminal velocity ~10 м/с смещение
  ~0.16 м — меньше половины высоты `CharacterController`, пол не проскакивается.
