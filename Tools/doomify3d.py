"""Doomify 3D: unified doom-style post-process for Enhanced TRELLIS meshes.

Decouples geometry from style (spec 2026-08-12-doomify3d-design.md): the
TRELLIS conditioning image may be as detailed as geometry needs; this filter
enforces the doom look afterwards, identically for every asset:

  geometry -> quadric decimation to a low-poly budget
  texture  -> re-baked: the decimated mesh gets its own per-face chart atlas,
              every texel samples the nearest point of the ORIGINAL surface
              (normal-filtered so a thin shell's inner side never leaks),
              then the atlas is quantized to a WAD palette
  preview  -> tiny software rasterizer (ortho, unlit, nearest sampling),
              matching the in-game unlit shader, so gate panels need no Unity

Emission masks (if present) are re-baked through the same mapping, so
runtime effects like the armor gem blink stay valid.

Usage:
  python Tools/doomify3d.py --lump ARM1A0 [--tris 6000] [--atlas 512]
      [--palette native|playpal] [--out <dir>]

Reads Assets/Resources/ExperimentalPickups/<lump>/<lump>.obj + _albedo.png,
writes doomified obj/albedo(/emission) plus before/after render panels to
--out (default: Logs/doomify3d/<lump>).
"""
from __future__ import annotations

import argparse
import struct
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

REPO = Path(__file__).resolve().parent.parent
WAD = REPO / "Assets/StreamingAssets/wads/freedoom1.wad"
PICKUPS = REPO / "Assets/Resources/ExperimentalPickups"


# --- WAD reading -----------------------------------------------------------

def wad_lump(name: str) -> bytes:
    data = WAD.read_bytes()
    ident, count, dirofs = struct.unpack_from("<4sii", data, 0)
    assert ident in (b"IWAD", b"PWAD"), "not a WAD"
    want = name.upper().encode().ljust(8, b"\0")
    for i in range(count):
        pos, size, nm = struct.unpack_from("<ii8s", data, dirofs + 16 * i)
        if nm == want:
            return data[pos:pos + size]
    raise SystemExit(f"lump {name} not found in {WAD.name}")


def playpal() -> np.ndarray:
    return np.frombuffer(wad_lump("PLAYPAL")[:768], np.uint8).reshape(256, 3)


def sprite_palette(lump: str) -> np.ndarray:
    """Palette entries actually used by the native sprite picture."""
    raw = wad_lump(lump)
    w, h, _, _ = struct.unpack_from("<hhhh", raw, 0)
    cols = struct.unpack_from(f"<{w}i", raw, 8)
    used = set()
    for ofs in cols:
        p = ofs
        while raw[p] != 0xFF:
            length = raw[p + 1]
            used.update(raw[p + 3:p + 3 + length])
            p += 4 + length
    return playpal()[sorted(used)]


def quantize(arr: np.ndarray, palette: np.ndarray) -> np.ndarray:
    """Snap the RGB channels of an RGBA array to the nearest palette color."""
    rgb = arr[..., :3].reshape(-1, 3).astype(np.int32)
    pal = palette.astype(np.int32)
    out = np.empty(len(rgb), np.intp)
    for i in range(0, len(rgb), 65536):
        chunk = rgb[i:i + 65536]
        d = ((chunk[:, None, :] - pal[None, :, :]) ** 2).sum(2)
        out[i:i + 65536] = d.argmin(1)
    res = arr.copy()
    res[..., :3] = palette[out].reshape(arr.shape[:2] + (3,))
    return res


# --- mesh loading / export -------------------------------------------------

def load_obj(path: Path):
    """OBJ -> welded positions, triangles, per-corner UVs (F,3,2)."""
    vs, vts, fv, ft = [], [], [], []
    for line in path.read_text().splitlines():
        if line.startswith("v "):
            vs.append([float(x) for x in line.split()[1:4]])
        elif line.startswith("vt "):
            vts.append([float(x) for x in line.split()[1:3]])
        elif line.startswith("f "):
            vi3, ti3 = [], []
            for tok in line.split()[1:4]:
                parts = tok.split("/")
                vi3.append(int(parts[0]) - 1)
                ti3.append(int(parts[1]) - 1
                           if len(parts) > 1 and parts[1] else int(parts[0]) - 1)
            fv.append(vi3)
            ft.append(ti3)
    verts = np.array(vs)
    vt = np.array(vts) if vts else np.zeros((1, 2))
    tris = np.array(fv, np.int64)
    cuv = vt[np.array(ft, np.int64)]  # (F,3,2)
    return verts, tris, cuv


