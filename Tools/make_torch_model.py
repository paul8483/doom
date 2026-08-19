"""Build the Enhanced 3D firesticks out of their own WAD sprites.

A torch is a metal stand carrying a flame, and BOTH halves are solids of
revolution — which is why this wave needs no conditioning drawing and no
TRELLIS run at all:

  * the STAND is a turned object (pole, rings, bowl, base). Its silhouette
    revolved about the thing's axis IS its shape, and its shading is radial
    for the same reason a cylinder's is.
  * the FLAME cannot be a baked surface: its hot core (white on the blue and
    green torches, yellow on the red one) sits INSIDE the plume, so a bake
    returns a flat coloured cone and loses the very thing that reads as fire.
    That is the fireball finding of 2026-08-19, one dimension richer — a ball
    needs a radial ramp, a plume needs radius AND height.

So both parts are built the same way: the MESH carries only the shape,
voxelized at the sprite's own pixel pitch, and the COLOUR is re-derived per
fragment by `Doom/ExperimentalTorch` from a table baked here out of the lump's
own texels — u = distance from the part's axis as SEEN, v = height. The flame
gets one mesh and one table per animation frame (A-D), so its silhouette
animates like the sprite's; alpha 0 in a table means that frame has no flame
there. Nothing is drawn and nothing is invented.

Mesh conventions match `make_fireball_mesh.py`: outer voxel faces only,
counter-clockwise from outside, OBJ normalized (axis at x=z=0, bottom at
y=0, height exactly 1.0). Vertex UV carries (radius of this voxel's row in
object units, height of this vertex); the shader divides by the former to get
its normalized radial coordinate, and reads the axis' own bend out of the
1 x N `_spine` table.

Outputs (Assets/Resources/ExperimentalTorches/<BASE>/):
  <BASE>_stand.obj / .mtl            lathe stand
  <BASE>_stand_profile|_spine.png    its colour and axis tables
  <BASE><F>0_flame.obj / .mtl        plume for animation frame F
  <BASE><F>0_flame_profile|_spine.png

Usage:
  python Tools/make_torch_model.py            # all six firesticks
  python Tools/make_torch_model.py TBLU TRED
"""

import hashlib
import math
import struct
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from split_torch_sprite import (  # noqa: E402
    SPLIT_ROW, LANTERN_ROW, FRAMES, WAD_PATH, read_directory, read_palette,
    decode_patch, fire_mask)

REPO = Path(__file__).resolve().parent.parent
OUT_ROOT = REPO / "Assets" / "Resources" / "ExperimentalTorches"
META_TEMPLATE_DIR = REPO / "Assets" / "Resources" / "ExperimentalProjectiles" / "BAL1"

PROFILE_W = 16   # radial buckets across the plume's own radius


def patch_header(data, pos):
    width, height, leftoff, topoff = struct.unpack_from("<hhhh", data, pos)
    return width, height, leftoff, topoff


def flame_rows(img, split):
    """Opaque texels of the flame region, as [(row, [(x, rgb), ...]), ...]."""
    px = img.load()
    rows = []
    for y in range(min(split, img.height)):
        line = []
        for x in range(img.width):
            r, g, b, a = px[x, y]
            if a:
                line.append((x, (r, g, b)))
        rows.append(line)
    return rows


def row_centre(line):
    """Centroid of one sprite row of flame, in pixel coordinates."""
    if not line:
        return None
    return sum(x + 0.5 for x, _rgb in line) / len(line)


def spine(rows, split, axis):
    """Per-row centre and radius of ONE frame's plume, about its own spine.

    Two shapes were tried and rejected on the preview before this one:
    revolving about the thing's axis (a tongue that curls to one side reads
    as its distance from the axis, so the plume came back a symmetric urn,
    far wider than the sprite and with no taper), and revolving the envelope
    of all four frames about a shared spine (the union of a left-leaning and
    a right-leaning tongue is just as fat). The mesh is free here — it is
    computed, not generated — so each frame gets its own plume and the
    silhouette animates like the sprite's does.
    """
    centre = [None] * split
    radius = [0.0] * split
    for y in range(split):
        c = row_centre(rows[y])
        if c is None:
            continue
        centre[y] = c
        for x, _rgb in rows[y]:
            radius[y] = max(radius[y], abs(x - c), abs(x + 1 - c))
    # Rows with no flame in any frame still need a centre for interpolation.
    last = None
    for y in range(split):
        if centre[y] is None:
            centre[y] = last
        else:
            last = centre[y]
    nxt = None
    for y in range(split - 1, -1, -1):
        if centre[y] is None:
            centre[y] = nxt
        else:
            nxt = centre[y]
    return [c if c is not None else axis for c in centre], radius


