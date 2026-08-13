"""Dump native TRELLIS.2 conditioning canvases from WAD sprite lumps.

Protocol (docs/superpowers/plans/2026-08-06-trellis2-pickups.md):
  1. Decode the sprite patch with the first PLAYPAL.
  2. Crop only fully transparent padding.
  3. Integer nearest-neighbor upscale so the major axis is at most 416 px.
  4. Center on a transparent 512x512 RGBA canvas.

Usage:
  python Tools/dump_trellis_native_inputs.py POSS B C D E F G
"""

import struct
import sys
from pathlib import Path

from PIL import Image

REPO = Path(__file__).resolve().parent.parent
WAD_PATH = REPO / "Assets" / "StreamingAssets" / "wads" / "freedoom1.wad"
OUT_DIR = REPO / "Textures" / "Trellis2" / "NativeInputs"
CANVAS = 512
MAX_AXIS = 416


def read_directory(data):
    ident, numlumps, infotableofs = struct.unpack_from("<4sii", data, 0)
    if ident not in (b"IWAD", b"PWAD"):
        raise ValueError(f"not a WAD: {ident}")
    lumps = []
    for i in range(numlumps):
        filepos, size, raw_name = struct.unpack_from(
            "<ii8s", data, infotableofs + 16 * i)
        name = raw_name.rstrip(b"\0").decode("ascii", "replace")
        lumps.append((name, filepos, size))
    return lumps


def find_lump(lumps, name):
    for lump_name, pos, size in lumps:
        if lump_name == name:
            return pos, size
    return None


def sprite_lump(lumps, sprite, frame, rotation):
    """Resolve frame+rotation inside S_START..S_END, honoring combined
    mirror names like POSSB2B8 (we only need rotation 1, but keep it exact)."""
    in_sprites = False
    want = f"{frame}{rotation}"
    for name, pos, size in lumps:
        if name in ("S_START", "SS_START"):
            in_sprites = True
            continue
        if name in ("S_END", "SS_END"):
            in_sprites = False
            continue
        if not in_sprites or not name.startswith(sprite):
            continue
        tail = name[len(sprite):]
        # tail is FR or FRFR (mirrored pair)
        if tail[:2] == want or (len(tail) == 4 and tail[2:] == want):
            mirrored = len(tail) == 4 and tail[2:] == want
            return pos, size, mirrored
    return None


def decode_patch(data, pos, palette):
    width, height, _leftoff, _topoff = struct.unpack_from("<hhhh", data, pos)
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
            p += 3  # topdelta, length, unused pad
            for i in range(length):
                idx = data[p]
                p += 1
                r, g, b = palette[idx]
                px[x, topdelta + i] = (r, g, b, 255)
            p += 1  # trailing pad
    return img


def to_canvas(img, mirrored):
    if mirrored:
        img = img.transpose(Image.FLIP_LEFT_RIGHT)
    bbox = img.getbbox()
    img = img.crop(bbox)
    scale = max(1, MAX_AXIS // max(img.width, img.height))
    img = img.resize((img.width * scale, img.height * scale), Image.NEAREST)
    canvas = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
    canvas.paste(img, ((CANVAS - img.width) // 2, (CANVAS - img.height) // 2))
    return canvas


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 1
    sprite = sys.argv[1].upper()
    frames = [f.upper() for f in sys.argv[2:]]

    data = WAD_PATH.read_bytes()
    lumps = read_directory(data)

    pal_entry = find_lump(lumps, "PLAYPAL")
    if pal_entry is None:
        raise SystemExit("PLAYPAL not found")
    pal_raw = data[pal_entry[0]:pal_entry[0] + 768]
    palette = [tuple(pal_raw[i * 3:i * 3 + 3]) for i in range(256)]

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    for frame in frames:
        entry = sprite_lump(lumps, sprite, frame, "1")
        if entry is None:
            print(f"  {sprite}{frame}1: NOT FOUND")
            continue
        pos, _size, mirrored = entry
        img = decode_patch(data, pos, palette)
        out = OUT_DIR / f"{sprite}{frame}1-trellis.png"
        to_canvas(img, mirrored).save(out)
        print(f"  {sprite}{frame}1 -> {out.name}"
              f" ({img.width}x{img.height}{', mirrored' if mirrored else ''})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
