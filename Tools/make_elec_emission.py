"""Emission mask for ELECA0: the recessed blue CRT screens glow steady.
Same instrument as make_colu_emission.py — the zone comes from MESH height
(the UV atlas is not spatial), color decides within the zone. Blue lives
only on the screens (native palette rule), so the color test is a simple
blue-dominance check; the height band keeps stray blue-ish steel dark.

Usage: python Tools/make_elec_emission.py
(reads and writes Assets/Resources/ExperimentalPickups/ELECA0/)
"""
import sys
import numpy as np
from PIL import Image
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from doomify3d import load_obj

REPO = Path(__file__).resolve().parent.parent
dst = REPO / "Assets" / "Resources" / "ExperimentalPickups" / "ELECA0"

verts, tris, cuv = load_obj(dst / "ELECA0.obj")
albedo = Image.open(dst / "ELECA0_albedo.png").convert("RGBA")
W, H = albedo.size
alb = np.asarray(albedo).astype(np.int16)

v = np.asarray(verts, dtype=np.float64)
t = np.asarray(tris)
uv = np.asarray(cuv, dtype=np.float64)
ymin, ymax = v[:, 1].min(), v[:, 1].max()
tri_y = (v[t[:, 0], 1] + v[t[:, 1], 1] + v[t[:, 2], 1]) / 3.0
tri_yn = (tri_y - ymin) / (ymax - ymin)

mask = np.zeros((H, W), dtype=np.uint8)

def rasterize(face_ids, color_test):
    hit = 0
    for f in face_ids:
        pts = uv[f]
        xs = pts[:, 0] * (W - 1)
        ys = (1.0 - pts[:, 1]) * (H - 1)
        x0, x1 = int(np.floor(xs.min())), int(np.ceil(xs.max()))
        y0, y1 = int(np.floor(ys.min())), int(np.ceil(ys.max()))
        if x1 - x0 > W // 4 or y1 - y0 > H // 4:
            continue
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
                r, g, b = int(alb[py, px, 0]), int(alb[py, px, 1]), int(alb[py, px, 2])
                if color_test(r, g, b):
                    mask[py, px] = 255
                    hit += 1
    return hit

def screen_blue(r, g, b):
    return b > 80 and b > r * 1.6 and b > g * 1.6

# Native ELECA0: the CRT screen band sits at 67-74% from the top, i.e.
# 26-33% of mesh height from the floor; the band is padded for the bezel.
screens = rasterize(np.nonzero((tri_yn > 0.18) & (tri_yn < 0.42))[0], screen_blue)

out = np.zeros((H, W, 4), dtype=np.uint8)
out[:, :, 0] = mask
out[:, :, 3] = 255
Image.fromarray(out).save(dst / "ELECA0_emission.png")
print(f"screen texels {screens}, mask -> ELECA0_emission.png")
