"""Split a Freedoom torch sprite into its stand and its flame.

The six E1M5 firesticks (`TBLU`/`TGRN`/`TRED`/`SMBT`/`SMGT`/`SMRT`) are one
design: a wrought metal stand carrying a bowl, and a flame plume above it.
Only the flame animates across frames A-D — but the frames differ almost
everywhere (the flame relights the brass every tic), so an inter-frame diff
does NOT separate them: on `TBLU` 535 of 873 opaque texels change. The split
is therefore a measured row per lump, read off the sprite once and pinned
here.

Two consumers, two jobs (the 2026-08-19 decision, see the design spec):

  * the STAND is solid, structural and static -> it becomes a TRELLIS.2 mesh,
    and this tool writes its conditioning canvas (same protocol as
    `dump_trellis_native_inputs.py`: crop, integer nearest upscale, 512
    canvas), with every flame row cut away so the Space is never asked to
    reconstruct fire;
  * the FLAME is a wisp with its hot core INSIDE the plume -> no mesh and no
    bake can carry it (the fireball wave, 2026-08-19). It is re-derived by a
    shader, and this tool writes the flame crops that
    `Tools/make_torch_flame.py` bakes its colour table from.

Usage:
  python Tools/split_torch_sprite.py            # all six firesticks
  python Tools/split_torch_sprite.py TBLU TRED
"""

import struct
import sys
from pathlib import Path

from PIL import Image

REPO = Path(__file__).resolve().parent.parent
WAD_PATH = REPO / "Assets" / "StreamingAssets" / "wads" / "freedoom1.wad"
NATIVE_DIR = REPO / "Textures" / "Trellis2" / "NativeInputs"
FLAME_DIR = REPO / "Textures" / "Trellis2" / "TorchFlames"
CANVAS = 512
MAX_AXIS = 416

# First row of the metal head, measured on the Freedoom sprites (row 0 = top).
# Everything above it is flame only; everything from it down is the stand.
# The green flame licks a few rows into its bowl, which the conditioning
# redraw cleans up — cutting lower would eat the bowl itself.
SPLIT_ROW = {
    "TBLU": 38,
    "TGRN": 44,
    "TRED": 38,
    "SMBT": 38,
    "SMGT": 44,
    "SMRT": 38,
}

FRAMES = "ABCD"

# The candelabra is not a torch: its three fires sit INSIDE metal lantern
# cages, so a row split would hand the bars to the flame. Its metal is
# everything except warm, bright texels above the lantern floor — measured
# once, like the split rows above.
LANTERN_ROW = {"CBRA": 18}

# The candle is a torch in miniature and needs no Space at all: rows 0-5 are
# its flame, the rest is a wax cylinder — a lathe body like the firesticks’.
# Its fire does not animate (vanilla CAND is a single frame), so it ships as
# one anchored fire beside the body, the same shape the candelabra uses.
CANDLE_ROW = {"CAND": 6}



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


def read_palette(data, lumps):
    pos, _size = lumps["PLAYPAL"]
    raw = data[pos:pos + 768]
    return [tuple(raw[i * 3:i * 3 + 3]) for i in range(256)]


def decode_patch(data, pos, palette):
    width, height, _leftoff, _topoff = struct.unpack_from("<hhhh", data, pos)
    img = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    px = img.load()
    col_ofs = struct.unpack_from(f"<{width}i", data, pos + 8)
    for x in range(width):
        p = pos + col_ofs[x]
        while data[p] != 0xFF:
            topdelta = data[p]
            length = data[p + 1]
            p += 3
            for i in range(length):
                r, g, b = palette[data[p + i]]
                px[x, topdelta + i] = (r, g, b, 255)
            p += length + 1
    return img


