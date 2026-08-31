# MRM-70 — Biome vegetation distribution, measured revision

Written 2026-08-30, branch `mrm-70`. Supersedes the spacing and weight numbers in
`Docs/Design/Island-Terrain-Reference/Vibe/GPT biome analysis.md` (the "GPT report"), which stays
useful for **art intent** — the biome descriptions, the tonal logic, the acid-colour ceilings, the
story-prop lists — but whose **numbers cannot be used**, because it was written from screenshots
with no access to the prefabs.

This document replaces those numbers with values measured out of the actual project.

---

## 1. How the measurements were taken

Every prefab was instantiated in the Editor and measured from the **renderer bounds of its `Visual`
child** — the real mesh, after Carlos's hand-corrected scale and pivot pass. 190 prefabs across four
folders.

Three things follow from that, and they drive everything below:

**Colliders are not the size reference.** The MRM-70 batch gave every prop a capsule spanning the
full mesh height from below-ground root to canopy top, so collider height is systematically wrong as
a size signal — `AP_Tree_04_GTree01_01_SM_2` has a 25.6 m capsule and a 34.0 m mesh. Collider data
is used in this document for exactly one thing: **how wide the prop blocks walking**.

**Pivots are sunk.** 68 of 190 meshes sit below Y=0. For trees it's 1-8%, ordinary root sink. For
**rocks and stumps it is 30-50%** — `GoblinRock01` is a 3.23 m mesh with only **1.62 m above
ground**, `TurtleRock04` is 2.30 m with **1.15 m** showing. So "visible height" is the number that
matters for silhouette and cover, and it is used throughout.

**The player is 1.8 m tall, 0.8 m wide** (`Player.prefab`, CapsuleCollider h=1.8 r=0.4). Every
"can I walk through / hide behind / see over" judgement in this document is against that capsule.

**The island is smaller than any existing document says.** The live terrain is **1024 × 1024 m**
with **64.2 ha of land above sea level**, not the 4000 m or 4103 × 7085 m terrains referenced
elsewhere. This is measured off the open scene — see §6.1, and read it before trusting any area or
absolute instance count anywhere in the project.

---

## 2. What the GPT report got wrong, and why

Five systematic errors, all traceable to not knowing the prefab sizes.

### 2.1 The trees are 2-4× bigger than it assumed

It read the forest firs as ordinary 8-12 m trees and spaced them at 4.5-7 m. They are **28-42 m tall
with 12-20 m crowns**. At 4.5 m spacing a 20 m crown overlaps roughly twenty of its neighbours.
That is unreadable visually and pointless to render.

The Eerie Forest table is the worst case. It treats `AP_GraveKeepers_B01/B03/B07` as small dead
saplings at 3.5-7 m. They are **18.6, 25.0 and 20.8 m wide**. And `AP_S_Tree_01`, given a 4-7 m
spacing as a "rooted dead-tree variation", is **30.1 m wide and 33.8 m tall** — the second-largest
asset in the project. It is a hero, one per district.

### 2.2 Half the "flowers" are multi-metre patch meshes

The report spaces ground cover at 0.45-1.5 m throughout. That is right for the small
`*_LOD` flowers (`VioletDaisy_LOD` is 0.41 m) and badly wrong for the rest:

| Asset | GPT spacing | Actual footprint | Why it matters |
|---|---|---:|---|
| `AP_Neutral_Foliage_A_Weed01_SM` | 0.45-0.90 m | **3.25 m**, 1.5 m tall | At 0.45 m this is a solid opaque wall at chest height. It is 15% of the Glade budget. |
| `AP_flower_001A` | 1.0-2.0 m | **4.94 m** | A five-metre patch mesh, not a flower. |
| `AP_Mushroom_B01` / `C01` | 1.5-3.0 m | **4.55 / 4.33 m** | Fungal patches. |
| `AP_plant_001_13` | 1.5-3.0 m | **4.08 m** | A shrub. |
| `AP_GR_003GR_002_D` | 1.2-2.5 m | **3.09 m** | Already-clustered wildflower patch. |
| `African_violet_LOD` | 0.7-1.5 m | **2.09 m** | |
| `AP_Nest_B01` | 5.0-9.0 m | **7.72 m** | Described as "stick and twig nests". It is nearly 8 m across. |

