"""Tone-match monster frame albedos to an anchor frame (stop-motion shimmer fix).

TRELLIS re-imagines texture tone per generation, so stop-motion frame meshes
shimmer (POSS torso: cream <-> tan). This transfers per-channel mean/std RGB
statistics from the anchor frame's raw 1024px albedo onto each other frame,
then reapplies the doomify texture pass (256px BOX cap + shared native
palette quantization). Geometry and UVs are untouched.

Usage:
  python Tools/tone_match_albedo.py --anchor <dirA>/POSSA1_albedo.png \
      --palette-lump POSSA1 --texcap 256 \
      --frames POSSB1=<dirB>/POSSB1_albedo.png POSSC1=... \
      --out-resources Assets/Resources/ExperimentalMonsters/POSS
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
import doomify3d  # noqa: E402  (WAD palette + doomify_texture reuse)


def shirt_mask(arr: np.ndarray) -> np.ndarray:
    """Warm low-saturation texels (beige shirt / skin): hue 10-70deg,
    saturation above the gray floor. Excludes green pants (hue ~80-140)
    and gray boots (near-zero saturation)."""
    rgb = arr[..., :3].astype(np.float64) / 255.0
    mx = rgb.max(-1)
    mn = rgb.min(-1)
    delta = mx - mn
    sat = np.where(mx > 0, delta / np.maximum(mx, 1e-9), 0)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    hue = np.zeros_like(mx)
    m = delta > 1e-9
    rm = m & (mx == r)
    gm = m & (mx == g) & ~rm
    bm = m & (mx == b) & ~rm & ~gm
    hue[rm] = (60 * ((g - b) / delta) % 360)[rm]
    hue[gm] = (60 * ((b - r) / delta) + 120)[gm]
    hue[bm] = (60 * ((r - g) / delta) + 240)[bm]
    return (sat > 0.08) & (hue >= 10) & (hue <= 70) & (mx > 0.15)


def armor_mask(arr: np.ndarray) -> np.ndarray:
    """Near-neutral texels (gray armor plate): low saturation, not near-black.
    Excludes the red head, skin (warm, saturated) and green pants."""
    rgb = arr[..., :3].astype(np.float64) / 255.0
    mx = rgb.max(-1)
    mn = rgb.min(-1)
    sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-9), 0)
    return (sat <= 0.25) & (mx > 0.15)


def pants_mask(arr: np.ndarray) -> np.ndarray:
    """Green-hue texels (fatigue pants): hue 60-170, saturated enough to
    exclude gray armor."""
    rgb = arr[..., :3].astype(np.float64) / 255.0
    mx = rgb.max(-1)
    mn = rgb.min(-1)
    delta = mx - mn
    sat = np.where(mx > 0, delta / np.maximum(mx, 1e-9), 0)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    with np.errstate(invalid="ignore", divide="ignore"):
        hue = np.zeros_like(mx)
        m = delta > 1e-9
        rm = m & (mx == r)
        gm = m & (mx == g) & ~rm
        bm = m & (mx == b) & ~rm & ~gm
        hue[rm] = (60 * ((g - b) / delta) % 360)[rm]
        hue[gm] = (60 * ((b - r) / delta) + 120)[gm]
        hue[bm] = (60 * ((r - g) / delta) + 240)[bm]
    return (sat > 0.12) & (hue >= 60) & (hue <= 170) & (mx > 0.1)


MASKS = {"shirt": shirt_mask, "armor": armor_mask, "pants": pants_mask}


def masked_stats(arr: np.ndarray, mask: np.ndarray):
    rgb = arr[..., :3].reshape(-1, 3).astype(np.float64)[mask.reshape(-1)]
    if len(rgb) < 64:
        rgb = arr[..., :3].reshape(-1, 3).astype(np.float64)
    return rgb.mean(0), rgb.std(0) + 1e-6


def transfer(src: Image.Image, ref_mean, ref_std, mask_fn=shirt_mask) -> Image.Image:
    arr = np.asarray(src.convert("RGBA")).copy()
    mask = mask_fn(arr)
    rgb = arr[..., :3].astype(np.float64)
    mean, std = masked_stats(arr, mask)
    corrected = (rgb - mean) / std * ref_std + ref_mean
    rgb[mask] = corrected[mask]
    arr[..., :3] = np.clip(rgb, 0, 255).astype(np.uint8)
    return Image.fromarray(arr)


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--anchor", required=True)
    p.add_argument("--palette-lump", required=True)
    p.add_argument("--texcap", type=int, default=256)
    p.add_argument("--frames", nargs="+", required=True,
                   help="LUMP=path/to/raw_albedo.png pairs")
    p.add_argument("--out-resources", required=True)
    p.add_argument("--mask", default="shirt",
                   help="comma-separated region masks applied in order")
    a = p.parse_args()

    mask_fns = [MASKS[name] for name in a.mask.split(",")]
    ref = Image.open(a.anchor)
    ref_arr = np.asarray(ref.convert("RGBA"))
    ref_stats = [masked_stats(ref_arr, fn(ref_arr)) for fn in mask_fns]
    pal = doomify3d.sprite_palette(a.palette_lump)
    out_dir = Path(a.out_resources)

    for pair in a.frames:
        lump, path = pair.split("=", 1)
        matched = Image.open(path)
        for fn, (m, s) in zip(mask_fns, ref_stats):
            matched = transfer(matched, m, s, fn)
        final = doomify3d.doomify_texture(matched, pal, a.texcap)
        dst = out_dir / f"{lump}_albedo.png"
        final.save(dst)
        print(f"{lump}: tone-matched -> {dst}")


if __name__ == "__main__":
    main()
