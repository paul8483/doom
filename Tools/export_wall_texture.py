"""Export a composite wall texture or a flat from freedoom1.wad for the redraw pipeline.

Composes wall textures from TEXTURE1/TEXTURE2 + PNAMES patches through PLAYPAL
(same composition rules as Doom.Graphics.TextureSet); names not present in
TEXTURE1/2 fall back to raw 64x64 flat lumps (palette indices). Writes:

  Textures/WorldRedraw/<LUMP>/native.png     — 1x native pixels
  Textures/WorldRedraw/<LUMP>/native-x4.png  — integer nearest 4x (GPT input:
                                               "redraw exactly this at this size")

Usage:
  python Tools/export_wall_texture.py COMP2 COMPTALL
"""
import os
import struct
import sys

from PIL import Image

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
WAD = os.path.join(REPO, "Assets", "StreamingAssets", "wads", "freedoom1.wad")
OUT_ROOT = os.path.join(REPO, "Textures", "WorldRedraw")
SCALE = 4


def load_wad(path):
    with open(path, "rb") as f:
        data = f.read()
    _, numlumps, diroff = struct.unpack_from("<4sii", data, 0)
    lumps = []
    index = {}
    for i in range(numlumps):
        off, size, name = struct.unpack_from("<ii8s", data, diroff + 16 * i)
        nm = name.rstrip(b"\0").decode("ascii", "replace").upper()
        lumps.append((nm, off, size))
        index.setdefault(nm, i)
    return data, lumps, index


def compose(data, lumps, index, texname):
    def lump_bytes(nm):
        _, off, size = lumps[index[nm]]
        return data[off:off + size]

    pn = lump_bytes("PNAMES")
    npn = struct.unpack_from("<i", pn, 0)[0]
    pnames = [pn[4 + 8 * i:4 + 8 * i + 8].rstrip(b"\0").decode("ascii", "replace").upper()
              for i in range(npn)]

    for tl in ("TEXTURE1", "TEXTURE2"):
        if tl not in index:
            continue
        b = lump_bytes(tl)
        n = struct.unpack_from("<i", b, 0)[0]
        offs = struct.unpack_from("<%di" % n, b, 4)
        for o in offs:
            nm = b[o:o + 8].rstrip(b"\0").decode("ascii", "replace").upper()
            if nm != texname:
                continue
            w, h = struct.unpack_from("<hh", b, o + 12)
            pc = struct.unpack_from("<h", b, o + 20)[0]
            canvas = bytearray(w * h)  # palette indices, 0 default
            cover = bytearray(w * h)   # 1 where any patch wrote (masked alpha)
            for pi in range(pc):
                px, py, pnum = struct.unpack_from("<hhh", b, o + 22 + 10 * pi)
                draw_patch(canvas, cover, w, h, lump_bytes(pnames[pnum]), px, py)
            return w, h, canvas, cover

    # Not a composite texture — try a raw 64x64 flat lump (always opaque).
    if texname in index:
        _, off, size = lumps[index[texname]]
        if size == 64 * 64:
            return 64, 64, bytearray(data[off:off + size]), bytearray(b"\1" * size)
    raise SystemExit("not a TEXTURE1/2 texture nor a 64x64 flat: " + texname)


def draw_patch(canvas, cover, w, h, b, ox, oy):
    pw, _ph, _lo, _to = struct.unpack_from("<hhhh", b, 0)
    colofs = struct.unpack_from("<%di" % pw, b, 8)
    for col in range(pw):
        x = ox + col
        if x < 0 or x >= w:
            continue
        p = colofs[col]
        while True:
            topdelta = b[p]
            if topdelta == 0xFF:
                break
            length = b[p + 1]
            p += 3
            for j in range(length):
                y = oy + topdelta + j
                if 0 <= y < h:
                    canvas[y * w + x] = b[p]
                    cover[y * w + x] = 1
                p += 1
            p += 1


def export(texname):
    data, lumps, index = load_wad(WAD)
    pal = data[lumps[index["PLAYPAL"]][1]:][:768]
    w, h, canvas, cover = compose(data, lumps, index, texname)

    # Masked textures (uncovered texels) export as RGBA with alpha 0 holes;
    # fully covered ones stay RGB so opaque-wave outputs are byte-identical.
    masked = 0 in cover
    img = Image.new("RGBA" if masked else "RGB", (w, h))
    px = img.load()
    for y in range(h):
        for x in range(w):
            i = y * w + x
            c = canvas[i]
            rgb = (pal[c * 3], pal[c * 3 + 1], pal[c * 3 + 2])
            if masked:
                px[x, y] = rgb + ((255,) if cover[i] else (0,))
            else:
                px[x, y] = rgb

    out_dir = os.path.join(OUT_ROOT, texname)
    os.makedirs(out_dir, exist_ok=True)
    img.save(os.path.join(out_dir, "native.png"))
    img.resize((w * SCALE, h * SCALE), Image.NEAREST).save(
        os.path.join(out_dir, "native-x4.png"))
    print("OK %s %dx%d -> %s (native + native-x4 %dx%d)"
          % (texname, w, h, out_dir, w * SCALE, h * SCALE))


if __name__ == "__main__":
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    for t in sys.argv[1:]:
        export(t.upper())
