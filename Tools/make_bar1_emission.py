"""Emission mask for BAR1B0: red ring lamps (top zone) + green band (mid zone).

UV islands are not spatial, so zones come from MESH height: every triangle's
mean object-space Y decides its zone, then texels inside the triangle's UV
footprint are tested by color. This is what keeps the emblem's red marks dark —
they share the lamp hue but live at mid height. Binary mask (255/0) in R —
endpoint values survive any sRGB interpretation.

Usage: python Tools/make_bar1_emission.py
(reads and writes Assets/Resources/ExperimentalPickups/BAR1B0/)
"""
import sys
import numpy as np
from PIL import Image
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from doomify3d import load_obj

REPO = Path(__file__).resolve().parent.parent
dst = REPO / "Assets" / "Resources" / "ExperimentalPickups" / "BAR1B0"

verts, tris, cuv = load_obj(dst / "BAR1B0.obj")
albedo = Image.open(dst / "BAR1B0_albedo.png").convert("RGBA")
W, H = albedo.size
alb = np.asarray(albedo).astype(np.int16)

v = np.asarray(verts, dtype=np.float64)
t = np.asarray(tris)
uv = np.asarray(cuv, dtype=np.float64)  # (F,3,2)
ymin, ymax = v[:, 1].min(), v[:, 1].max()
tri_y = (v[t[:, 0], 1] + v[t[:, 1], 1] + v[t[:, 2], 1]) / 3.0
tri_yn = (tri_y - ymin) / (ymax - ymin)

mask = np.zeros((H, W), dtype=np.uint8)

def rasterize(face_ids, color_test):
    hit = 0
    for f in face_ids:
        pts = uv[f]  # 3x2 in [0,1], v up
        xs = pts[:, 0] * (W - 1)
        ys = (1.0 - pts[:, 1]) * (H - 1)
        x0, x1 = int(np.floor(xs.min())), int(np.ceil(xs.max()))
        y0, y1 = int(np.floor(ys.min())), int(np.ceil(ys.max()))
        if x1 - x0 > W // 4 or y1 - y0 > H // 4:
            continue  # degenerate atlas-spanning triangle
        ax, ay = xs[0], ys[0]
        bx, by = xs[1], ys[1]
        cx, cy = xs[2], ys[2]
        den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(den) < 1e-9:
            continue
        for py in range(max(0, y0), min(H, y1 + 1)):
            for px in range(max(0, x0), min(W, x1 + 1)):
                l1 = ((by - cy) * (px - cx) + (cx - bx) * (py - cy)) / den
                l2 = ((cy - ay) * (px - cx) + (ax - cx) * (py - cy)) / den
                l3 = 1.0 - l1 - l2
                if l1 < -0.02 or l2 < -0.02 or l3 < -0.02:
                    continue
                r, g, b = alb[py, px, 0], alb[py, px, 1], alb[py, px, 2]
                if color_test(int(r), int(g), int(b)):
                    mask[py, px] = 255
                    hit += 1
    return hit

# Red ring lamps: top 25% of the mesh; red/pink dominant and bright enough.
lamp_faces = np.nonzero(tri_yn > 0.75)[0]
lamps = rasterize(lamp_faces, lambda r, g, b: r > 90 and r > g * 1.6 and r > b * 1.6)

# Green band: mid zone; clearly green-dominant.
band_faces = np.nonzero((tri_yn > 0.40) & (tri_yn < 0.80))[0]
band = rasterize(band_faces, lambda r, g, b: g > 80 and g > r * 1.35 and g > b * 1.35)

out = np.zeros((H, W, 4), dtype=np.uint8)
out[:, :, 0] = mask
out[:, :, 3] = 255
Image.fromarray(out).save(dst / "BAR1B0_emission.png")
print(f"lamp texels {lamps}, band texels {band}, mask -> BAR1B0_emission.png")
