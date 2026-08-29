"""Enforce anim-frame identity outside the animation zone.

Generalization of enforce_switch_pair.py for in-place animated wall series
(ROCKRED, SLADRIP): frames of one sequence must be pixel-identical outside
the zone that actually animates, or the 8-tic frame swap pops the whole
wall. Outside the native anchor-vs-frame diff zone (dilated, feathered)
the frame takes the anchor redraw's pixels; inside, its own paint stays.
Run AFTER tone matching, BEFORE quantize/install.

Usage:
  python Tools/enforce_anim_frames.py ROCKRED1 ROCKRED2 ROCKRED3
  python Tools/enforce_anim_frames.py SLADRIP1 SLADRIP2 SLADRIP3
(first name is the anchor, the rest are pinned to it)
"""
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ROOT = os.path.join(REPO, "Textures", "WorldRedraw")
DILATE = 12   # px of margin around the native diff (4x space)
FEATHER = 4   # px of crossfade at the zone border
THRESHOLD = 16


def enforce(anchor, frame):
    na = np.asarray(Image.open(os.path.join(ROOT, anchor, "native-x4.png")).convert("RGB"), int)
    nf = np.asarray(Image.open(os.path.join(ROOT, frame, "native-x4.png")).convert("RGB"), int)
    ra = np.asarray(Image.open(os.path.join(ROOT, anchor, "redraw.png")).convert("RGB"), float)
    rf_path = os.path.join(ROOT, frame, "redraw.png")
    rf = np.asarray(Image.open(rf_path).convert("RGB"), float)

    zone = (np.abs(na - nf).max(axis=2) > THRESHOLD).astype(np.uint8) * 255
    zone_img = Image.fromarray(zone, "L").filter(ImageFilter.MaxFilter(2 * DILATE + 1))
    mask = np.asarray(zone_img.filter(ImageFilter.GaussianBlur(FEATHER)), float) / 255.0

    out = rf * mask[..., None] + ra * (1.0 - mask[..., None])
    Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGB").save(rf_path)

    outside = (np.abs(out - ra).max(axis=2) > 16)[mask < 0.01].mean() * 100
    print("OK %-9s zone %5.1f%%  outside-diff after: %.2f%%"
          % (frame, (mask > 0.5).mean() * 100, outside))


if __name__ == "__main__":
    if len(sys.argv) < 3:
        raise SystemExit(__doc__)
    anchor = sys.argv[1].upper()
    for f in sys.argv[2:]:
        enforce(anchor, f.upper())
