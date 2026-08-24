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

## Task 9 performance gate (E1M7)

Date: 2026-07-23 17:14
| Metric | Value |
|--------|-------|
| Map build time | 0,88s |
| Classic→Enhanced first switch (yielded warm) | 13,55s |
| Avg repeat switch (20×, timeScale=0) | 13,8ms |
| TextureCount / MaterialCount / NormalMapCount | 1722 / 162 / 237 |
| EnhancedVariantCount | 237 |
| Enhanced 4× albedo bytes | 151,2 MB |
| Normal+height bytes | 151,2 MB |
| Sprite Enhanced bytes | 59,4 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-07-23 17:15
| Metric | Value |
|--------|-------|
| Map build time | 0,40s |
| Classic→Enhanced first switch (yielded warm) | 13,25s |
| Repeat Classic↔Enhanced (warm) | 10,7ms |
| TextureCount / MaterialCount / NormalMapCount | 1680 / 154 / 227 |
| EnhancedVariantCount | 227 |
| Native albedo bytes | 7,3 MB |
| Enhanced 4× albedo bytes | 156,3 MB |
| Normal+height bytes | 156,3 MB |
| Sprite Enhanced bytes | 59,9 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 971,7 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-07-23 17:23
| Metric | Value |
|--------|-------|
| Map build time | 0,88s |
| Classic→Enhanced first switch (yielded warm) | 14,67s |
| Avg repeat switch (20×, timeScale=0) | 14,0ms |
| TextureCount / MaterialCount / NormalMapCount | 1722 / 162 / 237 |
| EnhancedVariantCount | 237 |
| Enhanced 4× albedo bytes | 151,2 MB |
| Normal+height bytes | 151,2 MB |
| Sprite Enhanced bytes | 59,4 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-07-23 17:23
| Metric | Value |
|--------|-------|
| Map build time | 0,41s |
| Classic→Enhanced first switch (yielded warm) | 13,72s |
| Repeat Classic↔Enhanced (warm) | 11,4ms |
| TextureCount / MaterialCount / NormalMapCount | 1680 / 154 / 227 |
| EnhancedVariantCount | 227 |
| Native albedo bytes | 7,3 MB |
| Enhanced 4× albedo bytes | 156,3 MB |
| Normal+height bytes | 156,3 MB |
| Sprite Enhanced bytes | 59,9 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 996,0 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-07-23 17:31
| Metric | Value |
|--------|-------|
| Map build time | 0,84s |
| Classic→Enhanced first switch (yielded warm) | 13,46s |
| Avg repeat switch (20×, timeScale=0) | 12,0ms |
| TextureCount / MaterialCount / NormalMapCount | 1722 / 162 / 237 |
| EnhancedVariantCount | 237 |
| Enhanced 4× albedo bytes | 151,2 MB |
| Normal+height bytes | 151,2 MB |
| Sprite Enhanced bytes | 59,4 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-07-23 17:32
| Metric | Value |
|--------|-------|
| Map build time | 0,40s |
| Classic→Enhanced first switch (yielded warm) | 13,27s |
| Repeat Classic↔Enhanced (warm) | 10,0ms |
| TextureCount / MaterialCount / NormalMapCount | 1680 / 154 / 227 |
| EnhancedVariantCount | 227 |
| Native albedo bytes | 7,3 MB |
| Enhanced 4× albedo bytes | 156,3 MB |
| Normal+height bytes | 156,3 MB |
| Sprite Enhanced bytes | 59,9 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 965,5 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-07-23 17:58
| Metric | Value |
|--------|-------|
| Map build time | 0,93s |
| Classic→Enhanced first switch (yielded warm) | 13,12s |
| Avg repeat switch (20×, timeScale=0) | 15,7ms |
| TextureCount / MaterialCount / NormalMapCount | 1722 / 162 / 237 |
| EnhancedVariantCount | 237 |
| Enhanced 4× albedo bytes | 151,2 MB |
| Normal+height bytes | 151,2 MB |
| Sprite Enhanced bytes | 59,4 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-07-23 17:59
| Metric | Value |
|--------|-------|
| Map build time | 0,41s |
| Classic→Enhanced first switch (yielded warm) | 12,64s |
| Repeat Classic↔Enhanced (warm) | 10,5ms |
| TextureCount / MaterialCount / NormalMapCount | 1680 / 154 / 227 |
| EnhancedVariantCount | 227 |
| Native albedo bytes | 7,3 MB |
| Enhanced 4× albedo bytes | 156,3 MB |
| Normal+height bytes | 156,3 MB |
| Sprite Enhanced bytes | 59,9 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 1075,5 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-07-23 22:56
| Metric | Value |
|--------|-------|
| Map build time | 0,95s |
| Classic→Enhanced first switch (yielded warm) | 14,44s |
| Avg repeat switch (20×, timeScale=0) | 13,4ms |
| TextureCount / MaterialCount / NormalMapCount | 1722 / 162 / 237 |
| EnhancedVariantCount | 237 |
| Enhanced 4× albedo bytes | 151,2 MB |
| Normal+height bytes | 151,2 MB |
| Sprite Enhanced bytes | 59,4 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-07-23 22:57
| Metric | Value |
|--------|-------|
| Map build time | 0,42s |
| Classic→Enhanced first switch (yielded warm) | 13,81s |
| Repeat Classic↔Enhanced (warm) | 11,4ms |
| TextureCount / MaterialCount / NormalMapCount | 1680 / 154 / 227 |
| EnhancedVariantCount | 227 |
| Native albedo bytes | 7,3 MB |
| Enhanced 4× albedo bytes | 156,3 MB |
| Normal+height bytes | 156,3 MB |
| Sprite Enhanced bytes | 59,9 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 1038,3 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-07-23 23:13
| Metric | Value |
|--------|-------|
| Map build time | 0,94s |
| Classic→Enhanced first switch (yielded warm) | 14,34s |
| Avg repeat switch (20×, timeScale=0) | 13,9ms |
| TextureCount / MaterialCount / NormalMapCount | 1722 / 162 / 237 |
| EnhancedVariantCount | 237 |
| Enhanced 4× albedo bytes | 151,2 MB |
| Normal+height bytes | 151,2 MB |
| Sprite Enhanced bytes | 59,4 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-07-23 23:13
| Metric | Value |
|--------|-------|
| Map build time | 0,42s |
| Classic→Enhanced first switch (yielded warm) | 13,94s |
| Repeat Classic↔Enhanced (warm) | 11,2ms |
| TextureCount / MaterialCount / NormalMapCount | 1680 / 154 / 227 |
| EnhancedVariantCount | 227 |
| Native albedo bytes | 7,3 MB |
| Enhanced 4× albedo bytes | 156,3 MB |
| Normal+height bytes | 156,3 MB |
| Sprite Enhanced bytes | 59,9 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 1011,1 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-07-23 23:19
| Metric | Value |
|--------|-------|
| Map build time | 0,89s |
| Classic→Enhanced first switch (yielded warm) | 14,35s |
| Avg repeat switch (20×, timeScale=0) | 15,1ms |
| TextureCount / MaterialCount / NormalMapCount | 1722 / 162 / 237 |
| EnhancedVariantCount | 237 |
| Enhanced 4× albedo bytes | 151,2 MB |
| Normal+height bytes | 151,2 MB |
| Sprite Enhanced bytes | 59,4 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-07-23 23:20
| Metric | Value |
|--------|-------|
| Map build time | 0,40s |
| Classic→Enhanced first switch (yielded warm) | 14,00s |
| Repeat Classic↔Enhanced (warm) | 10,6ms |
| TextureCount / MaterialCount / NormalMapCount | 1680 / 154 / 227 |
| EnhancedVariantCount | 227 |
| Native albedo bytes | 7,3 MB |
| Enhanced 4× albedo bytes | 156,3 MB |
| Normal+height bytes | 156,3 MB |
| Sprite Enhanced bytes | 59,9 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 1041,7 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-07-23 23:30
| Metric | Value |
|--------|-------|
| Map build time | 0,84s |
| Classic→Enhanced first switch (yielded warm) | 14,36s |
| Avg repeat switch (20×, timeScale=0) | 12,8ms |
| TextureCount / MaterialCount / NormalMapCount | 1722 / 162 / 237 |
| EnhancedVariantCount | 237 |
| Enhanced 4× albedo bytes | 151,2 MB |
| Normal+height bytes | 151,2 MB |
| Sprite Enhanced bytes | 59,4 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-07-23 23:31
| Metric | Value |
|--------|-------|
| Map build time | 0,39s |
| Classic→Enhanced first switch (yielded warm) | 14,47s |
| Repeat Classic↔Enhanced (warm) | 10,3ms |
| TextureCount / MaterialCount / NormalMapCount | 1680 / 154 / 227 |
| EnhancedVariantCount | 227 |
| Native albedo bytes | 7,3 MB |
| Enhanced 4× albedo bytes | 156,3 MB |
| Normal+height bytes | 156,3 MB |
| Sprite Enhanced bytes | 59,9 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 969,7 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-07-24 00:27
| Metric | Value |
|--------|-------|
| Map build time | 0,93s |
| Classic→Enhanced first switch (yielded warm) | 14,58s |
| Avg repeat switch (20×, timeScale=0) | 14,8ms |
| TextureCount / MaterialCount / NormalMapCount | 1722 / 162 / 237 |
| EnhancedVariantCount | 237 |
| Enhanced 4× albedo bytes | 151,2 MB |
| Normal+height bytes | 151,2 MB |
| Sprite Enhanced bytes | 59,4 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-07-24 00:28
| Metric | Value |
|--------|-------|
| Map build time | 0,41s |
| Classic→Enhanced first switch (yielded warm) | 14,04s |
| Repeat Classic↔Enhanced (warm) | 10,4ms |
| TextureCount / MaterialCount / NormalMapCount | 1680 / 154 / 227 |
| EnhancedVariantCount | 227 |
| Native albedo bytes | 7,3 MB |
| Enhanced 4× albedo bytes | 156,3 MB |
| Normal+height bytes | 156,3 MB |
| Sprite Enhanced bytes | 59,9 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 1043,2 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-07-24 00:44
| Metric | Value |
|--------|-------|
| Map build time | 0,95s |
| Classic→Enhanced first switch (yielded warm) | 15,34s |
| Avg repeat switch (20×, timeScale=0) | 14,3ms |
| TextureCount / MaterialCount / NormalMapCount | 1722 / 162 / 237 |
| EnhancedVariantCount | 237 |
| Enhanced 4× albedo bytes | 151,2 MB |
| Normal+height bytes | 151,2 MB |
| Sprite Enhanced bytes | 59,4 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-07-24 00:45
| Metric | Value |
|--------|-------|
| Map build time | 0,42s |
| Classic→Enhanced first switch (yielded warm) | 14,25s |
| Repeat Classic↔Enhanced (warm) | 10,4ms |
| TextureCount / MaterialCount / NormalMapCount | 1680 / 154 / 227 |
| EnhancedVariantCount | 227 |
| Native albedo bytes | 7,3 MB |
| Enhanced 4× albedo bytes | 156,3 MB |
| Normal+height bytes | 156,3 MB |
| Sprite Enhanced bytes | 59,9 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 1028,3 MB |
| Mitigation ladder | none applied |

