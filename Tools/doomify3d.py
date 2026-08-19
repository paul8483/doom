"""Doomify 3D: unified doom-style post-process for Enhanced TRELLIS meshes.

Decouples geometry from style (spec 2026-08-12-doomify3d-design.md): the
TRELLIS conditioning image may be as detailed as geometry needs; this filter
enforces the doom look afterwards, identically for every asset:

  geometry -> pymeshlab quadric edge collapse WITH texture coordinates, so
              the original UV atlas survives and no re-bake is needed.
              TRELLIS voxel-reconstruction meshes are full of micro-crack
              boundary edges, so boundary preservation must stay OFF or the
              filter stalls at ~half the input faces (ARM1A0 finding).
  albedo   -> resolution cap (BOX average) + quantization to a WAD palette
              (full PLAYPAL or the colors of the asset's native sprite lump)
  emission -> untouched: the UV space is preserved, existing masks stay valid
  preview  -> tiny software rasterizer (ortho, unlit, nearest sampling),
              matching the in-game unlit shader, so gate panels need no Unity

History: v1 tried fast_simplification + per-face chart re-bake — REJECT at
the ARM1A0 gate (thin features crushed, chart texture too crude).

Usage:
  python Tools/doomify3d.py --lump ARM1A0 [--tris 20000] [--texcap 256]
      [--palette native|playpal] [--out <dir>]

Reads Assets/Resources/ExperimentalPickups/<lump>/<lump>.obj + _albedo.png,
writes doomified obj/albedo plus before/after render panels to --out
(default: Logs/doomify3d/<lump>).
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


def image_palette(path: Path) -> np.ndarray:
    """Palette entries actually used by an opaque region of a PNG.

    A torch stand shares its lump with the flame above it, so quantizing to
    `sprite_palette("TBLUA0")` would offer the brass every blue and white the
    fire uses. Pointing this at the stand-only crop written by
    `Tools/split_torch_sprite.py` keeps the metal in metal colours.
    """
    arr = np.asarray(Image.open(path).convert("RGBA"))
    rgb = arr[arr[..., 3] > 0][:, :3]
    if len(rgb) == 0:
        raise SystemExit(f"palette image {path} is empty")
    return np.unique(rgb.reshape(-1, 3), axis=0)


def tone_match(img: Image.Image, ref_path: Path) -> Image.Image:
    """Match an albedo's per-channel mean/std to a reference picture.

    A TRELLIS bake carries the exposure of whatever the conditioning image was
    drawn at, and it is always FLATTER than DOOM art: the torch stands came
    back at std 16-27 against the sprite's 40-55, which reads as washed-out
    plaster next to the neighbouring pixels (2026-08-19). Matching statistics
    against the asset's own native crop restores the contrast and the warmth
    without inventing a colour — same instrument the monster track uses in
    global_tone_match.py, pointed at the sprite instead of an anchor frame.
    """
    ref = np.asarray(Image.open(ref_path).convert("RGBA")).astype(np.float64)
    rm = ref[..., 3] > 0
    if not rm.any():
        raise SystemExit(f"tone image {ref_path} is empty")
    rmean, rstd = ref[..., :3][rm].mean(0), ref[..., :3][rm].std(0)

    a = np.asarray(img.convert("RGBA")).astype(np.float64)
    m = a[..., 3] > 0
    src = a[..., :3][m]
    out = (src - src.mean(0)) / np.maximum(src.std(0), 1e-6) * rstd + rmean
    a[..., :3][m] = np.clip(out, 0, 255)
    return Image.fromarray(a.astype(np.uint8))


# --- texture pass ----------------------------------------------------------

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


def doomify_texture(img: Image.Image, palette: np.ndarray, cap: int) -> Image.Image:
    rgba = img.convert("RGBA")
    if max(rgba.size) > cap:
        s = cap / max(rgba.size)
        rgba = rgba.resize((max(1, round(rgba.width * s)),
                            max(1, round(rgba.height * s))), Image.BOX)
    return Image.fromarray(quantize(np.asarray(rgba), palette))


# --- mesh pass -------------------------------------------------------------

def decimate_with_uv(src_obj: Path, dst_obj: Path, target: int) -> int:
    """UV-preserving quadric decimation via pymeshlab.

    preserveboundary must stay False: TRELLIS meshes carry tens of thousands
    of micro-crack boundary edges and locking them stalls the collapse.
    Iterates because the with-texture filter may stop above the target.
    """
    import pymeshlab
    ms = pymeshlab.MeshSet()
    ms.load_new_mesh(str(src_obj))
    # Weld the voxel-reconstruction micro-cracks first: otherwise the
    # unlocked boundary edges drift during collapse and open visible slits
    # (ARM1A0 pauldron finding, 2026-08-13).
    before = ms.current_mesh().vertex_number()
    ms.meshing_merge_close_vertices()
    print(f"weld: {before} -> {ms.current_mesh().vertex_number()} verts")
    prev = ms.current_mesh().face_number()
    for _ in range(8):
        ms.meshing_decimation_quadric_edge_collapse_with_texture(
            targetfacenum=target, preserveboundary=False,
            planarquadric=True, qualitythr=0.5)
        n = ms.current_mesh().face_number()
        if n <= target or n >= prev:
            break
        prev = n
    ms.save_current_mesh(str(dst_obj))
    # pymeshlab writes `mtllib ./<lump>.obj.mtl`, but the repo keeps the
    # material as `<lump>.mtl` — Unity then finds nothing and imports the
    # frame with its default WHITE material, which no test used to notice
    # (POSS death frames shipped white, 2026-08-16). Name it as it ships.
    lines = dst_obj.read_text(encoding="utf-8", errors="ignore").splitlines()
    for i, line in enumerate(lines):
        if line.startswith("mtllib "):
            lines[i] = f"mtllib {dst_obj.stem}.mtl"
            break
    dst_obj.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return ms.current_mesh().face_number()


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
    # accepted preset (ARM1A0 in-game gate 2026-08-13, variant A):
    # 40k tris / 256px / native palette; import side: Point, no compression
    p.add_argument("--tris", type=int, default=40000)
    p.add_argument("--texcap", type=int, default=256)
    p.add_argument("--palette", choices=["native", "playpal"], default="native")
    # stop-motion monster frames must share ONE palette or textures flicker
    # between frame meshes; pass the anchor frame's lump (e.g. POSSA1).
    p.add_argument("--palette-lump", default=None,
                   help="quantize to this lump's native palette instead of --lump's")
    p.add_argument("--tone-image", default=None,
                   help="match the albedo's mean/std to this picture's opaque "
                        "texels before quantizing (usually the native crop)")
    p.add_argument("--palette-image", default=None,
                   help="quantize to the colors of this PNG's opaque texels "
                        "(for parts that share a lump, e.g. a torch stand)")
    p.add_argument("--out", default=None)
    # monsters live under Assets/Resources/ExperimentalMonsters/<SPRITE>/
    p.add_argument("--src", default=None,
                   help="source dir with <lump>.obj + <lump>_albedo.png "
                        "(default: Assets/Resources/ExperimentalPickups/<lump>)")
    a = p.parse_args()

    src = Path(a.src) if a.src else PICKUPS / a.lump
    out = Path(a.out) if a.out else REPO / "Logs" / "doomify3d" / a.lump
    out.mkdir(parents=True, exist_ok=True)

    if a.palette_image:
        pal = image_palette(Path(a.palette_image))
        print(f"palette: image {Path(a.palette_image).name} ({len(pal)} colors)")
    else:
        pal = (sprite_palette(a.palette_lump or a.lump)
               if a.palette == "native" else playpal())
        print(f"palette: {a.palette} ({len(pal)} colors)")

    albedo = Image.open(src / f"{a.lump}_albedo.png")

    # Decimate BEFORE writing the styled texture: pymeshlab's save copies the
    # material's source texture next to the OBJ and silently clobbers a file
    # with the same name (this shipped raw 1024px albedo twice, 2026-08-13).
    n = decimate_with_uv(src / f"{a.lump}.obj", out / f"{a.lump}.obj", a.tris)

    if a.tone_image:
        albedo = tone_match(albedo, Path(a.tone_image))
        print(f"tone: matched to {Path(a.tone_image).name}")
    new_tex = doomify_texture(albedo, pal, a.texcap)
    new_tex.save(out / f"{a.lump}_albedo.png")
    print(f"albedo: {albedo.size} -> {new_tex.size}")
    verts, tris, cuv = load_obj(src / f"{a.lump}.obj")
    dv, dt, dcuv = load_obj(out / f"{a.lump}.obj")
    print(f"mesh: {len(tris)} -> {n} tris")

    for yaw in (0.0, 35.0):
        old = render(verts, tris, cuv, albedo, yaw)
        new = render(dv, dt, dcuv, new_tex, yaw)
        name = out / f"{a.lump}-doomify-yaw{int(yaw)}.png"
        compose([old, new],
                [f"{a.lump} BYLO (raw {len(tris)//1000}k tris, "
                 f"{albedo.width}px)",
                 f"STALO ({n//1000}k tris, UV kept, {new_tex.width}px, "
                 f"{len(pal)} colors)"],
                name)
        print("panel:", name)


if __name__ == "__main__":
    main()