def stand_rows(img, split):
    """Opaque texels of the metal stand, as rows counted from the split down."""
    px = img.load()
    rows = []
    for y in range(split, img.height):
        line = []
        for x in range(img.width):
            r, g, b, a = px[x, y]
            if a:
                line.append((x, (r, g, b)))
        rows.append(line)
    return rows


def lathe(rows, axis):
    """Per-row centre and radius of the stand, about the thing's own axis.

    The stand is a turned object — pole, rings, bowl, base — so its silhouette
    revolved about the axis IS its shape, and its shading is radial for the
    same reason a cylinder's is. Measuring about the axis rather than the row
    centroid keeps it that way: the ornate head is decoration, and letting it
    drag the centre would bend a straight pole.
    """
    centre = [axis] * len(rows)
    radius = [0.0] * len(rows)
    for y, line in enumerate(rows):
        for x, _rgb in line:
            radius[y] = max(radius[y], abs(x - axis), abs(x + 1 - axis))
    return centre, radius


def build_profile(rows, radius, centre, about_centroid=True):
    """One texel per (normalized radius, sprite row) for a single frame.

    Alpha 0 means this frame has no flame there — that is how the silhouette
    animates on a static mesh. Inside the outermost filled bucket the ramp is
    closed by borrowing the nearest filled neighbour: fire is solid from the
    axis outwards, and a curling tongue that misses the axis in one row must
    not punch a hole through the plume.
    """
    height = len(rows)
    out = Image.new("RGBA", (PROFILE_W, height), (0, 0, 0, 0))
    op = out.load()
    for y, line in enumerate(rows):
        if not line or radius[y] <= 0:
            continue
        # Each frame is measured about ITS OWN centroid: a tongue that leans
        # right in one frame is still a plume seen from its own middle, and
        # bucketing it about the shared spine would hollow the core out.
        own = (row_centre(line) or centre[y]) if about_centroid else centre[y]
        buckets = [[] for _ in range(PROFILE_W)]
        for x, rgb in line:
            d = abs(x + 0.5 - own) / radius[y]
            buckets[min(PROFILE_W - 1, int(d * PROFILE_W))].append(rgb)
        # The mesh reaches the row's voxel edge, but pixels are binned by their
        # centres, so the outermost half-texel ring had no colour and the whole
        # outer shell was clipped away (first gate panel, 2026-08-19). Carry the
        # edge colour out to the last bucket instead.
        outermost = max((i for i, b in enumerate(buckets) if b), default=-1)
        if 0 <= outermost < PROFILE_W - 1:
            for i in range(outermost + 1, PROFILE_W):
                buckets[i] = list(buckets[outermost])
            outermost = PROFILE_W - 1
        for i in range(outermost + 1):
            if buckets[i]:
                continue
            for step in range(1, PROFILE_W):
                for j in (i - step, i + step):
                    if 0 <= j <= outermost and buckets[j]:
                        buckets[i] = list(buckets[j])
                        break
                if buckets[i]:
                    break
        for i, bucket in enumerate(buckets):
            if not bucket:
                continue
            r = round(sum(c[0] for c in bucket) / len(bucket))
            g = round(sum(c[1] for c in bucket) / len(bucket))
            b = round(sum(c[2] for c in bucket) / len(bucket))
            op[i, y] = (r, g, b, 255)
    return out


FACES = [
    ((1, 0, 0), [(1, 0, 0), (1, 1, 0), (1, 1, 1), (1, 0, 1)]),
    ((-1, 0, 0), [(0, 0, 0), (0, 0, 1), (0, 1, 1), (0, 1, 0)]),
    ((0, 1, 0), [(0, 1, 0), (0, 1, 1), (1, 1, 1), (1, 1, 0)]),
    ((0, -1, 0), [(0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1)]),
    ((0, 0, 1), [(0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1)]),
    ((0, 0, -1), [(0, 0, 0), (0, 1, 0), (1, 1, 0), (1, 0, 0)]),
]


def build_plume(radius, centre, axis):
    """Voxel solid of revolution at the sprite's pixel pitch.

    Grid cell = one sprite texel. Row j counts up from the flame's bottom, so
    it reads sprite row `split - 1 - j`. Cells are addressed relative to the
    thing's axis, which is where DOOM draws the sprite's `leftoffset` column.
    """
    split = len(radius)
    rmax = max(radius)
    span = int(math.ceil(rmax + max(abs(c - axis) for c in centre))) + 2
    filled = set()
    for j in range(split):
        y = split - 1 - j
        r = radius[y]
        if r <= 0:
            continue
        off = centre[y] - axis
        for i in range(-span, span):
            for k in range(-span, span):
                if math.hypot(i + 0.5 - off, k + 0.5) <= r:
                    filled.add((i, j, k))

    verts, uvs, tris = [], [], []
    index = {}
    scale = 1.0 / split
    for (i, j, k) in sorted(filled):
        u = radius[split - 1 - j] * scale
        for normal, corners in FACES:
            if (i + normal[0], j + normal[1], k + normal[2]) in filled:
                continue
            face = []
            for (dx, dy, dz) in corners:
                p = ((i + dx) * scale, (j + dy) * scale, (k + dz) * scale)
                key = (p, round(u, 6))
                if key not in index:
                    index[key] = len(verts)
                    verts.append(p)
                    uvs.append((u, p[1]))
                face.append(index[key])
            tris.append((face[0], face[1], face[2]))
            tris.append((face[0], face[2], face[3]))
    return verts, uvs, tris


