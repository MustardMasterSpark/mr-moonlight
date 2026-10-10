"""Move the AST-295 Medieval Furniture Props prefabs (URP re-import, reorganised 2026-10-10) from Playground into Mr. Moonlight.

Usage: python move_pack_to_moonlight_medieval_furniture.py dry|go <usage.txt>      (MOVE_DEST=<staging> to build outside Assets first)
usage.txt lines: material|maxPrefabDim m|prefabs using it|avgPrefabDim m   (measured in Playground, LOD0 renderers only)

* every prefab in `Prefabs/` (89, incl. the 4 PS_* particle prefabs) + the FBX and materials they use; vendor GUIDs kept so the nested
  PS_ instances inside the candle prefabs still resolve;
* the two vendor Shader Graphs (S_BasicTextured, S_Blend) -> RetroLit: base + normal, AO = green channel of the packed RAM map
  (R roughness, G AO, B metal) multiplied into the base, tint + tiling; the UV2 dirt overlay and emission are dropped;
* the VFX materials (M_CandleFlame, M_Smoke, M_Emitter) and their textures are copied AS THEY ARE (Carlos: "move those as they are");
* BaseColor goes through the pixelation filter, size per material from the size of the prefabs that use it (tier() below).
NOT moved: `Meshes/Materials` (51 unused HDRP leftovers), `Shaders/`, `Scenes/`.
"""
import os, re, sys, shutil, uuid, subprocess, tempfile, collections
import numpy as np
from PIL import Image

MODE, USAGE = sys.argv[1], sys.argv[2]
PK = r"E:\playground\Playground\Assets\PLAYGROUND\AST-295 (Medieval Furniture)"
MR = r"E:\MrMoonlight"
FINAL = os.path.join(MR, "Assets", "_Project", "Art", "Buildings & Props", "AST-295 (Medieval Furniture)")
DEST = os.environ.get("MOVE_DEST", FINAL)
CHURCH = os.path.join(MR, "Assets", "_Project", "Art", "Buildings & Props", "WoodenChurch")
rxg = re.compile(r"^guid: ([0-9a-f]{32})", re.M)
rxref = re.compile(r"guid: ([0-9a-f]{32})")
AS_IS = {"M_CandleFlame", "M_Smoke", "M_Emitter"}      # particle materials: copied untouched
TINY_ATLAS_FLOOR = 512                                    # materials shared by many prefabs are atlases: never below this


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
        usage[n] = (float(mx), int(c), float(av))


def tier(name):
    mx, c, av = usage.get(name, (1.0, 1, 1.0))
    s = 1024 if mx >= 3.0 else (512 if mx >= 0.8 else 256)
    if c >= 8:
        s = max(s, TINY_ATLAS_FLOOR)
    return s


prefabs = {p: meta_guid(p) for p in walk(os.path.join(PK, "Prefabs"), (".prefab",))}
byg = {}
for p in walk(PK, (".prefab", ".fbx", ".mat", ".png", ".tga", ".exr")):
    if "Meshes" + os.sep + "Materials" in p:
        continue
    g = meta_guid(p)
    if g:
        byg[g] = p
used_fbx, used_mat, unresolved = set(), set(), set()
for p in prefabs:
    for g in set(rxref.findall(open(p, encoding="utf8", errors="ignore").read())):
        if g not in byg:
            unresolved.add(g)
        elif byg[g].lower().endswith(".fbx"):
            used_fbx.add(g)
        elif byg[g].lower().endswith(".mat"):
            used_mat.add(g)
for g in list(used_fbx):
    for r in set(rxref.findall(open(byg[g] + ".meta", encoding="utf8", errors="ignore").read())):
        if r in byg and byg[r].lower().endswith(".mat"):
            used_mat.add(r)
print("prefabs", len(prefabs), "| fbx", len(used_fbx), "| materials used", len(used_mat), "| unresolved guids (not copied)", len(unresolved))


def parse(path):
    t = open(path, encoding="utf8", errors="ignore").read()
    tex = {}
    for m in re.finditer(r"    - (\S+):\n\s+m_Texture: \{fileID: \d+, guid: ([0-9a-f]{32})", t):
        if m.group(2) in byg:
            tex[m.group(1)] = byg[m.group(2)]
    col = {m.group(1): tuple(float(m.group(i)) for i in (2, 3, 4, 5)) for m in re.finditer(r"    - (\S+): \{r: ([-\d.e]+), g: ([-\d.e]+), b: ([-\d.e]+), a: ([-\d.e]+)\}", t)}
    flo = {m.group(1): float(m.group(2)) for m in re.finditer(r"    - (\S+): ([-\d.e]+)\n", t)}
    sh = re.search(r"m_Shader: \{fileID: -?\d+, guid: ([0-9a-f]{32})", t).group(1)
    return dict(tex=tex, col=col, flo=flo, shader=sh)


