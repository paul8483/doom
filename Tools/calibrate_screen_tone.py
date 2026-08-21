"""Calibrate a doomified albedo's gain so the RENDERED (screen-space)
luminance matches the native sprite.

Why texel statistics are not enough (BAR1 barrel, 2026-08-21): after
coverage-restricted tone_match the albedo's texel mean equalled the sprite's
(63 vs 67), yet in game the mesh read a whole shade lighter — screen lum 88
vs the sprite's 61. Bright rim UV islands cover far more VISIBLE area on the
mesh than their thin highlights do in the sprite, so tone must be judged
through the unlit render, not in the atlas. This tool iterates a global gain
on UV-covered texels, re-quantizes to the lump's native palette, and stops
when the rendered luminance matches the native crop's.

Usage:
  python Tools/calibrate_screen_tone.py --lump BAR1B0 \
      --src Logs/doomify3d/BAR1B0-512tone-v2 --out <albedo-out.png>
(reads <src>/<lump>.obj + <lump>_albedo.png; the native canvas comes from
Textures/Trellis2/NativeInputs/<lump>-trellis.png)
"""
import argparse
import sys
import numpy as np
from PIL import Image
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from doomify3d import load_obj, render, uv_coverage_mask, quantize, sprite_palette

REPO = Path(__file__).resolve().parent.parent


def lum(v):
    return 0.299 * v[:, 0] + 0.587 * v[:, 1] + 0.114 * v[:, 2]


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--lump", required=True)
    p.add_argument("--src", required=True,
                   help="dir with <lump>.obj + <lump>_albedo.png (doomified)")
    p.add_argument("--out", required=True, help="output albedo PNG")
    p.add_argument("--iters", type=int, default=4)
    a = p.parse_args()

    src = Path(a.src)
    native = np.asarray(Image.open(
        REPO / "Textures" / "Trellis2" / "NativeInputs" / f"{a.lump}-trellis.png"
    ).convert("RGBA")).astype(float)
    nm = native[..., 3] > 0
    target = lum(native[..., :3][nm]).mean()

    base = np.asarray(Image.open(src / f"{a.lump}_albedo.png").convert("RGBA")).astype(float)
    dv, dt, dcuv = load_obj(src / f"{a.lump}.obj")
    cover = uv_coverage_mask(src / f"{a.lump}.obj", (base.shape[1], base.shape[0]))
    m = cover & (base[..., 3] > 0)
    pal = sprite_palette(a.lump)

    # Palette quantization makes the response non-monotonic (COLUA0 oscillated
    # 85 -> 77 -> 87 around a target of 82), so keep the best-seen iteration
    # rather than the last one.
    gain, best, best_err = 1.0, None, float("inf")
    for i in range(a.iters):
        work = base.copy()
        work[..., :3][m] = np.clip(work[..., :3][m] * gain, 0, 255)
        img = Image.fromarray(quantize(work.astype(np.uint8), pal))
        r = render(dv, dt, dcuv, img, 0.0).astype(float)
        got = lum(r[..., :3][r[..., 3] > 0]).mean()
        err = abs(got - target)
        print(f"iter {i}: gain {gain:.3f} -> screen lum {got:.1f} (target {target:.1f})")
        if err < best_err:
            best, best_err = img, err
        if err < 1.5:
            break
        gain *= target / got

    best.save(a.out)
    print(f"saved {a.out} (err {best_err:.1f})")


if __name__ == "__main__":
    main()
