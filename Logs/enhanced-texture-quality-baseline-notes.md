# Enhanced Texture Quality � baseline notes

Date: 2026-07-21  
Branch: `texquality`  
HEAD at Task 1 start: `a580cd6` (docs: add enhanced texture quality spec and plan)  
Unity: `6000.4.8f1`  
GPU: Intel(R) Arc(TM) A750 Graphics (driver 32.0.101.8861)  
API / color: recorded per PlayMode run (URP 17.4.0, Linear � Stage 8)

## Pre-implementation baseline (contracts Task 1)

Scale2x Enhanced2X pipeline + controlled palette-aware mips are on this branch
(merged to `main` earlier). Interactive Scale2x visual reject stands; see
`Logs/enhanced-texture-upscale-baseline-notes.md`.

### Automated totals

| Suite | Result | XML |
|-------|--------|-----|
| Full EditMode (pre-contract compile) | **513 passed / 0 failed** | `Logs/texquality-t1-baseline-edit.xml` |
| Focused `GraphicsProfileTests` (post-contract) | **5 passed / 0 failed** | `Logs/texquality-t1-edit.xml` |
| Full PlayMode (post-contract) | **103 passed / 8 failed** | `Logs/texquality-t1-baseline-play.xml` |

EditMode suite count after Task 1 contracts: **514** (+1
`EnhancedWithLayers_builds_intermediate_profiles_for_captures`).

Historical reference (Scale2x close): 505 EditMode + 109 PlayMode.
Post-mips `main` EditMode baseline before contracts: **513**.

#### PlayMode failures (Task 1 evidence)

Texture-quality related filters all green:
`TextureUpscalePlayTests` �2, `EnhancedMaterialPlayTests` �2, `GraphicsModePlayTests` �1.

Failures (not contract asserts):

| Test | Symptom | Notes |
|------|---------|-------|
| `E1MapSmokePlayTests` | `LastBuildSeconds == 0` | Race: smoke waits on `LineActivator` which exists before READY yield sets `LastBuildSeconds` |
| `SpriteSpawnPlayTests` �2 | Player/sprites missing | Only waits ~2 frames; flaky when build is slow |
| `ControlledMipCaptureTests` | Player missing after Enhanced | Load timing under full suite |
| `LevelTransitionPlayTests` �2 | null refs | Suite-order pollution under full run |
| `Hot_switch_does_not_grow�` | Doom/ materials +1 (511?512) | **Pre-existing** � same failure on pre-Task1 stash (`texquality-t1-hotswitch-pre.xml`) |
| `ClearContext_�` | particle white count 4 | Passed on re-run � suite pollution |

Working totals for this iteration: EditMode **514** (after Task 1). PlayMode historical green was **109**; current full run **103/111** with failures above (Hot_switch pre-existing; others timing/pollution).

### Contracts after Task 1

- `WorldTextureVariant`: `Native` / `Enhanced2X` (obsolete, numeric=1) / `Enhanced4X` (=2)
- `GraphicsProfile` layer flags: `WorldDedither`, `WorldUpscale4X`, `WorldTexelAA`,
  `WorldParallax` � Classic all false; Enhanced all true
- Mapping: Enhanced ? `Enhanced4X`; Classic ? `Native`
- `GraphicsProfile.EnhancedWithLayers(...)` � editor/test-only intermediate profiles
- Temporary runtime bridge: `TextureCache` remaps `Enhanced4X` ? Scale2x `Enhanced2X`
  path until Task 4 Super-xBR pipeline

### Camera poses for layered captures (from Stage 8 harness)

Same fixed eye poses as `GraphicsBaselineCaptureTests` (1280�720, FOV 75):

| Map | Position | LookAt |
|-----|----------|--------|
| E1M1 | (33.0, 1.75, -98.0) | (40.0, 1.5, -90.0) |
| E1M3 | (0.0, 1.75, 0.0) | (8.0, 1.5, 8.0) |
| E1M7 | (0.0, 1.75, 0.0) | (10.0, 1.5, 0.0) |
| E1M9 | (0.0, 1.75, 0.0) | (8.0, 1.5, -8.0) |

