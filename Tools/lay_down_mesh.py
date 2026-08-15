"""Bake a pitch rotation into a doomified OBJ — lay a stood-up pose down.

TRELLIS reads its conditioning image as the FRONT view of an upright subject.
A fall frame drawn as a body on its back (SPOSK0) therefore comes back
standing: measured on the raw SPOSK0 reconstruction, Y/X was 1.02 with Z only
0.59 — a figure on its feet with the legs splayed at the camera, not a body on
the floor. In game it read as a zombie kneeling in mid-air.

Rotating the mesh about X fixes it, and the angle is not free: the runtime
scales each frame so its bounds height equals the native patch height, so the
pitch that makes the mesh's own aspect match the sprite's is the one that
lands the corpse at sprite footprint. For SPOSK0 (native 52x34):

    -60deg -> 39 px wide at patch height   (75% of the sprite)
    -68deg -> 44 px
    -75deg -> 50 px                        (accepted 2026-08-15)

Negative pitch puts the head away from the camera and the soles toward it,
matching how the death sprites are drawn. UVs are untouched, so a texture
already projected onto the mesh stays valid.

Usage:
  python Tools/lay_down_mesh.py --obj <mesh.obj> --out <mesh.obj> --pitch -75
"""
from __future__ import annotations

import argparse
import math
from pathlib import Path


def rotate_x(x: float, y: float, z: float, c: float, s: float):
    return x, y * c - z * s, y * s + z * c


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--obj", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--pitch", type=float, required=True,
                   help="degrees about X; negative lays the figure onto its "
                        "back with the head away from the camera")
    a = p.parse_args()

    c, s = math.cos(math.radians(a.pitch)), math.sin(math.radians(a.pitch))
    src, dst = Path(a.obj), Path(a.out)
    dst.parent.mkdir(parents=True, exist_ok=True)
    out = []
    for line in src.read_text(encoding="utf-8", errors="ignore").splitlines():
        if line.startswith("v ") or line.startswith("vn "):
            tag, sx, sy, sz = line.split()[:4]
            x, y, z = rotate_x(float(sx), float(sy), float(sz), c, s)
            out.append(f"{tag} {x:.6f} {y:.6f} {z:.6f}")
        else:
            out.append(line)
    dst.write_text("\n".join(out) + "\n", encoding="utf-8")
    print(f"{dst.name}: pitched {a.pitch:+.0f}deg")


if __name__ == "__main__":
    main()
