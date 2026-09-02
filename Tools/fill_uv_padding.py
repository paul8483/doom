"""Fill the unused atlas padding of a doomified albedo with island colours.

Why: Unity imports the pickup/monster albedos with mipmaps and Point
filtering. A TRELLIS bake leaves the texels between UV islands dark, and
the bake itself carries a dark anti-aliased fringe along every island edge.
At 256 px the BOX downsample diluted both; at 512 px an object that spans
~150 screen px samples mips 1-2, whose box averages mix the dark padding and
the dark fringe into every triangle border — black speckles along the UV
seams of the health flask (BON1A0, 2026-09-02), the same mechanism as the
corpse-512 wave (there fixed by hand, here as a tool).

What it does (texture only — mesh, UVs and emission zones untouched):
  1. coverage  = texels referenced by the OBJ's UV triangles
     (doomify3d.uv_coverage_mask);
  2. --rim N   : the N-texel inner ring of each island is treated as
                 unreliable bake fringe and repainted from the island
                 interior (islands too thin to survive the erosion keep
                 their own texels);
  3. --despeckle T : isolated dark texels inside the islands (lum < T while
                 the 5x5 median around them is brighter by >= 30) take the
                 median colour — bake noise that palette quantization
                 snapped to the sprite's outline colours;
  4. every uncovered texel takes the colour of its nearest kept texel
     (exact EDT nearest, no blur), so the mips average island colour with
     island colour;
  5. changed texels are re-snapped to the colours the albedo already used,
     so the palette stays the native one doomify3d chose.
The emission mask gets step 4 only (its zones are authored, not baked).

Usage:
  python Tools/fill_uv_padding.py Assets/Resources/ExperimentalPickups/BON1A0/BON1A0.obj \
      --albedo BON1A0_albedo.png [--emission BON1A0_emission.png] \
      [--rim 2] [--despeckle 60] [--out-dir DIR] [--dump-mask]
Paths of --albedo/--emission are relative to the OBJ's folder; without
--out-dir the files are rewritten in place (a .bak copy is kept).
"""
import argparse
import shutil
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

sys.path.insert(0, str(Path(__file__).resolve().parent))
import doomify3d  # noqa: E402


def lum(rgb: np.ndarray) -> np.ndarray:
    return rgb[..., 0] * 0.299 + rgb[..., 1] * 0.587 + rgb[..., 2] * 0.114


def nearest_fill(rgb: np.ndarray, keep: np.ndarray) -> np.ndarray:
    """Every texel outside `keep` takes the colour of the nearest kept texel."""
    if keep.all():
        return rgb.copy()
    _, (iy, ix) = ndimage.distance_transform_edt(~keep, return_indices=True)
    return rgb[iy, ix]


def erode_islands(cov: np.ndarray, rim: int) -> np.ndarray:
    """Coverage eroded by `rim`, but islands that would vanish keep themselves."""
    if rim <= 0:
        return cov
    eroded = ndimage.binary_erosion(cov, iterations=rim, border_value=0)
    labels, n = ndimage.label(cov)
    if n == 0:
        return cov
    survivors = ndimage.sum(eroded, labels, index=np.arange(1, n + 1)) > 0
    keep = eroded.copy()
    for i in np.nonzero(~survivors)[0]:
        keep |= labels == (i + 1)
    return keep


def despeckle(rgb: np.ndarray, region: np.ndarray, thr: float) -> np.ndarray:
    """Isolated dark texels inside `region` take the local 5x5 median colour."""
    out = rgb.copy()
    L = lum(rgb)
    medL = ndimage.median_filter(L, size=5)
    dark = region & (L < thr) & (medL - L >= 30)
    if not dark.any():
        return out, 0
    for c in range(3):
        medc = ndimage.median_filter(rgb[..., c], size=5)
        out[..., c][dark] = medc[dark]
    return out, int(dark.sum())


def tri_despeckle(rgb: np.ndarray, obj_path: Path, thr: float, passes: int = 2):
    """Mesh-aware despeckle: a triangle whose atlas footprint is dark while
    most of its vertex-adjacent neighbours are bright takes the median colour
    of those neighbours. Aimed at the 40k-triangle decimations whose tiny
    triangles (a third of BON1A0's rasterize to no whole texel) sample one
    fringe texel each and light up as black dots on the glass."""
    from collections import defaultdict
    from PIL import ImageDraw
    verts, tris, cuv = doomify3d.load_obj(obj_path)
    h, w = rgb.shape[:2]
    n = len(cuv)
    ids_img = Image.new("I", (w, h), -1)
    d = ImageDraw.Draw(ids_img)
    for i, tri in enumerate(cuv):
        d.polygon([(u * (w - 1), (1.0 - v) * (h - 1)) for u, v in tri], fill=i)
    ids = np.asarray(ids_img)
    c = cuv.mean(1)
    cx = np.clip((c[:, 0] * (w - 1)).round().astype(int), 0, w - 1)
    cy = np.clip(((1 - c[:, 1]) * (h - 1)).round().astype(int), 0, h - 1)
    adj = defaultdict(set)
    byv = defaultdict(list)
    for i, t in enumerate(tris):
        for v in t:
            byv[v].append(i)
    for lst in byv.values():
        for i in lst:
            adj[i].update(lst)
    out = rgb.copy()
    total = 0
    for _ in range(passes):
        L = lum(out)
        inside = ids >= 0
        cnt = np.bincount(ids[inside], minlength=n).astype(float)
        sums = [np.bincount(ids[inside], weights=out[..., k][inside], minlength=n)
                for k in range(3)]
        empty = cnt == 0
        mean = np.stack([np.where(empty, out[cy, cx, k], sums[k] / np.maximum(cnt, 1))
                         for k in range(3)], 1)
        meanL = mean @ np.array([0.299, 0.587, 0.114])
        fixed = 0
        for i in range(n):
            if meanL[i] >= thr:
                continue
            nb = [j for j in adj[i] if j != i]
            if not nb:
                continue
            nbL = meanL[nb]
            bright = nbL > thr + 30
            if bright.mean() < 0.6:
                continue
            col = np.median(mean[np.array(nb)[bright]], axis=0)
            foot = ids == i
            foot[cy[i], cx[i]] = True
            foot = ndimage.binary_dilation(foot, iterations=1) & ((ids == i) | ~inside | foot)
            out[foot] = col
            fixed += 1
        total += fixed
        if fixed == 0:
            break
    return out, total


