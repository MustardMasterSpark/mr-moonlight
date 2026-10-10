"""Move AST-294 Barricades (Sketchfab rip: ONE FBX with 339 skinned meshes in a grid) into Mr. Moonlight as split, static prefabs.

Usage: python move_pack_to_moonlight_barricades.py dry|go <usage.txt>      (MOVE_DEST=<staging> to build outside Assets first)

The splitting itself was done in Unity (Playground) with SkinnedMeshRenderer.BakeMesh: each mesh baked to a static Mesh asset, grouped into
  * 124 PIECES   (the red area of the lineup: meshes that touch each other, e.g. one object split per material) and
  * 26 STRUCTURES (the blue area: islands of meshes separated by > 0.4 m),
each prefab pivoted at its bounds centre on the ground. Those live in Playground `AST-294 (Barricades)/_Built/{Meshes,Prefabs}`.
This script moves them across: 14 URP/Lit materials -> RetroLit (base + normal, AO multiplied in where a T_*_AO.png exists), BaseColor pixelated
(size per material from how large the objects using it are), vendor-built prefab/mesh/material GUIDs kept so the prefabs still resolve.
usage.txt lines: material|maxDim m|renderers|avgDim m.
"""
import os, re, sys, shutil, uuid, subprocess, tempfile, collections
import numpy as np
from PIL import Image

MODE, USAGE = sys.argv[1], sys.argv[2]
PK = r"E:\playground\Playground\Assets\PLAYGROUND\AST-294 (Barricades)"
BUILT = os.path.join(PK, "_Built")
MR = r"E:\MrMoonlight"
FINAL = os.path.join(MR, "Assets", "_Project", "Art", "Buildings & Props", "AST-294 (Barricades)")
DEST = os.environ.get("MOVE_DEST", FINAL)
CHURCH = os.path.join(MR, "Assets", "_Project", "Art", "Buildings & Props", "WoodenChurch")
rxg = re.compile(r"^guid: ([0-9a-f]{32})", re.M)
TIERS = (512, 1024, 2048)


def meta_guid(p):
    m = rxg.search(open(p + ".meta", encoding="utf8", errors="ignore").read(400))
    return m.group(1) if m else None


usage = {}
for line in open(USAGE, encoding="utf8"):
    if "|" in line:
        n, mx, c, av = line.strip().split("|")
        usage[n] = (float(mx), float(av))


def tier(name):
    mx, av = usage.get(name, (1.0, 1.0))
    if av >= 4.0 or mx >= 20.0:
        return 2048
    return 1024 if av >= 1.5 else 512


mats = {}
for f in sorted(os.listdir(os.path.join(PK, "Materials"))):
    if f.endswith(".mat"):
        mats[f[:-4]] = os.path.join(PK, "Materials", f)
byg = {}
for d, _, fs in os.walk(PK):
    for f in fs:
        if f.endswith(".meta") and not f.endswith(".mat.meta") or f.endswith(".png.meta"):
            p = os.path.join(d, f[:-5])
            g = meta_guid(p)
            if g:
                byg[g] = p
plan = collections.OrderedDict()
for name, path in mats.items():
    t = open(path, encoding="utf8", errors="ignore").read()
    tex = {k: byg[g] for k, g in re.findall(r"- (_BaseMap|_BumpMap):\n\s+m_Texture: \{fileID: \d+, guid: ([0-9a-f]{32})", t) if g in byg}
    e = dict(name=name, path=path, guid=meta_guid(path), base=tex.get("_BaseMap"), normal=tex.get("_BumpMap"), size=tier(name), clip=False, cull=2)
    e["ao"] = (e["base"].replace("_BaseColor", "_AO") if e["base"] else None)
    if e["ao"] and not os.path.exists(e["ao"]):
        e["ao"] = None
    if e["base"]:
        a = np.asarray(Image.open(e["base"]).convert("RGBA").getchannel("A"))
        # only the grid texture is a real cut-out (see-through mesh); the metals keep gloss data in alpha, ground has soft alpha
        if name == "M_Grid" and (a < 128).mean() > 0.02:
            e["clip"], e["cull"] = True, 0
        sz = min(Image.open(e["base"]).size)
        e["size"] = min(e["size"], 1 << (int(sz).bit_length() - 1))
    plan[name] = e
    print("  %-12s base=%-28s size=%-5s ao=%-5s clip=%-5s usage(max/avg)=%s" % (name, os.path.basename(e["base"] or "-"), e["size"], bool(e["ao"]), e["clip"], usage.get(name)))

built_files = [os.path.join(d, f) for d, _, fs in os.walk(BUILT) for f in fs if f.endswith((".prefab", ".asset"))]
print("built prefabs", sum(1 for f in built_files if f.endswith(".prefab")), "| mesh assets", sum(1 for f in built_files if f.endswith(".asset")), "| materials", len(plan))
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
mine = {meta_guid(f) for f in built_files} | {e["guid"] for e in plan.values()}
coll = mine & mr
print("GUID collisions with Mr. Moonlight:", len(coll))
if coll or MODE == "dry":
    sys.exit(1 if coll else 0)

