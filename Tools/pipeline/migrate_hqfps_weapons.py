#!/usr/bin/env python3
"""
MRM-25 / MRM-9 — migrate HQ FPS weapon art from the Weapons project into Mr. Moonlight.

Carlos keeps a dedicated Unity project at E:\\playground\\weapon holding the whole
HQ FPS Weapons 2.0 asset. Mr. Moonlight already carries every *prefab* and every
*item definition* from it (they came across wholesale during the MRM-9 controller
swap); what was trimmed to keep the build small was the ART — meshes and 4K
textures — and the AUDIO.

This brings a named subset of that art across, applying the two-map pipeline pass
on the way:

    BaseColor : AO multiplied in, quantised + Bayer-dithered, resampled to 512 (nearest)
    Normal    : resampled to 256 (Lanczos), never quantised, never dithered
    AO/MET/MaskMap : dropped — RetroLit has no slot for them

GUIDs are preserved by copying each source .meta verbatim, so every wieldable
prefab, magazine and pickup already in Mr. Moonlight relinks with zero fixup.
Verified: the wieldable prefabs here reference the same FBX/material GUIDs the
Weapons project assigns.

Two things this does NOT copy verbatim:

  * The .png.meta gets filterMode/maxTextureSize patched to match the existing
    M1911/DBShotgun precedent (Point filter + 512 cap on BaseColor, 512 cap on
    Normal — normals stay Bilinear, per Carlos's standing rule that only
    diffuse gets pixelated).

  * The WORLD material (the one on the pickup/dropped mesh) is REBUILT from
    Mr. Moonlight's own RetroLit template rather than copied, because the
    vendor's world shader GUID does not exist in this project and a straight
    copy renders magenta. The FP_ (first-person) material IS copied verbatim —
    its Shader Graphs/LitFieldOfView shader does exist here and carries the
    viewmodel FOV warp that RetroLit has no equivalent for.

Usage:
    python migrate_hqfps_weapons.py plan     # report only, touches nothing
    python migrate_hqfps_weapons.py apply
"""

from __future__ import annotations

import re
import shutil
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from texture_pass import pixelate, multiply_ao  # noqa: E402

try:
    from PIL import Image
except ImportError:
    sys.exit("Needs Pillow and numpy:  python -m pip install pillow numpy")


SRC = Path("E:/playground/weapon/Assets/PolymindGames")
DST = Path("E:/MrMoonlight/Assets/ThirdParty/PolymindGames")

# Matches the existing M1911 / DBShotgun / Crossbow / BaseballBat pass exactly.
BASE_SIZE = 512
MAP_SIZE = 256
LEVELS = 10
DITHER = 1.0

# Mr. Moonlight's own already-converted RetroLit world material, used as the body
# template for every new world material. Its texture references get swapped out.
TEMPLATE_MAT = DST / "HQFPS/Art/Meshes/Wieldables/M1911/Materials/M1911.mat"
TEMPLATE_ALBEDO_GUID = "625a7173213a10949a78e32860b9a1a1"   # M1911.png
TEMPLATE_NORMAL_GUID = "8bb54556b5f193249af1a09e3f0206c0"   # M1911_NRM.png
# A dangling reference in the template - the texture it names does not exist in
# this project. RetroLit samples no emission map anyway, so new materials get 0.
TEMPLATE_EMISSION_GUID = "83f3343a607db824a9fdf24ac66b0cf8"

# Folders to bring across, relative to each project's PolymindGames root.
WIELDABLE_ART = "HQFPS/Art/Meshes/Wieldables"
FOLDERS = [
    f"{WIELDABLE_ART}/AKM",
    f"{WIELDABLE_ART}/CombatKnife",
    f"{WIELDABLE_ART}/FireAxe",
    f"{WIELDABLE_ART}/FragGrenade",
    f"{WIELDABLE_ART}/MolotovCocktail",
    f"{WIELDABLE_ART}/R870",
    f"{WIELDABLE_ART}/M1A",
    f"{WIELDABLE_ART}/Revolver",
    f"{WIELDABLE_ART}/HuntingRifle",
    f"{WIELDABLE_ART}/Syringe",
    # The 5x scope. Deleted during MRM-9's crossbow-descope, but Carlos asked for
    # the hunting rifle WITH its scope, so it has to come back.
    "FPSCore/Art/Models/Attachments/SniperScope1",
    # Molotov ignition flame.
    "HQFPS/Art/VFX/LighterFlame",
]

