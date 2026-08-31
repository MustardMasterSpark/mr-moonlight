#!/usr/bin/env python3
"""
Mr. Moonlight — terrain texture pass (MRM-70).

Batch-converts downloaded Poly Haven-style ground texture sets (Diffuse JPG/PNG +
nor_gl EXR normal + Roughness JPG/EXR, 4K) into the two files a Gaia TerrainLayer
actually needs:

    T_Terrain_<NN>_<Biome>_Diffuse.png   RGBA, pixelated RGB + smoothness in ALPHA
    T_Terrain_<NN>_<Biome>_Normal.png    RGB, Lanczos-resampled, renormalized, untouched by pixelation

Why alpha carries smoothness: this project's terrain shader reads smoothness from
the diffuse texture's ALPHA channel, not the TerrainLayer's own Smoothness slider
(confirmed dead control, see Docs/mrm70-biome-vegetation-strategy.md — a fully
opaque diffuse alpha caused the "mirror-finish ground" bug fixed 2026-08-25).
Smoothness = 1 - roughness, taken from the source Roughness map so it is measured,
not guessed, and folded into the diffuse PNG's alpha at export.

Reuses Docs/3d-asset-pipeline.md's Bayer-dither quantiser from texture_pass.py so
terrain and props share one visual style. Needs an EXR reader for the nor_gl
normal maps and (on some biomes) the Roughness map — imageio + imageio-freeimage:

    python -m pip install pillow numpy imageio imageio-freeimage
    python -c "import imageio; imageio.plugins.freeimage.download()"   # one-time

Usage:
    python terrain_texture_pass.py run <source_root> <output_dir> [--size 1024]

<source_root> holds one subfolder per biome, e.g. "01_Forest", "02_Autumn Forest",
each with a textures/ folder containing *_diff*_4k.*, *_nor_gl_4k.exr,
*_rough_4k.*. Matches the "Terrain Sources" layout Carlos downloaded into.
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

try:
    import numpy as np
    from PIL import Image
except ImportError:
    sys.exit("Needs Pillow and numpy:  python -m pip install pillow numpy")

sys.path.insert(0, str(Path(__file__).parent))
from texture_pass import BAYER4, DEFAULT_DITHER, DEFAULT_LEVELS, pixelate  # noqa: E402

DEFAULT_SIZE = 1024

DIFF_RE = re.compile(r"_diff(use)?_4k\.", re.IGNORECASE)
NORMAL_RE = re.compile(r"_nor_gl_4k\.exr$", re.IGNORECASE)
ROUGH_RE = re.compile(r"_rough_4k\.", re.IGNORECASE)


def _exr_reader():
    try:
        import imageio.v3 as iio
    except ImportError:
        sys.exit(
            "Needs imageio + imageio-freeimage for the .exr normal/roughness maps:\n"
            "  python -m pip install imageio imageio-freeimage\n"
            "  python -c \"import imageio; imageio.plugins.freeimage.download()\""
        )
    return iio


def find_one(folder: Path, pattern: re.Pattern) -> Path | None:
    for p in sorted(folder.rglob("*")):
        if p.is_file() and pattern.search(p.name):
            return p
    return None


def load_gray_any(path: Path) -> np.ndarray:
    """Single-channel float32 0..1, from PNG/JPG via Pillow or EXR via imageio."""
    if path.suffix.lower() == ".exr":
        iio = _exr_reader()
        arr = iio.imread(str(path), plugin="EXR-FI")
        arr = np.clip(np.asarray(arr, dtype=np.float32), 0.0, 1.0)
        if arr.ndim == 3:
            arr = arr[:, :, 0]  # roughness EXRs are greyscale broadcast across RGB
        return arr
    return np.asarray(Image.open(path).convert("L"), dtype=np.float32) / 255.0


def resize_gray(arr: np.ndarray, size: int) -> np.ndarray:
    if arr.shape[:2] == (size, size):
        return arr
    im = Image.fromarray((np.clip(arr, 0.0, 1.0) * 255.0).astype(np.uint8), "L")
    im = im.resize((size, size), Image.LANCZOS)
    return np.asarray(im, dtype=np.float32) / 255.0


def process_diffuse(diff_path: Path, rough_path: Path | None, size: int) -> Image.Image:
    with Image.open(diff_path) as im:
        im = im.convert("RGB")
        if im.size != (size, size):
            im = im.resize((size, size), Image.LANCZOS)
        pixelated = pixelate(im, DEFAULT_LEVELS, DEFAULT_DITHER, None)  # already at target size

    if rough_path is not None:
        roughness = resize_gray(load_gray_any(rough_path), size)
        smoothness = 1.0 - roughness
    else:
        smoothness = np.full((size, size), 0.1, dtype=np.float32)  # matches project's CONST_SMOOTHNESS fallback

    out = pixelated.convert("RGBA")
    out.putalpha(Image.fromarray((smoothness * 255.0 + 0.5).astype(np.uint8), "L"))
    return out


def process_normal(normal_path: Path, size: int) -> Image.Image:
    """Lanczos-resample an OpenGL-convention (Y+) normal EXR, then renormalize.

    Plain per-channel resampling drifts vector length off 1.0 at edges/detail;
    renormalizing after resize keeps the result a valid tangent-space normal
    instead of just a resized colour image. Never quantised/dithered — that
    produces faceted lighting (Docs/3d-asset-pipeline.md).
    """
    iio = _exr_reader()
    arr = iio.imread(str(normal_path), plugin="EXR-FI")
    arr = np.clip(np.asarray(arr, dtype=np.float32), 0.0, 1.0)[:, :, :3]  # drop alpha if present

    vec = arr * 2.0 - 1.0  # decode 0..1 -> -1..1

    if arr.shape[:2] != (size, size):
        resized = np.empty((size, size, 3), dtype=np.float32)
        for c in range(3):
            chan = Image.fromarray(vec[:, :, c], mode="F")
            chan = chan.resize((size, size), Image.LANCZOS)
            resized[:, :, c] = np.asarray(chan, dtype=np.float32)
        vec = resized

    length = np.sqrt(np.sum(vec * vec, axis=-1, keepdims=True))
    length = np.maximum(length, 1e-6)
    vec = vec / length

    encoded = (vec + 1.0) * 0.5  # -1..1 -> 0..1
    return Image.fromarray((np.clip(encoded, 0.0, 1.0) * 255.0 + 0.5).astype(np.uint8), "RGB")


def biome_label(folder_name: str) -> tuple[str, str]:
    """'02_Autumn Forest' -> ('02', 'AutumnForest')."""
    num, _, name = folder_name.partition("_")
    return num, name.replace(" ", "")


def run(source_root: Path, out_dir: Path, size: int) -> int:
    out_dir.mkdir(parents=True, exist_ok=True)
    biome_folders = sorted(
        p for p in source_root.iterdir() if p.is_dir() and re.match(r"^\d\d_", p.name)
    )
    if not biome_folders:
        print(f"No NN_BiomeName folders found in {source_root}")
        return 1

    written = 0
    for folder in biome_folders:
        num, biome = biome_label(folder.name)
        textures_dir = folder / "textures"
        search_dir = textures_dir if textures_dir.is_dir() else folder

        diff_path = find_one(search_dir, DIFF_RE)
        normal_path = find_one(search_dir, NORMAL_RE)
        rough_path = find_one(search_dir, ROUGH_RE)

        print(f"[{folder.name}]")
        if diff_path is None:
            print("  MISSING diffuse — skipped")
            print()
            continue
        if normal_path is None:
            print("  MISSING nor_gl EXR normal — skipped")
            print()
            continue
        if rough_path is None:
            print(f"  no roughness map found — smoothness falls back to constant 0.1")

        diff_out = out_dir / f"T_Terrain_{num}_{biome}_Diffuse.png"
        normal_out = out_dir / f"T_Terrain_{num}_{biome}_Normal.png"

        process_diffuse(diff_path, rough_path, size).save(diff_out)
        print(f"  diffuse  {diff_path.name} (+ {rough_path.name if rough_path else 'no roughness'})  ->  {diff_out.name}")

        process_normal(normal_path, size).save(normal_out)
        print(f"  normal   {normal_path.name}  ->  {normal_out.name}")

        written += 2
        print()

    print(f"{written} file(s) -> {out_dir}")
    return 0


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)

    r = sub.add_parser("run", help="process every biome folder under source_root")
    r.add_argument("source_root", type=Path)
    r.add_argument("output_dir", type=Path)
    r.add_argument("--size", type=int, default=DEFAULT_SIZE, help=f"output resolution, square (default {DEFAULT_SIZE})")

    args = ap.parse_args()
    if not args.source_root.is_dir():
        print(f"Not a folder: {args.source_root}")
        return 1

    print(f"size={args.size} levels={DEFAULT_LEVELS} dither={DEFAULT_DITHER}\n")
    return run(args.source_root, args.output_dir, args.size)


if __name__ == "__main__":
    raise SystemExit(main())
