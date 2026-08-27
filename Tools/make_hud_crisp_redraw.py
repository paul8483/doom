"""Deterministic crisp 4x for degenerate-size HUD glyphs.

A 4x6 small digit carries 24 pixels, every one load-bearing: GPT redraws
mangled the interior twice (the counter wandered, windows filled), because
there is nothing to repaint at that size. The honest fix for the Enhanced
blur is a nearest-neighbor 4x of the native itself — hard fat pixels, the
exact Classic look, dropped into the redraw slot so the Super-xBR smear
never runs (HUD wave 1 set C decision, 2026-08-28).

Usage:
  python Tools/make_hud_crisp_redraw.py --lumps STYSNUM0 ...
  python Tools/make_hud_crisp_redraw.py --small-digits
"""
from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent / "Textures" / "HudRedraw"


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--lumps", nargs="*", default=None)
    p.add_argument("--small-digits", action="store_true")
    a = p.parse_args()

    lumps = list(a.lumps or [])
    if a.small_digits:
        lumps += [f"STYSNUM{i}" for i in range(10)]
        lumps += [f"STGNUM{i}" for i in range(10)]
    if not lumps:
        raise SystemExit("nothing to do: pass --lumps or --small-digits")

    for lump in lumps:
        d = ROOT / lump
        im = Image.open(d / "native.png").convert("RGBA")
        out = im.resize((im.width * 4, im.height * 4), Image.NEAREST)
        out.save(d / "redraw.png")
        print(f"{lump}: {im.size} -> {out.size} (nearest 4x)")


if __name__ == "__main__":
    main()
