"""Move a PSX prefab pack (AST-296 PSX Hospital Church or AST-297 PSX Wooden Fences) from Playground into Mr. Moonlight.

Usage: python move_pack_to_moonlight_psx.py 296|297 dry|go <usage.txt>      (MOVE_DEST=<staging> to build outside Assets first)
usage.txt lines: material|maxDim m|prefabs using it|avg   (measured in Playground)

Both packs: prefabs = a root with one MeshRenderer, URP/Lit material with a BASE map only (no normal, no AO). Materials -> RetroLit, base through the
pixelation filter. Size per material from the size of the objects (>= 3 m 1024, >= 0.8 m 512, smaller 256; a material shared by 8+ prefabs never
below 512) and NEVER above the texture's true effective resolution (several PSX textures are an upscaled lower-res image).
296: meshes are sub-assets of Hospital.fbx (the FBX is copied, GUID kept).  297: baked mesh .asset files (copied, GUIDs kept); its materials were
extracted from the FBX into real .mat files in Playground first (2026-10-10).
"""
import os, re, sys, shutil, uuid, subprocess, tempfile, collections
import numpy as np
from PIL import Image

WHICH, MODE, USAGE = sys.argv[1], sys.argv[2], sys.argv[3]
NAME = {"296": "AST-296 (PSX Hospital Church)", "297": "AST-297 (PSX Wooden Fences)"}[WHICH]
PK = os.path.join(r"E:\playground\Playground\Assets\PLAYGROUND", NAME)
MR = r"E:\MrMoonlight"
FINAL = os.path.join(MR, "Assets", "_Project", "Art", "Buildings & Props", NAME)
DEST = os.environ.get("MOVE_DEST", FINAL)
CHURCH = os.path.join(MR, "Assets", "_Project", "Art", "Buildings & Props", "WoodenChurch")
rxg = re.compile(r"^guid: ([0-9a-f]{32})", re.M)
rxref = re.compile(r"guid: ([0-9a-f]{32})")


def walk(r, exts):
    for d, _, fs in os.walk(r):
        for f in fs:
            if f.lower().endswith(exts):
                yield os.path.join(d, f)


def meta_guid(p):
    m = rxg.search(open(p + ".meta", encoding="utf8", errors="ignore").read(400))
    return m.group(1) if m else None


usage = {}
for line in open(USAGE, encoding="utf8"):
    if "|" in line:
        n, mx, c, av = line.strip().split("|")
        usage[n] = (float(mx), int(c))


def tier(name):
    mx, c = usage.get(name, (1.0, 1))
    s = 1024 if mx >= 3.0 else (512 if mx >= 0.8 else 256)
    return max(s, 512) if c >= 8 else s