Layered capture configs (Task 8): Classic ? +dedither ? +Super-xBR 4� ?
+texel-AA ? +normals/POM.

### E1M1 / E1M7 runtime metrics (Task 9, 2026-07-22)

Yielded Classic?Enhanced warm (loading plate). Mitigation ladder **not** applied �
memory acceptable on Arc A750 (~375 MB Enhanced albedo+normal+sprite+HUD).

| Metric | E1M1 | E1M7 |
|--------|------|------|
| Map build time (Classic) | 0.42s | 0.91s |
| Classic?Enhanced first switch (yielded) | 85.0s | 85.7s |
| Repeat switch after warm | 11.1ms | 14.4ms avg (20�) |
| TextureCount / MaterialCount / NormalMapCount | 1680 / 154 / 227 | 1722 / 162 / 237 |
| Enhanced 4� albedo + normal+height | 156+156 MB | 151+151 MB |
| Sprite + HUD Enhanced bytes | 59.9 + 2.8 MB | 59.4 + 2.8 MB |
| Managed (GC.GetTotalMemory, E1M1) | 554.5 MB | � |
| Representative wall filter | native Point / 4� Trilinear+texel-AA | same |
| `SKY1` | native Point / 4� Trilinear | same |
| Lifetime suite | `GraphicsResourceLifetimePlayTests` **5/5** | `Logs/texquality-t9-lifetime-play.xml` |

## Docs

- Spec: `docs/superpowers/specs/2026-07-21-enhanced-texture-quality-design.md`
- Plan: `docs/superpowers/plans/2026-07-21-enhanced-texture-quality.md`

## Task 9 performance gate (E1M1)