Conversely the genuinely small ones are **over**-spaced: `VioletDaisy_LOD` and `YellowDaisy_LOD`
(0.41 m) are given 1.5-3.5 m, `Blue Aster_LOD` (0.52 m) is given 1.2-2.4 m. These are exactly the
assets that should go dense, as you said.

### 2.3 Triangle cost was invisible to it, and it inverted the right answer

The report builds Eerie Forest on the `GraveKeepers` family at ~30% combined weight and 3-7 m
spacing. Those are the **most expensive meshes in the project**:

- `AP_Tree_Curse_H01_2` — 27,975 tris
- `AP_GraveKeepers_B02` — 27,036 tris
- `AP_GraveKeepers_B01` — 20,824 tris
- `AP_GraveKeepers_B04` — 17,378 tris **for a 6 m tree** (2,863 tris per metre, worst ratio we own)

Meanwhile the cheap dead trees it treats as secondary are extraordinary value:
`AP_DeadTree02` is **725 tris for a 12.8 m tree**, `AP_Tree_DeadTree01_SM` is 1,962 tris for 13.6 m.

So the Eerie and Heretic tables below are **inverted from the GPT version**: cheap dead trees carry
the base density, and the GraveKeepers/Curse family drops to a sparse layer *on top* of it. Same
look, roughly a tenth of the geometry. This matters — the WebGL platform switch happened because the
island measured 21,946 draw calls, and that history is worth not repeating.

**To be explicit, because it is easy to misread as a cut: nothing was removed.** All 154 curated
prefabs appear in the tables below, and 8 more were added. The expensive claw and ritual trees keep
meaningful presence — 12-22 of each GraveKeepers across Eerie Forest, 10-19 of each Curse tree in
Heretic — they simply stop being the thing that carries the wall of trunks.

### 2.4 Mountain is built on rocks we do not have

The report's Mountain gives `AP_M6_Rock_FieldStoneStone05_SM` a 10% weight as the "primary granite
mass" at 4-8 m spacing. It is **1.34 m across and 0.68 m above ground** — a knee-high stone.

Measured across the whole set, the tallest rock we own is `AP_M6_Rock_SeashoreWallStone01_SM` at
**4.03 m visible**. After that: `CemeteryRock02` 2.55 m, `RF_Boulder5` 2.46 m, `GoblinRock02`
1.75 m, `GoblinRock01` 1.62 m. There is no fractured-granite mass in the library at all.

**Recommendation:** Mountain's rock reads from **terrain sculpting and Gaia rock stamps**, with
these props as dressing on top. Trying to build cliffs out of 1-4 m boulders will not work, and no
amount of weight tuning fixes it. Flagging rather than silently substituting — same as the birch
call in the original strategy doc.

### 2.5 Several assets are miscategorised outright

| Asset | GPT called it | Actually |
|---|---|---|
| `AP_Plant_003_07` | "Isolated low autumn form", 4-7 m spacing | A **10.1 m tree** with a collider |
| `AP_Tree_Heretic_B05` | Hero, 20-35 m spacing | A **1.7 m stake** — should be dense set dressing |
| `AP_Tree_Heretic_B03` | Hero, 18-30 m spacing | A **3.9 m stake cluster** |
| `AP_Tree_Heretic_A01_2` | "Sculptural heretic tree", 25-40 m | Only **8.0 m** — smaller than a normal forest tree |
| `AP_M6_Tree_MonsterTreeBark…` | "Monstrous root silhouette", 30-50 m | **9.9 m** — smaller than the average canopy tree |
| `AP_Tree_Heretic_D03` | "Low root altar frame", 12-22 m | A **15.0 m** ground formation — nearly touching at that spacing |

---

## 3. The replacement system: spacing derived from footprint

The GPT report gives absolute metres per asset, which is why it fails the moment an asset is not the
size it assumed. This document instead gives every asset a **density tier**, and derives spacing
from the measured footprint:

> **spacing = k × footprint**, where footprint = max(bounds X, bounds Z)

