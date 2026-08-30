"""Repaint away-facing texels of monster frames from ONE anchor frame's back.

project_hint_texture.py made the fronts of a stop-motion set consistent (one
hint set), but --back-from-bake paints each frame's back from that frame's OWN
raw TRELLIS bake — and bakes drift between generations, so the back pulses
tone and content through the walk cycle (SPOS, reported 2026-08-30: grey vs
rust vs near-black armor frame to frame). Statistics cannot fix that (the
SPOS color saga verdict), so this applies the projection doctrine to the back:
every away-facing texel of a target frame takes the color of the NEAREST
away-facing surface point of the anchor mesh, matched in normalized mesh
space (poses share the body core, so a thigh maps to the thigh, armor to
armor). Screen-space row alignment was tried first and rejected: the anchor's
own back render carries its hand/gun at torso rows, and row stretching smears
them across every other pose. Raw bakes are not needed — meshes, UVs and the
installed albedos carry everything, and colors come straight from the
anchor's quantized albedo, so no re-quantization blur.

Usage:
  python Tools/repaint_back_from_anchor.py \
      --dir Assets/Resources/ExperimentalMonsters/SPOS \
      --anchor SPOSA1 --frames SPOSB1 SPOSC1 SPOSD1 SPOSE1 SPOSF1 SPOSG1 \
      --palette-lump SPOSA1
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy.spatial import cKDTree

sys.path.insert(0, str(Path(__file__).resolve().parent))
import doomify3d  # noqa: E402
from project_hint_texture import rasterize_positions  # noqa: E402
from recolor_to_hint import strong_blue  # noqa: E402

PROJ = 1024


def frame_cloud(d: Path, lump: str, grid: int, eps: float):
    """Rasterize a frame's atlas: per-1024-texel normalized position plus the
    REAR-SHELL classification. The plain occluded-texel split from
    project_hint_texture is wrong for this tool on both sides: the anchor's
    occluded set carries FRONT surfaces (chest behind the gun, visor under
    the brim) that poison the color cloud with black/blue at near-back
    positions, and repainting a target's occluded-front texels would show
    through from the front at parallax angles. The surface a viewer behind
    the monster actually sees is the depth MINIMUM per (x, y) cell — front
    is +Z (settled 2026-08-15) — so both the cloud and the repaint set are
    texels within eps of their cell's minimum."""
    verts, tris, cuv = doomify3d.load_obj(d / f"{lump}.obj")
    v = verts.copy()
    y_min, y_max = v[:, 1].min(), v[:, 1].max()
    h = max(1e-9, y_max - y_min)
    v /= h
    v[:, 1] -= y_min / h
    pos, _, filled = rasterize_positions(v, tris, cuv, PROJ)
    pts = pos[filled]
    # Center x/z on the surface MEDIAN, not the bbox: a stride leg or an
    # outstretched gun inflates the bbox on one side (SPOSB1 z-span 0.94 vs
    # the set's ~0.65) and shifts every body texel relative to the anchor's.
    # The torso dominates the texel count, so the median pins body-to-body.
    pts[:, 0] -= np.median(pts[:, 0])
    pts[:, 2] -= np.median(pts[:, 2])

    depth = pts[:, 2]
    gx = ((pts[:, 0] - pts[:, 0].min()) /
          max(1e-9, np.ptp(pts[:, 0])) * (grid - 1)).round().astype(np.int64)
    gy = (pts[:, 1] * (grid - 1)).round().astype(np.int64)
    _, inv = np.unique(gy * grid + gx, return_inverse=True)
    cell_min = np.full(inv.max() + 1, np.inf)
    np.minimum.at(cell_min, inv, depth)
    cell_max = np.full(inv.max() + 1, -np.inf)
    np.maximum.at(cell_max, inv, depth)
    # eps is ABSOLUTE in height-normalized units: a span-relative threshold
    # breathes with the pose (the stride frame's z-span is 1.5x the stand's).
    # No front-half cap: a cell's depth minimum IS what a viewer behind sees,
    # including the rear side of a forward leg or the gun's underside.
    shell = (depth - cell_min[inv]) <= eps
    # Everything at least eps behind the front surface is what the hint
    # projection painted from the per-frame bake — the whole flicker source.
    hidden = (cell_max[inv] - depth) > eps
    return pts, filled, shell, hidden


def yaw_rot(deg: float) -> np.ndarray:
    r = np.radians(deg)
    return np.array([[np.cos(r), 0, np.sin(r)], [0, 1, 0],
                     [-np.sin(r), 0, np.cos(r)]])


def rotate_centered(pts: np.ndarray, back: np.ndarray, deg: float):
    """Rotate the FULL surface cloud and re-center by the FULL surface
    median, then cut the back subset: centering by the back subset's own
    median pushes the whole query forward (the subset lives at negative z)
    and mismatches every frame, even a perfectly aligned one."""
    r = pts @ yaw_rot(deg).T
    r[:, 0] -= np.median(r[:, 0])
    r[:, 2] -= np.median(r[:, 2])
    return r[back]


