# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project status

Stage 0 of the plan is done: Unity project scaffolded at the repo root, Git initialized (default branch `main`), Freedoom Phase 1 placed under `Assets/StreamingAssets/wads/freedoom1.wad`. No gameplay code exists yet — Stage 1 (WAD reader) is the next concrete work.

The plan is in `docs/doom-unity-remake-plan.md` (written in Russian).

## What this project is

A from-scratch reimplementation of DOOM in **Unity + C#**. The original WAD files supply the data (geometry, textures, sprites, sounds); all rendering, physics, and game logic are written anew on top of Unity. No custom maps, textures, or assets are authored — everything comes from a WAD.

Development and distribution use **Freedoom** (free, format-compatible) rather than the commercial `DOOM.WAD`.

## Architectural ground rules

These decisions are load-bearing for the whole project — keep them in mind when designing any subsystem:

- **The original BSP data is intentionally discarded.** `NODES`, `SSECTORS`, `SEGS`, and `BLOCKMAP` are not read or used. Unity handles visibility and physics. Only the raw geometry lumps (`VERTEXES`, `LINEDEFS`, `SIDEDEFS`, `SECTORS`, `THINGS`) feed the level builder, which generates Unity meshes for floors, ceilings, and walls.
- **Do not port the original C engine.** Port data formats and gameplay logic (sector behaviour, AI, weapon rules), not the renderer or BSP traversal.
- **Do not author custom content.** If a feature seems to need a hand-made texture, mesh, or map, that is a signal to re-read the WAD spec — the data is already there.
- **WAD binary format is little-endian.** Read with `BinaryReader` and stay consistent.
- **Wall texture pipeline is multi-step.** Wall textures are composed from patches via `TEXTURE1`/`TEXTURE2` + `PNAMES`, in column-major format with transparency. Flats (floor/ceiling) are raw 64×64 palette indices. Both go through `PLAYPAL` (256-color palette) before becoming `Texture2D`.
- **Sprites are camera-facing billboards**, with frame/angle encoded in the lump name.

## Roadmap shape

The plan (`docs/doom-unity-remake-plan.md`) is structured as 8 stages (0–7), each ending in a visible, runnable milestone:

0. Environment setup → 1. WAD reader → 2. Grey block-out geometry of one map → 3. First-person player with collisions → 4. Palette and textures → 5. Sprite objects → 6. Gameplay (HP, weapons, AI, doors, sound) → 7. Polish (HUD, menus, saves, optimization).

Prefer landing each stage as its own visible milestone rather than building several layers in parallel — the plan explicitly calls out that most DOOM remakes die on textures, sprites, and AI, so each step should produce something you can see and run.

## Project layout

- `Assets/` — Unity assets (scripts, scenes, meshes — once we write them)
- `Assets/StreamingAssets/wads/` — WAD files, copied verbatim into builds. **WADs live here, not in regular `Assets/`**, because anything else under `Assets/` goes through Unity's importer, which would try to interpret the binary as some asset type. `StreamingAssets` is the one Unity folder that ships files unchanged and is reachable at runtime via `Application.streamingAssetsPath`.
- `Packages/`, `ProjectSettings/`, `UserSettings/` — Unity project metadata (committed; `UserSettings/` ignored per gitignore).
- `Library/`, `Temp/`, `Logs/`, `obj/` — Unity-generated, gitignored.
- `docs/` — design docs, including the stage-by-stage plan.

## Toolchain

- **Unity Editor:** `6000.4.8f1` at `C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe`. The version is pinned by `ProjectSettings/ProjectVersion.txt`; Unity Hub will auto-pick this editor when the project is opened.
- **Test WAD:** Freedoom v0.13.0 Phase 1 (`Assets/StreamingAssets/wads/freedoom1.wad`, ~28 MB), freely redistributable, so it is intentionally checked into Git — the repo should clone-and-run.

## Build, run, test

The Unity project still has no scenes or scripts, so there is no build yet. Day-to-day work happens inside the Unity Editor (open the repo root as a Unity project from Hub). Useful CLI invocations when needed:

```powershell
# Open the project in the editor (interactive)
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" -projectPath "D:\Development\doom"

# Headless: run EditMode + PlayMode tests once Unity Test Framework is in use
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
    -batchmode -nographics -projectPath "D:\Development\doom" `
    -runTests -testPlatform EditMode -logFile - -quit
```

There are no tests, scenes, or build targets configured yet — extend this section as those land.