| Tier | k range | Meaning | Typical use |
|:--:|---|---|---|
| **D** | 0.60 – 0.90 | Dense — crowns touch and interlock | Ground cover, sub-canopy backbone |
| **M** | 1.00 – 1.60 | Medium — crowns just clear each other | Canopy, shrubs |
| **S** | 2.00 – 3.50 | Sparse — clear gaps between | Emergents, accents, rock |
| **A** | 4.50 – 8.00 | Accent — rare punctuation | Expensive meshes, survivors |
| **H** | — | Hand-placed, capped at 1 per authored node | Heroes |

Floors apply so tiny meshes don't collapse to sub-half-metre spacing: 0.5 m for D, 0.8 m for M,
1.5 m for S, 3.0 m for A.

Feed the two ends of the range straight into `m_locationIncrementMin` / `m_locationIncrementMax`.

**Converting spacing to counts.** For Poisson-disc scatter at mean spacing `d`:

> **instances per hectare ≈ 8000 / d²**

That is the relationship used to set every stratum target below, and it is the knob to turn when we
start tuning against a real build. A 12 m canopy spacing gives ~55 trees/ha; a 3 m ground-cover
spacing gives ~890/ha.

### Why this still produces a dense forest

Density does **not** come from crown overlap — it comes from **stacking strata**. Each biome below
is split into 4-7 layers, each scattered as its own Gaia pass at its own spacing, and the totals
add. Forest reaches ~1,345 instances/ha with no layer interpenetrating itself.

What actually makes the player feel buried is trunk count at eye level plus understory plus fog, not
canopy density. With 30-40 m trees, a 12-15 m trunk spacing still puts 5-8 trunks across any 40 m
sightline.

### Traversal

Player half-width is 0.4 m. For a **2.4-3.0 m clear primary route**, a collidable prop's centre must
sit at least `1.5 m + its blocking radius` off the centreline. For forest firs (blocking radius
0.7-1.4 m) that is 2.2-2.9 m — comfortable.

**Trees are not the traversal risk.** The risk is a short list of props whose collider is far wider
than they look — see Appendix A. `RF_Log2` is 0.68 m tall and blocks 5.0 m; `BrokenTree01` is 0.44 m
tall and blocks 3.8 m. The player will read those as steppable and get hard-stopped.

---

### 3.5 Slope, altitude, and the difference between "where" and "which way up"

Two settings get confused with each other, so to be precise:

- **Slope filter** — *where a species is allowed to spawn.* "Don't put a pine on ground steeper than
  30°." It has nothing to do with the tree's angle.
- **Align to normal** (Gaia: *rotate to slope*) — *whether the placed instance tilts to match the
  ground.* This is the one that makes a tree lean.

Almost always you want **slope filter ON, align-to-normal OFF for anything that grows**. Real trees
grow vertically toward light regardless of the hillside; a forest of trees leaning perpendicular to
a slope reads as broken immediately. Rocks, logs and ground clutter are the opposite — they *rest*
on the surface, so they should align.

**This is exactly what the buried roots are for.** A vertical tree on a 20° slope leaves a visible
gap on the uphill side of the trunk unless its base sits below the surface. The 1-8% root sink on
the tree meshes (Appendix B) is what lets align-to-normal stay off without floating. The instinct
was right, and it is load-bearing for this whole approach — do not "fix" the buried pivots.

**The island has more slope than it looks.** Measured on the live terrain, land above sea level:

| Slope | Share of land | Cumulative |
|---|---:|---:|
| 0-9° | 24.9% | 24.9% |
| 9-18° | 40.0% | 64.9% |
| 18-27° | 21.6% | 86.5% |
| 27-36° | 7.7% | 94.2% |
| 36-45° | 3.4% | 97.6% |
| 45-54° | 1.6% | 99.2% |
| 54-72° | 0.8% | 100% |

Only a quarter of the island is genuinely flat, and **a third of it is over 18°**. A slope filter is
therefore not a formality — set the tree cap too low and a third of the island spawns nothing.

#### Slope rules by functional class

Apply per spawn rule. These are Gaia `ImageMask` values (`m_slopeMin` / `m_slopeMax`).