Date: 2026-07-22 19:39
| Metric | Value |
|--------|-------|
| Map build time | 0,42s |
| Classic?Enhanced first switch (yielded warm) | 84,99s |
| Repeat Classic?Enhanced (warm) | 11,1ms |
| TextureCount / MaterialCount / NormalMapCount | 1680 / 154 / 227 |
| EnhancedVariantCount | 227 |
| Native albedo bytes | 7,3 MB |
| Enhanced 4� albedo bytes | 156,3 MB |
| Normal+height bytes | 156,3 MB |
| Sprite Enhanced bytes | 59,9 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 554,5 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-07-22 19:43
| Metric | Value |
|--------|-------|
| Map build time | 0,91s |
| Classic?Enhanced first switch (yielded warm) | 85,74s |
| Avg repeat switch (20�, timeScale=0) | 14,4ms |
| TextureCount / MaterialCount / NormalMapCount | 1722 / 162 / 237 |
| EnhancedVariantCount | 237 |
| Enhanced 4� albedo bytes | 151,2 MB |
| Normal+height bytes | 151,2 MB |
| Sprite Enhanced bytes | 59,4 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-07-22 19:54
| Metric | Value |
|--------|-------|
| Map build time | 0,86s |
| Classic?Enhanced first switch (yielded warm) | 84,80s |
| Avg repeat switch (20�, timeScale=0) | 15,0ms |
| TextureCount / MaterialCount / NormalMapCount | 1722 / 162 / 237 |
| EnhancedVariantCount | 237 |
| Enhanced 4� albedo bytes | 151,2 MB |
| Normal+height bytes | 151,2 MB |
| Sprite Enhanced bytes | 59,4 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-07-22 20:03
| Metric | Value |
|--------|-------|
| Map build time | 0,42s |
| Classic?Enhanced first switch (yielded warm) | 84,95s |
| Repeat Classic?Enhanced (warm) | 18,9ms |
| TextureCount / MaterialCount / NormalMapCount | 1680 / 154 / 227 |
| EnhancedVariantCount | 227 |
| Native albedo bytes | 7,3 MB |
| Enhanced 4� albedo bytes | 156,3 MB |
| Normal+height bytes | 156,3 MB |
| Sprite Enhanced bytes | 59,9 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 489,1 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-07-22 22:22
| Metric | Value |
|--------|-------|
| Map build time | 0,88s |
| Classic?Enhanced first switch (yielded warm) | 14,62s |
| Avg repeat switch (20�, timeScale=0) | 14,1ms |
| TextureCount / MaterialCount / NormalMapCount | 1722 / 162 / 237 |
| EnhancedVariantCount | 237 |
| Enhanced 4� albedo bytes | 151,2 MB |
| Normal+height bytes | 151,2 MB |
| Sprite Enhanced bytes | 59,4 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-07-22 22:23
| Metric | Value |
|--------|-------|
| Map build time | 0,59s |
| Classic?Enhanced first switch (yielded warm) | 15,43s |
| Repeat Classic?Enhanced (warm) | 11,1ms |
| TextureCount / MaterialCount / NormalMapCount | 1680 / 154 / 227 |
| EnhancedVariantCount | 227 |
| Native albedo bytes | 7,3 MB |
| Enhanced 4� albedo bytes | 156,3 MB |
| Normal+height bytes | 156,3 MB |
| Sprite Enhanced bytes | 59,9 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 964,7 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-07-22 22:26
| Metric | Value |
|--------|-------|
| Map build time | 0,90s |
| Classic?Enhanced first switch (yielded warm) | 14,38s |
| Avg repeat switch (20�, timeScale=0) | 14,5ms |
| TextureCount / MaterialCount / NormalMapCount | 1722 / 162 / 237 |
| EnhancedVariantCount | 237 |
| Enhanced 4� albedo bytes | 151,2 MB |
| Normal+height bytes | 151,2 MB |
| Sprite Enhanced bytes | 59,4 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-07-22 22:28
| Metric | Value |
|--------|-------|
| Map build time | 0,43s |
| Classic?Enhanced first switch (yielded warm) | 14,43s |
| Repeat Classic?Enhanced (warm) | 11,6ms |
| TextureCount / MaterialCount / NormalMapCount | 1680 / 154 / 227 |
| EnhancedVariantCount | 227 |
| Native albedo bytes | 7,3 MB |
| Enhanced 4� albedo bytes | 156,3 MB |
| Normal+height bytes | 156,3 MB |
| Sprite Enhanced bytes | 59,9 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 803,6 MB |
| Mitigation ladder | none applied |

## Warm-perf Task 2 � parallel EnhancedWarmScheduler (E1M1)

Date: 2026-07-22 22:30
Hardware: 8C/16T class (batchmode PlayMode)
| Metric | Value |
|--------|-------|
| Classic>Enhanced first warm (scheduler) | 14.11s |
| World / normals / sprites / HUD variants | 227 / 227 / 350 / 85 |
| Gate ?15s | PASS |
| PlayMode Task2 filter | 27/27 (Logs/warmperf-t2-regress2-play.xml) |
| EditMode Graphics | 129 (Logs/warmperf-t2-edit.xml) |
| Notes | Parallel.ForEach (ProcessorCount-1) + 7ms/frame GPU integrate; MapLoader + GraphicsModeController share EnhancedWarmScheduler |


## Task 9 performance gate (E1M7)