plan = collections.OrderedDict()
base_jobs, norm_jobs = collections.OrderedDict(), collections.OrderedDict()
asis_files = set()
for g in sorted(used_mat, key=lambda x: byg[x]):
    path = byg[g]
    name = os.path.splitext(os.path.basename(path))[0]
    m = parse(path)
    e = dict(name=name, path=path, guid=g, size=tier(name), kind="lit", clip=False, cull=2)
    if name in AS_IS:
        e["kind"] = "asis"
        for tp in m["tex"].values():
            asis_files.add(tp)
        plan[name] = e
        continue
    e["base"] = m["tex"].get("_BaseMap")
    e["normal"] = m["tex"].get("_NormalMap")
    ram = m["tex"].get("_RAM")
    e["ao"] = ram if ram and os.path.basename(ram) != "T_Mask.png" else None          # T_Mask is the neutral grey placeholder
    t = m["col"].get("_Tint") or m["col"].get("_02_BaseDiffuse_Tint") or (1, 1, 1, 0)
    a = max(0.0, min(1.0, t[3]))
    e["tint"] = tuple(max(0.08, 1 + (t[i] - 1) * a) for i in range(3))
    til = m["col"].get("_Tiling") or m["col"].get("_BaseTiling")
    e["tile"] = (til[0] or 1, til[1] or 1) if til else (1, 1)
    if m["flo"].get("_UseOpacity", 0) == 1 and m["tex"].get("_OpacityMap"):
        e["clip"], e["cull"] = True, 0
        e["opacity"] = m["tex"]["_OpacityMap"]
    e["emissive"] = bool(m["tex"].get("_EmissiveMap") and m["flo"].get("_UseEmissive", 0) == 1)
    plan[name] = e
    if e["base"]:
        sz = min(Image.open(e["base"]).size)
        e["size"] = min(e["size"], 1 << (int(sz).bit_length() - 1))
        k = (e["base"], e["ao"], e.get("opacity"))
        j = base_jobs.setdefault(k, dict(src=e["base"], ao=e["ao"], opacity=e.get("opacity"), clip=e["clip"], size=0, stem=None, mats=[]))
        j["size"] = max(j["size"], e["size"])
        j["mats"].append(name)
        e["bkey"] = k
    if e["normal"]:
        j = norm_jobs.setdefault(e["normal"], dict(src=e["normal"], size=0, stem=None, mats=[]))
        j["size"] = max(j["size"], max(256, e["size"] // 2))
        e["nkey"] = e["normal"]


def stem_of(p, drop):
    s = os.path.splitext(os.path.basename(p))[0]
    s = re.sub(r"_(BC|N|D|BaseColor|Normal|RAM)$", "", s)
    s = re.sub(r"[^A-Za-z0-9_]", "", s)
    return "T_" + (s[2:] if s.startswith("T_") else s)


used = set()
for d in (base_jobs, norm_jobs):
    for k, j in d.items():
        s0 = stem_of(j["src"], True)
        s, i = s0, 2
        while s in used:
            s = "%s_%d" % (s0, i)
            i += 1
        used.add(s)
        j["stem"] = s
for k, j in norm_jobs.items():
    sz = min(Image.open(j["src"]).size)
    j["size"] = min(j["size"] or 256, 1 << (int(sz).bit_length() - 1))
print("base textures", len(base_jobs), "normals", len(norm_jobs), "| as-is files", len(asis_files), "| base sizes", dict(collections.Counter(j["size"] for j in base_jobs.values())),
      "| normal sizes", dict(collections.Counter(j["size"] for j in norm_jobs.values())))
for n, e in plan.items():
    if e["kind"] == "asis":
        print("  %-26s AS-IS (particle material)" % n)
        continue
    j = base_jobs.get(e.get("bkey"))
    print("  %-26s base=%-26s size=%-5s n=%-5s ao=%-5s clip=%-5s tint=%s tile=%s emissiveDropped=%s usage=%s" % (n, os.path.basename(e["base"] or "-"), j["size"] if j else 0, bool(e["normal"]), bool(e["ao"]), e["clip"], tuple(round(x, 2) for x in e["tint"]), e["tile"], e["emissive"], usage.get(n)))

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
asis_guids = {meta_guid(p) for p in asis_files}
coll = (set(prefabs.values()) | used_fbx | used_mat | asis_guids) & mr
print("GUID collisions with Mr. Moonlight:", len(coll))
if coll or MODE == "dry":
    sys.exit(1 if coll else 0)

# ------------------------------------------------------------------ textures
work = tempfile.mkdtemp(prefix="furn_tex_")
alpha_stems = set()
for k, j in base_jobs.items():
    im = Image.open(j["src"]).convert("RGBA")
    if j["opacity"]:
        om = Image.open(j["opacity"]).convert("RGBA")
        a = np.asarray(om.getchannel("A"))
        a = a if (a < 250).mean() > 0.01 else np.asarray(om.getchannel("R"))
        a = Image.fromarray(a)
        if a.size != im.size:
            a = a.resize(im.size, Image.LANCZOS)
        im.putalpha(a)
    d = os.path.join(work, "b%d" % j["size"])
    os.makedirs(d, exist_ok=True)
    if j["clip"]:
        alpha_stems.add(j["stem"])
        im.save(os.path.join(d, j["stem"] + "_BaseColor.png"))
    else:
        im.convert("RGB").save(os.path.join(d, j["stem"] + "_BaseColor.png"))
    if j["ao"]:
        ao = np.asarray(Image.open(j["ao"]).convert("RGB").getchannel("G"))
        Image.fromarray(ao).save(os.path.join(d, j["stem"] + "_AO.png"))
for k, j in norm_jobs.items():
    d = os.path.join(work, "n%d" % j["size"])
    os.makedirs(d, exist_ok=True)
    Image.open(j["src"]).convert("RGB").save(os.path.join(d, j["stem"] + "_Normal.png"))
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
for p in sorted(asis_files):                                  # particle textures untouched, GUIDs kept
    shutil.copy2(p, os.path.join(tex_dir, os.path.basename(p)))
    shutil.copy2(p + ".meta", os.path.join(tex_dir, os.path.basename(p) + ".meta"))
print("textures placed", len(tex_guid), "+ as-is", len(asis_files))

# ------------------------------------------------------------------ materials
tpl = open(os.path.join(CHURCH, "Materials", "M_WoodLogs0023.mat"), encoding="utf8").read()
mdir = os.path.join(DEST, "Materials")
os.makedirs(mdir, exist_ok=True)
for name, e in plan.items():
    if e["kind"] == "asis":
        shutil.copy2(e["path"], os.path.join(mdir, name + ".mat"))
        shutil.copy2(e["path"] + ".meta", os.path.join(mdir, name + ".mat.meta"))
        continue
    t = tpl.replace("m_Name: M_WoodLogs0023", "m_Name: " + name)
    bg = tex_guid.get(base_jobs[e["bkey"]]["stem"] + "_BaseColor") if "bkey" in e else None
    ng = tex_guid.get(norm_jobs[e["nkey"]]["stem"] + "_Normal") if "nkey" in e else None
    for prop, guid in (("_BaseMap", bg), ("_NormalMap", ng)):
        ref = "{fileID: 2800000, guid: %s, type: 3}" % guid if guid else "{fileID: 0}"
        t = re.sub(r"(- %s:\n\s+m_Texture: )\{[^}]*\}(\n\s+m_Scale: )\{[^}]*\}" % prop, lambda m: m.group(1) + ref + m.group(2) + "{x: %s, y: %s}" % e["tile"], t, count=1)
    t = t.replace("    - _ResolutionLimit: 256", "    - _ResolutionLimit: 8192")
    t = t.replace("    - _Cull: 2", "    - _Cull: %d" % e["cull"])
    if e["clip"]:
        t = t.replace("    - _AlphaClip: 0", "    - _AlphaClip: 1")
        t = t.replace("  m_ValidKeywords:\n", "  m_ValidKeywords:\n  - _ALPHATEST_ON\n")
    t = t.replace("    - _BaseColor: {r: 0.588, g: 0.588, b: 0.588, a: 1}", "    - _BaseColor: {r: %s, g: %s, b: %s, a: 1}" % e["tint"])
    assert "guid: 9d78dfd702f8ca1449b3d47347019785" not in t and "guid: 694fd3f9ff308d94d9e6d569279b1a1c" not in t, "church texture leaked into " + name
    open(os.path.join(mdir, name + ".mat"), "w", encoding="utf8", newline="\n").write(t)
    shutil.copy2(e["path"] + ".meta", os.path.join(mdir, name + ".mat.meta"))
print("materials written", len(plan), "(as-is:", sum(1 for e in plan.values() if e["kind"] == "asis"), ")")

# ------------------------------------------------------------------ FBX + prefabs
n = 0
for g in sorted(used_fbx):
    dst = os.path.join(DEST, "Meshes", os.path.relpath(byg[g], os.path.join(PK, "Meshes")))
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    shutil.copy2(byg[g], dst)
    shutil.copy2(byg[g] + ".meta", dst + ".meta")
    n += 1
for p in prefabs:
    dst = os.path.join(DEST, "Prefabs", os.path.relpath(p, os.path.join(PK, "Prefabs")))
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    shutil.copy2(p, dst)
    shutil.copy2(p + ".meta", dst + ".meta")
    n += 1
print("fbx + prefabs copied:", n, "| texture work dir", work)
