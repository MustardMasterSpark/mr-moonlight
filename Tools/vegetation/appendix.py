# -*- coding: utf-8 -*-
exec(open('gen.py', encoding='utf-8').read())

rows = list(R.values())
for r in rows:
    r['_fp'] = float(r['footprint']); r['_h'] = float(r['visH'])
    r['_vis'] = float(r['visibleH']); r['_min'] = float(r['minY'])
    r['_br'] = float(r['blockR']); r['_t'] = int(r['tris'])

print("## Appendix A — trip-wall risk: props that block far wider than they look\n")
print("Blocking diameter divided by visible height. Anything over ~3 reads to the player as an")
print("invisible wall: a knee-high log that hard-stops you. These need either a lowered/removed")
print("collider, a vault, or hand placement well off the walking line.\n")
print("| Asset | Visible H (m) | Block dia (m) | Ratio | Verdict |")
print("|---|---:|---:|---:|---|")
tw = [r for r in rows if r['_br'] > 0 and r['_vis'] > 0 and (r['_br']*2)/r['_vis'] > 2.5]
tw.sort(key=lambda r: -(r['_br']*2)/r['_vis'])
for r in tw:
    ratio = (r['_br']*2)/r['_vis']
    v = "Remove collider or make it vaultable" if ratio > 5 else "Vault, or keep off traversal lines"
    print("| `%s` | %.2f | %.2f | **%.1f×** | %s |" % (r['name'], r['_vis'], r['_br']*2, ratio, v))

print("\n## Appendix B — burial: meshes sunk below the pivot\n")
print("Only the ones where it changes a placement decision (>15%% of the mesh below Y=0).")
print("Trees are all 1-8%% (normal root sink). The **rocks and stumps are 30-50%% buried**, which")
print("is why none of them give standing cover.\n")
print("| Asset | Bounds H (m) | min.Y (m) | Visible H (m) | % buried |")
print("|---|---:|---:|---:|---:|")
bu = [r for r in rows if r['_h'] > 0 and -r['_min']/r['_h'] > 0.15]
bu.sort(key=lambda r: r['_min']/r['_h'])
for r in bu:
    print("| `%s` | %.2f | %.2f | **%.2f** | %.0f%% |" % (r['name'], r['_h'], r['_min'], r['_vis'], -r['_min']/r['_h']*100))

print("\n## Appendix C — cover value against a 1.8 m player\n")
print("Collidable props only, trees under 8 m included. This is the honest answer to")
print("\"what can the player actually hide behind\".\n")
print("| Asset | Visible H (m) | Block dia (m) | Cover class |")
print("|---|---:|---:|---|")
cv = []
for r in rows:
    if r['_br'] <= 0: continue
    if ('Tree' in r['name'] or 'tree' in r['name']) and r['_h'] > 8: continue
    cv.append(r)
cv.sort(key=lambda r: -r['_vis'])
for r in cv:
    v = r['_vis']
    t = "**Standing cover**" if v >= 1.8 else ("Crouch cover" if v >= 1.0 else ("Trip / ankle" if v >= 0.4 else "Decal"))
    print("| `%s` | %.2f | %.2f | %s |" % (r['name'], v, r['_br']*2, t))

print("\n## Appendix D — triangle cost tiers\n")
print("**Most expensive meshes** (these must stay rare — see the Eerie/Heretic notes):\n")
print("| Asset | Tris | Visible H (m) | Tris per metre |")
print("|---|---:|---:|---:|")
ex = [r for r in rows if r['_vis'] > 3]
ex.sort(key=lambda r: -r['_t'])
for r in ex[:16]:
    print("| `%s` | %s | %.1f | %s |" % (r['name'], "{:,}".format(r['_t']), r['_vis'], "{:,.0f}".format(r['_t']/r['_vis'])))
print("\n**Cheapest large trees** — these are what carry base density:\n")
print("| Asset | Tris | Visible H (m) | Tris per metre |")
print("|---|---:|---:|---:|")
ch = [r for r in rows if r['_vis'] > 9]
ch.sort(key=lambda r: r['_t'])
for r in ch[:14]:
    print("| `%s` | %s | %.1f | %s |" % (r['name'], "{:,}".format(r['_t']), r['_vis'], "{:,.0f}".format(r['_t']/r['_vis'])))

print("\n## Appendix E — full measured inventory\n")
print("Every prefab, measured from the `Visual` child's renderer bounds. **Foot** = max(X, Z),")
print("the number every spacing value in this document is derived from.\n")
print("| Asset | Folder | Foot (m) | Bounds H | Visible H | Block dia | Tris |")
print("|---|---|---:|---:|---:|---:|---:|")
al = sorted(rows, key=lambda r: (r['folder'], -r['_vis']))
for r in al:
    blk = "%.2f" % (r['_br']*2) if r['_br'] > 0 else "—"
    print("| `%s` | %s | %.2f | %.2f | %.2f | %s | %s |"
          % (r['name'], r['folder'], r['_fp'], r['_h'], r['_vis'], blk, "{:,}".format(r['_t'])))
