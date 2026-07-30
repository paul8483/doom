# Real-ESRGAN ONNX models (Gate 0 candidates)

Weights for the Neural Sprite Upscale experiment. These are **algorithm
parameters** (like Super-xBR code), not authored game content. Inference
input is always WAD-decoded pixels only.

## License

BSD 3-Clause — see `LICENSE.txt` (upstream: xinntao/Real-ESRGAN,
Copyright (c) 2021, Xintao Wang).

## Models

| File | Role | Size | SHA-256 |
|------|------|------|--------|
| `RealESRGAN_x4plus_anime_6B.onnx` | Primary candidate (anime / flat art, 4×) | ~18 MB | `2648CAB4C4343541C1AA291C6754E9E8EDBE7A813FFFC2A677423DD12CB6B7F7` |
| `realesr-animevideov3_x4.onnx` | Lightweight alternate (4×) | ~2.5 MB | `00ECE3AC21C43EE31459216B5174B2CEA0C5325044C5142AEB840F4890E175FF` |

### Provenance

- Official training weights: [xinntao/Real-ESRGAN](https://github.com/xinntao/Real-ESRGAN)
  - `RealESRGAN_x4plus_anime_6B.pth`
  - `realesr-animevideov3.pth` (release v0.2.5.0)
- ONNX artifacts vendored here (no local torch available at Gate 0):
  - anime_6B: [deepghs/imgutils-models](https://huggingface.co/deepghs/imgutils-models/blob/main/real_esrgan/RealESRGAN_x4plus_anime_6B.onnx)
  - animevideov3 x4: [tidus2102/Real-ESRGAN](https://huggingface.co/tidus2102/Real-ESRGAN/blob/main/RealESR-AnimeVideo-v3_x4.onnx)

### Official conversion (when torch is available)

```bash
# From a Real-ESRGAN clone with weights downloaded:
python scripts/pytorch2onnx.py \
  -i weights/RealESRGAN_x4plus_anime_6B.pth \
  -o RealESRGAN_x4plus_anime_6B.onnx \
  --params params_realesrganet_x4plus_anime_6B
```

After replacing an ONNX file, update the SHA-256 row above and bump
`EnhancedPipelineVersion` (runtime rule once neural is wired).

## Runtime note

Loaded via Unity Sentis / Inference Engine (`com.unity.ai.inference`).
RGB only — alpha stays on the Super-xBR path (see design spec).
