"""Heal a redraw's wrap seam by cross-fading the wrapped edge pair.

For a border of B pixels, column i and column (w-1-i) are blended toward
each other with a weight that reaches 0.5 at the outermost pair (making the
wrapped columns identical) and falls to 0 at depth B. Run AFTER
tone_match_redraw.py (the heal edits redraw.png in place; the raw generation
already sits in redraw-raw.png). Wave-4 rationale: GPT occasionally paints
the two edges of a tiling texture differently (TEKWALL1, CRATE1) and doors
never tile in-game but still must satisfy the allowlist seam test
(BIGDOOR2/BIGDOOR6) — a few-texel cross-fade is invisible at either scale.

Usage:
  python Tools/heal_tile_seam.py TEKWALL1 CRATE1 ...        # horizontal
  python Tools/heal_tile_seam.py NAME --vertical            # vertical too
  python Tools/heal_tile_seam.py NAME --border 16
"""
import os
import sys

import numpy as np
from PIL import Image

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(REPO, "Textures", "WorldRedraw")
DEFAULT_BORDER = 12  # 4x pixels = 3 native texels


def heal(lump, border, vertical):
    path = os.path.join(SRC, lump, "redraw.png")
    img = np.asarray(Image.open(path).convert("RGB"), dtype=np.float64)
    h, w, _ = img.shape

    for i in range(border):
        t = 0.5 * (border - i) / border
        a = img[:, i].copy()
        b = img[:, w - 1 - i].copy()
        img[:, i] = a * (1 - t) + b * t
        img[:, w - 1 - i] = b * (1 - t) + a * t
    if vertical:
        for i in range(border):
            t = 0.5 * (border - i) / border
            a = img[i, :].copy()
            b = img[h - 1 - i, :].copy()
            img[i, :] = a * (1 - t) + b * t
            img[h - 1 - i, :] = b * (1 - t) + a * t

    Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), "RGB").save(path)
    print("OK", lump, "border", border, "vertical" if vertical else "horizontal")


if __name__ == "__main__":
    args = [a for a in sys.argv[1:]]
    vertical = "--vertical" in args
    border = DEFAULT_BORDER
    if "--border" in args:
        border = int(args[args.index("--border") + 1])
        del args[args.index("--border"):args.index("--border") + 2]
    names = [a for a in args if not a.startswith("--")]
    if not names:
        raise SystemExit(__doc__)
    for n in names:
        heal(n.upper(), border, vertical)
