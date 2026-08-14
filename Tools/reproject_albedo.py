"""Reproject one reference frame's albedo onto every other frame's mesh.

Statistical tone matching cannot stop stop-motion color flicker: TRELLIS
bakes highlights and tints onto different body parts per frame, so however
well the histograms agree, the PLACEMENT wanders (SPOS armor flicker,
2026-08-14). This kills the problem by construction: every frame samples
its texels' colors from the nearest surface point of ONE user-approved
reference frame, so all frames literally wear the same paint.

Mechanics per target frame:
  1. both meshes normalized to a common frame (feet at 0, unit height,
     centered XZ) — poses differ but body parts roughly correspond;
  2. target UV atlas rasterized at texcap resolution -> 3D position per
     texel; nearest reference surface point (KD-tree over triangle-corner
     samples) supplies the color from the reference albedo;
  3. result quantized to the shared native palette as usual;
  4. emission mask regenerated from the transferred visor blue.

Extremity smears (a hand sampling the gun) are accepted: stop-motion
frames flash for 2-8 tics and uniform paint beats wandering highlights.

Usage:
  python Tools/reproject_albedo.py --ref SPOSC1 \
      --dir Assets/Resources/ExperimentalMonsters/SPOS \
      --targets SPOSA1 SPOSB1 SPOSD1 SPOSE1 SPOSF1 SPOSG1 \
      --palette-lump SPOSA1 --texcap 256
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


def normalize(verts: np.ndarray) -> np.ndarray:
    v = verts.copy()
    v[:, 0] -= v[:, 0].mean()
    v[:, 2] -= v[:, 2].mean()
    v[:, 1] -= v[:, 1].min()
    h = max(1e-9, v[:, 1].max())
    return v / h


def sample_colors(tex: np.ndarray, uv: np.ndarray) -> np.ndarray:
    th, tw = tex.shape[:2]
    x = np.clip((uv[:, 0] * (tw - 1)).round().astype(int), 0, tw - 1)
    y = np.clip(((1 - uv[:, 1]) * (th - 1)).round().astype(int), 0, th - 1)
    return tex[y, x, :3]


def strong_blue(arr: np.ndarray) -> np.ndarray:
    r = arr[..., 0].astype(np.float64)
    g = arr[..., 1].astype(np.float64)
    b = arr[..., 2].astype(np.float64)
    return (b > 100) & (b > r * 1.35) & (b > g * 1.2)


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--ref", required=True)
    p.add_argument("--dir", required=True)
    p.add_argument("--targets", nargs="+", required=True)
    p.add_argument("--palette-lump", required=True)
    p.add_argument("--texcap", type=int, default=256)
    a = p.parse_args()

    d = Path(a.dir)
    ref_verts, ref_tris, ref_cuv = doomify3d.load_obj(d / f"{a.ref}.obj")
    ref_tex = np.asarray(Image.open(d / f"{a.ref}_albedo.png").convert("RGBA"))
    ref_norm = normalize(ref_verts)

    # Surface samples: triangle corners + centroids (positions -> colors).
    corners = ref_tris.reshape(-1)
    pts = ref_norm[corners]
    uvs = ref_cuv.reshape(-1, 2)
    cols = sample_colors(ref_tex, uvs)
    cen_pts = ref_norm[ref_tris].mean(1)
    cen_uv = ref_cuv.mean(1)
    cen_cols = sample_colors(ref_tex, cen_uv)
    pts_all = np.vstack([pts, cen_pts])
    colors = np.vstack([cols, cen_cols]).astype(np.float64)

    # Voxel-median zone smoothing (cell ~6% of height): each sample takes
    # its cell's median color, turning the reference into flat semantic
    # zones (armor/head/pants). Raw and even k-median sampling reproduced
    # C's plate highlights as black/white camo noise on other frames.
    cell = 0.03
    keys = np.floor(pts_all / cell).astype(np.int64)
    uk, inv = np.unique(keys, axis=0, return_inverse=True)
    for i in range(len(uk)):
        sel = inv == i
        colors[sel] = np.median(colors[sel], axis=0)
    tree = cKDTree(pts_all)

    # Thin features (blue visor slits) die in the voxel median — carry them
    # as a separate overlay layer sampled only from strongly-blue texels.
    src_blue = strong_blue(np.vstack([sample_colors(ref_tex, uvs),
                                      sample_colors(ref_tex, cen_uv)])
                           .reshape(-1, 1, 3)).reshape(-1)
    blue_tree = cKDTree(pts_all[src_blue]) if src_blue.any() else None
    print(f"ref {a.ref}: {len(colors)} samples, {len(uk)} zones, "
          f"{int(src_blue.sum())} visor samples")

    pal = doomify3d.sprite_palette(a.palette_lump)
    size = a.texcap

    for lump in a.targets:
        verts, tris, cuv = doomify3d.load_obj(d / f"{lump}.obj")
        vn = normalize(verts)

        # Rasterize the UV atlas: texel -> interpolated 3D position.
        pos = np.zeros((size, size, 3), np.float64)
        filled = np.zeros((size, size), bool)
        for f, (i0, i1, i2) in enumerate(tris):
            uv = cuv[f]
            xs = uv[:, 0] * (size - 1)
            ys = (1 - uv[:, 1]) * (size - 1)
            x0, x1 = int(max(0, xs.min())), int(min(size - 1, np.ceil(xs.max())))
            y0, y1 = int(max(0, ys.min())), int(min(size - 1, np.ceil(ys.max())))
            if x1 < x0 or y1 < y0:
                continue
            px, py = np.meshgrid(np.arange(x0, x1 + 1), np.arange(y0, y1 + 1))
            den = (ys[1] - ys[2]) * (xs[0] - xs[2]) + (xs[2] - xs[1]) * (ys[0] - ys[2])
            if abs(den) < 1e-12:
                continue
            w0 = ((ys[1] - ys[2]) * (px - xs[2]) + (xs[2] - xs[1]) * (py - ys[2])) / den
            w1 = ((ys[2] - ys[0]) * (px - xs[2]) + (xs[0] - xs[2]) * (py - ys[2])) / den
            w2 = 1 - w0 - w1
            pad = 0.02
            m = (w0 >= -pad) & (w1 >= -pad) & (w2 >= -pad)
            if not m.any():
                continue
            my, mx = py[m], px[m]
            p3 = (w0[m, None] * vn[i0] + w1[m, None] * vn[i1] + w2[m, None] * vn[i2])
            pos[my, mx] = p3
            filled[my, mx] = True

        idx = np.argwhere(filled)
        _, nn = tree.query(pos[filled], k=6, workers=-1)
        med = np.median(colors[nn], axis=1)
        out = np.zeros((size, size, 4), np.uint8)
        out[idx[:, 0], idx[:, 1], :3] = np.clip(med, 0, 255).astype(np.uint8)
        out[idx[:, 0], idx[:, 1], 3] = 255

        # Visor overlay: texels whose surface point sits on the reference's
        # blue slits get the vivid visor blue (and later the emission mask).
        if blue_tree is not None:
            bd, _ = blue_tree.query(pos[filled], k=1, workers=-1)
            on = bd < 0.012
            vy, vx = idx[on, 0], idx[on, 1]
            out[vy, vx, 0] = 40
            out[vy, vx, 1] = 70
            out[vy, vx, 2] = 220

        img = Image.fromarray(out)
        final = doomify3d.doomify_texture(img, pal, a.texcap)
        final.save(d / f"{lump}_albedo.png")

        blue = strong_blue(np.asarray(final.convert("RGBA")))
        Image.fromarray((blue * 255).astype(np.uint8), "L").save(
            d / f"{lump}_emission.png")
        print(f"{lump}: {filled.sum()} texels reprojected, "
              f"visor px {int(blue.sum())}")

    # Reference frame keeps its own albedo; refresh its emission mask the
    # same way so the glow is uniform across frames.
    blue = strong_blue(ref_tex)
    Image.fromarray((blue * 255).astype(np.uint8), "L").resize(
        (size, size), Image.BOX).save(d / f"{a.ref}_emission.png")
    print(f"{a.ref}: emission refreshed ({int(blue.sum())} px at source res)")


if __name__ == "__main__":
    main()