def save_obj(path: Path, verts, tris, cuv, mtl_name: str):
    lines = [f"mtllib {mtl_name}.mtl", f"usemtl {mtl_name}"]
    lines += [f"v {x:.6f} {y:.6f} {z:.6f}" for x, y, z in verts]
    for f in range(len(tris)):
        for k in range(3):
            lines.append(f"vt {cuv[f, k, 0]:.6f} {cuv[f, k, 1]:.6f}")
    for f, (a, b, c) in enumerate(tris):
        t = 3 * f
        lines.append(f"f {a+1}/{t+1} {b+1}/{t+2} {c+1}/{t+3}")
    path.write_text("\n".join(lines) + "\n")


def face_normals(v, f):
    n = np.cross(v[f[:, 1]] - v[f[:, 0]], v[f[:, 2]] - v[f[:, 0]])
    return n / np.maximum(1e-12, np.linalg.norm(n, axis=1))[:, None]


def decimate(verts, tris, target: int):
    import fast_simplification
    reduction = max(0.0, 1.0 - target / len(tris))
    new_v, new_f = fast_simplification.simplify(
        verts.astype(np.float32), tris.astype(np.int32),
        target_reduction=reduction)
    return new_v.astype(np.float64), new_f.astype(np.int64)


# --- texture bake ----------------------------------------------------------

