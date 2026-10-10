"""Move AST-074 Graveyard Tombstones from Playground into Mr. Moonlight (prefabs + everything they need).

Usage: python move_pack_to_moonlight_graveyard_tombstones.py dry|go

Adapted from move_pack_to_moonlight_witch_village.py. Keeps vendor GUIDs for prefabs / FBX / materials so the
vendor prefabs (including the 9 nested ones) keep resolving. Materials are rebuilt as RetroLit (BaseColor + Normal
only, vendor smoothness alpha dropped). BaseColor goes through the pixelation filter (texture_pass.py).
"""
import json, os, re, sys, shutil, uuid, subprocess, tempfile
from PIL import Image

MODE = sys.argv[1]  # dry | go
PG = r"E:\playground\Playground"
PACK = os.path.join(PG, "Assets", "PLAYGROUND", "AST-074 (Graveyard Tombstones)", "Lyrebird-Studio", "Graveyard Tombstones")
ART = os.path.join(PACK, "Art")
MR = r"E:\MrMoonlight"
DEST = os.path.join(MR, "Assets", "_Project", "Art", "Buildings & Props", "AST-074 (Graveyard Tombstones)")
CHURCH = os.path.join(MR, "Assets", "_Project", "Art", "Buildings & Props", "WoodenChurch")
BASE_SIZE, MAP_SIZE = "512", "256"
rxg = re.compile(r"^guid: ([0-9a-f]{32})", re.M)
rxref = re.compile(r"guid: ([0-9a-f]{32})")


def meta_guid(path):
    m = rxg.search(open(path + ".meta", encoding="utf8", errors="ignore").read(400))
    return m.group(1) if m else None


def walk(root, exts):
    for r, ds, fs in os.walk(root):
        for f in fs:
            if f.lower().endswith(exts):
                yield os.path.join(r, f)


prefabs = {p: meta_guid(p) for p in walk(os.path.join(PACK, "Prefabs"), (".prefab",))}
fbx = {p: meta_guid(p) for p in walk(os.path.join(ART, "Meshes"), (".fbx",))}
mats = {p: meta_guid(p) for p in walk(os.path.join(ART, "Materials"), (".mat",))}
tex_by_guid = {meta_guid(p): p for p in walk(os.path.join(ART, "Textures"), (".png", ".tif"))}
print("prefabs", len(prefabs), "fbx", len(fbx), "materials", len(mats))

# every guid a prefab references must be inside the pack
all_guids = set(prefabs.values()) | set(fbx.values()) | set(mats.values())
unresolved = set()
for p in prefabs:
    for g in set(rxref.findall(open(p, encoding="utf8", errors="ignore").read())):
        if g not in all_guids:
            unresolved.add(g)
print("prefab references outside the pack:", unresolved or "none")

# material plan: name -> base/normal source
plan = {}
for mp in sorted(mats):
    name = os.path.splitext(os.path.basename(mp))[0]
    t = open(mp, encoding="utf8").read()

    def tex(key):
        m = re.search(r"- %s:\n\s+m_Texture: \{fileID: \d+, guid: ([0-9a-f]{32})" % key, t)
        return tex_by_guid.get(m.group(1)) if m else None
    plan[name] = {"path": mp, "guid": mats[mp], "base": tex("_BaseMap") or tex("_MainTex"), "normal": tex("_BumpMap"), "stem": "T_" + name[2:]}
for n, v in plan.items():
    print("  ", n, "->", v["stem"], "| base", os.path.basename(v["base"] or "NONE"), "| normal", os.path.basename(v["normal"] or "NONE"))
    assert v["base"], "no base texture for " + n

mr_guids = set()
for r, ds, fs in os.walk(os.path.join(MR, "Assets")):
    for f in fs:
        if f.endswith(".meta"):
            try:
                m = rxg.search(open(os.path.join(r, f), encoding="utf8", errors="ignore").read(300))
            except OSError:
                continue
            if m:
                mr_guids.add(m.group(1))
coll = all_guids & mr_guids
print("GUID collisions with Mr. Moonlight:", len(coll))
if coll or unresolved or MODE == "dry":
    print("DRY RUN / STOP - nothing written")
    sys.exit(1 if (coll or unresolved) else 0)

