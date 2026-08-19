"""Re-anchor the candelabra's fires onto its generated metal.

`make_torch_model.py` measures each caged fire off the SPRITE — that is where
its shape and colour come from — but a reconstruction rebuilds the object with
its own proportions: CBRA came back 38 patch-pixels wide against the sprite's
31, with its lanterns at x -13.5 / 0 / +13.5 instead of -10 / +0.5 / +11. Left
alone, three fires would hang beside their cages instead of inside them.

So the anchors are fitted here, after the metal exists: each cage is found in
the mesh, and the sprite's fire is mapped into it by the ratio of the two cage
spans, which keeps the fire the same fraction of its lantern as the sprite
draws it. Only the table changes; the fire meshes and their colour tables stay
exactly as the sprite made them.

Usage:
  python Tools/fit_fires_to_mesh.py            # CBRA
  python Tools/fit_fires_to_mesh.py CBRA
"""

import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import doomify3d  # noqa: E402
from split_torch_sprite import (  # noqa: E402
    LANTERN_ROW, WAD_PATH, read_directory, read_palette, decode_patch, fire_mask)
from make_torch_model import (  # noqa: E402
    OUT_ROOT, fire_clusters, patch_header)


def sprite_cages(img, floor_row, clusters):
    """Per lantern: the metal cage's row span in the sprite, in patch pixels
    measured up from the thing's feet."""
    mask = fire_mask(img, floor_row)
    px = img.load()
    width, height = img.size
    result = []
    for cluster in clusters:
        # The cage is the metal in this lantern's columns above the floor row.
        lo = int(cluster["centre"] - 6)
        hi = int(cluster["centre"] + 6)
        rows = [y for y in range(min(floor_row, height))
                for x in range(max(0, lo), min(width, hi + 1))
                if px[x, y][3] and not mask[y][x]]
        if not rows:
            rows = [cluster["top"], cluster["bottom"]]
        top, bottom = min(rows), max(rows)
        result.append((height - 1 - bottom, bottom - top + 1))  # (yMin, span)
    return result


def mesh_cages(obj_path, patch_height):
    """The generated metal's lanterns: x centre and y span per cage, in patch
    pixels, found by the gaps the cages leave between them."""
    verts, _tris, _uv = doomify3d.load_obj(obj_path)
    v = np.asarray(verts, dtype=float)
    lo, hi = v.min(0), v.max(0)
    scale = patch_height / (hi[1] - lo[1])
    p = (v - [(lo[0] + hi[0]) / 2, lo[1], (lo[2] + hi[2]) / 2]) * scale

    top = p[p[:, 1] > patch_height * 0.62]
    hist, edges = np.histogram(top[:, 0], bins=48)
    threshold = hist.mean() * 0.15
    splits, previous = [], -1e9
    for i, count in enumerate(hist):
        if count >= threshold:
            continue
        middle = (edges[i] + edges[i + 1]) / 2
        if middle - previous > 2:
            splits.append(middle)
        previous = middle

    bounds = [-1e9] + [x for x in splits
                       if abs(x) < patch_height] + [1e9]
    cages = []
    for i in range(len(bounds) - 1):
        c = top[(top[:, 0] > bounds[i]) & (top[:, 0] <= bounds[i + 1])]
        if len(c) < 200:
            continue
        cages.append({
            "centre": float((c[:, 0].min() + c[:, 0].max()) / 2),
            "yMin": float(c[:, 1].min()),
            "span": float(c[:, 1].max() - c[:, 1].min()),
        })
    cages.sort(key=lambda c: c["centre"])
    return cages


def main():
    names = [a.upper() for a in sys.argv[1:]] or list(LANTERN_ROW)
    data = WAD_PATH.read_bytes()
    lumps = read_directory(data)
    palette = read_palette(data, lumps)

    for base in names:
        floor_row = LANTERN_ROW[base]
        pos = lumps[f"{base}A0"][0]
        _w, patch_height, leftoff, _top = patch_header(data, pos)
        img = decode_patch(data, pos, palette)
        clusters = fire_clusters(img, floor_row)
        cages_2d = sprite_cages(img, floor_row, clusters)

        out_dir = OUT_ROOT / base
        obj = out_dir / f"{base}_stand_mesh.obj"
        if not obj.exists():
            print(f"  {base}: no generated metal at {obj}")
            continue
        cages_3d = mesh_cages(obj, patch_height)
        if len(cages_3d) != len(clusters):
            print(f"  {base}: found {len(cages_3d)} cages in the mesh for "
                  f"{len(clusters)} fires — not fitting blindly")
            continue

        lines, summary = [], []
        for index, (cluster, (cage_y, cage_span), cage) in enumerate(
                zip(clusters, cages_2d, cages_3d)):
            rows = len(cluster["rows"])
            fire_y = patch_height - 1 - cluster["bottom"]
            ratio = cage["span"] / max(cage_span, 1e-6)
            fitted_y = cage["yMin"] + (fire_y - cage_y) * ratio
            fitted_h = rows * ratio
            name = f"{base}_fire{index}"
            lines.append(
                f"{name} {cage['centre']:.3f} {fitted_y:.3f} {fitted_h:.3f}")
            summary.append(
                f"{index}:x{cage['centre']:+.1f}/y{fitted_y:.1f}/h{fitted_h:.1f}"
                f" (sprite x{cluster['centre'] - leftoff:+.1f}/y{fire_y}/h{rows})")

        table = out_dir / f"{base}_fires.txt"
        table.write_text("\n".join(lines) + "\n", encoding="ascii")
        print(f"  {base}: fitted to the mesh -> " + "  ".join(summary))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
