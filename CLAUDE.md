# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project status

Stages 0, 1, 2, and 3 of the plan are done:

- **Stage 0:** Unity project scaffolded at the repo root, Git initialized (branch `main`), Freedoom Phase 1 placed under `Assets/StreamingAssets/wads/freedoom1.wad`.
- **Stage 1:** WAD reader implemented in `Assets/Scripts/Wad/` (asmdef `Doom.Wad`, `noEngineReferences: true`). Public surface: `WadFile.Open(path)`, `Header`, `Directory`, `FindLump`, `ReadLump`, plus `WadMapNames.IsMapMarker`. Editor menu `Tools > Doom > Dump freedoom1.wad` confirms it reads the bundled WAD end-to-end. 30 EditMode tests (unit + integration) pass via Unity Test Framework.
- **Stage 2:** Map geometry pipeline implemented in `Assets/Scripts/Map/` (asmdef `Doom.Map`, `noEngineReferences: true`) and `Assets/Scripts/MapBuild/` (Unity glue). High-level API: `MapData.Load(wad, mapName)` → `MapGeometryBuilder.Build(map)` → per-sector `SectorMeshes`. Triangulation via vendored LibTessDotNet v1.1.15 (`Assets/ThirdParty/LibTessDotNet/`). `MapLoader` MonoBehaviour with auto-bootstrap renders E1M1 grey block-out on Play in `Stage2_MapPreview.unity`. 62 EditMode tests pass including freedoom1.wad integration coverage.
- **Stage 3:** Player + collisions. `MapLoader` spawns Player from `THINGS` Type 1 at scene start; `PlayerController` MonoBehaviour (`Doom.MapBuild`) uses the new Input System (`com.unity.inputsystem`, hand-built `InputActionMap`) for WASD + mouse-look + Shift-sprint. `worldScale = 1/32` plumbed through `MapGeometryBuilder` so the player is ~1.75 m tall and gravity = -9.81 m/s² works natively. A PlayMode test asserts the player lands on E1M1's floor without falling through.

The master roadmap is in `docs/doom-unity-remake-plan.md` (written in Russian); detailed per-stage design specs live under `docs/superpowers/specs/`, named `YYYY-MM-DD-<topic>-design.md`: Stage 1 in `2026-05-28-wad-reader-design.md`, Stage 2 in `2026-05-28-geometry-design.md`, Stage 3 in `2026-05-29-player-design.md`, and Stage 4 in `2026-05-30-textures-design.md`. Stage 4 (palette and textures) is the next concrete work.

## What this project is

A from-scratch reimplementation of DOOM in **Unity + C#**. The original WAD files supply the data (geometry, textures, sprites, sounds); all rendering, physics, and game logic are written anew on top of Unity. No custom maps, textures, or assets are authored — everything comes from a WAD.

Development and distribution use **Freedoom** (free, format-compatible) rather than the commercial `DOOM.WAD`.

## Architectural ground rules

These decisions are load-bearing for the whole project — keep them in mind when designing any subsystem:

- **The original BSP data is intentionally discarded.** `NODES`, `SSECTORS`, `SEGS`, `BLOCKMAP`, `REJECT` are not read or used. Unity handles visibility and physics. Only the raw geometry lumps (`VERTEXES`, `LINEDEFS`, `SIDEDEFS`, `SECTORS`, `THINGS`) feed the level builder. Sector outlines are reconstructed by `SectorPolygonBuilder` (half-edge chaining with signed-area classification) — not by reading BSP segments.
- **Do not port the original C engine.** Port data formats and gameplay logic (sector behaviour, AI, weapon rules), not the renderer or BSP traversal.
- **Do not author custom content.** If a feature seems to need a hand-made texture, mesh, or map, that is a signal to re-read the WAD spec — the data is already there.
- **WAD binary format is little-endian.** Read with `BinaryReader` and stay consistent.
- **Wall texture pipeline is multi-step.** Wall textures are composed from patches via `TEXTURE1`/`TEXTURE2` + `PNAMES`, in column-major format with transparency. Flats (floor/ceiling) are raw 64×64 palette indices. Both go through `PLAYPAL` (256-color palette) before becoming `Texture2D`.
- **Sprites are camera-facing billboards**, with frame/angle encoded in the lump name.
- **World scale is `1 DOOM unit × worldScale = 1 Unity meter`**, with `worldScale = 1f / 32f` as the project default. Plumbed through `MapGeometryBuilder` so `MeshData` outputs are already in meters; `MapLoader` does not re-scale. Player height (56 DOOM units) becomes ~1.75 m; gravity is -9.81 m/s² natively.

## Roadmap shape

The plan (`docs/doom-unity-remake-plan.md`) is structured as 8 stages (0–7), each ending in a visible, runnable milestone:

0. Environment setup → 1. WAD reader → 2. Grey block-out geometry of one map → 3. First-person player with collisions → 4. Palette and textures → 5. Sprite objects → 6. Gameplay (HP, weapons, AI, doors, sound) → 7. Polish (HUD, menus, saves, optimization).

Prefer landing each stage as its own visible milestone rather than building several layers in parallel — the plan explicitly calls out that most DOOM remakes die on textures, sprites, and AI, so each step should produce something you can see and run.

## Project layout

