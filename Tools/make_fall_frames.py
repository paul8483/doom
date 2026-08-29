"""Synthesize waterfall animation frames by scrolling the anchor redraw.

The falls (SFALL/WFALL/BFALL) animate as downward flow; frames 2-4 are the
anchor redraw rolled down by height/4 per frame (128 px at 4x for a 64x128
wall), so the 4-frame loop closes exactly and every frame shares the same
texels - inter-frame consistency by construction. WFALL's native frames ARE
a 32 px/frame downward scroll; SFALL/BFALL natives boil in place, and the
synthesized scroll deliberately replaces the boil with steady flow (wave-15
approach decision). Enhanced's fluid cross-fade turns the 8-tic steps into
continuous motion.

Run AFTER the anchor is validated (both axes), tone-matched and healed:
frames inherit whatever the anchor's redraw.png holds at call time.

Usage:
  python Tools/make_fall_frames.py SFALL WFALL BFALL   (series base names)
"""
import os
import sys

import numpy as np
from PIL import Image

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ROOT = os.path.join(REPO, "Textures", "WorldRedraw")
FRAMES = 4


def synth(series):
    anchor_path = os.path.join(ROOT, series + "1", "redraw.png")
    anchor = np.asarray(Image.open(anchor_path).convert("RGB"))
    step = anchor.shape[0] // FRAMES  # 128 px at 4x: full loop = height
    for k in range(1, FRAMES):
        name = "%s%d" % (series, k + 1)
        out_dir = os.path.join(ROOT, name)
        os.makedirs(out_dir, exist_ok=True)
        # positive roll moves content DOWN - the direction water falls
        # (matches WFALL's native +32 px/frame scroll).
        frame = np.roll(anchor, step * k, axis=0)
        Image.fromarray(frame, "RGB").save(os.path.join(out_dir, "redraw.png"))
        print("OK %s = %s1 rolled down %d px" % (name, series, step * k))


if __name__ == "__main__":
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    for s in sys.argv[1:]:
        synth(s.upper())