def to_canvas(img):
    """Native-input protocol: crop transparent padding, integer nearest
    upscale to at most MAX_AXIS, centre on a transparent 512 canvas."""
    bbox = img.getbbox()
    if bbox is None:
        raise ValueError("empty image")
    img = img.crop(bbox)
    scale = max(1, MAX_AXIS // max(img.width, img.height))
    img = img.resize((img.width * scale, img.height * scale), Image.NEAREST)
    canvas = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
    canvas.paste(img, ((CANVAS - img.width) // 2, (CANVAS - img.height) // 2))
    return canvas


def cut(img, row, keep):
    """keep='stand' -> rows [row, end); keep='flame' -> rows [0, row)."""
    out = img.copy()
    px = out.load()
    ys = range(0, row) if keep == "stand" else range(row, img.height)
    for y in ys:
        for x in range(img.width):
            px[x, y] = (0, 0, 0, 0)
    return out


def fire_mask(img, floor_row):
    """Warm, bright texels above the lantern floor — the caged fire only.

    The candelabra's brass is warm too, so colour alone takes half the stand
    with it; the floor row is what separates a lantern's inside from the bowls
    below. Above it the only warm thing is the fire, because the cage bars are
    grey steel.
    """
    px = img.load()
    mask = [[False] * img.width for _ in range(img.height)]
    for y in range(min(floor_row, img.height)):
        for x in range(img.width):
            r, g, b, a = px[x, y]
            if not a:
                continue
            lum = 0.30 * r + 0.59 * g + 0.11 * b
            if r > b + 50 and lum > 80:
                mask[y][x] = True
    return mask


def apply_mask(img, mask, keep_masked):
    out = img.copy()
    px = out.load()
    for y in range(img.height):
        for x in range(img.width):
            if mask[y][x] != keep_masked:
                px[x, y] = (0, 0, 0, 0)
    return out


def split_lantern(base, img, floor_row):
    """The candelabra: metal (cages included) versus the fire inside them."""
    mask = fire_mask(img, floor_row)
    fire = apply_mask(img, mask, keep_masked=True)
    metal = apply_mask(img, mask, keep_masked=False)
    fire.save(FLAME_DIR / f"{base}A0-fire.png")
    metal.save(FLAME_DIR / f"{base}A0-stand-native.png")
    out = NATIVE_DIR / f"{base}A0-stand-trellis.png"
    to_canvas(metal).save(out)
    lit = sum(row.count(True) for row in mask)
    print(f"  {base}: {lit} fire texels below row {floor_row} -> "
          f"{out.name} + {base}A0-fire.png")


def main():
    names = [a.upper() for a in sys.argv[1:]] or (
        list(SPLIT_ROW) + list(LANTERN_ROW))
    data = WAD_PATH.read_bytes()
    lumps = read_directory(data)
    palette = read_palette(data, lumps)
    NATIVE_DIR.mkdir(parents=True, exist_ok=True)
    FLAME_DIR.mkdir(parents=True, exist_ok=True)

    for base in names:
        if base in LANTERN_ROW:
            split_lantern(base, decode_patch(data, lumps[f"{base}A0"][0], palette),
                          LANTERN_ROW[base])
            continue
        if base not in SPLIT_ROW:
            print(f"  {base}: no measured split row")
            continue
        row = SPLIT_ROW[base]
        for frame in FRAMES:
            lump = f"{base}{frame}0"
            if lump not in lumps:
                print(f"  {lump}: NOT FOUND")
                continue
            img = decode_patch(data, lumps[lump][0], palette)
            flame = cut(img, row, "flame")
            flame.save(FLAME_DIR / f"{lump}-flame.png")
            if frame == "A":
                stand = cut(img, row, "stand")
                stand.save(FLAME_DIR / f"{base}A0-stand-native.png")
                out = NATIVE_DIR / f"{base}A0-stand-trellis.png"
                to_canvas(stand).save(out)
                print(f"  {base}: split at row {row} of {img.height} -> "
                      f"{out.name} + {len(FRAMES)} flame crops")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
