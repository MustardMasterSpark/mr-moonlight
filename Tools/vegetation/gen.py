# -*- coding: utf-8 -*-
import csv, io, sys
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
R = {r['name']: r for r in csv.DictReader(open('veg_sizes.csv')) if r['name']}

def fp(n):  return float(R[n]['footprint'])
def vis(n): return float(R[n]['visibleH'])
def br(n):  return float(R[n]['blockR'])
def tr(n):  return int(R[n]['tris'])

# tier -> (k_min, k_max, absolute floor)
TIER = {'D': (0.60, 0.90, 0.5),
        'M': (1.00, 1.60, 0.8),
        'S': (2.00, 3.50, 1.5),
        'A': (4.50, 8.00, 3.0),
        'H': (None, None, None)}

def bounds(n, t):
    kmin, kmax, fl = TIER[t]
    f = fp(n)
    return max(kmin * f, fl), max(kmax * f, fl * 1.6)

def spacing(n, t):
    if t == 'H':
        return 'hand'
    a, b = bounds(n, t)
    return "%.1f-%.1f" % (a, b)

# Poisson-disc: achievable instances per hectare at mean centre-to-centre spacing d
def per_ha(d):
    return 8000.0 / (d * d)

CONFLICTS = []

def table(sname, dens, items, area, biome):
    """dens = stratum target instances/ha; area = biome area in ha."""
    out = []
    out.append("**%s** — target **%g instances/ha**\n" % (sname, dens))
    out.append("| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |")
    out.append("|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|")
    for n, pct, t, intent in items:
        blk = "%.1f" % (br(n) * 2) if br(n) > 0 else "—"
        want = dens * pct / 100.0
        if t == 'H':
            cnt = "1/node"
        else:
            a, b = bounds(n, t)
            cap = per_ha((a + b) / 2.0)
            if want > cap * 1.15:
                CONFLICTS.append((biome, sname, n, t, want, cap))
                cnt = "**%.0f** ⚠" % (cap * area)
            else:
                cnt = "%.0f" % (want * area)
        out.append("| `%s` | %g | %s | %s | %.1f | %.1f | %s | %s | %s | %s |"
                   % (n, pct, t, spacing(n, t), fp(n), vis(n), blk,
                      "{:,}".format(tr(n)), cnt, intent))
    return "\n".join(out) + "\n"