def bake(new_v, new_f, verts, tris, cuv, textures, atlas: int):
    """Bake per-face chart atlas(es) for the decimated mesh.

    Each decimated face owns half of a square chart cell. Every covered texel
    is mapped to a 3D point on the new face, matched to the nearest
    normal-agreeing original face (KD-tree over centroids), converted to the
    original UV via clamped barycentrics, and sampled from each source
    texture with nearest filtering.

    Returns (per-corner UVs (F,3,2), [baked RGBA arrays per texture]).
    """
    from scipy.spatial import cKDTree

    F = len(new_f)
    per_row = int(np.ceil(np.sqrt(F / 2)))
    cell = max(6, atlas // per_row)
    per_row = atlas // cell
    if per_row * per_row * 2 < F:
        per_row = int(np.ceil(np.sqrt(F / 2)))
        atlas = per_row * cell
    m = 1.25  # px inset keeps nearest sampling off chart borders

    # chart corner pixel coords (x right, y down) per face
    lo = np.array([[m, m], [cell - 1 - m, m], [m, cell - 1 - m]])
    hi = np.array([[cell - 1 - m, cell - 1 - m], [m, cell - 1 - m],
                   [cell - 1 - m, m]])
    sq, corner = np.arange(F) // 2, np.arange(F) % 2
    base = np.stack([(sq % per_row) * cell, (sq // per_row) * cell], 1)  # x,y
    chart_px = np.where(corner[:, None, None] == 0, lo[None], hi[None]) \
        + base[:, None, :]
    uv = np.empty((F, 3, 2))
    uv[..., 0] = (chart_px[..., 0] + 0.5) / atlas
    uv[..., 1] = 1.0 - (chart_px[..., 1] + 0.5) / atlas

    # rasterize charts -> 3D sample points
    tex_ij, pts, face_of = [], [], []
    ys, xs = np.meshgrid(np.arange(cell), np.arange(cell), indexing="ij")
    grid = np.stack([xs, ys], -1).reshape(-1, 2).astype(np.float64)  # x,y
    for f in range(F):
        local = chart_px[f] - base[f]
        a, b, c = local
        d = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
        w0 = ((b[1] - c[1]) * (grid[:, 0] - c[0])
              + (c[0] - b[0]) * (grid[:, 1] - c[1])) / d
        w1 = ((c[1] - a[1]) * (grid[:, 0] - c[0])
              + (a[0] - c[0]) * (grid[:, 1] - c[1])) / d
        w2 = 1 - w0 - w1
        eps = 0.35  # cover a little beyond the inset triangle
        inside = (w0 >= -eps) & (w1 >= -eps) & (w2 >= -eps)
        w = np.clip(np.stack([w0, w1, w2], 1)[inside], 0, None)
        w /= w.sum(1, keepdims=True)
        p3 = w @ new_v[new_f[f]]
        g = grid[inside].astype(int)
        tex_ij.append(np.stack([base[f, 1] + g[:, 1], base[f, 0] + g[:, 0]], 1))
        pts.append(p3)
        face_of.append(np.full(len(p3), f))
    tex_ij = np.concatenate(tex_ij)
    pts = np.concatenate(pts)
    face_of = np.concatenate(face_of)

    # nearest normal-agreeing original face per sample point; prefer strong
    # normal agreement, fall back to weak, then to plain nearest
    oc = verts[tris].mean(1)
    on = face_normals(verts, tris)
    nn = face_normals(new_v, new_f)
    _, cand = cKDTree(oc).query(pts, k=12)
    dots = (on[cand] * nn[face_of][:, None, :]).sum(2)
    strong, weak = dots > 0.4, dots > 0.0
    pick = np.where(strong.any(1), strong.argmax(1),
                    np.where(weak.any(1), weak.argmax(1), 0))
    src = cand[np.arange(len(pts)), pick]

    # clamped barycentric on the source face -> original UV
    tp = verts[tris[src]]
    e0, e1, ep = tp[:, 1] - tp[:, 0], tp[:, 2] - tp[:, 0], pts - tp[:, 0]
    d00 = (e0 * e0).sum(1)
    d01 = (e0 * e1).sum(1)
    d11 = (e1 * e1).sum(1)
    d20 = (ep * e0).sum(1)
    d21 = (ep * e1).sum(1)
    den = np.where(np.abs(d00 * d11 - d01 * d01) < 1e-12, 1.0,
                   d00 * d11 - d01 * d01)
    bv = (d11 * d20 - d01 * d21) / den
    bw = (d00 * d21 - d01 * d20) / den
    bary = np.clip(np.stack([1 - bv - bw, bv, bw], 1), 0, None)
    bary /= bary.sum(1, keepdims=True)
    ouv = (bary[:, :, None] * cuv[src]).sum(1)

    baked = []
    for tex in textures:
        # area-average the bake source: one texel of the chunky atlas covers
        # a large surface patch, and point-sampling the full-res texture
        # aliases the baked-in shading into camo noise
        if max(tex.size) > 256:
            s = 256 / max(tex.size)
            tex = tex.resize((max(1, round(tex.width * s)),
                              max(1, round(tex.height * s))), Image.BOX)
        t = np.asarray(tex.convert("RGBA"))
        th, tw = t.shape[:2]
        ti = np.clip(((1 - ouv[:, 1]) * th).astype(int), 0, th - 1)
        tj = np.clip((ouv[:, 0] * tw).astype(int), 0, tw - 1)
        out = np.zeros((atlas, atlas, 4), np.uint8)
        out[tex_ij[:, 0], tex_ij[:, 1]] = t[ti, tj]
        out[tex_ij[:, 0], tex_ij[:, 1], 3] = 255
        baked.append(out)
    return uv, baked


# --- software preview renderer --------------------------------------------

def render(verts, tris, cuv, tex: Image.Image, yaw_deg: float, size=512):
    t = np.asarray(tex.convert("RGBA"))
    th, tw = t.shape[:2]
    yaw = np.radians(yaw_deg)
    pitch = np.radians(-12.0)
    ry = np.array([[np.cos(yaw), 0, np.sin(yaw)], [0, 1, 0],
                   [-np.sin(yaw), 0, np.cos(yaw)]])
    rx = np.array([[1, 0, 0], [0, np.cos(pitch), -np.sin(pitch)],
                   [0, np.sin(pitch), np.cos(pitch)]])
    v = (verts - verts.mean(0)) @ ry.T @ rx.T
    scale = (size * 0.42) / max(1e-9, np.abs(v[:, :2]).max())
    x = v[:, 0] * scale + size / 2
    y = size / 2 - v[:, 1] * scale
    z = v[:, 2]

    color = np.zeros((size, size, 4), np.uint8)
    depth = np.full((size, size), -np.inf)
    for f, (a, b, c) in enumerate(tris):
        xs = np.array([x[a], x[b], x[c]])
        ys = np.array([y[a], y[b], y[c]])
        x0, x1 = int(max(0, xs.min())), int(min(size - 1, np.ceil(xs.max())))
        y0, y1 = int(max(0, ys.min())), int(min(size - 1, np.ceil(ys.max())))
        if x1 < x0 or y1 < y0:
            continue
        px, py = np.meshgrid(np.arange(x0, x1 + 1), np.arange(y0, y1 + 1))
        d = (ys[1] - ys[2]) * (xs[0] - xs[2]) + (xs[2] - xs[1]) * (ys[0] - ys[2])
        if abs(d) < 1e-9:
            continue
        w0 = ((ys[1] - ys[2]) * (px - xs[2]) + (xs[2] - xs[1]) * (py - ys[2])) / d
        w1 = ((ys[2] - ys[0]) * (px - xs[2]) + (xs[0] - xs[2]) * (py - ys[2])) / d
        w2 = 1 - w0 - w1
        m = (w0 >= 0) & (w1 >= 0) & (w2 >= 0)
        if not m.any():
            continue
        pz = w0 * z[a] + w1 * z[b] + w2 * z[c]
        my, mx = py[m], px[m]
        keep = pz[m] > depth[my, mx]
        my, mx = my[keep], mx[keep]
        if len(my) == 0:
            continue
        depth[my, mx] = pz[m][keep]
        u0, u1, u2 = cuv[f, :, 0]
        v0, v1, v2 = cuv[f, :, 1]
        u = (w0 * u0 + w1 * u1 + w2 * u2)[m][keep]
        vv = (w0 * v0 + w1 * v1 + w2 * v2)[m][keep]
        ti = np.clip(((1 - vv) * th).astype(int), 0, th - 1)
        tj = np.clip((u * tw).astype(int), 0, tw - 1)
        color[my, mx] = t[ti, tj]
        color[my, mx, 3] = 255
    return color


def checker(size, cell=16):
    img = Image.new("RGB", (size, size), (40, 40, 40))
    d = ImageDraw.Draw(img)
    for yy in range(0, size, cell):
        for xx in range(0, size, cell):
            if (xx // cell + yy // cell) % 2 == 0:
                d.rectangle([xx, yy, xx + cell - 1, yy + cell - 1],
                            fill=(56, 56, 56))
    return img


def compose(panels, labels, out: Path):
    size = panels[0].shape[0]
    pad, label_h = 12, 26
    W = len(panels) * (size + pad) + pad
    H = size + label_h + pad
    img = Image.new("RGB", (W, H), (24, 24, 24))
    d = ImageDraw.Draw(img)
    for i, (p, lab) in enumerate(zip(panels, labels)):
        x = pad + i * (size + pad)
        cell = checker(size)
        fg = Image.fromarray(p)
        cell.paste(fg, (0, 0), fg)
        img.paste(cell, (x, label_h))
        d.text((x, 7), lab, fill=(230, 230, 230))
    img.save(out)


# --- main ------------------------------------------------------------------

def main():
    p = argparse.ArgumentParser()
    p.add_argument("--lump", required=True)
    p.add_argument("--tris", type=int, default=6000)
    p.add_argument("--atlas", type=int, default=512)
    p.add_argument("--palette", choices=["native", "playpal"], default="native")
    p.add_argument("--out", default=None)
    a = p.parse_args()

    src = PICKUPS / a.lump
    out = Path(a.out) if a.out else REPO / "Logs" / "doomify3d" / a.lump
    out.mkdir(parents=True, exist_ok=True)

    pal = sprite_palette(a.lump) if a.palette == "native" else playpal()
    print(f"palette: {a.palette} ({len(pal)} colors)")

    albedo = Image.open(src / f"{a.lump}_albedo.png")
    emission_path = src / f"{a.lump}_emission.png"
    textures = [albedo]
    if emission_path.exists():
        textures.append(Image.open(emission_path))

    verts, tris, cuv = load_obj(src / f"{a.lump}.obj")
    print(f"mesh: {len(tris)} tris, {len(verts)} verts")
    dv, dt = decimate(verts, tris, a.tris)
    print(f"decimated: {len(dt)} tris, {len(dv)} verts")

    duv, baked = bake(dv, dt, verts, tris, cuv, textures, a.atlas)
    baked[0] = quantize(baked[0], pal)
    new_tex = Image.fromarray(baked[0])
    new_tex.save(out / f"{a.lump}_albedo.png")
    print(f"baked albedo: {new_tex.size}, palette applied")
    if len(baked) > 1:
        Image.fromarray(baked[1]).save(out / f"{a.lump}_emission.png")
        print("baked emission mask")

    save_obj(out / f"{a.lump}.obj", dv, dt, duv, a.lump)

    for yaw in (0.0, 35.0):
        old = render(verts, tris, cuv, albedo, yaw)
        new = render(dv, dt, duv, new_tex, yaw)
        name = out / f"{a.lump}-doomify-yaw{int(yaw)}.png"
        compose([old, new],
                [f"{a.lump} BYLO (raw {len(tris)//1000}k tris, "
                 f"{albedo.width}px)",
                 f"STALO (doomify {len(dt)//1000}k tris, baked "
                 f"{new_tex.width}px, {len(pal)} colors)"],
                name)
        print("panel:", name)


if __name__ == "__main__":
    main()
