#!/usr/bin/env python3
"""
Mr. Moonlight — asset prep orchestrator for MRM-70.

Wraps texture_pass.py so a whole source asset (mesh + however many raw
texture maps) goes from wherever it lives on disk to a finished
E:\\Props\\Environment\\Prepared Props\\<Asset>\\ folder in one call, per
Docs/3d-asset-pipeline.md's naming convention.

    python prepare_asset.py <manifest.json>

Manifest shape:

    {
      "pack": "RF",
      "asset": "Tree1",
      "dest": "E:/Props/Environment/Prepared Props/RF_Tree1",
      "mesh": "E:/Props/Environment/Raw/LonelyForest/Mesh/Tree1.fbx",
      "textures": {
        "BaseColor": "E:/.../Trees.tga",
        "AO": "E:/.../Trees_AO.png"
      },
      "texture_pass": {"size": 512, "map_size": 256, "levels": 10, "dither": 1.0}
    }

`size` targets BaseColor/Emission, `map_size` targets Normal/Mask — the
pipeline doc's default ratio is map_size = size / 2 (512/256 for props,
1024/512 for terrain textures).

`textures` keys are the map type exactly as texture_pass.py expects in a
suffix (BaseColor, Normal, AO, Rough, Metal, Smooth, Mask, Emission) —
case doesn't matter. Missing keys just mean that input is absent; the mask
packer already falls back to constants for those.

Optional `"passthrough": ["BaseColor"]` — map types listed here skip
texture_pass.py entirely and get copied straight through at native
resolution. This is for sources that are already in the target pixel-art
style (Retro Realism's own BaseColor per Docs/3d-asset-pipeline.md section 0)
where re-quantizing would double-process an image that's already correct.

What it does, per asset:
  1. Copies every source file (mesh + all textures) untouched into
     <dest>/Source/, original filenames kept, for archival.
  2. Stages renamed copies of the textures into a temp folder using the
     <asset>_<MapType> convention texture_pass.py reads.
  3. Invokes texture_pass.py on the staging folder.
  4. Moves its _out/ results into <dest>/ as T_<pack>_<asset>_<MapType>.png,
     matching Docs/unity-conventions.md's T_ prefix + the pipeline
     importer's required suffix.
  5. Copies the mesh into <dest>/ as <pack>_<asset>.fbx, if one was given.

Does not write analysis.md — that needs a human/Claude judgment call per
asset (polycount verdict, wind/instancing note), not something to template
blindly.
"""

from __future__ import annotations

import json
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

THIS_DIR = Path(__file__).resolve().parent
TEXTURE_PASS = THIS_DIR / "texture_pass.py"


def _place_mesh(mesh: str, source_dir: Path, dest: Path, pack: str, asset: str) -> None:
    mesh_path = Path(mesh)
    if not mesh_path.is_file():
        print(f"  MISSING    mesh: {mesh_path}")
        return
    shutil.copy2(mesh_path, source_dir / mesh_path.name)
    dest_mesh = dest / f"{pack}_{asset}{mesh_path.suffix.lower()}"
    shutil.copy2(mesh_path, dest_mesh)
    print(f"  placed     {dest_mesh.name}")


def main() -> int:
    if len(sys.argv) != 2:
        print(f"Usage: {sys.argv[0]} <manifest.json>")
        return 1

    manifest_path = Path(sys.argv[1])
    manifest = json.loads(manifest_path.read_text())

    pack = manifest["pack"]
    asset = manifest["asset"]
    dest = Path(manifest["dest"])
    mesh = manifest.get("mesh")
    textures: dict[str, str] = manifest.get("textures", {})
    tp_opts = manifest.get("texture_pass", {})
    passthrough = {m.lower() for m in manifest.get("passthrough", [])}

    source_dir = dest / "Source"
    dest.mkdir(parents=True, exist_ok=True)
    source_dir.mkdir(exist_ok=True)

    print(f"=== {pack}_{asset} -> {dest} ===")

    with tempfile.TemporaryDirectory(prefix=f"mrm70_{pack}_{asset}_") as tmp:
        stage = Path(tmp)

        staged_any = False
        for map_type, src in textures.items():
            src = Path(src)
            if not src.is_file():
                print(f"  MISSING    {map_type}: {src}")
                continue
            shutil.copy2(src, source_dir / src.name)

            if map_type.lower() in passthrough:
                dest_name = f"T_{pack}_{asset}_{map_type}{src.suffix.lower()}"
                shutil.copy2(src, dest / dest_name)
                print(f"  passthrough {src.name} -> {dest_name} (native res, no texture_pass)")
                continue

            staged = stage / f"{asset}_{map_type}{src.suffix.lower()}"
            shutil.copy2(src, staged)
            staged_any = True
            print(f"  staged     {src.name} -> {staged.name}")

        if not staged_any:
            print("\n(nothing needed texture_pass.py — all textures were passthrough or absent)")
            if mesh:
                _place_mesh(mesh, source_dir, dest, pack, asset)
            print(f"\nDone. Write {dest / 'analysis.md'} by hand.")
            return 0

        cmd = [sys.executable, str(TEXTURE_PASS), "run", str(stage)]
        if tp_opts.get("size"):
            cmd += ["--size", str(tp_opts["size"])]
        if tp_opts.get("map_size"):
            cmd += ["--map-size", str(tp_opts["map_size"])]
        if "levels" in tp_opts:
            cmd += ["--levels", str(tp_opts["levels"])]
        if "dither" in tp_opts:
            cmd += ["--dither", str(tp_opts["dither"])]

        print(f"\n$ {' '.join(cmd)}")
        result = subprocess.run(cmd, capture_output=True, text=True)
        print(result.stdout)
        if result.returncode != 0:
            print(result.stderr)
            return result.returncode

        out_dir = stage / "_out"
        for f in sorted(out_dir.iterdir()):
            map_type = f.stem.rsplit("_", 1)[-1]
            dest_name = f"T_{pack}_{asset}_{map_type}.png"
            shutil.copy2(f, dest / dest_name)
            print(f"  placed     {dest_name}")

    if mesh:
        _place_mesh(mesh, source_dir, dest, pack, asset)

    print(f"\nDone. Write {dest / 'analysis.md'} by hand.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
