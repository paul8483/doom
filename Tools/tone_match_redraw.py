"""Tone-match a world redraw to its native composite texture.

GPT redraws drift dark (wave 2 came in at 0.53-0.90 of native luminance while
the accepted wave-1 band was ~0.87-0.97), and a flat wall's texel statistics
ARE its screen statistics, so the correction is a per-channel affine transfer
measured on the redraw downscaled to native size:

    y = (x - mean_redraw) * clip(std_native/std_redraw, 1/CAP, CAP) + mean_native

The mean moves fully; the std gain is capped so painted gradients are not
amplified into noise (BRICK10 came in at 0.39 of native contrast — an uncapped
transfer would boost every soft gradient x2.6). The original generation is
preserved once as redraw-raw.png; redraw.png becomes the corrected file, so
downstream (validator, quantize, install) needs no changes. Safe to re-run:
statistics are re-measured from redraw-raw.png when it exists.

Usage:
  python Tools/tone_match_redraw.py STARGR1 STARGR2 ...
"""
import os
import sys

import numpy as np
from PIL import Image

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(REPO, "Textures", "WorldRedraw")
STD_CAP = 1.6
LUM = np.array([0.299, 0.587, 0.114])


def tone_match(lump):
    folder = os.path.join(SRC, lump)
    raw_path = os.path.join(folder, "redraw-raw.png")
    out_path = os.path.join(folder, "redraw.png")
    if not os.path.exists(raw_path):
        os.replace(out_path, raw_path)

    native = Image.open(os.path.join(folder, "native.png")).convert("RGB")
    raw = Image.open(raw_path).convert("RGB")

    a = np.asarray(native, dtype=np.float64)
    b_small = np.asarray(raw.resize(native.size, Image.LANCZOS), dtype=np.float64)
    b = np.asarray(raw, dtype=np.float64)

    out = np.empty_like(b)
    for c in range(3):
        mean_n, std_n = a[..., c].mean(), a[..., c].std()
        mean_r, std_r = b_small[..., c].mean(), b_small[..., c].std()
        gain = std_n / max(std_r, 1e-6)
        gain = min(max(gain, 1.0 / STD_CAP), STD_CAP)
        out[..., c] = (b[..., c] - mean_r) * gain + mean_n

    out = np.clip(out, 0, 255).astype(np.uint8)
    Image.fromarray(out, "RGB").save(out_path)

    la = (a * LUM).sum(2).mean()
    before = (b_small * LUM).sum(2).mean()
    small = np.asarray(
        Image.fromarray(out, "RGB").resize(native.size, Image.LANCZOS),
        dtype=np.float64)
    after = (small * LUM).sum(2).mean()
    print("OK %-9s lum native %6.1f  redraw %6.1f (%.2f) -> %6.1f (%.2f)"
          % (lump, la, before, before / la, after, after / la))


if __name__ == "__main__":
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    for t in sys.argv[1:]:
        tone_match(t.upper())
