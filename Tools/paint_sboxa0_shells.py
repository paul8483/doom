"""Paint readable SHELLS labels onto SBOXA0 albedo UV text strips."""
from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

REPO = Path(__file__).resolve().parents[1]
ALBEDO = REPO / "Assets/Resources/ExperimentalPickups/SBOXA0/SBOXA0_albedo.png"
BACKUP = REPO / "Logs/SBOXA0_albedo_pre_shells.png"
PREVIEW = REPO / "Logs/sboxa0-fixed-panels"

GOLD = (230, 190, 50, 255)
LETTER_FILL = (240, 200, 40, 255)
LETTER_OUTLINE = (120, 70, 20, 255)

LETTERS = {
    "S": [
        "01110",
        "10001",
        "10000",
        "01110",
        "00001",
        "10001",
        "01110",
    ],
    "H": [
        "10001",
        "10001",
        "10001",
        "11111",
        "10001",
        "10001",
        "10001",
    ],
    "E": [
        "11111",
        "10000",
        "10000",
        "11110",
        "10000",
        "10000",
        "11111",
    ],
    "L": [
        "10000",
        "10000",
        "10000",
        "10000",
        "10000",
        "10000",
        "11111",
    ],
}


def render_shells(scale: int = 3, gap: int = 1) -> Image.Image:
    word = "SHELLS"
    letter_w, letter_h = 5, 7
    width = len(word) * letter_w * scale + (len(word) - 1) * gap * scale + 2 * scale
    height = letter_h * scale + 2 * scale
    img = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    px = img.load()

    def blit_letter(ch: str, ox: int, oy: int, color: tuple[int, int, int, int]) -> None:
        for gy, row in enumerate(LETTERS[ch]):
            for gx, bit in enumerate(row):
                if bit != "1":
                    continue
                for dy in range(scale):
                    for dx in range(scale):
                        px[ox + gx * scale + dx, oy + gy * scale + dy] = color

    outline_off = max(1, scale // 3)
    x = scale
    for ch in word:
        for dx, dy in (
            (-1, 0),
            (1, 0),
            (0, -1),
            (0, 1),
            (-1, -1),
            (1, 1),
            (-1, 1),
            (1, -1),
        ):
            blit_letter(ch, x + dx * outline_off, scale + dy * outline_off, LETTER_OUTLINE)
        blit_letter(ch, x, scale, LETTER_FILL)
        x += letter_w * scale + gap * scale
    return img


def fit_shells(max_w: int, max_h: int, rotate_cw: bool = False) -> Image.Image:
    best = None
    for scale in range(1, 16):
        cand = render_shells(scale=scale, gap=1)
        if rotate_cw:
            cand = cand.rotate(-90, expand=True)
        if cand.width <= max_w and cand.height <= max_h:
            best = cand
        else:
            break
    if best is None:
        best = render_shells(scale=1, gap=1)
        if rotate_cw:
            best = best.rotate(-90, expand=True)
    return best


def median_fill(a: np.ndarray, labeled: np.ndarray, strip: dict) -> tuple[int, int, int, int]:
    y0, y1, x0, x1 = strip["y0"], strip["y1"], strip["x0"], strip["x1"]
    mask = labeled[y0 : y1 + 1, x0 : x1 + 1] == strip["i"]
    sub = a[y0 : y1 + 1, x0 : x1 + 1]
    rr = sub[:, :, 0].astype(np.int16)
    gg = sub[:, :, 1].astype(np.int16)
    bb = sub[:, :, 2].astype(np.int16)
    yellow = (rr > 150) & (gg > 110) & (bb < 120)
    redish = (
        (rr > 100)
        & (rr >= gg)
        & ((rr - np.minimum(gg, bb)) > 40)
        & ~yellow
    )
    m = mask & redish
    if int(m.sum()) < 10:
        return (160, 40, 30, 255)
    med = np.median(sub[m], axis=0).astype(np.uint8)
    return (int(med[0]), int(med[1]), int(med[2]), 255)


def find_strips(a: np.ndarray) -> tuple[list[dict], np.ndarray]:
    r = a[:, :, 0].astype(np.int16)
    g = a[:, :, 1].astype(np.int16)
    b = a[:, :, 2].astype(np.int16)
    red = (r > 100) & (r >= g) & (r >= b) & ((r - np.minimum(g, b)) > 40)
    yellow = (r > 150) & (g > 110) & (b < 120)
    red_f = ndimage.binary_closing(ndimage.binary_dilation(red, iterations=1), iterations=3)
    labeled, n = ndimage.label(red_f)

    strips: list[dict] = []
    for i in range(1, n + 1):
        ys, xs = np.where(labeled == i)
        area = len(xs)
        if area < 2500:
            continue
        x0, x1 = int(xs.min()), int(xs.max())
        y0, y1 = int(ys.min()), int(ys.max())
        w, h = x1 - x0 + 1, y1 - y0 + 1
        mask = labeled[y0 : y1 + 1, x0 : x1 + 1] == i
        ypix = int((yellow[y0 : y1 + 1, x0 : x1 + 1] & mask).sum())
        frac = ypix / area
        asp = w / max(h, 1)
        if frac > 0.25 and (asp > 2.0 or asp < 0.5) and min(w, h) >= 40 and max(w, h) <= 220:
            strips.append(
                {
                    "i": i,
                    "x0": x0,
                    "y0": y0,
                    "x1": x1,
                    "y1": y1,
                    "w": w,
                    "h": h,
                    "ypix": ypix,
                    "frac": frac,
                    "orient": "h" if asp > 1 else "v",
                }
            )
    return strips, labeled


def detect_uv_mirror_flags(strips: list[dict], obj_path: Path) -> None:
    """Mark strips whose UV winding is flipped (texture appears mirrored on mesh)."""
    try:
        import trimesh
    except ImportError:
        for s in strips:
            # Fallback: horizontal packs from this GLB are the mirrored charts.
            s["mirror"] = s["orient"] == "h"
        return

    mesh = trimesh.load(str(obj_path), force="mesh")
    uv = np.asarray(mesh.visual.uv, dtype=np.float64)
    faces = np.asarray(mesh.faces)
    u0, u1, u2 = uv[faces[:, 0]], uv[faces[:, 1]], uv[faces[:, 2]]
    uv_cross = (u1[:, 0] - u0[:, 0]) * (u2[:, 1] - u0[:, 1]) - (u1[:, 1] - u0[:, 1]) * (
        u2[:, 0] - u0[:, 0]
    )
    cent = (u0 + u1 + u2) / 3.0
    px = cent[:, 0] * 1024.0
    py = (1.0 - cent[:, 1]) * 1024.0

    for s in strips:
        m = (
            (px >= s["x0"])
            & (px < s["x1"] + 1)
            & (py >= s["y0"])
            & (py < s["y1"] + 1)
        )
        if int(m.sum()) == 0:
            s["mirror"] = s["orient"] == "h"
            s["uv_cross_mean"] = 0.0
            continue
        mean = float(uv_cross[m].mean())
        s["uv_cross_mean"] = mean
        # Negative UV winding => paint mirrored so world-space text reads L→R.
        s["mirror"] = mean < 0.0


def paint_strip(
    out: Image.Image,
    base: np.ndarray,
    labeled: np.ndarray,
    strip: dict,
    fill: tuple[int, int, int, int],
) -> None:
    # Text strips are packed as near-rectangular UV islands; paint the full
    # bbox so letter gaps inside the old yellow bars cannot punch holes.
    _ = base, labeled
    tw, th = strip["w"], strip["h"]
    x0, y0 = strip["x0"], strip["y0"]

    panel = Image.new("RGBA", (tw, th), fill)
    draw = ImageDraw.Draw(panel)

    if strip["orient"] == "h":
        stripe_h = max(4, th // 5)
        label_h = th - stripe_h - 2
        draw.rectangle([0, 0, tw - 1, stripe_h - 1], fill=GOLD)
        label = fit_shells(tw - 6, max(8, label_h - 2), rotate_cw=False)
        lx = (tw - label.width) // 2
        ly = stripe_h + max(0, (label_h - label.height) // 2)
        panel.alpha_composite(label, (lx, ly))
    else:
        stripe_w = max(4, tw // 5)
        label_w = tw - stripe_w - 2
        draw.rectangle([0, 0, stripe_w - 1, th - 1], fill=GOLD)
        label = fit_shells(max(8, label_w - 2), th - 6, rotate_cw=True)
        lx = stripe_w + max(0, (label_w - label.width) // 2)
        ly = (th - label.height) // 2
        panel.alpha_composite(label, (lx, ly))

    if strip.get("mirror"):
        # Compensate flipped UV charts so SHELLS reads correctly in world space.
        panel = panel.transpose(Image.Transpose.FLIP_LEFT_RIGHT)

    out.paste(panel, (x0, y0))


def main() -> None:
    # Always start from the pre-paint backup when present so re-runs are idempotent.
    source = BACKUP if BACKUP.exists() else ALBEDO
    im = Image.open(source).convert("RGBA")
    a = np.asarray(im).copy()
    BACKUP.parent.mkdir(parents=True, exist_ok=True)
    if not BACKUP.exists():
        Image.fromarray(a).save(BACKUP)

    strips, labeled = find_strips(a)
    obj_path = ALBEDO.parent / "SBOXA0.obj"
    detect_uv_mirror_flags(strips, obj_path)
    print(f"found {len(strips)} text strips from {source.name}")
    out = Image.fromarray(a.copy())
    PREVIEW.mkdir(parents=True, exist_ok=True)

    # Crate-red matching native/shapehint identity (TRELLIS bake drifts brown).
    crate_red = (168, 32, 28, 255)

    for idx, strip in enumerate(strips):
        fill = crate_red
        mirror = bool(strip.get("mirror"))
        print(
            f"  [{idx}] {strip['orient']} {strip['w']}x{strip['h']} "
            f"@({strip['x0']},{strip['y0']}) mirror={mirror} "
            f"uv_cross={strip.get('uv_cross_mean', 0):.3e} fill={fill}"
        )
        paint_strip(out, a, labeled, strip, fill)
        crop = out.crop((strip["x0"], strip["y0"], strip["x1"] + 1, strip["y1"] + 1))
        scale = max(1, min(4, 400 // max(strip["w"], 1)))
        flag = "m" if mirror else "n"
        name = f"{idx:02d}_{strip['orient']}_{flag}_{strip['w']}x{strip['h']}.png"
        crop.resize((strip["w"] * scale, strip["h"] * scale), Image.NEAREST).save(PREVIEW / name)

    out.save(ALBEDO)
    # Also keep a Logs copy for inspection when Unity locks the asset.
    logs_copy = REPO / "Logs/SBOXA0_albedo_painted.png"
    out.save(logs_copy)
    print(f"wrote {ALBEDO}")
    print(f"logs copy {logs_copy}")
    print(f"backup {BACKUP}")


if __name__ == "__main__":
    main()
