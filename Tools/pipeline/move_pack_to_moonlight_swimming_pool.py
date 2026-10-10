"""Move AST-162 Abandoned Swimming Pool (URP version) from Playground into Mr. Moonlight.

Usage: python move_pack_to_moonlight_swimming_pool.py dry|go <usage.txt>

* every vendor prefab (148) + the FBX they use, vendor GUIDs kept (so the scene's nested instances resolve);
* the vendor URP Shader Graph materials are rebuilt as RetroLit (BaseColor + Normal only, AO multiplied into the
  BaseColor, tint + tiling kept, dirt/blend/mask layers dropped); glass and the edge decal stay URP/Lit transparent;
* BaseColor goes through the pixelation filter; size is chosen per material from how big the objects using it are
  (usage.txt: name|maxDim m|instances|avgDim m, measured from the open scene), never below 1024 (never above the source);
* the scene's `Meshes` root (3,190 nested prefab instances) is lifted out of AbandonedPoolURP.unity into one prefab.
Adapted from move_pack_to_moonlight_witch_village.py / ..._shed.py.
"""
import os, re, sys, shutil, uuid, subprocess, tempfile, collections
import numpy as np
from PIL import Image

MODE = sys.argv[1]
USAGE = sys.argv[2]
PG = r"E:\playground\Playground"
ROOT = os.path.join(PG, "Assets", "PLAYGROUND", "AST-162 (Swimming Pool)", "LeartesStudios", "AbandonedPool")
ART = os.path.join(ROOT, "Art")
SCENE = os.path.join(ROOT, "Scenes", "AbandonedPoolURP.unity")
MR = r"E:\MrMoonlight"
DEST = os.path.join(MR, "Assets", "_Project", "Art", "Buildings & Props", "AST-162 (Swimming Pool)")
CHURCH = os.path.join(MR, "Assets", "_Project", "Art", "Buildings & Props", "WoodenChurch")
VILLAGE_MATS = os.path.join(MR, "Assets", "_Project", "Art", "Buildings & Props", "AST-292 (Witch Village)", "Materials")
SHED_PREFAB_META = os.path.join(MR, "Assets", "_Project", "Art", "Buildings & Props", "AST-108 (The Shed)", "Prefabs", "The Shed.prefab.meta")
rxg = re.compile(r"^guid: ([0-9a-f]{32})", re.M)
rxref = re.compile(r"guid: ([0-9a-f]{32})")
SHADER_URPLIT = "933532a4fcc9baf4fa0491de14d08ed7"

# shader graph guid prefix -> property reference names (read from the .shadergraph files)
GRAPH = {
    "03e8deba": dict(name="S_2MASKS", base="Texture2D_3e79033d41be4ed9a0940530b58ea53e", normal="Texture2D_840d23db13e043f891b25f3b8582c36d",
                     ao="Texture2D_4154481551e94165b85cff250877afc9", tint="Color_c30230a9e7ad469ab5f63da8c7027dbc", tile="Color_71d8dfd789d14f8282bc3f7c93b75996"),
    "dcd2dbc3": dict(name="S_Masked", base="Texture2D_3e79033d41be4ed9a0940530b58ea53e", normal="Texture2D_9e55e19979f8417dacad815385eca3eb",
                     ao="Texture2D_5306de00521d42eb81920a679e7eca37", tint="Color_c30230a9e7ad469ab5f63da8c7027dbc", tile="Color_71d8dfd789d14f8282bc3f7c93b75996"),
    "24d4878a": dict(name="S_Master", base="Texture2D_640062ce90574780bfb7a257717cbf0f", normal="Texture2D_60dc05dac51343d087e21030e1eb6402",
                     ao="Texture2D_e48a92cb654d4b7692e39d3bdf3d0df8", tint="Color_4ac1faeb9feb49e68e08639aed4565e2", tile="Color_83db8a705bcf48d5b6aae96f6bb81e88", amount="Vector1_6ddd5db100974b41b83d64e410cd7bbb", amount_default=1.0),
    "339127ec": dict(name="S_Vegetation", base="Texture2D_518e65fecdbc414393220cffe2f468a7", normal="Texture2D_9afa3f2350f84bcbb11db3cc8b1158c5",
                     ao="Texture2D_610c24001d7741e49566ef0051184685", opacity="Texture2D_ad1df4cadbef47dd8723fa72ceaef29d", tint="Color_1a47e6ddb976436ebe76a684ea02f911", veg=True),
    "4a46781b": dict(name="S_Blend", base="Texture2D_41f337bd88564f4893658d4597e852fc", normal="Texture2D_b56b9b3ce11242f4aea422d5cc1fa653",
                     ao="Texture2D_e38c43459b5744278a16c9c97ab0a0dc", tint="Color_b146d89561bb4e8fa308e738b5b67936", tile="Color_1263df77c6ca4bb78a24a1f53be30df0", amount="Vector1_293b491d1ce94223b50ab34adf5ad163", amount_default=0.0),
    "6aac4ac3": dict(name="S_ConcreteBaseWorldAligned", base="_BaseMap", normal="_NormalMap", ao="_ORMMap", tint="_BaseMapTint"),
    "fd37c0fd": dict(name="S_EdgeDecal", opacity="Texture2D_16633cea21904078876daad12caec832", decal=True),
}
GLASS = {"M_Glass", "M_BrokenGlass"}          # transparent: keep URP/Lit like the village glass
CLIP_NAMES = {"M_Fanfence"}                    # chain-link: alpha clip, not blend


