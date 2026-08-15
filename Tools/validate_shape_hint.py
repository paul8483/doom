"""Pre-flight for incoming v2 conditioning redraws.

Two failure modes have cost whole iterations before, and both are cheap to
catch before anything is sent to TRELLIS:

  * a redraw arrives with the background BAKED IN (opaque everywhere, or a
    checkerboard the tools then have to guess at), which poisons the
    silhouette the projection step relies on;
  * a death/fall frame is drawn as a STANDING figure. Mesh scale comes from
    the frame's native patch height, so an upright redraw of a collapsing
    pose is silently stretched in game.

The first is a hard failure. The second is only ever a hint: accepted v2
redraws deviate from their native aspect by up to ~50% (SPOSG1 spreads the
arms far wider than the sprite does), so the aspect line is printed as
information for the eye, not as a gate.

Usage:
  python Tools/validate_shape_hint.py SPOSH0 SPOSI0 ...
  python Tools/validate_shape_hint.py --file some-hint.png --lump SPOSH0
"""
from __future__ import annotations

import argparse
import struct
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

sys.path.insert(0, str(Path(__file__).resolve().parent))
import doomify3d  # noqa: E402
from recolor_to_hint import hint_foreground  # noqa: E402

# 3D/ holds the volumetric conditioning renders; the 2D/ sibling
# holds the display-grade redraws imported by DisplayRedrawAllowlist.
HINTS = doomify3d.REPO / "Textures/Trellis2/ShapeHints/3D"
ASPECT_TOLERANCE = 0.35


def native_aspect(lump: str):
    """width/height of the native patch, or None when the lump is missing."""
    try:
        raw = doomify3d.wad_lump(lump)
    except SystemExit:
        return None
    w, h, _, _ = struct.unpack_from("<hhhh", raw, 0)
    return w / max(1, h)


def check(path: Path, lump: str) -> bool:
    if not path.exists():
        print(f"{lump}: MISSING {path}")
        return False
    arr = np.asarray(Image.open(path).convert("RGBA"))
    h, w = arr.shape[:2]
    ok = True
    print(f"{lump}: {w}x{h}")

    alpha = arr[..., 3]
    opaque = float((alpha > 0).mean())
    fg = hint_foreground(arr)
    if alpha.min() == 255:
        # Not fatal by itself: the accepted v2 set is opaque RGB with a baked
        # checkerboard, which hint_foreground strips. It IS fatal when the
        # background is a flat photo-like backdrop it cannot recognize.
        print(f"  alpha: fully opaque, foreground by checker removal "
              f"{fg.mean() * 100:.1f}% of canvas")
        if fg.mean() > 0.85:
            print("  FAIL: background not separable — no alpha and no "
                  "checkerboard; ask for a transparent or checkered render")
            ok = False
    else:
        print(f"  alpha: {opaque * 100:.1f}% opaque")
        fg = fg & (alpha > 128)

    labels, n = ndimage.label(fg)
    if n == 0:
        print("  FAIL: empty silhouette")
        return False
    sizes = ndimage.sum(fg, labels, range(1, n + 1))
    fg = labels == (1 + int(np.argmax(sizes)))
    stray = 1 - sizes.max() / max(1.0, sizes.sum())
    ys, xs = np.where(fg)
    bw, bh = xs.max() - xs.min() + 1, ys.max() - ys.min() + 1
    print(f"  silhouette: {bw}x{bh} (aspect {bw / bh:.2f}), "
          f"{n} components, {stray * 100:.1f}% outside the largest")

    native = native_aspect(lump)
    if native is None:
        print("  note: no such WAD lump — aspect not compared")
    else:
        drift = abs(bw / bh - native) / native
        print(f"  native patch aspect {native:.2f}, redraw {bw / bh:.2f} "
              f"({drift * 100:.0f}% off)")
        if drift > ASPECT_TOLERANCE and bw / bh < native:
            print("  LOOK: the redraw is TALLER than the native frame — on a "
                  "fall frame that usually means it was drawn standing")
    return ok


def main():
    p = argparse.ArgumentParser()
    p.add_argument("lumps", nargs="*", help="e.g. SPOSH0 SPOSI0")
    p.add_argument("--file", default=None,
                   help="check this file instead of the ShapeHints path")
    p.add_argument("--lump", default=None, help="lump name for --file")
    a = p.parse_args()

    if a.file:
        return 0 if check(Path(a.file), a.lump or Path(a.file).stem) else 1
    bad = 0
    for lump in a.lumps:
        if not check(HINTS / f"{lump}-depth-shapehint-v2.png", lump):
            bad += 1
        print()
    print(f"{len(a.lumps) - bad}/{len(a.lumps)} hints ready")
    return 1 if bad else 0


if __name__ == "__main__":
    raise SystemExit(main())
