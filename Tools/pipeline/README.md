# Pipeline tools

Local automation for `Docs/3d-asset-pipeline.md`. **These run without Claude.**
Use them directly; only come back to me when something they can't do comes up.

## One-time setup

```
python -m pip install pillow numpy
```

## texture_pass.py — pixelation + mask packing

```
python Tools/pipeline/texture_pass.py run <folder>
```

Reads every image in `<folder>`, writes results to `<folder>/_out/`.
**Nothing is overwritten in place.**

### Two-map standard (changed 2026-08-27, MRM-72)

**`RetroLit.shader` samples only `_BaseMap` and `_NormalMap`.** There is no
`_MetallicGlossMap`, no `_OcclusionMap` and no emission map anywhere in Retro
Shaders Pro — so a packed Mask would be shipped, compressed, loaded into memory
and read by nothing.

**Default output is therefore two files**, and AO is multiplied *into* the
BaseColor, which is the only way RetroLit will ever show it (and what PSX-era
art actually did).

### Name your files with these suffixes

| Filename | What happens |
|---|---|
| `Rock_BaseColor.png` | AO multiplied in, then quantised + Bayer dithered |
| `Rock_Emission.png` | Quantised + dithered. **AO is not applied** — emission is self-lit |
| `Rock_Normal.png` | Copied through, resampled to `--map-size` |
| `Rock_AO.png` | Multiplied into the matching `_BaseColor` |
| `Rock_Rough.png` | **Unused** unless `--mask`. Reported, never mangled |
| `Rock_Metal.png` | **Unused** unless `--mask`. Reported, never mangled |
| `Rock_Mask.png` | **Unused** unless `--mask` |

Anything with an unrecognised suffix is skipped and reported, never mangled.
An AO with no matching BaseColor to fold into is reported as a warning rather
than silently dropped.

### `--mask` — the old three-map behaviour

Pass `--mask` to restore packing (R=metallic, G=occlusion, B=0, A=smoothness)
and leave AO out of the BaseColor. Only worth it for an asset headed for
`URP/Lit` rather than RetroLit. Missing bake inputs fall back to constants —
metallic 0, occlusion white, smoothness 0.1 — so a partial bake still produces
a valid Mask.

Under RetroLit, metallic and smoothness are the material's scalar `_Glossiness`,
set once per material rather than per texel.

### Options

| Flag | Default | Notes |
|---|---|---|
| `--levels N` | `10` | Colour levels per channel. Retro Realism measures **9–12**. Lower = harsher. |
| `--dither F` | `1.0` | Bayer strength, `0` disables. |
| `--size N` | unchanged | BaseColor/Emission target, nearest-neighbour. |
| `--map-size N` | unchanged | Normal/Mask target, Lanczos resample. Pipeline default is half of `--size` (512/256 props, 1024/512 terrain textures) — pass both, they don't share one value. |
| `--mask` | off | Also pack a Mask, and keep AO out of the BaseColor. For URP/Lit assets only. |

```bash
# harsher, chunkier, resized to 512
python Tools/pipeline/texture_pass.py run E:\Props\Environment\Rock --levels 6 --size 512

# no dither, keep source resolution
python Tools/pipeline/texture_pass.py run E:\Props\Environment\Rock --dither 0
```

The Bayer matrix is lifted from Retro Realism's own `Bayer4x4.tga`, so the
dither pattern matches the style baseline exactly.

## prepare_asset.py — one source asset -> one Prepared Props folder

```
python Tools/pipeline/prepare_asset.py <manifest.json>
```

Wraps `texture_pass.py` for MRM-70's asset-prep pass: copies a mesh + its raw
texture maps into `Prepared Props/<Pack>_<Asset>/Source/` for archival,
stages renamed copies, runs `texture_pass.py`, and drops the results into
`Prepared Props/<Pack>_<Asset>/` as `T_<Pack>_<Asset>_<MapType>.png`. See the
script's own docstring for the manifest JSON shape. Doesn't write
`analysis.md` — that's a per-asset judgment call (polycount verdict,
optimization, wind/instancing note), not something to template blindly.

## The wizard

These tools are the automated half of `Docs/3d-prop-pipeline-wizard.md` (MRM-72).
Fire the wizard with **`/prop`** to be walked through a whole asset; use these
directly when you already know exactly what you want.

## MoonlightTextureImporter.cs — Unity import settings, automatic

Lives at `Assets/_Project/Code/Editor/`. Nothing to run.

Any texture dropped under **`Assets/_Project/Art/`** whose filename ends in
`_BaseColor` / `_Normal` / `_Mask` / `_Emission` gets the pipeline doc's §5.1
settings applied on import — sRGB, filter mode, size, compression, mips,
WebGL DXT override. It logs which preset it applied.

**Max size is a ceiling per art category, not a forced size** (changed 2026-08-27,
MRM-72): 2048 under `Characters/` and `Enemies/`, 1024 under `Weapons/`, 512
elsewhere; support maps get half. Unity never upscales, so the resolution the
wizard asked for and `texture_pass.py` wrote is the resolution that survives — the
old flat 512 cap silently discarded that answer on anything larger.

**This exists mainly to guarantee `Filter Mode = Point` on BaseColor.** Unity
defaults to Bilinear, which blurs the quantised pixels back into mush and
silently undoes the entire pixelation pass. Normal and Mask stay Bilinear on
purpose — point-filtering a normal map produces faceted lighting.

Files outside `Art/`, or without a recognised suffix, import normally.

## Not built yet

**Headless Blender bake** (`blender --background --python bake.py`) — would
make the Lane C bake token-free too. Deliberately deferred until the first real
hi-poly/low-poly pair exists, so it can be written and tested against actual
geometry in one pass instead of debugged blind.