work = tempfile.mkdtemp(prefix="barr_tex_")
alpha_stems = set()
for name, e in plan.items():
    if not e["base"]:
        continue
    stem = os.path.basename(e["base"])[:-len("_BaseColor.png")]
    e["stem"] = stem
    im = Image.open(e["base"]).convert("RGBA")
    d = os.path.join(work, "b%d" % e["size"])
    os.makedirs(d, exist_ok=True)
    if e["clip"]:
        alpha_stems.add(stem)
        im.save(os.path.join(d, stem + "_BaseColor.png"))
    else:
        im.convert("RGB").save(os.path.join(d, stem + "_BaseColor.png"))
    if e["ao"]:
        Image.open(e["ao"]).convert("RGB").getchannel("R").save(os.path.join(d, stem + "_AO.png"))
    if e["normal"]:
        nsz = max(256, e["size"] // 2)
        nsz = min(nsz, 1 << (int(min(Image.open(e["normal"]).size)).bit_length() - 1))
        dn = os.path.join(work, "n%d" % nsz)
        os.makedirs(dn, exist_ok=True)
        Image.open(e["normal"]).convert("RGB").save(os.path.join(dn, stem + "_Normal.png"))
for sub in sorted(os.listdir(work)):
    size = int(sub[1:])
    args = [sys.executable, os.path.join(MR, "Tools", "pipeline", "texture_pass.py"), "run", os.path.join(work, sub)]
    args += ["--size", str(size), "--map-size", "256"] if sub[0] == "b" else ["--size", "256", "--map-size", str(size)]
    r = subprocess.run(args, capture_output=True, text=True)
    print("texture_pass", sub, "rc", r.returncode, len(os.listdir(os.path.join(work, sub, "_out"))) if os.path.isdir(os.path.join(work, sub, "_out")) else r.stderr[-300:])
    if r.returncode != 0:
        sys.exit(1)
tex_dir = os.path.join(DEST, "Textures")
os.makedirs(tex_dir, exist_ok=True)
tpl_base = open([os.path.join(CHURCH, "Textures", f) for f in os.listdir(os.path.join(CHURCH, "Textures")) if f.endswith("BaseColor.png.meta")][0], encoding="utf8").read()
tpl_norm = open([os.path.join(CHURCH, "Textures", f) for f in os.listdir(os.path.join(CHURCH, "Textures")) if f.endswith("Normal.png.meta")][0], encoding="utf8").read()
tex_guid = {}
for sub in os.listdir(work):
    for fn in os.listdir(os.path.join(work, sub, "_out")):
        stem = fn[:-4]
        g = uuid.uuid4().hex
        t = rxg.sub("guid: " + g, tpl_norm if stem.endswith("_Normal") else tpl_base, count=1)
        if stem.endswith("_BaseColor") and stem[:-len("_BaseColor")] in alpha_stems:
            t = t.replace("alphaIsTransparency: 0", "alphaIsTransparency: 1")
        shutil.copy2(os.path.join(work, sub, "_out", fn), os.path.join(tex_dir, fn))
        open(os.path.join(tex_dir, fn + ".meta"), "w", encoding="utf8", newline="\n").write(t)
        tex_guid[stem] = g
print("textures placed", len(tex_guid))

tpl = open(os.path.join(CHURCH, "Materials", "M_WoodLogs0023.mat"), encoding="utf8").read()
mdir = os.path.join(DEST, "Materials")
os.makedirs(mdir, exist_ok=True)
for name, e in plan.items():
    t = tpl.replace("m_Name: M_WoodLogs0023", "m_Name: " + name)
    bg = tex_guid.get(e["stem"] + "_BaseColor") if e.get("stem") else None
    ng = tex_guid.get(e["stem"] + "_Normal") if e.get("stem") and e["normal"] else None
    for prop, guid in (("_BaseMap", bg), ("_NormalMap", ng)):
        ref = "{fileID: 2800000, guid: %s, type: 3}" % guid if guid else "{fileID: 0}"
        t = re.sub(r"(- %s:\n\s+m_Texture: )\{[^}]*\}" % prop, lambda m: m.group(1) + ref, t, count=1)
    t = t.replace("    - _ResolutionLimit: 256", "    - _ResolutionLimit: 8192")
    t = t.replace("    - _Cull: 2", "    - _Cull: %d" % e["cull"])
    if e["clip"]:
        t = t.replace("    - _AlphaClip: 0", "    - _AlphaClip: 1")
        t = t.replace("  m_ValidKeywords:\n", "  m_ValidKeywords:\n  - _ALPHATEST_ON\n")
    t = t.replace("    - _BaseColor: {r: 0.588, g: 0.588, b: 0.588, a: 1}", "    - _BaseColor: {r: 1, g: 1, b: 1, a: 1}")
    assert "guid: 9d78dfd702f8ca1449b3d47347019785" not in t and "guid: 694fd3f9ff308d94d9e6d569279b1a1c" not in t, "church texture leaked into " + name
    open(os.path.join(mdir, name + ".mat"), "w", encoding="utf8", newline="\n").write(t)
    shutil.copy2(e["path"] + ".meta", os.path.join(mdir, name + ".mat.meta"))
print("materials written", len(plan))

n = 0
for f in built_files:
    rel = os.path.relpath(f, BUILT)
    dst = os.path.join(DEST, rel)
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    shutil.copy2(f, dst)
    shutil.copy2(f + ".meta", dst + ".meta")
    n += 1
print("prefabs + meshes copied:", n, "| texture work dir", work)