## Task 10 close — visual gate SUCCESS (2026-07-24)

**Verdict: SUCCESS.** User interactive sign-off: graphics improved; iteration closed.

### Automation evidence (reused from warm-perf Task 5, 2026-07-24)

| Suite | Result | Notes |
|-------|--------|-------|
| Full EditMode | **601/601** | First full green after Task 1 contracts growth |
| Full PlayMode | **144/144** | First full PlayMode green since Task 1 baseline 103/111 |
| Windows standalone | OK ~128 MB | Missing shaders / editor-only deps not observed |
| E1 smoke Classic+Enhanced | covered by `E1MapSmokePlayTests` in full PlayMode | |

### Warm performance (accepted for sign-off)

| Path | E1M1 |
|------|------|
| Compute warm (parallel scheduler) | ~14 s (Task 2 gate ≤15 s PASS) |
| Cold disk pack warm | ~2.8–3.7 s (Task 4 gate ≤~5 s PASS), pack ~367 MB |
| Same-map reload / level transition store | 0 compute when session hit |

### Layer contribution summary

| Layer | Verdict on Freedoom E1 |
|-------|------------------------|
| Dedither (pattern-gate) | ≈ no-op (0.00–0.02% px); keep for retail DOOM.WAD |
| Super-xBR 4× world | Primary visible win vs Scale2x reject |
| Sprites / weapon / HUD 4× + Sharpen 0.5 | Removes mixed-resolution feel after world upgrade |
| Texel-AA | Accepted part of Enhanced close-up sampling |
| Normals + POM (solid opaque) | Accepted depth layer; no reject on seams/edges |
| Warm-perf stack | Required for usable Enhanced loads |

### Limits carried forward

- Menus / intermission / title stay native
- Texel-AA + POM are world-material concerns (POM solid opaque only)
- Neural upscale (ESRGAN/Sentis) still deferred
- Mitigation ladder unused (memory acceptable on Arc A750 class)