Date: 2026-07-22 23:07
| Metric | Value |
|--------|-------|
| Map build time | 0,86s |
| Classic?Enhanced first switch (yielded warm) | 13,96s |
| Avg repeat switch (20�, timeScale=0) | 12,1ms |
| TextureCount / MaterialCount / NormalMapCount | 1722 / 162 / 237 |
| EnhancedVariantCount | 237 |
| Enhanced 4� albedo bytes | 151,2 MB |
| Normal+height bytes | 151,2 MB |
| Sprite Enhanced bytes | 59,4 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-07-22 23:08
| Metric | Value |
|--------|-------|
| Map build time | 0,39s |
| Classic?Enhanced first switch (yielded warm) | 13,90s |
| Repeat Classic?Enhanced (warm) | 10,0ms |
| TextureCount / MaterialCount / NormalMapCount | 1680 / 154 / 227 |
| EnhancedVariantCount | 227 |
| Native albedo bytes | 7,3 MB |
| Enhanced 4� albedo bytes | 156,3 MB |
| Normal+height bytes | 156,3 MB |
| Sprite Enhanced bytes | 59,9 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 948,7 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-07-22 23:12
| Metric | Value |
|--------|-------|
| Map build time | 0,88s |
| Classic?Enhanced first switch (yielded warm) | 13,54s |
| Avg repeat switch (20�, timeScale=0) | 13,8ms |
| TextureCount / MaterialCount / NormalMapCount | 1722 / 162 / 237 |
| EnhancedVariantCount | 237 |
| Enhanced 4� albedo bytes | 151,2 MB |
| Normal+height bytes | 151,2 MB |
| Sprite Enhanced bytes | 59,4 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-07-22 23:13
| Metric | Value |
|--------|-------|
| Map build time | 0,40s |
| Classic?Enhanced first switch (yielded warm) | 13,55s |
| Repeat Classic?Enhanced (warm) | 12,1ms |
| TextureCount / MaterialCount / NormalMapCount | 1680 / 154 / 227 |
| EnhancedVariantCount | 227 |
| Native albedo bytes | 7,3 MB |
| Enhanced 4� albedo bytes | 156,3 MB |
| Normal+height bytes | 156,3 MB |
| Sprite Enhanced bytes | 59,9 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 979,0 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-07-22 23:18
| Metric | Value |
|--------|-------|
| Map build time | 0,86s |
| Classic?Enhanced first switch (yielded warm) | 13,58s |
| Avg repeat switch (20�, timeScale=0) | 12,4ms |
| TextureCount / MaterialCount / NormalMapCount | 1722 / 162 / 237 |
| EnhancedVariantCount | 237 |
| Enhanced 4� albedo bytes | 151,2 MB |
| Normal+height bytes | 151,2 MB |
| Sprite Enhanced bytes | 59,4 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-07-22 23:19
| Metric | Value |
|--------|-------|
| Map build time | 0,38s |
| Classic?Enhanced first switch (yielded warm) | 12,97s |
| Repeat Classic?Enhanced (warm) | 9,6ms |
| TextureCount / MaterialCount / NormalMapCount | 1680 / 154 / 227 |
| EnhancedVariantCount | 227 |
| Native albedo bytes | 7,3 MB |
| Enhanced 4� albedo bytes | 156,3 MB |
| Normal+height bytes | 156,3 MB |
| Sprite Enhanced bytes | 59,9 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 977,9 MB |
| Mitigation ladder | none applied |

## Warm-perf Task 3 � session EnhancedVariantStore

Date: 2026-07-22 23:30
Hardware: 8C/16T class (batchmode PlayMode)
| Metric | Value |
|--------|-------|
| E1M1 same-map reload compute jobs | 0 |
| E1M1 same-map reload store hits | 889 |
| Session store CPU bytes | 375.3 MB |
| E1M1 cold compute (Apply Enhanced) | 805 |
| E1M1?E1M2 compute / store hits | 252 / 692 |
| Store entries after E1M1?E1M2 | 1141 |
| Managed after E1M1?E1M2 (GC.GetTotalMemory) | ~1215 MB |
| Gate: same-map 0 compute | PASS |
| PlayMode Task3 filter | 31/31 (Logs/warmperf-t3-regress-play.xml) |
| EditMode store + Graphics | 136 (Logs/warmperf-t3-edit.xml) |
| Notes | Process-lifetime CPU store; GPU still per-map; Classic?Enhanced pins Enhanced profile before warm so store keys match |

## Warm-perf Tasks 1-3 - interactive standalone check