# Weapon audio. Deliberately omits F1, MP5 and Flashlight - not weapons we carry.
# Note there is NO FireAxe audio anywhere in the vendor package; it falls back to
# the shared melee containers.
AUDIO_FOLDERS = [
    "HQFPS/Audio/SFX/Wieldables/AKM",
    "HQFPS/Audio/SFX/Wieldables/CombatKnife",
    "HQFPS/Audio/SFX/Wieldables/HuntingRifle",
    "HQFPS/Audio/SFX/Wieldables/M1A",
    "HQFPS/Audio/SFX/Wieldables/R870",
    "HQFPS/Audio/SFX/Wieldables/Revolvers",
    "HQFPS/Audio/SFX/Wieldables/Syringe",
    "HQFPS/Audio/SFX/Wieldables/Throwables",
]

# Suffixes that are inputs to the pass but never ship as their own file.
DROP_SUFFIXES = ("_AO", "_MET", "_MaskMap")


def guid_of(meta: Path) -> str | None:
    if not meta.is_file():
        return None
    m = re.search(r"^guid: ([0-9a-f]{32})", meta.read_text(encoding="utf-8", errors="replace"), re.M)
    return m.group(1) if m else None


def patch_texture_meta(text: str, *, point_filter: bool) -> str:
    """Match the import settings the existing weapon textures already use.

    Point filtering on the BaseColor is what makes the 512 map read as pixel art
    rather than a blurry small texture. Normals are left on the vendor's Bilinear
    - Carlos's standing rule is that only diffuse gets pixelated.
    """
    text = re.sub(r"^(\s*)maxTextureSize: \d+", rf"\g<1>maxTextureSize: {BASE_SIZE}", text, flags=re.M)
    if point_filter:
        text = re.sub(r"^(\s*)filterMode: \d+", r"\g<1>filterMode: 0", text, flags=re.M)
    return text


def build_world_material(name: str, albedo_guid: str, normal_guid: str | None) -> str:
    """Rebuild a world material on RetroLit from Mr. Moonlight's own template."""
    body = TEMPLATE_MAT.read_text(encoding="utf-8")

    body = body.replace(TEMPLATE_ALBEDO_GUID, albedo_guid)
    if normal_guid:
        body = body.replace(TEMPLATE_NORMAL_GUID, normal_guid)
    else:
        body = re.sub(
            r"\{fileID: 2800000, guid: " + TEMPLATE_NORMAL_GUID + r", type: 3\}",
            "{fileID: 0}",
            body,
        )
    # RetroLit samples no emission map, and the template's reference is dangling.
    body = re.sub(
        r"\{fileID: 2800000, guid: " + TEMPLATE_EMISSION_GUID + r", type: 3\}",
        "{fileID: 0}",
        body,
    )
    body = re.sub(r"^(\s*)m_Name: M1911$", rf"\g<1>m_Name: {name}", body, flags=re.M)

    # Vertex snapping OFF. _SnapsPerUnit is a WORLD-space constant (64 points per
    # metre), so a ~0.3 m weapon gets ~19 snap positions across its whole length -
    # which reads as broken geometry, not retro charm. Documented in
    # Docs/3d-prop-pipeline-wizard.md 3.2 and re-confirmed by Carlos on MRM-9.
    # The shader branches on the KEYWORD, so both have to agree: Off is index 3.
    body = re.sub(r"^(\s*)- _SNAPMODE_\w+$", r"\g<1>- _SNAPMODE_OFF", body, flags=re.M)
    body = re.sub(r"^(\s*)- _SnapMode: \d+", r"\g<1>- _SnapMode: 3", body, flags=re.M)
    return body


