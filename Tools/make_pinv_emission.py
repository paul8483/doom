"""Emission mask for PINVA0: the green glass shell glows steady, the
pentagram engraving (dark grooves) stays dark.

Same instrument as make_soul_emission.py — purely chromatic split, no
mesh-height zone: the shell quantized to greens at lum 63-123, the grooves
to lum 18-51 (R channel = mask, alpha 255).

Usage: python Tools/make_pinv_emission.py
(reads and writes Assets/Resources/ExperimentalPickups/PINVA0/)
"""
import numpy as np
from PIL import Image
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
dst = REPO / "Assets" / "Resources" / "ExperimentalPickups" / "PINVA0"

albedo = np.asarray(Image.open(dst / "PINVA0_albedo.png").convert("RGBA"))
r = albedo[..., 0].astype(np.int16)
g = albedo[..., 1].astype(np.int16)
b = albedo[..., 2].astype(np.int16)
lum = 0.299 * r + 0.587 * g + 0.114 * b

# Glass: green-led and bright enough that the engraving's near-black
# greens (lum <= 51 in the quantized palette) never pass.
glass = (g > r) & (g > b) & (lum > 55)

out = np.zeros(albedo.shape[:2] + (4,), dtype=np.uint8)
out[..., 0] = np.where(glass, 255, 0)
out[..., 3] = 255
Image.fromarray(out).save(dst / "PINVA0_emission.png")
print(f"glass texels {int(glass.sum())} / {glass.size}, "
      f"mask -> PINVA0_emission.png")