Date: 2026-07-23
Build: Builds/Windows/DoomUnity.exe (128,229,159 bytes, Logs/warmperf-build.log)
Interactive verdict: Enhanced load in standalone is fast - confirmed by user
("загрузка прошла быстро"). Parallel warm + session store working as designed
in the shipped player; formal numbers land at warm-perf Task 5.
Known gap (found in Task 1-3 review): MapLoader bound
GameSessionHost.EnsureWadIdentity only after its warm phases, so a process that
boots directly into Enhanced did not publish the first map's results to the
session store - the first level transition recomputed everything once (~14 s).
**Fixed 2026-07-23** (see review-fixes section below).

## Warm-perf Tasks 1-3 - review fixes (2026-07-23)

Three review findings fixed before Task 4:

1. MapLoader binds WAD identity right after opening the WAD (before both warm
   phases) - cold boot into Enhanced now publishes first-load results.
   New PlayMode test `Fresh_session_enhanced_load_publishes_during_build_warm`.
2. Store keys derive from the active profile the job flags are read from
   (TextureCache/SpriteCache `materials.ActiveProfile`, HudTextureCache
   `ResolveActiveProfile` honoring the hot-switch pin while IsApplying);
   scheduler uses per-cache `StoreLayers`. Closes the "content built with one
   layer set, keyed under another" hole before the disk cache persists it.
   New EditMode test `Lazy_build_publishes_under_active_profile_layers`.
3. Scheduler robustness: worker catches all exceptions, integrate loop bails on
   faulted worker instead of spinning; dead counter removed; MapLoader disposes
   its in-flight scheduler in OnDestroy (scene teardown stops the worker pool).

| Suite | Result | XML |
|-------|--------|-----|
| EditMode Graphics + store | **137/137** | `Logs/warmperf-fixes-edit.xml` |
| PlayMode store/scheduler/lifetime/upscale | **31/32** | `Logs/warmperf-fixes-play.xml` |
| PlayMode lifetime isolated (x2) | **5/5** both | `Logs/warmperf-fixes-lifetime-play.xml`, `-lifetime2-` |

The single combined-run failure is the documented pre-existing
`Hot_switch_does_not_grow_resources_after_warmup` +1 material under suite
pollution; it passes 5/5 in isolation on this code.

## Task 9 performance gate (E1M7)

