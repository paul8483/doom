# Enhanced TLITE Glow — дизайн (REJECT)

**Дата:** 2026-08-11  
**Статус:** ❌ REJECT (interactive 2026-08-11)  
**Superseded by:** `docs/superpowers/specs/2026-08-11-enhanced-tlite-per-bulb-flicker-design.md`

## Почему REJECT

Синтетический sector Glow (special 8) пульсирует `_SectorAmbient` на **весь**
сектор (потолок + стены/пол пролёта) синхронно. Нужно было независимое плавное
мерцание **каждой лампочки** на flat `TLITE*`, без «дыхания» всего пролёта.

Реализация sector Glow удалена; eligibility (`EnhancedLampGlowRules`) сохранена
для ceiling-only shader flicker.
