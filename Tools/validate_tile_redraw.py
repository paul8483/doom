"""Tech-gate for wall/flat texture redraws (run BEFORE any visual gate).

Checks, per lump directory Textures/WorldRedraw/<LUMP>/:
  1. size: redraw is exactly SCALE x the native.png dimensions;
  2. opacity: no transparent pixels (walls are opaque surfaces);
  3. seam: the wrapped edge-pair difference (rightmost column against leftmost,
     and bottom row against top for flats / --vertical) must stay close to the
     image's own mean neighbour-column difference. The native tiles by
     construction, so its ratio calibrates the scale: we report both and fail
     when the redraw ratio exceeds --max-ratio (default 2.0) AND exceeds the
     native ratio by more than --slack (default 1.5x).

Usage:
  python Tools/validate_tile_redraw.py COMP2 [--file redraw.png] [--vertical]
"""
import argparse
import os
import sys

from PIL import Image

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ROOT = os.path.join(REPO, "Textures", "WorldRedraw")
SCALE = 4


def premultiplied(img):
    """RGB * alpha so hole texels (arbitrary RGB under alpha 0) drop out."""
    rgba = img.convert("RGBA")
    px = rgba.load()
    out = Image.new("RGB", rgba.size)
    op = out.load()
    for y in range(rgba.size[1]):
        for x in range(rgba.size[0]):
            r, g, b, a = px[x, y]
            op[x, y] = (r * a // 255, g * a // 255, b * a // 255)
    return out


def seam_ratio(img, vertical):
    """Wrapped-edge mean abs diff / interior mean neighbour diff."""
    rgb = img.convert("RGB")
    w, h = rgb.size
    px = rgb.load()

    def col_diff(x0, x1):
        s = 0
        for y in range(h):
            a, b = px[x0, y], px[x1, y]
            s += abs(a[0] - b[0]) + abs(a[1] - b[1]) + abs(a[2] - b[2])
        return s / h

    def row_diff(y0, y1):
        s = 0
        for x in range(w):
            a, b = px[x, y0], px[x, y1]
            s += abs(a[0] - b[0]) + abs(a[1] - b[1]) + abs(a[2] - b[2])
        return s / w

    if vertical:
        interior = sum(row_diff(y, y + 1) for y in range(h - 1)) / (h - 1)
        seam = row_diff(h - 1, 0)
    else:
        interior = sum(col_diff(x, x + 1) for x in range(w - 1)) / (w - 1)
        seam = col_diff(w - 1, 0)
    return seam / max(interior, 1e-6), seam, interior


def transparent_fraction(img):
    if img.mode not in ("RGBA", "LA", "PA"):
        return 0.0
    hist = img.getchannel("A").histogram()
    return hist[0] / (img.size[0] * img.size[1])


def check(lump, fname, vertical, max_ratio, slack, masked):
    d = os.path.join(ROOT, lump)
    native = Image.open(os.path.join(d, "native.png"))
    path = os.path.join(d, fname)
    if not os.path.isfile(path):
        print("FAIL %s: missing %s" % (lump, fname))
        return False
    redraw = Image.open(path)
    ok = True

    want = (native.width * SCALE, native.height * SCALE)
    if redraw.size != want:
        print("FAIL %s size: %s, want %s" % (lump, redraw.size, want))
        ok = False

    if masked:
        # Masked mid-textures: the redraw must keep a hole fraction close to
        # the native silhouette; the seam runs on premultiplied RGB so hole
        # texels drop out of the metric.
        nt = transparent_fraction(native)
        rt = transparent_fraction(redraw)
        if nt == 0:
            print("FAIL %s masked: native has no transparency" % lump)
            ok = False
        elif abs(rt - nt) > 0.10:
            print("FAIL %s alpha coverage: redraw %.2f transparent vs native %.2f"
                  % (lump, rt, nt))
            ok = False
        else:
            print("ok %s alpha coverage: redraw %.2f vs native %.2f"
                  % (lump, rt, nt))
        native = premultiplied(native)
        redraw = premultiplied(redraw)
    elif redraw.mode in ("RGBA", "LA", "PA"):
        alpha = redraw.getchannel("A")
        lo, _hi = alpha.getextrema()
        if lo < 255:
            print("FAIL %s opacity: min alpha %d" % (lump, lo))
            ok = False

    axes = [False, True] if vertical else [False]
    for vert in axes:
        n_ratio, _, _ = seam_ratio(native, vert)
        r_ratio, seam, interior = seam_ratio(redraw, vert)
        axis = "vertical" if vert else "horizontal"
        verdict = "ok"
        if r_ratio > max_ratio and r_ratio > n_ratio * slack:
            verdict = "FAIL"
            ok = False
        print("%s %s %s seam: redraw ratio %.2f (seam %.1f / interior %.1f), "
              "native ratio %.2f" % (verdict, lump, axis, r_ratio, seam,
                                     interior, n_ratio))

    if ok:
        print("PASS %s %s" % (lump, fname))
    return ok


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("lumps", nargs="+")
    ap.add_argument("--file", default="redraw.png")
    ap.add_argument("--scale", type=int, default=4,
                    help="authoring scale (SKY1 uses 8)")
    ap.add_argument("--vertical", action="store_true",
                    help="also check the vertical wrap (flats)")
    ap.add_argument("--max-ratio", type=float, default=2.0)
    ap.add_argument("--slack", type=float, default=1.5)
    ap.add_argument("--masked", action="store_true",
                    help="masked mid-texture: require alpha holes close to the "
                         "native fraction, seam over premultiplied RGB")
    a = ap.parse_args()
    globals()["SCALE"] = a.scale
    good = all(check(l.upper(), a.file, a.vertical, a.max_ratio, a.slack,
                     a.masked)
               for l in a.lumps)
    sys.exit(0 if good else 1)
