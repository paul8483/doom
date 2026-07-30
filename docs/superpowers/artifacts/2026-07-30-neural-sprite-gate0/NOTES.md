# Neural Sprite Upscale — Gate 0 analysis archive

**Date:** 2026-07-30  
**Branch:** `esrgan`  
**Outcome:** ❌ FAIL — experiment stopped before runtime integration  
**Spec/plan:** `docs/superpowers/specs/2026-07-30-neural-sprite-upscale-design.md`,
`docs/superpowers/plans/2026-07-30-neural-sprite-upscale.md`

## What is archived here

Triptych PNGs (left → right):

1. **native** — Freedoom WAD patch, nearest-neighbor display ×8  
2. **Super-xBR 4× + Sharpen 0.5** — current Enhanced sprite path  
3. **Real-ESRGAN 4×** — neural RGB from native+AlphaBleed; alpha from Super-xBR

| Folder | Model | ONNX | SHA-256 |
|--------|-------|------|---------|
| `anime_6B/` | RealESRGAN_x4plus_anime_6B | `Assets/ThirdParty/RealEsrgan/RealESRGAN_x4plus_anime_6B.onnx` | `2648CAB4C4343541C1AA291C6754E9E8EDBE7A813FFFC2A677423DD12CB6B7F7` |
| `animevideov3/` | realesr-animevideov3 ×4 | `Assets/ThirdParty/RealEsrgan/realesr-animevideov3_x4.onnx` | `00ECE3AC21C43EE31459216B5174B2CEA0C5325044C5142AEB840F4890E175FF` |

18 PNGs per model (~5.1 MB total). Sentis `com.unity.ai.inference` 2.6.1, backend GPUCompute.

### Input policy (important for re-reads)

Neural input = **native WAD pixels** after `AlphaBleedGuard.Dilate` only.  
Not Enhanced/Super-xBR. Output size exactly 4×; alpha channel merged from Super-xBR.

### Lump set

Imp rotations/frames that exist as discrete lumps, zombie, shotgun guy (partial),
`SHTGA0`, `MEDIA0`, `STBAR`, imp walk `TROOA1/B1/C1/D1`. Some DOOM dual-name
rotations (`A2A8` etc.) were skipped as missing discrete lump names.

## Interactive verdict (user)

- Neural results also look soft/smeared.  
- Contours became ragged / jagged.  
- Example called out: `animevideov3/POSSA6.png` — not better than Super-xBR panel;
  arm silhouette uneven (“waves”).  
- Both candidates rejected. Tasks 2–6 not started. Runtime stays Super-xBR+Sharpen.

## Suggested analysis focus

- Silhouette stability vs Super-xBR on limbs (`POSSA6`, `SPOSA*`, `SHTGA0`).  
- Whether anime-tuned ESRGAN over-fits flat cel art vs Freedoom’s dithered shading.  
- Temporal consistency across `TROOA1` → `TROOB1` → `TROOC1` → `TROOD1`.  
- STBAR / HUD text legibility vs Super-xBR.  
- Future options (out of scope after Gate 0 fail): different model family,
  edge-aware post, scale 2× only, category-limited apply — only if revisited.

## Tooling that produced these

`Tools > Doom > Dump Neural Sprite Preview`  
→ `Assets/Scripts/MapBuild/Editor/NeuralSpritePreviewMenu.cs`  
Original dump also under gitignored `Logs/neural-preview/` (copy kept here for analysis).
