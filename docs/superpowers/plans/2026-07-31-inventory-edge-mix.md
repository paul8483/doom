# Inventory EdgeMix 8× — implementation plan/status

**Статус:** 🟡 implementation complete, standalone visual gate pending.  
**Спека:** `docs/superpowers/specs/2026-07-31-inventory-edge-mix-design.md`.

## Выполнено

- [x] Локализовать scope до world pickup sprites.
- [x] Отклонить EdgeMix 4×: переход занимает слишком большую долю
  маленького спрайта и размывает детали.
- [x] Зафиксировать кандидат EdgeMix 8× с полосой перехода 2+2 pixels.
- [x] Реализовать deterministic premultiplied-alpha upscaler в
  `Doom.Graphics`.
- [x] Добавить отдельные variant/job kind и cache routing.
- [x] Пометить map pickups, animated pickups и runtime death drops.
- [x] Сохранить monster/projectile Super-xBR 4× и Classic native path.
- [x] Поднять Enhanced pipeline version.
- [x] Добавить unit, runner, codec и PlayMode routing tests.
- [x] Собрать Windows standalone.

## Автоматические результаты

- EditMode EdgeMix/runner: **15/15 PASS**.
- EditMode codec: **9/9 PASS**.
- PlayMode sprite routing/hot-switch: **8/8 PASS**.
- Windows build: **SUCCESS**, `Builds/Windows/DoomUnity.exe`, ~128 MB.

## До закрытия

- [ ] Интерактивно сравнить Classic/Enhanced в standalone.
- [ ] Проверить static, animated и dropped pickups.
- [ ] Зафиксировать visual verdict пользователя.
- [ ] При успехе прогнать полный EditMode/PlayMode suite.
- [ ] Снять warm-time и memory delta для 8× pickup textures.
- [ ] При провале сохранить artifacts/reason и откатить runtime routing.
