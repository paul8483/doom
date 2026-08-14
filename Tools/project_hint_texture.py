"""Texture monster frame meshes by projecting their own v2 hint renders.

TRELLIS bakes a NEW texture every generation, so stop-motion frames flicker
however the tones are matched afterwards. The v2 hint redraws, in contrast,
are one consistent set (same character, same colors, per-frame poses) — and
TRELLIS matched each mesh's silhouette to its hint. So the hint IS the
texture: every atlas texel takes the hint pixel at its projected front-view
(x, y). Hidden/back surfaces inherit the frontmost color at that (x, y) —
the usual single-view projection trade, fine at DOOM scales.

Usage:
  python Tools/project_hint_texture.py --palette-lump SPOSA1 --texcap 256 \
      --dir Assets/Resources/ExperimentalMonsters/SPOS \
      --frames SPOSA1=Textures/Trellis2/ShapeHints/SPOSA1-depth-shapehint-v2.png ...
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

sys.path.insert(0, str(Path(__file__).resolve().parent))
import doomify3d  # noqa: E402
from recolor_to_hint import hint_foreground, strong_blue  # noqa: E402
from tone_match_albedo import armor_mask  # noqa: E402


def rasterize_positions(verts, tris, cuv, size):
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
        m = (w0 >= -0.02) & (w1 >= -0.02) & (w2 >= -0.02)
        if not m.any():
            continue
        my, mx = py[m], px[m]
        p3 = (w0[m, None] * verts[i0] + w1[m, None] * verts[i1]
              + w2[m, None] * verts[i2])
        pos[my, mx] = p3
        filled[my, mx] = True
    return pos, filled


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--dir", required=True)
    p.add_argument("--frames", nargs="+", required=True,
                   help="LUMP=path/to/hint.png pairs")
    p.add_argument("--palette-lump", required=True)
    p.add_argument("--texcap", type=int, default=256)
    a = p.parse_args()

    d = Path(a.dir)
    pal = doomify3d.sprite_palette(a.palette_lump)
    size = a.texcap

    for pair in a.frames:
        lump, hint_path = pair.split("=", 1)
        hint = np.asarray(Image.open(hint_path).convert("RGBA"))
        fg = hint_foreground(hint)
        # Checker removal leaves speckle at tile boundaries; the character
        # is the largest connected component (else the bbox spans the whole
        # canvas and the projection mapping collapses).
        labels, n = ndimage.label(fg)
        if n > 1:
            sizes = ndimage.sum(fg, labels, range(1, n + 1))
            fg = labels == (1 + int(np.argmax(sizes)))
        # Cut the anti-aliased rim: edge pixels blend with the checkerboard
        # and quantize to palette skin-pink (pink fringes, 2026-08-14).
        fg = ndimage.binary_erosion(fg, iterations=2)
        ys, xs = np.where(fg)
        hx0, hx1 = xs.min(), xs.max()
        hy0, hy1 = ys.min(), ys.max()
        # Nearest-foreground lookup for texels that project just outside
        # the silhouette (mesh vs hint edge mismatch).
        _, (iy, ix) = ndimage.distance_transform_edt(
            ~fg, return_indices=True)

        verts, tris, cuv = doomify3d.load_obj(d / f"{lump}.obj")
        v = verts.copy()
        v[:, 0] -= (v[:, 0].min() + v[:, 0].max()) / 2
        y_min, y_max = v[:, 1].min(), v[:, 1].max()
        x_half = max(1e-9, np.abs(v[:, 0]).max())

        # Project at the atlas' native resolution: TRELLIS atlases are
        # thousands of micro-islands, and at texcap resolution multiple
        # triangles contend for one texel (hand colors landing on shins).
        # The BOX cap down to texcap happens in doomify_texture as usual.
        proj = 1024
        pos, filled = rasterize_positions(v, tris, cuv, proj)
        pts = pos[filled]
        # Row-by-row silhouette alignment: TRELLIS geometry is not
        # pixel-aligned with the hint, so a plain bbox mapping leaves ~25%
        # of texels outside the silhouette (they then snapped to hand skin —
        # the pink offset-shadow, 2026-08-14). Mapping each mesh row's x-span
        # onto the hint row's fg span keeps every texel inside by
        # construction.
        w = hy1 - (pts[:, 1] - y_min) / max(1e-9, y_max - y_min) * (hy1 - hy0)
        wi = np.clip(w.round().astype(int), 0, hint.shape[0] - 1)
        rows_any = fg.any(1)
        row_ids = np.where(rows_any)[0]
        nearest_row = row_ids[np.clip(
            np.searchsorted(row_ids, np.arange(fg.shape[0])), 0,
            len(row_ids) - 1)]
        wi2 = nearest_row[wi]
        cols_idx = np.arange(fg.shape[1])
        rmax = np.where(fg, cols_idx[None, :], -1).max(1)
        rmin = np.where(fg, cols_idx[None, :], fg.shape[1] + 1).min(1)

        n_rows = hint.shape[0]
        mesh_min = np.full(n_rows, np.inf)
        mesh_max = np.full(n_rows, -np.inf)
        np.minimum.at(mesh_min, wi2, pts[:, 0])
        np.maximum.at(mesh_max, wi2, pts[:, 0])
        span = np.maximum(mesh_max - mesh_min, 1e-9)
        rel = (pts[:, 0] - mesh_min[wi2]) / span[wi2]
        ui2 = (rmin[wi2] + rel * (rmax[wi2] - rmin[wi2])).round().astype(int)
        ui2 = np.clip(ui2, 0, hint.shape[1] - 1)
        # Interior gaps (between the legs): nearest silhouette pixel.
        miss = ~fg[wi2, ui2]
        my, mx = wi2[miss], ui2[miss]
        ui2 = ui2.copy(); wi2 = wi2.copy()
        ui2[miss] = ix[my, mx]
        wi2[miss] = iy[my, mx]
        cols = hint[wi2, ui2, :3].copy()
        # No skin exists below the waist in the hint design (legs are olive
        # fatigues + black boots), but inner-leg texels land on the HANDS
        # holding the gun in the same rows — from behind that painted whole
        # pink legs (2026-08-14). Any skin-like sample on the lower body is
        # replaced by its row's median non-skin color.
        am = armor_mask(hint) & fg
        armor_med = np.median(hint[..., :3][am], axis=0)
        hf = hint[..., :3].astype(np.float64)
        skin_px = (hf[..., 0] > 110) & (hf[..., 0] > hf[..., 2] + 15)
        row_med = np.tile(armor_med, (hint.shape[0], 1))
        for row in np.unique(wi2):
            sel = fg[row] & ~skin_px[row]
            if sel.sum() >= 8:
                row_med[row] = np.median(hint[row, sel, :3], axis=0)
        y_rel = (pts[:, 1] - y_min) / max(1e-9, y_max - y_min)
        cf = cols.astype(np.float64)
        skin_hit = (cf[:, 0] > 110) & (cf[:, 0] > cf[:, 2] + 8)
        low = y_rel < 0.45
        fix = skin_hit & low
        cols[fix] = np.clip(row_med[wi2[fix]], 0, 255).astype(np.uint8)
        # Residual light warm-grays on boots still quantize to palette pink:
        # nothing below the waist is brighter than dark boot highlights, so
        # cap lower-body luminance outright.
        cf = cols.astype(np.float64)
        lum = cf.mean(1)
        hot = low & (lum > 120)
        cols[hot] = np.clip(cf[hot] * (120.0 / lum[hot])[:, None],
                            0, 255).astype(np.uint8)

        idx = np.argwhere(filled)
        out = np.zeros((proj, proj, 4), np.uint8)
        out[idx[:, 0], idx[:, 1], :3] = cols
        out[idx[:, 0], idx[:, 1], 3] = 255

        final = doomify3d.doomify_texture(Image.fromarray(out), pal, size)
        final.save(d / f"{lump}_albedo.png")
        blue = strong_blue(np.asarray(final.convert("RGBA")))
        Image.fromarray((blue * 255).astype(np.uint8), "L").save(
            d / f"{lump}_emission.png")
        print(f"{lump}: {int(filled.sum())} texels, visor px {int(blue.sum())}")


if __name__ == "__main__":
    main()
