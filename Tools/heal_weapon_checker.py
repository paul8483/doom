"""Cut baked fake-transparency checker out of weapon viewmodel redraws.

GPT draws the subject slightly narrower than the native silhouette and
fills the gap with the transparency checkerboard (#FFF/#CCC 8px blocks
at the 4x canvas); clipping alpha to the exact native mask then keeps
that gap OPAQUE, which renders in game as a white halo band along the
weapon's edge.

The heal is native-guided so legit white detail survives:
  seeds = strict checker pixels (flat light blocks alternating with flat
          gray blocks on the 8px grid)
        + light desaturated pixels touching the mask boundary whose
          native color is dissimilar (a real fringe touches the boundary
          by construction; BFG flash spikes are white over a white
          native and stay protected)
  grow  = BFS from seeds through light, native-dissimilar pixels
  cut   = alpha 0 on the grown region, one erosion pass over the
          contaminated rim, speckle cleanup

The original file is parked once as redraw-raw.png (pit convention).

Usage:
  python Tools/heal_weapon_checker.py --lumps PUNGD0 ...
  python Tools/heal_weapon_checker.py --all
"""
from __future__ import annotations

import argparse
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent / "Textures" / "WeaponRedraw"


def lum_sat(rgb: np.ndarray):
    lum = rgb.mean(-1)
    sat = rgb.max(-1) - rgb.min(-1)
    return lum, sat


def flat_mask(lum: np.ndarray, rad: int = 2, tol: float = 6.0) -> np.ndarray:
    """Local flatness: max-min of lum in a (2rad+1)^2 window below tol."""
    h, w = lum.shape
    mx = lum.copy()
    mn = lum.copy()
    for dy in range(-rad, rad + 1):
        for dx in range(-rad, rad + 1):
            sh = np.full_like(lum, np.nan)
            ys, yd = (dy, None) if dy >= 0 else (0, dy)
            xs, xd = (dx, None) if dx >= 0 else (0, dx)
            src = lum[max(0, dy):h + min(0, dy), max(0, dx):w + min(0, dx)]
            sh[max(0, -dy):h + min(0, -dy), max(0, -dx):w + min(0, -dx)] = src
            mx = np.fmax(mx, sh)
            mn = np.fmin(mn, sh)
    return (mx - mn) < tol


def shift(m: np.ndarray, dy: int, dx: int) -> np.ndarray:
    out = np.zeros_like(m)
    h, w = m.shape
    out[max(0, -dy):h + min(0, -dy), max(0, -dx):w + min(0, -dx)] = \
        m[max(0, dy):h + min(0, dy), max(0, dx):w + min(0, dx)]
    return out


def neighbors8(m: np.ndarray) -> np.ndarray:
    out = np.zeros_like(m)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            if dy == 0 and dx == 0:
                continue
            out |= shift(m, dy, dx)
    return out


