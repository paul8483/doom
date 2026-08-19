"""Build the Enhanced 3D fireball (imp's BAL1) out of its own WAD sprite.

The fly frames are a 15x15 glowing disc: a white-hot core, a yellow body and
a dark orange rim. A solid ball cannot show that gradient — the core sits
inside the sphere and the surface would read as one flat orange shell — so
the split here is deliberate:

  * the MESH carries only the shape, a voxel ball on the sprite's own pixel
    grid, so the silhouette is as chunky as the sprite and the thing
    occludes, spins and sits in the world like a real object;
  * the GRADIENT is re-derived per fragment by the shader from the distance
    to the centre of the projected disc, so the core reads white from every
    angle exactly as the sprite does.

No colour is invented: this tool bakes the sprite's own texels into a
radius x variant table. Column = distance from the disc centre, row = one of
the colours the sprite actually uses at that distance (its per-pixel
boiling). Every voxel picks a row by a hash of its position, so a spinning
ball keeps the sprite's noise instead of a smooth ramp.

Outputs (Assets/Resources/ExperimentalProjectiles/BAL1/):
  BAL1.obj / BAL1.mtl        shared voxel ball, radius 0.5 in object space,
                             UV.y = the voxel's variant row (UV.x unused —
                             the shader computes it from the view)
  BAL1<F>0_profile.png       16 (radius) x 16 (variant) colour table

Usage:
  python Tools/make_fireball_mesh.py             # BAL1, frames A and B
  python Tools/make_fireball_mesh.py BAL1 A B
"""

import math
import struct
import sys
from pathlib import Path

from PIL import Image

REPO = Path(__file__).resolve().parent.parent
WAD_PATH = REPO / "Assets" / "StreamingAssets" / "wads" / "freedoom1.wad"
OUT_ROOT = REPO / "Assets" / "Resources" / "ExperimentalProjectiles"

PROFILE_W = 16   # radial buckets, matching the sprite's ~7.3 px radius
PROFILE_H = 16   # colour variants per bucket (the sprite's own noise)


# -- WAD reading (same protocol as dump_trellis_native_inputs.py) -----------

def read_directory(data):
    ident, numlumps, infotableofs = struct.unpack_from("<4sii", data, 0)
    if ident not in (b"IWAD", b"PWAD"):
        raise ValueError(f"not a WAD: {ident}")
    lumps = {}
    for i in range(numlumps):
        filepos, size, raw = struct.unpack_from(
            "<ii8s", data, infotableofs + 16 * i)
        lumps.setdefault(raw.rstrip(b"\0").decode("ascii", "replace"),
                         (filepos, size))
    return lumps


def decode_patch(data, pos, palette):
    width, height, _left, _top = struct.unpack_from("<hhhh", data, pos)
    img = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    px = img.load()
    col_ofs = struct.unpack_from(f"<{width}i", data, pos + 8)
    for x in range(width):
        p = pos + col_ofs[x]
        while True:
            topdelta = data[p]
            if topdelta == 0xFF:
                break
            length = data[p + 1]
            p += 3
            for i in range(length):
                px[x, topdelta + i] = (*palette[data[p]], 255)
                p += 1
            p += 1
    return img


# -- The radius x variant colour table --------------------------------------

def build_profile(img):
    """One texel per (distance from centre, colour variant at that distance).

    Buckets are filled from the sprite itself; a bucket with no pixels of its
    own borrows the nearest one that has some, so the ramp never gaps. Within
    a bucket the colours are sorted by luminance and sampled evenly across
    the rows, which keeps the ring's real spread (a random pick would clump).
    """
    w, h = img.size
    px = img.load()
    cx, cy = w / 2.0, h / 2.0
    rmax = 0.0
    for y in range(h):
        for x in range(w):
            if px[x, y][3]:
                rmax = max(rmax, math.hypot(x + 0.5 - cx, y + 0.5 - cy))

    buckets = [[] for _ in range(PROFILE_W)]
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            d = math.hypot(x + 0.5 - cx, y + 0.5 - cy) / rmax
            buckets[min(PROFILE_W - 1, int(d * PROFILE_W))].append((r, g, b))

    for i, bucket in enumerate(buckets):
        if bucket:
            continue
        for step in range(1, PROFILE_W):
            for j in (i - step, i + step):
                if 0 <= j < PROFILE_W and buckets[j]:
                    buckets[i] = list(buckets[j])
                    break
            if buckets[i]:
                break

    out = Image.new("RGBA", (PROFILE_W, PROFILE_H), (0, 0, 0, 255))
    op = out.load()
    for col, bucket in enumerate(buckets):
        ordered = sorted(
            bucket, key=lambda c: 0.30 * c[0] + 0.59 * c[1] + 0.11 * c[2])
        for row in range(PROFILE_H):
            k = 0 if len(ordered) == 1 else \
                round(row * (len(ordered) - 1) / (PROFILE_H - 1))
            op[col, row] = (*ordered[k], 255)
    return out, rmax