| Class | Slope min | Slope max | Align to normal | Land available | Reasoning |
|---|---:|---:|:--:|---:|---|
| Canopy tree (strata A) | 0° | **30°** | **Off** | 94% | Grows vertical; steep ground has thin soil |
| Sub-canopy tree (strata B) | 0° | **35°** | **Off** | 96% | Slightly hardier, clings further up |
| Small tree / shrub | 0° | **40°** | Off | 98% | |
| Fern / ground cover | 0° | **45°** | 25% partial | 99% | Slight lean reads as growth toward light |
| Grass / flower | 0° | **40°** | 25% partial | 98% | |
| Rock / boulder | 0° | **60°** | **On, 100%** | 100% | Must sit flush or it floats |
| Log / driftwood | 0° | **22°** | **On, 100%** | ~80% | A log on a 40° slope would have rolled |
| Root formation / flat decal | 0° | **18°** | **On, 100%** | 65% | Large flat meshes tear through steep ground |
| Hero trees | — | — | Off | hand | Place on chosen ground |

The tight caps on **logs and root formations** are the important ones. Those are the widest flat
meshes we own (`RF_Log3` 10.1 m, `AP_Tree_Heretic_D03` 15.0 m) and on steep ground they visibly
intersect the terrain at one end and float at the other.

#### Altitude bands

The island runs from sea level to **~60 m above it**, so altitude is a coarse tool here, not a fine
one. Distribution of land by height above sea:

| Height above sea | Share of land |
|---|---:|
| 0-8 m | 21.5% |
| 8-16 m | 20.8% |
| 16-24 m | 22.9% |
| 24-32 m | 19.7% |
| 32-40 m | 10.9% |
| 40-48 m | 3.8% |
| 48-56 m | 0.2% |

Use Gaia's **`m_seaLevelRelativeHeightMin` / `m_seaLevelRelativeHeightMax`** with
`m_heightMaskType = RelativeToSeaLevel`. That keeps the rules correct if the Crest water level ever
moves off Y=8.

| Biome | Height min | Height max | Note |
|---|---:|---:|---|
| Beach | 0 m | **5 m** | The wet band only; hard cut at the treeline |
| Forest | 3 m | 45 m | Effectively unrestricted |
| Autumn Forest | 3 m | 40 m | |
| Eerie Forest | 5 m | 45 m | |
| Heretic Forest | 5 m | 45 m | |
| Fountain | 5 m | 30 m | Sits in a bowl |
| Flak Tower | 8 m | 35 m | Open plain, not a summit |
| Glade | 12 m | 45 m | A hill, so keep it off the lowlands |
| Mountain | **30 m** | 60 m | See the warning below |

> **Mountain is the weak biome, and altitude is why.** Only **4% of the island's land sits above
> 40 m**, and the highest point is ~60 m above sea. Combined with §2.4 — the tallest rock we own is
> 4.03 m — "Mountain" on this terrain is currently a 60 m hill with knee-high rocks on it. It needs
> a terrain decision before vegetation can save it. Raising the mountain in Gaia's Stamper is
> probably cheaper than any asset fix.

#### If the filters make it look worse, turn them off — but not for everything

These constraints are **not baked in**. Every one is an `ImageMask` on a spawn rule, so relaxing
them is a per-rule edit: set `m_slopeMin = 0` / `m_slopeMax = 90`, widen the height range, set
`m_strength = 0`, or delete the mask. Fully reversible, and a reasonable thing to do if the first
generation leaves bald patches or the island reads emptier than intended.

**Realism is not the reason to keep them.** Coverage and readability are. So the honest split:

