"""Global tone match across a monster's frame albedos + palette re-quantization.

`project_hint_texture.py` takes each frame's colour straight from that frame's
own v2 hint, which fixes WHERE highlights sit but inherits whatever exposure
the hint was drawn at. The POSS hint set spans a 3x luminance range (hint B
sits at 184, hint F at 62), so the projected walk cycle pulsed in brightness
by 40% between frames — visible in game as the zombie changing colour as it
walks (reported 2026-08-15).

The masked matcher in tone_match_albedo.py only reaches the hue bands it knows
(shirt/pants/armor), which left 30% of the spread. Matching mean/std globally
over every opaque texel against the anchor frame closes it to 5% and keeps the
per-frame highlight placement the projection established.

Usage:
  python Tools/global_tone_match.py <anchor_albedo.png> <PALETTE_LUMP> <out_dir>       LUMP=<albedo.png> [LUMP=<albedo.png> ...]
"""
import sys
from pathlib import Path
import numpy as np
from PIL import Image
sys.path.insert(0, r"D:\Development\doom\Tools")
import doomify3d

anchor, palette_lump, outdir = sys.argv[1], sys.argv[2], Path(sys.argv[3])
frames = sys.argv[4:]
outdir.mkdir(parents=True, exist_ok=True)
pal = doomify3d.sprite_palette(palette_lump)

ref = np.asarray(Image.open(anchor).convert("RGBA")).astype(np.float64)
rm = ref[..., 3] > 0
ref_mean, ref_std = ref[..., :3][rm].mean(0), ref[..., :3][rm].std(0)

for spec in frames:
    lump, path = spec.split("=", 1)
    a = np.asarray(Image.open(path).convert("RGBA")).astype(np.float64)
    m = a[..., 3] > 0
    src = a[..., :3][m]
    out = (src - src.mean(0)) / np.maximum(src.std(0), 1e-6) * ref_std + ref_mean
    a[..., :3][m] = np.clip(out, 0, 255)
    img = Image.fromarray(a.astype(np.uint8))
    doomify3d.doomify_texture(img, pal, 256).save(outdir / f"{lump}_albedo.png")
    print(f"{lump}: matched")
