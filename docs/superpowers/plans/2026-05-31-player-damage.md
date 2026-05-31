# Stage 6b: Player Damage & HP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the player health and armor with a clean `TakeDamage` API, deal DOOM floor damage (nukage/slime/lava sector specials) while standing on a damaging sector, and handle death with a keyboard respawn — visible on a debug HP/armor readout.

**Architecture:** A new pure-C# assembly `Doom.Game` holds `HealthModel` (HP/armor + DOOM armor absorption, unit-tested). `Doom.Specials` gains `SectorDamageTable` (sector special → damage-per-tic, ported data). `Doom.MapBuild` adds `SectorRef` (sector index on floor GameObjects), `PlayerHealth` (wraps the model, raises `Died`), `FloorDamageSystem` (timed raycast-down damage while grounded), `PlayerDeathHandler` (freeze + "You died" overlay + respawn on R), and `PlayerHud` (debug HP/armor text), all wired by `MapLoader`.

**Tech Stack:** Unity 6000.4.8f1 (Built-in pipeline), C#, Unity Test Framework (NUnit, EditMode + PlayMode), new Input System (`Keyboard.current` for the R respawn key).

---

## Conventions used throughout this plan

**Unity Editor path:** `C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe`

**EditMode test run** (PowerShell; narrow `-testFilter` per task; delete the xml first to avoid stale reads):
```powershell
Remove-Item "D:\Development\doom\Logs\test-results.xml" -ErrorAction SilentlyContinue
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -nographics -projectPath "D:\Development\doom" `
    -runTests -testPlatform EditMode `
    -testFilter "Doom.Game.Tests" `
    -testResults "D:\Development\doom\Logs\test-results.xml" `
    -logFile "D:\Development\doom\Logs\test-run.log"
```

**PlayMode test run** (no `-nographics`, no `-quit`):
```powershell
Remove-Item "D:\Development\doom\Logs\playmode-results.xml" -ErrorAction SilentlyContinue
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -projectPath "D:\Development\doom" `
    -runTests -testPlatform PlayMode `
    -testResults "D:\Development\doom\Logs\playmode-results.xml" `
    -logFile "D:\Development\doom\Logs\playmode-run.log"
```

Read results (PASS/FAIL is ONLY in the XML, never the log):
```powershell
Select-String -Path "D:\Development\doom\Logs\test-results.xml" -Pattern 'result="(Passed|Failed)"' | Select-Object -First 10
Select-String -Path "D:\Development\doom\Logs\test-results.xml" -Pattern 'total="\d+"|passed="\d+"|failed="\d+"' | Select-Object -First 3
```
Never add `-quit` with `-runTests` (Unity exits before the runner starts). VERIFY the XML is freshly written (check its timestamp) and contains the expected test class before trusting a result.

**Import/compile pass** (generates `.meta` for new files AND compiles all assemblies — use for Unity-coupled tasks with no unit test):
```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -nographics -projectPath "D:\Development\doom" `
    -executeMethod Doom.Wad.Editor.WadInspectorMenu.DumpFreedoom1 `
    -logFile "D:\Development\doom\Logs\import.log" -quit
