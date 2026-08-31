---

## 8. Totals and the budget sanity check

| Biome | Instances/ha | Triangles/ha | Area (ha) | Instances |
|---|---:|---:|---:|---:|
| Forest | 1,345 | 656 k | 16.0 | 21,520 |
| Autumn Forest | 1,165 | 441 k | 18.0 | 20,970 |
| Beach | 194 | 103 k | 5.0 | 970 |
| Eerie Forest | 610 | 814 k | 5.0 | 3,052 |
| Heretic Forest | 499 | 913 k | 4.0 | 1,995 |
| Flak Tower | 2,212 | 856 k | 3.5 | 7,742 |
| Fountain | 3,025 | 1,136 k | 1.0 | 3,025 |
| Glade | 3,513 | 1,325 k | 0.5 | 1,756 |
| Mountain | 468 | 282 k | 5.0 | 2,340 |
| **Total** | | | **~57.5 ha** | **~63,400** |

Areas are the placeholder allocation from §6.2, re-anchored to the real 64.2 ha of land. **The
instances/ha and triangles/ha columns are the real outputs** and survive any change to the biome
map; the instance column moves with the areas.

Three things worth noticing:

**Instance count is not the problem; batching is.** ~63 k instances sounds large next to the
previous build's 17,350 trees, but ~75% of it is non-colliding ground cover of 9-300 tris that GPU
instancing handles in a handful of draw calls. Terrain trees and terrain details both instance
automatically. The number to watch is **unique prototypes visible at once**, not instances.

**Eerie and Heretic are the expensive biomes per hectare** despite being the sparsest, entirely
because of the GraveKeepers/Curse meshes. They are already at sparse tier. If a build shows a
problem, cut those two strata before touching anything else.

**The Fountain and Glade look alarming per hectare** — 1.1-1.3 M triangles — but they are 1.0 and
0.5 ha, and almost all of it is sub-metre flower meshes. They are the two smallest biomes in the
game and the cost is bounded.

## 9. Open items before the Gaia pass

1. **Define the biome map — the hard blocker.** §6.3. Nothing in §7 can spawn without it, and it
   is Carlos's call. Also needs the nine landmark positions and the player spawn re-anchored to the
   -512...+512 frame.

2. **Rebuild the terrain detail layer.** The current `Island_TerrainData` has 0 layers,
   0 details, `detailResolution = 0`. Terrain layers are the biome masks, so nothing in this
   document can be spawned until they exist. The 27-prototype configuration is recoverable from
   `Island_Original_TerrainData_Backup.asset`.

3. **Re-anchor the biome regions.** §3 of the strategy doc is invalid against the current terrain.
   Needed before any absolute instance count means anything.

4. **Mountain rock — decision needed.** As §2.4 says, we own nothing above 4 m. Terrain sculpting
   plus Gaia stamps, or a rock asset, or accept a low-relief mountain. Your call.

5. **Trip-wall colliders (Appendix A).** Eight props block 2.5-8.6× wider than their visible height.
   Worth fixing at the prefab before scattering thousands of them.

6. **Sanity-check the GraveKeepers colliders.** All six landed on exactly `radius = 2.00`, which
   looks like a clamp in the batch script rather than measured trunk geometry. Their meshes are
   6-27 m wide, so a uniform value across all six is suspicious.
   `AP_M6_Tree_MonsterTreeBark_SM_PHJ_2` is the clearest case: 6.7 m blocking diameter on a 9.9 m
   tree, 68% of its own footprint.

7. **All 154 curated prefabs are in.** Verified programmatically: zero dropped from the GPT list,
   8 added (`RF_Fern1/2`, `RF_Sapling1/2`, `TSA_Heather_A/B`, `TSA_GrassDry_C`, `TSA_BushDry_B`).
   The 28 remaining project prefabs are the `GFF_*`/`TSA_*` grass set, which belongs on the terrain
   detail layer rather than in prefab scatter — see §4.

8. **Tier tuning is expected.** Every number here is a defensible starting point, not a survey. The
   intended workflow is: spawn a biome, walk it, change **tiers** (not metres), respawn.

