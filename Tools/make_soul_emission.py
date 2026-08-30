"""Emission mask for SOULA0: the golden shell glows steady, the skull's
recesses (eye sockets, nasal cavity, mouth interior) stay dark.

Unlike COLU/ELEC no mesh-height zone is needed — the split is purely
chromatic: the shell is warm saturated gold, the recesses are dark brown.
Same output convention as make_elec_emission.py (R channel = mask).

Usage: python Tools/make_soul_emission.py
(reads and writes Assets/Resources/ExperimentalPickups/SOULA0/)
"""
import numpy as np
from PIL import Image
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
dst = REPO / "Assets" / "Resources" / "ExperimentalPickups" / "SOULA0"

albedo = np.asarray(Image.open(dst / "SOULA0_albedo.png").convert("RGBA"))
r = albedo[..., 0].astype(np.int16)
g = albedo[..., 1].astype(np.int16)
b = albedo[..., 2].astype(np.int16)
lum = 0.299 * r + 0.587 * g + 0.114 * b

# Gold: red-led warmth with blue clearly below green, bright enough that the
# quantized recess browns (lum ~40-80 in the native palette) never pass.
gold = (r > 120) & (g > 90) & (b < g) & (lum > 95)

out = np.zeros(albedo.shape[:2] + (4,), dtype=np.uint8)
out[..., 0] = np.where(gold, 255, 0)
out[..., 3] = 255
Image.fromarray(out).save(dst / "SOULA0_emission.png")
print(f"gold texels {int(gold.sum())} / {gold.size}, "
      f"mask -> SOULA0_emission.png")