```
Then check `Logs\import.log` for `error CS` (must be none): `Select-String -Path "D:\Development\doom\Logs\import.log" -Pattern 'error CS|Compilation failed'`. (`-quit` IS correct here because this uses `-executeMethod`, not `-runTests`.) Unity batchmode often writes a 2-line `.meta` for a new `.cs`; if so, append the `MonoImporter:` block copied from a sibling `.cs.meta` in the same folder (keeping Unity's generated GUID for the new file).

**Unity is slow** (a few minutes per run) and only ONE instance can use the project at a time — run Unity commands sequentially, never in parallel, with a generous timeout (~600000 ms).

**Commit discipline:** every task ends with a commit. NEVER `git add -A` (there is an intentionally-untracked `ProjectSettings/SceneTemplateSettings.json` and an unrelated modified `ProjectSettings/ProjectSettings.asset` — leave both). Stage explicit paths and the `.meta` files Unity generated (run `git status`). End commit messages with:
`Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`

**Existing types you will use:**
- `Doom.Map.MapData`: `Sector[] Sectors` — each `Sector` has `ushort Special` (and `short FloorHeight, CeilingHeight; ushort Tag`). `Thing[] Things` (Type 1 = Player 1 start).
- `Doom.MapBuild.MapLoader` (`Assets/Scripts/MapBuild/MapLoader.cs`): `SpawnPlayer(MapData map, Bounds? bounds)` builds the Player GameObject (local vars `player`, `cc` (CharacterController), `cameraGO`, `pc` (PlayerController), `activator` (LineActivator), and `pos`/`yaw` for the spawn point). `PopulateSectorRoot(...)` (added in Stage 6a) builds each sector's Floor/Ceiling/Wall child GameObjects via a static `AddChild(...)` that RETURNS the created `GameObject` (or null on empty). `worldScale` field = `1f/32f`.
- `Doom.MapBuild.PlayerController`, `Doom.MapBuild.LineActivator` (both MonoBehaviours on the Player; can be disabled via `.enabled`).
- `Doom.MapBuild` asmdef already references `Doom.Wad`, `Doom.Map`, `Doom.Graphics`, `Unity.InputSystem`, `Doom.Things`, `Doom.Specials`.

**Design spec:** `docs/superpowers/specs/2026-05-31-player-damage-design.md` (the WHAT/WHY).

---

## File Structure

**New — `Doom.Game` (pure C#, `noEngineReferences: true`, no references):**
- `Assets/Scripts/Game/Doom.Game.asmdef`
- `Assets/Scripts/Game/HealthModel.cs` — HP/armor state + DOOM armor absorption.

**New — `Doom.Specials`:**
- `Assets/Scripts/Specials/SectorDamageTable.cs` — sector special → damage-per-tic.

**New — `Doom.MapBuild`:**
- `Assets/Scripts/MapBuild/SectorRef.cs` — sector index marker on floor GameObjects.
- `Assets/Scripts/MapBuild/PlayerHealth.cs` — model wrapper + `Died` event.
- `Assets/Scripts/MapBuild/FloorDamageSystem.cs` — timed floor damage.
- `Assets/Scripts/MapBuild/PlayerDeathHandler.cs` — freeze + overlay + respawn.
- `Assets/Scripts/MapBuild/PlayerHud.cs` — debug HP/armor readout.

**Modified — `Doom.MapBuild`:** `Doom.MapBuild.asmdef` (add `Doom.Game`), `MapLoader.cs` (attach `SectorRef` to floors; attach the four player components + init in `SpawnPlayer`).

**New — tests:** `Assets/Tests/EditMode/Game/*`, `Assets/Tests/EditMode/Specials/SectorDamageTableTests.cs`, `Assets/Tests/PlayMode/PlayerDamagePlayTests.cs`.

---

## Phase A — pure logic (`Doom.Game`, `Doom.Specials`)

### Task A1: Scaffold `Doom.Game` + `HealthModel`

**Files:**
- Create: `Assets/Scripts/Game/Doom.Game.asmdef`
- Create: `Assets/Scripts/Game/HealthModel.cs`
- Create: `Assets/Tests/EditMode/Game/Doom.Game.Tests.asmdef`
- Create: `Assets/Tests/EditMode/Game/HealthModelTests.cs`

- [ ] **Step 1: Production asmdef** — `Assets/Scripts/Game/Doom.Game.asmdef`:
```json
{
    "name": "Doom.Game",
    "rootNamespace": "Doom.Game",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": true
}
```

- [ ] **Step 2: Test asmdef** — `Assets/Tests/EditMode/Game/Doom.Game.Tests.asmdef`:
```json
{
    "name": "Doom.Game.Tests",
    "rootNamespace": "Doom.Game.Tests",
    "references": ["Doom.Game", "UnityEngine.TestRunner", "UnityEditor.TestRunner"],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"],
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 3: Failing tests** — `Assets/Tests/EditMode/Game/HealthModelTests.cs`:
```csharp
using NUnit.Framework;

namespace Doom.Game.Tests
{
    public class HealthModelTests
    {
        [Test]
        public void New_model_starts_full_health_no_armor()
        {
            var h = new HealthModel();
            Assert.That(h.Health, Is.EqualTo(100));
            Assert.That(h.Armor, Is.EqualTo(0));
            Assert.That(h.ArmorType, Is.EqualTo(ArmorKind.None));
            Assert.That(h.IsDead, Is.False);
        }

        [Test]
        public void Damage_without_armor_reduces_health_fully()
        {
            var h = new HealthModel();
            h.ApplyDamage(30);
            Assert.That(h.Health, Is.EqualTo(70));
        }

        [Test]
        public void Health_clamps_at_zero_and_is_dead()
        {
            var h = new HealthModel();
            h.ApplyDamage(250);
            Assert.That(h.Health, Is.EqualTo(0));
            Assert.That(h.IsDead, Is.True);
        }

        [Test]
        public void Green_armor_absorbs_one_third()
        {
            var h = new HealthModel(100, 100, ArmorKind.Green);
            h.ApplyDamage(30);            // saved = 30/3 = 10
            Assert.That(h.Health, Is.EqualTo(80));
            Assert.That(h.Armor, Is.EqualTo(90));
        }

        [Test]
        public void Blue_armor_absorbs_one_half()
        {
            var h = new HealthModel(100, 100, ArmorKind.Blue);
            h.ApplyDamage(30);            // saved = 30/2 = 15
            Assert.That(h.Health, Is.EqualTo(85));
            Assert.That(h.Armor, Is.EqualTo(85));
        }

        [Test]
        public void Armor_runs_out_then_full_damage_to_health()
        {
            var h = new HealthModel(100, 5, ArmorKind.Green);
            h.ApplyDamage(30);            // saved would be 10, but only 5 armor left
            Assert.That(h.Armor, Is.EqualTo(0));
            Assert.That(h.ArmorType, Is.EqualTo(ArmorKind.None));
            Assert.That(h.Health, Is.EqualTo(75)); // 100 - (30 - 5)
        }

        [Test]
        public void Reset_restores_full_health_and_clears_armor()
        {
            var h = new HealthModel(10, 50, ArmorKind.Blue);
            h.Reset();
            Assert.That(h.Health, Is.EqualTo(100));
            Assert.That(h.Armor, Is.EqualTo(0));
            Assert.That(h.ArmorType, Is.EqualTo(ArmorKind.None));
        }
    }
}
```

- [ ] **Step 4: Open the Editor to import the asmdefs, then run** with `-testFilter "Doom.Game.Tests"`. Expected: FAIL (`HealthModel`/`ArmorKind` not found).

- [ ] **Step 5: Implement** `Assets/Scripts/Game/HealthModel.cs`:
```csharp
namespace Doom.Game
{
    /// Which armor the player wears. Green (security) absorbs 1/3 of damage,
    /// Blue (combat) absorbs 1/2 — ported from DOOM P_DamageMobj.
    public enum ArmorKind { None, Green, Blue }

    /// Pure player health/armor state. Engine-free so it unit-tests without Unity.
    /// Future enemy/weapon damage routes through ApplyDamage too.
    public sealed class HealthModel
    {
        public const int MaxHealth = 100;
        public const int MaxArmor = 200;

        public int Health { get; private set; }
        public int Armor { get; private set; }
        public ArmorKind ArmorType { get; private set; }

        public HealthModel() => Reset();

        public HealthModel(int health, int armor, ArmorKind armorType)
        {
            Health = health;
            Armor = armor;
            ArmorType = armorType;
        }

        public bool IsDead => Health <= 0;

        /// Apply incoming damage: armor absorbs a fraction (integer math), depleting
        /// 1 point per absorbed point; the remainder hits health (clamped at 0).
        public void ApplyDamage(int damage)
        {
            if (damage <= 0) return;
            if (ArmorType != ArmorKind.None && Armor > 0)
            {
                int saved = ArmorType == ArmorKind.Green ? damage / 3 : damage / 2;
                if (Armor <= saved) { saved = Armor; ArmorType = ArmorKind.None; }
                Armor -= saved;
                damage -= saved;
            }
            Health -= damage;
            if (Health < 0) Health = 0;
        }

        /// Restore to a fresh-spawn state (respawn).
        public void Reset()
        {
            Health = MaxHealth;
            Armor = 0;
            ArmorType = ArmorKind.None;
        }
    }
}
```

- [ ] **Step 6: Run** with `-testFilter "Doom.Game.Tests"`. Expected: PASS (7).

- [ ] **Step 7: Commit** (run `git status` for the exact `.meta`/folder-meta paths Unity generated):
```powershell
git add Assets/Scripts/Game/Doom.Game.asmdef Assets/Scripts/Game/Doom.Game.asmdef.meta `
        Assets/Scripts/Game/HealthModel.cs Assets/Scripts/Game/HealthModel.cs.meta `
        Assets/Scripts/Game.meta `
        Assets/Tests/EditMode/Game/Doom.Game.Tests.asmdef Assets/Tests/EditMode/Game/Doom.Game.Tests.asmdef.meta `
        Assets/Tests/EditMode/Game/HealthModelTests.cs Assets/Tests/EditMode/Game/HealthModelTests.cs.meta `
        Assets/Tests/EditMode/Game.meta
git commit -m "Stage 6b: scaffold Doom.Game + HealthModel (HP/armor, DOOM absorption)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task A2: `SectorDamageTable` (ported damaging-sector data)

**Files:**
- Create: `Assets/Scripts/Specials/SectorDamageTable.cs`
- Test: `Assets/Tests/EditMode/Specials/SectorDamageTableTests.cs`

The test asmdef `Doom.Specials.Tests` already exists (Stage 6a) and references `Doom.Specials`, `Doom.Wad`, `Doom.Wad.Tests`, `Doom.Map`, `Doom.Map.Tests`.

- [ ] **Step 1: Failing test** — `Assets/Tests/EditMode/Specials/SectorDamageTableTests.cs`:
```csharp
using NUnit.Framework;

namespace Doom.Specials.Tests
{
    public class SectorDamageTableTests
    {
        [Test]
        public void Nukage_special_7_does_5()
            => Assert.That(SectorDamageTable.DamagePerTick(7), Is.EqualTo(5));

        [Test]
        public void Hellslime_special_5_does_10()
            => Assert.That(SectorDamageTable.DamagePerTick(5), Is.EqualTo(10));

        [Test]
        public void Strobe_hurt_special_4_does_20()
            => Assert.That(SectorDamageTable.DamagePerTick(4), Is.EqualTo(20));

        [Test]
        public void Super_hellslime_special_16_does_20()
            => Assert.That(SectorDamageTable.DamagePerTick(16), Is.EqualTo(20));

        [Test]
        public void Exit_super_damage_special_11_does_20()
            => Assert.That(SectorDamageTable.DamagePerTick(11), Is.EqualTo(20));

        [Test]
        public void Non_damaging_specials_do_zero()
        {
            Assert.That(SectorDamageTable.DamagePerTick(0), Is.EqualTo(0));  // normal
            Assert.That(SectorDamageTable.DamagePerTick(9), Is.EqualTo(0));  // secret
            Assert.That(SectorDamageTable.DamagePerTick(1), Is.EqualTo(0));  // light blink
        }
    }
}
```

- [ ] **Step 2: Run to verify FAIL** with `-testFilter "Doom.Specials.Tests.SectorDamageTableTests"` (`SectorDamageTable` not found).

- [ ] **Step 3: Implement** `Assets/Scripts/Specials/SectorDamageTable.cs`:
```csharp
namespace Doom.Specials
{
    /// Ported DOOM damaging-sector classification (P_PlayerInSpecialSector). Maps a
    /// Sector.Special to the damage applied per ~0.9s tic while the player stands on
    /// that sector's floor. 0 = not a damaging sector.
    public static class SectorDamageTable
    {
        public static int DamagePerTick(int special) => special switch
        {
            7 => 5,    // nukage
            5 => 10,   // hellslime
            4 => 20,   // strobe + hurt
            16 => 20,  // super hellslime
            11 => 20,  // exit super damage (the level-exit on low HP is deferred to Stage 7)
            _ => 0,
        };
    }
}
```

- [ ] **Step 4: Run to verify PASS** with `-testFilter "Doom.Specials.Tests.SectorDamageTableTests"`. Expected: PASS (6).

- [ ] **Step 5: Commit**:
```powershell
git add Assets/Scripts/Specials/SectorDamageTable.cs Assets/Scripts/Specials/SectorDamageTable.cs.meta `
        Assets/Tests/EditMode/Specials/SectorDamageTableTests.cs Assets/Tests/EditMode/Specials/SectorDamageTableTests.cs.meta
git commit -m "Stage 6b: SectorDamageTable — ported DOOM damaging-sector specials

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Phase B — Unity runtime (`Doom.MapBuild`)

### Task B1: `SectorRef` + attach to floor GameObjects

**Files:**
- Create: `Assets/Scripts/MapBuild/SectorRef.cs`
- Modify: `Assets/Scripts/MapBuild/MapLoader.cs` (attach `SectorRef` in `PopulateSectorRoot`)

No unit test (verified by compile here; exercised by the PlayMode test in C1).

- [ ] **Step 1: Implement** `Assets/Scripts/MapBuild/SectorRef.cs`:
```csharp
using UnityEngine;

namespace Doom.MapBuild
{
    /// Marks a sector's floor GameObject with its sector index, so a downward
    /// raycast can resolve which sector the player is standing on (for floor damage).
    public sealed class SectorRef : MonoBehaviour
    {
        public int SectorIndex = -1;
    }
}
```

- [ ] **Step 2: Attach in `PopulateSectorRoot`.** READ `Assets/Scripts/MapBuild/MapLoader.cs` first to find `PopulateSectorRoot` and the local variable holding the FLOOR child GameObject returned by `AddChild(...)` (the static `AddChild` returns the created `GameObject`, or null for empty geometry). The floor child is the one built from `sm.Floor`. After it is created, attach a `SectorRef` carrying `sm.SectorIdx`. Concretely, where the floor child is created (adapt the variable names to the real code):
```csharp
var floorGo = AddChild(sectorRoot, "Floor", sm.Floor, floorMaterial, ColliderMode.Render, worldScale, ref bounds);
if (floorGo != null)
    floorGo.AddComponent<SectorRef>().SectorIndex = sm.SectorIdx;
```
Because `PopulateSectorRoot` is the SHARED path used by both the initial build and the Stage 6a rebuild, attaching here means `SectorRef` is automatically re-applied whenever a sector's floor is rebuilt (door/lift animation) — no extra wiring needed. (If the floor child is created inside a helper rather than directly in `PopulateSectorRoot`, attach it at whichever single shared point creates the floor child. Do NOT attach to ceiling/wall children — only the floor.)

- [ ] **Step 3: Import/compile pass**; confirm zero `error CS` in `Logs\import.log`. Ensure `SectorRef.cs.meta` has a `MonoImporter:` block (append from a sibling like `LineRef.cs.meta` if Unity wrote a 2-line stub, keeping Unity's GUID).

- [ ] **Step 4: Commit**:
```powershell
git add Assets/Scripts/MapBuild/SectorRef.cs Assets/Scripts/MapBuild/SectorRef.cs.meta `
        Assets/Scripts/MapBuild/MapLoader.cs
git commit -m "Stage 6b: SectorRef — tag floor GameObjects with their sector index

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task B2: `PlayerHealth`

**Files:**
- Create: `Assets/Scripts/MapBuild/PlayerHealth.cs`
- Modify: `Assets/Scripts/MapBuild/Doom.MapBuild.asmdef` (add `"Doom.Game"`)

No unit test (the model is tested in A1; this wrapper is exercised by the PlayMode test in C1).

- [ ] **Step 1: Add `"Doom.Game"`** to the `references` array of `Assets/Scripts/MapBuild/Doom.MapBuild.asmdef` (READ first; keep all existing references — they are name-based: `Doom.Wad`, `Doom.Map`, `Doom.Graphics`, `Unity.InputSystem`, `Doom.Things`, `Doom.Specials`). Append `"Doom.Game"`.

- [ ] **Step 2: Implement** `Assets/Scripts/MapBuild/PlayerHealth.cs`:
```csharp
using System;
using UnityEngine;
using Doom.Game;

namespace Doom.MapBuild
{
    /// Player-side health/armor component. Wraps the pure HealthModel, exposes the
    /// TakeDamage entry point (future enemies/weapons call it too), and raises Died
    /// exactly once when health reaches zero.
    public sealed class PlayerHealth : MonoBehaviour
    {
        readonly HealthModel model = new HealthModel();
        bool deadAnnounced;

        public int Health => model.Health;
        public int Armor => model.Armor;
        public bool IsDead => model.IsDead;

        /// Raised once when the player dies (health hits 0).
        public event Action Died;

        public void TakeDamage(int amount)
        {
            if (deadAnnounced || amount <= 0) return;
            model.ApplyDamage(amount);
            if (model.IsDead)
            {
                deadAnnounced = true;
                Died?.Invoke();
            }
        }

        /// Respawn: restore full health and re-arm the Died event.
        public void ResetHealth()
        {
            model.Reset();
            deadAnnounced = false;
        }
    }
}
```

- [ ] **Step 3: Import/compile pass**; zero `error CS`; valid `.meta` (append `MonoImporter:` block if a stub).

- [ ] **Step 4: Commit**:
```powershell
git add Assets/Scripts/MapBuild/PlayerHealth.cs Assets/Scripts/MapBuild/PlayerHealth.cs.meta `
        Assets/Scripts/MapBuild/Doom.MapBuild.asmdef
git commit -m "Stage 6b: PlayerHealth — model wrapper + Died event; ref Doom.Game

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task B3: `FloorDamageSystem`

**Files:**
- Create: `Assets/Scripts/MapBuild/FloorDamageSystem.cs`

No unit test (exercised by the PlayMode test in C1).

- [ ] **Step 1: Implement** `Assets/Scripts/MapBuild/FloorDamageSystem.cs`:
```csharp
using UnityEngine;
using Doom.Map;
using Doom.Specials;

namespace Doom.MapBuild
{
    /// Deals DOOM floor damage: while the player is grounded on a sector whose
    /// Special is a damaging type, applies that damage every ~0.914s (32 of 35
    /// tics) — matching P_PlayerInSpecialSector's `leveltime & 0x1f` cadence.
    public sealed class FloorDamageSystem : MonoBehaviour
    {
        const float TicInterval = 32f / 35f;   // ~0.914s

        MapData map;
        float worldScale;
        PlayerHealth health;
        CharacterController cc;
        float timer;

        public void Init(MapData map, float worldScale, PlayerHealth health, CharacterController cc)
        {
            this.map = map;
            this.worldScale = worldScale;
            this.health = health;
            this.cc = cc;
            timer = 0f;
        }

        void Update()
        {
            if (map == null || health == null || health.IsDead) return;
            timer += Time.deltaTime;
            if (timer < TicInterval) return;
            timer -= TicInterval;
            TryApplyFloorDamageOnce();
        }

        /// Runs one floor-damage check now. Returns the damage applied (0 if the
        /// player isn't grounded on a damaging sector). Public so tests can drive it
        /// deterministically without waiting for the accumulator.
        public int TryApplyFloorDamageOnce()
        {
            if (cc != null && !cc.isGrounded) return 0;
            int special = SectorSpecialUnderPlayer();
            if (special < 0) return 0;
            int dmg = SectorDamageTable.DamagePerTick(special);
            if (dmg > 0) health.TakeDamage(dmg);
            return dmg;
        }

        /// The Special of the sector whose floor the player stands on, or -1 if a
        /// downward raycast finds no SectorRef. Public so tests can assert the
        /// raycast→SectorRef chain resolves.
        public int SectorSpecialUnderPlayer()
        {
            if (map == null) return -1;
            // transform.position is at the player's feet; cast down from just above.
            Vector3 origin = transform.position + Vector3.up * (16f * worldScale);
            float range = 48f * worldScale;
            if (!Physics.Raycast(origin, Vector3.down, out var hit, range,
                                 ~0, QueryTriggerInteraction.Ignore))
                return -1;
            var sref = hit.collider.GetComponentInParent<SectorRef>();
            if (sref == null || sref.SectorIndex < 0 || sref.SectorIndex >= map.Sectors.Length)
                return -1;
            return map.Sectors[sref.SectorIndex].Special;
        }
    }
}
```
NOTE on the raycast: the player GameObject's `transform.position` is at the player's feet (the CharacterController's `center` is offset up by half its height), and the floor collider sits at the feet. Casting from `feet + 0.5m` down `1.5m` reliably hits the floor child (whose `SectorRef` we read) before anything else. `QueryTriggerInteraction.Ignore` skips trigger colliders. If during implementation the player's own `CharacterController` capsule is found instead of the floor, mirror the floor-anchor raycast approach already used by `ThingSpawner` (Stage 5) — read `Assets/Scripts/MapBuild/ThingSpawner.cs` for the exact origin/range/mask it uses to find floors, and match it. Do NOT change the raycast to hit ceilings.

- [ ] **Step 2: Import/compile pass**; zero `error CS`; valid `.meta`.

- [ ] **Step 3: Commit**:
```powershell
git add Assets/Scripts/MapBuild/FloorDamageSystem.cs Assets/Scripts/MapBuild/FloorDamageSystem.cs.meta
git commit -m "Stage 6b: FloorDamageSystem — timed DOOM floor damage while grounded

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task B4: `PlayerDeathHandler` + `PlayerHud`

**Files:**
- Create: `Assets/Scripts/MapBuild/PlayerDeathHandler.cs`
- Create: `Assets/Scripts/MapBuild/PlayerHud.cs`

No unit test (exercised by the PlayMode test in C1).

- [ ] **Step 1: Implement** `Assets/Scripts/MapBuild/PlayerDeathHandler.cs`:
```csharp
using UnityEngine;
using UnityEngine.InputSystem;

namespace Doom.MapBuild
{
    /// On player death: freeze movement/use/floor-damage, show a "You died" overlay,
    /// and respawn at the start when R is pressed.
    public sealed class PlayerDeathHandler : MonoBehaviour
    {
        PlayerHealth health;
        PlayerController controller;
        LineActivator activator;
        FloorDamageSystem damage;
        CharacterController cc;
        Vector3 startPos;
        Quaternion startRot;
        bool dead;

        public void Init(PlayerHealth health, PlayerController controller, LineActivator activator,
                         FloorDamageSystem damage, CharacterController cc,
                         Vector3 startPos, Quaternion startRot)
        {
            this.health = health;
            this.controller = controller;
            this.activator = activator;
            this.damage = damage;
            this.cc = cc;
            this.startPos = startPos;
            this.startRot = startRot;
            health.Died += OnDied;
        }

        void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
        }

        void OnDied()
        {
            dead = true;
            SetActive(false);
        }

        void Update()
        {
            if (!dead) return;
            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame) Respawn();
        }

        /// Respawn at the start with full health. Public so tests can drive it.
        public void Respawn()
        {
            if (!dead) return;
            health.ResetHealth();
            // CharacterController must be disabled to teleport, else it eats the move.
            if (cc != null) cc.enabled = false;
            transform.position = startPos;
            transform.rotation = startRot;
            if (cc != null) cc.enabled = true;
            SetActive(true);
            dead = false;
        }

        void SetActive(bool on)
        {
            if (controller != null) controller.enabled = on;
            if (activator != null) activator.enabled = on;
            if (damage != null) damage.enabled = on;
        }

        void OnGUI()
        {
            if (!dead) return;
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                alignment = TextAnchor.MiddleCenter,
            };
            style.normal.textColor = Color.red;
            GUI.Label(new Rect(0, Screen.height / 2f - 40f, Screen.width, 80f),
                      "You died — press R", style);
        }
    }
}
```

- [ ] **Step 2: Implement** `Assets/Scripts/MapBuild/PlayerHud.cs`:
```csharp
using UnityEngine;

namespace Doom.MapBuild
{
    /// Minimal debug readout of health/armor in the top-left. This is a temporary
    /// placeholder — a real HUD (face, ammo, weapon) lands in Stage 7.
    public sealed class PlayerHud : MonoBehaviour
    {
        PlayerHealth health;

        public void Init(PlayerHealth health) => this.health = health;

        void OnGUI()
        {
            if (health == null) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 20 };
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(12f, 8f, 300f, 28f), $"HEALTH {health.Health}", style);
            GUI.Label(new Rect(12f, 32f, 300f, 28f), $"ARMOR {health.Armor}", style);
        }
    }
}
```

- [ ] **Step 3: Import/compile pass**; zero `error CS`; valid `.meta` for both files (append `MonoImporter:` block if a stub).

- [ ] **Step 4: Commit**:
```powershell
git add Assets/Scripts/MapBuild/PlayerDeathHandler.cs Assets/Scripts/MapBuild/PlayerDeathHandler.cs.meta `
        Assets/Scripts/MapBuild/PlayerHud.cs Assets/Scripts/MapBuild/PlayerHud.cs.meta
git commit -m "Stage 6b: PlayerDeathHandler (freeze + overlay + respawn) + PlayerHud

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task B5: Wire the player components into `MapLoader.SpawnPlayer`

**Files:**
- Modify: `Assets/Scripts/MapBuild/MapLoader.cs`

No unit test (exercised by the PlayMode test in C1 + the existing PlayMode regression).

- [ ] **Step 1: Add the components in `SpawnPlayer`.** READ `Assets/Scripts/MapBuild/MapLoader.cs` `SpawnPlayer` first. It already creates `player`, `cc` (CharacterController), `cameraGO`, `pc` (PlayerController), `activator` (LineActivator), and has the spawn `pos` (Vector3) and `yaw` (float). AFTER the `activator.Init(...)` line, add the health/damage/death/HUD components and initialize them:
```csharp
// Health, floor damage, death/respawn, and a minimal HP/armor readout (Stage 6b).
var health = player.AddComponent<PlayerHealth>();

var hud = player.AddComponent<PlayerHud>();
hud.Init(health);

var floorDamage = player.AddComponent<FloorDamageSystem>();
floorDamage.Init(map, worldScale, health, cc);

var death = player.AddComponent<PlayerDeathHandler>();
death.Init(health, pc, activator, floorDamage, cc, pos, Quaternion.Euler(0f, yaw, 0f));
```
(Use the REAL local variable names from `SpawnPlayer`. `pos`/`yaw` are the spawn point already computed near the top of the method; if they are scoped so they aren't visible at this point, hoist their declarations or capture them into locals before the camera/controller block. `Quaternion.Euler(0f, yaw, 0f)` must match how the method already sets `player.transform.rotation`.)

- [ ] **Step 2: Import/compile pass**; zero `error CS`.

- [ ] **Step 3: Run the existing PlayMode tests** (regression — the new components must not break player spawn/scene build). Delete the xml first; use the PlayMode command from Conventions with the default (no) filter:
```powershell
Remove-Item "D:\Development\doom\Logs\b5-play.xml" -ErrorAction SilentlyContinue
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -projectPath "D:\Development\doom" `
    -runTests -testPlatform PlayMode `
    -testResults "D:\Development\doom\Logs\b5-play.xml" `
    -logFile "D:\Development\doom\Logs\b5-play.log"
```
Read the fresh xml: top-level `total`/`passed`/`failed`. Expected: the existing 5 PlayMode tests still pass (`total=5 passed=5 failed=0`). Check `b5-play.log` for any `NullReferenceException` during scene build (there must be none — a bad Init wiring would throw here).

- [ ] **Step 4: Commit**:
```powershell
git add Assets/Scripts/MapBuild/MapLoader.cs
git commit -m "Stage 6b: wire PlayerHealth/FloorDamageSystem/Death/HUD into MapLoader

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Phase C — integration test & docs

### Task C1: PlayMode test — floor damage, death, respawn

**Files:**
- Create: `Assets/Tests/PlayMode/PlayerDamagePlayTests.cs`

The PlayMode asmdef `Doom.Stage3.PlayTests` already references `Doom.MapBuild`, `Doom.Map`, `Doom.Wad`, `Doom.Specials`. It does NOT reference `Doom.Game`, but the test only touches `Doom.MapBuild` types (`PlayerHealth`, `FloorDamageSystem`, `PlayerDeathHandler`) and `Doom.Map`/`Doom.Wad`, so no asmdef change is needed. (If the test references `Doom.Game` types directly, add `"Doom.Game"` to that asmdef and note it.)

- [ ] **Step 1: Write the test** — `Assets/Tests/PlayMode/PlayerDamagePlayTests.cs`:
```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.MapBuild;

namespace Doom.Stage3.PlayTests
{
    public class PlayerDamagePlayTests
    {
        [SetUp]
        public void SetUp() => LogAssert.ignoreFailingMessages = true; // PhysX cook warnings

        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        static IEnumerator LoadE1M1()
        {
            SceneManager.LoadScene("Stage2_MapPreview");
            yield return null; yield return null;
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator TakeDamage_reduces_health_through_the_wired_component()
        {
            yield return LoadE1M1();
            var health = Object.FindAnyObjectByType<PlayerHealth>();
            Assert.That(health, Is.Not.Null, "PlayerHealth should be on the Player");
            Assert.That(health.Health, Is.EqualTo(100));

            health.TakeDamage(30);
            Assert.That(health.Health, Is.EqualTo(70));
        }

        [UnityTest]
        public IEnumerator Floor_damage_path_resolves_the_spawn_sector_and_is_safe()
        {
            yield return LoadE1M1();
            var floorDamage = Object.FindAnyObjectByType<FloorDamageSystem>();
            Assert.That(floorDamage, Is.Not.Null, "FloorDamageSystem should be on the Player");

            // Let the player settle onto the floor.
            for (int i = 0; i < 30; i++) yield return new WaitForFixedUpdate();

            // The downward raycast must resolve a SectorRef under the player (>= 0).
            int special = floorDamage.SectorSpecialUnderPlayer();
            Assert.That(special, Is.GreaterThanOrEqualTo(0),
                "raycast should find the spawn sector's SectorRef");

            // The spawn sector is a normal (non-damaging) floor → one tick deals 0,
            // and the chain runs without exceptions.
            var health = Object.FindAnyObjectByType<PlayerHealth>();
            int before = health.Health;
            int applied = floorDamage.TryApplyFloorDamageOnce();
            Assert.That(applied, Is.EqualTo(0), "spawn floor is not a damaging sector");
            Assert.That(health.Health, Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator Death_disables_control_and_respawn_restores()
        {
            yield return LoadE1M1();
            var health = Object.FindAnyObjectByType<PlayerHealth>();
            var pc = health.GetComponent<PlayerController>();
            var death = health.GetComponent<PlayerDeathHandler>();
            Assert.That(pc, Is.Not.Null);
            Assert.That(death, Is.Not.Null);

            var startPos = health.transform.position;

            health.TakeDamage(1000);              // fatal
            yield return null;
            Assert.That(health.IsDead, Is.True);
            Assert.That(pc.enabled, Is.False, "controls freeze on death");

            health.transform.position += new Vector3(5f, 0f, 5f); // wander while dead
            death.Respawn();
            Assert.That(health.Health, Is.EqualTo(100));
            Assert.That(pc.enabled, Is.True, "controls restore on respawn");
            Assert.That(Vector3.Distance(health.transform.position, startPos), Is.LessThan(0.5f),
                "respawn returns the player to the start");
        }
    }
}
```
NOTE on the floor-damage assertion: this test verifies the raycast→`SectorRef`→`SectorDamageTable` chain runs end-to-end and resolves the spawn sector, and that a non-damaging floor deals 0 (the EditMode `SectorDamageTableTests` already pins the damage VALUES). If, during implementation, you confirm `freedoom1.wad` E1M1 contains a damaging sector (scan `map.Sectors` for a Special in {4,5,7,11,16}), you MAY additionally position the player over it and assert `TryApplyFloorDamageOnce()` returns that sector's damage and health drops — but do not weaken or remove the deterministic checks above, and do not fake a positive result if E1M1 has no damaging sector.

- [ ] **Step 2: Run the PlayMode tests.** Use the PlayMode command from Conventions (delete the xml first). Expected: all pass — the existing 5 + these 3 = 8. Confirm the three `PlayerDamagePlayTests` are present and `Passed` in the fresh xml, and check the log for exceptions (none).

- [ ] **Step 3: Commit**:
```powershell
git add Assets/Tests/PlayMode/PlayerDamagePlayTests.cs Assets/Tests/PlayMode/PlayerDamagePlayTests.cs.meta
git commit -m "Stage 6b: PlayMode test — floor-damage path, death, respawn

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task C2: Full sweep + visual check + docs

**Files:**
- Modify: `docs/doom-unity-remake-plan.md`, `CLAUDE.md`

- [ ] **Step 1: Run ALL EditMode tests** (no filter) to a fresh xml. Record the exact top-level `total`/`passed`/`failed`. (Baseline before 6b is 124 EditMode; 6b adds 7 `HealthModel` + 6 `SectorDamageTable` = 13 → expect 137.)
```powershell
Remove-Item "D:\Development\doom\Logs\c2-edit.xml" -ErrorAction SilentlyContinue
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -nographics -projectPath "D:\Development\doom" `
    -runTests -testPlatform EditMode `
    -testResults "D:\Development\doom\Logs\c2-edit.xml" `
    -logFile "D:\Development\doom\Logs\c2-edit.log"
```

- [ ] **Step 2: Run ALL PlayMode tests** to a fresh xml (baseline 5 + 3 = expect 8):
```powershell
Remove-Item "D:\Development\doom\Logs\c2-play.xml" -ErrorAction SilentlyContinue
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -projectPath "D:\Development\doom" `
    -runTests -testPlatform PlayMode `
    -testResults "D:\Development\doom\Logs\c2-play.xml" `
    -logFile "D:\Development\doom\Logs\c2-play.log"
```

- [ ] **Step 3: Read both XMLs, record totals.** If ANYTHING failed, STOP and fix before docs (read the failing `<test-case>` entries; grep the logs for `error CS`). Both suites must be fully green.

- [ ] **Step 4: Visual check (manual, recommended — note as user-TODO if running headless).** In the Editor: open `Stage2_MapPreview.unity`, Play — the HP/ARMOR readout shows in the corner; if E1M1 has a nukage/slime/lava sector, walk into it and watch HEALTH tick down ~once/second; reduce health to 0 → "You died — press R" appears → press R → respawn at the start with HEALTH 100. (An agent running headless cannot do this — record it as a user-TODO; the PlayMode test already proves the wired model/death/respawn programmatically.)

- [ ] **Step 5: Update the roadmap** `docs/doom-unity-remake-plan.md`. READ IT FIRST. Under `## Этап 6. Игровая логика`, mark the health bullet done — change `- Здоровье, броня, урон.` to `- Здоровье, броня, урон. ✅ (под-этап 6b)`. Add a short Russian paragraph (matching the existing `**Под-этап 6a … ✅**` style) describing 6b: new `Doom.Game` (`HealthModel` с DOOM-поглощением брони), `SectorDamageTable` в `Doom.Specials`, и `Doom.MapBuild`-компоненты `SectorRef`/`PlayerHealth`/`FloorDamageSystem`/`PlayerDeathHandler`/`PlayerHud`; урон-полы, смерть/респавн по R, минимальный показ HP/брони; отложено (радиокостюм, exit-по-11, секрет-сектор, подбор брони/аптечек, краш-урон, HUD). Reference the design `docs/superpowers/specs/2026-05-31-player-damage-design.md` and plan `docs/superpowers/plans/2026-05-31-player-damage.md`.

- [ ] **Step 6: Update `CLAUDE.md`.** READ IT FIRST.
  - Update the project-status header line (currently "...Stage 6 is split into sub-stages 6a–6f and 6a is done") to note 6a AND 6b are done.
  - Add a **Stage 6b** status bullet AFTER the Stage 6a bullet, same terse style: new `Doom.Game` (`HealthModel`); `Doom.Specials` `SectorDamageTable`; `Doom.MapBuild` `SectorRef`/`PlayerHealth`/`FloorDamageSystem`/`PlayerDeathHandler`/`PlayerHud`; floor damage while grounded, death + R-respawn, debug HP/armor OnGUI; deferred items (radiation suit, special-11 level exit, secret sectors, armor/health pickups, crush damage, real HUD).
  - Update the spec/plan reference line to add `Stage 6b = 2026-05-31-player-damage`.
  - Update the test-count line to the ACTUAL new totals from Steps 1–3, and add a Stage-6b bullet to the test-suite breakdown with the per-area counts (`Doom.Game.Tests` HealthModel = 7; `Doom.Specials.Tests` SectorDamageTable = 6; PlayMode `PlayerDamagePlayTests` = 3).
  - Change the "next concrete work" pointer from "Stage 6b (player damage & HP)" to "Stage 6c (weapons & shooting)".
  - Keep all existing Stage 1–6a wording intact.

- [ ] **Step 7: Commit**:
```powershell
git add docs/doom-unity-remake-plan.md CLAUDE.md
git commit -m "Stage 6b done: mark plan + CLAUDE.md

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Self-Review (completed by plan author)

**Spec coverage:**
- Модель HP/брони с DOOM-поглощением → A1 (`HealthModel`, юнит-тесты зелёная/синяя/истощение/кламп/Reset). Классификация урон-секторов → A2 (`SectorDamageTable`, 5/7/4/16/11 + нули). Определение сектора под игроком → B1 (`SectorRef` на полу в общем `PopulateSectorRoot`) + B3 (луч вниз). Точка входа урона → B2 (`PlayerHealth.TakeDamage` + `Died`). Урон-полы каждые 32/35 c пока grounded → B3 (`FloorDamageSystem`). Смерть/блок/оверлей/респавн по R → B4 (`PlayerDeathHandler`). Показ HP/брони → B4 (`PlayerHud`). Провод → B5 (`MapLoader.SpawnPlayer`). Тесты модели+смерти+респавна+цепочки луча → C1 (PlayMode). Доки → C2.
- Краевые из спеки: радиокостюм/exit-по-11/секрет-9/подбор брони/краш — отложены (не в задачах, отмечены в доках C2). Урон только grounded → B3 (`cc.isGrounded`). Телепорт через `enabled` тоггл CC → B4 `Respawn`. Броня старт 0 → `HealthModel.Reset`/конструктор по умолчанию.

**Placeholder scan:** Полный код в каждом шаге (asmdef'ы, `HealthModel`, `SectorDamageTable`, все пять MonoBehaviour'ов, тесты). Два места требуют чтения реального `MapLoader` (B1 — имя floor-child переменной в `PopulateSectorRoot`; B5 — реальные имена `pos`/`yaw`/`pc`/`activator`/`cc` в `SpawnPlayer`) — это адаптация к существующему коду, не плейсхолдеры; даны точные ориентиры. Луч в B3 ссылается на `ThingSpawner` как фолбэк-образец — конкретный код дан, ссылка лишь на случай коллизии с капсулой.

**Type consistency:** `HealthModel{ Health, Armor, ArmorType, MaxHealth, MaxArmor, IsDead, ApplyDamage(int), Reset(), ctor(), ctor(int,int,ArmorKind) }`; `ArmorKind{ None, Green, Blue }`. `SectorDamageTable.DamagePerTick(int)→int`. `SectorRef{ int SectorIndex }`. `PlayerHealth{ Health, Armor, IsDead, event Died, TakeDamage(int), ResetHealth() }`. `FloorDamageSystem{ Init(MapData,float,PlayerHealth,CharacterController), TryApplyFloorDamageOnce()→int, SectorSpecialUnderPlayer()→int }`. `PlayerDeathHandler{ Init(PlayerHealth,PlayerController,LineActivator,FloorDamageSystem,CharacterController,Vector3,Quaternion), Respawn() }`. `PlayerHud{ Init(PlayerHealth) }`. Signatures consistent across B2–B5 and the C1 test (`TakeDamage`, `SectorSpecialUnderPlayer`, `TryApplyFloorDamageOnce`, `Respawn`, `.enabled`).

**Known risks flagged in tasks:** raycast hitting the player capsule instead of the floor (B3 — mirror `ThingSpawner`); `pos`/`yaw` scope in `SpawnPlayer` (B5 — hoist if needed); E1M1 may lack a damaging sector (C1 — deterministic checks don't depend on it; optional positive case if present); `.meta` `MonoImporter` block for new MonoBehaviours (handled via import pass in every B task).