# -- The voxel ball ---------------------------------------------------------

# Corners wind counter-clockwise seen from outside, so cross(c1-c0, c2-c0)
# points along the face normal — the convention every other OBJ in the repo
# already uses.
FACES = [
    ((1, 0, 0), [(1, 0, 0), (1, 1, 0), (1, 1, 1), (1, 0, 1)]),
    ((-1, 0, 0), [(0, 0, 0), (0, 0, 1), (0, 1, 1), (0, 1, 0)]),
    ((0, 1, 0), [(0, 1, 0), (0, 1, 1), (1, 1, 1), (1, 1, 0)]),
    ((0, -1, 0), [(0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1)]),
    ((0, 0, 1), [(0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1)]),
    ((0, 0, -1), [(0, 0, 0), (0, 1, 0), (1, 1, 0), (1, 0, 0)]),
]


def variant_row(i, j, k):
    """Deterministic per-voxel variant. The ball spins, so a hash keeps the
    boiling incoherent instead of banding it along an axis."""
    h = (i * 73856093) ^ (j * 19349663) ^ (k * 83492791)
    return (h & 0x7FFFFFFF) % PROFILE_H


def build_ball(size, radius):
    """Voxel sphere at the sprite's own pixel pitch, outer faces only."""
    c = size / 2.0
    filled = set()
    for i in range(size):
        for j in range(size):
            for k in range(size):
                d = math.sqrt((i + 0.5 - c) ** 2 + (j + 0.5 - c) ** 2
                              + (k + 0.5 - c) ** 2)
                if d <= radius:
                    filled.add((i, j, k))

    verts, uvs, tris = [], [], []
    index = {}
    extent = 0.0
    for (i, j, k) in sorted(filled):
        row = (variant_row(i, j, k) + 0.5) / PROFILE_H
        for normal, corners in FACES:
            if (i + normal[0], j + normal[1], k + normal[2]) in filled:
                continue
            face = []
            for (dx, dy, dz) in corners:
                p = (i + dx - c, j + dy - c, k + dz - c)
                extent = max(extent, abs(p[0]), abs(p[1]), abs(p[2]))
                key = (p, row)
                if key not in index:
                    index[key] = len(verts)
                    verts.append(p)
                    uvs.append((0.5, row))
                face.append(index[key])
            tris.append((face[0], face[1], face[2]))
            tris.append((face[0], face[2], face[3]))

    scale = 0.5 / extent
    verts = [(x * scale, y * scale, z * scale) for (x, y, z) in verts]
    return verts, uvs, tris


def write_obj(path, mtl_name, material, verts, uvs, tris):
    lines = [f"mtllib {mtl_name}", "o Fireball"]
    lines += [f"v {x:.6f} {y:.6f} {z:.6f}" for (x, y, z) in verts]
    lines += [f"vt {u:.6f} {v:.6f}" for (u, v) in uvs]
    lines.append(f"usemtl {material}")
    lines += [f"f {a + 1}/{a + 1} {b + 1}/{b + 1} {c + 1}/{c + 1}"
              for (a, b, c) in tris]
    path.write_text("\n".join(lines) + "\n", encoding="ascii")


def main():
    args = sys.argv[1:]
    sprite = (args[0] if args else "BAL1").upper()
    frames = [f.upper() for f in args[1:]] or ["A", "B"]

    data = WAD_PATH.read_bytes()
    lumps = read_directory(data)
    pal_pos = lumps["PLAYPAL"][0]
    palette = [tuple(data[pal_pos + i * 3: pal_pos + i * 3 + 3])
               for i in range(256)]

    out_dir = OUT_ROOT / sprite
    out_dir.mkdir(parents=True, exist_ok=True)

    size = None
    radius = None
    for frame in frames:
        lump = f"{sprite}{frame}0"
        if lump not in lumps:
            print(f"  {lump}: NOT FOUND")
            continue
        img = decode_patch(data, lumps[lump][0], palette)
        profile, rmax = build_profile(img)
        profile.save(out_dir / f"{lump}_profile.png")
        print(f"  {lump}: {img.width}x{img.height}, r={rmax:.2f} -> "
              f"{lump}_profile.png ({PROFILE_W}x{PROFILE_H})")
        if size is None:
            size, radius = img.width, rmax

    if size is None:
        return 1

    verts, uvs, tris = build_ball(size, radius)
    write_obj(out_dir / f"{sprite}.obj", f"{sprite}.mtl", "fireball",
              verts, uvs, tris)
    (out_dir / f"{sprite}.mtl").write_text(
        "\n".join([
            "newmtl fireball",
            "Ka 1.000 1.000 1.000",
            "Kd 1.000 1.000 1.000",
            "d 1.0",
            "illum 1",
            f"map_Kd {sprite}{frames[0]}0_profile.png",
        ]) + "\n", encoding="ascii")
    print(f"  {sprite}.obj: {len(verts)} verts, {len(tris)} tris "
          f"(voxel ball {size}^3, r={radius:.2f})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
