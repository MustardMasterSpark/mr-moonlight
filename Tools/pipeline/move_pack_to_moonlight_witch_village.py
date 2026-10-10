import json, os, re, sys, shutil, uuid, subprocess, collections
import numpy as np
from PIL import Image

MODE = sys.argv[1]  # dry | go
sp = r"C:\Users\calva\AppData\Local\Temp\claude\E--MrMoonlight\784ac8cd-9cc3-4aab-9dfb-5208d64874a4\scratchpad"
PG = r"E:\playground\Playground"
ART = os.path.join(PG, "Assets", "PLAYGROUND", "AST-292 (Witch Village)", "LeartesStudios", "WitchVillage", "Art")
MR = r"E:\MrMoonlight"
DEST = os.path.join(MR, "Assets", "_Project", "Art", "Buildings & Props", "AST-292 (Witch Village)")
CHURCH = os.path.join(MR, "Assets", "_Project", "Art", "Buildings & Props", "WoodenChurch")
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


# ---------------------------------------------------------------- inventories
prefabs = {p: meta_guid(p) for p in walk(os.path.join(ART, "Prefabs"), (".prefab",))}
prefabs.update({p: meta_guid(p) for p in walk(os.path.join(ART, "Particles"), (".prefab",))})
fbx = {p: meta_guid(p) for p in walk(os.path.join(ART, "Meshes"), (".fbx",))}
mats = {p: meta_guid(p) for p in walk(os.path.join(ART, "Materials"), (".mat",))}
mat_by_guid = {g: p for p, g in mats.items()}
fbx_by_guid = {g: p for p, g in fbx.items()}
print("prefabs", len(prefabs), "fbx", len(fbx), "vendor materials", len(mats))

spec = {e["path"]: e for e in json.load(open(sp + r"\witch_spec.json"))}
water_guid = [g for p, g in mats.items() if os.path.basename(p) == "M_Water2.mat"][0]

kept = {}
dropped = []
for p, g in prefabs.items():
    refs = set(rxref.findall(open(p, encoding="utf8", errors="ignore").read()))
    if water_guid in refs:
        dropped.append(os.path.basename(p))
        continue
    kept[p] = refs
print("kept prefabs", len(kept), "dropped (water):", dropped)

used_fbx, used_mats = set(), set()
for p, refs in kept.items():
    for g in refs:
        if g in fbx_by_guid:
            used_fbx.add(g)
        elif g in mat_by_guid:
            used_mats.add(g)
for g in list(used_fbx):
    for r in set(rxref.findall(open(fbx_by_guid[g] + ".meta", encoding="utf8", errors="ignore").read())):
        if r in mat_by_guid:
            used_mats.add(r)
print("used fbx", len(used_fbx), "used materials", len(used_mats))

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
preserve = set(prefabs[p] for p in kept) | used_fbx | used_mats
coll = [g for g in preserve if g in mr_guids]
print("GUID collisions with Mr. Moonlight:", len(coll))
if coll:
    sys.exit(1)

# ---------------------------------------------------------------- material / texture plan
CLIP = {"M_DriedLeaves", "M_Chains", "M_DeadBranches", "M_DeadWillow", "M_Rags", "M_Books", "M_HangingBouqet", "M_Ivy", "M_PineBranches",
        "M_Lilypads", "M_Drapes", "M_TallGrassD", "M_MossClump_Layered", "M_WhiteMoss"}
TINT = {"M_WoodBowls_Red": [1.0, 0.55, 0.50, 1.0], "M_WoodBowls_yellow": [1.0, 0.92, 0.50, 1.0]}
PARTICLE_MATS = {"M_DustP"}
TEXT = lambda rel: os.path.join(ART, rel.replace("/", os.sep))


def clean_stem(rel):
    s = os.path.splitext(os.path.basename(rel))[0]
    s = re.sub(r" - Copy - Copy$", "_Copy2", s)
    s = re.sub(r" - Copy$", "_Copy", s)
    s = re.sub(r"_(BMask|B|N|D|O|ORMMask|ORMH|ORM|R|AO)(?=_|$)", "", s)
    return re.sub(r"[^A-Za-z0-9_]", "", s)


base_jobs = collections.OrderedDict()   # stem -> {src, ao, alpha_from}
norm_jobs = collections.OrderedDict()   # stem -> src
src_to_base_stem = {}                    # (src, alpha_from) -> stem
src_to_norm_stem = {}
used_stems = set()


def unique(stem):
    s, i = stem, 2
    while s in used_stems:
        s = "%s_%d" % (stem, i)
        i += 1
    used_stems.add(s)
    return s


