# Enhanced TLITE Glow — дизайн

**Дата:** 2026-08-11  
**Статус:** 📋 planned  
**Unity:** `6000.4.8f1`  
**План:** `docs/superpowers/plans/2026-08-11-enhanced-tlite-glow.md`  
**Контекст Stage 8:** `docs/superpowers/specs/2026-07-11-enhanced-graphics-design.md`  
  (sector light + `_SectorAmbient`)

## Контекст и решение

В Freedoom многие «лампочки» на потолке — сектора с flat `TLITE*` и
**special 0** (статичный light level). Vanilla плавное мерцание — это sector
special **8** (Glow): light ходит между min(сосед) и max сектора со скоростью 8
за тик (`RuntimeLightRules.TickGlow`).

Референс для приёмки: save slot 0, **E1M5**, двор у входа. Игрок ≈ doom
`(-2451, 1498)`, смотрит вверх на козырёк. Лампы — сектор **1**, потолок
`TLITE6_6`, light **256**, special **0**, соседний двор light **192** → ожидаемый
Glow-диапазон **192↔256**.

**Решение:** только в **Enhanced** синтетически навешивать Glow на eligible
статичные TLITE-сектора. **Classic** остаётся бит-верным WAD (лампы статичны).
WAD не меняем; gameplay / collision / save schema не меняем.

## Scope

Eligible сектор:

1. `CeilingFlat` начинается с `TLITE` (case-insensitive);
2. WAD `Special` **не** мапится на light kind
   (`RuntimeLightRules.TryKindFromSectorSpecial` → false).

Охват на E1 Freedoom: порядка **~82** статичных TLITE-потолка (включая
козырёк E1M5). Сектора, у которых уже есть light special (1/2/3/4/8/12/13/17),
не трогаем.

Как и у настоящего special 8, пульсирует **весь** light сектора (потолок +
стены/пол этой тонкой «ленты» ламп) — не отдельный emissive только на flat.

## Пользовательский контракт

| Режим | Поведение TLITE special 0 |
|-------|---------------------------|
| Classic | Static(WAD `LightLevel`), как в WAD |
| Enhanced (`SectorAmbientBinding`) | Glow thinker, как special 8 |

- Hot-switch Classic ↔ Enhanced без перезагрузки сцены.
- Отдельной Options-строки нет: эффект — часть Enhanced presentation.
- Save schema без bump: после restore Enhanced заново навешивает Glow по
  WAD flats/specials.

## Архитектура

Sector light visuals в Enhanced уже идут через MPB `_SectorAmbient` /
`_SectorAmbientWeight` в `RuntimeSectorLights`. Classic держит baked vertex
colors — синтетический Glow под Classic не должен оставаться в thinker state.

```text
Init / NotifyProfileChanged / post-Restore
  → eligible? (TLITE + no WAD light special)
      → Enhanced+ambient → InitFromSector(wadLight, special: 8, lowestNeighbor)
      → Classic / ambient off → Static(wadLight)
  → иначе → WAD thinker без изменений
```

Чистая eligibility-правила живут в `Doom.Specials` (без Unity), применение —
в `RuntimeSectorLights`.

## Критерий приёмки

1. Slot 0 / E1M5, Enhanced: козырёк `TLITE6_6` плавно мерцает.
2. Hot-switch в Classic: лампы снова статичны на WAD 256.
3. Сектора с настоящим WAD Glow/Flicker/Strobe не меняют поведение.
4. EditMode eligibility + PlayMode Enhanced Glow / Classic static — green.

## Вне scope

- Правки specials в Freedoom WAD.
- Dynamic Unity point lights на TLITE (пул `EnhancedLightSystem`).
- Мерцание не-TLITE «ламповых» текстур стен (`LITE3` mid и т.п.).