| Class | Safe to relax or remove? | Why |
|---|---|---|
| **Grass, flowers, ferns, ground cover** | **Yes, remove entirely** | No colliders, tiny meshes. They look fine on any slope and removing the filter is the fastest way to kill bald patches |
| **Canopy and sub-canopy trees** | **Yes, raise to 45-60°** | Vertical placement plus buried roots (§3.5) means a tree on a 45° slope still reads correctly. This is the cheapest way to fill the island |
| **Shrubs, saplings, small trees** | Yes, raise to 50° | Same reasoning |
| **Boulders and rocks** | Already at 60° | Align-to-normal does the work |
| ⚠️ **Logs and driftwood** | **Keep the 22° cap** | `RF_Log1/2/3`, `AP_Tree_Break_02/03_SM`, `BrokenTree01` — 5-10 m long and near-flat. On steep ground one end buries and the other floats |
| ⚠️ **Root formations and flat decals** | **Keep the 18° cap** | `AP_Tree_Heretic_D02/D03/D03_02` (12-15 m across), `AP_Nest_B01` (7.7 m), `AP_Tree_Dry_N02`. These are the widest flat meshes we own and they tear through sloped terrain visibly |

So: roughly a dozen assets genuinely need the slope cap, and everything else can be opened up
freely. **If the first pass looks sparse or patchy, relax the trees and ground cover first** — that
is where the coverage is — and leave the logs and root formations alone.

One thing that is *not* a slope filter and should not be changed to compensate: **align-to-normal
stays off for trees**. Tilting trees to match the hillside is what actually makes steep-ground
vegetation look broken, not the fact that it is on a slope.

### 3.6 Clustering — how to stop the scatter looking uniform

Poisson-disc scatter is *evenly* random, which reads as artificial. Real vegetation grows in
same-species pockets with ragged edges. Gaia does this with a **noise mask on the spawn rule** —
these fields exist on `Gaia.SpawnRule` and are the mechanism to use:

| Field | Set to | Effect |
|---|---|---|
| `m_noiseMask` | `Perlin` (or `Billow` for harder-edged pockets) | Enables clustering. `NoiseType` = None / Perlin / Billow / Ridged |
| `m_noiseZoom` | **3-5× the species' mean spacing**, in metres | Pocket size. A tree at 12 m spacing wants ~40-60 |
| `m_noiseStrength` | 0.6-0.8 | How strongly the mask gates spawning. 1.0 gives bald patches |
| `m_noiseMaskSeed` | **A different value per species** | Critical — same seed makes every species clump in the same place |
| `m_noiseMaskOctaves` | 3 | Edge raggedness |
| `m_noiseMaskPersistence` | 0.5 | |
| `m_noiseInvert` | `true` for the *contrast* species | See below |

**Three patterns worth knowing:**

1. **Species pockets.** Every species in strata A/B/C gets its own `m_noiseMaskSeed`. That alone
   turns an even scatter into 3-7 tree clumps and satisfies "cluster by species" throughout §7.

2. **The autumn grove pierce.** Autumn Forest asks for 1-2 dark pines inside each warm grove. Give
   the conifer stratum the **same seed** as the autumn stratum with `m_noiseInvert = true`, so pines
   land in the gaps between orange groves rather than at random.

3. **Combat pockets.** The 5-8 m clearings every 25-40 m come free from noise clustering at
   `m_noiseStrength` ≈ 0.75 — the mask's low areas become the clearings. No separate pass needed.

Spacing stays as specified in §7; the noise mask only decides *where* the Poisson field is allowed
to place, never how close two instances may get.

---

## 4. Grass and ground detail — deliberately outside the budget

You were right that grass shouldn't be in the per-biome prefab weights, and there's a stronger
reason than proportion: **in the previous build it wasn't prefabs at all.** The grass in your
screenshot was a **Unity terrain detail layer**.

`Island_Original_TerrainData_Backup.asset` still carries the whole thing — **27 detail prototypes**
across 8 terrain layers: 12 `GFF_*` texture billboards plus `TSA_*` mesh details, with per-prototype
density from 0.2 to 6.0 and per-biome colour tinting. That is the `BiomeGrassSetup` pass described
in `mrm70-biome-vegetation-strategy.md` §6b.

**The current live terrain has none of it.** `Island_TerrainData.asset` reads
`detailResolution = 0`, 0 terrain layers, 0 detail prototypes, 0 trees. All 27 prototypes and the
splatmap were lost in the Gaia regeneration. Rebuilding that detail pass is a prerequisite for
biome vegetation, because **terrain layers are the biome masks** — the spawner rules key off them.

### The vegetation assets that are *not* in the prefab folder

