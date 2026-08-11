# Enhanced TLITE Glow — план реализации

**Дата:** 2026-08-11  
**Статус:** 📋 planned  
**Unity:** `6000.4.8f1`  
**Спека:** `docs/superpowers/specs/2026-08-11-enhanced-tlite-glow-design.md`

Classic не трогаем визуально; WAD specials не меняем; save schema без bump.
Каждый визуальный шаг закрывается интерактивным вердиктом (референс: slot 0,
E1M5 козырёк).

## Task 1 — Pure eligibility rules

**Файлы:**
- Create: `Assets/Scripts/Specials/EnhancedLampGlowRules.cs`
- Create/extend: `Assets/Tests/EditMode/Specials/EnhancedLampGlowRulesTests.cs`

1. `EnhancedLampGlowRules.IsEligible(string ceilingFlat, int sectorSpecial)`:
   - ceiling flat starts with `TLITE` ( OrdinalIgnoreCase; null/empty → false);
   - `!RuntimeLightRules.TryKindFromSectorSpecial(sectorSpecial, out _)`.
2. EditMode: `TLITE6_6` + special 0 → true; `TLITE6_5` + special 8 → false;
   `FLOOR5_2` + 0 → false; `tlite6_6` case → true; special 17 → false.

## Task 2 — Runtime apply / clear

**Файлы:**
- Modify: `Assets/Scripts/MapBuild/RuntimeSectorLights.cs`
- Modify: `Assets/Scripts/MapBuild/WorldSnapshotRestore.cs` (если restore не
  проходит через путь, где уже зовётся profile/init hook)

1. `ApplyEnhancedLampGlows()`:
   - Enhanced + `SectorAmbientBinding`: eligible →
     `InitFromSector(wad.LightLevel, special: 8, LowestNeighborLight(...))`;
   - иначе eligible → `SectorLightState.Static(wad.LightLevel)`.
2. Вызывать после WAD init-цикла в `Init`, из `NotifyProfileChanged`, и после
   sector light restore (чтобы mid-pulse save не залипал под Classic и Glow
   снова навешивался в Enhanced).
3. Не трогать сектора с уже существующим WAD light kind.

## Task 3 — Save capture (WAD-authoritative для eligible)

**Файлы:**
- Modify: `Assets/Scripts/MapBuild/WorldSnapshotCapture.cs`

Для eligible TLITE-секторов писать в snapshot `map.Sectors[i].LightLevel` и
`LightCount = 0` (не текущий pulsed `GetLight`), чтобы сейв оставался
WAD-истинным. Restore + Task 2 снова применяют Glow в Enhanced.

## Task 4 — PlayMode coverage

**Файлы:**
- Extend: `Assets/Tests/PlayMode/SectorLightPlayTests.cs`
  (или новый `EnhancedLampGlowPlayTests.cs`)

1. Загрузить E1M5 (или карту с известным статичным TLITE), найти eligible
   сектор.
2. Enhanced: `Kind == Glow`, light меняется за N тиков.
3. Classic: `Kind == None`, light == WAD `LightLevel`.
4. Hot-switch туда-обратно без утечек/залипания Kind.

## Task 5 — Interactive gate

1. Load slot 0 (E1M5 двор), Enhanced On, взгляд вверх на козырёк —
   плавное мерцание.
2. Options → Classic — статичные лампы.
3. Вердикт пользователя → статус спеки/плана.

## Task 6 — Suites (после зелёного eyeball)

1. Затронутые EditMode + PlayMode (или полный прогон по принятому ритуалу).
2. Windows standalone — только когда Editor не держит project lock
   (batchmode или `Tools > Doom > Build Windows Standalone` из открытого
   Editor).

## Out of scope

- Правки Freedoom WAD.
- Point lights / bloom-only хаки на TLITE.
- Анимация `LITE*` wall textures.
