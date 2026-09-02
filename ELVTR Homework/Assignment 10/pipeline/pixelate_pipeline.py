#!/usr/bin/env python3
"""
Assignment #10 — texture pixelation pipeline.

Standalone, single-image version of the pixelation step Mr. Moonlight actually
ships with: Tools/pipeline/texture_pass.py in the main project repo. That
script processes whole prop folders (BaseColor + Normal + AO, two-map RetroLit
standard); this one does the one thing this assignment asks for so it can run
on its own, with no dependency on the game project: feed it ONE image, it
resizes to a target size (default 512, nearest-neighbour so edges stay hard)
and runs the same ordered-dither colour quantisation pass over it.

The quantiser is copied verbatim from texture_pass.py's pixelate() function —
same 4x4 Bayer matrix (measured off Retro Realism's own Bayer4x4.tga), same
per-channel level count, same math. This is not a reimplementation invented
for the homework; it is the real technique, extracted so it can run in
isolation.

Usage:
    python pixelate_pipeline.py <input_image> [--size 512] [--levels 10] [--dither 1.0] [--out PATH]

No API calls, no network, no LLM in the loop. Deterministic image processing:
same input + same flags always produce the same output.
"""

from __future__ import annotations

import argparse
import sys
import time
from pathlib import Path

try:
    import numpy as np
    from PIL import Image
except ImportError:
    sys.exit("Needs Pillow and numpy:  python -m pip install pillow numpy")


# The textbook 4x4 ordered Bayer matrix — identical to the one in the shipped
# Tools/pipeline/texture_pass.py, measured off Retro Realism's Bayer4x4.tga.
BAYER4 = np.array(
    [
        [0, 8, 2, 10],
        [12, 4, 14, 6],
        [3, 11, 1, 9],
        [15, 7, 13, 5],
    ],
    dtype=np.float32,
)

DEFAULT_SIZE = 512
DEFAULT_LEVELS = 10
DEFAULT_DITHER = 1.0

READABLE = (".png", ".tga", ".tif", ".tiff", ".jpg", ".jpeg", ".bmp")


def bayer_tile(h: int, w: int) -> np.ndarray:
    """Bayer matrix tiled to h x w, centred on zero in the range [-0.5, 0.5]."""
    t = np.tile(BAYER4 / 16.0 - 0.5, (h // 4 + 1, w // 4 + 1))
    return t[:h, :w]


def resize(img: Image.Image, size: int) -> Image.Image:
    """Nearest-neighbour resize so quantised pixels stay hard-edged, not blurred."""
    return img.resize((size, size), Image.NEAREST)


def pixelate(img: Image.Image, levels: int, dither: float) -> Image.Image:
    """Quantise to `levels` per channel with ordered (Bayer) dithering.

    Alpha is preserved and never dithered — dithering it would fringe a
    cutout edge.
    """
    has_alpha = "A" in img.getbands()
    alpha = np.asarray(img.getchannel("A")) if has_alpha else None

    rgb = np.asarray(img.convert("RGB"), dtype=np.float32) / 255.0
    h, w = rgb.shape[:2]

    step = 1.0 / (levels - 1)
    offset = bayer_tile(h, w)[:, :, None] * step * dither
    q = np.round(np.clip(rgb + offset, 0.0, 1.0) * (levels - 1)) / (levels - 1)

    out = Image.fromarray((q * 255.0 + 0.5).astype(np.uint8), "RGB")
    if has_alpha:
        out = out.convert("RGBA")
        out.putalpha(Image.fromarray(alpha))
    return out


def run(input_path: Path, size: int, levels: int, dither: float, out_path: Path) -> dict:
    """Runs resize + pixelate on one image and reports real, measured numbers.

    No API calls happen anywhere in this function — this is the entire cost
    of the pipeline: local CPU time, nothing else.
    """
    t_start = time.perf_counter()

    with Image.open(input_path) as im:
        im.load()
        original_size = im.size
        original_mode = im.mode
        resized = resize(im, size)
        result = pixelate(resized, levels, dither)

    out_path.parent.mkdir(parents=True, exist_ok=True)
    result.save(out_path)

    elapsed = time.perf_counter() - t_start
    in_bytes = input_path.stat().st_size
    out_bytes = out_path.stat().st_size

    return {
        "input_path": str(input_path),
        "output_path": str(out_path),
        "original_size": original_size,
        "original_mode": original_mode,
        "output_size": (size, size),
        "levels": levels,
        "dither": dither,
        "elapsed_seconds": elapsed,
        "input_bytes": in_bytes,
        "output_bytes": out_bytes,
        "api_calls": 0,
        "api_cost_usd": 0.0,
    }


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("input", type=Path, help="source texture image")
    ap.add_argument("--size", type=int, default=DEFAULT_SIZE, help=f"square output size (default {DEFAULT_SIZE})")
    ap.add_argument("--levels", type=int, default=DEFAULT_LEVELS, help=f"colour levels per channel (default {DEFAULT_LEVELS})")
    ap.add_argument("--dither", type=float, default=DEFAULT_DITHER, help="Bayer dither strength 0..1 (default 1.0; 0 disables)")
    ap.add_argument("--out", type=Path, default=None, help="output path (default: <name>_pixelated.png next to input)")

    args = ap.parse_args()

    if not args.input.is_file():
        print(f"Not a file: {args.input}")
        return 1

    if args.input.suffix.lower() not in READABLE:
        print(f"Unreadable format '{args.input.suffix}'. Convert to one of: {', '.join(READABLE)}")
        return 1

    out_path = args.out or args.input.with_name(f"{args.input.stem}_{args.size}_pixelated.png")

    stats = run(args.input, args.size, args.levels, args.dither, out_path)

    print(f"input       {stats['input_path']}  ({stats['original_size'][0]}x{stats['original_size'][1]}, {stats['original_mode']}, {stats['input_bytes']:,} bytes)")
    print(f"output      {stats['output_path']}  ({stats['output_size'][0]}x{stats['output_size'][1]}, {stats['output_bytes']:,} bytes)")
    print(f"settings    levels={stats['levels']} dither={stats['dither']}")
    print(f"time        {stats['elapsed_seconds']:.4f}s (CPU, local)")
    print(f"api calls   {stats['api_calls']}")
    print(f"api cost    ${stats['api_cost_usd']:.2f}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