All 154 assets the GPT report names do exist. **36 more exist that it never mentions**, because they
were never in the screenshots you gave it:

| Set | Count | Location | What they are |
|---|---:|---|---|
| `GFF_Grass01-02`, `GFF_GrassFlower01-10` | 12 | `Prefabs/World/Vegetation/GrassFlowers/` | 0.5 m cross-quad billboards, **4 tris each**. The green grass in your screenshot. Source textures in `Art/Environment/Vegetation/GrassFlowers/Textures/`. |
| `TSA_Grass_A-D`, `TSA_GrassDry_A-C`, `TSA_Fern_A-C`, `TSA_Bush_A/B`, `TSA_BushDry_A/B`, `TSA_Heather_A/B`, `TSA_Plant_A-D` | 20 | `Prefabs/World/Vegetation/TerrainSampleAssets/` | 0.2-1.3 m mesh details, 61-1,268 tris. Unity Terrain Sample Assets. |
| `RF_Fern1`, `RF_Fern2` | 2 | `Prefabs/World/Vegetation/RetroRealism/` | 0.73 / 1.34 m ferns, **9 and 33 tris**. Absurdly cheap, and simply overlooked. |
| `RF_Sapling1`, `RF_Sapling2` | 2 | `Prefabs/World/Vegetation/RetroRealism/` | 2.4 / 5.4 m young trees. Fill the gap between shrub and canopy. |

There are also 16 `TL_TSA_Ground_*` and `TL_GFF_GroundGrass01` terrain layers already built under
`Art/Environment/Terrain/`.

**How they're used below.** The 12 `GFF_*` and most `TSA_*` go back to the **terrain detail layer**,
where they cost almost nothing and can carpet the whole island — they are not in any biome's
100-point budget. The four genuinely useful outliers (`RF_Fern1/2`, `RF_Sapling1/2`) and a few
`TSA_*` that read as distinct props (`TSA_Heather_A/B`, `TSA_GrassDry_C`, `TSA_BushDry_B`) **are**
folded into the tables below.

### Suggested detail-layer densities

Carried over from the old terrain, which was tuned and looked right:

| Biome | Detail density | Palette |
|---|---|---|
| Forest | 5.7-6.0 (`GFF_Grass01/02` lead) | Deep green, mossy |
| Autumn Forest | 2.7-5.1, `GrassFlower02/07` | Dry gold and rust |
| Glade | 6.0 across the board | Wind-pale green |
| Fountain | 4.5-6.0 + flower cards | Blue-white |
| Flak Tower | 4.2-5.7 dry | Golden dry grass |
| Eerie / Heretic | 0.2-0.5 only | Near-black, dead |
| Mountain | 0.2-0.4 in pockets | Grey-green scrub |
| Beach | 0 on sand | — |

---

## 5. Reading the tables

- **%** is the asset's share **within its stratum**, not the whole biome. Each stratum is a separate
  Gaia spawn pass, so this is directly the spawn weight.
- **Tier** is the density tier from §3. Retune a biome by changing tiers, not by editing metres.
- **Spacing** is computed, `k × footprint`. Feed both ends to the increment min/max.
- **Foot** is max(X, Z) of the visual mesh — the number the spacing derives from.
- **Vis H** is height above Y=0, after burial.
- **Blocks** is collider blocking diameter. **—** means it does not block the player at all, which
  is your licence to raise its count freely.
- **≈ Count** is the resulting instance count for that asset across the biome, computed as the
  *lower* of two constraints: what the stratum target asks for, and what the minimum-spacing rule
  physically permits (`8000 / spacing²` per hectare). A **⚠** means spacing is the binding one and
  the spawner will underfill the weight. As shipped there are none — every row is density-limited.
- Stratum targets in **instances/ha** are the thing to tune first against a real build.

> **Why the count column exists.** A first pass of this document put the GraveKeepers and Curse
> trees on the accent tier, whose 67-200 m minimum spacing silently overrode the density they were
> given — `AP_GraveKeepers_B03_2` came out at 3 instances in the whole of Eerie Forest. The two
> knobs can contradict each other, and the contradiction is invisible unless the resulting count is
> printed. `biomes.py` now asserts on it.

