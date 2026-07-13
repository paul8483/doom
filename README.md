# Doom Unity Remake

A from-scratch remake of the original DOOM in **Unity + C#**. Maps, textures, sprites, and sounds come from WAD files; rendering, physics, and gameplay logic are written anew. No custom maps, textures, or assets are authored.

Development and distribution use **[Freedoom](https://freedoom.github.io/)** (Phase 1) — a free IWAD that is format-compatible with DOOM. The commercial `DOOM.WAD` is not required.

## Status

**Stages 0–8 are complete.** Full Episode 1 (E1M1–E1M9) is playable: movement, doors/lifts, weapons, monsters, pickups, sound/music, HUD, menus, saves, and level transitions.

Two graphics modes — **Classic** and **Enhanced** — switch live from Options without reloading the map or changing gameplay.

| Area | What’s included |
| --- | --- |
| Geometry | Sectors, walls, floors/ceilings from the WAD (no BSP) |
| Player | FPS controls, collisions, HP/armor, keys, powers |
| Weapons | Fist, pistol, shotgun, chaingun, chainsaw, rocket, plasma, BFG |
| Monsters | E1 roster (zombies, imps, demons, barons, spectre) + AI |
| Specials | Doors, lifts, stairs, teleports, exits, lights, floor damage, barrels |
| Audio | DMX SFX + OPL3 music (MUS/MIDI from the WAD) |
| UI | WAD HUD, menus, intermission, settings, save slots |
| Graphics | URP 17, Classic / Enhanced modes |

## Classic and Enhanced

Both modes share the same geometry, physics, AI, and WAD data. Switching only changes the visual profile (materials, lights, post-processing). HUD, menus, and the weapon viewmodel always stay sharp (virtual 320×200 screen, nearest-neighbour scaling).

### Classic (default)

Closest to the classic WAD-driven look:

- Point filtering for textures and sprites
- Unlit shaders: albedo × sector light, no Unity Lights
- No bloom, SSAO, fog, normal maps, particles, decals, or real-time shadows
- Original WAD texture resolution

### Enhanced

The same WAD content through a modern URP path (no authored replacement textures or models):

- Lit materials + runtime normal maps derived from WAD texture luminance
- Bilinear/trilinear sampling with mipmaps / anisotropic filtering
- Dynamic lights (muzzle flash, projectiles, lamps, explosions) and a bounded shadow pool
- Post-processing: HDR, SSAO, soft bloom, fog, MSAA
- WAD `SKY*` sky, animated fluids (nukage/lava), particles, and decals
- Sprites lit by sector ambient + local lights; dedicated spectre material

Switch via **Options → Graphics Mode → Classic | Enhanced**.

## Getting started

1. Install **Unity 6000.4.8f1** (pinned in `ProjectSettings/ProjectVersion.txt`).
2. Open the repository root as a Unity project.
3. Open `Assets/Scenes/Stage2_MapPreview.unity` and press Play — E1M1 loads from `Assets/StreamingAssets/wads/freedoom1.wad`.

Windows standalone: editor menu **Tools → Doom → Build Windows Standalone** → `Builds/Windows/`.

## Architecture (brief)

- Original BSP lumps (`NODES`, `SSECTORS`, `SEGS`, `BLOCKMAP`, `REJECT`) are **not used** — Unity handles visibility and physics.
- Pure C# assemblies without `UnityEngine` (`Doom.Wad`, `Doom.Map`, `Doom.Graphics`, `Doom.Game`, …) are separated from Unity glue (`Doom.MapBuild`).
- World scale: `1 DOOM unit × 1/32 = 1 meter` — player ~1.75 m, gravity −9.81 m/s² natively.
- All visual content is decoded from the WAD at runtime (`PLAYPAL`, flats, patches, `TEXTURE1`/`PNAMES`, sprites, DMX, MUS/MIDI).

Stage roadmap: [`docs/doom-unity-remake-plan.md`](docs/doom-unity-remake-plan.md). Design specs and implementation plans live under `docs/superpowers/`.

## Tests

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
| Freedoom Phase 1 | BSD-like (Freedoom) | `Assets/StreamingAssets/wads/freedoom1.wad` |

This is an independent reimplementation. Not affiliated with id Software / Bethesda / Microsoft.
