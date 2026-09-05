# Doom Unity 3D Remake 2.1.1

A patch release over 2.1.0 that closes the bugs found while playing the
2.1.0 build, plus the vanilla-fidelity tails they uncovered. No new assets;
Episode 1 with Freedoom Phase 1 as before.

### Fixes (both modes)

**The endless "jump" grunt**
- A pack of monsters standing at a keyed door no longer "uses" it: the
  port let monsters use every push door, ran the PLAYER's key check for
  them and played the 2D key-denial grunt four times a second, audible
  anywhere on the map, for as long as they stood there (E1M4 blue door).
  As in vanilla P_UseSpecialLine, a monster opens only the manual door
  (special 1); keyed doors, D1 open-stay and blaze doors are walls to it,
  so it picks a new direction instead of waiting at them

**Doors, floors, lifts**
- Movers resumed from a save keep their sound and their dwell: floors and
  lifts in motion grind at once, a door saved mid-open waits VDOORWAIT at
  the top and closes with its cue (restored movers used to run silent and
  a mid-open door shut the moment it finished opening)
- Lifts sound like T_PlatRaise: pstart when they set off (down, and again
  up after the wait), pstop at the bottom and at the top, and no motor
  loop — the DSSTNMOV loop stays with floors and ceilings, where vanilla
  plays it every 8 tics
- A lift rising into an actor heads back down, waits and tries again
  (T_PlatRaise "crushed"), instead of holding in place until the actor
  leaves — a monster stuck on a raised pillar used to hold it forever
- A stop cue was dropped whenever the mover had no motor loop registered
  (the lift's top pstop)

**Sounds**
- Key denial grunts sfx_oof (EV_VerticalDoor / EV_DoLockedDoor); the port
  used sfx_noway, which belongs to P_UseLines
- Using a bare wall — a line without a special and without an opening —
  grunts sfx_noway at the player (PTR_UseTraverse); the port never played
  it before

**Saves (schema v8, older saves still load)**
- A crusher started by the silent special (141) stays silent after a
  load; v7 and older saves read their crushers as loud

### Compatibility

- Saves from 1.x, 2.0 and 2.1.0 load (schemas v1–v7 migrate on read; v8
  adds the silent-crusher flag)
- Settings unchanged (schema v4)

### Download

- `DoomUnity-2.1.1-windows-x64.zip` — unpack and run `DoomUnity.exe`
- `DoomUnity-2.1.1-linux-x64.zip` — unpack and run `DoomUnity.x86_64`

### Notes

- Known limits carried over from 2.1.0: Classic texture animations are
  still static; raise-and-change specials move the floor but do not change
  the flat; no difficulty menu (Ultra-Violence)
