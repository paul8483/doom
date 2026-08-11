# Enhanced TLITE per-bulb flicker — план

**Дата:** 2026-08-11  
**Статус:** 👀 interactive gate (awaiting eyeball)  
**Спека:** `docs/superpowers/specs/2026-08-11-enhanced-tlite-per-bulb-flicker-design.md`

## Task 1 — Reject sector Glow + docs

Mark old glow spec/plan REJECT. Revert synthetic `InitFromSector(..., 8)` and
eligible capture special-case.

## Task 2 — Shader

`DoomEnhancedWorld.shader`: `_LampFlicker` + grid/amp/speed/luma; UV cell hash;
luma gate; smooth time pulse; local ambient/emission on bulbs only.

## Task 3 — Ceiling MPB

`RuntimeSectorLights`: after ambient apply, set/clear lamp MPB on eligible
`Ceiling` children. Init / NotifyProfileChanged / RefreshSectorVisual.

## Task 4 — Tests + interactive

PlayMode: E1M5 eligible `Kind==None`, light static; Enhanced Ceiling
`_LampFlicker>0`; Classic cleared. Interactive slot 0 gate; suites/build after.
