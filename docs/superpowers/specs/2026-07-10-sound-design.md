# Этап 6f. Звук и музыка — дизайн

**Дата:** 2026-07-10
**Статус:** реализовано (Tasks 1–12); Freedoom `D_*` — SMF MIDI (авто-детект в `MusicScore`)
**Предыдущие этапы:** 0–5 и под-этапы 6a–6e завершены
**Контекст:** финальный под-этап Stage 6 (игровая логика). Разрез 6a–6f:
двери/секторы → урон/HP → оружие → ИИ → предметы → **звук**.

> **Уточнение реализации:** `freedoom1.wad` хранит музыку как Type-1 MIDI
> (`MThd`), а не DMX MUS. Парсер принимает оба формата; OPL-путь через
> `GENMIDI` общий.

## Цель

Озвучить уже работающий игровой цикл данными самого WAD: оружие стреляет,
предметы подбираются со звуком, игрок и монстры кричат/получают урон/умирают,
двери и лифты слышны в мире, а при загрузке уровня играет его музыка.

Видимая (и слышимая) веха на E1M1: после загрузки начинается `D_E1M1`;
пистолет, дробовик и кулак звучат по-разному; первая комната просыпается с
криками монстров; атаки, боль и смерти озвучены; дверь открывается из точки
сектора; аптечка даёт короткий UI-звук. Все данные берутся из
`freedoom1.wad` — WAV, MIDI, SoundFont и вручную записанных клипов в проекте
не появляется.

## Объём этапа

**Входит:**
- **DMX sound effects (`DS*`)** — декодирование 8-байтового заголовка
  format 3 и unsigned 8-bit mono PCM с исходной частотой (обычно 11025 или
  22050 Гц). Некорректные/обрезанные lumps отклоняются предсказуемо.
- **Кэш Unity-клипов** — `DS*` превращаются в `AudioClip` пока WAD открыт.
  После выхода из `MapLoader.Build()` проигрывание не обращается к
  `WadFile`.
- **Центральное воспроизведение** — ограниченный пул `AudioSource`, 2D-звуки
  игрока/UI и 3D-звуки объектов мира, линейное затухание на DOOM-дистанциях.
- **Оружие:** кулак `DSPUNCH`, пистолет/пулемёт `DSPISTOL`, дробовик
  `DSSHOTGN`; звук выстрела совпадает с существующим событием `Fired`.
- **Предметы:** обычные вещи `DSITEMUP`, новое оружие `DSWPNUP`, powerup
  `DSGETPOW`. Звук возникает только после успешного `ItemRules.TryPickup`.
- **Игрок:** боль `DSPLPAIN`, смерть `DSPLDETH`/доступный death-вариант,
  отказ запертой двери `DSNOWAY` с безопасным fallback на `DSOOF`.
- **Монстры E1 (POSS/SPOS/TROO/SARG):** sight, active, attack, pain и death
  из таблиц `info.c`; варианты sight/death выбираются `DoomRandom`.
  Фаербол импа имеет launch `DSFIRSHT` и impact `DSFIRXPL`.
- **Секторы:** двери `DSDOROPN`/`DSDORCLS`, лифты и полы — зацикленный
  `DSSTNMOV` во время движения и `DSPSTOP` при остановке; switch —
  `DSSWTCHN`.
- **Музыка:** MUS-lump текущей карты (`E1M1` → `D_E1M1`) проигрывается в
  цикле. MUS-события и инструменты `GENMIDI` управляют OPL-синтезатором;
  PCM генерируется потоково в Unity. Это WAD-only путь без внешнего
  SoundFont.
- **Громкости:** отдельные сериализуемые `sfxVolume` и `musicVolume`;
  полноценное меню настроек остаётся Stage 7.
- **Отказоустойчивость:** отсутствующий отдельный SFX не ломает карту —
  предупреждение один раз и тишина; отсутствие/ошибка `D_<MAP>` или
  `GENMIDI` отключает только музыку.

**Сознательно отложено:**
- Точная эмуляция восьми DMX-каналов, приоритетов `S_sfx[]`, случайного
  pitch-shift и channel stealing оригинала.
- Секторная окклюзия слышимого звука, `ML_SOUNDBLOCK` и реверберация.
  `NoiseAlert` остаётся геймплейной заливкой пробуждения, а не аудиомикшером.
