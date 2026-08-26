"""Enforce switch-pair identity outside the actuator zone.

A switch pair (SW1XXX/SW2XXX) must be pixel-identical everywhere except the
actuator, or the swap pops the whole wall on press. Generation gets close but
strays (wave 12 came in at up to 0.7% of texels differing outside the zone),
so the invariant is enforced deterministically: outside the native pair's
diff zone (dilated, feathered) SW2 takes SW1's pixels; inside, SW2's own
paint stays. Run AFTER tone matching, BEFORE quantize/install.

Usage:
  python Tools/enforce_switch_pair.py BLUE COMP ...   (pair suffixes)
"""
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ROOT = os.path.join(REPO, "Textures", "WorldRedraw")
DILATE = 12   # px of margin around the native actuator diff
FEATHER = 4   # px of crossfade at the zone border


def enforce(pair):
    p1, p2 = "SW1" + pair, "SW2" + pair
    n1 = np.asarray(Image.open(os.path.join(ROOT, p1, "native-x4.png")).convert("RGB"), int)
    n2 = np.asarray(Image.open(os.path.join(ROOT, p2, "native-x4.png")).convert("RGB"), int)
    r1 = np.asarray(Image.open(os.path.join(ROOT, p1, "redraw.png")).convert("RGB"), float)
    r2_path = os.path.join(ROOT, p2, "redraw.png")
    r2 = np.asarray(Image.open(r2_path).convert("RGB"), float)

    zone = (np.abs(n1 - n2).max(axis=2) > 16).astype(np.uint8) * 255
    zone_img = Image.fromarray(zone, "L").filter(ImageFilter.MaxFilter(2 * DILATE + 1))
    mask = np.asarray(zone_img.filter(ImageFilter.GaussianBlur(FEATHER)), float) / 255.0

    out = r2 * mask[..., None] + r1 * (1.0 - mask[..., None])
    Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGB").save(r2_path)

    outside = (np.abs(out - r1).max(axis=2) > 16)[mask < 0.01].mean() * 100
    print("OK %-6s zone %4.1f%%  outside-diff after: %.2f%%"
          % (pair, (mask > 0.5).mean() * 100, outside))


if __name__ == "__main__":
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    for p in sys.argv[1:]:
        enforce(p.upper())