matinfo = collections.OrderedDict()
for mp in sorted(mat_by_guid[g] for g in used_mats):
    name = os.path.splitext(os.path.basename(mp))[0]
    e = spec.get("Materials/" + os.path.basename(mp))
    if e is None or e["kind"] == "skip":
        continue
    if e["kind"] in ("particle", "other") or name in PARTICLE_MATS:
        matinfo[name] = {"kind": "particle" if e["kind"] != "other" else "other", "path": mp, "guid": mats[mp]}
        continue
    base, normal, ao = e["base"], e["normal"], e["ao"]
    if not base and name in TINT:
        sib = spec["Materials/M_WoodBowls.mat"]
        base, normal, ao = sib["base"], sib["normal"], sib["ao"]
    if not base:
        print("   NO BASE:", name)
        continue
    mask = e["tex_all"].get("_OpacityMask")
    variant = mask if (mask and mask != base) else None
    key = (base, variant)
    if key not in src_to_base_stem:
        stem = unique(("T_" + name[2:]) if variant else clean_stem(base))
        src_to_base_stem[key] = stem
        base_jobs[stem] = {"src": base, "ao": ao, "alpha_from": variant}
    stem = src_to_base_stem[key]
    if ao and clean_stem(ao) == clean_stem(base):
        base_jobs[stem]["ao"] = ao
    nstem = None
    if normal:
        if normal not in src_to_norm_stem:
            src_to_norm_stem[normal] = unique(clean_stem(normal) + "_Normal")
            norm_jobs[src_to_norm_stem[normal]] = normal
        nstem = src_to_norm_stem[normal]
    cull = 0 if name in CLIP else e["floats"].get("_Cull", 2)
    matinfo[name] = {"kind": "lit", "path": mp, "guid": mats[mp], "base": stem + "_BaseColor", "normal": nstem, "clip": name in CLIP, "cull": cull, "tint": TINT.get(name)}

PART_TEX = {"T_Smoke": ("Textures/T_Smoke.png", None), "T_FireSubUV": ("Textures/T_Fire_SubUV.PNG", None),
            "T_Flame": ("Textures/T_Flame_B.PNG", "R"), "T_CandleFire": ("Textures/T_CandleFire.TGA", None)}
for k, (src, af) in PART_TEX.items():
    stem = unique(k)
    base_jobs[stem] = {"src": src, "ao": None, "alpha_from": af}
    assert stem == k, "particle texture name collision " + k
print("lit materials", sum(1 for m in matinfo.values() if m["kind"] == "lit"), "| other", [(k, v["kind"]) for k, v in matinfo.items() if v["kind"] != "lit"])
print("base texture jobs", len(base_jobs), "| normal jobs", len(norm_jobs))
json.dump({"matinfo": matinfo, "base_jobs": base_jobs, "norm_jobs": norm_jobs, "kept": [os.path.relpath(p, ART) for p in kept]}, open(sp + r"\witch_plan.json", "w"), indent=1)
if MODE == "dry":
    print("DRY RUN - nothing written")
    sys.exit(0)

# ---------------------------------------------------------------- GO 1: texture pass
work = os.path.join(sp, "witch_tex_work")
if os.path.exists(work):
    shutil.rmtree(work)
os.makedirs(work)
alpha_stems = set()
for stem, j in base_jobs.items():
    im = Image.open(TEXT(j["src"])).convert("RGBA")
    if j["alpha_from"]:
        if j["alpha_from"] == "R":
            a = Image.fromarray(np.asarray(im.getchannel("R")))
        else:
            mim = Image.open(TEXT(j["alpha_from"])).convert("RGBA")
            ma = np.asarray(mim.getchannel("A"))
            a = Image.fromarray(ma if (ma < 128).mean() > 0.01 else np.asarray(mim.getchannel("R")))
            if a.size != im.size:
                a = a.resize(im.size, Image.LANCZOS)
        im.putalpha(a)
    if np.asarray(im.getchannel("A")).min() < 250:
        alpha_stems.add(stem)
        im.save(os.path.join(work, stem + "_BaseColor.png"))
    else:
        im.convert("RGB").save(os.path.join(work, stem + "_BaseColor.png"))
    if j["ao"]:
        Image.open(TEXT(j["ao"])).convert("RGB").getchannel("R").save(os.path.join(work, stem + "_AO.png"))
for stem, src in norm_jobs.items():
    Image.open(TEXT(src)).convert("RGB").save(os.path.join(work, stem + ".png"))
print("work files", len(os.listdir(work)), "| bases with alpha:", len(alpha_stems))
r = subprocess.run([sys.executable, os.path.join(MR, "Tools", "pipeline", "texture_pass.py"), "run", work, "--size", "512", "--map-size", "256"], capture_output=True, text=True)
print("\n".join(r.stdout.strip().splitlines()[-4:]))
if r.returncode != 0:
    print(r.stderr)
    sys.exit(1)
outdir = os.path.join(work, "_out")
outs = set(os.listdir(outdir))
print("texture_pass outputs:", len(outs))

