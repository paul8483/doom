"""Build the bylo/stalo gate panel for world texture redraws.

Per lump, stacks four full-width rows — native (nearest 4x), current
Super-xBR 4x (right half of Logs/upscale-preview/<LUMP>-4x.png), the
full-color redraw, the PLAYPAL-quantized redraw — plus a strip of 2x zoom
crops from the same spot in all four. Output: Logs/worldredraw-gate/.

Usage:
  python Tools/make_worldredraw_panel.py COMP2 --crop 40,280 COMPTALL --crop 170,30
"""
import os
import sys

from PIL import Image, ImageDraw

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(REPO, "Textures", "WorldRedraw")
PREVIEW = os.path.join(REPO, "Logs", "upscale-preview")
OUT = os.path.join(REPO, "Logs", "worldredraw-gate")
GAP = 16
LABEL_H = 26
CROP = 128     # crop side in 4x pixels
CROP_SCALE = 3


def load_superxbr(lump, w4, h4):
    """Right half of the UpscalePreviewMenu compare image."""
    path = os.path.join(PREVIEW, lump + "-4x.png")
    img = Image.open(path).convert("RGB")
    assert img.size == (w4 * 2 + GAP, h4), (lump, img.size)
    return img.crop((w4 + GAP, 0, w4 * 2 + GAP, h4))


def label_row(draw, y, w, text):
    draw.rectangle((0, y, w, y + LABEL_H), fill=(24, 24, 24))
    draw.text((8, y + 6), text, fill=(230, 230, 230))


def build(lump, crop_xy):
    native = Image.open(os.path.join(SRC, lump, "native.png")).convert("RGB")
    w4, h4 = native.width * 4, native.height * 4
    rows = [
        ("NATIVE x4 (nearest, Classic texels)",
         native.resize((w4, h4), Image.NEAREST)),
        ("SUPER-XBR 4x (current Enhanced)", load_superxbr(lump, w4, h4)),
        ("REDRAW (full color)",
         Image.open(os.path.join(SRC, lump, "redraw.png")).convert("RGB")),
        ("REDRAW (PLAYPAL quantized)",
         Image.open(os.path.join(SRC, lump, "redraw-pal.png")).convert("RGB")),
    ]
    for _, img in rows:
        assert img.size == (w4, h4), (lump, img.size)

    if crop_xy == "auto":
        cx, cy = brightest_window(rows[2][1])
    else:
        cx, cy = crop_xy
    cx = max(0, min(cx, w4 - CROP))
    cy = max(0, min(cy, h4 - CROP))
    crops = [img.crop((cx, cy, cx + CROP, cy + CROP))
                .resize((CROP * CROP_SCALE, CROP * CROP_SCALE), Image.NEAREST)
             for _, img in rows]

    crop_side = CROP * CROP_SCALE
    crops_w = len(crops) * crop_side + (len(crops) - 1) * 8
    panel_w = max(w4, crops_w)
    panel_h = len(rows) * (LABEL_H + h4) + LABEL_H + crop_side + 8

    panel = Image.new("RGB", (panel_w, panel_h), (12, 12, 12))
    draw = ImageDraw.Draw(panel)
    y = 0
    for text, img in rows:
        label_row(draw, y, panel_w, "%s - %s" % (lump, text))
        y += LABEL_H
        panel.paste(img, ((panel_w - w4) // 2, y))
        # mark the crop window on the full view
        ox = (panel_w - w4) // 2
        draw.rectangle((ox + cx, y + cy, ox + cx + CROP, y + cy + CROP),
                       outline=(255, 64, 64), width=2)
        y += h4
    label_row(draw, y, panel_w,
              "%s - crop %dx%d at (%d,%d), zoom x%d: native | superxbr | redraw | redraw-pal"
              % (lump, CROP, CROP, cx, cy, CROP_SCALE))
    y += LABEL_H
    x = (panel_w - crops_w) // 2
    for c in crops:
        panel.paste(c, (x, y))
        x += crop_side + 8

    os.makedirs(OUT, exist_ok=True)
    out_path = os.path.join(OUT, lump + "-bylo-stalo.png")
    panel.save(out_path)
    print("OK", out_path, panel.size)


def brightest_window(img, step=32):
    """Top-left of the CROP x CROP window with the highest luminance sum —
    screens and lit detail are the most informative spot to zoom."""
    gray = img.convert("L")
    small = gray.resize((max(1, gray.width // step), max(1, gray.height // step)))
    px = small.load()
    win = max(1, CROP // step)
    best, bx, by = -1, 0, 0
    for y in range(max(1, small.height - win + 1)):
        for x in range(max(1, small.width - win + 1)):
            s = sum(px[x + dx, y + dy]
                    for dy in range(min(win, small.height - y))
                    for dx in range(min(win, small.width - x)))
            if s > best:
                best, bx, by = s, x, y
    return bx * step, by * step


def parse_args(argv):
    jobs, i = [], 0
    while i < len(argv):
        lump = argv[i].upper()
        i += 1
        crop = "auto"
        if i + 1 < len(argv) + 1 and i < len(argv) and argv[i] == "--crop":
            v = argv[i + 1]
            crop = "auto" if v == "auto" else tuple(int(t) for t in v.split(","))
            i += 2
        jobs.append((lump, crop))
    return jobs


if __name__ == "__main__":
    for lump, crop in parse_args(sys.argv[1:]):
        build(lump, crop)