def walk(r, exts):
    for d, _, fs in os.walk(r):
        for f in fs:
            if f.lower().endswith(exts):
                yield os.path.join(d, f)


def meta_guid(p):
    m = rxg.search(open(p + ".meta", encoding="utf8", errors="ignore").read(400))
    return m.group(1) if m else None


prefabs = {p: meta_guid(p) for p in walk(os.path.join(ART, "Prefabs"), (".prefab",))}
fbx = {p: meta_guid(p) for p in walk(ART, (".fbx",))}
mats = {p: meta_guid(p) for p in walk(ART, (".mat",))}
texs = {p: meta_guid(p) for p in walk(ART, (".tga", ".bmp", ".png", ".exr"))}
byg = {}
for d in (prefabs, fbx, mats, texs):
    for p, g in d.items():
        byg[g] = p

used_fbx, used_mat = set(), set()
for p in prefabs:
    for g in set(rxref.findall(open(p, encoding="utf8", errors="ignore").read())):
        if g in byg:
            if byg[g] in fbx:
                used_fbx.add(g)
            elif byg[g] in mats:
                used_mat.add(g)
for g in list(used_fbx):
    for r in set(rxref.findall(open(byg[g] + ".meta", encoding="utf8", errors="ignore").read())):
        if r in byg and byg[r] in mats:
            used_mat.add(r)
print("prefabs", len(prefabs), "| fbx used", len(used_fbx), "of", len(fbx), "| materials used", len(used_mat))

usage = {}
for line in open(USAGE, encoding="utf8"):
    if "|" in line:
        n, mx, c, av = line.strip().split("|")
        usage[n] = (float(mx), int(c), float(av))


def parse_mat(path):
    t = open(path, encoding="utf8", errors="ignore").read()
    tex = {}
    for m in re.finditer(r"    - (\S+):\n\s+m_Texture: \{fileID: (\d+), guid: ([0-9a-f]{32}).*\n\s+m_Scale: \{x: ([-\d.e]+), y: ([-\d.e]+)\}", t):
        if m.group(3) in byg and byg[m.group(3)] in texs:
            tex[m.group(1)] = (byg[m.group(3)], float(m.group(4)), float(m.group(5)))
    col = {m.group(1): tuple(float(m.group(i)) for i in (2, 3, 4, 5)) for m in re.finditer(r"    - (\S+): \{r: ([-\d.e]+), g: ([-\d.e]+), b: ([-\d.e]+), a: ([-\d.e]+)\}", t)}
    flo = {m.group(1): float(m.group(2)) for m in re.finditer(r"    - (\S+): ([-\d.e]+)\n", t)}
    sh = re.search(r"m_Shader: \{fileID: -?\d+, guid: ([0-9a-f]{32})", t).group(1)
    return dict(tex=tex, col=col, flo=flo, shader=sh)


def tier(name):
    mx, c, av = usage.get(name, (0, 0, 0))
    return 2048 if (av >= 6.0 or mx >= 20.0) else 1024


def clean(p):
    s = os.path.splitext(os.path.basename(p))[0]
    s = re.sub(r"_(BaseColor|Basecolor|B|D|N|Nrm|Normal|AORM|ORM|O|AO)$", "", s, flags=re.I)
    return re.sub(r"[^A-Za-z0-9_]", "", s)