def snap_to_palette(rgb: np.ndarray, changed: np.ndarray, palette: np.ndarray) -> np.ndarray:
    out = rgb.copy()
    pts = rgb[changed].astype(np.int32)
    if len(pts) == 0:
        return out
    pal = palette.astype(np.int32)
    # chunked nearest-colour search (palette is small: tens of colours)
    best = np.empty(len(pts), np.int64)
    for s in range(0, len(pts), 65536):
        d = ((pts[s:s + 65536, None, :] - pal[None, :, :]) ** 2).sum(-1)
        best[s:s + 65536] = d.argmin(1)
    out[changed] = palette[best]
    return out


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("obj", type=Path)
    ap.add_argument("--albedo", required=True)
    ap.add_argument("--emission", default=None)
    ap.add_argument("--rim", type=int, default=0)
    ap.add_argument("--despeckle", type=float, default=None)
    ap.add_argument("--tri-despeckle", type=float, default=None,
                    help="repaint dark triangles among bright neighbours (lum threshold)")
    ap.add_argument("--out-dir", type=Path, default=None)
    ap.add_argument("--dump-mask", action="store_true")
    a = ap.parse_args()

    folder = a.obj.parent
    alb_path = folder / a.albedo
    img = Image.open(alb_path)
    rgba = np.asarray(img.convert("RGBA")).copy()
    rgb = rgba[..., :3]
    h, w = rgb.shape[:2]
    cov = doomify3d.uv_coverage_mask(a.obj, (w, h))
    palette = np.unique(rgb[cov].reshape(-1, 3), axis=0)
    print(f"{alb_path.name}: {w}x{h}, coverage {cov.mean():.3f}, "
          f"palette {len(palette)} colours, mean lum inside {lum(rgb)[cov].mean():.1f} "
          f"outside {lum(rgb)[~cov].mean():.1f}")

    keep = erode_islands(cov, a.rim)
    work = rgb.copy()
    n_spec = 0
    if a.despeckle is not None:
        work, n_spec = despeckle(work, keep, a.despeckle)
    if a.tri_despeckle is not None:
        work, n_tri = tri_despeckle(work, a.obj, a.tri_despeckle)
        print(f"  tri-despeckle {a.tri_despeckle}: repainted {n_tri} triangles")
    filled = nearest_fill(work, keep)
    changed = np.any(filled != rgb, axis=-1)
    filled = snap_to_palette(filled, changed, palette)
    print(f"  rim {a.rim}: kept {keep.mean():.3f} of atlas; despeckled {n_spec} texels; "
          f"changed {changed.mean():.3f} of atlas ({(changed & cov).mean():.3f} inside coverage); "
          f"mean lum outside now {lum(filled)[~cov].mean():.1f}, "
          f"island-border dark(<60) {(lum(rgb)[cov & ndimage.binary_dilation(~cov)] < 60).mean():.3f} -> "
          f"{(lum(filled)[cov & ndimage.binary_dilation(~cov)] < 60).mean():.3f}")

    out_dir = a.out_dir or folder
    out_dir.mkdir(parents=True, exist_ok=True)
    if a.out_dir is None:
        shutil.copyfile(alb_path, alb_path.with_suffix(".png.bak"))
    rgba[..., :3] = filled
    Image.fromarray(rgba).save(out_dir / a.albedo)

    if a.emission:
        em_path = folder / a.emission
        em = np.asarray(Image.open(em_path).convert("RGBA")).copy()
        em[..., :3] = nearest_fill(em[..., :3], cov)
        if a.out_dir is None:
            shutil.copyfile(em_path, em_path.with_suffix(".png.bak"))
        Image.fromarray(em).save(out_dir / a.emission)
        print(f"  emission padding filled: {em_path.name}")

    if a.dump_mask:
        Image.fromarray((keep * 255).astype(np.uint8)).save(out_dir / "uv_keep_mask.png")
        Image.fromarray((changed * 255).astype(np.uint8)).save(out_dir / "uv_changed_mask.png")


if __name__ == "__main__":
    main()
