# Doom Unity 3D Remake

**Not a source port.** The engine here is written from scratch in **Unity + C#** — rendering, physics, monster AI, sound, savegames. The only thing taken from DOOM is its data format: maps, sprites and sounds stream from the WAD at runtime.

And it doesn't stop at reimplementation. The **Enhanced** mode regenerates the game's presentation from its own pixel art: every monster and object becomes a **stop-motion 3D model** derived from its WAD sprite, and every wall, floor and weapon of Episode 1 is **redrawn at 4×** — while the **Classic** mode keeps the bit-faithful WAD look. The two switch live, mid-game.

Development and distribution use **[Freedoom](https://freedoom.github.io/)** (Phase 1) — a free IWAD that is format-compatible with DOOM. The commercial `DOOM.WAD` is not required.

**Play without the Editor:** download Windows or Linux builds from [Releases](https://github.com/paul8483/doom/releases) (current: **v2.0.0**).

## Screenshots

*(coming soon: Classic vs Enhanced side-by-side, 3D monsters in combat, redrawn corridors)*

## What's inside

Full Episode 1 (E1M1–E1M9) is playable end-to-end: movement, doors/lifts, weapons, monsters, pickups, sound/music, HUD, menus, saves, and level transitions.

| Area | What's included |
| --- | --- |
| Geometry | Sectors, walls, floors/ceilings from the WAD (no BSP) |
| Player | FPS controls, collisions, HP/armor, keys, powers |
| Weapons | Fist, pistol, shotgun, chaingun, chainsaw, rocket, plasma, BFG |
| Monsters | E1 roster (zombies, imps, demons, barons, spectre) + AI |
| Specials | Doors, lifts, stairs, teleports, exits, lights, floor damage, barrels, switches |
| Audio | DMX SFX + OPL3 music (MUS/MIDI from the WAD) |
| UI | WAD HUD, menus, intermission, settings, save slots |
| Graphics | URP 17, live Classic / Enhanced switch |

## Classic and Enhanced

Both modes share the same geometry, physics, AI, and WAD data. Switching only changes the presentation; menus and intermission stay at native WAD resolution.

### Classic (default)

Closest to the classic WAD-driven look:

- Point filtering for textures and sprites
- Unlit shaders: albedo × sector light, no Unity Lights
- No bloom, SSAO, fog, normal maps, particles, decals, or real-time shadows
- Original WAD texture resolution (HUD / weapon view on a virtual 320×200 screen)

### Enhanced

The game's own pixel art, regenerated:

- **3D objects and monsters** — every pickup, decoration, torch, pillar, lamp, barrel, tree and corpse is a textured mesh derived from its WAD sprite (~40k tris, native sprite palette, tone matched to the sprite); all five E1 monsters run stop-motion 3D through walk, attack, pain and the whole death chain, the spectre as a translucent ghost mesh
- **Redrawn world** — exact-4× redraws cover every wall, floor and ceiling of the episode (doors, switches, masked grates with true alpha, animated waterfalls with frame-consistent scrolling); first-person weapons redrawn for all eight guns; the sky stays native
- Multi-scale normals + parallax on solid surfaces, texel-aware sampling, per-fixture lamp flicker
- Dynamic lights (muzzle flash, projectiles, lamps, explosions) and a bounded shadow pool
- Post-processing: HDR, SSAO, soft bloom, sector fog, MSAA
- WAD `SKY*` sky sphere, animated fluids with cross-fade, particles, decals
- Session / disk-pack warm cache so level transitions do not recompute

Switch via **Options → Graphics Mode → Classic | Enhanced**.

## Getting started

### Play a release build

1. Grab `DoomUnity-*-windows-x64.zip` or `DoomUnity-*-linux-x64.zip` from [Releases](https://github.com/paul8483/doom/releases).
2. Unpack and run `DoomUnity.exe` (Windows) or `DoomUnity.x86_64` (Linux; `chmod +x` if needed).
3. Freedoom Phase 1 is already bundled under StreamingAssets.

### Develop in the Editor

1. Install **Unity 6000.4.8f1** (pinned in `ProjectSettings/ProjectVersion.txt`). For Linux player builds, also install **Linux Build Support (Mono)** for that Editor.
2. Open the repository root as a Unity project.
3. Open `Assets/Scenes/Stage2_MapPreview.unity` and press Play — E1M1 loads from `Assets/StreamingAssets/wads/freedoom1.wad`.

Standalone builds from the Editor:

- **Tools → Doom → Build Windows Standalone** → `Builds/Windows/`
- **Tools → Doom → Build Linux Standalone** → `Builds/Linux/`

Both builds ship a `Licenses/` folder with the third-party license texts.

## Architecture (brief)

- Original BSP lumps (`NODES`, `SSECTORS`, `SEGS`, `BLOCKMAP`, `REJECT`) are **not used** — Unity handles visibility and physics.
- Pure C# assemblies without `UnityEngine` (`Doom.Wad`, `Doom.Map`, `Doom.Graphics`, `Doom.Game`, …) are separated from Unity glue (`Doom.MapBuild`).
- World scale: `1 DOOM unit × 1/32 = 1 meter` — player ~1.75 m, gravity −9.81 m/s² natively.
- All visual content is decoded from the WAD at runtime (`PLAYPAL`, flats, patches, `TEXTURE1`/`PNAMES`, sprites, DMX, MUS/MIDI).
- The WAD stays the source of identity: gameplay, collision and saves run on WAD data in every mode. Enhanced assets are a derived presentation layer keyed to their source lumps — each mesh and redraw traces back to the sprite or texture it was generated from.

Stage roadmap: [`docs/doom-unity-remake-plan.md`](docs/doom-unity-remake-plan.md). Design specs and implementation plans live under `docs/superpowers/`.

## Tests

**709 EditMode + 197 PlayMode** tests (Unity Test Framework):

```powershell
# EditMode
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
  -batchmode -nographics -projectPath "<repo>" `
  -runTests -testPlatform EditMode `
  -testResults "<repo>\Logs\test-results.xml" `
  -logFile "<repo>\Logs\test-run.log"

# PlayMode (omit -nographics — PhysX needs a graphics device)
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
  -batchmode -projectPath "<repo>" `
  -runTests -testPlatform PlayMode `
  -testResults "<repo>\Logs\playmode-results.xml" `
  -logFile "<repo>\Logs\playmode-run.log"
```

Do not add `-quit` together with `-runTests`.

## Third-party licenses

| Component | License | Path |
| --- | --- | --- |
| LibTessDotNet | SGI Free B 2.0 | `Assets/ThirdParty/LibTessDotNet/` |
| Nuked OPL3 (managed) | LGPL-2.1 | `Assets/ThirdParty/NukedOpl/` |
| Super-xBR | MIT | `Assets/ThirdParty/SuperXbr/` |
| Freedoom Phase 1 | BSD-like (Freedoom) | `Assets/StreamingAssets/wads/freedoom1.wad` |

This is an independent reimplementation. Not affiliated with id Software / Bethesda / Microsoft.
