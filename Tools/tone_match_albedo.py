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
    """Armor plate texels by complement: everything that is not warm skin/head
    (saturated warm hue), not green pants, and not near-black. Bluish-lit
    plates (re-rolled SPOS redraws glow blue onto the armor) stay included —
    the old low-saturation-only mask skipped them and the tone transfer left
    those frames bright (armor flicker found in-game 2026-08-14)."""
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
    warm = (sat > 0.25) & ((hue >= 330) | (hue <= 60))
    green = (sat > 0.12) & (hue >= 60) & (hue <= 170)
    return ~warm & ~green & (mx > 0.12)


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


def flesh_mask(arr: np.ndarray) -> np.ndarray:
    """Pink/red flesh (SARG worm): hue wrapping 330-360/0-25, saturated."""
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
    return (sat > 0.12) & ((hue >= 330) | (hue <= 25)) & (mx > 0.1)


MASKS = {"shirt": shirt_mask, "armor": armor_mask, "pants": pants_mask,
         "flesh": flesh_mask}


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


def transfer_hist(src: Image.Image, ref_pixels: np.ndarray,
                  mask_fn) -> Image.Image:
    """Full histogram (CDF) matching of the masked region to the reference
    region, per channel. Unlike mean/std, this equalizes distribution SHAPE —
    frames dominated by bright plate can't stay a half-tone lighter."""
    arr = np.asarray(src.convert("RGBA")).copy()
    mask = mask_fn(arr)
    if mask.sum() < 64 or len(ref_pixels) < 64:
        return Image.fromarray(arr)
    rgb = arr[..., :3].astype(np.float64)
    quantiles = np.linspace(0.0, 1.0, 256)
    for c in range(3):
        src_vals = rgb[..., c][mask]
        src_q = np.quantile(src_vals, quantiles)
        ref_q = np.quantile(ref_pixels[:, c], quantiles)
        rgb[..., c][mask] = np.interp(src_vals, src_q, ref_q)
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
    p.add_argument("--method", choices=["stats", "hist"], default="stats")
    # Clip the reference pixel pool to a quantile band (per mask, "lo,hi").
    # Stop-motion frames bake highlights on DIFFERENT body parts; dropping
    # the bright tail keeps every frame's region in one tight band so the
    # wandering highlights stop reading as frame-to-frame color flicker.
    p.add_argument("--ref-clip", default=None,
                   help="comma pairs per mask, e.g. '0.02:0.65,0:1'")
    a = p.parse_args()

    mask_fns = [MASKS[name] for name in a.mask.split(",")]
    ref = Image.open(a.anchor)
    ref_arr = np.asarray(ref.convert("RGBA"))
    # Reference region pixels: opaque + masked (works for native sprite
    # canvases with transparent padding and for opaque mesh albedos alike).
    opaque = ref_arr[..., 3] > 0
    ref_stats = [masked_stats(ref_arr, fn(ref_arr)) for fn in mask_fns]
    ref_pixels = [
        ref_arr[..., :3][fn(ref_arr) & opaque].astype(np.float64)
        for fn in mask_fns
    ]
    if a.ref_clip:
        clips = [c.split(":") for c in a.ref_clip.split(",")]
        for i, (lo, hi) in enumerate(clips):
            if i >= len(ref_pixels) or len(ref_pixels[i]) < 64:
                continue
            lum = ref_pixels[i].mean(1)
            lo_v = np.quantile(lum, float(lo))
            hi_v = np.quantile(lum, float(hi))
            keep = (lum >= lo_v) & (lum <= hi_v)
            if keep.sum() >= 64:
                ref_pixels[i] = ref_pixels[i][keep]
    pal = doomify3d.sprite_palette(a.palette_lump)
    out_dir = Path(a.out_resources)

    for pair in a.frames:
        lump, path = pair.split("=", 1)
        matched = Image.open(path)
        for i, fn in enumerate(mask_fns):
            if a.method == "hist":
                matched = transfer_hist(matched, ref_pixels[i], fn)
            else:
                m, s = ref_stats[i]
                matched = transfer(matched, m, s, fn)
        final = doomify3d.doomify_texture(matched, pal, a.texcap)
        dst = out_dir / f"{lump}_albedo.png"
        final.save(dst)
        print(f"{lump}: tone-matched ({a.method}) -> {dst}")


if __name__ == "__main__":
    main()