plan = collections.OrderedDict()
base_jobs, norm_jobs = collections.OrderedDict(), collections.OrderedDict()   # key -> job
for g in sorted(used_mat, key=lambda x: byg[x]):
    path = byg[g]
    name = os.path.splitext(os.path.basename(path))[0]
    m = parse_mat(path)
    cfg = GRAPH.get(m["shader"][:8])
    e = dict(name=name, path=path, guid=g, kind="lit", base=None, normal=None, ao=None, alpha=None, tint=(1, 1, 1, 1), tile=(1, 1), clip=False, cull=2, size=tier(name), raw=m)
    if m["shader"] == SHADER_URPLIT:
        e["base"] = (m["tex"].get("_BaseMap") or m["tex"].get("_MainTex") or (None,))[0]
        e["normal"] = (m["tex"].get("_BumpMap") or (None,))[0]
        e["ao"] = (m["tex"].get("_OcclusionMap") or (None,))[0]
        bc = m["col"].get("_BaseColor", (1, 1, 1, 1))
        e["tint"] = bc
        e["clip"] = m["flo"].get("_AlphaClip", 0) == 1
        e["cull"] = int(m["flo"].get("_Cull", 2))
        if name in GLASS:
            e["kind"] = "glass"
        elif m["flo"].get("_Surface", 0) == 1 and name in CLIP_NAMES:
            e["clip"], e["cull"] = True, 0
    elif cfg is None:
        raise SystemExit("unknown shader %s on %s" % (m["shader"], name))
    else:
        if cfg.get("decal"):
            e["kind"] = "decal"
            e["alpha"] = (m["tex"].get(cfg["opacity"]) or (None,))[0]
        else:
            e["base"] = (m["tex"].get(cfg["base"]) or (None,))[0]
            e["normal"] = (m["tex"].get(cfg["normal"]) or (None,))[0]
            e["ao"] = (m["tex"].get(cfg["ao"]) or (None,))[0]
            t = m["col"].get(cfg["tint"], (1, 1, 1, 1))
            a = m["flo"].get(cfg["amount"], cfg["amount_default"]) if cfg.get("amount") else t[3]
            a = max(0.0, min(1.0, a))
            # lerp(white, tint, amount); floor at 0.08 so a "black variant" keeps visible detail instead of flat black
            e["tint"] = tuple(max(0.08, 1 + (t[i] - 1) * a) for i in range(3)) + (1,)
            if cfg.get("tile") and cfg["tile"] in m["col"]:
                e["tile"] = (m["col"][cfg["tile"]][0] or 1, m["col"][cfg["tile"]][1] or 1)
            if cfg.get("veg"):
                e["clip"], e["cull"] = True, 0
                op = m["tex"].get(cfg["opacity"])
                e["alpha"] = op[0] if op else None
    plan[name] = e

