"""Install accepted weapon viewmodel redraws into Resources/EnhancedWeapons.

Copies Textures/WeaponRedraw/<LUMP>/redraw.png (healed + tone-matched) to
Assets/Resources/EnhancedWeapons/<LUMP>.png and writes a .meta cloned from
the EnhancedHud import template (Point, uncompressed, no mips, readable)
with a fresh GUID. Existing .meta files keep their GUID.

Usage:
  python Tools/install_weapon_redraws.py
"""
from __future__ import annotations

import shutil
import uuid
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
SRC = REPO / "Textures" / "WeaponRedraw"
DST = REPO / "Assets" / "Resources" / "EnhancedWeapons"
META_TEMPLATE = REPO / "Assets" / "Resources" / "EnhancedHud" / "STBAR.png.meta"


def main():
    DST.mkdir(parents=True, exist_ok=True)
    template = META_TEMPLATE.read_text(encoding="utf-8")
    installed = 0
    for d in sorted(SRC.iterdir()):
        if not d.is_dir():
            continue
        src = d / "redraw.png"
        if not src.exists():
            print(f"{d.name}: no redraw.png, skipped")
            continue
        dst = DST / f"{d.name}.png"
        shutil.copyfile(src, dst)
        meta = dst.with_suffix(".png.meta")
        if not meta.exists():
            lines = template.splitlines(keepends=True)
            out = []
            for line in lines:
                if line.startswith("guid:"):
                    out.append(f"guid: {uuid.uuid4().hex}\n")
                else:
                    out.append(line)
            meta.write_text("".join(out), encoding="utf-8")
        installed += 1
        print(f"{d.name}: installed")
    print(f"done: {installed} redraws -> {DST}")


if __name__ == "__main__":
    main()