SPINE_RANGE = 0.5   # object units the stored offset spans, +/-


def build_spine_texture(centre, axis, split):
    """1 x N table of the plume's own axis offset per height.

    The shader needs it before it can measure anything: the projected radial
    coordinate is taken from the flame's spine, and the spine bends. OBJ
    carries one UV channel, which already holds (radius, height), so the bend
    travels as its own tiny LUT rather than as a second vertex stream.
    R = offset / SPINE_RANGE * 0.5 + 0.5, in object units (mesh height 1.0).
    """
    out = Image.new("RGBA", (1, split), (128, 0, 0, 255))
    op = out.load()
    for y in range(split):
        off = (centre[y] - axis) / split
        v = int(round((off / SPINE_RANGE * 0.5 + 0.5) * 255))
        op[0, y] = (max(0, min(255, v)), 0, 0, 255)
    return out


def write_obj(path, mtl_name, material, verts, uvs, tris):
    lines = [f"mtllib {mtl_name}", "o TorchFlame"]
    lines += [f"v {x:.6f} {y:.6f} {z:.6f}" for (x, y, z) in verts]
    lines += [f"vt {u:.6f} {v:.6f}" for (u, v) in uvs]
    lines.append(f"usemtl {material}")
    lines += [f"f {a + 1}/{a + 1} {b + 1}/{b + 1} {c + 1}/{c + 1}"
              for (a, b, c) in tris]
    path.write_text("\n".join(lines) + "\n", encoding="ascii")


def write_meta(asset_path, template_name, srgb=True):
    """Clone BAL1's import settings (Point, no mip, Clamp for the tables) so a
    headless run sees the same import as the editor. The GUID is derived from
    the asset path, so regenerating an asset keeps its references intact.

    `srgb=False` is not cosmetic. The spine table carries a NUMBER, not a
    colour, and this project renders Linear: imported as sRGB, its neutral 128
    came back as 0.216, and the shader read the plume's axis as shifted by 0.28
    of the part's height. The torch then rendered correctly from the one yaw
    where that shift points at the camera and fell apart from every other —
    which is exactly what the first gate panel showed (2026-08-19). Same lesson
    as packed shader parameters that must not travel through SetColor.
    """
    meta = asset_path.with_suffix(asset_path.suffix + ".meta")
    template = (META_TEMPLATE_DIR / template_name).read_text(encoding="ascii")
    rel = asset_path.relative_to(REPO).as_posix()
    guid = hashlib.md5(rel.encode("ascii")).hexdigest()
    out = []
    for line in template.splitlines():
        if line.startswith("guid: "):
            out.append(f"guid: {guid}")
        elif not srgb and line.strip() == "sRGBTexture: 1":
            out.append(line.replace("sRGBTexture: 1", "sRGBTexture: 0"))
        elif not srgb and line.strip() == "linearTexture: 0":
            out.append(line.replace("linearTexture: 0", "linearTexture: 1"))
        else:
            out.append(line)
    meta.write_text("\n".join(out) + "\n", encoding="ascii")


def write_part(out_dir, name, rows, centre, radius, axis, about_centroid):
    """One presentation part: plume/lathe mesh + its colour and spine tables."""
    profile = build_profile(rows, radius, centre, about_centroid)
    prof_path = out_dir / f"{name}_profile.png"
    profile.save(prof_path)
    write_meta(prof_path, "BAL1A0_profile.png.meta")

    spine_path = out_dir / f"{name}_spine.png"
    build_spine_texture(centre, axis, len(rows)).save(spine_path)
    write_meta(spine_path, "BAL1A0_profile.png.meta", srgb=False)

    verts, uvs, tris = build_plume(radius, centre, axis)
    obj = out_dir / f"{name}.obj"
    write_obj(obj, f"{name}.mtl", "torchpart", verts, uvs, tris)
    write_meta(obj, "BAL1.obj.meta")
    mtl = out_dir / f"{name}.mtl"
    mtl.write_text("\n".join([
        "newmtl torchpart",
        "Ka 1.000 1.000 1.000",
        "Kd 1.000 1.000 1.000",
        "d 1.0",
        "illum 1",
        f"map_Kd {name}_profile.png",
    ]) + "\n", encoding="ascii")
    write_meta(mtl, "BAL1.mtl.meta")
    return len(tris), max(radius)