def heal(lump: str, use_lake: bool = False,
         rim_aggressive: bool = False) -> None:
    d = ROOT / lump
    raw_path = d / "redraw-raw.png"
    src_path = raw_path if raw_path.exists() else d / "redraw.png"
    red = np.array(Image.open(src_path).convert("RGBA"))
    nat = np.array(Image.open(d / "native-x4.png").convert("RGBA"))

    a = red[..., 3] > 0
    rl, rs = lum_sat(red[..., :3].astype(float))
    nl, ns = lum_sat(nat[..., :3].astype(float))

    dissim = np.abs(red[..., :3].astype(int) - nat[..., :3].astype(int)) \
        .max(-1) > 48

    white = (rs < 16) & (rl >= 235)
    gray = (rs < 16) & (rl >= 183) & (rl <= 230)
    flat = flat_mask(rl)
    wf, gf = white & flat, gray & flat
    checker = np.zeros_like(a)
    for dy, dx in ((8, 0), (-8, 0), (0, 8), (0, -8)):
        checker |= wf & shift(gf, dy, dx)
        checker |= gf & shift(wf, dy, dx)
    checker &= a

    hole = ~a
    near_hole = neighbors8(neighbors8(hole))
    fringe = a & near_hole & (rs < 55) & (rl >= 185) & dissim

    # depth cap: the halo is a boundary band — BFS distance from the mask
    # boundary, so a bright interior (over-toned redraws, flash cores)
    # can never be eaten no matter how dissimilar it reads.
    h, w = a.shape
    depth = np.full((h, w), 1 << 30, np.int32)
    q = deque()
    for y, x in zip(*np.nonzero(a & neighbors8(hole))):
        depth[y, x] = 0
        q.append((y, x))
    while q:
        y, x = q.popleft()
        nd = depth[y, x] + 1
        if nd > 28:
            continue
        for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
            if 0 <= ny < h and 0 <= nx < w and a[ny, nx] \
                    and nd < depth[ny, nx]:
                depth[ny, nx] = nd
                q.append((ny, nx))
    band = depth <= 28

    # plain flat white lakes: GPT sometimes fills the background with
    # solid white instead of the checker (PLSG hid a 19k-px white lake
    # under the hull). A big connected flat near-white native-dissimilar
    # component is never honest subject detail; small flat cores (flash
    # spikes) stay protected by the area threshold.
    # tol 12: a subtle 245/252 checker (PLSG) must read as one flat lake,
    # not fragment into per-block islands below the area threshold
    lake = np.zeros_like(a)
    if use_lake:
        lake = a & flat_mask(rl, tol=12.0) & (rs < 16) & (rl >= 238) & dissim
    lake_seed = np.zeros_like(a)
    seen = np.zeros_like(a)
    h0, w0 = a.shape
    for sy, sx in zip(*np.nonzero(lake)):
        if seen[sy, sx]:
            continue
        comp = [(sy, sx)]
        seen[sy, sx] = True
        qq = deque(comp)
        while qq:
            y, x = qq.popleft()
            for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
                if 0 <= ny < h0 and 0 <= nx < w0 and lake[ny, nx] \
                        and not seen[ny, nx]:
                    seen[ny, nx] = True
                    comp.append((ny, nx))
                    qq.append((ny, nx))
        if len(comp) >= 400:
            for y, x in comp:
                lake_seed[y, x] = True

    seeds = checker | (fringe & band) | lake_seed
    # strict checker-class pixels (flat white/gray) may be cut at ANY
    # depth — the 8-grid alternation signature is safe interior of a
    # subject (PLSG hid a checker lake >28px deep under the hull);
    # the looser light-and-dissimilar class stays confined to the band.
    strict = a & flat & (white | gray) & dissim
    grow_ok = (a & band & (rs < 55) & (rl >= 185) & dissim) | strict
    cut = seeds.copy()
    q = deque(zip(*np.nonzero(seeds)))
    while q:
        y, x = q.popleft()
        for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
            if 0 <= ny < h and 0 <= nx < w and grow_ok[ny, nx] \
                    and not cut[ny, nx]:
                cut[ny, nx] = True
                q.append((ny, nx))

    # rim erosion: three passes over contaminated neighbors of the cut zone
    for _ in range(3):
        rim = a & ~cut & neighbors8(cut) & band & (rl > 150) & dissim
        if not rim.any():
            break
        cut |= rim
    # --rim-aggressive: pale tinted halo (pink over skin) sits above the
    # desaturation classes — keep absorbing contact neighbors of the cut
    # region while they stay light and native-dissimilar
    if rim_aggressive:
        for _ in range(40):
            rim = a & ~cut & neighbors8(cut) & band                 & (rl > 165) & (rs < 80) & dissim
            if not rim.any():
                break
            cut |= rim
    # speckle cleanup to fixpoint: light islands mostly surrounded by
    # cut/holes get absorbed (a lake cut leaves salt noise on the subtle
    # checker's block borders — two passes were not enough)
    for _ in range(50):
        nb_open = np.zeros(a.shape, np.uint8)
        opened = cut | hole
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if dy == 0 and dx == 0:
                    continue
                nb_open += shift(opened, dy, dx).astype(np.uint8)
        add = a & ~cut & (nb_open >= 5) & (rl > 170) & (rs < 55)
        if not add.any():
            break
        cut |= add
    # lake salt: bright desaturated debris near a lake cut resists the
    # majority vote (mutually supporting clumps) — sweep it directly
    if use_lake and lake_seed.any():
        near_cut = cut.copy()
        for _ in range(12):
            near_cut = near_cut | neighbors8(near_cut)
        cut |= a & near_cut & (rl >= 200) & (rs < 30) & dissim

    n = int(cut.sum())
    if n == 0:
        print(f"{lump}: clean, nothing cut")
        return
    if not raw_path.exists():
        (d / "redraw.png").rename(raw_path)
    out = red.copy()
    out[cut] = 0
    Image.fromarray(out, "RGBA").save(d / "redraw.png")
    print(f"{lump}: cut {n}px ({100.0 * n / a.sum():.2f}% of opaque)")


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--lumps", nargs="*", default=None)
    p.add_argument("--all", action="store_true")
    p.add_argument("--lake", action="store_true",
                   help="also seed big interior flat-white lakes "
                        "(PLSG-style backgrounds; unsafe for flashes)")
    p.add_argument("--rim-aggressive", action="store_true",
                   help="keep eroding pale tinted halo by contact from "
                        "the cut region (PUNG pink fringe)")
    args = p.parse_args()
    lumps = args.lumps or []
    if args.all:
        lumps = [d.name for d in sorted(ROOT.iterdir()) if d.is_dir()]
    if not lumps:
        raise SystemExit("pass --lumps or --all")
    for lump in lumps:
        heal(lump, use_lake=args.lake,
             rim_aggressive=args.rim_aggressive)


if __name__ == "__main__":
    main()