- Доплер, HRTF-настройка и ручной stereo-pan по углу DOOM.
- Полный звуковой набор механик, не входивших в исходный scope 6f: телепорты,
  crusher, exit, секреты и projectile weapons. Chainsaw/rocket позднее
  подключены расширениями 6c; plasma/BFG будут подключены по
  `docs/superpowers/specs/2026-07-11-plasma-bfg-design.md`.
- Шаги игрока: в оригинальном DOOM обычных footstep SFX нет; добавлять
  современный звук ходьбы без WAD-источника не будем.
- Музыкальные backend'ы General MIDI/GUS. На этом этапе фиксируется один
  переносимый OPL-путь через `GENMIDI`.
- Меню громкости, mute/pause UI и сохранение настроек — Stage 7.

## Ключевые решения

1. **Две независимые подсистемы:** короткие `DS*` — заранее декодированные
   `AudioClip`; музыка — потоковый MUS/GENMIDI/OPL-синтез. MUS не является
   аудиофайлом и Unity не умеет играть его напрямую.
2. **Новый pure-C# слой `Doom.Audio`:** форматы WAD и музыкальный sequencer
   тестируются без Unity. `AudioClip`, `AudioSource` и spatial audio живут
   только в `Doom.MapBuild`.
3. **WAD-only музыка:** внешний SoundFont отвергнут — это новый контент и
   нарушение архитектурного правила проекта. Инструменты читаются из
   `GENMIDI`, как в DOS DOOM.
4. **OPL-core не пишем с нуля:** vendored managed C# port Nuked OPL3
   (LGPL-2.1) используется только как регистровый синтезатор. MUS driver и
   `GENMIDI` mapping портируются в проект с опорой на MIT LittleMUS /
   linuxdoom-compatible semantics; лицензии и upstream фиксируются рядом с
   исходниками.
5. **Явная инъекция `SoundSystem`:** без глобального `FindObjectOfType` на
   каждом событии. `MapLoader` создаёт один сервис и передаёт его игроку,
   `ThingSpawner`, `LineActivator` и движущимся секторам.
6. **Геймплейные тики не зависят от аудио:** отсутствие listener/device,
   пропущенный lump или исчерпание пула не меняют урон, FSM, подбор или
   движение сектора.
7. **Крики монстров инициирует FSM:** `MonsterBrain` отдаёт чистую команду
   `PlaySound(MonsterSoundCue)`. Так sight/pain/attack/death привязаны к
   правильному состоянию/кадру и покрываются EditMode-тестами.
8. **Проигрывание и `NoiseAlert` разделены:** выстрел по-прежнему будит
   секторы через существующий `PlayerWeapons.Fired`; слышимый клип не
   запускает вторую игровую тревогу.

## Архитектура

### `Doom.Audio` — чистые форматы и музыкальный driver

Новая сборка `Assets/Scripts/Audio/Doom.Audio.asmdef`,
`noEngineReferences: true`, ссылка на `Doom.Wad` и OPL-core.

- `DecodedSound` — `SampleRate`, `byte[] Samples` (unsigned 8-bit mono).
- `DmxSound.Decode(byte[])`:
  - format (`UInt16 LE`) должен быть 3;
  - sample rate (`UInt16 LE`) > 0;
  - declared count (`UInt32 LE`) не выходит за lump;
  - payload начинается с byte 8;
  - 16-байтовые DMX lead-in/lead-out не воспроизводятся, если lump
    соответствует этому стандартному padding.
- `SoundCatalog` — case-insensitive lookup `DS*`, чтение по имени из WAD.
- `MusSong` / `MusReader` — заголовок `MUS\x1A`, инструменты, score events,
  variable-length delay; строгие bounds-checks.
- `GenMidiBank` — заголовок `#OPL_II#`, 175 инструментов × 36 байт.
- `MusOplPlayer` — состояние каналов/голосов, контроллеры, ноты, pitch,
  percussion channel 15, запись регистров OPL и `Render(float[] stereo)`.
  На end-of-score полностью сбрасывает sequencer/chip и начинает loop без
  выделений памяти.
- `MusicLumpName.ForMap("E1M1")` → `D_E1M1`;
  `ForMap("MAP01")` → таблица DOOM II-имён откладывается до появления MAPxx
  контента, но API не хардкодит E1 в Unity-слое.