def effective(path):
    """largest power-of-two size the image really has (a nearest-upscaled image is smaller than its file)"""
    im = Image.open(path).convert("RGB")
    a = np.asarray(im).astype(int)
    n = min(im.size)
    best = n
    for f in (2, 4, 8, 16):
        if n // f < 16:
            break
        up = np.asarray(im.resize((im.size[0] // f, im.size[1] // f), Image.NEAREST).resize(im.size, Image.NEAREST)).astype(int)
        if np.abs(a - up).mean() < 1.0:
            best = n // f
    return best


prefabs = {p: meta_guid(p) for p in walk(os.path.join(PK, "Prefabs"), (".prefab",))}
byg = {}
for p in walk(PK, (".prefab", ".fbx", ".mat", ".png", ".asset")):
    g = meta_guid(p)
    if g:
        byg[g] = p
used = collections.defaultdict(set)
unresolved = set()
for p in prefabs:
    for g in set(rxref.findall(open(p, encoding="utf8", errors="ignore").read())):
        if g in byg:
            used[os.path.splitext(byg[g])[1].lower()].add(g)
        else:
            unresolved.add(g)
print(NAME, "| prefabs", len(prefabs), "| fbx", len(used[".fbx"]), "| baked meshes (.asset)", len(used[".asset"]), "| materials", len(used[".mat"]), "| unresolved", len(unresolved))

plan = collections.OrderedDict()
jobs = collections.OrderedDict()
for g in sorted(used[".mat"], key=lambda x: byg[x]):
    path = byg[g]
    name = os.path.splitext(os.path.basename(path))[0]
    t = open(path, encoding="utf8", errors="ignore").read()
    m = re.search(r"- _BaseMap:\n\s+m_Texture: \{fileID: \d+, guid: ([0-9a-f]{32})", t)
    base = byg.get(m.group(1)) if m else None
    col = re.search(r"- _BaseColor: \{r: ([-\d.e]+), g: ([-\d.e]+), b: ([-\d.e]+)", t)
    e = dict(name=name, path=path, guid=g, base=base, tint=tuple(max(0.08, float(c)) for c in col.groups()) if col else (1, 1, 1))
    plan[name] = e
    if base:
        eff = effective(base)
        e["size"] = min(tier(name), eff, 1 << (int(min(Image.open(base).size)).bit_length() - 1))
        e["eff"] = eff
        j = jobs.setdefault(base, dict(src=base, size=0, stem="T_" + re.sub(r"[^A-Za-z0-9_]", "", re.sub(r"^T_", "", os.path.splitext(os.path.basename(base))[0])), mats=[]))
        j["size"] = max(j["size"], e["size"])
        j["mats"].append(name)
        e["jkey"] = base
print("base textures", len(jobs), "| sizes", dict(collections.Counter(j["size"] for j in jobs.values())))
for n, e in plan.items():
    print("  %-26s base=%-30s size=%-5s effective=%-5s usage=%s" % (n, os.path.basename(e["base"] or "-"), jobs[e["jkey"]]["size"] if "jkey" in e else 0, e.get("eff"), usage.get(n)))

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
mine = set(prefabs.values()) | used[".fbx"] | used[".asset"] | used[".mat"]
coll = mine & mr
print("GUID collisions with Mr. Moonlight:", len(coll))
if coll or MODE == "dry":
    sys.exit(1 if coll else 0)

work = tempfile.mkdtemp(prefix="psx_tex_")
for k, j in jobs.items():
    d = os.path.join(work, "b%d" % j["size"])
    os.makedirs(d, exist_ok=True)
    Image.open(j["src"]).convert("RGB").save(os.path.join(d, j["stem"] + "_BaseColor.png"))
for sub in sorted(os.listdir(work)):
    r = subprocess.run([sys.executable, os.path.join(MR, "Tools", "pipeline", "texture_pass.py"), "run", os.path.join(work, sub), "--size", sub[1:]], capture_output=True, text=True)
    print("texture_pass", sub, "rc", r.returncode, len(os.listdir(os.path.join(work, sub, "_out"))) if os.path.isdir(os.path.join(work, sub, "_out")) else r.stderr[-300:])
    if r.returncode != 0:
        sys.exit(1)
tex_dir = os.path.join(DEST, "Textures")
os.makedirs(tex_dir, exist_ok=True)
tpl_base = open([os.path.join(CHURCH, "Textures", f) for f in os.listdir(os.path.join(CHURCH, "Textures")) if f.endswith("BaseColor.png.meta")][0], encoding="utf8").read()
tex_guid = {}
for sub in os.listdir(work):
    for fn in os.listdir(os.path.join(work, sub, "_out")):
        g = uuid.uuid4().hex
        shutil.copy2(os.path.join(work, sub, "_out", fn), os.path.join(tex_dir, fn))
        open(os.path.join(tex_dir, fn + ".meta"), "w", encoding="utf8", newline="\n").write(rxg.sub("guid: " + g, tpl_base, count=1))
        tex_guid[fn[:-4]] = g
print("textures placed", len(tex_guid))

tpl = open(os.path.join(CHURCH, "Materials", "M_WoodLogs0023.mat"), encoding="utf8").read()
mdir = os.path.join(DEST, "Materials")
os.makedirs(mdir, exist_ok=True)
for name, e in plan.items():
    t = tpl.replace("m_Name: M_WoodLogs0023", "m_Name: " + name)
    bg = tex_guid.get(jobs[e["jkey"]]["stem"] + "_BaseColor") if "jkey" in e else None
    for prop, guid in (("_BaseMap", bg), ("_NormalMap", None)):
        ref = "{fileID: 2800000, guid: %s, type: 3}" % guid if guid else "{fileID: 0}"
        t = re.sub(r"(- %s:\n\s+m_Texture: )\{[^}]*\}" % prop, lambda m: m.group(1) + ref, t, count=1)
    t = t.replace("    - _ResolutionLimit: 256", "    - _ResolutionLimit: 8192")
    t = t.replace("    - _BaseColor: {r: 0.588, g: 0.588, b: 0.588, a: 1}", "    - _BaseColor: {r: %s, g: %s, b: %s, a: 1}" % e["tint"])
    assert "guid: 9d78dfd702f8ca1449b3d47347019785" not in t and "guid: 694fd3f9ff308d94d9e6d569279b1a1c" not in t, "church texture leaked into " + name
    open(os.path.join(mdir, name + ".mat"), "w", encoding="utf8", newline="\n").write(t)
    shutil.copy2(e["path"] + ".meta", os.path.join(mdir, name + ".mat.meta"))
print("materials written", len(plan))

n = 0
for p in prefabs:
    dst = os.path.join(DEST, "Prefabs", os.path.relpath(p, os.path.join(PK, "Prefabs")))
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    shutil.copy2(p, dst)
    shutil.copy2(p + ".meta", dst + ".meta")
    n += 1
for g in sorted(used[".fbx"] | used[".asset"]):
    dst = os.path.join(DEST, "Meshes", os.path.basename(byg[g]))
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    shutil.copy2(byg[g], dst)
    shutil.copy2(byg[g] + ".meta", dst + ".meta")
    n += 1
print("prefabs + meshes copied:", n, "| texture work dir", work)
