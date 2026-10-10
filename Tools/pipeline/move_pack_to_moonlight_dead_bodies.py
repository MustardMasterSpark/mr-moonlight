"""Move the AST-293 Dead Bodies `Prefabs/` folder (156 prefabs) from Playground into Mr. Moonlight.

Usage: python move_pack_to_moonlight_dead_bodies.py dry|go <usage.txt>
usage.txt lines: material|maxDim m|renderers|avgDim m   (measured in Playground from the prefabs)

* every prefab in `Skeleton_DeadBodies/Prefabs` + the FBX and materials they use, vendor GUIDs kept (the 26 nested prefabs keep resolving);
* vendor URP/Lit materials (converted from Standard) -> RetroLit: base + normal, AO (G channel of the occlusion map, the Unity convention)
  multiplied into the base; the transparent particle materials (blanket, mummy wraps, hair) become alpha-clip, double sided;
* BaseColor goes through the pixelation filter, size chosen per material from how large the objects using it are (SIZE_OVERRIDE below).
Adapted from move_pack_to_moonlight_swimming_pool.py. The Mega_Blood_Pack prefabs are NOT included.
"""
import os, re, sys, shutil, uuid, subprocess, tempfile, collections
import numpy as np
from PIL import Image

MODE, USAGE = sys.argv[1], sys.argv[2]
PACK = r"E:\playground\Playground\Assets\PLAYGROUND\AST-293 (Dead Bodies)\Phoenix3D\Skeleton_DeadBodies"
MR = r"E:\MrMoonlight"
FINAL = os.path.join(MR, "Assets", "_Project", "Art", "Buildings & Props", "AST-293 (Dead Bodies)")
DEST = os.environ.get("MOVE_DEST", FINAL)      # build into a staging folder first (MOVE_DEST), then move it into Assets after Carlos saves
CHURCH = os.path.join(MR, "Assets", "_Project", "Art", "Buildings & Props", "WoodenChurch")
rxg = re.compile(r"^guid: ([0-9a-f]{32})", re.M)
rxref = re.compile(r"guid: ([0-9a-f]{32})")
SIZE_OVERRIDE = {"Bones": 1024, "Burned_Bones": 1024,                      # one atlas over a whole 1.8 m skeleton
                 "Skull_Upper_Part": 512, "Skull_Lower_Part": 512, "Burned_Skull_Upper_Part": 512, "Burned_Skull_Lower_Part": 512}
TIERS = (256, 512, 1024)


def walk(r, exts):
    for d, _, fs in os.walk(r):
        for f in fs:
            if f.lower().endswith(exts):
                yield os.path.join(d, f)


def meta_guid(p):
    m = rxg.search(open(p + ".meta", encoding="utf8", errors="ignore").read(400))
    return m.group(1) if m else None


prefabs = {p: meta_guid(p) for p in walk(os.path.join(PACK, "Prefabs"), (".prefab",))}
byg = {}
for p in walk(PACK, (".prefab", ".fbx", ".mat", ".png", ".exr", ".hdr", ".tga")):
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
print("prefabs", len(prefabs), "| fbx", len(used_fbx), "| materials", len(used_mat), "| unresolved GUIDs", len(unresolved))

usage = {}
for line in open(USAGE, encoding="utf8"):
    if "|" in line:
        n, mx, c, av = line.strip().split("|")
        usage[n] = (float(mx), float(av))


def tier(name):
    if name in SIZE_OVERRIDE:
        return SIZE_OVERRIDE[name]
    mx, av = usage.get(name, (1.0, 1.0))
    i = 2 if av >= 1.5 else (1 if av >= 0.5 else 0)
    if mx >= 3.0:
        i = min(2, i + 1)
    if mx >= 1.5:
        i = 2      # a body-sized object is always seen close up
    return TIERS[i]


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


def clean(p):
    s = os.path.splitext(os.path.basename(p))[0]
    return re.sub(r"[^A-Za-z0-9_]", "", s)


