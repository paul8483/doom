"""Repack a doomified mesh's UV atlas with xatlas and re-bake its albedo.

Why: TRELLIS.2 bakes fragment a curved surface into thousands of UV islands
packed edge to edge (BON1A0: 2830 islands on a 512 atlas). Unity's mip
chain box-averages across island borders, so at the mip levels a ~150 px
pickup samples, glass texels mix with the strap metal packed next to them
in the atlas — pale/dark specks along every triangle border that no
texel-space filter can reach, because in the atlas those neighbours are
legitimate texels of another island (2026-09-02). Repacking with a few
large charts and real padding between them makes every mip average an
island with itself.

What it does (geometry untouched — positions, triangle count and order
stay; only the UVs and the PNGs change, so runtime height rules and the
asset tests see the same mesh):
  1. xatlas.parametrize on the mesh (charts by normal similarity, padding);
  2. the new albedo is re-baked by projection: every texel of the new atlas
     finds its triangle (same face index as the old mesh) and barycentric
     weights, maps them to the old per-corner UVs and samples the old
     albedo (nearest);
  3. padding takes the nearest chart's colour (EDT), so mips stay clean;
  4. the OBJ is rewritten with the new vt/f (xatlas may split vertices at
     chart seams; positions are copied through vmapping).
The emission mask is NOT carried over — regenerate it with the asset's
make_*_emission.py on the repacked OBJ + albedo (zones come from mesh
height and colour, so the recipe is atlas-independent).

Usage:
  python Tools/repack_uv_atlas.py Assets/Resources/ExperimentalPickups/BON1A0/BON1A0.obj \
      --albedo BON1A0_albedo.png --out-dir DIR [--resolution 512] [--padding 8]
"""
import argparse
import sys
from pathlib import Path

import numpy as np
import xatlas
from PIL import Image, ImageDraw
from scipy import ndimage

sys.path.insert(0, str(Path(__file__).resolve().parent))
import doomify3d  # noqa: E402


def read_obj_raw(path: Path):
    """Positions, per-corner UVs (F,3,2) and position indices (F,3), plus
    the mtllib/usemtl lines to carry over."""
    vs, vts, faces, fuv, header = [], [], [], [], []
    for line in path.read_text().splitlines():
        if line.startswith("v "):
            vs.append([float(x) for x in line.split()[1:4]])
        elif line.startswith("vt "):
            p = line.split()
            vts.append([float(p[1]), float(p[2])])
        elif line.startswith("f "):
            vi, ti = [], []
            for tok in line.split()[1:4]:
                parts = tok.split("/")
                vi.append(int(parts[0]) - 1)
                ti.append(int(parts[1]) - 1 if len(parts) > 1 and parts[1] else -1)
            faces.append(vi)
            fuv.append(ti)
        elif line.startswith("mtllib") or line.startswith("usemtl"):
            header.append(line)
    vs = np.asarray(vs, np.float64)
    vts = np.asarray(vts, np.float64)
    faces = np.asarray(faces, np.int64)
    fuv = np.asarray(fuv, np.int64)
    cuv = vts[fuv]  # (F,3,2)
    return vs, faces, cuv, header


def rasterize_ids(cuv: np.ndarray, w: int, h: int) -> np.ndarray:
    img = Image.new("I", (w, h), -1)
    d = ImageDraw.Draw(img)
    for i, tri in enumerate(cuv):
        d.polygon([(u * (w - 1), (1.0 - v) * (h - 1)) for u, v in tri], fill=i)
    return np.asarray(img)