- `Assets/` — Unity assets (scripts, scenes, meshes — once we write them)
- `Assets/Materials/MapBlockOut/` — three Standard-shader materials (`Floor.mat`, `Ceiling.mat`, `Wall.mat`) for grey block-out rendering.
- `Assets/Scenes/Stage2_MapPreview.unity` — preview scene; auto-bootstrap creates `MapLoader` on Play, loads E1M1 from `freedoom1.wad`.
- `Assets/Scripts/Map/` — WAD geometry → C# `MeshData` (asmdef `Doom.Map`, `noEngineReferences: true`). Houses `MapData` (lump parsers + `Load`), `SectorPolygonBuilder` (half-edge chaining), `SectorTriangulator` (LibTessDotNet wrapper), `WallMeshBuilder`, and the `MapGeometryBuilder` orchestrator.
- `Assets/Scripts/MapBuild/` — Unity-coupled glue (asmdef `Doom.MapBuild`). `MapLoader` MonoBehaviour turns `MeshData` into `UnityEngine.Mesh`, instantiates per-sector `GameObject` hierarchy with `MeshFilter`/`MeshRenderer`/`MeshCollider`, and auto-bootstraps on scene load.
- `Assets/Scripts/Wad/` — WAD parser. Pure C# under asmdef `Doom.Wad` with `noEngineReferences: true` (the parser must not depend on `UnityEngine` — see architectural rules above).
- `Assets/Scripts/Wad/Editor/` — Editor-only tools (asmdef `Doom.Wad.Editor`, `includePlatforms: ["Editor"]`). Currently houses `WadInspectorMenu` (`Tools > Doom > Dump freedoom1.wad`).
- `Assets/StreamingAssets/wads/` — WAD files, copied verbatim into builds. **WADs live here, not in regular `Assets/`**, because anything else under `Assets/` goes through Unity's importer, which would try to interpret the binary as some asset type. `StreamingAssets` is the one Unity folder that ships files unchanged and is reachable at runtime via `Application.streamingAssetsPath`.
- `Assets/Tests/EditMode/Map/` — NUnit EditMode tests for the Map/MapBuild pipeline (asmdef `Doom.Map.Tests`): unit tests on `SyntheticMapBuilder`-built lumps plus `freedoom1.wad` integration/regression coverage (see the test-suite breakdown under "Build, run, test").
- `Assets/Tests/EditMode/Wad/` — NUnit EditMode tests (asmdef `Doom.Wad.Tests`). Unit tests use a `SyntheticWadBuilder`; integration tests run against `freedoom1.wad`.
- `Assets/Tests/PlayMode/` — Unity PlayMode tests (asmdef `Doom.Stage3.PlayTests`). Runs in Play context; required for verifying `CharacterController` + collider integration. Run WITHOUT `-nographics` (PhysX needs a real/null gfx device for collision callbacks).
- `Assets/ThirdParty/LibTessDotNet/` — vendored LibTessDotNet v1.1.15 sources (SGI Free Software License B 2.0). Asmdef `LibTessDotNet` with `noEngineReferences: true`, `autoReferenced: false`.
- `Packages/`, `ProjectSettings/`, `UserSettings/` — Unity project metadata (committed; `UserSettings/` ignored per gitignore).
- `Library/`, `Temp/`, `Logs/`, `obj/` — Unity-generated, gitignored.
- `docs/` — design docs, including the stage-by-stage plan.

## Toolchain

- **Unity Editor:** `6000.4.8f1` at `C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe`. The version is pinned by `ProjectSettings/ProjectVersion.txt`; Unity Hub will auto-pick this editor when the project is opened.
- **Test WAD:** Freedoom v0.13.0 Phase 1 (`Assets/StreamingAssets/wads/freedoom1.wad`, ~28 MB), freely redistributable, so it is intentionally checked into Git — the repo should clone-and-run.

## Build, run, test

Day-to-day work happens inside the Unity Editor (open the repo root as a Unity project from Hub). No player build target is configured yet — that's a Stage 7 concern.

Useful CLI invocations:

```powershell
# Open the project in the editor (interactive)
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" -projectPath "D:\Development\doom"

# Headless: run EditMode tests (Unity Test Framework, NUnit-style)
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -nographics -projectPath "D:\Development\doom" `
    -runTests -testPlatform EditMode `
    -testResults "D:\Development\doom\Logs\test-results.xml" `
    -logFile "D:\Development\doom\Logs\test-run.log"

# Headless: run PlayMode tests (slower; physics needs no -nographics)
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -projectPath "D:\Development\doom" `
    -runTests -testPlatform PlayMode `
    -testResults "D:\Development\doom\Logs\playmode-results.xml" `
    -logFile "D:\Development\doom\Logs\playmode-run.log"

# Headless: execute the WAD dump menu without GUI
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -nographics -projectPath "D:\Development\doom" `
    -executeMethod Doom.Wad.Editor.WadInspectorMenu.DumpFreedoom1 `
    -logFile "D:\Development\doom\Logs\dump.log" -quit
```

**Test CLI gotcha:** `-runTests` controls its own exit; do NOT add `-quit` with it (Unity exits before the runner starts). Per-test PASS/FAIL only lands in the `-testResults` XML, not the editor log. Use `-quit` only with `-executeMethod` or pure-compile runs.

The current test suite is 73 EditMode + 1 PlayMode tests:
- 30 from Stage 1 (WAD reader): 4 integration tests against `freedoom1.wad`, the rest unit tests on `SyntheticWadBuilder`-built blobs.
- 32 from Stage 2 (Map pipeline): 5 integration tests against `freedoom1.wad`, the rest unit tests on `SyntheticMapBuilder`-built lumps.
- 9 from Stage 3 EditMode (Player): THINGS parsing (+2), THINGS as required lump in Load (+2), worldScale plumbing (+4), Freedoom Player-1-start integration (+1), plus 1 PlayMode test asserting the player lands on E1M1's floor.
- 2 geometry-robustness regressions on `freedoom1.wad` E1M1: `SectorPolygonBuilder` must produce simple (non-self-intersecting) sector rings, and `MapGeometryBuilder` output must contain no degenerate (zero-area) triangles. These pin the fixes for self-intersecting sector contours (greedy chaining → angular face-tracing) and zero-area wall quads.