### `Assets/ThirdParty/NukedOpl`

Managed C# port Nuked OPL3, отдельная asmdef без Unity-зависимостей.
Сохраняются upstream URL, версия/commit и полный LGPL-2.1 текст. В коде
проекта используется тонкий `IOplChip`-адаптер, чтобы parser/sequencer не
зависели от конкретных имён vendored API.

### `Doom.Game` — таблицы звуков игрока и оружия

- `WeaponDef.FireSound`:
  - Fist → `DSPUNCH`;
  - Pistol → `DSPISTOL`;
  - Shotgun → `DSSHOTGN`;
  - Chaingun → `DSPISTOL`.
- `PickupSoundTable.For(doomedNum)` → Item / Weapon / Power.
  Таблица не проигрывает звук и не зависит от Unity.

### `Doom.Things` — звуковые данные монстров

`MonsterDef` дополняется данными, а не Unity-ссылками:
- `string[] SightSounds`;
- `string ActiveSound`;
- `string AttackSound`, `string MeleeSound`;
- `string PainSound`;
- `string[] DeathSounds`.

Для E1 фиксируются семейства оригинальных имён:
- POSS/SPOS: `DSPOSIT1..3`, `DSPOSACT`, pistol/shotgun,
  `DSPOPAIN`, `DSPODTH1..3`;
- TROO: `DSBGSIT1..2`, `DSDMACT`, `DSFIRSHT`/`DSCLAW`,
  `DSDMPAIN`, `DSBGDTH1..2`;
- SARG: `DSSGTSIT`, `DSDMACT`, `DSSGTATK`, `DSDMPAIN`,
  `DSSGTDTH`.

Фактическое наличие всех имён проверяется на `freedoom1.wad`; если Freedoom
не содержит конкретный вариант, таблица корректируется к совместимому
стандартному lump, а не генерируется клип.

### `Doom.MapBuild` — Unity playback

**`SoundCache`:**
- получает открытый `WadFile`;
- `Get("DSPISTOL")` декодирует через `DmxSound`, создаёт mono `AudioClip`,
  переводя sample `b` в float `(b - 128) / 128f`;
- кэширует успехи и отсутствующие/битые lumps;
- все runtime-used SFX pre-warm'ятся в `MapLoader.Build()` до закрытия WAD;
- `Get` после закрытия возвращает только уже готовый клип.

**`SoundSystem`:**
- пул 16 `AudioSource` с возможностью расширить после профилирования;
- `PlayLocal(name)` — 2D, для оружия/игрока/UI;
- `PlayAt(name, position)` — 3D one-shot;
- `PlayLoop(name, ownerKey, position)` / `StopLoop(ownerKey, stopSound)` —
  движение сектора без множащихся источников;
- world SFX: `spatialBlend=1`, `minDistance=160*worldScale`,
  `maxDistance=1200*worldScale`, linear rolloff;
- local SFX: `spatialBlend=0`;
- при исчерпании пула тихий/самый старый one-shot может быть вытеснен;
  музыка имеет отдельный `AudioSource` и не участвует в пуле.

**`MusicPlayer`:**
- до закрытия WAD копирует bytes `D_<MAP>` и `GENMIDI`;
- создаёт streaming `AudioClip` stereo 44100 Hz;
- PCM callback вызывает только pure-C# `MusOplPlayer.Render`;
- callback не обращается к Unity API (кроме заполнения переданного массива),
  не аллоцирует и не берёт блокирующих locks;
- `OnDestroy` останавливает source и освобождает состояние.

## Интеграция событий

```
PlayerWeapons.Fired(def)
  → SoundSystem.PlayLocal(def.FireSound)
  → NoiseAlertSystem (существующий игровой шум, без изменений)

PlayerInventory.TryPickup(doomedNum) accepted
  → PickedUp(doomedNum)
  → PickupSoundTable → PlayLocal(DSITEMUP / DSWPNUP / DSGETPOW)

PlayerHealth.TakeDamage
  → non-lethal Damaged → DSPLPAIN
  → Died → DSPLDETH (pain в этот тик не играет)

MonsterBrain:
  Wake → Sight
  attack fire entry → Attack/Melee
  enter Pain → Pain
  NotifyKilled → Death
  occasional Chase entry → Active
    → IMonsterWorld.PlaySound(cue)
    → MonsterController → PlayAt(def sound, transform.position)

Projectile launch/impact
  → DSFIRSHT / DSFIRXPL at world position

LineActivator accepted
  → switch DSSWTCHN
  → SectorMover start/return/stop
      door: DSDOROPN / DSDORCLS
      floor/lift: loop DSSTNMOV / stop DSPSTOP

LineActivator rejected by key
  → local DSNOWAY (fallback DSOOF)
```

