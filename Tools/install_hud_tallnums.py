"""Fit generated STT* redraws onto 208x256 hard-alpha canvases.

Reads Cursor-generated RGB images from the assets folder, keys the near-black
backdrop, fills enclosed counters with native dark-red, fits each glyph to the
native opaque bbox at 16x, clips to a half-texel dilation of the native mask,
tone-matches, and writes redraw-raw.png / redraw.png next to each native.png.
"""
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

REPO = Path(__file__).resolve().parent.parent
ASSETS = Path.home() / ".cursor" / "projects" / "d-Development-doom" / "assets"
ROOT = REPO / "Textures" / "HudRedraw"
LUMPS = [f"STTNUM{i}" for i in range(10)] + ["STTMINUS", "STTPRCNT"]
TARGET = (208, 256)
LUM = np.array([0.299, 0.587, 0.114])
STD_CAP = 1.6
DARK_FILL = np.array([79, 0, 0, 255], np.uint8)
EMPTY_MAX = 10


def flood_exterior(empty: np.ndarray) -> np.ndarray:
    h, w = empty.shape
    ext = np.zeros((h, w), dtype=bool)
    q = deque()

    def push(y, x):
        if empty[y, x] and not ext[y, x]:
            ext[y, x] = True
            q.append((y, x))

    for x in range(w):
        push(0, x)
        push(h - 1, x)
    for y in range(h):
        push(y, 0)
        push(y, w - 1)
    while q:
        y, x = q.popleft()
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and empty[ny, nx] and not ext[ny, nx]:
                ext[ny, nx] = True
                q.append((ny, nx))
    return ext


def native_bbox(native_rgba: np.ndarray):
    a = native_rgba[..., 3] > 0
    ys, xs = np.where(a)
    return int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1


def process(name: str):
    src = Image.open(ASSETS / f"{name}-redraw.png").convert("RGB")
    arr = np.asarray(src)
    empty = arr.max(axis=2) < EMPTY_MAX
    ext = flood_exterior(empty)
    interior_hole = empty & ~ext

    keyed = np.zeros((arr.shape[0], arr.shape[1], 4), np.uint8)
    keyed[..., :3] = arr
    keyed[..., 3] = np.where(ext, 0, 255)
    if interior_hole.any():
        keyed[interior_hole] = DARK_FILL
    n_fill = int(interior_hole.sum())

    op = keyed[..., 3] > 0
    ys, xs = np.where(op)
    pad = 4
    x0 = max(0, int(xs.min()) - pad)
    x1 = min(keyed.shape[1], int(xs.max()) + 1 + pad)
    y0 = max(0, int(ys.min()) - pad)
    y1 = min(keyed.shape[0], int(ys.max()) + 1 + pad)
    cropped = Image.fromarray(keyed[y0:y1, x0:x1], "RGBA")

    native = np.asarray(Image.open(ROOT / name / "native.png").convert("RGBA"))
    nx0, ny0, nx1, ny1 = native_bbox(native)
    tx0, ty0, tx1, ty1 = nx0 * 16, ny0 * 16, nx1 * 16, ny1 * 16
    fitted = cropped.resize((tx1 - tx0, ty1 - ty0), Image.LANCZOS)
    canvas = Image.new("RGBA", TARGET, (0, 0, 0, 0))
    canvas.paste(fitted, (tx0, ty0), fitted)
    canvas_np = np.asarray(canvas).copy()

    nat_mask = np.repeat(np.repeat(native[..., 3] > 0, 16, axis=0), 16, axis=1)
    dilated = np.asarray(
        Image.fromarray(nat_mask.astype(np.uint8) * 255, "L").filter(
            ImageFilter.MaxFilter(17)
        )
    ) > 0
    canvas_np[..., 3] = np.where(dilated, canvas_np[..., 3], 0)
    canvas_np[..., 3] = np.where(canvas_np[..., 3] >= 128, 255, 0).astype(np.uint8)
    trans = canvas_np[..., 3] == 0
    canvas_np[trans, :3] = 0

    Image.fromarray(canvas_np, "RGBA").save(ROOT / name / "redraw-raw.png")

    mask_n = native[..., 3] > 0
    small = np.asarray(
        Image.fromarray(canvas_np, "RGBA").resize(native.shape[1::-1], Image.LANCZOS),
        np.float64,
    )
    mask_r = small[..., 3] > 128
    matched = canvas_np.copy()
    if mask_n.any() and mask_r.any():
        b = canvas_np.astype(np.float64)
        for c in range(3):
            mean_n = native[..., c][mask_n].astype(np.float64).mean()
            std_n = native[..., c][mask_n].astype(np.float64).std()
            mean_r = small[..., c][mask_r].mean()
            std_r = small[..., c][mask_r].std()
            gain = min(max(std_n / max(std_r, 1e-6), 1.0 / STD_CAP), STD_CAP)
            b[..., c] = (b[..., c] - mean_r) * gain + mean_n
        matched[..., :3] = np.clip(b[..., :3], 0, 255).astype(np.uint8)
        matched[trans, :3] = 0

    Image.fromarray(matched, "RGBA").save(ROOT / name / "redraw.png")

    after_small = np.asarray(
        Image.fromarray(matched, "RGBA").resize(native.shape[1::-1], Image.LANCZOS),
        np.float64,
    )
    la = (native[..., :3].astype(np.float64) * LUM).sum(2)[mask_n].mean()
    after_mask = after_small[..., 3] > 128
    after = (after_small[..., :3] * LUM).sum(2)[after_mask].mean() if after_mask.any() else 0.0
    res = matched[..., 3] > 0
    inter = np.logical_and(nat_mask, res).sum()
    union = np.logical_or(nat_mask, res).sum()
    iou = inter / max(union, 1)
    print(
        f"{name:10s} hole={n_fill:7d}  lum {la:5.1f}->{after:5.1f} "
        f"({after / max(la, 1e-6):4.2f})  IoU={iou:.3f}  opaque={int(res.sum())}"
    )
    return matched


def main():
    for name in LUMPS:
        process(name)


if __name__ == "__main__":
    main()