def bake_by_projection(old_rgba: np.ndarray, old_cuv: np.ndarray,
                       new_cuv: np.ndarray, w: int, h: int, supersample: int = 2,
                       order: np.ndarray = None):
    """For every new-atlas texel covered by a triangle, sample the old albedo
    at the corresponding old UV. Supersampled so thin triangles still get a
    texel; uncovered texels are filled from the nearest covered one."""
    oh, ow = old_rgba.shape[:2]
    W, H = w * supersample, h * supersample
    if order is None:
        ids = rasterize_ids(new_cuv, W, H)
    else:
        # draw innermost first so the outermost face owns each texel
        raster = rasterize_ids(new_cuv[order], W, H)
        ids = np.where(raster >= 0, order[np.maximum(raster, 0)], -1)
    out = np.zeros((H, W, 4), np.float64)
    hit = ids >= 0
    ys, xs = np.nonzero(hit)
    tri = ids[ys, xs]
    # barycentric in new UV space
    p = np.stack([(xs + 0.5) / W, 1.0 - (ys + 0.5) / H], 1)
    a, b, c = new_cuv[tri, 0], new_cuv[tri, 1], new_cuv[tri, 2]
    v0, v1, v2 = b - a, c - a, p - a
    d00 = (v0 * v0).sum(1); d01 = (v0 * v1).sum(1); d11 = (v1 * v1).sum(1)
    d20 = (v2 * v0).sum(1); d21 = (v2 * v1).sum(1)
    den = d00 * d11 - d01 * d01
    den = np.where(np.abs(den) < 1e-18, 1e-18, den)
    wb = (d11 * d20 - d01 * d21) / den
    wc = (d00 * d21 - d01 * d20) / den
    wa = 1.0 - wb - wc
    # clamp slightly outside (polygon raster inclusivity)
    wa, wb, wc = np.clip(wa, 0, 1), np.clip(wb, 0, 1), np.clip(wc, 0, 1)
    s = wa + wb + wc
    wa, wb, wc = wa / s, wb / s, wc / s
    ouv = old_cuv[tri, 0] * wa[:, None] + old_cuv[tri, 1] * wb[:, None] + old_cuv[tri, 2] * wc[:, None]
    ox = np.clip((ouv[:, 0] * (ow - 1)).round().astype(int), 0, ow - 1)
    oy = np.clip(((1.0 - ouv[:, 1]) * (oh - 1)).round().astype(int), 0, oh - 1)
    out[ys, xs] = old_rgba[oy, ox]
    # downsample: average covered samples only
    cov = hit.astype(np.float64)
    def box(arr):
        return arr.reshape(h, supersample, w, supersample).mean((1, 3))
    covd = box(cov)
    rgba = np.stack([box(out[..., k] * cov) for k in range(4)], -1)
    rgba = rgba / np.maximum(covd[..., None], 1e-9)
    covered = covd > 0
    # padding: nearest covered texel
    _, (iy, ix) = ndimage.distance_transform_edt(~covered, return_indices=True)
    rgba = rgba[iy, ix]
    rgba[..., 3] = 255
    return rgba.round().clip(0, 255).astype(np.uint8), covered