## Жизненный цикл WAD и pre-warm

`MapLoader.Build()` держит `using var wad`; после возврата WAD закрыт. Поэтому:

1. создать `SoundCache` сразу после открытия WAD;
2. собрать множество имён из `WeaponTable`, `PickupSoundTable`,
   `MonsterTable` и фиксированного списка sector/player SFX;
3. вызвать `SoundCache.Get` для каждого имени;
4. скопировать bytes текущего `D_<MAP>` и `GENMIDI` в `MusicPlayer`;
5. только затем завершить `Build()`.

Ленивая попытка впервые прочитать SFX из закрытого WAD считается багом и
закрывается тестом.

## Тестирование

**EditMode, `Doom.Audio.Tests`:**
- DMX: валидный format 3, частота/длина/PCM; unsigned→signed mapping;
  bad format, short header, declared length за пределами lump;
- стандартный lead/trail padding не попадает в полезный клип;
- MUS: header, channel events, note-with-volume, release, pitch,
  controller/system events, variable-length delay, score end, malformed bounds;
- GENMIDI: signature, 175 records, short bank reject;
- sequencer: детерминированное число samples между событиями, note on/off,
  percussion channel, loop reset, `Render` без аллокаций после warm-up;
- real-WAD: все обязательные `DS*`, `D_E1M1` и `GENMIDI` существуют и
  декодируются.

**EditMode, существующие сборки:**
- `WeaponTableTests`: у каждого оружия есть FireSound;
- `MonsterTableTests`: обязательные sound fields заполнены, варианты
  резолвятся в Freedoom;
- `MonsterBrain` fake-world: cue приходит ровно при wake/attack/pain/death,
  не меняя существующую FSM.
- `PickupSoundTableTests`: weapon/power/item классификация.

**PlayMode:**
- после загрузки `SoundSystem`, `MusicPlayer` и один `AudioListener`
  существуют; обязательные клипы pre-warm'нуты;
- `FireOnceForTest()` запускает локальный weapon source с `DSPISTOL`;
- успешный pickup запускает соответствующий cue, rejected pickup — нет;
- дверь создаёт world source, а mover не плодит второй loop при retrigger;
- убийство монстра запускает death cue; тест проверяет выбранный clip/source,
  а не наличие реального аудиоустройства.

**Интерактивно:**
- баланс music/SFX, отсутствие треска на MUS-loop;
- 3D-позиция монстров и дверей, затухание на расстоянии;
- одновременная драка не обрывает оружие/ключевые звуки;
- паузы/перезагрузки сцены не оставляют старую музыку.

## Критерий готовности

На E1M1 музыка запускается и зацикливается из `D_E1M1`; оружие, pickup,
игрок, четыре E1-монстра, фаербол, двери/лифты и switches используют
соответствующие `DS*` lumps. World sounds позиционные, local sounds не
зависят от положения камеры. После закрытия WAD нет runtime-чтений lumps,
ошибки отдельных звуков деградируют в тишину, а не ломают игру. Полные
EditMode + PlayMode зелёные; интерактивная аудиоприёмка пройдена; CLAUDE.md
и roadmap отмечают Stage 6 / 6f завершённым.

## Связанные документы

- Мастер-план: `docs/doom-unity-remake-plan.md` (Этап 6, под-этап 6f).
- План реализации: `docs/superpowers/plans/2026-07-10-sound.md`.
- Оружие: `docs/superpowers/specs/2026-07-02-weapons-design.md`.
- Монстры: `docs/superpowers/specs/2026-07-03-monster-ai-design.md`.
- Предметы: `docs/superpowers/specs/2026-07-10-pickups-design.md`.
- Секторы: `docs/superpowers/specs/2026-05-31-doors-design.md`.
