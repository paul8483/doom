"""Redraw the paired 4x6 status-bar digits at a hard-edged 16x scale."""
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter


ROOT = Path(__file__).resolve().parent.parent / "Textures" / "HudRedraw"
SCALE = 16


def luminance(rgb: np.ndarray) -> np.ndarray:
    return rgb[..., 0] * 0.2126 + rgb[..., 1] * 0.7152 + rgb[..., 2] * 0.0722


def shared_bevel(mask: np.ndarray) -> np.ndarray:
    """Return one restrained top-left bevel field shared by both paints."""
    soft = np.asarray(
        Image.fromarray(mask.astype(np.uint8) * 255, "L").filter(
            ImageFilter.GaussianBlur(radius=3.0)
        ),
        dtype=np.float32,
    ) / 255.0
    gy, gx = np.gradient(soft)
    directional = -(gx + gy) / np.sqrt(2.0)
    peak = max(float(np.max(np.abs(directional))), 1e-6)
    directional /= peak

    height = np.linspace(1.0, -1.0, mask.shape[0], dtype=np.float32)[:, None]
    field = 1.0 + 0.13 * directional + 0.025 * height
    return np.clip(field, 0.82, 1.18)


def paint(mask: np.ndarray, field: np.ndarray, native: np.ndarray, yellow: bool) -> np.ndarray:
    source = native[..., :3][native[..., 3] != 0].astype(np.float32)
    base = source.mean(axis=0)
    rgb = base[None, None, :] * field[..., None]

    # Keep the bevel chromatically legible without adding surface noise.
    highlight = np.clip(field - 1.0, 0.0, None)[..., None]
    tint = np.array([10.0, 5.0, -5.0] if yellow else [-3.0, 2.0, 8.0])
    rgb += highlight * tint

    # Preserve each native paint's mean perceived brightness.
    target_luma = float(luminance(source).mean())
    current_luma = float(luminance(rgb[mask]).mean())
    rgb *= target_luma / max(current_luma, 1e-6)
    rgb = np.clip(np.rint(rgb), 0, 255).astype(np.uint8)

    out = np.zeros((*mask.shape, 4), dtype=np.uint8)
    out[mask, :3] = rgb[mask]
    out[mask, 3] = 255
    return out


def main() -> None:
    max_luma_drift = 0.0
    for digit in range(10):
        yellow_dir = ROOT / f"STYSNUM{digit}"
        gray_dir = ROOT / f"STGNUM{digit}"
        yellow = np.asarray(Image.open(yellow_dir / "native.png").convert("RGBA"))
        gray = np.asarray(Image.open(gray_dir / "native.png").convert("RGBA"))

        if yellow.shape != (6, 4, 4) or gray.shape != (6, 4, 4):
            raise ValueError(f"digit {digit}: expected paired 4x6 RGBA inputs")
        if not np.array_equal(yellow[..., 3], gray[..., 3]):
            raise ValueError(f"digit {digit}: yellow/gray silhouettes differ")

        mask = np.repeat(np.repeat(yellow[..., 3] != 0, SCALE, axis=0), SCALE, axis=1)
        field = shared_bevel(mask)
        yellow_out = paint(mask, field, yellow, True)
        gray_out = paint(mask, field, gray, False)

        expected_alpha = np.repeat(
            np.repeat(yellow[..., 3], SCALE, axis=0), SCALE, axis=1
        )
        for native, output in ((yellow, yellow_out), (gray, gray_out)):
            if output.shape != (96, 64, 4):
                raise ValueError(f"digit {digit}: invalid output dimensions")
            if not np.array_equal(output[..., 3], expected_alpha):
                raise ValueError(f"digit {digit}: output silhouette drifted")
            native_luma = float(luminance(native[..., :3])[native[..., 3] != 0].mean())
            output_luma = float(luminance(output[..., :3])[mask].mean())
            max_luma_drift = max(max_luma_drift, abs(output_luma - native_luma))

        Image.fromarray(yellow_out, "RGBA").save(yellow_dir / "redraw.png")
        Image.fromarray(gray_out, "RGBA").save(gray_dir / "redraw.png")

    print(
        "Wrote 20 paired 64x96 hard-edged HUD digit redraws; "
        f"max mean-luminance drift {max_luma_drift:.3f}."
    )


if __name__ == "__main__":
    main()
