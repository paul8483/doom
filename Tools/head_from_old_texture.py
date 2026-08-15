"""Composite a projected albedo with the head region of an earlier texture.

`project_hint_texture.py` paints every mesh from its own v2 hint, which fixes
stop-motion color drift but pays for it wherever the hint's front view cannot
describe a recessed feature: the SARG demon's maw loses its teeth and its
horns go dull, because the mouth interior and the fangs project from the same
(x, y) as the lips around them.

`--head-from-bake` (the TROO/BOSS remedy) does not help here — the raw TRELLIS
bake's head is flat dark red. What does work is taking the head from the
monster's PREVIOUS accepted texture, which is geometry-aligned by construction:
body keeps the projection's cross-frame consistency, head keeps the readable
maw. Accepted at the SARG re-roll gate 2026-08-15.

Usage:
  python Tools/head_from_old_texture.py --obj <mesh.obj> \
      --new <projected_albedo.png> --old <accepted_albedo.png> \
      --out <albedo.png> [--y-rel 0.70]
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
import doomify3d  # noqa: E402
from project_hint_texture import rasterize_positions  # noqa: E402


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--obj", required=True)
    p.add_argument("--new", required=True)
    p.add_argument("--old", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--y-rel", type=float, default=0.70,
                   help="fraction of mesh height above which texels come "
                        "from --old")
    a = p.parse_args()

    verts, tris, cuv = doomify3d.load_obj(Path(a.obj))
    v = verts.copy()
    v[:, 0] -= (v[:, 0].min() + v[:, 0].max()) / 2
    # Rasterize at the atlas' native resolution for the same reason the
    # projection does: at texcap resolution triangles contend for texels.
    proj = 1024
    pos, _, filled = rasterize_positions(v, tris, cuv, proj)
    y_min, y_max = v[:, 1].min(), v[:, 1].max()
    y_rel = np.zeros((proj, proj), np.float64)
    y_rel[filled] = (pos[filled][:, 1] - y_min) / max(1e-9, y_max - y_min)
    head1024 = (y_rel >= a.y_rel) & filled

    new = np.asarray(Image.open(a.new).convert("RGBA")).copy()
    old = np.asarray(Image.open(a.old).convert("RGBA"))
    size = new.shape[0]
    head = np.asarray(Image.fromarray((head1024 * 255).astype(np.uint8), "L")
                      .resize((size, size), Image.BOX)) > 127
    new[head] = old[head]
    Image.fromarray(new).save(a.out)
    print(f"{Path(a.out).name}: head texels {int(head.sum())}")


if __name__ == "__main__":
    main()
