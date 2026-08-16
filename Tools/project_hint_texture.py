"""Texture monster frame meshes by projecting their own v2 hint renders.

TRELLIS bakes a NEW texture every generation, so stop-motion frames flicker
however the tones are matched afterwards. The v2 hint redraws, in contrast,
are one consistent set (same character, same colors, per-frame poses) — and
TRELLIS matched each mesh's silhouette to its hint. So the hint IS the
texture: every atlas texel takes the hint pixel at its projected front-view
(x, y). Hidden/back surfaces would inherit the frontmost color at that (x, y),
which paints the spine with the chest — pass --back-from-bake to take those
away-facing texels from TRELLIS' own (geometry-correct) bake instead.

Usage:
  python Tools/project_hint_texture.py --palette-lump SPOSA1 --texcap 256 \
      --dir Assets/Resources/ExperimentalMonsters/SPOS \
      --frames SPOSA1=Textures/Trellis2/ShapeHints/3D/SPOSA1-depth-shapehint-v2.png ...
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
    nrm = np.zeros((size, size, 3), np.float64)
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
        n = np.cross(verts[i1] - verts[i0], verts[i2] - verts[i0])
        ln = np.linalg.norm(n)
        if ln > 1e-12:
            nrm[my, mx] = n / ln
        filled[my, mx] = True
    return pos, nrm, filled


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--dir", required=True)
    p.add_argument("--frames", nargs="+", required=True,
                   help="LUMP=path/to/hint.png pairs")
    p.add_argument("--palette-lump", required=True)
    p.add_argument("--texcap", type=int, default=256)
    # SPOS-specific: its design has no skin below the waist, so skin-like
    # samples there are projection leaks. Monsters that ARE skin all over
    # (TROO imp) must not pass this flag.
    p.add_argument("--lower-skin-fix", action="store_true")
    # TROO imp: golden belly plates are a FRONTAL feature; side-facing arm
    # and torso-rim surfaces picking them up read as yellow leak spots.
    p.add_argument("--side-accent-fix", action="store_true")
    # A frontal projection has nothing to say about the back, so the rear of
    # the body inherits the front image: chest wounds, belt buckle and visor
    # all reappear behind, and the monster reads as facing you from every
    # angle (POSS, 2026-08-15). TRELLIS did generate a real back, so take
    # away-facing texels from its bake and keep the hint for what faces us.
    p.add_argument("--back-from-bake", default=None,
                   help="dir with <lump>-stage/<lump>_albedo.png raw TRELLIS "
                        "bakes (the raw atlas survives decimation)")
    p.add_argument("--back-depth-eps", type=float, default=0.06,
                   help="cell-relative depth gap that counts as 'behind'")
    p.add_argument("--back-grid", type=int, default=256,
                   help="mesh-space occlusion grid; 256 leaves ~16 texels per "
                        "cell on a 40k-tri frame (128 over-marks thin limbs, "
                        "512 leaves rear texels alone in a cell again)")
    p.add_argument("--dump-back-mask", action="store_true",
                   help="also write <lump>_backmask.png (white = from bake); "
                        "render it as an albedo to see the split on the mesh")
    # Head region keeps the (palette-quantized) TRELLIS bake: projection
    # smears the crest across the whole skull top, which reads as a second
    # head flickering during the walk cycle. The bake's head is geometry-
    # aligned (eyes on the actual face). Value = y_rel threshold.
    p.add_argument("--head-from-bake", type=float, default=None)
    p.add_argument("--bake-dir", default=None,
                   help="dir with <lump>/<lump>_albedo.png doomify outputs")
    p.add_argument("--raw-bake-dir", default=None,
                   help="dir with <lump>-stage/<lump>_albedo.png raw TRELLIS bakes")
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
        pos, nrm, filled = rasterize_positions(v, tris, cuv, proj)
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
        if a.side_accent_fix:
            # Suppress warm-gold accents on side-facing surfaces (|nz| small:
            # neither the frontal belly nor the mirrored back), replacing with
            # the row's median non-gold body color.
            nz = nrm[filled][:, 2]
            cf = cols.astype(np.float64)
            gold = (cf[:, 0] > 130) & (cf[:, 1] > 95) & \
                   (cf[:, 0] > cf[:, 2] + 50)
            hfj = hint[..., :3].astype(np.float64)
            gold_px = (hfj[..., 0] > 130) & (hfj[..., 1] > 95) & \
                      (hfj[..., 0] > hfj[..., 2] + 50)
            body_med = np.median(hint[..., :3][fg & ~gold_px], axis=0)
            row_body = np.tile(body_med, (hint.shape[0], 1))
            for row in np.unique(wi2):
                sel = fg[row] & ~gold_px[row]
                if sel.sum() >= 8:
                    row_body[row] = np.median(hint[row, sel, :3], axis=0)
            # Outward normals verified via signed volume: gold survives only
            # on genuinely front-facing surfaces — sides AND the mirrored
            # back both leaked at grazing angles (2026-08-14).
            fixg = gold & (nz < 0.35)
            cols[fixg] = np.clip(row_body[wi2[fixg]], 0, 255).astype(np.uint8)

        if a.lower_skin_fix:
            y_rel = (pts[:, 1] - y_min) / max(1e-9, y_max - y_min)
            cf = cols.astype(np.float64)
            skin_hit = (cf[:, 0] > 110) & (cf[:, 0] > cf[:, 2] + 8)
            low = y_rel < 0.45
            fix = skin_hit & low
            cols[fix] = np.clip(row_med[wi2[fix]], 0, 255).astype(np.uint8)
            # Residual light warm-grays on boots still quantize to palette
            # pink: nothing below the waist is brighter than dark boot
            # highlights, so cap lower-body luminance outright.
            cf = cols.astype(np.float64)
            lum = cf.mean(1)
            hot = low & (lum > 120)
            cols[hot] = np.clip(cf[hot] * (120.0 / lum[hot])[:, None],
                                0, 255).astype(np.uint8)

        back_map = None
        if a.back_from_bake:
            bake_img = Image.open(
                Path(a.back_from_bake) / f"{lump}-stage" /
                f"{lump}_albedo.png").convert("RGB")
            if bake_img.size != (proj, proj):
                bake_img = bake_img.resize((proj, proj), Image.NEAREST)
            bake_arr = np.asarray(bake_img)
            idx_all = np.argwhere(filled)
            # Triangle normals are unreliable after decimation (measured mean
            # nz +0.04 front vs -0.02 back — no separation), so classify by
            # DEPTH: within one projected hint pixel the nearest surface is the
            # one the hint describes, everything behind it is the back.
            #
            # Which way is near was settled 2026-08-15 by rendering a mesh with
            # its own geometry-correct TRELLIS bake from both sides: yaw 0 in
            # doomify3d.render (which keeps the LARGER z) shows the visor, the
            # pistol and the chest wounds, yaw 180 a clean back — and the hint
            # maps to that same view (x straight through). So FRONT IS +Z.
            # The earlier experiment assumed -Z, i.e. it repainted the face
            # with the back and kept the leak it was meant to remove.
            depth = pts[:, 2]
            # Bin in MESH space, not in hint pixels: the row-by-row alignment
            # stretches the front and the back of a row differently, so 22% of
            # rear texels landed in a hint cell of their own with no front
            # surface to be occluded by — that is how a blue strap survived on
            # the back of POSS' helmet after the sign was already fixed.
            g = a.back_grid
            gx = ((pts[:, 0] - pts[:, 0].min()) /
                  max(1e-9, np.ptp(pts[:, 0])) * (g - 1)).round().astype(np.int64)
            gy = ((pts[:, 1] - pts[:, 1].min()) /
                  max(1e-9, np.ptp(pts[:, 1])) * (g - 1)).round().astype(np.int64)
            _, inv = np.unique(gy * g + gx, return_inverse=True)
            cell_max = np.full(inv.max() + 1, -np.inf)
            np.maximum.at(cell_max, inv, depth)
            near = cell_max[inv]
            span = max(1e-6, depth.max() - depth.min())
            back = (near - depth) > a.back_depth_eps * span
            cols[back] = bake_arr[idx_all[back, 0], idx_all[back, 1]]
            back_map = np.zeros((proj, proj), bool)
            back_map[idx_all[back, 0], idx_all[back, 1]] = True
            print(f"  {lump}: back texels from bake "
                  f"{100 * back.mean():.0f}%", flush=True)

        idx = np.argwhere(filled)
        out = np.zeros((proj, proj, 4), np.uint8)
        out[idx[:, 0], idx[:, 1], :3] = cols
        out[idx[:, 0], idx[:, 1], 3] = 255

        final = doomify3d.doomify_texture(Image.fromarray(out), pal, size)

        if a.head_from_bake is not None and a.bake_dir:
            y_rel_map = np.zeros((proj, proj), np.float64)
            y_rel_map[filled] = (pts[:, 1] - y_min) / max(1e-9, y_max - y_min)
            head1024 = (y_rel_map >= a.head_from_bake) & filled
            head256 = np.asarray(Image.fromarray(
                (head1024 * 255).astype(np.uint8), "L").resize(
                    (size, size), Image.BOX)) > 127
            bake = np.asarray(Image.open(
                Path(a.bake_dir) / lump / f"{lump}_albedo.png").convert("RGBA"))
            fin = np.asarray(final.convert("RGBA")).copy()
            fin[head256] = bake[head256]
            # Eye boost: the bake's yellow eyes are a few texels and die in
            # quantization, leaving the face unreadable (the imp was reported
            # as «walking backwards»). Find frontal bright-yellow head texels
            # in the raw bake at 1024 and repaint them saturated at 256.
            if a.raw_bake_dir:
                raw_bake = np.asarray(Image.open(
                    Path(a.raw_bake_dir) / f"{lump}-stage" / f"{lump}_albedo.png"
                ).convert("RGB")).astype(np.float64)
                if raw_bake.shape[0] != proj:
                    raw_bake = np.asarray(Image.fromarray(
                        raw_bake.astype(np.uint8)).resize(
                            (proj, proj), Image.BOX)).astype(np.float64)
                nz_map = np.zeros((proj, proj), np.float64)
                nz_map[filled] = nrm[filled][:, 2]
                eye1024 = head1024 & (nz_map > 0.15) & \
                    (raw_bake[..., 0] > 135) & (raw_bake[..., 1] > 100) & \
                    (raw_bake[..., 2] < 120) & \
                    (raw_bake[..., 1] > raw_bake[..., 2] + 20)
                eye256 = np.asarray(Image.fromarray(
                    (eye1024 * 255).astype(np.uint8), "L").resize(
                        (size, size), Image.BOX)) > 16
                fin[eye256] = (255, 216, 48, 255)
            final = Image.fromarray(fin)

        back256 = None
        if back_map is not None:
            back256 = np.asarray(Image.fromarray(
                (back_map * 255).astype(np.uint8), "L").resize(
                    (size, size), Image.BOX)) > 127
            # TRELLIS wraps the visor band around the back of the helmet as a
            # dark navy strap, and quantization to the sprite palette pulls it
            # up to the visor's own bright blue — so the back of the head lit
            # up again even with the projection fixed. Darken those texels and
            # re-snap: the strap survives, the accent does not. Checked after
            # quantization on purpose; the raw navy is too dark to detect.
            fin = np.asarray(final.convert("RGBA")).copy()
            bad = strong_blue(fin) & back256
            if bad.any():
                dim = (fin[bad].astype(np.float64) * np.array([0.4, 0.4, 0.5, 1]))
                fin[bad] = doomify3d.quantize(
                    dim.astype(np.uint8)[None, ...], pal)[0]
                final = Image.fromarray(fin)
            print(f"  {lump}: back-blue texels re-snapped {int(bad.sum())}")
            if a.dump_back_mask:
                Image.fromarray(
                    np.where(back256, 255, 40).astype(np.uint8), "L").convert(
                        "RGB").save(d / f"{lump}_backmask.png")

        final.save(d / f"{lump}_albedo.png")
        blue = strong_blue(np.asarray(final.convert("RGBA")))
        if back256 is not None:
            # The steady glow belongs to the visor, so it may only light
            # surfaces that face the player; anything classified as back keeps
            # its color but never emits (a glowing skull is what sent the
            # 2026-08-15 POSS build back).
            blue &= ~back256
        Image.fromarray((blue * 255).astype(np.uint8), "L").save(
            d / f"{lump}_emission.png")
        print(f"{lump}: {int(filled.sum())} texels, visor px {int(blue.sum())}")


if __name__ == "__main__":
    main()
