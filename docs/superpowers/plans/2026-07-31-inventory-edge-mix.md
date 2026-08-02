# Sprite EdgeMix 8× — implementation plan/status

**Статус:** 🟢 предметы и противники реализованы, standalone visual gate
успешно закрыт 2026-08-02; 🟡 first-person оружие запланировано.  
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

## Автоматические результаты

- EditMode EdgeMix/runner: **15/15 PASS**.
- EditMode codec: **9/9 PASS**.
- PlayMode sprite routing/hot-switch: **8/8 PASS**.
- Windows build: **SUCCESS**, `Builds/Windows/DoomUnity.exe`, ~128 MB.

## Осталось для закрытия реализованного scope

- [ ] При успехе прогнать полный EditMode/PlayMode suite.
- [ ] Снять warm-time и memory delta для 8× pickup/enemy textures.
- [ ] Обновить итоговые test/perf результаты в спеке и project status.

## Следующая стадия — first-person оружие

- [ ] Добавить `EnhancedWeapon8X` и `EnhancedJobKind.WeaponSprite`.
- [ ] Расширить session/disk cache codec и при необходимости поднять
  `EnhancedPipelineVersion`.
- [ ] Добавить в `SpriteCache` отдельную регистрацию weapon lumps и
  `GetWeapon`, сохранив native Classic fallback.
- [ ] Перевести `WeaponView.DrawPatch` на weapon routing.
- [ ] Применить EdgeMix 8× к idle/fire кадрам и muzzle flash.
- [ ] Проверить, что нативные header dimensions/offsets сохраняют placement,
  bob, lowering и clipping над status bar.
- [ ] Добавить unit, runner, codec и PlayMode hot-switch/routing tests.
- [ ] Собрать Windows standalone.
- [ ] Сравнить Classic/Enhanced для fist, pistol, shotgun, chaingun,
  chainsaw, rocket launcher, plasma и BFG.
- [ ] Проверить детали оружия, анимацию и muzzle flash на halo/размытие.
- [ ] Зафиксировать отдельный visual verdict; при провале оставить weapon
  path на Super-xBR 4× до улучшения EdgeMix.
