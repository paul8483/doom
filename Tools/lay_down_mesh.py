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


def rotate_y(x: float, y: float, z: float, c: float, s: float):
    return x * c + z * s, y, -x * s + z * c


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--obj", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--pitch", type=float, default=0.0,
                   help="degrees about X; negative lays the figure onto its "
                        "back with the head away from the camera")
    # A weapon drawn in three-quarter (the anti-blob rule for TRELLIS) comes
    # back angled ACROSS the body, so a fire frame reads sideways in game —
    # the muzzle must face the target. Baking the yaw keeps the runtime's
    # shared "offset 0" convention; the angle is the one whose doomify3d
    # render shows the muzzle dead-on (SPOSF1: -40, 2026-08-27).
    p.add_argument("--yaw", type=float, default=0.0,
                   help="degrees about Y, applied BEFORE the pitch; the value "
                        "that renders muzzle-on becomes the new front")
    # A corpse frame is a body PRESSED to the floor, but a top-down drawing
    # carries no thickness, so TRELLIS returns a rounded mound: TROOM0 came
    # back 0.97 x 0.72 x 0.98 and rendered 35 px wide where the sprite is 57
    # (the corpse would shrink at the very frame the sprite spreads out).
    # Rotation cannot fix it — the mound has no thin axis to turn toward the
    # camera — so squash Y until the silhouette matches the sprite's own
    # aspect. Applied AFTER projection: UVs and textures stay valid.
    p.add_argument("--flatten-to-aspect", type=float, default=None,
                   help="scale Y so that X/Y equals this (native patch "
                        "width/height); use on mound-shaped corpse frames")
    # Frames that lie flat are normalized by WIDTH in the runtime, so their
    # own thickness is the pose: stretching Y to satisfy the height rule is
    # what stood the piles up on 2026-08-17 ("kuchi ne lezhat na polu" came
    # back after the tilt was removed). --scale-y undoes such a stretch
    # exactly; it is a plain axis scale, so UVs stay valid and normals, as
    # with --flatten-to-aspect, are only rotated.
    p.add_argument("--scale-y", type=float, default=None,
                   help="multiply Y by this after the pitch (use 1/f to undo "
                        "an earlier stretch of f)")
    a = p.parse_args()

    c, s = math.cos(math.radians(a.pitch)), math.sin(math.radians(a.pitch))
    cy, sy_ = math.cos(math.radians(a.yaw)), math.sin(math.radians(a.yaw))
    src, dst = Path(a.obj), Path(a.out)
    dst.parent.mkdir(parents=True, exist_ok=True)
    lines = src.read_text(encoding="utf-8", errors="ignore").splitlines()

    def transform(x: float, y: float, z: float):
        x, y, z = rotate_y(x, y, z, cy, sy_)
        return rotate_x(x, y, z, c, s)

    verts = []
    for line in lines:
        if line.startswith("v "):
            _, sx, sy, sz = line.split()[:4]
            verts.append(transform(float(sx), float(sy), float(sz)))

    scale_y = 1.0
    if a.scale_y is not None:
        scale_y = a.scale_y
    if a.flatten_to_aspect is not None and verts:
        xs = [v[0] for v in verts]
        ys = [v[1] for v in verts]
        span_x = max(xs) - min(xs)
        span_y = max(ys) - min(ys)
        if span_x > 0 and span_y > 0:
            scale_y = (span_x / a.flatten_to_aspect) / span_y

    out = []
    for line in lines:
        if line.startswith("v ") or line.startswith("vn "):
            tag, sx, sy, sz = line.split()[:4]
            x, y, z = transform(float(sx), float(sy), float(sz))
            # Positions squash; normals only rotate — scaling a normal is not
            # the inverse-transpose it would need, and the shader is unlit.
            if tag == "v":
                y *= scale_y
            out.append(f"{tag} {x:.6f} {y:.6f} {z:.6f}")
        else:
            out.append(line)
    dst.write_text("\n".join(out) + "\n", encoding="utf-8")
    note = f", scaled Y x{scale_y:.3f}" if scale_y != 1.0 else ""
    yaw_note = f", yawed {a.yaw:+.0f}deg" if a.yaw != 0.0 else ""
    print(f"{dst.name}: pitched {a.pitch:+.0f}deg{yaw_note}{note}")


if __name__ == "__main__":
    main()