# ---------------------------------------------------------------- texture pass
work = tempfile.mkdtemp(prefix="tomb_tex_")
for n, v in plan.items():
    Image.open(v["base"]).convert("RGB").save(os.path.join(work, v["stem"] + "_BaseColor.png"))
    if v["normal"]:
        Image.open(v["normal"]).convert("RGB").save(os.path.join(work, v["stem"] + "_Normal.png"))
r = subprocess.run([sys.executable, os.path.join(MR, "Tools", "pipeline", "texture_pass.py"), "run", work, "--size", BASE_SIZE, "--map-size", MAP_SIZE], capture_output=True, text=True)
print("\n".join(r.stdout.strip().splitlines()[-4:]))
if r.returncode != 0:
    print(r.stderr)
    sys.exit(1)
outdir = os.path.join(work, "_out")
outs = sorted(os.listdir(outdir))
print("texture_pass outputs:", len(outs), "(work dir kept:", work + ")")

os.makedirs(os.path.join(DEST, "Textures"), exist_ok=True)
tpl_base = open([os.path.join(CHURCH, "Textures", f) for f in os.listdir(os.path.join(CHURCH, "Textures")) if f.endswith("BaseColor.png.meta")][0], encoding="utf8").read()
tpl_norm = open([os.path.join(CHURCH, "Textures", f) for f in os.listdir(os.path.join(CHURCH, "Textures")) if f.endswith("Normal.png.meta")][0], encoding="utf8").read()
tex_guid = {}
for fn in outs:
    stem = fn[:-4]
    g = uuid.uuid4().hex
    shutil.copy2(os.path.join(outdir, fn), os.path.join(DEST, "Textures", fn))
    open(os.path.join(DEST, "Textures", fn + ".meta"), "w", encoding="utf8", newline="\n").write(
        rxg.sub("guid: " + g, tpl_norm if stem.endswith("_Normal") else tpl_base, count=1))
    tex_guid[stem] = g

# ---------------------------------------------------------------- RetroLit materials (church template, vendor GUID kept)
tpl_mat = open(os.path.join(CHURCH, "Materials", "M_WoodLogs0023.mat"), encoding="utf8").read()
os.makedirs(os.path.join(DEST, "Materials"), exist_ok=True)
for name, v in plan.items():
    t = tpl_mat.replace("m_Name: M_WoodLogs0023", "m_Name: " + name)
    t = re.sub(r"(- _BaseMap:\n\s+m_Texture: \{fileID: 2800000, guid: )[0-9a-f]{32}", r"\g<1>" + tex_guid[v["stem"] + "_BaseColor"], t)
    if v["normal"]:
        t = re.sub(r"(- _NormalMap:\n\s+m_Texture: \{fileID: 2800000, guid: )[0-9a-f]{32}", r"\g<1>" + tex_guid[v["stem"] + "_Normal"], t)
    else:
        t = re.sub(r"(- _NormalMap:\n\s+m_Texture: )\{fileID: 2800000, guid: [0-9a-f]{32}, type: 3\}", r"\g<1>{fileID: 0}", t)
    t = t.replace("    - _ResolutionLimit: 256", "    - _ResolutionLimit: 8192")
    t = t.replace("    - _BaseColor: {r: 0.588, g: 0.588, b: 0.588, a: 1}", "    - _BaseColor: {r: 1, g: 1, b: 1, a: 1}")
    assert "guid: 9d78dfd702f8ca1449b3d47347019785" not in t and "guid: 694fd3f9ff308d94d9e6d569279b1a1c" not in t, "church texture guid leaked into " + name
    open(os.path.join(DEST, "Materials", name + ".mat"), "w", encoding="utf8", newline="\n").write(t)
    shutil.copy2(v["path"] + ".meta", os.path.join(DEST, "Materials", name + ".mat.meta"))
print("materials written", len(plan))

# ---------------------------------------------------------------- meshes + prefabs (vendor GUIDs kept)
n = 0
for p in fbx:
    dst = os.path.join(DEST, "Meshes", os.path.relpath(p, os.path.join(ART, "Meshes")))
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    shutil.copy2(p, dst)
    shutil.copy2(p + ".meta", dst + ".meta")
    n += 1
for p in prefabs:
    dst = os.path.join(DEST, "Prefabs", os.path.relpath(p, os.path.join(PACK, "Prefabs")))
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    shutil.copy2(p, dst)
    shutil.copy2(p + ".meta", dst + ".meta")
    n += 1
print("copied fbx + prefabs:", n)
