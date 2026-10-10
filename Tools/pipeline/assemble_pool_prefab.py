"""Lift the `Meshes` root out of the AST-162 Abandoned Pool URP scene as a prefab (text extraction, no Unity needed).

Usage: python assemble_pool_prefab.py <N|all> <out.prefab> [<guid-for-meta>]
  N = keep only the first N nested prefab instances (for import timing tests); `all` = every instance.
Writes <out>.meta too (new GUID unless one is given). Vendor GUIDs are kept inside, so the nested instances
resolve against the copied vendor prefabs in `Art/Buildings & Props/AST-162 (Swimming Pool)/Prefabs`.
"""
import re, sys, uuid

SCENE = r"E:\playground\Playground\Assets\PLAYGROUND\AST-162 (Swimming Pool)\LeartesStudios\AbandonedPool\Scenes\AbandonedPoolURP.unity"
META_TPL = r"E:\MrMoonlight\Assets\_Project\Art\Buildings & Props\AST-108 (The Shed)\Prefabs\The Shed.prefab.meta"
N = sys.argv[1]
OUT = sys.argv[2]
GUID = sys.argv[3] if len(sys.argv) > 3 else uuid.uuid4().hex

docs = re.split(r"(?m)^(?=--- !u!)", open(SCENE, encoding="utf8").read())
header, docs = docs[0], docs[1:]
go_doc = next(d for d in docs if d.startswith("--- !u!1 ") and re.search(r"m_Name: Meshes\n", d))
go_id = re.match(r"--- !u!1 &(\d+)", go_doc).group(1)
tr_doc = next(d for d in docs if d.startswith("--- !u!4 ") and ("m_GameObject: {fileID: %s}" % go_id) in d)
tr_id = re.match(r"--- !u!4 &(\d+)", tr_doc).group(1)
pis = [d for d in docs if d.startswith("--- !u!1001") and ("m_TransformParent: {fileID: %s}" % tr_id) in d]
if N != "all":
    pis = pis[: int(N)]
pi_ids = {re.match(r"--- !u!1001 &(\d+)", d).group(1) for d in pis}
stripped = []
for d in docs:
    if " stripped" in d.split("\n")[0]:
        m = re.search(r"m_PrefabInstance: \{fileID: (\d+)\}", d)
        if m and m.group(1) in pi_ids:
            stripped.append(d)
# the root transform lists its children: keep only the kept instances' stripped transforms
kept_tr = {re.match(r"--- !u!4 &(\d+)", d).group(1) for d in stripped if d.startswith("--- !u!4 ")}
head, _, rest = tr_doc.partition("  m_Children:\n")
kids = re.findall(r"  - \{fileID: (\d+)\}\n", rest.split("  m_Father")[0])
tail = "  m_Father" + rest.split("  m_Father", 1)[1]
tr_doc = head + "  m_Children:\n" + "".join("  - {fileID: %s}\n" % k for k in kids if k in kept_tr) + tail
tr_doc = re.sub(r"m_LocalPosition: \{[^}]*\}", "m_LocalPosition: {x: 0, y: 0, z: 0}", tr_doc, count=1)
tr_doc = re.sub(r"m_Father: \{[^}]*\}", "m_Father: {fileID: 0}", tr_doc, count=1)
go_doc = go_doc.replace("m_Name: Meshes\n", "m_Name: %s\n" % re.sub(r"\.prefab$", "", OUT.replace("\\", "/").split("/")[-1]), 1)
open(OUT, "w", encoding="utf8", newline="\n").write(header + go_doc + tr_doc + "".join(pis) + "".join(stripped))
open(OUT + ".meta", "w", encoding="utf8", newline="\n").write(re.sub(r"(?m)^guid: [0-9a-f]{32}", "guid: " + GUID, open(META_TPL, encoding="utf8").read(), count=1))
print("instances", len(pis), "stripped", len(stripped), "children listed", sum(1 for k in kids if k in kept_tr), "->", OUT)