def align_yaw(pts: np.ndarray, back: np.ndarray, tree: cKDTree,
              w: np.ndarray) -> float:
    """Frames carry REAL yaw offsets against the anchor — the fire pose is
    baked at -40 so the muzzle faces the target, and walk poses twist the
    shoulders — so the color lookup must match bodies in the anchor's
    orientation (the classification stays in display space, where front is
    +Z for every mesh). Minimize mean NN distance over a coarse+fine sweep."""
    def score(deg):
        q = rotate_centered(pts, back, deg)[::7]
        dd, _ = tree.query(q * w, workers=-1)
        return float(dd.mean())

    coarse = min(range(-60, 65, 5), key=score)
    return min(range(coarse - 4, coarse + 5), key=score)


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--dir", required=True)
    p.add_argument("--anchor", required=True)
    p.add_argument("--frames", nargs="+", required=True)
    p.add_argument("--palette-lump", required=True)
    p.add_argument("--knn", type=int, default=32,
                   help="median over this many anchor neighbors: a lone "
                        "crevice/flap texel must not win a misaligned query "
                        "(poses differ, mean NN miss ~0.06 body heights)")
    p.add_argument("--texcap", type=int, default=256)
    p.add_argument("--back-depth-eps", type=float, default=0.05,
                   help="rear-shell thickness in height-normalized units")
    p.add_argument("--back-grid", type=int, default=256)
    # Poses differ mostly in depth (a swung leg travels along z), so distance
    # weighs z under x/y: the lookup should stay on the same body part, not
    # jump to whatever limb happens to pass nearby.
    p.add_argument("--z-weight", type=float, default=0.5)
    a = p.parse_args()

    d = Path(a.dir)
    pal = doomify3d.sprite_palette(a.palette_lump)
    size = a.texcap
    k = PROJ // size

    apts, afilled, aback, _ = frame_cloud(d, a.anchor, a.back_grid,
                                          a.back_depth_eps)
    aidx = np.argwhere(afilled)[aback]
    alb_a = np.asarray(Image.open(
        d / f"{a.anchor}_albedo.png").convert("RGB"))
    acols = alb_a[aidx[:, 0] // k, aidx[:, 1] // k]
    cloud = apts[aback] * np.array([1.0, 1.0, a.z_weight])
    tree = cKDTree(cloud)
    print(f"{a.anchor}: anchor back cloud {len(cloud)} texels")

    for lump in a.frames:
        pts, filled, shell, hidden = frame_cloud(d, lump, a.back_grid,
                                                 a.back_depth_eps)
        # Repaint the rear shell AND every hidden texel: both were painted
        # from the per-frame bake, so both carry the frame-to-frame drift.
        # Only the front shell (the hint projection) stays untouched.
        back = shell | hidden
        w = np.array([1.0, 1.0, a.z_weight])
        yaw = align_yaw(pts, back, tree, w)
        q = rotate_centered(pts, back, yaw) * w
        dist, nn = tree.query(q, k=a.knn, workers=-1)
        med = np.median(acols[nn].astype(np.float64), axis=1)
        newcols = doomify3d.quantize(
            np.clip(med, 0, 255).astype(np.uint8)[None, ...], pal)[0]
        idx_all = np.argwhere(filled)

        # Decide per atlas texel of the shipped texcap resolution: replace a
        # texel only when most of its 4x4 sub-texels are away-facing, and give
        # it the MODE of their looked-up palette colors (palette colors do not
        # average).
        sub = np.zeros((PROJ, PROJ, 3), np.int32)
        hit = np.zeros((PROJ, PROJ), bool)
        bidx = idx_all[back]
        sub[bidx[:, 0], bidx[:, 1]] = newcols
        hit[bidx[:, 0], bidx[:, 1]] = True

        hit4 = hit.reshape(size, k, size, k)
        cov = hit4.mean((1, 3))
        take = cov > 0.5
        sub4 = sub.reshape(size, k, size, k, 3)

        alb_path = d / f"{lump}_albedo.png"
        alb = np.asarray(Image.open(alb_path).convert("RGBA")).copy()
        ty, tx = np.where(take)
        for yy, xx in zip(ty, tx):
            block = sub4[yy, :, xx, :, :][hit4[yy, :, xx, :]]
            packed = (block[:, 0].astype(np.int64) << 16) | \
                (block[:, 1].astype(np.int64) << 8) | block[:, 2]
            vals, counts = np.unique(packed, return_counts=True)
            c = int(vals[np.argmax(counts)])
            alb[yy, xx, :3] = (c >> 16, (c >> 8) & 255, c & 255)

        blue = int((strong_blue(alb) & take).sum())
        Image.fromarray(alb).save(alb_path)
        print(f"{lump}: back {100 * back.mean():.0f}%, yaw {yaw:+d}, "
              f"repainted {int(take.sum())}/{size * size} texels, mean NN "
              f"dist {dist.mean():.4f}, blue on back {blue}")


if __name__ == "__main__":
    main()
