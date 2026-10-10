"""Preflight check for assets that were copied/generated into Assets/ BEFORE Unity is asked to import them.

Usage: python preflight_import.py <folder-or-file> [more paths ...]
Exit code 1 = BLOCK (do not refresh Unity), 0 = OK (warnings are advice).

Why: on 2026-10-10 a hand-extracted prefab (3,190 nested prefab instances lifted out of a vendor scene) made the
Mr. Moonlight editor hang for 20+ minutes, and a 25-instance copy hung it again. Nothing structural was wrong that a
parser could see, so the rule is: generated YAML (prefab/scene/material/controller written by a script, not by Unity)
is tested in Playground first. This script catches the things that CAN be known for certain and flags those cases.
See Docs/asset-import-update-process.md ("Crash countermeasures").
"""
import os, re, sys, collections

MR = r"E:\MrMoonlight"
ASSETS = os.path.join(MR, "Assets")
YAML_EXT = (".prefab", ".mat", ".unity", ".asset", ".controller", ".anim", ".overrideController", ".physicMaterial", ".mask")
NESTING_WARN = 20          # nested PrefabInstances in one prefab file: test in Playground first
BIG_FILE_MB = 200
BUILTIN = {"0000000000000000e000000000000000", "0000000000000000f000000000000000"}
rxg = re.compile(r"^guid: ([0-9a-f]{32})", re.M)
rxref = re.compile(r"guid: ([0-9a-f]{32})")

targets = [os.path.abspath(a) for a in sys.argv[1:]]
if not targets:
    sys.exit(__doc__)
files = []
for t in targets:
    if os.path.isfile(t):
        files.append(t)
    else:
        for d, ds, fs in os.walk(t):
            for f in fs:
                files.append(os.path.join(d, f))
assets = [f for f in files if not f.endswith(".meta")]
metas = [f for f in files if f.endswith(".meta")]
block, warn = [], []


def guid_of(meta):
    try:
        m = rxg.search(open(meta, encoding="utf8", errors="ignore").read(400))
    except OSError:
        return None
    return m.group(1) if m else None


# 1. meta pairing + empty files + huge files
for a in assets:
    if not os.path.exists(a + ".meta"):
        block.append("no .meta next to %s (Unity would invent a new GUID and break references)" % a)
    if os.path.getsize(a) == 0:
        block.append("zero-byte file: %s" % a)
    elif os.path.getsize(a) > BIG_FILE_MB * 1024 * 1024:
        warn.append("very large file (%d MB): %s" % (os.path.getsize(a) // 2**20, a))
for m in metas:
    base = m[:-5]
    if not os.path.exists(base):
        block.append("orphan .meta (no asset): %s" % m)

# 2. GUID collisions: inside the target, and against every other .meta in Assets
mine = {}
for m in metas:
    g = guid_of(m)
    if g:
        if g in mine:
            block.append("duplicate GUID %s inside the target: %s and %s" % (g, mine[g], m))
        mine[g] = m
known = set()
mine_paths = {os.path.normcase(m) for m in metas}
for d, ds, fs in os.walk(ASSETS):
    for f in fs:
        if f.endswith(".meta"):
            p = os.path.join(d, f)
            g = guid_of(p)
            if g:
                known.add(g)
                if g in mine and os.path.normcase(p) not in mine_paths:
                    block.append("GUID %s of %s is already used by %s" % (g, mine[g], p))
pkg = os.path.join(MR, "Library", "PackageCache")
pkg_guids = set()
if os.path.isdir(pkg):
    for d, ds, fs in os.walk(pkg):
        for f in fs:
            if f.endswith(".meta"):
                g = guid_of(os.path.join(d, f))
                if g:
                    pkg_guids.add(g)

# 3. YAML assets: structure, nesting, unresolved references
nest_report, unresolved = [], collections.defaultdict(set)
for a in assets:
    if not a.endswith(YAML_EXT):
        continue
    try:
        t = open(a, encoding="utf8", errors="strict").read()
    except UnicodeDecodeError:
        continue   # binary-serialised asset, nothing to parse
    if not t.startswith("%YAML"):
        if not t.startswith("\ufeff%YAML"):
            continue
    for g in set(rxref.findall(t)):
        if g not in known and g not in pkg_guids and g not in BUILTIN and g not in mine:
            unresolved[a].add(g)
    if a.endswith(".prefab"):
        ids = set(re.findall(r"(?m)^--- !u!\d+ &(-?\d+)", t))
        pis = re.findall(r"(?m)^--- !u!1001 &(\d+)", t)
        stripped_pi = set(re.findall(r"(?ms)^--- !u!\d+ &\d+ stripped\n.*?m_PrefabInstance: \{fileID: (\d+)\}", t))
        for pi in stripped_pi:
            if pi not in set(pis):
                block.append("%s: a stripped object points at PrefabInstance %s which is not in the file" % (a, pi))
                break
        for tp in set(re.findall(r"m_TransformParent: \{fileID: (-?\d+)\}", t)):
            if tp != "0" and tp not in ids:
                block.append("%s: m_TransformParent %s is not defined in the file" % (a, tp))
                break
        if len(pis) >= NESTING_WARN:
            nest_report.append((len(pis), a))
for n, a in sorted(nest_report, reverse=True):
    warn.append("GENERATED-NESTING RISK: %s holds %d nested PrefabInstances. If a SCRIPT wrote it (not Unity), import it in Playground first, "
                "1 piece, then 25, then all; never first in Mr. Moonlight." % (a, n))
for a, gs in unresolved.items():
    warn.append("%s references %d GUID(s) not found in Assets/Packages (e.g. %s)" % (a, len(gs), sorted(gs)[0]))

print("preflight: %d assets, %d metas" % (len(assets), len(metas)))
for w in warn:
    print("WARN :", w)
for b in block:
    print("BLOCK:", b)
if block:
    print("RESULT: BLOCK - fix the above before asking Unity to refresh")
    sys.exit(1)
print("RESULT: OK" + (" (with warnings)" if warn else ""))