def fire_clusters(img, floor_row):
    """The candelabra's three caged fires, as separate column clusters.

    Each lantern is its own little plume with its own spine, so they cannot
    share one table: the shader reads a single axis offset per height, and
    three fires sit at three different x.
    """
    mask = fire_mask(img, floor_row)
    px = img.load()
    width, height = img.size
    columns = [x for x in range(width)
               if any(mask[y][x] for y in range(height))]
    if not columns:
        return []

    groups, current = [], [columns[0]]
    for x in columns[1:]:
        if x - current[-1] <= 1:
            current.append(x)
        else:
            groups.append(current)
            current = [x]
    groups.append(current)

    clusters = []
    for group in groups:
        lo, hi = group[0], group[-1]
        rows = []
        for y in range(height):
            line = [(x, px[x, y][:3]) for x in range(lo, hi + 1) if mask[y][x]]
            rows.append(line)
        used = [y for y, line in enumerate(rows) if line]
        if not used:
            continue
        top, bottom = used[0], used[-1]
        clusters.append({
            "rows": rows[top:bottom + 1],
            "top": top,
            "bottom": bottom,
            "centre": (lo + hi + 1) / 2.0,
        })
    return clusters


def build_lantern(base, img, floor_row, patch, out_dir):
    """The candelabra: three fires, each an independent part, plus the table
    the runtime places them by. The metal — stand, arms and the cages the
    fires sit in — is a generated mesh, because a candelabra is not a solid of
    revolution and the lathe would smear its arms into a disc."""
    width, height, leftoff, topoff = patch
    clusters = fire_clusters(img, floor_row)
    lines = []
    summary = []
    for index, cluster in enumerate(clusters):
        rows = cluster["rows"]
        centre, radius = spine(rows, len(rows), cluster["centre"])
        name = f"{base}_fire{index}"
        tris, rmax = write_part(out_dir, name, rows, centre, radius,
                                cluster["centre"], about_centroid=True)
        # Anchors in patch pixels: x from the thing's axis, y from its feet.
        offset_x = cluster["centre"] - leftoff
        bottom_y = height - 1 - cluster["bottom"]
        lines.append(f"{name} {offset_x:.3f} {bottom_y} {len(rows)}")
        summary.append(f"{index}:{tris}t/x{offset_x:+.1f}/y{bottom_y}")

    table = out_dir / f"{base}_fires.txt"
    table.write_text("\n".join(lines) + "\n", encoding="ascii")
    write_meta(table, "BAL1.mtl.meta")
    print(f"  {base}: patch {width}x{height} off=({leftoff},{topoff}), "
          f"{len(clusters)} caged fires -> " + " ".join(summary))


def main():
    names = [a.upper() for a in sys.argv[1:]] or (
        list(SPLIT_ROW) + list(LANTERN_ROW))
    data = WAD_PATH.read_bytes()
    lumps = read_directory(data)
    palette = read_palette(data, lumps)

    for base in names:
        if base in LANTERN_ROW:
            pos = lumps[f"{base}A0"][0]
            out_dir = OUT_ROOT / base
            out_dir.mkdir(parents=True, exist_ok=True)
            build_lantern(base,
                          decode_patch(data, pos, palette),
                          LANTERN_ROW[base],
                          patch_header(data, pos),
                          out_dir)
            continue
        if base not in SPLIT_ROW:
            print(f"  {base}: no measured split row")
            continue
        split = SPLIT_ROW[base]
        pos = lumps[f"{base}A0"][0]
        width, height, leftoff, topoff = patch_header(data, pos)
        axis = float(leftoff)

        out_dir = OUT_ROOT / base
        out_dir.mkdir(parents=True, exist_ok=True)
        summary = []

        img_a = decode_patch(data, lumps[f"{base}A0"][0], palette)
        srows = stand_rows(img_a, split)
        scentre, sradius = lathe(srows, axis)
        tris, rmax = write_part(out_dir, f"{base}_stand", srows,
                                scentre, sradius, axis, about_centroid=False)
        summary.append(f"stand:{tris}t/{len(srows)}rows/r{rmax:.1f}")

        for f in FRAMES:
            img = decode_patch(data, lumps[f"{base}{f}0"][0], palette)
            rows = flame_rows(img, split)
            centre, radius = spine(rows, split, axis)
            tris, rmax = write_part(out_dir, f"{base}{f}0_flame", rows,
                                    centre, radius, axis, about_centroid=True)
            summary.append(f"{f}:{tris}t/r{rmax:.1f}")

        print(f"  {base}: patch {width}x{height} off=({leftoff},{topoff}), "
              f"split {split} -> " + " ".join(summary))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
