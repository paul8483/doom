"""Doom-style a TRELLIS conditioning shapehint: pixelate + palette-quantize.

The accepted doom-style references (ROCKA0/SHELA0/tree v2 hints) share chunky
pixel blocks, a narrow muted palette and stepped facet shading with no
specular gloss. GPT redraws of hard-surface items (ARM1B0, STIMA0) come out
airbrushed instead; this tool degrades them to the reference look while
leaving the checkerboard backdrop untouched.

Usage:
  python Tools/doomify_shapehint.py --src <in.png> --dst <out.png>
      [--cells 56] [--colors 18]
"""
from __future__ import annotations

import argparse
from collections import Counter
from pathlib import Path

import numpy as np
from PIL import Image

LIGHT_MAX = 225      # border flood-fill: near-white/neutral checker
LIGHT_SAT = 20
CHECKER_TOL = 10     # enclosed pockets: exact checker colors +/- tol
MIN_CELL_COVERAGE = 0.5


def light_mask(rgb: np.ndarray) -> np.ndarray:
    mx = rgb.max(axis=2).astype(np.int16)
    mn = rgb.min(axis=2).astype(np.int16)
    return (mx >= LIGHT_MAX) & ((mx - mn) <= LIGHT_SAT)


def flood_from_border(candidate: np.ndarray) -> np.ndarray:
    h, w = candidate.shape
    seen = np.zeros_like(candidate, dtype=bool)
    stack = []
    for x in range(w):
        stack += [(0, x), (h - 1, x)]
    for y in range(h):
        stack += [(y, 0), (y, w - 1)]
    while stack:
        y, x = stack.pop()
        if y < 0 or x < 0 or y >= h or x >= w:
            continue
        if seen[y, x] or not candidate[y, x]:
            continue
        seen[y, x] = True
        stack += [(y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)]
    return seen


def checker_colors(rgb: np.ndarray, border: np.ndarray) -> list[tuple[int, int, int]]:
    """The two dominant exact colors of the border-connected checkerboard."""
    pixels = rgb[border]
    counts = Counter(map(tuple, pixels[:: max(1, len(pixels) // 20000)]))
    return [c for c, _ in counts.most_common(2)]


def doomify(src: Path, dst: Path, cells: int, colors: int) -> None:
    img = Image.open(src).convert("RGB")
    rgb = np.asarray(img).copy()
    h, w, _ = rgb.shape

    border_backdrop = flood_from_border(light_mask(rgb))
    backdrop = border_backdrop.copy()
    for c in checker_colors(rgb, border_backdrop):
        close = (np.abs(rgb.astype(np.int16) - np.array(c, dtype=np.int16))
                 .max(axis=2) <= CHECKER_TOL)
        backdrop |= close
    subject = ~backdrop

    ys, xs = np.nonzero(subject)
    if len(ys) == 0:
        raise SystemExit(f"{src}: no subject pixels found")
    y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    bw, bh = x1 - x0, y1 - y0
    cell = max(1, round(max(bw, bh) / cells))
    gw, gh = (bw + cell - 1) // cell, (bh + cell - 1) // cell

    # Average subject pixels per grid cell (backdrop excluded from the mean).
    small = np.zeros((gh, gw, 3), dtype=np.float64)
    cover = np.zeros((gh, gw), dtype=np.float64)
    for gy in range(gh):
        sy = y0 + gy * cell
        ey = min(sy + cell, y1)
        for gx in range(gw):
            sx = x0 + gx * cell
            ex = min(sx + cell, x1)
            m = subject[sy:ey, sx:ex]
            n = int(m.sum())
            area = (ey - sy) * (ex - sx)
            cover[gy, gx] = n / area
            if n:
                small[gy, gx] = rgb[sy:ey, sx:ex][m].mean(axis=0)

    filled = cover >= MIN_CELL_COVERAGE
    # Median-cut quantize the filled cells to a narrow palette.
    cells_img = Image.fromarray(small.astype(np.uint8), "RGB")
    quant = cells_img.quantize(colors=colors, method=Image.MEDIANCUT)
    small_q = np.asarray(quant.convert("RGB"))

    out = rgb.copy()
    for gy in range(gh):
        sy = y0 + gy * cell
        ey = min(sy + cell, y1)
        for gx in range(gw):
            if not filled[gy, gx]:
                continue
            sx = x0 + gx * cell
            ex = min(sx + cell, x1)
            out[sy:ey, sx:ex] = small_q[gy, gx]

    # Cells dropped below coverage revert to backdrop: repaint any leftover
    # subject pixels there with the nearest checker color so no smooth
    # remnants survive around the blocky silhouette.
    keep = np.zeros((h, w), dtype=bool)
    for gy in range(gh):
        sy = y0 + gy * cell
        ey = min(sy + cell, y1)
        for gx in range(gw):
            if filled[gy, gx]:
                sx = x0 + gx * cell
                ex = min(sx + cell, x1)
                keep[sy:ey, sx:ex] = True
    stray = subject & ~keep
    if stray.any():
        c = checker_colors(rgb, border_backdrop)[0]
        out[stray] = c

    Image.fromarray(out, "RGB").save(dst)
    print(f"OK {src.name} -> {dst.name} "
          f"(cell={cell}px grid={gw}x{gh} colors<={colors})")


def main() -> None:
    p = argparse.ArgumentParser()
    p.add_argument("--src", required=True)
    p.add_argument("--dst", required=True)
    p.add_argument("--cells", type=int, default=56)
    p.add_argument("--colors", type=int, default=18)
    a = p.parse_args()
    doomify(Path(a.src), Path(a.dst), a.cells, a.colors)


if __name__ == "__main__":
    main()
