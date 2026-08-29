"""Tone-match healed weapon viewmodel redraws to their native lumps.

Same per-channel mean/std transfer as tone_match_redraw.py (std-cap 1.6),
but sourced from the HEALED redraw.png (heal_weapon_checker.py runs first
— redraw-raw.png keeps the untouched GPT output as provenance, so stats
would otherwise be polluted by the fake-transparency checker), and the
statistics run over texels opaque in BOTH the redraw and the native,
measured on the redraw downscaled to native size. Alpha ships unchanged.

Usage:
  python Tools/tone_match_weapon.py PUNGA0 ...
  python Tools/tone_match_weapon.py --all
"""
from __future__ import annotations

import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent / "Textures" / "WeaponRedraw"
STD_CAP = 1.6
LUM = np.array([0.299, 0.587, 0.114])


def tone_match(lump: str) -> None:
    d = ROOT / lump
    red_img = Image.open(d / "redraw.png").convert("RGBA")
    nat_img = Image.open(d / "native.png").convert("RGBA")

    red = np.asarray(red_img, dtype=np.float64)
    nat = np.asarray(nat_img, dtype=np.float64)

    small = np.asarray(
        red_img.resize(nat_img.size, Image.LANCZOS), dtype=np.float64)
    m = (small[..., 3] > 128) & (nat[..., 3] > 0)
    if m.sum() < 16:
        print(f"{lump}: too few shared opaque texels, skipped")
        return

    out = red.copy()
    ra = red[..., 3] > 0
    for c in range(3):
        rn, rr = nat[..., c][m], small[..., c][m]
        gain = np.clip(
            rn.std() / max(rr.std(), 1e-6), 1.0 / STD_CAP, STD_CAP)
        ch = (red[..., c] - rr.mean()) * gain + rn.mean()
        out[..., c] = np.where(ra, np.clip(ch, 0, 255), red[..., c])

    la = float((nat[..., :3][m] @ LUM).mean())
    before = float((small[..., :3][m] @ LUM).mean())
    out_small = np.asarray(
        Image.fromarray(out.astype(np.uint8), "RGBA")
        .resize(nat_img.size, Image.LANCZOS), dtype=np.float64)
    after = float((out_small[..., :3][m] @ LUM).mean())
    Image.fromarray(out.astype(np.uint8), "RGBA").save(d / "redraw.png")
    print(f"{lump}: lum native {la:6.1f}  redraw {before:6.1f} "
          f"({before / la:.2f}) -> {after:6.1f} ({after / la:.2f}, "
          f"abs err {abs(after - la):.1f})")


def main():
    args = sys.argv[1:]
    if not args:
        raise SystemExit(__doc__)
    if args[0] == "--all":
        lumps = [d.name for d in sorted(ROOT.iterdir()) if d.is_dir()]
    else:
        lumps = [a.upper() for a in args]
    for lump in lumps:
        tone_match(lump)


if __name__ == "__main__":
    main()