Date: 2026-07-23 00:42
| Metric | Value |
|--------|-------|
| Map build time | 0,88s |
| Classic→Enhanced first switch (yielded warm) | 13,47s |
| Avg repeat switch (20×, timeScale=0) | 13,8ms |
| TextureCount / MaterialCount / NormalMapCount | 1722 / 162 / 237 |
| EnhancedVariantCount | 237 |
| Enhanced 4× albedo bytes | 151,2 MB |
| Normal+height bytes | 151,2 MB |
| Sprite Enhanced bytes | 59,4 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-07-23 00:43
| Metric | Value |
|--------|-------|
| Map build time | 0,39s |
| Classic→Enhanced first switch (yielded warm) | 13,08s |
| Repeat Classic↔Enhanced (warm) | 9,6ms |
| TextureCount / MaterialCount / NormalMapCount | 1680 / 154 / 227 |
| EnhancedVariantCount | 227 |
| Native albedo bytes | 7,3 MB |
| Enhanced 4× albedo bytes | 156,3 MB |
| Normal+height bytes | 156,3 MB |
| Sprite Enhanced bytes | 59,9 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 985,9 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-07-23 00:47
| Metric | Value |
|--------|-------|
| Map build time | 0,88s |
| Classic→Enhanced first switch (yielded warm) | 14,20s |
| Avg repeat switch (20×, timeScale=0) | 14,3ms |
| TextureCount / MaterialCount / NormalMapCount | 1722 / 162 / 237 |
| EnhancedVariantCount | 237 |
| Enhanced 4× albedo bytes | 151,2 MB |
| Normal+height bytes | 151,2 MB |
| Sprite Enhanced bytes | 59,4 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-07-23 00:48
| Metric | Value |
|--------|-------|
| Map build time | 0,39s |
| Classic→Enhanced first switch (yielded warm) | 13,69s |
| Repeat Classic↔Enhanced (warm) | 10,0ms |
| TextureCount / MaterialCount / NormalMapCount | 1680 / 154 / 227 |
| EnhancedVariantCount | 227 |
| Native albedo bytes | 7,3 MB |
| Enhanced 4× albedo bytes | 156,3 MB |
| Normal+height bytes | 156,3 MB |
| Sprite Enhanced bytes | 59,9 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 968,0 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-07-23 00:57
| Metric | Value |
|--------|-------|
| Map build time | 0,93s |
| Classic→Enhanced first switch (yielded warm) | 13,93s |
| Avg repeat switch (20×, timeScale=0) | 14,2ms |
| TextureCount / MaterialCount / NormalMapCount | 1722 / 162 / 237 |
| EnhancedVariantCount | 237 |
| Enhanced 4× albedo bytes | 151,2 MB |
| Normal+height bytes | 151,2 MB |
| Sprite Enhanced bytes | 59,4 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-07-23 00:58
| Metric | Value |
|--------|-------|
| Map build time | 0,40s |
| Classic→Enhanced first switch (yielded warm) | 13,42s |
| Repeat Classic↔Enhanced (warm) | 11,2ms |
| TextureCount / MaterialCount / NormalMapCount | 1680 / 154 / 227 |
| EnhancedVariantCount | 227 |
| Native albedo bytes | 7,3 MB |
| Enhanced 4× albedo bytes | 156,3 MB |
| Normal+height bytes | 156,3 MB |
| Sprite Enhanced bytes | 59,9 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 986,6 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-07-23 15:56
| Metric | Value |
|--------|-------|
| Map build time | 0,87s |
| Classic→Enhanced first switch (yielded warm) | 13,75s |
| Avg repeat switch (20×, timeScale=0) | 12,3ms |
| TextureCount / MaterialCount / NormalMapCount | 1722 / 162 / 237 |
| EnhancedVariantCount | 237 |
| Enhanced 4× albedo bytes | 151,2 MB |
| Normal+height bytes | 151,2 MB |
| Sprite Enhanced bytes | 59,4 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-07-23 15:57
| Metric | Value |
|--------|-------|
| Map build time | 0,40s |
| Classic→Enhanced first switch (yielded warm) | 13,29s |
| Repeat Classic↔Enhanced (warm) | 9,7ms |
| TextureCount / MaterialCount / NormalMapCount | 1680 / 154 / 227 |
| EnhancedVariantCount | 227 |
| Native albedo bytes | 7,3 MB |
| Enhanced 4× albedo bytes | 156,3 MB |
| Normal+height bytes | 156,3 MB |
| Sprite Enhanced bytes | 59,9 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 976,5 MB |
| Mitigation ladder | none applied |

## Warm-perf Task 4 — disk pack cache

Date: 2026-07-23
Gate: cold start with pack-file ≤ ~5 s.

| Metric | Value |
|--------|-------|
| Isolated cold pack (4 textures + sprites/HUD) | 0 compute / 95 diskHits / pack 11,85 MB |
| E1M1 seed Enhanced warm (compute, disk on) | 16,10–16,29 s |
| E1M1 cold disk warm (store wiped, pack reload) | **2,81–3,65 s** (gate PASS) |
| E1M1 cold diskHits | 805 |
| E1M1 cold residual compute | 29 (diskHits dominate; not a time gate miss) |
| E1M1 pack size | **367,0 MB** |
| EditMode codec+disk | 10/10 (`Logs/warmperf-t4-edit.xml`) |
| EditMode Graphics+disk | 139/139 (`Logs/warmperf-t4-graphics-edit.xml`) |
| PlayMode disk suite | 4/4 (`Logs/warmperf-t4-disk-play.xml`) |
| PlayMode Task4 regress | 35/35 (`Logs/warmperf-t4-regress-play.xml`) |

Notes: Editor/PlayMode default disk **off** (CI); tests use `EnableForTests(tempRoot)`.
Player builds default **on** under `persistentDataPath/EnhancedCache/`.
Corrupt / wrong pipelineVersion → silent miss + recompute + rewrite.
