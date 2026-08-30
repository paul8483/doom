# Doom Unity Remake 3D 2.0.0 (draft)

*(Release title gains «3D» from 2.0 on — the headline change of this
generation; the executable and zip names keep the DoomUnity prefix, and
productName/companyName stay untouched so 1.x saves keep their path.)*

The Enhanced mode generation update: AI-derived 3D objects and monsters,
and a full-episode set of redrawn world textures. Episode 1 remains fully
playable with Freedoom Phase 1 bundled.

### What's new since 1.0.3

**Enhanced 3D objects (AI-derived from the WAD sprites)**
- Every pickup, decoration, torch, pillar, lamp, barrel, tree and corpse of
  the episode is now a textured 3D mesh generated from its own WAD sprite
  (TRELLIS.2 + a deterministic doomify pass: ~40k tris, native sprite
  palette, tone matched to the sprite on screen)
- All five E1 monsters run stop-motion 3D, Voxel-Doom style: walk, attack,
  pain, and the whole death chain down to the body on the floor; XDEATH gib
  pools and map-placed dead marines are meshes too
- The spectre is a translucent ghost mesh (MF_SHADOW analog with UV
  shimmer); Classic and 3D Off keep the sprite fuzz billboard
- The imp fireball is a volumetric ball with a hot core; zombie gunfire
  shows shader muzzle bursts baked from native sprite texels
- Options → «3D Objects» On/Off toggles the whole layer at runtime

**Redrawn world textures (Enhanced)**
- Full episode coverage with exact-4× GPT redraws: all walls (15 waves —
  panels, brick, metal, doors, switches, hell stone, masked grates with
  true alpha), all floors and ceilings, and the animated waterfalls with
  frame-consistent scrolling and cross-fade. The only exception is the sky
  (native)
- First-person weapons: 4× redraws for all eight guns, muzzle flashes
  included
- EdgeMix 8× from 1.0.2/1.0.3 is removed entirely — Enhanced sprites now
  route mesh → redraw → native, with no transitional blur

**Classic-fidelity fixes (both modes)**
- Switches now change texture when pressed (P_ChangeSwitchTexture port
  with the pressed/unpressed pair swap; the port never did this before)
- Decorations, torches and power-up spheres animate at their vanilla
  cadence; per-fixture lamp flicker on light panels in Enhanced
- Flat/wall texture name collision (STEP1/STEP2) resolved — 57 surfaces
  were silently rendering the wrong image since 1.0.x

**Distribution**
- The build now ships a `Licenses/` folder: Freedoom (BSD), LibTessDotNet
  (SGI Free B 2.0), Nuked OPL3 (LGPL-2.1), Super-xBR (MIT)

### Compatibility

- Saves from 1.x load (schemas v1–v6 migrate on read); saves from a newer
  version are rejected with a clear message
- Settings from 1.x are picked up; an unknown schema safely falls back to
  defaults

### Download

- `DoomUnity-2.0.0-windows-x64.zip` — unpack and run `DoomUnity.exe`
- `DoomUnity-2.0.0-linux-x64.zip` — unpack and run `DoomUnity.x86_64`
  (`chmod +x` if needed)

### Requirements

- 64-bit Windows or 64-bit Linux
- No Unity Editor needed

### Notes

- IWAD: Freedoom Phase 1 (`freedoom1.wad`) is included via StreamingAssets
- Commercial `DOOM.WAD` is not required
- Classic mode is unchanged; switch Classic/Enhanced in Options
- Known limits: the sky stays native; Classic texture animations are still
  static (vanilla flips them every 8 tics) — candidate for 2.x; menus and
  intermission stay native
- macOS builds are not in this release
