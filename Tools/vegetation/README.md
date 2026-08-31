# Vegetation distribution generator (MRM-70)

Regenerates `Docs/mrm70-biome-distribution-measured.md` from **measured prefab geometry**, so the
biome spacing numbers cannot drift out of sync with the actual assets.

Run this whenever a vegetation prefab is rescaled, re-pivoted, or added.

## Step 1 — re-measure the prefabs (Unity)

Run `measure_prefabs.cs` through the UnityMCP `execute_code` tool (it is a method body, not a
file to compile). It instantiates every prefab under the four vegetation folders and writes
`veg_sizes.csv` here.

It records, per prefab:

| Column | Meaning |
|---|---|
| `visW` / `visH` / `visD` | Renderer bounds of the `Visual` child — the **real mesh**, not the collider |
| `footprint` | `max(visW, visD)` — every spacing value derives from this |
| `minY` | Lowest point of the mesh. Negative = sunk below the pivot |
| `visibleH` | Height actually above ground, i.e. `visH + minY` |
| `blockR` | Widest horizontal collider half-extent — how far it blocks walking |
| `colType` | capsule / box / mesh / NONE |
| `tris` | Triangle count |

**Colliders are deliberately not used for size.** The MRM-70 batch gave every prop a capsule
spanning the full mesh height including below-ground roots, so collider height is not a size signal.

## Step 2 — regenerate the document

```sh
cd Tools/vegetation
sh build.sh
```

That regenerates the whole document and re-runs the self-check. It prints a warning if any
spacing rule has started contradicting its stratum target.

`biomes.py` asserts that every stratum's weights sum to 100, so an editing mistake fails loudly
instead of producing a quietly wrong table.

## Files

| File | What it is |
|---|---|
| `measure_prefabs.cs` | UnityMCP snippet that produces `veg_sizes.csv` |
| `veg_sizes.csv` | Measured geometry, 190 prefabs. Regenerate; do not hand-edit |
| `gen.py` | Spacing formula (`k × footprint`), tier table, markdown row builder |
| `biomes.py` | The 9 biome specs — **this is the file to edit when retuning** |
| `appendix.py` | Trip-wall, burial, cover, triangle-cost and full-inventory appendices |
| `build.sh` | Assembles the document and runs the self-check |
| `head.md` | Prose §1-5 — measurement basis, GPT corrections, spacing/slope/clustering, grass layer |
| `sec_boundaries.md` | §6 — terrain facts and the biome-map blocker |
| `tail.md` | §8-9 — totals and open items |
| `sec_gaia.md` | §10 — Gaia field-by-field execution guide |

## Retuning

Change **tiers**, not metres. `D` / `M` / `S` / `A` / `H` in `biomes.py` map to `k` multipliers in
`gen.py`; spacing recomputes from the measured footprint. Stratum instance-per-hectare targets are
the other knob — `instances/ha ≈ 8000 / spacing²`.