def cube_unwrap(positions: np.ndarray, faces: np.ndarray, resolution: int, padding: int,
                layers: int = 3):
    """Six planar charts by dominant face normal, with projection layers.

    A voxel-derived surface (TRELLIS.2) has axis-aligned face normals, so
    xatlas' normal-deviation charting shatters it (BON1A0: 9186 charts); the
    cube projection assigns each triangle to one of six charts. Faces that
    lose the depth test of their chart (the inner voxel shell, the glass
    strip beside a strap's side wall) are re-projected into the next layer,
    so every face owns its own texels. Layout: layer 0 = 3x2 large cells
    over the top 2/3 of the atlas; layers 1-2 share the bottom strip as two
    rows of six half-size cells (their faces are hidden or oblique).
    Returns per-corner UVs (F,3,2), chart id per face, draw order
    (innermost first) and the number of faces per layer."""
    tri = positions[faces]
    n = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0])
    axis = np.abs(n).argmax(1)
    sign = np.sign(n[np.arange(len(n)), axis])
    sign[sign == 0] = 1
    chart = axis * 2 + (sign < 0)
    centroid = tri.mean(1)
    depth = centroid[np.arange(len(faces)), axis] * sign

    def planar(cid, p):
        ax = cid // 2
        neg = cid % 2 == 1
        if ax == 0:
            u, v = p[..., 2], p[..., 1]
        elif ax == 1:
            u, v = p[..., 0], p[..., 2]
        else:
            u, v = p[..., 0], p[..., 1]
        if neg:
            u = -u
        return u, v

    big = resolution // 3
    top_h = big * 2
    small_w = resolution // 6
    small_h = (resolution - top_h) // max(1, layers - 1) if layers > 1 else 0

    def cell(layer, cid):
        if layer == 0:
            return (cid % 3) * big, (cid // 3) * big, big, big
        return cid * small_w, top_h + (layer - 1) * small_h, small_w, small_h

    cuv = np.zeros((len(faces), 3, 2))
    remaining = np.ones(len(faces), bool)
    per_layer = []
    for layer in range(layers):
        placed = np.zeros(len(faces), bool)
        for cid in range(6):
            sel = np.nonzero(remaining & (chart == cid))[0]
            if len(sel) == 0:
                continue
            u, v = planar(cid, tri[sel])
            umin, vmin = u.min(), v.min()
            span = max(u.max() - umin, v.max() - vmin, 1e-9)
            cx, cy, cw, ch = cell(layer, cid)
            scale = min(cw - 2 * padding, ch - 2 * padding) / span
            cuv[sel, :, 0] = (cx + padding + (u - umin) * scale) / resolution
            cuv[sel, :, 1] = (cy + padding + (v - vmin) * scale) / resolution
            if layer == layers - 1:
                placed[sel] = True
                continue
            # depth test at 2x: a face keeps this layer if it owns its
            # centroid texel or at least half of its raster footprint
            order = sel[np.argsort(depth[sel], kind="stable")]
            R = resolution * 2
            raster = rasterize_ids(cuv[order], R, R)
            ids = np.where(raster >= 0, order[np.maximum(raster, 0)], -1)
            # a face is occluded (goes to the next layer) only when the
            # outermost face at its centroid texel lies clearly further out
            # along the chart axis; coplanar neighbours sharing a texel stay
            c = cuv[sel].mean(1)
            cxi = np.clip((c[:, 0] * (R - 1)).round().astype(int), 0, R - 1)
            cyi = np.clip(((1 - c[:, 1]) * (R - 1)).round().astype(int), 0, R - 1)
            owner = ids[cyi, cxi]
            # two voxel steps: staircase neighbours of one surface are not occluders
            eps = 0.035 * float(np.ptp(positions, axis=0).max())
            occluded = (owner >= 0) & (owner != sel) & (depth[np.maximum(owner, 0)] > depth[sel] + eps)
            placed[sel[~occluded]] = True
        per_layer.append(int(placed.sum()))
        remaining &= ~placed
        if not remaining.any():
            break
    order = np.argsort(depth, kind="stable")
    return cuv, chart, order, per_layer


def write_obj(path: Path, header, positions, uvs, faces_v, faces_t):
    lines = list(header)
    for p in positions:
        lines.append(f"v {p[0]:.6f} {p[1]:.6f} {p[2]:.6f}")
    for t in uvs:
        lines.append(f"vt {t[0]:.6f} {t[1]:.6f}")
    for fv, ft in zip(faces_v, faces_t):
        lines.append("f " + " ".join(f"{fv[k] + 1}/{ft[k] + 1}" for k in range(3)))
    path.write_text("\n".join(lines) + "\n")


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("obj", type=Path)
    ap.add_argument("--albedo", required=True)
    ap.add_argument("--out-dir", type=Path, required=True)
    ap.add_argument("--resolution", type=int, default=512)
    ap.add_argument("--padding", type=int, default=8)
    ap.add_argument("--max-cost", type=float, default=2.0,
                    help="xatlas chart max_cost: higher = fewer, larger charts")
    ap.add_argument("--mode", choices=["cube", "xatlas"], default="cube",
                    help="cube: six planar charts by dominant normal (voxel meshes); "
                         "xatlas: normal-deviation charting (smooth meshes)")
    a = ap.parse_args()

    folder = a.obj.parent
    positions, faces, old_cuv, header = read_obj_raw(a.obj)
    old = np.asarray(Image.open(folder / a.albedo).convert("RGBA"))
    old_cov = doomify3d.uv_coverage_mask(a.obj, (old.shape[1], old.shape[0]))
    _, n_old = ndimage.label(old_cov)
    print(f"{a.obj.name}: {len(positions)} verts, {len(faces)} tris, old atlas "
          f"{old.shape[1]}x{old.shape[0]} with {n_old} islands")

    if a.mode == "cube":
        w = h = a.resolution
        new_cuv, chart, order, per_layer = cube_unwrap(positions, faces, a.resolution, a.padding)
        print(f"cube: faces per chart {np.bincount(chart, minlength=6).tolist()}, "
              f"faces per layer {per_layer}")
        new_pos = positions
        indices = faces
        uv01 = new_cuv.reshape(-1, 2)
        faces_t = np.arange(len(faces) * 3).reshape(-1, 3)
        rgba, covered = bake_by_projection(old, old_cuv, new_cuv, w, h, order=order)
        _, n_new = ndimage.label(covered)
        border = covered & ndimage.binary_dilation(~covered)
        print(f"bake: coverage {covered.mean():.3f}, {n_new} islands, "
              f"border texels {border.mean():.3f} of atlas")
        a.out_dir.mkdir(parents=True, exist_ok=True)
        Image.fromarray(rgba).save(a.out_dir / a.albedo)
        write_obj(a.out_dir / a.obj.name, header, new_pos, uv01, indices, faces_t)
        mtl = folder / (a.obj.stem + ".mtl")
        if mtl.exists():
            (a.out_dir / mtl.name).write_bytes(mtl.read_bytes())
        Image.fromarray((covered * 255).astype(np.uint8)).save(a.out_dir / "coverage.png")
        print(f"wrote {a.out_dir / a.obj.name} and {a.out_dir / a.albedo}")
        return

    atlas = xatlas.Atlas()
    atlas.add_mesh(positions.astype(np.float32), faces.astype(np.uint32))
    co = xatlas.ChartOptions()
    co.max_cost = a.max_cost
    po = xatlas.PackOptions()
    po.resolution = a.resolution
    po.padding = a.padding
    po.bilinear = True
    po.rotate_charts = True
    atlas.generate(chart_options=co, pack_options=po)
    vmapping, indices, uvs = atlas[0]
    print(f"xatlas: {atlas.chart_count} charts, atlas {atlas.width}x{atlas.height}, "
          f"{len(uvs)} uv-verts")
    if atlas.width != a.resolution or atlas.height != a.resolution:
        print("  NOTE: xatlas chose a different size; texels are mapped into it as-is")
    w, h = int(atlas.width), int(atlas.height)
    indices = np.asarray(indices, np.int64)
    uvs = np.asarray(uvs, np.float64)
    # xatlas uvs are in texel units of the generated atlas
    uv01 = uvs / np.array([w, h], np.float64)
    # xatlas keeps triangle order; guard the correspondence
    assert len(indices) == len(faces), (len(indices), len(faces))
    new_pos = positions[np.asarray(vmapping, np.int64)]
    new_cuv = uv01[indices]
    # verify geometry survives untouched
    assert np.allclose(new_pos[indices], positions[faces], atol=1e-9)

    rgba, covered = bake_by_projection(old, old_cuv, new_cuv, w, h)
    _, n_new = ndimage.label(covered)
    border = covered & ndimage.binary_dilation(~covered)
    print(f"bake: coverage {covered.mean():.3f}, {n_new} islands, "
          f"border texels {border.mean():.3f} of atlas")

    a.out_dir.mkdir(parents=True, exist_ok=True)
    Image.fromarray(rgba).save(a.out_dir / a.albedo)
    write_obj(a.out_dir / a.obj.name, header, new_pos, uv01, indices, indices)
    mtl = folder / (a.obj.stem + ".mtl")
    if mtl.exists():
        (a.out_dir / mtl.name).write_bytes(mtl.read_bytes())
    Image.fromarray((covered * 255).astype(np.uint8)).save(a.out_dir / "coverage.png")
    print(f"wrote {a.out_dir / a.obj.name} and {a.out_dir / a.albedo}")


if __name__ == "__main__":
    main()
