"""Finish HUD face redraws: 16x GPT canvas -> exact-4x redraw.png.

Each face lump folder under Textures/HudRedraw/<LUMP>/ holds native.png and
a 16x redraw.png fresh from generation (real alpha). This tool clips the
alpha to a half-texel dilation of the native mask, downscales premultiplied
Lanczos to exactly 4x native, binarizes alpha, tone-matches per channel
against the native's opaque texels (std cap 1.6), and writes
redraw-raw.png (pre-tone 4x) + redraw.png (final 4x) beside the native.

Usage: python Tools/install_hud_faces.py STFST00 STFST01 ...
       (no args = HUD wave 2 set 1)
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

REPO = Path(__file__).resolve().parent.parent
ROOT = REPO / "Textures" / "HudRedraw"
LUM = np.array([0.299, 0.587, 0.114])
STD_CAP = 1.6
SRC_SCALE = 16
OUT_SCALE = 4

SET1 = ["STFST00", "STFST01", "STFST02", "STFTL00", "STFTR00",
        "STFOUCH0", "STFEVL0", "STFKILL0", "STFGOD0"]


def process(name: str):
    folder = ROOT / name
    native = np.asarray(Image.open(folder / "native.png").convert("RGBA"))
    nh, nw = native.shape[:2]
    # The 16x generation is provenance: park it as redraw-16x.png once,
    # then always work from there (re-runs stay idempotent after
    # redraw.png becomes the final 4x).
    src16 = folder / "redraw-16x.png"
    if not src16.exists():
        cand = Image.open(folder / "redraw.png").convert("RGBA")
        if cand.size != (nw * SRC_SCALE, nh * SRC_SCALE):
            raise SystemExit(f"{name}: redraw {cand.size}, expected "
                             f"{(nw * SRC_SCALE, nh * SRC_SCALE)} (16x native)")
        cand.save(src16)
    src = Image.open(src16).convert("RGBA")
    big = np.asarray(src).astype(np.float64)

    # Clip alpha to half-texel dilation of the native mask (nearest 16x).
    nat_mask = np.repeat(np.repeat(native[..., 3] >= 128, SRC_SCALE, 0),
                         SRC_SCALE, 1)
    dilated = np.asarray(
        Image.fromarray(nat_mask.astype(np.uint8) * 255, "L").filter(
            ImageFilter.MaxFilter(SRC_SCALE + 1))) > 0
    big[..., 3] = np.where(dilated, big[..., 3], 0)

    # Premultiplied Lanczos 16x -> 4x.
    pre = big.copy()
    pre[..., :3] *= pre[..., 3:4] / 255.0
    small = np.asarray(
        Image.fromarray(np.clip(pre, 0, 255).astype(np.uint8), "RGBA").resize(
            (nw * OUT_SCALE, nh * OUT_SCALE), Image.LANCZOS)).astype(np.float64)
    a = small[..., 3]
    rgb = np.zeros_like(small[..., :3])
    np.divide(small[..., :3] * 255.0, a[..., None], out=rgb,
              where=a[..., None] > 0)
    out = np.zeros((nh * OUT_SCALE, nw * OUT_SCALE, 4), np.uint8)
    out[..., :3] = np.clip(rgb, 0, 255).astype(np.uint8)
    out[..., 3] = np.where(a >= 128, 255, 0).astype(np.uint8)
    out[out[..., 3] == 0, :3] = 0
    Image.fromarray(out, "RGBA").save(folder / "redraw-raw.png")

    # Heal the canvas filler. The generation fills the parts of the
    # native-shaped canvas its (slightly narrower) head does not reach
    # with cream/white paper. In game that would flash beige between
    # grimaces. Freedoom face natives carry no gray backdrop panel —
    # head plus a dark outline fill the canvas — so every filler texel
    # is either over the native dark outline (fill it dark from the
    # native) or over native face (make it transparent and let the
    # STBAR face slot show, as it would around a narrower head).
    # Filler is cream/white (near-neutral, R-B < ~75; skin is > ~100)
    # CONNECTED to the patch border or a transparent hole — teeth,
    # sclera and god-eyes are interior and stay untouched.
    nat4_rgb = np.asarray(
        Image.fromarray(native, "RGBA").resize(
            (nw * OUT_SCALE, nh * OUT_SCALE), Image.LANCZOS)).astype(np.float64)
    nat4_nearest = np.repeat(np.repeat(native, OUT_SCALE, 0), OUT_SCALE, 1)
    lum_nat = (nat4_nearest[..., :3].astype(np.float64) * LUM).sum(2)
    lum_out = (out[..., :3].astype(np.float64) * LUM).sum(2)
    warm = out[..., 0].astype(np.float64) - out[..., 2].astype(np.float64)
    blue = out[..., 2].astype(np.float64)
    opaque = out[..., 3] > 0
    cream = opaque & (lum_out > 65) & (blue > 95) & (warm < 95)
    edge = np.zeros_like(opaque)
    edge[0, :] = edge[-1, :] = edge[:, 0] = edge[:, -1] = True
    holes = ~opaque
    edge[1:, :] |= holes[:-1, :]
    edge[:-1, :] |= holes[1:, :]
    edge[:, 1:] |= holes[:, :-1]
    edge[:, :-1] |= holes[:, 1:]
    flood = cream & edge
    while True:
        spread = np.zeros_like(flood)
        spread[1:, :] |= flood[:-1, :]
        spread[:-1, :] |= flood[1:, :]
        spread[:, 1:] |= flood[:, :-1]
        spread[:, :-1] |= flood[:, 1:]
        new = flood | (spread & cream)
        if (new == flood).all():
            break
        flood = new
    if flood.any():
        own_dark = opaque & ~flood & (lum_out < 60)
        fill = nat4_rgb[..., :3].copy()
        dark_nat = (nat4_nearest[..., 3] >= 128) & (lum_nat < 60)
        if own_dark.sum() > 200 and dark_nat.any():
            for c in range(3):
                mean_o = out[..., c][own_dark].astype(np.float64).mean()
                std_o = out[..., c][own_dark].astype(np.float64).std()
                mean_f = fill[..., c][dark_nat].mean()
                std_f = fill[..., c][dark_nat].std()
                fill[..., c] = ((fill[..., c] - mean_f)
                                * (std_o / max(std_f, 1e-6)) + mean_o)
        over_dark = flood & (lum_nat < 60)
        over_face = flood & ~over_dark
        out[over_dark, :3] = np.clip(fill, 0, 255).astype(np.uint8)[over_dark]
        out[over_face] = 0
        Image.fromarray(out, "RGBA").save(folder / "redraw-raw.png")

    # Tone match against the native's opaque texels.
    mask_n = native[..., 3] >= 128
    mask_r = out[..., 3] > 0
    matched = out.copy()
    b = out.astype(np.float64)
    for c in range(3):
        mean_n = native[..., c][mask_n].astype(np.float64).mean()
        std_n = native[..., c][mask_n].astype(np.float64).std()
        mean_r = b[..., c][mask_r].mean()
        std_r = b[..., c][mask_r].std()
        gain = min(max(std_n / max(std_r, 1e-6), 1.0 / STD_CAP), STD_CAP)
        b[..., c] = (b[..., c] - mean_r) * gain + mean_n
    matched[..., :3] = np.clip(b[..., :3], 0, 255).astype(np.uint8)
    matched[~mask_r, :3] = 0
    Image.fromarray(matched, "RGBA").save(folder / "redraw.png")

    lum_n = (native[..., :3].astype(np.float64) * LUM).sum(2)[mask_n].mean()
    lum_r = (matched[..., :3].astype(np.float64) * LUM).sum(2)[mask_r].mean()
    nat4 = np.repeat(np.repeat(mask_n, OUT_SCALE, 0), OUT_SCALE, 1)
    iou = (nat4 & mask_r).sum() / max((nat4 | mask_r).sum(), 1)
    holes_n = 100.0 * (~mask_n).mean()
    holes_r = 100.0 * (~mask_r).mean()
    print(f"{name:9s} lum {lum_n:5.1f}->{lum_r:5.1f} "
          f"({lum_r / max(lum_n, 1e-6):4.2f})  IoU={iou:.3f}  "
          f"holes {holes_n:4.1f}%->{holes_r:4.1f}%")


def main():
    lumps = sys.argv[1:] or SET1
    for name in lumps:
        process(name)


if __name__ == "__main__":
    main()
