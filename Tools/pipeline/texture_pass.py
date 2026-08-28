#!/usr/bin/env python3
"""
Mr. Moonlight — texture pass.

Runs the pixelation and mask-packing steps of Docs/3d-asset-pipeline.md locally.
No Claude, no Substance, no Unity. Just point it at a folder.

    python texture_pass.py run <folder>

TWO-MAP STANDARD (MRM-72, 2026-08-27). RetroLit.shader samples only _BaseMap
and _NormalMap — there is no _MetallicGlossMap, no _OcclusionMap and no
emission map anywhere in Retro Shaders Pro. So by default this writes exactly
two files per asset, and AO is multiplied INTO the BaseColor — the only way
RetroLit will ever show it, and what PSX-era art actually did.

Naming convention (case-insensitive suffix before the extension):

    Rock_BaseColor.png   -> AO multiplied in, then pixelated (quantise + dither)
    Rock_Emission.png    -> pixelated
    Rock_Normal.png      -> passed through, resampled to --map-size
    Rock_AO.png          -> multiplied into the matching BaseColor
    Rock_Rough.png           Rock_Metal.png        >  unused unless --mask; reported, never mangled
    Rock_Mask.png        /

Pass --mask to restore the old three-map behaviour and pack
R=metallic, G=occlusion, B=0, A=smoothness. Only worth it for an asset headed
for URP/Lit rather than RetroLit; metallic and smoothness are otherwise the
material's scalar _Glossiness, set once per material rather than per texel.

Outputs land in <folder>/_out/ — nothing is overwritten in place.
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

try:
    import numpy as np
    from PIL import Image
except ImportError:
    sys.exit("Needs Pillow and numpy:  python -m pip install pillow numpy")


# The textbook 4x4 ordered Bayer matrix. Measured out of Retro Realism's
# own Bayer4x4.tga, so this reproduces the style baseline's dither exactly.
BAYER4 = np.array(
    [
        [0, 8, 2, 10],
        [12, 4, 14, 6],
        [3, 11, 1, 9],
        [15, 7, 13, 5],
    ],
    dtype=np.float32,
)

# Defaults from the measured style baseline (9-12 levels per channel).
DEFAULT_LEVELS = 10
DEFAULT_DITHER = 1.0

# Fallbacks when a bake input is absent.
CONST_METALLIC = 0.0
CONST_OCCLUSION = 1.0
CONST_SMOOTHNESS = 0.1

# Extensions Pillow reads out of the box.
READABLE = (".png", ".tga", ".tif", ".tiff", ".jpg", ".jpeg", ".bmp")

# Image formats we knowingly cannot read without extra plugins. Listed so they
# can be reported rather than silently filtered out of existence.
UNREADABLE_IMAGE = (".exr", ".hdr", ".psd", ".dds")

PIXELATE = ("basecolor", "bc", "albedo", "emission", "e")
PASSTHROUGH = ("normal", "n", "mask", "m")
MASK_INPUTS = ("ao", "occlusion", "rough", "roughness", "metal", "metallic", "smooth")


def suffix_of(path: Path) -> str:
    """Trailing _Token of a filename stem, lowercased. '' if there is none."""
    stem = path.stem
    return stem.rsplit("_", 1)[-1].lower() if "_" in stem else ""


def base_of(path: Path) -> str:
    """Filename stem with its trailing _Token removed."""
    stem = path.stem
    return stem.rsplit("_", 1)[0] if "_" in stem else stem


def load_gray(path: Path) -> np.ndarray:
    """Single channel as float 0..1."""
    return np.asarray(Image.open(path).convert("L"), dtype=np.float32) / 255.0


def bayer_tile(h: int, w: int) -> np.ndarray:
    """Bayer matrix tiled to h x w, centred on zero in the range [-0.5, 0.5]."""
    t = np.tile(BAYER4 / 16.0 - 0.5, (h // 4 + 1, w // 4 + 1))
    return t[:h, :w]


def pixelate(img: Image.Image, levels: int, dither: float, size: int | None) -> Image.Image:
    """Quantise to `levels` per channel with ordered dithering.

    Resizing, if asked for, is nearest-neighbour so pixels stay hard-edged.
    Alpha is preserved and never dithered - dithering it would fringe the
    cutout edge on foliage.
    """
    if size:
        img = img.resize((size, size), Image.NEAREST)

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


def multiply_ao(img: Image.Image, ao_path: Path) -> Image.Image:
    """Multiply an AO map into an image's RGB, leaving alpha untouched.

    This is the two-map standard's occlusion step: RetroLit has no
    _OcclusionMap slot, so baked AO either goes into the albedo here or it is
    thrown away. Applied at native resolution *before* pixelation, so the
    quantiser sees the final colour instead of quantising twice.
    """
    has_alpha = "A" in img.getbands()
    alpha = img.getchannel("A") if has_alpha else None

    rgb = np.asarray(img.convert("RGB"), dtype=np.float32) / 255.0
    h, w = rgb.shape[:2]

    with Image.open(ao_path) as ao_im:
        ao = ao_im.convert("L")
        if ao.size != (w, h):
            ao = ao.resize((w, h), Image.LANCZOS)
        ao_a = np.asarray(ao, dtype=np.float32) / 255.0

    out = Image.fromarray(
        (np.clip(rgb * ao_a[:, :, None], 0.0, 1.0) * 255.0 + 0.5).astype(np.uint8), "RGB"
    )
    if has_alpha:
        out = out.convert("RGBA")
        out.putalpha(alpha)
    return out


def build_mask(parts: dict[str, Path], size: int | None) -> Image.Image | None:
    """Pack R=metallic, G=occlusion, B=0, A=smoothness.

    Roughness is inverted into smoothness. Any channel with no source file
    falls back to its constant, so a partial bake still yields a valid Mask.
    """
    ref = next(iter(parts.values()), None)
    if ref is None:
        return None

    with Image.open(ref) as im:
        h, w = (size, size) if size else (im.height, im.width)

    def channel(names: tuple[str, ...], const: float, invert: bool = False) -> np.ndarray:
        for n in names:
            if n in parts:
                a = load_gray(parts[n])
                if a.shape != (h, w):
                    a = np.asarray(
                        Image.fromarray((a * 255).astype(np.uint8)).resize(
                            (w, h), Image.NEAREST
                        ),
                        dtype=np.float32,
                    ) / 255.0
                return 1.0 - a if invert else a
        return np.full((h, w), const, dtype=np.float32)

    metallic = channel(("metal", "metallic"), CONST_METALLIC)
    occlusion = channel(("ao", "occlusion"), CONST_OCCLUSION)

    if "smooth" in parts:
        smoothness = channel(("smooth",), CONST_SMOOTHNESS)
    else:
        smoothness = channel(("rough", "roughness"), CONST_SMOOTHNESS, invert=True)

    stack = np.stack(
        [metallic, occlusion, np.zeros_like(metallic), smoothness], axis=-1
    )
    return Image.fromarray((stack * 255.0 + 0.5).astype(np.uint8), "RGBA")


def run(
    folder: Path,
    levels: int,
    dither: float,
    size: int | None,
    map_size: int | None,
    want_mask: bool = False,
) -> int:
    out_dir = folder / "_out"
    out_dir.mkdir(exist_ok=True)

    files = [p for p in sorted(folder.iterdir()) if p.is_file()]
    images = [p for p in files if p.suffix.lower() in READABLE]

    # Anything image-shaped we cannot read must be REPORTED, never silently
    # dropped. Sources really do ship .exr normals (E:/Props/Props/RV), and a
    # map that vanishes without a word is the quietest possible failure.
    unreadable = [p for p in files if p.suffix.lower() in UNREADABLE_IMAGE]
    if unreadable:
        print("  CANNOT READ - convert these to PNG first:")
        for p in unreadable:
            print(f"    {p.name}")
        print()

    if not images:
        print(f"No readable images in {folder}")
        return 1

    # Index AO up front: it has to be multiplied into the BaseColor, which means
    # knowing about it before we reach the BaseColor in the sorted file list.
    ao_for: dict[str, Path] = {}
    for path in images:
        if suffix_of(path) in ("ao", "occlusion"):
            ao_for[base_of(path)] = path

    consumed_ao: set[str] = set()
    mask_groups: dict[str, dict[str, Path]] = {}
    unused: list[str] = []
    written = 0

    for path in images:
        sfx = suffix_of(path)

        if sfx in PIXELATE:
            base = base_of(path)
            with Image.open(path) as im:
                im.load()
                ao = ao_for.get(base)
                note = ""
                # Emission is self-lit - occluding it makes no physical sense.
                if ao is not None and not want_mask and sfx not in ("emission", "e"):
                    im = multiply_ao(im, ao)
                    consumed_ao.add(base)
                    note = f"  (AO {ao.name} multiplied in)"
                result = pixelate(im, levels, dither, size)
            dest = out_dir / f"{path.stem}.png"
            result.save(dest)
            print(f"  pixelated  {path.name}  ->  {dest.name}{note}")
            written += 1

        elif sfx in MASK_INPUTS:
            if want_mask:
                key = "smooth" if sfx == "smooth" else sfx
                mask_groups.setdefault(base_of(path), {})[key] = path
            elif sfx not in ("ao", "occlusion"):
                unused.append(path.name)

        elif sfx in PASSTHROUGH:
            if sfx in ("mask", "m") and not want_mask:
                unused.append(path.name)
                continue
            dest = out_dir / f"{path.stem}.png"
            with Image.open(path) as im:
                if map_size and im.size != (map_size, map_size):
                    im = im.resize((map_size, map_size), Image.LANCZOS)
                im.save(dest)
            print(f"  copied     {path.name}  ->  {dest.name}")
            written += 1

        else:
            print(f"  SKIPPED    {path.name}  (unrecognised suffix '_{sfx}')")

    for base, parts in mask_groups.items():
        mask = build_mask(parts, map_size)
        if mask is None:
            continue
        dest = out_dir / f"{base}_Mask.png"
        mask.save(dest)
        print(f"  packed     {base}_Mask.png  from {sorted(parts)}")
        written += 1

    if unused:
        print()
        print("  NOT WRITTEN - two-map standard, RetroLit samples no mask.")
        print("  Pass --mask only if this asset is headed for URP/Lit:")
        for name in unused:
            print(f"    {name}")

    # An AO with no BaseColor to fold into is silently lost work - say so.
    orphans = sorted(set(ao_for) - consumed_ao) if not want_mask else []
    if orphans:
        print()
        print("  WARNING: AO present with no matching BaseColor, so it was dropped:")
        for name in orphans:
            print(f"    {ao_for[name].name}")

    print()
    print(f"{written} file(s) -> {out_dir}")
    return 0


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)

    r = sub.add_parser("run", help="process a folder")
    r.add_argument("folder", type=Path)
    r.add_argument("--levels", type=int, default=DEFAULT_LEVELS,
                   help=f"colour levels per channel (default {DEFAULT_LEVELS}; baseline measures 9-12)")
    r.add_argument("--dither", type=float, default=DEFAULT_DITHER,
                   help="Bayer dither strength 0..1 (default 1.0; 0 disables)")
    r.add_argument("--size", type=int, default=None,
                   help="BaseColor/Emission target, nearest-neighbour (default: leave alone)")
    r.add_argument("--map-size", type=int, default=None,
                   help="Normal/Mask target, Lanczos resample (default: leave alone). "
                        "Pipeline doc default is half of --size.")
    r.add_argument("--mask", action="store_true",
                   help="Also pack a Mask (R=metallic, G=occlusion, B=0, "
                        "A=smoothness). Off by default: RetroLit samples no mask "
                        "and AO is folded into the BaseColor instead. Only for "
                        "assets headed for URP/Lit.")

    args = ap.parse_args()
    if not args.folder.is_dir():
        print(f"Not a folder: {args.folder}")
        return 1

    print(f"levels={args.levels} dither={args.dither} "
          f"size={args.size or 'unchanged'} map_size={args.map_size or 'unchanged'} " 
          f"maps={'3 (mask)' if args.mask else '2 (AO into BaseColor)'}\n")
    return run(args.folder, args.levels, args.dither, args.size, args.map_size, args.mask)


if __name__ == "__main__":
    raise SystemExit(main())