# ---------------------------------------------------------------- GO 2: place textures (+ metas from the church templates)
os.makedirs(os.path.join(DEST, "Textures"), exist_ok=True)
tpl_base = open([os.path.join(CHURCH, "Textures", f) for f in os.listdir(os.path.join(CHURCH, "Textures")) if f.endswith("BaseColor.png.meta")][0], encoding="utf8").read()
tpl_norm = open([os.path.join(CHURCH, "Textures", f) for f in os.listdir(os.path.join(CHURCH, "Textures")) if f.endswith("Normal.png.meta")][0], encoding="utf8").read()
tex_guid = {}
for fn in sorted(outs):
    stem = fn[:-4]
    kind_norm = stem.endswith("_Normal")
    tpl = tpl_norm if kind_norm else tpl_base
    g = uuid.uuid4().hex
    t = rxg.sub("guid: " + g, tpl, count=1)
    if stem[: -len("_BaseColor")] in alpha_stems and not kind_norm:
        t = t.replace("alphaIsTransparency: 0", "alphaIsTransparency: 1")
    shutil.copy2(os.path.join(outdir, fn), os.path.join(DEST, "Textures", fn))
    open(os.path.join(DEST, "Textures", fn + ".meta"), "w", encoding="utf8", newline="\n").write(t)
    tex_guid[stem] = g
json.dump(tex_guid, open(sp + r"\witch_texguid.json", "w"))
print("textures placed", len(tex_guid))

# ---------------------------------------------------------------- GO 3: RetroLit materials from the church template
tpl_mat = open(os.path.join(CHURCH, "Materials", "M_WoodLogs0023.mat"), encoding="utf8").read()
os.makedirs(os.path.join(DEST, "Materials"), exist_ok=True)
made = 0
for name, mi in matinfo.items():
    if mi["kind"] != "lit":
        continue
    t = tpl_mat
    t = t.replace("m_Name: M_WoodLogs0023", "m_Name: " + name)
    t = re.sub(r"(- _BaseMap:\n\s+m_Texture: \{fileID: 2800000, guid: )[0-9a-f]{32}", r"\g<1>" + tex_guid[mi["base"]], t)
    if mi["normal"]:
        t = re.sub(r"(- _NormalMap:\n\s+m_Texture: \{fileID: 2800000, guid: )[0-9a-f]{32}", r"\g<1>" + tex_guid[mi["normal"]], t)
    else:
        t = re.sub(r"(- _NormalMap:\n\s+m_Texture: )\{fileID: 2800000, guid: [0-9a-f]{32}, type: 3\}", r"\g<1>{fileID: 0}", t)
    t = t.replace("    - _ResolutionLimit: 256", "    - _ResolutionLimit: 8192")
    t = t.replace("    - _Cull: 2", "    - _Cull: %d" % int(mi["cull"]))
    if mi["clip"]:
        t = t.replace("    - _AlphaClip: 0", "    - _AlphaClip: 1")
        t = t.replace("  m_ValidKeywords:\n", "  m_ValidKeywords:\n  - _ALPHATEST_ON\n")
    c = mi["tint"] or [1, 1, 1, 1]
    t = t.replace("    - _BaseColor: {r: 0.588, g: 0.588, b: 0.588, a: 1}", "    - _BaseColor: {r: %s, g: %s, b: %s, a: %s}" % tuple(c))
    assert "guid: 9d78dfd702f8ca1449b3d47347019785" not in t and "guid: 694fd3f9ff308d94d9e6d569279b1a1c" not in t, "church texture guid leaked into " + name
    open(os.path.join(DEST, "Materials", name + ".mat"), "w", encoding="utf8", newline="\n").write(t)
    shutil.copy2(mi["path"] + ".meta", os.path.join(DEST, "Materials", name + ".mat.meta"))
    made += 1
# materials Unity will build fresh (particles, window glass): vendor .meta only, so the GUID is kept
for name, mi in matinfo.items():
    if mi["kind"] == "lit":
        continue
    shutil.copy2(mi["path"] + ".meta", os.path.join(DEST, "Materials", name + ".mat.meta"))
print("RetroLit materials written", made, "| to be built in Unity:", [n for n, m in matinfo.items() if m["kind"] != "lit"])

# ---------------------------------------------------------------- GO 4: meshes and prefabs (vendor GUIDs kept)
copied = collections.Counter()
for g in sorted(used_fbx):
    src = fbx_by_guid[g]
    rel = os.path.relpath(src, os.path.join(ART, "Meshes"))
    dst = os.path.join(DEST, "Meshes", rel)
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    shutil.copy2(src, dst)
    shutil.copy2(src + ".meta", dst + ".meta")
    copied["fbx"] += 1
for p in kept:
    rel = os.path.relpath(p, ART)
    dst = os.path.join(DEST, rel)
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    shutil.copy2(p, dst)
    shutil.copy2(p + ".meta", dst + ".meta")
    copied["prefab"] += 1
print("copied", dict(copied))
