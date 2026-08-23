"""Make the PLAYPAL-quantized variant of a full-color wall redraw.

One GPT generation yields both pilot palette variants: the full-color original
(redraw.png) and this deterministic quantization (redraw-pal.png), so the
user's palette decision is isolated from generation lottery.

Nearest-color match in sRGB with the same 0.30/0.59/0.11 luma weighting the
project uses elsewhere (EdgeMix contrast gate, doomify quantization).

Usage:
  python Tools/quantize_to_playpal.py COMP2 [--file redraw.png] [--out redraw-pal.png]
"""
import argparse
import os
import struct

from PIL import Image

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
WAD = os.path.join(REPO, "Assets", "StreamingAssets", "wads", "freedoom1.wad")
ROOT = os.path.join(REPO, "Textures", "WorldRedraw")
WEIGHTS = (0.30, 0.59, 0.11)


def load_playpal():
    with open(WAD, "rb") as f:
        data = f.read()
    _, numlumps, diroff = struct.unpack_from("<4sii", data, 0)
    for i in range(numlumps):
        off, size, name = struct.unpack_from("<ii8s", data, diroff + 16 * i)
        if name.rstrip(b"\0") == b"PLAYPAL":
            pal = data[off:off + 768]
            return [(pal[j * 3], pal[j * 3 + 1], pal[j * 3 + 2])
                    for j in range(256)]
    raise SystemExit("PLAYPAL not found")


def quantize(lump, fname, outname):
    palette = load_playpal()
    src = Image.open(os.path.join(ROOT, lump, fname)).convert("RGB")
    px = src.load()
    out = Image.new("RGB", src.size)
    opx = out.load()
    cache = {}
    wr, wg, wb = WEIGHTS
    for y in range(src.height):
        for x in range(src.width):
            c = px[x, y]
            hit = cache.get(c)
            if hit is None:
                r, g, b = c
                best, bestd = palette[0], 1e18
                for p in palette:
                    d = (wr * (r - p[0]) ** 2 + wg * (g - p[1]) ** 2
                         + wb * (b - p[2]) ** 2)
                    if d < bestd:
                        best, bestd = p, d
                cache[c] = hit = best
            opx[x, y] = hit
    out_path = os.path.join(ROOT, lump, outname)
    out.save(out_path)
    print("OK %s: %d distinct source colors -> %s" % (lump, len(cache), out_path))


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("lumps", nargs="+")
    ap.add_argument("--file", default="redraw.png")
    ap.add_argument("--out", default="redraw-pal.png")
    a = ap.parse_args()
    for l in a.lumps:
        quantize(l.upper(), a.file, a.out)