Iteration closed. Next roadmap item remains Stage 9 E1 compatibility polish (separate).

## Task 9 performance gate (E1M7)

Date: 2026-08-03 00:08
| Metric | Value |
|--------|-------|
| Map build time | 0,93s |
| Classic→Enhanced first switch (yielded warm) | 10,39s |
| Avg repeat switch (20×, timeScale=0) | 9,9ms |
| TextureCount / MaterialCount / NormalMapCount | 1722 / 162 / 237 |
| EnhancedVariantCount | 237 |
| Enhanced 4× albedo bytes | 151,2 MB |
| Normal+height bytes | 151,2 MB |
| Sprite Enhanced bytes | 213,2 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-03 00:09
| Metric | Value |
|--------|-------|
| Map build time | 0,51s |
| Classic→Enhanced first switch (yielded warm) | 9,78s |
| Repeat Classic↔Enhanced (warm) | 9,4ms |
| TextureCount / MaterialCount / NormalMapCount | 1680 / 154 / 227 |
| EnhancedVariantCount | 227 |
| Native albedo bytes | 7,3 MB |
| Enhanced 4× albedo bytes | 156,3 MB |
| Normal+height bytes | 156,3 MB |
| Sprite Enhanced bytes | 213,8 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 1100,0 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-03 00:22
| Metric | Value |
|--------|-------|
| Map build time | 0,80s |
| Classic→Enhanced first switch (yielded warm) | 9,90s |
| Avg repeat switch (20×, timeScale=0) | 8,9ms |
| TextureCount / MaterialCount / NormalMapCount | 1722 / 162 / 237 |
| EnhancedVariantCount | 237 |
| Enhanced 4× albedo bytes | 151,2 MB |
| Normal+height bytes | 151,2 MB |
| Sprite Enhanced bytes | 213,2 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-03 00:22
| Metric | Value |
|--------|-------|
| Map build time | 0,35s |
| Classic→Enhanced first switch (yielded warm) | 9,25s |
| Repeat Classic↔Enhanced (warm) | 8,8ms |
| TextureCount / MaterialCount / NormalMapCount | 1680 / 154 / 227 |
| EnhancedVariantCount | 227 |
| Native albedo bytes | 7,3 MB |
| Enhanced 4× albedo bytes | 156,3 MB |
| Normal+height bytes | 156,3 MB |
| Sprite Enhanced bytes | 213,8 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 1121,6 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-03 19:25
| Metric | Value |
|--------|-------|
| Map build time | 0,80s |
| Classic→Enhanced first switch (yielded warm) | 10,36s |
| Avg repeat switch (20×, timeScale=0) | 14,2ms |
| TextureCount / MaterialCount / NormalMapCount | 1722 / 162 / 237 |
| EnhancedVariantCount | 237 |
| Enhanced 4× albedo bytes | 151,2 MB |
| Normal+height bytes | 151,2 MB |
| Sprite Enhanced bytes | 213,2 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-03 19:25
| Metric | Value |
|--------|-------|
| Map build time | 0,38s |
| Classic→Enhanced first switch (yielded warm) | 9,50s |
| Repeat Classic↔Enhanced (warm) | 8,9ms |
| TextureCount / MaterialCount / NormalMapCount | 1680 / 154 / 227 |
| EnhancedVariantCount | 227 |
| Native albedo bytes | 7,3 MB |
| Enhanced 4× albedo bytes | 156,3 MB |
| Normal+height bytes | 156,3 MB |
| Sprite Enhanced bytes | 213,8 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 1116,5 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-03 19:32
| Metric | Value |
|--------|-------|
| Map build time | 0,76s |
| Classic→Enhanced first switch (yielded warm) | 9,97s |
| Avg repeat switch (20×, timeScale=0) | 8,9ms |
| TextureCount / MaterialCount / NormalMapCount | 1722 / 162 / 237 |
| EnhancedVariantCount | 237 |
| Enhanced 4× albedo bytes | 151,2 MB |
| Normal+height bytes | 151,2 MB |
| Sprite Enhanced bytes | 213,2 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-03 19:32
| Metric | Value |
|--------|-------|
| Map build time | 0,36s |
| Classic→Enhanced first switch (yielded warm) | 9,47s |
| Repeat Classic↔Enhanced (warm) | 21,3ms |
| TextureCount / MaterialCount / NormalMapCount | 1680 / 154 / 227 |
| EnhancedVariantCount | 227 |
| Native albedo bytes | 7,3 MB |
| Enhanced 4× albedo bytes | 156,3 MB |
| Normal+height bytes | 156,3 MB |
| Sprite Enhanced bytes | 213,8 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 1039,5 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-03 19:33
| Metric | Value |
|--------|-------|
| Map build time | 0,76s |
| Classic→Enhanced first switch (yielded warm) | 9,86s |
| Avg repeat switch (20×, timeScale=0) | 9,6ms |
| TextureCount / MaterialCount / NormalMapCount | 1722 / 162 / 237 |
| EnhancedVariantCount | 237 |
| Enhanced 4× albedo bytes | 151,2 MB |
| Normal+height bytes | 151,2 MB |
| Sprite Enhanced bytes | 213,2 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-03 19:34
| Metric | Value |
|--------|-------|
| Map build time | 0,35s |
| Classic→Enhanced first switch (yielded warm) | 9,50s |
| Repeat Classic↔Enhanced (warm) | 7,6ms |
| TextureCount / MaterialCount / NormalMapCount | 1680 / 154 / 227 |
| EnhancedVariantCount | 227 |
| Native albedo bytes | 7,3 MB |
| Enhanced 4× albedo bytes | 156,3 MB |
| Normal+height bytes | 156,3 MB |
| Sprite Enhanced bytes | 213,8 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 1125,3 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-03 19:37
| Metric | Value |
|--------|-------|
| Map build time | 0,38s |
| Classic→Enhanced first switch (yielded warm) | 9,59s |
| Repeat Classic↔Enhanced (warm) | 8,7ms |
| TextureCount / MaterialCount / NormalMapCount | 1680 / 154 / 227 |
| EnhancedVariantCount | 227 |
| Native albedo bytes | 7,3 MB |
| Enhanced 4× albedo bytes | 156,3 MB |
| Normal+height bytes | 156,3 MB |
| Sprite Enhanced bytes | 213,8 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 1127,7 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-03 20:09
| Metric | Value |
|--------|-------|
| Map build time | 0,80s |
| Classic→Enhanced first switch (yielded warm) | 10,19s |
| Avg repeat switch (20×, timeScale=0) | 10,1ms |
| TextureCount / MaterialCount / NormalMapCount | 1722 / 162 / 237 |
| EnhancedVariantCount | 237 |
| Enhanced 4× albedo bytes | 151,2 MB |
| Normal+height bytes | 151,2 MB |
| Sprite Enhanced bytes | 213,2 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-03 20:10
| Metric | Value |
|--------|-------|
| Map build time | 0,36s |
| Classic→Enhanced first switch (yielded warm) | 9,38s |
| Repeat Classic↔Enhanced (warm) | 8,0ms |
| TextureCount / MaterialCount / NormalMapCount | 1680 / 154 / 227 |
| EnhancedVariantCount | 227 |
| Native albedo bytes | 7,3 MB |
| Enhanced 4× albedo bytes | 156,3 MB |
| Normal+height bytes | 156,3 MB |
| Sprite Enhanced bytes | 213,8 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 1116,9 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-07 02:08
| Metric | Value |
|--------|-------|
| Map build time | 0,82s |
| Classic→Enhanced first switch (yielded warm) | 10,20s |
| Avg repeat switch (20×, timeScale=0) | 11,7ms |
| TextureCount / MaterialCount / NormalMapCount | 1731 / 330 / 240 |
| EnhancedVariantCount | 240 |
| Enhanced 4× albedo bytes | 154,2 MB |
| Normal+height bytes | 154,2 MB |
| Sprite Enhanced bytes | 213,2 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-07 02:09
| Metric | Value |
|--------|-------|
| Map build time | 0,40s |
| Classic→Enhanced first switch (yielded warm) | 11,13s |
| Repeat Classic↔Enhanced (warm) | 14,8ms |
| TextureCount / MaterialCount / NormalMapCount | 1692 / 316 / 231 |
| EnhancedVariantCount | 231 |
| Native albedo bytes | 7,4 MB |
| Enhanced 4× albedo bytes | 158,4 MB |
| Normal+height bytes | 158,4 MB |
| Sprite Enhanced bytes | 213,8 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 1130,2 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-07 23:23
| Metric | Value |
|--------|-------|
| Map build time | 0,84s |
| Classic→Enhanced first switch (yielded warm) | 10,06s |
| Avg repeat switch (20×, timeScale=0) | 13,2ms |
| TextureCount / MaterialCount / NormalMapCount | 1731 / 330 / 240 |
| EnhancedVariantCount | 240 |
| Enhanced 4× albedo bytes | 154,2 MB |
| Normal+height bytes | 154,2 MB |
| Sprite Enhanced bytes | 213,2 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-07 23:23
| Metric | Value |
|--------|-------|
| Map build time | 0,43s |
| Classic→Enhanced first switch (yielded warm) | 9,98s |
| Repeat Classic↔Enhanced (warm) | 11,8ms |
| TextureCount / MaterialCount / NormalMapCount | 1692 / 316 / 231 |
| EnhancedVariantCount | 231 |
| Native albedo bytes | 7,4 MB |
| Enhanced 4× albedo bytes | 158,4 MB |
| Normal+height bytes | 158,4 MB |
| Sprite Enhanced bytes | 213,8 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 1152,9 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-08 00:33
| Metric | Value |
|--------|-------|
| Map build time | 0,84s |
| Classic→Enhanced first switch (yielded warm) | 9,19s |
| Avg repeat switch (20×, timeScale=0) | 11,3ms |
| TextureCount / MaterialCount / NormalMapCount | 1434 / 330 / 240 |
| EnhancedVariantCount | 240 |
| Enhanced 4× albedo bytes | 154,2 MB |
| Normal+height bytes | 154,2 MB |
| Sprite Enhanced bytes | 8,1 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-08 00:34
| Metric | Value |
|--------|-------|
| Map build time | 0,41s |
| Classic→Enhanced first switch (yielded warm) | 8,96s |
| Repeat Classic↔Enhanced (warm) | 11,3ms |
| TextureCount / MaterialCount / NormalMapCount | 1393 / 316 / 231 |
| EnhancedVariantCount | 231 |
| Native albedo bytes | 7,4 MB |
| Enhanced 4× albedo bytes | 158,4 MB |
| Normal+height bytes | 158,4 MB |
| Sprite Enhanced bytes | 8,5 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 933,1 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-08 00:42
| Metric | Value |
|--------|-------|
| Map build time | 0,85s |
| Classic→Enhanced first switch (yielded warm) | 9,16s |
| Avg repeat switch (20×, timeScale=0) | 11,9ms |
| TextureCount / MaterialCount / NormalMapCount | 1434 / 330 / 240 |
| EnhancedVariantCount | 240 |
| Enhanced 4× albedo bytes | 154,2 MB |
| Normal+height bytes | 154,2 MB |
| Sprite Enhanced bytes | 8,1 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-08 01:30
| Metric | Value |
|--------|-------|
| Map build time | 0,83s |
| Classic→Enhanced first switch (yielded warm) | 9,13s |
| Avg repeat switch (20×, timeScale=0) | 10,9ms |
| TextureCount / MaterialCount / NormalMapCount | 1434 / 330 / 240 |
| EnhancedVariantCount | 240 |
| Enhanced 4× albedo bytes | 154,2 MB |
| Normal+height bytes | 154,2 MB |
| Sprite Enhanced bytes | 8,1 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-08 01:31
| Metric | Value |
|--------|-------|
| Map build time | 0,42s |
| Classic→Enhanced first switch (yielded warm) | 8,92s |
| Repeat Classic↔Enhanced (warm) | 10,3ms |
| TextureCount / MaterialCount / NormalMapCount | 1393 / 316 / 231 |
| EnhancedVariantCount | 231 |
| Native albedo bytes | 7,4 MB |
| Enhanced 4× albedo bytes | 158,4 MB |
| Normal+height bytes | 158,4 MB |
| Sprite Enhanced bytes | 8,5 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 931,5 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-08 13:39
| Metric | Value |
|--------|-------|
| Map build time | 0,87s |
| Classic→Enhanced first switch (yielded warm) | 9,31s |
| Avg repeat switch (20×, timeScale=0) | 12,2ms |
| TextureCount / MaterialCount / NormalMapCount | 1434 / 330 / 240 |
| EnhancedVariantCount | 240 |
| Enhanced 4× albedo bytes | 154,2 MB |
| Normal+height bytes | 154,2 MB |
| Sprite Enhanced bytes | 8,1 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-08 13:39
| Metric | Value |
|--------|-------|
| Map build time | 0,44s |
| Classic→Enhanced first switch (yielded warm) | 9,22s |
| Repeat Classic↔Enhanced (warm) | 13,1ms |
| TextureCount / MaterialCount / NormalMapCount | 1393 / 316 / 231 |
| EnhancedVariantCount | 231 |
| Native albedo bytes | 7,4 MB |
| Enhanced 4× albedo bytes | 158,4 MB |
| Normal+height bytes | 158,4 MB |
| Sprite Enhanced bytes | 8,5 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 932,4 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-08 14:10
| Metric | Value |
|--------|-------|
| Map build time | 0,89s |
| Classic→Enhanced first switch (yielded warm) | 9,45s |
| Avg repeat switch (20×, timeScale=0) | 14,5ms |
| TextureCount / MaterialCount / NormalMapCount | 1434 / 330 / 240 |
| EnhancedVariantCount | 240 |
| Enhanced 4× albedo bytes | 154,2 MB |
| Normal+height bytes | 154,2 MB |
| Sprite Enhanced bytes | 8,1 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-08 14:10
| Metric | Value |
|--------|-------|
| Map build time | 0,43s |
| Classic→Enhanced first switch (yielded warm) | 9,07s |
| Repeat Classic↔Enhanced (warm) | 12,5ms |
| TextureCount / MaterialCount / NormalMapCount | 1393 / 316 / 231 |
| EnhancedVariantCount | 231 |
| Native albedo bytes | 7,4 MB |
| Enhanced 4× albedo bytes | 158,4 MB |
| Normal+height bytes | 158,4 MB |
| Sprite Enhanced bytes | 8,5 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 926,0 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-08 14:34
| Metric | Value |
|--------|-------|
| Map build time | 0,87s |
| Classic→Enhanced first switch (yielded warm) | 9,33s |
| Avg repeat switch (20×, timeScale=0) | 11,5ms |
| TextureCount / MaterialCount / NormalMapCount | 1432 / 330 / 240 |
| EnhancedVariantCount | 240 |
| Enhanced 4× albedo bytes | 154,2 MB |
| Normal+height bytes | 154,2 MB |
| Sprite Enhanced bytes | 7,8 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-08 14:34
| Metric | Value |
|--------|-------|
| Map build time | 0,42s |
| Classic→Enhanced first switch (yielded warm) | 8,87s |
| Repeat Classic↔Enhanced (warm) | 11,4ms |
| TextureCount / MaterialCount / NormalMapCount | 1389 / 316 / 231 |
| EnhancedVariantCount | 231 |
| Native albedo bytes | 7,4 MB |
| Enhanced 4× albedo bytes | 158,4 MB |
| Normal+height bytes | 158,4 MB |
| Sprite Enhanced bytes | 7,0 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 941,5 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-08 14:51
| Metric | Value |
|--------|-------|
| Map build time | 0,84s |
| Classic→Enhanced first switch (yielded warm) | 9,18s |
| Avg repeat switch (20×, timeScale=0) | 14,5ms |
| TextureCount / MaterialCount / NormalMapCount | 1432 / 330 / 240 |
| EnhancedVariantCount | 240 |
| Enhanced 4× albedo bytes | 154,2 MB |
| Normal+height bytes | 154,2 MB |
| Sprite Enhanced bytes | 7,8 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-08 14:52
| Metric | Value |
|--------|-------|
| Map build time | 0,43s |
| Classic→Enhanced first switch (yielded warm) | 9,27s |
| Repeat Classic↔Enhanced (warm) | 11,6ms |
| TextureCount / MaterialCount / NormalMapCount | 1389 / 316 / 231 |
| EnhancedVariantCount | 231 |
| Native albedo bytes | 7,4 MB |
| Enhanced 4× albedo bytes | 158,4 MB |
| Normal+height bytes | 158,4 MB |
| Sprite Enhanced bytes | 7,0 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 950,3 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-08 15:02
| Metric | Value |
|--------|-------|
| Map build time | 0,85s |
| Classic→Enhanced first switch (yielded warm) | 9,22s |
| Avg repeat switch (20×, timeScale=0) | 12,1ms |
| TextureCount / MaterialCount / NormalMapCount | 1432 / 330 / 240 |
| EnhancedVariantCount | 240 |
| Enhanced 4× albedo bytes | 154,2 MB |
| Normal+height bytes | 154,2 MB |
| Sprite Enhanced bytes | 7,8 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-08 15:02
| Metric | Value |
|--------|-------|
| Map build time | 0,43s |
| Classic→Enhanced first switch (yielded warm) | 9,09s |
| Repeat Classic↔Enhanced (warm) | 11,1ms |
| TextureCount / MaterialCount / NormalMapCount | 1389 / 316 / 231 |
| EnhancedVariantCount | 231 |
| Native albedo bytes | 7,4 MB |
| Enhanced 4× albedo bytes | 158,4 MB |
| Normal+height bytes | 158,4 MB |
| Sprite Enhanced bytes | 7,0 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 927,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-10 01:09
| Metric | Value |
|--------|-------|
| Map build time | 0,91s |
| Classic→Enhanced first switch (yielded warm) | 9,22s |
| Avg repeat switch (20×, timeScale=0) | 11,3ms |
| TextureCount / MaterialCount / NormalMapCount | 1432 / 330 / 240 |
| EnhancedVariantCount | 240 |
| Enhanced 4× albedo bytes | 154,2 MB |
| Normal+height bytes | 154,2 MB |
| Sprite Enhanced bytes | 7,8 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-10 01:09
| Metric | Value |
|--------|-------|
| Map build time | 0,48s |
| Classic→Enhanced first switch (yielded warm) | 9,64s |
| Repeat Classic↔Enhanced (warm) | 12,8ms |
| TextureCount / MaterialCount / NormalMapCount | 1389 / 316 / 231 |
| EnhancedVariantCount | 231 |
| Native albedo bytes | 7,4 MB |
| Enhanced 4× albedo bytes | 158,4 MB |
| Normal+height bytes | 158,4 MB |
| Sprite Enhanced bytes | 7,0 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 912,3 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-10 01:30
| Metric | Value |
|--------|-------|
| Map build time | 0,88s |
| Classic→Enhanced first switch (yielded warm) | 8,89s |
| Avg repeat switch (20×, timeScale=0) | 10,7ms |
| TextureCount / MaterialCount / NormalMapCount | 1432 / 330 / 240 |
| EnhancedVariantCount | 240 |
| Enhanced 4× albedo bytes | 154,2 MB |
| Normal+height bytes | 154,2 MB |
| Sprite Enhanced bytes | 7,8 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-10 01:31
| Metric | Value |
|--------|-------|
| Map build time | 0,46s |
| Classic→Enhanced first switch (yielded warm) | 8,78s |
| Repeat Classic↔Enhanced (warm) | 11,5ms |
| TextureCount / MaterialCount / NormalMapCount | 1389 / 316 / 231 |
| EnhancedVariantCount | 231 |
| Native albedo bytes | 7,4 MB |
| Enhanced 4× albedo bytes | 158,4 MB |
| Normal+height bytes | 158,4 MB |
| Sprite Enhanced bytes | 7,0 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 924,1 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-10 02:16
| Metric | Value |
|--------|-------|
| Map build time | 1,02s |
| Classic→Enhanced first switch (yielded warm) | 9,23s |
| Avg repeat switch (20×, timeScale=0) | 13,0ms |
| TextureCount / MaterialCount / NormalMapCount | 1432 / 330 / 240 |
| EnhancedVariantCount | 240 |
| Enhanced 4× albedo bytes | 154,2 MB |
| Normal+height bytes | 154,2 MB |
| Sprite Enhanced bytes | 7,8 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-10 02:17
| Metric | Value |
|--------|-------|
| Map build time | 0,49s |
| Classic→Enhanced first switch (yielded warm) | 9,16s |
| Repeat Classic↔Enhanced (warm) | 11,6ms |
| TextureCount / MaterialCount / NormalMapCount | 1389 / 316 / 231 |
| EnhancedVariantCount | 231 |
| Native albedo bytes | 7,4 MB |
| Enhanced 4× albedo bytes | 158,4 MB |
| Normal+height bytes | 158,4 MB |
| Sprite Enhanced bytes | 7,0 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 922,3 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-13 02:23
| Metric | Value |
|--------|-------|
| Map build time | 0,89s |
| Classic→Enhanced first switch (yielded warm) | 8,74s |
| Avg repeat switch (20×, timeScale=0) | 14,3ms |
| TextureCount / MaterialCount / NormalMapCount | 1432 / 330 / 240 |
| EnhancedVariantCount | 240 |
| Enhanced 4× albedo bytes | 154,2 MB |
| Normal+height bytes | 154,2 MB |
| Sprite Enhanced bytes | 7,8 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-13 02:24
| Metric | Value |
|--------|-------|
| Map build time | 0,43s |
| Classic→Enhanced first switch (yielded warm) | 8,53s |
| Repeat Classic↔Enhanced (warm) | 12,9ms |
| TextureCount / MaterialCount / NormalMapCount | 1389 / 316 / 231 |
| EnhancedVariantCount | 231 |
| Native albedo bytes | 7,4 MB |
| Enhanced 4× albedo bytes | 158,4 MB |
| Normal+height bytes | 158,4 MB |
| Sprite Enhanced bytes | 7,0 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 936,1 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-13 23:54
| Metric | Value |
|--------|-------|
| Map build time | 0,91s |
| Classic→Enhanced first switch (yielded warm) | 8,51s |
| Avg repeat switch (20×, timeScale=0) | 14,1ms |
| TextureCount / MaterialCount / NormalMapCount | 1432 / 330 / 240 |
| EnhancedVariantCount | 240 |
| Enhanced 4× albedo bytes | 154,2 MB |
| Normal+height bytes | 154,2 MB |
| Sprite Enhanced bytes | 7,8 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-13 23:55
| Metric | Value |
|--------|-------|
| Map build time | 0,48s |
| Classic→Enhanced first switch (yielded warm) | 8,72s |
| Repeat Classic↔Enhanced (warm) | 13,2ms |
| TextureCount / MaterialCount / NormalMapCount | 1389 / 316 / 231 |
| EnhancedVariantCount | 231 |
| Native albedo bytes | 7,4 MB |
| Enhanced 4× albedo bytes | 158,4 MB |
| Normal+height bytes | 158,4 MB |
| Sprite Enhanced bytes | 7,0 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 1333,1 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-15 01:05
| Metric | Value |
|--------|-------|
| Map build time | 1,19s |
| Classic→Enhanced first switch (yielded warm) | 8,57s |
| Avg repeat switch (20×, timeScale=0) | 17,0ms |
| TextureCount / MaterialCount / NormalMapCount | 1432 / 330 / 240 |
| EnhancedVariantCount | 240 |
| Enhanced 4× albedo bytes | 154,2 MB |
| Normal+height bytes | 154,2 MB |
| Sprite Enhanced bytes | 7,8 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-15 01:05
| Metric | Value |
|--------|-------|
| Map build time | 0,59s |
| Classic→Enhanced first switch (yielded warm) | 8,80s |
| Repeat Classic↔Enhanced (warm) | 13,0ms |
| TextureCount / MaterialCount / NormalMapCount | 1389 / 316 / 231 |
| EnhancedVariantCount | 231 |
| Native albedo bytes | 7,4 MB |
| Enhanced 4× albedo bytes | 158,4 MB |
| Normal+height bytes | 158,4 MB |
| Sprite Enhanced bytes | 7,0 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 931,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-19 17:53
| Metric | Value |
|--------|-------|
| Map build time | 1,22s |
| Classic→Enhanced first switch (yielded warm) | 8,52s |
| Avg repeat switch (20×, timeScale=0) | 18,3ms |
| TextureCount / MaterialCount / NormalMapCount | 1432 / 330 / 240 |
| EnhancedVariantCount | 240 |
| Enhanced 4× albedo bytes | 154,2 MB |
| Normal+height bytes | 154,2 MB |
| Sprite Enhanced bytes | 7,8 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-19 17:53
| Metric | Value |
|--------|-------|
| Map build time | 0,58s |
| Classic→Enhanced first switch (yielded warm) | 8,36s |
| Repeat Classic↔Enhanced (warm) | 13,0ms |
| TextureCount / MaterialCount / NormalMapCount | 1389 / 316 / 231 |
| EnhancedVariantCount | 231 |
| Native albedo bytes | 7,4 MB |
| Enhanced 4× albedo bytes | 158,4 MB |
| Normal+height bytes | 158,4 MB |
| Sprite Enhanced bytes | 7,0 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 939,1 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-19 18:49
| Metric | Value |
|--------|-------|
| Map build time | 1,25s |
| Classic→Enhanced first switch (yielded warm) | 8,83s |
| Avg repeat switch (20×, timeScale=0) | 15,9ms |
| TextureCount / MaterialCount / NormalMapCount | 1432 / 330 / 240 |
| EnhancedVariantCount | 240 |
| Enhanced 4× albedo bytes | 154,2 MB |
| Normal+height bytes | 154,2 MB |
| Sprite Enhanced bytes | 7,8 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-19 18:50
| Metric | Value |
|--------|-------|
| Map build time | 0,62s |
| Classic→Enhanced first switch (yielded warm) | 8,93s |
| Repeat Classic↔Enhanced (warm) | 11,7ms |
| TextureCount / MaterialCount / NormalMapCount | 1389 / 316 / 231 |
| EnhancedVariantCount | 231 |
| Native albedo bytes | 7,4 MB |
| Enhanced 4× albedo bytes | 158,4 MB |
| Normal+height bytes | 158,4 MB |
| Sprite Enhanced bytes | 7,0 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 919,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-21 02:09
| Metric | Value |
|--------|-------|
| Map build time | 1,29s |
| Classic→Enhanced first switch (yielded warm) | 8,60s |
| Avg repeat switch (20×, timeScale=0) | 17,0ms |
| TextureCount / MaterialCount / NormalMapCount | 1446 / 330 / 240 |
| EnhancedVariantCount | 240 |
| Enhanced 4× albedo bytes | 154,2 MB |
| Normal+height bytes | 154,2 MB |
| Sprite Enhanced bytes | 8,6 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-21 02:10
| Metric | Value |
|--------|-------|
| Map build time | 0,60s |
| Classic→Enhanced first switch (yielded warm) | 8,53s |
| Repeat Classic↔Enhanced (warm) | 12,1ms |
| TextureCount / MaterialCount / NormalMapCount | 1389 / 316 / 231 |
| EnhancedVariantCount | 231 |
| Native albedo bytes | 7,4 MB |
| Enhanced 4× albedo bytes | 158,4 MB |
| Normal+height bytes | 158,4 MB |
| Sprite Enhanced bytes | 7,0 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 933,2 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-21 16:09
| Metric | Value |
|--------|-------|
| Map build time | 1,25s |
| Classic→Enhanced first switch (yielded warm) | 8,44s |
| Avg repeat switch (20×, timeScale=0) | 16,7ms |
| TextureCount / MaterialCount / NormalMapCount | 1446 / 330 / 240 |
| EnhancedVariantCount | 240 |
| Enhanced 4× albedo bytes | 154,2 MB |
| Normal+height bytes | 154,2 MB |
| Sprite Enhanced bytes | 8,6 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-21 16:09
| Metric | Value |
|--------|-------|
| Map build time | 0,59s |
| Classic→Enhanced first switch (yielded warm) | 8,42s |
| Repeat Classic↔Enhanced (warm) | 12,8ms |
| TextureCount / MaterialCount / NormalMapCount | 1389 / 316 / 231 |
| EnhancedVariantCount | 231 |
| Native albedo bytes | 7,4 MB |
| Enhanced 4× albedo bytes | 158,4 MB |
| Normal+height bytes | 158,4 MB |
| Sprite Enhanced bytes | 7,0 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 927,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-21 23:25
| Metric | Value |
|--------|-------|
| Map build time | 1,30s |
| Classic→Enhanced first switch (yielded warm) | 9,25s |
| Avg repeat switch (20×, timeScale=0) | 14,5ms |
| TextureCount / MaterialCount / NormalMapCount | 1446 / 330 / 240 |
| EnhancedVariantCount | 240 |
| Enhanced 4× albedo bytes | 154,2 MB |
| Normal+height bytes | 154,2 MB |
| Sprite Enhanced bytes | 8,6 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-21 23:25
| Metric | Value |
|--------|-------|
| Map build time | 0,68s |
| Classic→Enhanced first switch (yielded warm) | 8,83s |
| Repeat Classic↔Enhanced (warm) | 15,0ms |
| TextureCount / MaterialCount / NormalMapCount | 1389 / 316 / 231 |
| EnhancedVariantCount | 231 |
| Native albedo bytes | 7,4 MB |
| Enhanced 4× albedo bytes | 158,4 MB |
| Normal+height bytes | 158,4 MB |
| Sprite Enhanced bytes | 7,0 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 915,5 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-22 00:12
| Metric | Value |
|--------|-------|
| Map build time | 1,34s |
| Classic→Enhanced first switch (yielded warm) | 9,09s |
| Avg repeat switch (20×, timeScale=0) | 15,3ms |
| TextureCount / MaterialCount / NormalMapCount | 1446 / 330 / 240 |
| EnhancedVariantCount | 240 |
| Enhanced 4× albedo bytes | 154,2 MB |
| Normal+height bytes | 154,2 MB |
| Sprite Enhanced bytes | 8,6 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-22 00:13
| Metric | Value |
|--------|-------|
| Map build time | 0,63s |
| Classic→Enhanced first switch (yielded warm) | 8,77s |
| Repeat Classic↔Enhanced (warm) | 14,6ms |
| TextureCount / MaterialCount / NormalMapCount | 1389 / 316 / 231 |
| EnhancedVariantCount | 231 |
| Native albedo bytes | 7,4 MB |
| Enhanced 4× albedo bytes | 158,4 MB |
| Normal+height bytes | 158,4 MB |
| Sprite Enhanced bytes | 7,0 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 927,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-22 20:12
| Metric | Value |
|--------|-------|
| Map build time | 1,28s |
| Classic→Enhanced first switch (yielded warm) | 8,82s |
| Avg repeat switch (20×, timeScale=0) | 18,0ms |
| TextureCount / MaterialCount / NormalMapCount | 1446 / 330 / 240 |
| EnhancedVariantCount | 240 |
| Enhanced 4× albedo bytes | 154,2 MB |
| Normal+height bytes | 154,2 MB |
| Sprite Enhanced bytes | 8,6 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-22 20:12
| Metric | Value |
|--------|-------|
| Map build time | 0,61s |
| Classic→Enhanced first switch (yielded warm) | 8,50s |
| Repeat Classic↔Enhanced (warm) | 11,9ms |
| TextureCount / MaterialCount / NormalMapCount | 1389 / 316 / 231 |
| EnhancedVariantCount | 231 |
| Native albedo bytes | 7,4 MB |
| Enhanced 4× albedo bytes | 158,4 MB |
| Normal+height bytes | 158,4 MB |
| Sprite Enhanced bytes | 7,0 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 943,9 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-23 15:52
| Metric | Value |
|--------|-------|
| Map build time | 1,30s |
| Classic→Enhanced first switch (yielded warm) | 8,73s |
| Avg repeat switch (20×, timeScale=0) | 14,1ms |
| TextureCount / MaterialCount / NormalMapCount | 1446 / 330 / 240 |
| EnhancedVariantCount | 240 |
| Enhanced 4× albedo bytes | 154,2 MB |
| Normal+height bytes | 154,2 MB |
| Sprite Enhanced bytes | 8,6 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-23 15:52
| Metric | Value |
|--------|-------|
| Map build time | 0,62s |
| Classic→Enhanced first switch (yielded warm) | 8,48s |
| Repeat Classic↔Enhanced (warm) | 11,8ms |
| TextureCount / MaterialCount / NormalMapCount | 1389 / 316 / 231 |
| EnhancedVariantCount | 231 |
| Native albedo bytes | 7,4 MB |
| Enhanced 4× albedo bytes | 158,4 MB |
| Normal+height bytes | 158,4 MB |
| Sprite Enhanced bytes | 7,0 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 918,0 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-24 00:22
| Metric | Value |
|--------|-------|
| Map build time | 1,31s |
| Classic→Enhanced first switch (yielded warm) | 8,50s |
| Avg repeat switch (20×, timeScale=0) | 14,9ms |
| TextureCount / MaterialCount / NormalMapCount | 1446 / 330 / 240 |
| EnhancedVariantCount | 240 |
| Enhanced 4× albedo bytes | 154,2 MB |
| Normal+height bytes | 154,2 MB |
| Sprite Enhanced bytes | 8,6 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-24 00:23
| Metric | Value |
|--------|-------|
| Map build time | 0,63s |
| Classic→Enhanced first switch (yielded warm) | 8,42s |
| Repeat Classic↔Enhanced (warm) | 12,7ms |
| TextureCount / MaterialCount / NormalMapCount | 1389 / 316 / 231 |
| EnhancedVariantCount | 231 |
| Native albedo bytes | 7,4 MB |
| Enhanced 4× albedo bytes | 158,4 MB |
| Normal+height bytes | 158,4 MB |
| Sprite Enhanced bytes | 7,0 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 1001,5 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-24 00:58
| Metric | Value |
|--------|-------|
| Map build time | 1,31s |
| Classic→Enhanced first switch (yielded warm) | 8,77s |
| Avg repeat switch (20×, timeScale=0) | 15,8ms |
| TextureCount / MaterialCount / NormalMapCount | 1446 / 330 / 240 |
| EnhancedVariantCount | 240 |
| Enhanced 4× albedo bytes | 154,2 MB |
| Normal+height bytes | 154,2 MB |
| Sprite Enhanced bytes | 8,6 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-24 00:59
| Metric | Value |
|--------|-------|
| Map build time | 0,61s |
| Classic→Enhanced first switch (yielded warm) | 8,66s |
| Repeat Classic↔Enhanced (warm) | 11,9ms |
| TextureCount / MaterialCount / NormalMapCount | 1389 / 316 / 231 |
| EnhancedVariantCount | 231 |
| Native albedo bytes | 7,4 MB |
| Enhanced 4× albedo bytes | 158,4 MB |
| Normal+height bytes | 158,4 MB |
| Sprite Enhanced bytes | 7,0 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 922,5 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-24 17:07
| Metric | Value |
|--------|-------|
| Map build time | 1,30s |
| Classic→Enhanced first switch (yielded warm) | 8,23s |
| Avg repeat switch (20×, timeScale=0) | 16,0ms |
| TextureCount / MaterialCount / NormalMapCount | 1446 / 330 / 240 |
| EnhancedVariantCount | 240 |
| Enhanced 4× albedo bytes | 154,2 MB |
| Normal+height bytes | 154,2 MB |
| Sprite Enhanced bytes | 8,6 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-24 17:08
| Metric | Value |
|--------|-------|
| Map build time | 0,60s |
| Classic→Enhanced first switch (yielded warm) | 8,48s |
| Repeat Classic↔Enhanced (warm) | 13,3ms |
| TextureCount / MaterialCount / NormalMapCount | 1389 / 316 / 231 |
| EnhancedVariantCount | 231 |
| Native albedo bytes | 7,4 MB |
| Enhanced 4× albedo bytes | 158,4 MB |
| Normal+height bytes | 158,4 MB |
| Sprite Enhanced bytes | 7,0 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 1013,7 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-24 18:40
| Metric | Value |
|--------|-------|
| Map build time | 1,28s |
| Classic→Enhanced first switch (yielded warm) | 8,40s |
| Avg repeat switch (20×, timeScale=0) | 13,1ms |
| TextureCount / MaterialCount / NormalMapCount | 1446 / 330 / 240 |
| EnhancedVariantCount | 240 |
| Enhanced 4× albedo bytes | 154,2 MB |
| Normal+height bytes | 154,2 MB |
| Sprite Enhanced bytes | 8,6 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-24 18:41
| Metric | Value |
|--------|-------|
| Map build time | 0,60s |
| Classic→Enhanced first switch (yielded warm) | 8,60s |
| Repeat Classic↔Enhanced (warm) | 14,6ms |
| TextureCount / MaterialCount / NormalMapCount | 1389 / 316 / 231 |
| EnhancedVariantCount | 231 |
| Native albedo bytes | 7,4 MB |
| Enhanced 4× albedo bytes | 158,4 MB |
| Normal+height bytes | 158,4 MB |
| Sprite Enhanced bytes | 7,0 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 1040,3 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M7)

