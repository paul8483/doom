"""Equalize the BACK tone of stop-motion frames to one anchor frame.

The fronts of a monster's frame set are one consistent hint projection, but
each back is that frame's own TRELLIS bake, and bakes drift: the SPOS walk
cycle pulsed 33..48 mean luminance from behind (reported 2026-08-30). A full
repaint of the backs from the anchor's rear shell (repaint_back_from_anchor)
FAILED its in-game gate — mesh-space NN with per-frame yaw alignment smeared
half a back dark on angled views — so this is the conservative fix: keep
every frame's own back CONTENT (its bake is geometry-correct by
construction) and transfer only per-channel tone toward the anchor.

Method (the barrel doctrine — judge tone in SCREEN space): render anchor and
frame from behind with their installed albedos, take per-channel mean/std
over the opaque pixels, and apply the affine transfer to the frame's
non-front atlas texels (rear shell + hidden; the front shell is the accepted
hint projection and stays bit-identical). Std gain is capped and the result
re-quantized to the sprite palette.

Usage:
  python Tools/tone_match_back.py \
      --dir Assets/Resources/ExperimentalMonsters/SPOS \
      --anchor SPOSA1 --frames SPOSB1 SPOSC1 SPOSD1 SPOSE1 SPOSF1 SPOSG1 \
      --palette-lump SPOSA1
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
import doomify3d  # noqa: E402
from repaint_back_from_anchor import frame_cloud, PROJ  # noqa: E402


def back_stats(d: Path, lump: str):
    verts, tris, cuv = doomify3d.load_obj(d / f"{lump}.obj")
    tex = Image.open(d / f"{lump}_albedo.png").convert("RGB")
    img = doomify3d.render(verts, tris, cuv, tex, 180.0, size=512)
    arr = img.astype(np.float64)
    rgb = arr[..., :3][arr[..., 3] > 0]
    return rgb.mean(0), rgb.std(0)


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--dir", required=True)
    p.add_argument("--anchor", required=True)
    p.add_argument("--frames", nargs="+", required=True)
    p.add_argument("--palette-lump", required=True)
    p.add_argument("--texcap", type=int, default=256)
    p.add_argument("--back-depth-eps", type=float, default=0.05)
    p.add_argument("--back-grid", type=int, default=256)
    p.add_argument("--std-cap", type=float, default=1.6,
                   help="max std gain per channel (tone_match_redraw rule)")
    a = p.parse_args()

    d = Path(a.dir)
    pal = doomify3d.sprite_palette(a.palette_lump)
    size = a.texcap
    k = PROJ // size

    am, asd = back_stats(d, a.anchor)
    print(f"{a.anchor}: back screen mean {np.round(am, 1)} std "
          f"{np.round(asd, 1)}")

    for lump in a.frames:
        fm, fsd = back_stats(d, lump)
        gain = np.clip(asd / np.maximum(fsd, 1e-6), 1.0 / a.std_cap,
                       a.std_cap)

        pts, filled, shell, hidden = frame_cloud(d, lump, a.back_grid,
                                                 a.back_depth_eps)
        idx_all = np.argwhere(filled)
        hit = np.zeros((PROJ, PROJ), bool)
        b = idx_all[shell | hidden]
        hit[b[:, 0], b[:, 1]] = True
        take = hit.reshape(size, k, size, k).mean((1, 3)) > 0.5

        alb_path = d / f"{lump}_albedo.png"
        alb = np.asarray(Image.open(alb_path).convert("RGBA")).copy()
        px = alb[take, :3].astype(np.float64)
        px = (px - fm) * gain + am
        shifted = np.clip(px, 0, 255).astype(np.uint8)
        alb[take, :3] = doomify3d.quantize(shifted[None, ...], pal)[0]
        Image.fromarray(alb).save(alb_path)

        nm, _ = back_stats(d, lump)
        print(f"{lump}: mean {np.round(fm, 1)} -> {np.round(nm, 1)} "
              f"(anchor {np.round(am, 1)}), gain {np.round(gain, 2)}, "
              f"texels {int(take.sum())}")


if __name__ == "__main__":
    main()
