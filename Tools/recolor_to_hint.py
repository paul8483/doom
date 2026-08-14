"""Recolor monster frame albedos to match the v2 hint render exactly.

The user-approved hint is the color contract: dark gunmetal armor, olive
pants, red head, blue ONLY on the visor slits / chest dots. TRELLIS frames
drift: armor brightness wanders and bluish rim-light bakes onto shoulders,
which the palette then snaps to vivid visor blue (in-game blue blotches,
2026-08-14).

Per frame:
  1. stray bluish texels (relaxed test, no compact cluster) -> neutralized
     to gray so they rejoin the armor region;
  2. strong compact blue clusters (visor/chest) -> kept, exported as the
     emission mask;
  3. armor and pants histogram-matched to the hint's own pixel pools
     (checkerboard background removed by flat-tile color match);
  4. doomify palette quantization as usual.

Usage:
  python Tools/recolor_to_hint.py --hint Textures/.../SPOSA1-...-v2.png \
      --palette-lump SPOSA1 --texcap 256 \
      --frames SPOSA1=<stage>/SPOSA1_albedo.png ... \
      --out-resources Assets/Resources/ExperimentalMonsters/SPOS
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

sys.path.insert(0, str(Path(__file__).resolve().parent))
import doomify3d  # noqa: E402
from tone_match_albedo import armor_mask, pants_mask  # noqa: E402


def hint_foreground(arr: np.ndarray) -> np.ndarray:
    """Drop the baked checkerboard: its two tile grays are flat constants
    sampled from the corners; everything close to either is background."""
    h, w = arr.shape[:2]
    corners = [arr[2, 2, :3], arr[2, w - 3, :3],
               arr[h - 3, 2, :3], arr[h - 3, w - 3, :3]]
    tiles = []
    for c in corners:
        if not any(np.abs(c.astype(int) - t.astype(int)).max() < 6 for t in tiles):
            tiles.append(c)
    rgb = arr[..., :3].astype(int)
    bg = np.zeros(arr.shape[:2], dtype=bool)
    for t in tiles:
        bg |= (np.abs(rgb - t.astype(int)).max(-1) < 8)
    return ~bg


def strong_blue(arr: np.ndarray) -> np.ndarray:
    r = arr[..., 0].astype(np.float64)
    g = arr[..., 1].astype(np.float64)
    b = arr[..., 2].astype(np.float64)
    return (b > 100) & (b > r * 1.35) & (b > g * 1.2)


def weak_blue(arr: np.ndarray) -> np.ndarray:
    r = arr[..., 0].astype(np.float64)
    g = arr[..., 1].astype(np.float64)
    b = arr[..., 2].astype(np.float64)
    return (b > r + 6) & (b > g + 3)


def compact(mask: np.ndarray, erode: int = 3, dilate: int = 7) -> np.ndarray:
    """Erode-then-dilate: keeps clusters that survive erosion (visor bars),
    kills speckle and thin rim-light."""
    img = Image.fromarray((mask * 255).astype(np.uint8), "L")
    img = img.filter(ImageFilter.MinFilter(erode))
    img = img.filter(ImageFilter.MaxFilter(dilate))
    return np.asarray(img) > 127


def hist_match(values: np.ndarray, ref: np.ndarray) -> np.ndarray:
    quantiles = np.linspace(0.0, 1.0, 256)
    src_q = np.quantile(values, quantiles)
    ref_q = np.quantile(ref, quantiles)
    return np.interp(values, src_q, ref_q)


def clip_band(pixels: np.ndarray, lo: float, hi: float) -> np.ndarray:
    lum = pixels.mean(1)
    lo_v, hi_v = np.quantile(lum, lo), np.quantile(lum, hi)
    keep = (lum >= lo_v) & (lum <= hi_v)
    return pixels[keep] if keep.sum() >= 64 else pixels


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--hint", required=True)
    p.add_argument("--palette-lump", required=True)
    p.add_argument("--texcap", type=int, default=256)
    p.add_argument("--frames", nargs="+", required=True)
    p.add_argument("--out-resources", required=True)
    a = p.parse_args()

    hint = np.asarray(Image.open(a.hint).convert("RGBA"))
    fg = hint_foreground(hint)
    hint_blue = strong_blue(hint) & fg
    ref_armor = hint[..., :3][armor_mask(hint) & fg & ~hint_blue].astype(np.float64)
    ref_pants = hint[..., :3][pants_mask(hint) & fg].astype(np.float64)
    # Drop the hint's specular tail so wandering per-frame highlights can't
    # re-introduce frame-to-frame brightness jumps.
    ref_armor = clip_band(ref_armor, 0.02, 0.75)
    print(f"hint pools: armor {len(ref_armor)}, pants {len(ref_pants)}, "
          f"blue px {int(hint_blue.sum())}")

    pal = doomify3d.sprite_palette(a.palette_lump)
    out_dir = Path(a.out_resources)

    for pair in a.frames:
        lump, path = pair.split("=", 1)
        arr = np.asarray(Image.open(path).convert("RGBA")).copy()

        blue_all = weak_blue(arr)
        visor = compact(strong_blue(arr))
        # Faint-visor frames (D bakes the slits dull): fall back to compact
        # clusters of the relaxed test before declaring the visor missing.
        if visor.sum() < 300:
            visor = compact(blue_all)
        stray = blue_all & ~visor
        # Neutralize stray blue to its own gray value -> rejoins armor pool.
        gray = arr[..., :3].astype(np.float64).mean(-1)
        for c in range(3):
            arr[..., c][stray] = np.clip(gray[stray], 0, 255).astype(np.uint8)

        # Cream/beige plate drift: TRELLIS bakes some frames' armor in warm
        # beige, which reads as skin to the hue masks. Real skin (hands) is
        # small and dies under erosion; large warm blobs are armor.
        rgbf = arr[..., :3].astype(np.float64)
        mx = rgbf.max(-1) / 255.0
        mn = rgbf.min(-1) / 255.0
        delta = mx - mn
        sat = np.where(mx > 0, delta / np.maximum(mx, 1e-9), 0)
        r, g, b = rgbf[..., 0], rgbf[..., 1], rgbf[..., 2]
        beige = (sat > 0.2) & (r > b + 10) & (g > b * 0.9) & (r < 2.2 * b + 60)
        deep_red = (sat > 0.4) & (r > 1.6 * g) & (r > 1.6 * b)  # head stays
        big_beige = compact(beige & ~deep_red, erode=9, dilate=15)

        rgb = arr[..., :3].astype(np.float64)
        am = (armor_mask(arr) | big_beige) & ~visor & ~deep_red
        pm = pants_mask(arr) & ~visor & ~big_beige
        # The hint armor is neutral gunmetal: strip chroma before matching so
        # neither beige nor bluish shading survives as a per-frame tint.
        gray_all = rgb.mean(-1)
        for c in range(3):
            rgb[..., c][am] = gray_all[am]
        for c in range(3):
            if am.sum() >= 64:
                rgb[..., c][am] = hist_match(rgb[..., c][am], ref_armor[:, c])
            if pm.sum() >= 64 and len(ref_pants) >= 64:
                rgb[..., c][pm] = hist_match(rgb[..., c][pm], ref_pants[:, c])
        # Saturate the kept visor clusters toward the hint blue.
        rgb[..., 0][visor] *= 0.45
        rgb[..., 1][visor] *= 0.85
        rgb[..., 2][visor] = np.clip(rgb[..., 2][visor] * 1.5 + 30, 0, 255)
        arr[..., :3] = np.clip(rgb, 0, 255).astype(np.uint8)

        final = doomify3d.doomify_texture(Image.fromarray(arr), pal, a.texcap)
        final.save(out_dir / f"{lump}_albedo.png")

        mask_img = Image.fromarray((visor * 255).astype(np.uint8), "L")
        mask_img.resize((a.texcap, a.texcap), Image.BOX).save(
            out_dir / f"{lump}_emission.png")
        print(f"{lump}: visor px {int(visor.sum())}, stray neutralized "
              f"{int(stray.sum())}")


if __name__ == "__main__":
    main()