Date: 2026-08-24 18:45
| Metric | Value |
|--------|-------|
| Map build time | 1,25s |
| Classic→Enhanced first switch (yielded warm) | 9,22s |
| Avg repeat switch (20×, timeScale=0) | 32,6ms |
| TextureCount / MaterialCount / NormalMapCount | 1446 / 330 / 240 |
| EnhancedVariantCount | 240 |
| Enhanced 4× albedo bytes | 154,2 MB |
| Normal+height bytes | 154,2 MB |
| Sprite Enhanced bytes | 8,6 MB |
| HUD Enhanced bytes | 2,8 MB |
| Mitigation ladder | none applied |

## Task 9 performance gate (E1M1)

Date: 2026-08-24 18:46
| Metric | Value |
|--------|-------|
| Map build time | 0,61s |
| Classic→Enhanced first switch (yielded warm) | 8,37s |
| Repeat Classic↔Enhanced (warm) | 11,4ms |
| TextureCount / MaterialCount / NormalMapCount | 1389 / 316 / 231 |
| EnhancedVariantCount | 231 |
| Native albedo bytes | 7,4 MB |
| Enhanced 4× albedo bytes | 158,4 MB |
| Normal+height bytes | 158,4 MB |
| Sprite Enhanced bytes | 7,0 MB |
| HUD Enhanced bytes | 2,8 MB |
| Managed (GC.GetTotalMemory) | 946,2 MB |
| Mitigation ladder | none applied |
