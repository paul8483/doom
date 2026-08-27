"""Export status-bar UI patches for the HUD redraw pit.

Mirrors the world-redraw layout: Textures/HudRedraw/<NAME>/native.png
(RGBA, transparent where the patch has holes) plus native-x4.png (nearest
4x for the GPT canvas). The redraw comes back as redraw.png in the same
folder at exactly 4x the native size.

Usage:
  python Tools/export_hud_patch.py --lumps STBAR STARMS STTNUM0 ...
  python Tools/export_hud_patch.py --wave1
"""
from __future__ import annotations

import argparse
import struct
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
import doomify3d  # noqa: E402  (wad_lump + playpal reuse)

OUT_ROOT = Path(__file__).resolve().parent.parent / "Textures" / "HudRedraw"


def wave1_lumps() -> list[str]:
    lumps = ["STBAR", "STARMS", "STTMINUS", "STTPRCNT"]
    lumps += [f"STTNUM{i}" for i in range(10)]
    lumps += [f"STYSNUM{i}" for i in range(10)]
    lumps += [f"STGNUM{i}" for i in range(10)]
    lumps += [f"STKEYS{i}" for i in range(9)]
    return lumps


def face_lumps() -> list[str]:
    lumps = []
    for pain in range(5):
        for look in range(3):
            lumps.append(f"STFST{pain}{look}")
        lumps += [f"STFTL{pain}0", f"STFTR{pain}0",
                  f"STFOUCH{pain}", f"STFEVL{pain}", f"STFKILL{pain}"]
    lumps += ["STFGOD0", "STFDEAD0"]
    return lumps


def decode_patch(name: str) -> np.ndarray:
    raw = doomify3d.wad_lump(name)
    w, h, _, _ = struct.unpack_from("<hhhh", raw, 0)
    cols = struct.unpack_from(f"<{w}i", raw, 8)
    pal = doomify3d.playpal()
    img = np.zeros((h, w, 4), np.uint8)
    for x, ofs in enumerate(cols):
        p = ofs
        while raw[p] != 0xFF:
            top, length = raw[p], raw[p + 1]
            for i, idx in enumerate(raw[p + 3:p + 3 + length]):
                img[top + i, x, :3] = pal[idx]
                img[top + i, x, 3] = 255
            p += 4 + length
    return img


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--lumps", nargs="*", default=None)
    p.add_argument("--wave1", action="store_true")
    a = p.parse_args()

    lumps = list(a.lumps or [])
    if a.wave1:
        lumps += wave1_lumps()
    if not lumps:
        raise SystemExit("nothing to export: pass --lumps or --wave1")

    for lump in lumps:
        img = decode_patch(lump)
        d = OUT_ROOT / lump
        d.mkdir(parents=True, exist_ok=True)
        pil = Image.fromarray(img, "RGBA")
        pil.save(d / "native.png")
        pil.resize((img.shape[1] * 4, img.shape[0] * 4),
                   Image.NEAREST).save(d / "native-x4.png")
        holes = 100.0 * float((img[..., 3] == 0).mean())
        print(f"{lump}: {img.shape[1]}x{img.shape[0]} "
              f"(x4 {img.shape[1]*4}x{img.shape[0]*4}), holes {holes:.0f}%")


if __name__ == "__main__":
    main()