def process_folder(rel: str, apply: bool, log: list[str]) -> None:
    src = SRC / rel
    dst = DST / rel
    if not src.is_dir():
        log.append(f"  MISSING SOURCE: {rel}")
        return

    log.append(f"\n[{rel}]")
    if apply:
        dst.mkdir(parents=True, exist_ok=True)
        # Unity needs a .meta for the folder itself, or it invents a new GUID.
        for meta in (src.parent / (src.name + ".meta"),):
            if meta.is_file():
                shutil.copy2(meta, dst.parent / meta.name)

    # --- meshes and any loose files at the folder root -----------------------
    for f in sorted(src.iterdir()):
        if f.is_dir() or f.suffix == ".meta":
            continue
        log.append(f"  mesh/asset  {f.name}")
        if apply:
            shutil.copy2(f, dst / f.name)
            meta = f.with_suffix(f.suffix + ".meta")
            if meta.is_file():
                shutil.copy2(meta, dst / meta.name)

    mats_src = src / "Materials"
    if not mats_src.is_dir():
        return
    mats_dst = dst / "Materials"
    if apply:
        mats_dst.mkdir(parents=True, exist_ok=True)
        folder_meta = src / "Materials.meta"
        if folder_meta.is_file():
            shutil.copy2(folder_meta, dst / "Materials.meta")

    # --- textures -----------------------------------------------------------
    pngs = sorted(p for p in mats_src.iterdir() if p.suffix.lower() == ".png")
    bases = [p for p in pngs if not any(p.stem.endswith(s) for s in DROP_SUFFIXES + ("_NRM",))]
    guids: dict[str, tuple[str | None, str | None]] = {}

    for base in bases:
        stem = base.stem
        ao = mats_src / f"{stem}_AO.png"
        nrm = mats_src / f"{stem}_NRM.png"

        note = ""
        if apply:
            with Image.open(base) as im:
                im.load()
                if ao.is_file():
                    im = multiply_ao(im, ao)
                    note = f" (AO {ao.name} multiplied in)"
                out = pixelate(im, LEVELS, DITHER, BASE_SIZE)
            out.save(mats_dst / base.name)

            meta = base.with_suffix(".png.meta")
            if meta.is_file():
                (mats_dst / meta.name).write_text(
                    patch_texture_meta(meta.read_text(encoding="utf-8"), point_filter=True),
                    encoding="utf-8",
                )
        log.append(f"  pixelated   {base.name} -> {BASE_SIZE}px{note}")

        if nrm.is_file():
            if apply:
                with Image.open(nrm) as im:
                    im.load()
                    if im.size != (MAP_SIZE, MAP_SIZE):
                        im = im.resize((MAP_SIZE, MAP_SIZE), Image.LANCZOS)
                    im.save(mats_dst / nrm.name)
                meta = nrm.with_suffix(".png.meta")
                if meta.is_file():
                    (mats_dst / meta.name).write_text(
                        patch_texture_meta(meta.read_text(encoding="utf-8"), point_filter=False),
                        encoding="utf-8",
                    )
            log.append(f"  resampled   {nrm.name} -> {MAP_SIZE}px (no quantise, no dither)")

        guids[stem] = (
            guid_of(base.with_suffix(".png.meta")),
            guid_of(nrm.with_suffix(".png.meta")) if nrm.is_file() else None,
        )

    for p in pngs:
        if any(p.stem.endswith(s) for s in DROP_SUFFIXES):
            log.append(f"  dropped     {p.name} (RetroLit has no slot for it)")

    # --- materials ----------------------------------------------------------
    for mat in sorted(p for p in mats_src.iterdir() if p.suffix == ".mat"):
        meta = mat.with_suffix(".mat.meta")
        if mat.stem.startswith("FP_"):
            # Shader Graphs/LitFieldOfView exists in this project - copy verbatim.
            log.append(f"  copied FP   {mat.name} (vendor viewmodel shader, kept)")
            if apply:
                shutil.copy2(mat, mats_dst / mat.name)
                if meta.is_file():
                    shutil.copy2(meta, mats_dst / meta.name)
            continue

        albedo, normal = guids.get(mat.stem, (None, None))
        if albedo is None:
            log.append(f"  SKIPPED     {mat.name} (no matching BaseColor texture found)")
            continue
        log.append(f"  rebuilt     {mat.name} on RetroLit, snapping OFF")
        if apply:
            (mats_dst / mat.name).write_text(
                build_world_material(mat.stem, albedo, normal), encoding="utf-8"
            )
            if meta.is_file():
                shutil.copy2(meta, mats_dst / meta.name)


def process_audio(rel: str, apply: bool, log: list[str]) -> None:
    src = SRC / rel
    dst = DST / rel
    if not src.is_dir():
        log.append(f"  MISSING SOURCE: {rel}")
        return

    wavs = sorted(p for p in src.iterdir() if p.suffix.lower() == ".wav")
    log.append(f"\n[{rel}]  {len(wavs)} clip(s)")
    if apply:
        dst.mkdir(parents=True, exist_ok=True)
        folder_meta = src.parent / (src.name + ".meta")
        if folder_meta.is_file():
            shutil.copy2(folder_meta, dst.parent / folder_meta.name)
    for w in wavs:
        log.append(f"  {w.name}")
        if apply:
            shutil.copy2(w, dst / w.name)
            meta = w.with_suffix(".wav.meta")
            if meta.is_file():
                shutil.copy2(meta, dst / meta.name)


def main() -> int:
    if len(sys.argv) < 2 or sys.argv[1] not in ("plan", "apply"):
        print(__doc__)
        return 1
    apply = sys.argv[1] == "apply"

    if not SRC.is_dir():
        return print(f"Weapons project not found: {SRC}") or 1
    if not TEMPLATE_MAT.is_file():
        return print(f"RetroLit template material not found: {TEMPLATE_MAT}") or 1

    log: list[str] = []
    for rel in FOLDERS:
        process_folder(rel, apply, log)
    for rel in AUDIO_FOLDERS:
        process_audio(rel, apply, log)

    print("\n".join(log))
    print(f"\n{'APPLIED' if apply else 'PLAN ONLY — nothing written'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
