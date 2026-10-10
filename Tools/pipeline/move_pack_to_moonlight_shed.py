"""Move the AST-108 The Shed prefab (only that prefab) from Playground into Mr. Moonlight.

Usage: python move_pack_to_moonlight_shed.py dry|go

The vendor FBX find their materials BY NAME (materialSearch = Recursive-Up), so the layout keeps
Meshes/ and Materials/ as siblings and the rebuilt RetroLit materials keep the vendor file names + GUIDs.
Per-material BaseColor size is decided per material (SIZES below); normals are never pixelated.
"""
import os, re, sys, shutil, uuid, subprocess, tempfile
from PIL import Image

MODE = sys.argv[1]
SRC = r"E:\playground\Playground\Assets\PLAYGROUND\AST-108 (The Shed)\Blackant Master Studio\The Shed\The Shed Optimized"
MR = r"E:\MrMoonlight"
DEST = os.path.join(MR, "Assets", "_Project", "Art", "Buildings & Props", "AST-108 (The Shed)")
CHURCH = os.path.join(MR, "Assets", "_Project", "Art", "Buildings & Props", "WoodenChurch")
rxg = re.compile(r"^guid: ([0-9a-f]{32})", re.M)

# material file -> (stem, base src, normal src, base size, normal size, tint or None)
MATS = {
    "The Shed": ("T_Shed", "The Shed_Dif.tga", "The Shed_Nrm.tga", 1024, 512, None),
    "The SHed Tools_Dif": ("T_ShedTools", "The Shed Tools_Dif.tga", "The Shed Tools_Nrm.tga", 1024, 512, None),
    "The Shed Objects": ("T_ShedObjects", "The Shed Objects_Dif.tga", "The Shed Objects_Nrm.tga", 512, 256, None),
    "The Shed Ivy": (None, None, None, 0, 0, (0.13, 0.27, 0.09, 1)),       # vendor ships no ivy texture: flat colour
    "Decal_Debris01": (None, None, None, 0, 0, (0.791, 0.791, 0.791, 1)),  # vendor ships no decal texture: flat colour
}
MAT_PATH = {"Decal_Debris01": os.path.join("Meshes", "Materials", "Decal_Debris01.mat")}
FBX = ["The Shed.fbx", "The Shed Door.fbx", "The Shed Tools.fbx", "The Shed Objects.fbx", "The Shed Ivy.fbx", "The Shed Decals.fbx"]


def mp(name):
    return os.path.join(SRC, MAT_PATH.get(name, os.path.join("Materials", name + ".mat")))


def guid_of(path):
    return rxg.search(open(path + ".meta", encoding="utf8").read(400)).group(1)


files = [os.path.join(SRC, "Prefab", "The Shed.prefab"), os.path.join(SRC, "Meshes", "The Shed Door Ctrl.controller")]
files += [os.path.join(SRC, "Meshes", f) for f in FBX] + [mp(n) for n in MATS]
guids = {f: guid_of(f) for f in files}
mr = set()
for r, ds, fs in os.walk(os.path.join(MR, "Assets")):
    for f in fs:
        if f.endswith(".meta"):
            try:
                m = rxg.search(open(os.path.join(r, f), encoding="utf8", errors="ignore").read(300))
            except OSError:
                continue
            if m:
                mr.add(m.group(1))
coll = [f for f, g in guids.items() if g in mr]
print("files", len(files), "| GUID collisions:", coll or "none")
for n, v in MATS.items():
    print("  ", n, "->", v[0], "base", v[3], "normal", v[4])
if coll or MODE == "dry":
    sys.exit(1 if coll else 0)

work = tempfile.mkdtemp(prefix="shed_tex_")
for sizes in sorted({(v[3], v[4]) for v in MATS.values() if v[0]}):
    d = os.path.join(work, "%d_%d" % sizes)
    os.makedirs(d)
    for n, v in MATS.items():
        if v[0] and (v[3], v[4]) == sizes:
            Image.open(os.path.join(SRC, "Materials", v[1])).convert("RGB").save(os.path.join(d, v[0] + "_BaseColor.png"))
            Image.open(os.path.join(SRC, "Materials", v[2])).convert("RGB").save(os.path.join(d, v[0] + "_Normal.png"))
    r = subprocess.run([sys.executable, os.path.join(MR, "Tools", "pipeline", "texture_pass.py"), "run", d, "--size", str(sizes[0]), "--map-size", str(sizes[1])], capture_output=True, text=True)
    print("texture_pass", sizes, "rc", r.returncode, r.stdout.strip().splitlines()[-1] if r.stdout.strip() else r.stderr[-300:])
    if r.returncode != 0:
        sys.exit(1)

