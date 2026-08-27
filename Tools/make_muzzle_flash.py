"""Bake an irregular muzzle-flash sprite from a WAD fire frame's own texels.

A muzzle flash is not a smooth disc — the first shader draw (radial LUT on a
quad) read as an even circle and failed the gate («огонь не может быть ровным
кругом», 2026-08-27). The user's photo reference gives the SHAPE — a ragged
core, radial streaks, loose sparks — but a photoreal texture would break the
authenticity rule, so this bakes a chunky pixel-art burst instead, colored
strictly by the radial palette of the native flash ball (SPOSF1: white-yellow
core -> orange -> deep red rim, via PLAYPAL).

Deterministic: fixed seed, no Date/random drift between runs.

Usage:
  python Tools/make_muzzle_flash.py --lump SPOSF1 \
      --out Assets/Resources/ExperimentalMonsters/SPOS/SPOSF1_flash.png
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


def decode_patch(name: str) -> np.ndarray:
    raw = doomify3d.wad_lump(name)
    w, h, _, _ = struct.unpack_from("<hhhh", raw, 0)
    cols = struct.unpack_from(f"<{w}i", raw, 8)
    pal = doomify3d.playpal()
    img = np.zeros((h, w, 4), np.uint8)
    for x, ofs in enumerate(cols):
        p = ofs
        while raw[p] != 0xFF:
            top, length = raw[p], raw[p + 1]
            for i, idx in enumerate(raw[p + 3:p + 3 + length]):
                img[top + i, x, :3] = pal[idx]
                img[top + i, x, 3] = 255
            p += 4 + length
    return img


def flash_profile(lump: str, buckets: int = 8) -> np.ndarray:
    """Radial mean colors of the native flash ball, core -> rim."""
    a = decode_patch(lump).astype(float)
    op = a[..., 3] > 0
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    fire = op & (r > 140) & (r > b + 60) & (g > b)
    labels, n = ndimage.label(fire)
    sizes = ndimage.sum(fire, labels, range(1, n + 1))
    ball = labels == (1 + int(np.argmax(sizes)))
    ys, xs = np.where(ball)
    cy, cx = ys.mean(), xs.mean()
    rad = np.sqrt((xs - cx) ** 2 + (ys - cy) ** 2)
    R = max(np.percentile(rad, 99), 1e-6)
    prof, last = [], None
    for i in range(buckets):
        sel = (rad >= R * i / buckets) & (rad < R * (i + 1) / buckets)
        if sel.sum() > 0:
            last = a[ys[sel], xs[sel]][:, :3].mean(0)
        prof.append(last)
    for i in range(buckets - 1, -1, -1):  # backfill leading gaps
        if prof[i] is None:
            prof[i] = prof[i + 1]
    return np.array(prof)


def bake_burst(profile: np.ndarray, seed: int = 872506309,
               grid: int = 48, upscale: int = 4) -> np.ndarray:
    """Chunky pixel burst: noisy core + radial streaks + loose sparks.

    Colors come only from `profile` (core row 0 -> rim last row), so the
    burst stays inside the native flash palette by construction.
    """
    rng = np.random.RandomState(seed)
    c = grid / 2.0
    yy, xx = np.mgrid[0:grid, 0:grid]
    dx, dy = xx - c + 0.5, yy - c + 0.5
    rad = np.sqrt(dx * dx + dy * dy)
    ang = np.arctan2(dy, dx)

    # Ragged core: radius modulated by low-frequency angular noise.
    harmonics = rng.uniform(-1, 1, 6)
    phases = rng.uniform(0, 2 * np.pi, 6)
    edge = np.zeros_like(ang)
    for k in range(6):
        edge += harmonics[k] * np.cos((k + 2) * ang + phases[k])
    edge = 1.0 + 0.30 * edge / np.abs(harmonics).sum()
    core_r = grid * 0.215
    body = rad <= core_r * edge

    # Radial streaks: tapered rays of varied length and width.
    t = np.zeros((grid, grid), bool)
    for a0 in rng.uniform(0, 2 * np.pi, 9):
        length = core_r * rng.uniform(1.35, 2.05)
        width = rng.uniform(0.10, 0.22)
        d_ang = np.abs(np.angle(np.exp(1j * (ang - a0))))
        ray = (rad <= length) & (d_ang <= width * (1.0 - rad / (length * 1.15)))
        t |= ray
    body |= t

    # No loose sparks: single stray pixels around the burst read as noise at
    # game scale — only the solid flame body stays (gate 2026-08-27).

    # Color by distance normalized to the LOCAL ragged edge, so the red rim
    # of the native ball hugs the core's own outline (a global max radius
    # keeps the whole core flat yellow and the disc look returns).
    out = np.zeros((grid, grid, 4), np.uint8)
    local = core_r * edge * 1.06
    idx = np.clip((rad / local * len(profile)).astype(int),
                  0, len(profile) - 1)
    cols = profile[idx].astype(np.uint8)
    out[body, :3] = cols[body]
    out[body, 3] = 255

    big = np.kron(out, np.ones((upscale, upscale, 1), np.uint8))
    return big


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--lump", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--seed", type=int, default=872506309)
    a = p.parse_args()
    profile = flash_profile(a.lump)
    print("profile core->rim:")
    for row in profile:
        print("  ", row.round(0))
    burst = bake_burst(profile, seed=a.seed)
    Image.fromarray(burst, "RGBA").save(a.out)
    print(f"burst {burst.shape[1]}x{burst.shape[0]} -> {a.out}")


if __name__ == "__main__":
    main()
