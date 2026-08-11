# Enhanced TLITE per-bulb flicker — дизайн

**Дата:** 2026-08-11  
**Статус:** 👀 interactive gate (awaiting eyeball)  
**Unity:** `6000.4.8f1`  
**План:** `docs/superpowers/plans/2026-08-11-enhanced-tlite-per-bulb-flicker.md`  
**Reject prior:** `docs/superpowers/specs/2026-08-11-enhanced-tlite-glow-design.md`

## Контекст и решение

Freedoom потолочные «лампы» — сектора с light flats (`TLITE*`, `FLAT2`, …) и
special 0. Sector Glow (special 8 / синтетический thinker) мерцает **всем
пролётом** — REJECT.

**Решение:** только в **Enhanced**, только на **Ceiling** mesh eligible-сектора:
шейдер модулирует яркость/emission **каждой лампочки** по UV-ячейке + luminance
gate. Sector light / `_SectorAmbient` остаётся статичным WAD. **Classic** —
бит-верный WAD. WAD / gameplay / save schema не меняем.

Референс: save slot 0, E1M5 двор, козырёк `TLITE6_6`.

## Scope

Eligible: light ceiling flats (`TLITE*`, `FLAT2`, Freedoom panel `AQF010`/`AQF012`/…),
**or** light wall textures (`AQLITE*`, `LITE*`), и WAD sector special не light-kind.
Ceiling берёт WAD `CeilingFlat`; стены — имя текстуры. Сектора с настоящим light
special не трогаем.

## Пользовательский контракт

| Режим | Поведение |
|-------|-----------|
| Classic | Static WAD light; нет flicker |
| Enhanced | Static sector ambient; Ceiling light-flat per-fixture smooth flicker |

Hot-switch без reload. Нет Options-строки.

## Архитектура

```text
Init / NotifyProfileChanged / ApplyVisuals
  → sector thinkers = WAD only (no synthetic Glow)
  → Enhanced+ambient: eligible Ceiling MPB _LampFlicker=1 (+grid/amp/speed/luma)
  → Classic: clear MPB
Shader EnhancedWorld: if _LampFlicker, hash(cell)+sin(_Time), luma gate → local boost
```

## Критерий приёмки

1. Slot 0 / E1M5 Enhanced: лампочки мерцают независимо и плавнее; пролёт не дышит целиком.
2. Classic: статично.
3. WAD Glow/Flicker/Strobe сектора без изменений.
4. EditMode eligibility + PlayMode Kind==None + Ceiling MPB — green.

## Вне scope

- Point lights на каждую лампочку.
- `LITE*` wall flicker.
- Правки Freedoom WAD.