tex = os.path.join(DEST, "Textures")
os.makedirs(tex, exist_ok=True)
tpl_base = open([os.path.join(CHURCH, "Textures", f) for f in os.listdir(os.path.join(CHURCH, "Textures")) if f.endswith("BaseColor.png.meta")][0], encoding="utf8").read()
tpl_norm = open([os.path.join(CHURCH, "Textures", f) for f in os.listdir(os.path.join(CHURCH, "Textures")) if f.endswith("Normal.png.meta")][0], encoding="utf8").read()
tex_guid = {}
for sub in os.listdir(work):
    out = os.path.join(work, sub, "_out")
    if not os.path.isdir(out):
        continue
    for fn in os.listdir(out):
        g = uuid.uuid4().hex
        shutil.copy2(os.path.join(out, fn), os.path.join(tex, fn))
        open(os.path.join(tex, fn + ".meta"), "w", encoding="utf8", newline="\n").write(rxg.sub("guid: " + g, tpl_norm if fn.endswith("_Normal.png") else tpl_base, count=1))
        tex_guid[fn[:-4]] = g
print("textures placed", len(tex_guid))

tpl = open(os.path.join(CHURCH, "Materials", "M_WoodLogs0023.mat"), encoding="utf8").read()
for name, v in MATS.items():
    t = tpl.replace("m_Name: M_WoodLogs0023", "m_Name: " + name)
    if v[0]:
        t = re.sub(r"(- _BaseMap:\n\s+m_Texture: \{fileID: 2800000, guid: )[0-9a-f]{32}", r"\g<1>" + tex_guid[v[0] + "_BaseColor"], t)
        t = re.sub(r"(- _NormalMap:\n\s+m_Texture: \{fileID: 2800000, guid: )[0-9a-f]{32}", r"\g<1>" + tex_guid[v[0] + "_Normal"], t)
        c = (1, 1, 1, 1)
    else:
        t = re.sub(r"(- _BaseMap:\n\s+m_Texture: )\{fileID: 2800000, guid: [0-9a-f]{32}, type: 3\}", r"\g<1>{fileID: 0}", t)
        t = re.sub(r"(- _NormalMap:\n\s+m_Texture: )\{fileID: 2800000, guid: [0-9a-f]{32}, type: 3\}", r"\g<1>{fileID: 0}", t)
        c = v[5]
    t = t.replace("    - _ResolutionLimit: 256", "    - _ResolutionLimit: 8192")
    t = t.replace("    - _BaseColor: {r: 0.588, g: 0.588, b: 0.588, a: 1}", "    - _BaseColor: {r: %s, g: %s, b: %s, a: %s}" % c)
    if name == "The Shed Ivy":
        t = t.replace("    - _Cull: 2", "    - _Cull: 0")   # vendor ivy was double-sided
    assert "guid: 9d78dfd702f8ca1449b3d47347019785" not in t and "guid: 694fd3f9ff308d94d9e6d569279b1a1c" not in t
    dst = os.path.join(DEST, os.path.relpath(mp(name), SRC).replace("Meshes" + os.sep + "Materials", "Materials"))
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    open(dst, "w", encoding="utf8", newline="\n").write(t)
    shutil.copy2(mp(name) + ".meta", dst + ".meta")
print("materials written", len(MATS))

for f in files:
    if f.endswith(".mat"):
        continue
    rel = os.path.relpath(f, SRC).replace("Prefab" + os.sep, "Prefabs" + os.sep)
    dst = os.path.join(DEST, rel)
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    shutil.copy2(f, dst)
    shutil.copy2(f + ".meta", dst + ".meta")
print("prefab, controller and FBX copied; work dir", work)
