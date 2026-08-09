"""Convert a TRELLIS.2 GLB into ExperimentalPickups OBJ/MTL/albedo layout."""
from __future__ import annotations

import argparse
import shutil
import tempfile
from pathlib import Path

import trimesh


def _extract_albedo_image(scene: trimesh.Scene):
    for geom in scene.geometry.values():
        visual = getattr(geom, "visual", None)
        if visual is None:
            continue
        material = getattr(visual, "material", None)
        if material is None:
            continue
        for attr in ("baseColorTexture", "image"):
            img = getattr(material, attr, None)
            if img is not None:
                return img
    return None


def convert(glb_path: Path, out_dir: Path, lump: str) -> None:
    out_dir.mkdir(parents=True, exist_ok=True)
    scene = trimesh.load(str(glb_path), force="scene")
    if not isinstance(scene, trimesh.Scene):
        scene = trimesh.Scene(scene)

    with tempfile.TemporaryDirectory() as tmp:
        tmp_dir = Path(tmp)
        tmp_obj = tmp_dir / f"{lump}.obj"
        scene.export(str(tmp_obj))

        # Collect whatever trimesh wrote and normalize names.
        exported = list(tmp_dir.iterdir())
        obj_src = next(p for p in exported if p.suffix.lower() == ".obj")
        mtl_src = next((p for p in exported if p.suffix.lower() == ".mtl"), None)
        png_srcs = [p for p in exported if p.suffix.lower() in (".png", ".jpg", ".jpeg")]

        obj_dst = out_dir / f"{lump}.obj"
        mtl_dst = out_dir / f"{lump}.mtl"
        albedo_dst = out_dir / f"{lump}_albedo.png"

        # Prefer explicit albedo from material if export skipped textures.
        albedo_img = _extract_albedo_image(scene)
        if albedo_img is not None:
            albedo_img.save(albedo_dst)
        elif png_srcs:
            shutil.copy2(png_srcs[0], albedo_dst)
        else:
            raise RuntimeError(f"No albedo texture found in {glb_path}")

        # Rewrite OBJ to reference our MTL name.
        obj_text = obj_src.read_text(encoding="utf-8", errors="replace")
        lines = []
        saw_mtllib = False
        for line in obj_text.splitlines():
            if line.startswith("mtllib "):
                lines.append(f"mtllib {lump}.mtl")
                saw_mtllib = True
            else:
                lines.append(line)
        if not saw_mtllib:
            lines.insert(0, f"mtllib {lump}.mtl")
        # Keep the trimesh header comment style used by existing assets.
        if not any(line.startswith("# https://github.com/mikedh/trimesh") for line in lines[:3]):
            lines.insert(0, "# https://github.com/mikedh/trimesh")
            lines.insert(1, "")
        obj_dst.write_text("\n".join(lines) + "\n", encoding="utf-8")

        # Write a simple MTL that matches CLIPA0/SBOXA0 convention.
        mtl_dst.write_text(
            "\n".join(
                [
                    "# https://github.com/mikedh/trimesh",
                    "",
                    "newmtl material_0",
                    "Ka 0.40000000 0.40000000 0.40000000",
                    "Kd 1.00000000 1.00000000 1.00000000",
                    "Ks 0.40000000 0.40000000 0.40000000",
                    "Ns 1.00000000",
                    f"map_Kd {lump}_albedo.png",
                    "",
                ]
            ),
            encoding="utf-8",
        )

        # Ensure usemtl exists in OBJ.
        obj_body = obj_dst.read_text(encoding="utf-8")
        if "usemtl " not in obj_body:
            patched = []
            inserted = False
            for line in obj_body.splitlines():
                patched.append(line)
                if line.startswith("o ") and not inserted:
                    patched.append("usemtl material_0")
                    inserted = True
            if not inserted:
                patched.insert(2, "usemtl material_0")
            obj_dst.write_text("\n".join(patched) + "\n", encoding="utf-8")

        _ = mtl_src  # unused; we rewrite MTL intentionally


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--glb", required=True)
    parser.add_argument("--out-dir", required=True)
    parser.add_argument("--lump", required=True)
    args = parser.parse_args()
    convert(Path(args.glb), Path(args.out_dir), args.lump)
    print(f"OK {args.lump} -> {args.out_dir}")


if __name__ == "__main__":
    main()