# texture jobs (deduplicated; size = largest tier among the materials sharing the source)
for name, e in plan.items():
    if e["kind"] == "decal":
        if e["alpha"]:
            k = ("decal", e["alpha"])
            base_jobs.setdefault(k, dict(src=None, ao=None, alpha=e["alpha"], size=0, stem="T_" + name[2:], mats=[]))
            base_jobs[k]["size"] = max(base_jobs[k]["size"], e["size"])
            base_jobs[k]["mats"].append(name)
            e["bkey"] = k
        continue
    if e["base"]:
        a = e["alpha"] if (e["alpha"] and e["alpha"] != e["base"]) else None
        k = (e["base"], e["ao"], a, e["clip"] or e["kind"] == "glass")
        j = base_jobs.setdefault(k, dict(src=e["base"], ao=e["ao"], alpha=a, size=0, stem=clean(e["base"]), clip=e["clip"] or e["kind"] == "glass", mats=[]))
        j["size"] = max(j["size"], e["size"])
        j["mats"].append(name)
        e["bkey"] = k
    if e["normal"]:
        j = norm_jobs.setdefault(e["normal"], dict(src=e["normal"], size=0, stem=clean(e["normal"]) + "_Normal", mats=[]))
        j["size"] = max(j["size"], e["size"] // 2)
        e["nkey"] = e["normal"]
used = set()
for d in (base_jobs, norm_jobs):
    for k, j in d.items():
        s, i = j["stem"], 2
        while s in used:
            s = "%s_%d" % (j["stem"], i)
            i += 1
        used.add(s)
        j["stem"] = s
        try:
            src = j.get("src") or j["alpha"]
            sz = min(Image.open(src).size)
        except Exception:
            sz = 4096
        j["size"] = min(j["size"] if j["size"] else 1024, 1 << (int(sz).bit_length() - 1) if sz & (sz - 1) else sz)

print("materials:", collections.Counter(e["kind"] for e in plan.values()), "| base textures", len(base_jobs), "| normals", len(norm_jobs))
print("base size distribution:", dict(collections.Counter(j["size"] for j in base_jobs.values())), "| normals:", dict(collections.Counter(j["size"] for j in norm_jobs.values())))
for n, e in plan.items():
    sizes = base_jobs[e["bkey"]]["size"] if "bkey" in e else 0
    print("  %-22s %-7s base=%-28s n=%s ao=%s size=%s clip=%s tint=%s" % (n, e["kind"], os.path.basename(e["base"] or "-"), bool(e["normal"]), bool(e["ao"]), sizes, e["clip"], tuple(round(x, 2) for x in e["tint"][:3])))

# --------------------------------------------------------------------- collisions
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
keep = set(prefabs.values()) | used_fbx | used_mat
coll = keep & mr
print("GUID collisions with Mr. Moonlight:", len(coll))
if coll or MODE == "dry":
    sys.exit(1 if coll else 0)

# --------------------------------------------------------------------- GO 1: texture pass, grouped by size
work = tempfile.mkdtemp(prefix="pool_tex_")
groups = collections.defaultdict(list)
alpha_stems = set()
for k, j in base_jobs.items():
    if j.get("src"):
        im = Image.open(j["src"]).convert("RGBA")
        if j["alpha"]:
            mim = Image.open(j["alpha"]).convert("RGBA")
            ma = np.asarray(mim.getchannel("A"))
            a = Image.fromarray(ma if (ma < 128).mean() > 0.01 else np.asarray(mim.getchannel("R")))
            if a.size != im.size:
                a = a.resize(im.size, Image.LANCZOS)
            im.putalpha(a)
        has_alpha = j.get("clip") and np.asarray(im.getchannel("A")).min() < 250
    else:   # decal: dark flat colour, alpha from the opacity map
        mim = Image.open(j["alpha"]).convert("RGBA")
        im = Image.new("RGBA", mim.size, (20, 20, 20, 255))
        im.putalpha(Image.fromarray(np.asarray(mim.getchannel("R"))))
        has_alpha = True
    d = os.path.join(work, "b%d" % j["size"])
    os.makedirs(d, exist_ok=True)
    if has_alpha:
        alpha_stems.add(j["stem"])
        im.save(os.path.join(d, j["stem"] + "_BaseColor.png"))
    else:
        im.convert("RGB").save(os.path.join(d, j["stem"] + "_BaseColor.png"))
    if j.get("ao"):
        Image.open(j["ao"]).convert("RGB").getchannel("R").save(os.path.join(d, j["stem"] + "_AO.png"))
    groups[("b", j["size"])].append(j["stem"])
for k, j in norm_jobs.items():
    d = os.path.join(work, "n%d" % j["size"])
    os.makedirs(d, exist_ok=True)
    Image.open(j["src"]).convert("RGB").save(os.path.join(d, j["stem"] + ".png"))
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
        if stem[: -len("_BaseColor")] in alpha_stems and not stem.endswith("_Normal"):
            t = t.replace("alphaIsTransparency: 0", "alphaIsTransparency: 1")
        shutil.copy2(os.path.join(out, fn), os.path.join(tex_dir, fn))
        open(os.path.join(tex_dir, fn + ".meta"), "w", encoding="utf8", newline="\n").write(t)
        tex_guid[stem] = g
print("textures placed", len(tex_guid))

# --------------------------------------------------------------------- GO 2: materials
tpl_lit = open(os.path.join(CHURCH, "Materials", "M_WoodLogs0023.mat"), encoding="utf8").read()
tpl_glass = open(os.path.join(VILLAGE_MATS, "M_WallWindowGlass.mat"), encoding="utf8").read()
mdir = os.path.join(DEST, "Materials")
os.makedirs(mdir, exist_ok=True)


def set_tex(t, prop, guid, scale):
    pat = r"(- %s:\n\s+m_Texture: )\{[^}]*\}(\n\s+m_Scale: )\{[^}]*\}" % prop
    ref = "{fileID: 2800000, guid: %s, type: 3}" % guid if guid else "{fileID: 0}"
    return re.sub(pat, lambda m: m.group(1) + ref + m.group(2) + "{x: %s, y: %s}" % scale, t, count=1)


for name, e in plan.items():
    flat = (1, 1)
    bg = tex_guid[base_jobs[e["bkey"]]["stem"] + "_BaseColor"] if "bkey" in e else None
    ng = tex_guid[norm_jobs[e["nkey"]]["stem"]] if "nkey" in e else None
    if e["kind"] in ("glass", "decal"):
        t = tpl_glass.replace("m_Name: M_WallWindowGlass", "m_Name: " + name)
        t = set_tex(t, "_BaseMap", bg, (1, 1))
        c = e["tint"] if e["kind"] == "glass" else (1, 1, 1, 1)
        t = re.sub(r"    - _BaseColor: \{[^}]*\}", "    - _BaseColor: {r: %s, g: %s, b: %s, a: %s}" % tuple(c), t)
    else:
        t = tpl_lit.replace("m_Name: M_WoodLogs0023", "m_Name: " + name)
        t = set_tex(t, "_BaseMap", bg, e["tile"])
        t = set_tex(t, "_NormalMap", ng, e["tile"])
        t = t.replace("    - _ResolutionLimit: 256", "    - _ResolutionLimit: 8192")
        t = t.replace("    - _Cull: 2", "    - _Cull: %d" % e["cull"])
        if e["clip"]:
            t = t.replace("    - _AlphaClip: 0", "    - _AlphaClip: 1")
            t = t.replace("  m_ValidKeywords:\n", "  m_ValidKeywords:\n  - _ALPHATEST_ON\n")
        t = t.replace("    - _BaseColor: {r: 0.588, g: 0.588, b: 0.588, a: 1}", "    - _BaseColor: {r: %s, g: %s, b: %s, a: 1}" % tuple(e["tint"][:3]))
        assert "guid: 9d78dfd702f8ca1449b3d47347019785" not in t and "guid: 694fd3f9ff308d94d9e6d569279b1a1c" not in t, "church texture leaked into " + name
    open(os.path.join(mdir, name + ".mat"), "w", encoding="utf8", newline="\n").write(t)
    shutil.copy2(e["path"] + ".meta", os.path.join(mdir, name + ".mat.meta"))
print("materials written", len(plan))

# --------------------------------------------------------------------- GO 3: FBX (Read/Write off) + prefabs
n = 0
for g in sorted(used_fbx):
    src = byg[g]
    dst = os.path.join(DEST, "Meshes", os.path.relpath(src, os.path.join(ART, "Meshes")))
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    shutil.copy2(src, dst)
    mt = open(src + ".meta", encoding="utf8").read().replace("    isReadable: 1", "    isReadable: 0")
    open(dst + ".meta", "w", encoding="utf8", newline="\n").write(mt)
    n += 1
for p in prefabs:
    dst = os.path.join(DEST, "Prefabs", os.path.relpath(p, os.path.join(ART, "Prefabs")))
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    shutil.copy2(p, dst)
    shutil.copy2(p + ".meta", dst + ".meta")
    n += 1
print("fbx + prefabs copied:", n)

# --------------------------------------------------------------------- GO 4: assembled prefab from the scene's Meshes root
docs = re.split(r"(?m)^(?=--- !u!)", open(SCENE, encoding="utf8").read())
header, docs = docs[0], docs[1:]
go_doc = next(d for d in docs if d.startswith("--- !u!1 ") and re.search(r"m_Name: Meshes\n", d))
go_id = re.match(r"--- !u!1 &(\d+)", go_doc).group(1)
tr_doc = next(d for d in docs if d.startswith("--- !u!4 ") and ("m_GameObject: {fileID: %s}" % go_id) in d)
tr_id = re.match(r"--- !u!4 &(\d+)", tr_doc).group(1)
pis = [d for d in docs if d.startswith("--- !u!1001") and ("m_TransformParent: {fileID: %s}" % tr_id) in d]
pi_ids = {re.match(r"--- !u!1001 &(\d+)", d).group(1) for d in pis}
stripped = [d for d in docs if " stripped" in d.split("\n")[0] and re.search(r"m_PrefabInstance: \{fileID: (\d+)\}", d) and re.search(r"m_PrefabInstance: \{fileID: (\d+)\}", d).group(1) in pi_ids]
print("assembled: instances", len(pis), "stripped", len(stripped))
tr_doc = re.sub(r"m_LocalPosition: \{[^}]*\}", "m_LocalPosition: {x: 0, y: 0, z: 0}", tr_doc, count=1)
tr_doc = re.sub(r"m_Father: \{[^}]*\}", "m_Father: {fileID: 0}", tr_doc, count=1)
go_doc = go_doc.replace("m_Name: Meshes\n", "m_Name: AbandonedPool_Assembled\n", 1)
body = header + go_doc + tr_doc + "".join(pis) + "".join(stripped)
pdir = os.path.join(DEST, "Prefabs")
open(os.path.join(pdir, "AbandonedPool_Assembled.prefab"), "w", encoding="utf8", newline="\n").write(body)
open(os.path.join(pdir, "AbandonedPool_Assembled.prefab.meta"), "w", encoding="utf8", newline="\n").write(rxg.sub("guid: " + uuid.uuid4().hex, open(SHED_PREFAB_META, encoding="utf8").read(), count=1))
print("done; texture work dir", work)