plan = collections.OrderedDict()
base_jobs, norm_jobs = collections.OrderedDict(), collections.OrderedDict()
for g in sorted(used_mat, key=lambda x: byg[x]):
    path = byg[g]
    name = os.path.splitext(os.path.basename(path))[0]
    m = parse(path)
    e = dict(name=name, path=path, size=tier(name))
    e["base"] = m["tex"].get("_BaseMap") or m["tex"].get("_MainTex")
    if e["base"] and "Ambient_occlusion" in e["base"]:
        fix = e["base"].replace("Ambient_occlusion", "Base_Color")
        assert os.path.exists(fix), fix
        print("  FIX base map of", name, "pointed at an AO texture ->", os.path.basename(fix))
        e["base"] = fix
    e["normal"] = m["tex"].get("_BumpMap")
    e["ao"] = m["tex"].get("_OcclusionMap")
    bc = m["col"].get("_BaseColor") or m["col"].get("_Color") or (1, 1, 1, 1)
    e["tint"] = tuple(max(0.08, c) for c in bc[:3])
    particles = "Particles" in open(path, encoding="utf8", errors="ignore").read()[:3000] or m["flo"].get("_Surface", 0) == 1
    e["clip"] = m["flo"].get("_AlphaClip", 0) == 1 or particles
    e["cull"] = 0 if particles else int(m["flo"].get("_Cull", 2))
    if name == "Woman_Hair" and not e["base"]:
        e["tint"] = (0.10, 0.07, 0.05)      # the pack ships no hair texture at all
    plan[name] = e
    if e["base"]:
        k = (e["base"], e["ao"], e["clip"])
        j = base_jobs.setdefault(k, dict(src=e["base"], ao=e["ao"], clip=e["clip"], size=0, stem=clean(e["base"]), mats=[]))
        j["size"] = max(j["size"], e["size"])
        j["mats"].append(name)
        e["bkey"] = k
    if e["normal"]:
        j = norm_jobs.setdefault(e["normal"], dict(src=e["normal"], size=0, stem=clean(e["normal"]), mats=[]))
        j["size"] = max(j["size"], max(256, e["size"] // 2))
        e["nkey"] = e["normal"]
used = set()
for d, suffix in ((base_jobs, "_BaseColor"), (norm_jobs, "_Normal")):
    for k, j in d.items():
        s = "T_" + re.sub(r"(_(D|N|Normal|Diffuse|Albedo|BaseColor|AO|Occlusion|Height))+$", "", j["stem"], flags=re.I) if not j["stem"].startswith("T_") else j["stem"]
        s0, i = s, 2
        while s in used:
            s = "%s_%d" % (s0, i)
            i += 1
        used.add(s)
        j["stem"] = s
        sz = min(Image.open(j["src"]).size)
        p2 = 1 << (int(sz).bit_length() - 1)
        j["size"] = min(j["size"] or 512, p2)
print("base textures", len(base_jobs), "normals", len(norm_jobs), "| base sizes", dict(collections.Counter(j["size"] for j in base_jobs.values())),
      "| normal sizes", dict(collections.Counter(j["size"] for j in norm_jobs.values())))
for n, e in plan.items():
    j = base_jobs.get(e.get("bkey"))
    print("  %-26s base=%-30s size=%-5s n=%-5s ao=%-5s clip=%-5s cull=%d usage(max/avg)=%s" % (n, os.path.basename(e["base"] or "-"), j["size"] if j else 0, bool(e["normal"]), bool(e["ao"]), e["clip"], e["cull"], usage.get(n)))

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
coll = (set(prefabs.values()) | used_fbx | used_mat) & mr
print("GUID collisions with Mr. Moonlight:", len(coll))
if coll or unresolved or MODE == "dry":
    sys.exit(1 if (coll or unresolved) else 0)

# ------------------------------------------------------------------ textures
work = tempfile.mkdtemp(prefix="dead_tex_")
alpha_stems = set()
for k, j in base_jobs.items():
    im = Image.open(j["src"]).convert("RGBA")
    has_alpha = j["clip"] and np.asarray(im.getchannel("A")).min() < 250
    d = os.path.join(work, "b%d" % j["size"])
    os.makedirs(d, exist_ok=True)
    if has_alpha:
        alpha_stems.add(j["stem"])
        im.save(os.path.join(d, j["stem"] + "_BaseColor.png"))
    else:
        im.convert("RGB").save(os.path.join(d, j["stem"] + "_BaseColor.png"))
    if j["ao"]:
        Image.open(j["ao"]).convert("RGB").getchannel("G").save(os.path.join(d, j["stem"] + "_AO.png"))
for k, j in norm_jobs.items():
    d = os.path.join(work, "n%d" % j["size"])
    os.makedirs(d, exist_ok=True)
    nim = Image.open(j["src"]).convert("RGB")
    if "directx" in os.path.basename(j["src"]).lower():     # DirectX (Y-) -> Unity OpenGL (Y+): invert green
        a = np.asarray(nim).copy()
        a[..., 1] = 255 - a[..., 1]
        nim = Image.fromarray(a)
    nim.save(os.path.join(d, j["stem"] + "_Normal.png"))
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
    out = os.path.join(work, sub, "_out")
    for fn in os.listdir(out):
        stem = fn[:-4]
        g = uuid.uuid4().hex
        t = rxg.sub("guid: " + g, tpl_norm if stem.endswith("_Normal") else tpl_base, count=1)
        if stem.endswith("_BaseColor") and stem[:-len("_BaseColor")] in alpha_stems:
            t = t.replace("alphaIsTransparency: 0", "alphaIsTransparency: 1")
        shutil.copy2(os.path.join(out, fn), os.path.join(tex_dir, fn))
        open(os.path.join(tex_dir, fn + ".meta"), "w", encoding="utf8", newline="\n").write(t)
        tex_guid[stem] = g
print("textures placed", len(tex_guid))

# ------------------------------------------------------------------ materials
tpl = open(os.path.join(CHURCH, "Materials", "M_WoodLogs0023.mat"), encoding="utf8").read()
mdir = os.path.join(DEST, "Materials")
os.makedirs(mdir, exist_ok=True)
for name, e in plan.items():
    t = tpl.replace("m_Name: M_WoodLogs0023", "m_Name: " + name)
    bg = tex_guid[base_jobs[e["bkey"]]["stem"] + "_BaseColor"] if "bkey" in e else None
    ng = tex_guid[norm_jobs[e["nkey"]]["stem"] + "_Normal"] if "nkey" in e else None
    for prop, guid in (("_BaseMap", bg), ("_NormalMap", ng)):
        ref = "{fileID: 2800000, guid: %s, type: 3}" % guid if guid else "{fileID: 0}"
        t = re.sub(r"(- %s:\n\s+m_Texture: )\{[^}]*\}" % prop, lambda m: m.group(1) + ref, t, count=1)
    t = t.replace("    - _ResolutionLimit: 256", "    - _ResolutionLimit: 8192")
    t = t.replace("    - _Cull: 2", "    - _Cull: %d" % e["cull"])
    if e["clip"]:
        t = t.replace("    - _AlphaClip: 0", "    - _AlphaClip: 1")
        t = t.replace("  m_ValidKeywords:\n", "  m_ValidKeywords:\n  - _ALPHATEST_ON\n")
    t = t.replace("    - _BaseColor: {r: 0.588, g: 0.588, b: 0.588, a: 1}", "    - _BaseColor: {r: %s, g: %s, b: %s, a: 1}" % e["tint"])
    assert "guid: 9d78dfd702f8ca1449b3d47347019785" not in t and "guid: 694fd3f9ff308d94d9e6d569279b1a1c" not in t, "church texture leaked into " + name
    open(os.path.join(mdir, name + ".mat"), "w", encoding="utf8", newline="\n").write(t)
    shutil.copy2(e["path"] + ".meta", os.path.join(mdir, name + ".mat.meta"))
print("materials written", len(plan))

# ------------------------------------------------------------------ FBX + prefabs
n = 0
for g in sorted(used_fbx):
    dst = os.path.join(DEST, "Meshes", os.path.relpath(byg[g], os.path.join(PACK, "Models")))
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    shutil.copy2(byg[g], dst)
    shutil.copy2(byg[g] + ".meta", dst + ".meta")
    n += 1
for p in prefabs:
    dst = os.path.join(DEST, "Prefabs", os.path.relpath(p, os.path.join(PACK, "Prefabs")))
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    shutil.copy2(p, dst)
    shutil.copy2(p + ".meta", dst + ".meta")
    n += 1
print("fbx + prefabs copied:", n, "| texture work dir", work)
