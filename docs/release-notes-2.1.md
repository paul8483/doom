# Doom Unity 3D Remake 2.1.0

A bug-fix and optimization release over 2.0.0: a full code review of the
port against vanilla DOOM behaviour, plus the performance and memory
hot spots it found. No new assets; Episode 1 with Freedoom Phase 1 as before.

### Vanilla-fidelity fixes (both modes)

**Map things and level flow**
- Things now respect the THINGS skill bits and the multiplayer-only flag
  (P_SpawnMapThing). Every map used to spawn all three skill layers plus
  deathmatch extras at once, and counted them in the kill/item totals. The
  port plays Ultra-Violence (no difficulty menu yet)
- The intermission (stats screen) is now shown in builds and waits for
  Enter / Space / Use / Fire; the world is frozen under it. Before, it was
  hidden by a test-only flag and every exit jumped straight to the loading
  plate
- The E1M8 exit floor (sector special 11) hurts and exits even with a
  radiation suit; the suit also leaks the vanilla 5/256 on specials 16/4

**Doors, floors, lifts**
- Doors reverse when they close on the player or a monster, and pressing
  Use on a closing manual door reopens it (a player can also slam an open
  one). Rising floors and lifts hold instead of pushing an actor through
  the ceiling
- Open-stay doors no longer close after the wait, and S1/W1 normal doors
  (29/108/111) now do
- Walk-over lines fire from either side (only teleports are one-way), as
  P_CrossSpecialLine does
- "Raise floor to next higher / by 24 / by 32" plat types (14/15/20/22/47/
  66/67/68/95) rise once and stay instead of running as down-wait-up lifts;
  "floor up 24/512" (58/59/92/93/140) and the ceiling movers (40/41/43/44/
  72) now execute; turbo "lower to highest +8" (36/70/71/98) and crushing
  raises (55/56/65/94) use the right target; stairs 7/8 build at the
  vanilla FLOORSPEED/4 and 100/127 climb 16 units per step
- "Lower to lowest floor" starts from the sector's own floor (a floor
  already at the bottom no longer rises)
- Type 22 is no longer monster-activatable; skull keys open the doors of
  their colour as in vanilla
- Two-sided middle textures honour ML_DONTPEGBOTTOM

**Lights**
- Glow / flash / fire-flicker sectors take their minimum from the sector's
  own level downward (P_FindMinSurroundingLight); a glow with no darker
  neighbour is static instead of pulsing to black. Sector specials 12/13
  had their dark times swapped; the special-1 flash now alternates with
  the vanilla 65/8-tic timing; linedef 17 strobes slow; linedef 104 uses
  the lowest of own + neighbours
- Classic walls get vanilla "fake contrast" (horizontal walls one light
  step darker, vertical one brighter)

**Monsters and weapons**
- A monster whose infight target died goes back to the player, and any
  hit from the player (subject to the 100-tic threshold) pulls it off its
  current target
- Monster missiles explode on, but do not damage, monsters of their own
  kind (imps never infight imps)
- Monsters find the door they bumped into (the blocked-step check resolved
  every wall to the line nearest the map origin)
- Monster teleports never telefrag; a monster arriving on an occupied pad
  simply fails to teleport
- MF_JUSTHIT is set only when the pain roll succeeds; ambush monsters wake
  on noise when they can see the player; the baron's melee rolls %8
- Radius damage (rockets, barrels) needs line of sight and uses the vanilla
  Chebyshev distance; barrels explode on frame D (15 tics after death), so
  chains ripple and the animation runs 25→35 tics
- Weapon auto-switch follows P_CheckAmmo (plasma → chaingun → shotgun →
  pistol → chainsaw → rocket → BFG with more than 40 cells → fist); the
  chaingun's first shot is exact
- Death drops give half ammo (MF_DROPPED); health/armor bonuses and the
  soulsphere are always consumed
- Floating skulls, twitching corpses, the evil eye, skull piles and the
  impaled twitching human animate at their info.c cadence

**Saves (schema v7, older saves still load)**
- Live monsters keep their AI state across save/load (they used to wake up
  asleep); a monster saved mid-fall finishes falling instead of freezing;
  a save taken as a fireball exploded no longer revives it as an immortal
  ball; death drops remember the half-ammo flag; nothing is picked up during
  the restore frames

**Audio**
- Music is synthesised at the device output rate (it played ~9% fast and
  1.5 semitones sharp on 48 kHz devices); MIDI tempo changes are honoured
  piecewise; door/lift motor loops keep their channel under load; the
  synth is guarded against the stop/render race

### Enhanced

- Hot-switching Classic → Enhanced (or 3D Off → On) no longer leaves static
  meshes — barrels, columns, torches, corpses — facing whatever yaw the
  billboard had last written to the shared root
- A restored corpse snaps into place instead of sliding for a quarter
  second; no unused fall mesh is built on load
- Emission masks are imported as linear data (they were sRGB, which crushed
  the glow at island borders and at distance)

### Performance and memory

- Sector lights rebind only the sectors whose level changed, through cached
  renderer lists; the old path walked every renderer in the map 35 times a
  second whenever any glow/flicker sector ticked (in Classic too)
- Sprite billboards reconfigure their shared material only on profile or
  material change, not every frame; shader property checks are cached
- Monster frame meshes are built when mesh presentation is first shown
  (Classic no longer instantiates 7–8 meshes per monster it never draws)
- Redraw PNGs are no longer re-decoded on disk-pack hits, and the ~240 MB
  of decoded level-0 images is released after the warm; texture uploads
  reuse pooled flip buffers instead of allocating per mip level
- Sector adjacency is computed once per map (noise alerts and sector
  rebuilds scanned every linedef per call); per-frame floor probes use
  non-allocating raycasts; the HUD no longer builds ~40 strings per frame;
  options nudges no longer re-run every 3D model's presentation cascade
- The WAD handle and log hooks of an interrupted level build are released on
  teardown; the main-menu HUD textures are destroyed when replaced

### Compatibility

- Saves from 1.x and 2.0 load (schemas v1–v6 migrate on read; v7 adds
  monster AI state and the dropped flag)
- Settings unchanged (schema v4)

### Download

- `DoomUnity-2.1.0-windows-x64.zip` — unpack and run `DoomUnity.exe`
- `DoomUnity-2.1.0-linux-x64.zip` — unpack and run `DoomUnity.x86_64`

### Notes

- Known limits carried over: Classic texture animations are still static;
  raise-and-change specials move the floor but do not change the flat;
  no difficulty menu (Ultra-Violence)
