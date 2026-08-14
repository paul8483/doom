"""Extract blue-glow emission masks from monster frame albedos (SPOS visor).

Detects bluish texels in the raw TRELLIS albedo, saturates them toward the
vivid visor blue (so palette quantization lands on the native sprite's blue
entries and the albedo-tinted steady glow reads blue), writes the boosted
full-res albedo next to the raw one, and exports a downscaled emission mask
aligned with the final albedo UV space.

Frames whose albedo baked the visor gray yield a near-black mask: the glow
is simply absent there until the frame is re-rolled with a visible blue mask.

Usage:
  python Tools/extract_blue_emission.py --texcap 256 \
      --frames SPOSA1=<stage>/SPOSA1_albedo.png ... \
      --out-resources Assets/Resources/ExperimentalMonsters/SPOS
"""
from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
from PIL import Image


def bluish_mask(arr: np.ndarray) -> np.ndarray:
    r = arr[..., 0].astype(np.float64)
    g = arr[..., 1].astype(np.float64)
    b = arr[..., 2].astype(np.float64)
    return (b > r + 8) & (b > g + 4) & (b > 60)


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--texcap", type=int, default=256)
    p.add_argument("--frames", nargs="+", required=True,
                   help="LUMP=path/to/raw_albedo.png pairs")
    p.add_argument("--out-resources", required=True)
    a = p.parse_args()

    out_dir = Path(a.out_resources)
    for pair in a.frames:
        lump, path = pair.split("=", 1)
        src = Path(path)
        arr = np.asarray(Image.open(src).convert("RGB")).copy()
        mask = bluish_mask(arr)

        boosted = arr.astype(np.float64)
        boosted[..., 0][mask] *= 0.45
        boosted[..., 1][mask] *= 0.85
        boosted[..., 2][mask] = np.clip(boosted[..., 2][mask] * 1.6 + 25, 0, 255)
        boosted_path = src.with_name(f"{lump}_albedo_boosted.png")
        Image.fromarray(np.clip(boosted, 0, 255).astype(np.uint8)).save(boosted_path)

        mask_img = Image.fromarray((mask * 255).astype(np.uint8), "L")
        mask_small = mask_img.resize((a.texcap, a.texcap), Image.BOX)
        mask_dst = out_dir / f"{lump}_emission.png"
        mask_small.save(mask_dst)
        print(f"{lump}: blue px {int(mask.sum())} -> {boosted_path.name}, "
              f"{mask_dst.name}")


if __name__ == "__main__":
    main()
