# Sprite EdgeMix 8× — implementation plan/status

**Статус:** 🟢 EdgeMix 8× для предметов, противников и first-person оружия
закрыт 2026-08-02 (код + full suites + Windows standalone + interactive
visual gates). Осталось: warm-perf baseline.  
**Спека:** `docs/superpowers/specs/2026-07-31-inventory-edge-mix-design.md`.

## Выполнено

- [x] Реализовать первый scope для world pickup sprites.
- [x] Отклонить EdgeMix 4×: переход занимает слишком большую долю
  маленького спрайта и размывает детали.
- [x] Зафиксировать кандидат EdgeMix 8× с полосой перехода 2+2 pixels.
- [x] Реализовать deterministic premultiplied-alpha upscaler в
  `Doom.Graphics`.
- [x] Добавить отдельные variant/job kind и cache routing.
- [x] Пометить map pickups, animated pickups и runtime death drops.
- [x] Сохранить projectile/world-effect Super-xBR 4× и Classic native path.
- [x] Поднять Enhanced pipeline version.
- [x] Добавить unit, runner, codec и PlayMode routing tests.
- [x] Собрать Windows standalone.
- [x] Добавить отдельные enemy variant/job kind и cache routing.
- [x] Пометить и прогреть анимационные кадры противников, включая Spectre.
- [x] Перевести противников с Super-xBR 4× на EdgeMix 8× в Enhanced.
- [x] Интерактивно сравнить Classic/Enhanced для предметов и противников.
- [x] Зафиксировать visual verdict: результат лучше исходного благодаря
  сохранению оригинальных краёв; заметное размытие принято как известное
  ограничение для будущего улучшения алгоритма.
- [x] Добавить `EnhancedWeapon8X` и `EnhancedJobKind.WeaponSprite`.
- [x] Расширить session/disk cache codec и поднять
  `EnhancedPipelineVersion` до 3.
- [x] Добавить в `SpriteCache` отдельную регистрацию weapon lumps и
  `GetWeapon`, сохранив native Classic fallback.
- [x] Перевести `WeaponView.DrawPatch` на weapon routing.
- [x] Применить EdgeMix 8× к idle/fire кадрам и muzzle flash
  (`WarmNativeWeapon` в `MapLoader`).
- [x] Сохранить нативные header dimensions/offsets для placement /
  bob / lowering / STBAR clipping.
- [x] Добавить unit, runner, codec и PlayMode routing/placement tests.

## Автоматические результаты

- Focused EditMode runner + codec
  (`EnhancedJobRunnerTests|EnhancedCacheCodecTests`):
  **20/20 PASS** (2026-08-02, pipeline v3 + WeaponSprite).
- Focused PlayMode sprite/weapon routing + placement
  (`SpriteUpscalePlayTests|Weapon_placement_rect…`): **12/12 PASS**.
- Full EditMode: **610/610 PASS** (2026-08-02,
  `Logs/test-results.xml`).
- Full PlayMode: **149/149 PASS** (2026-08-02,
  `Logs/playmode-results.xml`; hot-switch weapon assert обновлён с 4×
  на EdgeMix 8× / `GetWeapon`).
- Windows build: **SUCCESS**, `Builds/Windows/DoomUnity.exe`, ~122 MB
  (`Logs/edge-mix-weapon-build-windows.log`).

## First-person оружие — visual gate

- [x] Прогнать focused EditMode/PlayMode suites.
- [x] Собрать Windows standalone.
- [x] Сравнить Classic/Enhanced для fist, pistol, shotgun, chaingun,
  chainsaw, rocket launcher, plasma и BFG.
- [x] Проверить детали оружия, анимацию и muzzle flash на halo/размытие.
- [x] Зафиксировать visual verdict: SUCCESS (см. спеку).

## Осталось для полного закрытия scope

- [x] Прогнать полный EditMode/PlayMode suite.
- [ ] Снять warm-time и memory delta для 8× pickup/enemy/weapon textures.
- [x] Обновить итоговые test результаты в спеке и project status
  (warm-perf цифры — после baseline).
