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

---

## 6. Terrain facts and biome boundaries — **the open blocker**

Everything in §7 is "what to place and how far apart". It cannot run until the island is divided
into biomes. That division **does not exist yet** and is Carlos's to define.

### 6.1 The terrain, as actually measured

Read off the live `Island.unity` scene, not from any document:

| Property | Value |
|---|---|
| Terrain object | `Terrain_0_0-20260829 - 035828` |
| Terrain data | `Assets/Gaia User Data/Sessions/GS-20260829 - 011148/Terrain Data/Terrain_0_0-20260829 - 035828.asset` |
| Size | **1024 × 1024 m**, Y range configured to 1024 m |
| World origin | (−512, 0, −512) — so world X and Z both run **−512 … +512** |
| Actual height used | 0 – **68.4 m** |
| Sea level (Crest) | **Y = 8** |
| Land above sea | **64.2 ha** (61.2% of the terrain) |
| Terrain layers | **0** |
| Detail prototypes | **0** |
| Tree prototypes / instances | **0 / 0** |
| Gaia spawner in scene | `World Designer`, 7 rules |

> **Two traps for anyone picking this up.**
>
> `Assets/_Project/Art/Environment/Terrain/Island_TerrainData.asset` is **not the live terrain.**
> It is 4000 × 4000 m and essentially flat (max height 20 m, 100% of it under 9°). The scene uses
> the Gaia session asset above. Editing the `_Project` one changes nothing you can see.
>
> The old `Island_Original_TerrainData_Backup.asset` is 4103 × 7085 m. **Every biome coordinate in
> `mrm70-biome-vegetation-strategy.md` §3 refers to that terrain** and is off by roughly 4-7× in
> both axes. Those numbers cannot be scaled across; the shape is different, not just the size.

### 6.2 What this changed in this document

The first draft of §7 used the old survey's biome areas, which summed to ~116 ha — **more land than
the island has.** Absolute instance counts were roughly 2× too high. Areas have been re-anchored to
the real 64.2 ha:

| Biome | Assumed area | Note |
|---|---:|---|
| Autumn Forest | 18.0 ha | Largest; contains the Glade |
| Forest | 16.0 ha | |
| Eerie Forest | 5.0 ha | Contains the Fountain |
| Beach | 5.0 ha | Perimeter band |
| Mountain | 5.0 ha | |
| Heretic Forest | 4.0 ha | |
| Flak Tower | 3.5 ha | |
| Fountain | 1.0 ha | Inside Eerie, not additional |
| Glade | 0.5 ha | Inside Autumn, not additional |
| **Assigned** | **~57.5 ha** | leaves ~6.7 ha of transition/hinterland |

**These are placeholders chosen to be plausible and to sum correctly — they are not a design.**
The instances/ha and triangles/ha columns are the real outputs and are unaffected. Replace the areas
once the map exists and the counts follow automatically.

### 6.3 What is needed before Gaia can run

**Carlos to define — the actual biome map.** Any of these forms works:

1. **Painted terrain layers** — the strategy doc's §2 approach, and the one the whole plan assumes:
   one `TerrainLayer` per biome, painted onto the splatmap, and every spawn rule masked to its
   layer. Gaia reads this natively.
2. **Polygon regions in world coordinates** — a list of X/Z polygons on the −512…+512 frame.
   Gaia `ImageMask` supports `PolyMask`.
3. **A biome map image** — a colour-coded PNG at terrain resolution, one colour per biome, applied
   as an `ImageMask` with `m_imageMaskSpace = World`. Probably the fastest route given
   `Docs/Design/Island-Terrain-Reference/Map/biomes.png` already exists in this style — it just has
   to be redrawn against the current island shape.

Whichever form, these must also be fixed before or alongside it:

- **The nine landmark positions** in the new coordinate frame — Chapel, Well, Mine Entrance, Mine
  Exit, Flak Tower, Cabin, Camp, Dock, Glade. The old ones are on the dead terrain.
- **The player spawn point.** The old (883, 80.6, 4489) is outside this terrain entirely.
- **Terrain layers must exist at all.** Currently zero. Terrain layers are the biome masks, so
  there is nothing for a spawn rule to key off. `Island_Original_TerrainData_Backup.asset` holds
  the previous 8-layer, 27-detail-prototype configuration to copy from — see §4.

Until the biome map exists, the only work that can proceed is **rebuilding the terrain layers and
the grass detail pass** (§4), which is biome-independent groundwork.


---

## 7. Per-biome distribution


### 1. Forest — the main lost biome

Density target **5 / 5** · biome area ~**16.0 ha** (old survey, needs re-anchoring)

**A. Canopy** — target **55 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_Tree_04_GTree01_01_SM_2` | 16 | M | 17.7-28.3 | 17.7 | 33.1 | 1.9 | 3,356 | 141 | Primary old-growth fir, cheap for its size |
| `AP_Tree_04_GTree01_02_SM` | 14 | M | 14.3-23.0 | 14.3 | 31.1 | 1.9 | 2,842 | 123 | Fir variation |
| `AP_Tree_04_GTree01_03_SM` | 11 | M | 12.3-19.6 | 12.3 | 37.5 | 2.0 | 4,532 | 97 | Narrow fir |
| `AP_Tree_Conifir_A_01_SM_2` | 12 | M | 12.8-20.5 | 12.8 | 30.9 | 1.5 | 3,082 | 106 | Dense evergreen mass |
| `AP_Tree_Conifir_A_02_SM` | 10 | M | 12.7-20.4 | 12.7 | 30.6 | 1.4 | 3,082 | 88 | Conifer variation |
| `AP_Tree_04_M01_01_SM_2` | 10 | M | 17.8-28.4 | 17.8 | 28.5 | 2.4 | 3,846 | 88 | Secondary spruce family |
| `AP_Tree_04_M01_02_SM` | 8 | S | 30.6-53.6 | 15.3 | 37.6 | 2.7 | 4,432 | 70 | Tall spruce |
| `AP_Tree_04_M01_03_SM` | 5 | S | 40.9-71.6 | 20.5 | 41.9 | 2.8 | 3,801 | 44 | EMERGENT — 41.9 m, tallest in the set |
| `AP_Tree_04_GTree01_04_SM` | 5 | S | 34.1-59.6 | 17.0 | 39.6 | 1.9 | 4,217 | 44 | EMERGENT — leaning 39.6 m fir |
| `AP_Tree_04_GTree01_05_SM` | 4 | S | 23.8-41.6 | 11.9 | 38.5 | 2.0 | 4,146 | 35 | EMERGENT — 38.5 m narrow crown |
| `AP_Tree_04_GTree01_06_SM` | 5 | S | 40.6-71.0 | 20.3 | 27.9 | 2.2 | 4,146 | 44 | Asymmetric 20 m crown, gap edge |

**B. Sub-canopy** — target **95 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `RF_Tree4` | 15 | D | 5.2-7.7 | 8.6 | 23.9 | 1.1 | 976 | 228 | Cheapest tall backbone — 976 tris for 24 m |
| `RF_Tree3` | 14 | D | 4.8-7.2 | 8.0 | 19.8 | 0.6 | 1,378 | 213 | Thin pine filler |
| `RF_Tree2` | 12 | D | 4.0-5.9 | 6.6 | 17.8 | 0.5 | 1,204 | 182 | Thin pine filler |
| `RF_Tree1` | 12 | D | 2.4-3.5 | 3.9 | 13.3 | 0.3 | 314 | 182 | Cheapest tree in the project — 314 tris |
| `AP_Norway_Spruce_01` | 10 | D | 2.4-3.6 | 4.0 | 12.7 | 0.4 | 1,514 | 152 | Narrow dark spire, 4.0 m footprint |
| `AP_Tree_04_PTree_01_SM_2` | 8 | M | 10.9-17.4 | 10.9 | 20.4 | 1.4 | 3,067 | 122 | Sparse lower-slope pine |
| `AP_Tree_04_PTree_03_SM` | 7 | M | 11.3-18.1 | 11.3 | 20.9 | 1.8 | 3,043 | 106 | Pine variation |
| `AP_Tree_04_M01_04_SM` | 6 | M | 13.5-21.6 | 13.5 | 23.4 | 1.7 | 2,978 | 91 | Sparse spruce |
| `AP_Tree_04_M01_05_SM` | 5 | M | 16.6-26.5 | 16.6 | 23.0 | 2.4 | 2,710 | 76 | Edge-of-gap spruce |
| `AP_BC_PineTree_02` | 5 | M | 6.7-10.7 | 6.7 | 12.4 | 0.9 | 2,338 | 76 | Broad pine |
| `AP_BC_PineTree_03` | 4 | M | 9.8-15.7 | 9.8 | 11.6 | 0.8 | 1,360 | 61 | Asymmetric pine |
| `AP_M6_Tree_ForestTree08_SM_JYI_2` | 2 | S | 38.7-67.7 | 19.3 | 25.0 | 2.0 | 1,305 | 30 | Broadleaf break — cheap at 25 m |

**C. Shrub / small tree** — target **250 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `RF_Bush2` | 18 | D | 0.7-1.1 | 1.2 | 1.3 | — | 8 | 720 | Low leafy cluster, no collider |
| `RF_Bush3` | 14 | D | 1.1-1.7 | 1.9 | 2.2 | — | 22 | 560 | Tall close-camera brush, no collider |
| `AP_Plant_001_08` | 14 | D | 1.0-1.5 | 1.6 | 1.2 | — | 228 | 560 | Dense leafy shrub |
| `AP_plant_001_28` | 12 | D | 1.9-2.9 | 3.2 | 2.2 | — | 138 | 480 | Brighter green shrub |
| `RF_Bush1` | 10 | D | 0.5-0.8 | 0.6 | 1.0 | — | 4 | 400 | Small upright, 0.6 m |
| `RF_Sapling1` | 10 | M | 2.4-3.9 | 2.4 | 2.4 | 0.3 | 244 | 400 | Young growth — GPT never used this |
| `RF_Sapling2` | 8 | M | 2.8-4.5 | 2.8 | 5.4 | 0.4 | 684 | 320 | Taller sapling — GPT never used this |
| `AP_M6_Tree_Bushtree01_SM` | 8 | M | 9.7-15.5 | 9.7 | 8.1 | 3.2 | 966 | 320 | Multi-stem cover bush, 9.7 m |
| `AP_M6_Tree_Bushtree02_SM` | 6 | M | 12.6-20.1 | 12.6 | 5.8 | 2.5 | 731 | 240 | Lighter multi-stem, 12.6 m |

**D. Ground cover (no colliders — go heavy)** — target **900 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `Fern_01A` | 18 | D | 1.1-1.7 | 1.8 | 0.5 | — | 156 | 2592 | Dominant fern bed |
| `Fern_01B` | 16 | D | 1.1-1.7 | 1.9 | 0.7 | — | 252 | 2304 | Fern variation |
| `Fern_02A` | 13 | D | 1.1-1.7 | 1.8 | 0.5 | — | 156 | 1872 | Yellow-green fern |
| `RF_Fern1` | 12 | D | 0.5-0.8 | 0.7 | 0.2 | — | 9 | 1728 | Tiny fern, 9 tris — GPT never used this |
| `Fern_02B` | 11 | D | 1.1-1.7 | 1.9 | 0.7 | — | 252 | 1584 | Fern variation |
| `Fern_03A` | 10 | D | 1.4-2.0 | 2.3 | 0.6 | — | 224 | 1440 | Long-frond fern, 2.3 m |
| `RF_Fern2` | 10 | D | 0.8-1.2 | 1.3 | 0.5 | — | 33 | 1440 | Small fern, 33 tris — GPT never used this |
| `AP_Mushroom_B01` | 5 | M | 4.5-7.3 | 4.5 | 0.8 | — | 1,036 | 720 | Fungal patch — 4.6 m mesh, not one mushroom |
| `AP_Mushroom_C01` | 5 | M | 4.3-6.9 | 4.3 | 0.7 | — | 1,114 | 720 | Second fungal patch — 4.3 m |

**E. Debris & rock** — target **45 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `RF_Log1` | 16 | M | 5.2-8.3 | 5.2 | 0.9 | 5.2 | 112 | 115 | Trail frame — WARNING 5.2 m block, 0.9 m visible |
| `RF_Stump1` | 15 | D | 1.2-1.7 | 1.9 | 1.6 | 1.2 | 112 | 108 | Stump near clearings |
| `RF_Log2` | 13 | M | 6.1-9.8 | 6.1 | 0.7 | 5.0 | 230 | 94 | Deadfall — WARNING 5.0 m block, 0.7 m visible |
| `RF_Boulder1` | 12 | D | 0.6-0.9 | 1.0 | 0.7 | 0.9 | 84 | 86 | Small mossy rock, 1.0 m |
| `RF_Boulder3` | 11 | M | 2.8-4.4 | 2.8 | 1.1 | 2.2 | 52 | 79 | Low broad boulder, crouch cover |
| `RF_Boulder4` | 10 | M | 3.0-4.9 | 3.0 | 1.7 | 2.1 | 62 | 72 | Largest wet moss boulder, crouch cover |
| `AP_M6_Rock_FieldStoneStone05_SM` | 9 | D | 0.8-1.2 | 1.3 | 0.7 | 1.3 | 480 | 65 | Ravine pebble, 1.3 m |
| `AP_M6_Rock_FieldStoneStone06_SM` | 8 | D | 0.9-1.4 | 1.6 | 1.2 | 1.6 | 1,008 | 58 | Streamside rock, 1.6 m |
| `AP_Tree_Break_MushroomTrunk_01_SM` | 6 | S | 4.6-8.1 | 2.3 | 0.9 | 1.8 | 801 | 43 | Fungal old-growth stump |

**Forest totals:** ~1,345 instances/ha · ~656,082 triangles/ha · ~21,520 instances across the biome.


### 2. Autumn Forest — the beautiful lie

Density target **4.5 / 5** · biome area ~**18.0 ha** (old survey, needs re-anchoring)

**A. Warm canopy** — target **50 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_FallTree_01_SM` | 18 | M | 8.9-14.2 | 8.9 | 25.1 | 0.8 | 2,890 | 162 | Primary warm vertical, 25.1 m |
| `AP_FallTree_01_SM_1` | 16 | M | 11.1-17.8 | 11.1 | 27.6 | 0.8 | 2,890 | 144 | Secondary yellow-orange, 27.6 m |
| `AP_Tree_AUT_White_A_02_SM` | 13 | M | 10.5-16.8 | 10.5 | 19.2 | 0.6 | 3,505 | 117 | Pale-bark cluster accent |
| `AP_Tree_color_001_01_2` | 12 | M | 11.6-18.5 | 11.6 | 11.3 | 2.2 | 1,428 | 108 | Dense amber crown |
| `AP_Tree_AUT_White_A_03_SM` | 11 | M | 9.7-15.5 | 9.7 | 18.9 | 0.6 | 2,823 | 99 | Pale-bark transition |
| `AP_FallTree_02_SM` | 10 | M | 8.1-13.0 | 8.1 | 15.9 | 0.4 | 2,535 | 90 | Late-autumn transition tree |
| `AP_Tree_color_001_03` | 10 | M | 10.1-16.1 | 10.1 | 9.4 | 1.8 | 1,460 | 90 | Olive/russet variation |
| `AP_Tree_Blackpoplar01_SM` | 6 | S | 30.2-52.8 | 15.1 | 21.6 | 2.0 | 3,057 | 54 | Wet lowland broadleaf |
| `AP_Tree_Oak01_SM` | 4 | S | 22.7-39.7 | 11.3 | 18.8 | 1.6 | 6,948 | 36 | Rare old broadleaf landmark |

**B. Dark conifer pierce — 1-2 per 5-12 tree grove** — target **30 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_Norway_Spruce_01` | 24 | M | 4.0-6.4 | 4.0 | 12.7 | 0.4 | 1,514 | 130 | Tall dark spire, navigation rhythm |
| `AP_BC_PineTree_02` | 20 | M | 6.7-10.7 | 6.7 | 12.4 | 0.9 | 2,338 | 108 | Broad evergreen mass |
| `AP_Tree_Conifir_A_01_SM_2` | 16 | S | 25.7-44.9 | 12.8 | 30.9 | 1.5 | 3,082 | 86 | Dense evergreen background |
| `AP_Tree_04_GTree01_01_SM_2` | 14 | M | 17.7-28.3 | 17.7 | 33.1 | 1.9 | 3,356 | 76 | Old heavy fir silhouette |
| `RF_Tree3` | 14 | M | 8.0-12.8 | 8.0 | 19.8 | 0.6 | 1,378 | 76 | Thin pine variation, depth filler |
| `AP_BC_PineTree_03` | 12 | M | 9.8-15.7 | 9.8 | 11.6 | 0.8 | 1,360 | 65 | Asymmetric pine |

**C. Small autumn crowns** — target **110 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_ENV_tree_SaGeeSukRim` | 34 | D | 5.2-7.8 | 8.6 | 10.0 | 0.6 | 288 | 673 | Small orange crown — 288 tris, use freely |
| `AP_ENV_tree_ToeMunJean` | 30 | D | 4.7-7.1 | 7.9 | 10.4 | 0.8 | 309 | 594 | Low broad autumn mass — 309 tris |
| `AP_ENV_tree_Nokmyung` | 14 | M | 3.5-5.6 | 3.5 | 3.9 | 0.3 | 289 | 277 | Soft round green canopy, 3.5 m |
| `RF_Sapling2` | 12 | M | 2.8-4.5 | 2.8 | 5.4 | 0.4 | 684 | 238 | Young pale trunk |
| `RF_Sapling1` | 10 | M | 2.4-3.9 | 2.4 | 2.4 | 0.3 | 244 | 198 | Small sapling |

**D. Shrub** — target **240 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_plant_001_13` | 24 | M | 4.1-6.5 | 4.1 | 1.7 | — | 282 | 1037 | Rust-orange shrub — 4.1 m patch mesh |
| `AP_Plant_001_08` | 20 | D | 1.0-1.5 | 1.6 | 1.2 | — | 228 | 864 | Dark green shrub counterpoint |
| `RF_Bush2` | 18 | D | 0.7-1.1 | 1.2 | 1.3 | — | 8 | 778 | Low leafy blocker |
| `AP_plant_001_14` | 14 | D | 1.3-1.9 | 2.2 | 1.4 | — | 28 | 605 | Darker low shrub |
| `RF_Bush3` | 12 | D | 1.1-1.7 | 1.9 | 2.2 | — | 22 | 518 | Tall close-camera brush |
| `AP_Neutral_Foliage_A_Weed01_SM` | 12 | M | 3.2-5.2 | 3.2 | 1.5 | — | 72 | 518 | Path-edge clump — 3.3 m wide, 1.5 m tall |

**E. Ground cover** — target **700 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `Fern_01A` | 26 | D | 1.1-1.7 | 1.8 | 0.5 | — | 156 | 3276 | Dominant green fern bed |
| `Fern_01B` | 22 | D | 1.1-1.7 | 1.9 | 0.7 | — | 252 | 2772 | Fern variation |
| `RF_Fern2` | 16 | D | 0.8-1.2 | 1.3 | 0.5 | — | 33 | 2016 | Small fern |
| `AP_Mushroom_A01_2` | 14 | D | 0.7-1.0 | 1.1 | 0.4 | — | 646 | 1764 | Small saturated Halloween accent |
| `AP_Flower_001_09` | 12 | M | 1.4-2.2 | 1.4 | 0.7 | — | 148 | 1512 | Rare red floral spot |
| `AP_Neutral_Foliage_A_Weed02_SM` | 10 | D | 0.9-1.4 | 1.5 | 0.8 | — | 148 | 1260 | Dry grass tuft |

**F. Debris & rock** — target **35 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `RF_Log1` | 20 | M | 5.2-8.3 | 5.2 | 0.9 | 5.2 | 112 | 126 | Mossy trail frame |
| `RF_Stump1` | 20 | M | 1.9-3.1 | 1.9 | 1.6 | 1.2 | 112 | 126 | Cut/broken rhythm near clearings |
| `RF_Boulder2` | 16 | M | 2.1-3.3 | 2.1 | 0.9 | 1.9 | 54 | 101 | Small mossy rock cluster |
| `AP_TurtleLake_Rock_LakeRock02_SM` | 16 | M | 1.4-2.3 | 1.4 | 0.8 | 1.3 | 854 | 101 | Damp rock at drainage lines |
| `AP_Tree_Break_MushroomTrunk_01_SM` | 14 | S | 4.6-8.1 | 2.3 | 0.9 | 1.8 | 801 | 88 | Fungal hero stump |
| `RF_Log2` | 14 | S | 12.3-21.5 | 6.1 | 0.7 | 5.0 | 230 | 88 | Secondary deadfall |

**Autumn Forest totals:** ~1,165 instances/ha · ~440,563 triangles/ha · ~20,970 instances across the biome.


### 3. Beach — cold exposure zone

Density target **1 / 5 on sand, 5 / 5 at treeline** · biome area ~**5.0 ha** (old survey, needs re-anchoring)

**A. Shore rock** — target **40 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_M6_Rock_SeashoreWallStone01_SM` | 20 | M | 4.8-7.7 | 4.8 | 4.0 | 3.9 | 1,600 | 40 | Primary outcrop — the ONLY 4 m standing rock we own |
| `AP_TurtleLake_Rock_LakeRock02_SM` | 16 | M | 1.4-2.3 | 1.4 | 0.8 | 1.3 | 854 | 32 | Low tide-pool edge rock |
| `AP_M6_Rock_FieldStoneStone06_SM` | 14 | M | 1.6-2.5 | 1.6 | 1.2 | 1.6 | 1,008 | 28 | Rounded wet boulder, 1.6 m |
| `AP_M6_Rock_FieldStoneStone05_SM` | 13 | M | 1.3-2.1 | 1.3 | 0.7 | 1.3 | 480 | 26 | Small fieldstone, 1.3 m |
| `RF_Boulder2` | 11 | M | 2.1-3.3 | 2.1 | 0.9 | 1.9 | 54 | 22 | Low scattered stone |
| `RF_Boulder1` | 9 | M | 1.0-1.6 | 1.0 | 0.7 | 0.9 | 84 | 18 | Mossy inland-edge pebble |
| `RF_Boulder5` | 8 | S | 9.2-16.0 | 4.6 | 2.5 | 3.1 | 58 | 16 | Wet rock colour variation, 4.6 m |
| `AP_TurtleLake_Rock_GoblinRock02_SM` | 5 | S | 7.8-13.7 | 3.9 | 1.8 | 2.1 | 1,370 | 10 | Occasional landmark boulder (42% buried) |
| `AP_TurtleLake_Rock_TurtleRock04_SM` | 4 | H | hand | 11.0 | 1.1 | 7.8 | 1,686 | 1/node | 11 m slab, only 1.15 m proud — hand-place as a rocky point |

**B. Driftwood — storm-deposit clusters, never a chain** — target **22 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `RF_Log1` | 20 | S | 10.3-18.1 | 5.2 | 0.9 | 5.2 | 112 | 22 | Long bleached horizontal |
| `RF_Log2` | 18 | S | 12.3-21.5 | 6.1 | 0.7 | 5.0 | 230 | 20 | Broken tapered log |
| `AP_Tree_Break_02_SM` | 14 | S | 12.7-22.2 | 6.3 | 1.3 | 6.0 | 1,252 | 15 | Dark waterlogged trunk, 6.3 m |
| `AP_Tree_Break_03_SM` | 12 | S | 18.6-32.6 | 9.3 | 1.3 | 8.5 | 1,238 | 13 | Secondary trunk, 9.3 m |
| `AP_TurtleLake_Tree_BrokenTree01_SM` | 12 | S | 8.4-14.7 | 4.2 | 0.4 | 3.8 | 489 | 13 | Shore log — WORST trip-wall in the set |
| `AP_TurtleLake_Tree_Stump02_SM` | 10 | S | 7.8-13.7 | 3.9 | 1.5 | 0.7 | 2,208 | 11 | Root cluster near wrack line |
| `RF_Stump2` | 8 | S | 5.4-9.5 | 2.7 | 3.4 | 1.4 | 152 | 9 | Root-like vertical accent |
| `RF_Log3` | 6 | H | hand | 10.1 | 1.9 | 8.6 | 404 | 1/node | 10 m x 1.9 m proud — hand-place, unvaultable |

**C. Treeline transition only — never a beach grove** — target **12 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_AlaskaCedar_001_2` | 30 | S | 10.4-18.2 | 5.2 | 10.9 | 0.4 | 963 | 18 | Wind-beaten survivor, 5.2 m footprint |
| `AP_Tree_04_PTree_05_SM` | 24 | S | 8.4-14.7 | 4.2 | 13.3 | 0.6 | 655 | 14 | Tall thin transition pine, 4.2 m |
| `RF_Tree2` | 20 | A | 29.7-52.8 | 6.6 | 17.8 | 0.5 | 1,204 | 12 | Sparse shore pine |
| `RF_Tree1` | 16 | A | 17.7-31.4 | 3.9 | 13.3 | 0.3 | 314 | 10 | Nearly bare shore pine |
| `AP_Tree_04_PTree_04_SM` | 10 | A | 34.6-61.5 | 7.7 | 17.6 | 0.8 | 1,194 | 6 | Wind-shaped pine |

**D. Salt grass above the high-tide line** — target **120 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_Neutral_Foliage_A_Weed02_SM` | 34 | M | 1.5-2.4 | 1.5 | 0.8 | — | 148 | 204 | Sparse grass |
| `AP_Neutral_Tree_A_PrickelGrass01_SM` | 28 | M | 1.9-3.1 | 1.9 | 1.2 | — | 1,087 | 168 | Pale dune/wrack grass |
| `AP_Neutral_Foliage_A_Weed01_SM` | 22 | S | 6.5-11.4 | 3.2 | 1.5 | — | 72 | 132 | Taller salt-beaten clump |
| `RF_Bush1` | 16 | S | 1.5-2.4 | 0.6 | 1.0 | — | 4 | 96 | Treeline-edge shrub only |

**Beach totals:** ~194 instances/ha · ~102,865 triangles/ha · ~970 instances across the biome.


### 4. Eerie Forest — ecological corruption

Density target **4.5 / 5 trunk density** · biome area ~**5.0 ha** (old survey, needs re-anchoring)

**A. Dead backbone — CHEAP meshes carry the density** — target **130 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_DeadTree02` | 18 | D | 9.7-14.5 | 16.1 | 13.0 | 1.5 | 725 | 117 | Primary leafless tree — 725 tris, the workhorse |
| `AP_Tree_DeadTree01_SM` | 14 | D | 11.0-16.5 | 18.3 | 13.6 | 1.7 | 1,962 | 91 | Dead tree, 1,962 tris |
| `AP_DeadTree03` | 12 | D | 9.8-14.7 | 16.3 | 12.3 | 2.1 | 2,211 | 78 | Y-shaped negative space |
| `AP_Tree_WNT_03_Bark_01_SM_2` | 12 | D | 4.0-6.0 | 6.6 | 17.5 | 0.8 | 3,607 | 78 | Thin winter snag, 6.6 m footprint |
| `AP_DeadTree04` | 11 | D | 7.7-11.5 | 12.8 | 11.4 | 2.6 | 1,803 | 72 | Leaning claw silhouette |
| `AP_Tree_WNT_M01_01_SM_3` | 10 | M | 16.6-26.6 | 16.6 | 19.7 | 1.8 | 2,949 | 65 | Bare winter crown |
| `AP_Tree_WNT_M_03_SM` | 10 | M | 13.4-21.4 | 13.4 | 16.7 | 1.2 | 2,872 | 65 | Dense black branch lattice |
| `AP_Tree_Burnt_04_SM` | 8 | M | 8.2-13.2 | 8.2 | 27.4 | 1.7 | 3,531 | 52 | Charred 27.6 m vertical punctuation |
| `AP_WhiteFir_MD_Dead_03` | 5 | M | 6.2-9.8 | 6.2 | 14.9 | 1.1 | 1,315 | 32 | Damaged fir, 1,315 tris |

**B. Expensive claw silhouettes — ACCENT ONLY** — target **14 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_GraveKeepers_B04` | 18 | S | 12.7-22.3 | 6.4 | 6.0 | 0.7 | 17,378 | 13 | Narrow snag — 17,378 tris for 6 m, worst ratio in the set |
| `AP_GraveKeepers_B02` | 15 | S | 29.9-52.4 | 15.0 | 14.7 | 2.9 | 27,036 | 10 | Upright tangled dead tree — 27,036 tris |
| `AP_GraveKeepers_B06` | 14 | S | 28.7-50.3 | 14.4 | 12.3 | 3.2 | 17,899 | 10 | Forked corridor blocker — 17,899 tris |
| `AP_GraveKeepers_B01` | 13 | S | 37.2-65.0 | 18.6 | 16.3 | 4.0 | 20,824 | 9 | Hanging lateral branch — 20,824 tris |
| `AP_GraveKeepers_B07` | 12 | S | 41.6-72.8 | 20.8 | 17.1 | 4.0 | 5,929 | 8 | Dense claw crown |
| `AP_GraveKeepers_B03_2` | 10 | S | 49.9-87.4 | 25.0 | 26.7 | 4.0 | 16,443 | 7 | Broad crooked mass, 25 m wide |
| `AP_Tree_Deadtree06_SM` | 10 | S | 18.6-32.5 | 9.3 | 15.5 | 3.5 | 6,837 | 7 | Large twisted dead tree |
| `AP_Tree_Dry_D01` | 8 | S | 15.6-27.3 | 7.8 | 7.7 | 2.8 | 2,243 | 6 | Root-heavy decayed trunk |

**C. Hero** — target **0.3 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_S_Tree_01` | 100 | H | hand | 30.1 | 33.8 | 6.4 | 2,530 | 1/node | 30 m x 33.8 m rooted giant — 1 per district, NEVER scattered |

**D. Rare living survivors — proof of corruption** — target **6 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_AlaskaCedar_001_2` | 34 | A | 23.4-41.5 | 5.2 | 10.9 | 0.4 | 963 | 10 | Rare surviving pine |
| `RF_Tree2` | 26 | A | 29.7-52.8 | 6.6 | 17.8 | 0.5 | 1,204 | 8 | Sickly green survivor |
| `AP_BC_PineTree_03` | 22 | A | 44.1-78.4 | 9.8 | 11.6 | 0.8 | 1,360 | 7 | Distant normal-tree reminder |
| `AP_Norway_Spruce_01` | 18 | A | 18.0-32.0 | 4.0 | 12.7 | 0.4 | 1,514 | 5 | Single dark vertical landmark |

**E. Dead ground layer** — target **420 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_Neutral_Foliage_A_Weed01_SM` | 32 | M | 3.2-5.2 | 3.2 | 1.5 | — | 72 | 672 | Dark dead-grass floor — 3.3 m wide |
| `AP_Neutral_Tree_A_PrickelGrass01_SM` | 28 | M | 1.9-3.1 | 1.9 | 1.2 | — | 1,087 | 588 | Pale ghostlike scrub |
| `AP_Tree_Dry_N02` | 20 | M | 5.2-8.4 | 5.2 | 0.6 | — | 1,627 | 420 | Branch litter — 5.3 m flat decal, no collider |
| `AP_Neutral_Foliage_A_Weed02_SM` | 18 | D | 0.9-1.4 | 1.5 | 0.8 | — | 148 | 378 | Short dead tuft |
| `AP_Nest_B01` | 2 | S | 15.4-27.0 | 7.7 | 2.0 | — | 451 | 42 | Stick nests — 7.7 m wide, NOT a small prop |

**F. Debris & rock** — target **40 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `RF_Stump2` | 26 | M | 2.7-4.4 | 2.7 | 3.4 | 1.4 | 152 | 52 | Narrow broken stump, 3.4 m proud |
| `RF_Boulder4` | 22 | M | 3.0-4.9 | 3.0 | 1.7 | 2.1 | 62 | 44 | Moss-dark cover rock |
| `AP_Tree_Break_Root_02_SM` | 20 | M | 3.0-4.8 | 3.0 | 1.3 | 1.3 | 509 | 40 | Root trip hazard |
| `RF_Log2` | 18 | S | 12.3-21.5 | 6.1 | 0.7 | 5.0 | 230 | 36 | Fallen corridor obstruction |
| `RF_Boulder3` | 14 | M | 2.8-4.4 | 2.8 | 1.1 | 2.2 | 52 | 28 | Low broad rock |

**Eerie Forest totals:** ~610 instances/ha · ~814,106 triangles/ha · ~3,052 instances across the biome.


### 5. Heretic Forest — authored ritual landscape, the final section

Density target **4 / 5** · biome area ~**4.0 ha** (old survey, needs re-anchoring)

**A. Dead base — same cheap backbone as Eerie, thinned to open ritual clearings** — target **90 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_DeadTree02` | 20 | D | 9.7-14.5 | 16.1 | 13.0 | 1.5 | 725 | 72 | Cheap leafless base |
| `AP_Tree_WNT_03_Bark_01_SM_2` | 16 | D | 4.0-6.0 | 6.6 | 17.5 | 0.8 | 3,607 | 58 | Thin background bars |
| `AP_Tree_DeadTree01_SM` | 14 | D | 11.0-16.5 | 18.3 | 13.6 | 1.7 | 1,962 | 50 | Dead-tree base |
| `AP_DeadTree03` | 12 | M | 16.3-26.1 | 16.3 | 12.3 | 2.1 | 2,211 | 43 | Leaning branch frame |
| `AP_DeadTree04` | 11 | M | 12.8-20.4 | 12.8 | 11.4 | 2.6 | 1,803 | 40 | Second leaning variation |
| `AP_Tree_WNT_M_03_SM` | 10 | M | 13.4-21.4 | 13.4 | 16.7 | 1.2 | 2,872 | 36 | Black branch lattice |
| `AP_Tree_Burnt_04_SM` | 9 | M | 8.2-13.2 | 8.2 | 27.4 | 1.7 | 3,531 | 32 | Charred marker |
| `AP_Tree_Deadtree06_SM` | 8 | S | 18.6-32.5 | 9.3 | 15.5 | 3.5 | 6,837 | 29 | Large twisted base tree |

**B. Curse trees — ritual mid-layer, expensive, keep sparse** — target **12 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_Tree_Curse_J07` | 26 | S | 29.6-51.9 | 14.8 | 13.8 | 2.1 | 13,461 | 12 | Ritual-adjacent crooked tree — 13,461 tris |
| `AP_Tree_Curse_J08` | 22 | S | 20.1-35.2 | 10.1 | 11.4 | 1.2 | 10,366 | 11 | Taller curse-tree — 10,366 tris |
| `AP_Tree_Curse_K01` | 20 | S | 19.6-34.3 | 9.8 | 9.5 | 3.1 | 18,270 | 10 | Thick clawed trunk — 18,270 tris |
| `AP_Tree_Dry_D01` | 18 | S | 15.6-27.3 | 7.8 | 7.7 | 2.8 | 2,243 | 9 | Root-heavy decayed tree |
| `AP_Tree_Curse_H01_2` | 14 | S | 31.3-54.7 | 15.6 | 9.9 | 3.7 | 27,975 | 7 | Hanging curse tree — 27,975 tris, heaviest mesh we own |

**C. Ritual heroes — CAP AT ONE PER AUTHORED NODE** — target **0.8 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_Building_exorcist_tree2` | 22 | H | hand | 18.1 | 14.8 | 1.6 | 1,141 | 1/node | Primary ritual landmark, 18 m — and only 1,141 tris |
| `AP_GangshiTree_2` | 18 | H | hand | 29.2 | 34.8 | 5.1 | 9,393 | 1/node | Altar tree — 29 m x 34.8 m, biggest thing in the set |
| `AP_M6_Tree_MonsterTreeBark_SM_PHJ_2` | 16 | H | hand | 9.9 | 12.2 | 6.7 | 9,304 | 1/node | Monstrous root — see collider audit, 6.7 m block |
| `AP_Tree_Heretic_A01_2` | 16 | H | hand | 8.0 | 7.9 | 2.7 | 7,780 | 1/node | Sculptural heretic tree — only 8 m, smaller than GPT assumed |
| `AP_sunghwangdang_Tree_pagoda_01` | 14 | H | hand | 4.5 | 4.5 | 3.8 | 2,668 | 1/node | Bound stone totem |
| `AP_sunghwangdang_Tree_pagoda_01_1` | 14 | H | hand | 3.6 | 3.9 | 3.2 | 3,645 | 1/node | Alternate cairn/totem |

**D. Ritual ground formations — large flat meshes, NOT small props** — target **18 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_Tree_Heretic_D03` | 22 | S | 30.1-52.6 | 15.0 | 2.3 | 6.3 | 4,500 | 16 | Root altar frame — 15.0 m across, 6.3 m block |
| `AP_Tree_Heretic_D03_02` | 20 | S | 24.4-42.6 | 12.2 | 2.4 | 2.8 | 4,170 | 14 | Second root formation, 12.2 m |
| `AP_Tree_Heretic_D02_01` | 20 | S | 24.4-42.7 | 12.2 | 1.6 | 3.4 | 4,668 | 14 | Crawling root formation, 12.2 m |
| `AP_Tree_Break_Root_02_SM` | 20 | M | 3.0-4.8 | 3.0 | 1.3 | 1.3 | 509 | 14 | Root ring / barrier |
| `AP_Nest_B01` | 18 | S | 15.4-27.0 | 7.7 | 2.0 | — | 451 | 13 | Twig altar filler — 7.7 m wide |

**E. Stakes & bound stones — small props, use them DENSE around nodes** — target **70 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_Tree_Heretic_B05` | 26 | D | 1.0-1.5 | 1.7 | 4.3 | 1.2 | 544 | 73 | Single stake, 1.7 m — GPT over-spaced this 12x |
| `AP_Tree_Heretic_B03` | 22 | D | 2.3-3.5 | 3.9 | 5.0 | 3.9 | 1,192 | 62 | Stake cluster, 3.9 m |
| `AP_GraveKeepers_C01` | 20 | M | 2.8-4.4 | 2.8 | 2.1 | 2.8 | 2,494 | 56 | Bound ritual boulder, 2.8 m |
| `AP_GraveKeepers_C02` | 18 | M | 2.5-4.0 | 2.5 | 4.0 | 2.5 | 2,620 | 50 | Narrow bound stone, 4.0 m proud |
| `RF_Stump2` | 14 | M | 2.7-4.4 | 2.7 | 3.4 | 1.4 | 152 | 39 | Stake-like broken trunk |

**F. Expensive claw accents** — target **8 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_GraveKeepers_B01` | 22 | S | 37.2-65.0 | 18.6 | 16.3 | 4.0 | 20,824 | 7 | Hanging branch frame |
| `AP_GraveKeepers_B02` | 20 | S | 29.9-52.4 | 15.0 | 14.7 | 2.9 | 27,036 | 6 | Tangled upright |
| `AP_GraveKeepers_B06` | 18 | S | 28.7-50.3 | 14.4 | 12.3 | 3.2 | 17,899 | 6 | Forked funnel tree |
| `AP_GraveKeepers_B07` | 16 | S | 41.6-72.8 | 20.8 | 17.1 | 4.0 | 5,929 | 5 | Dense claw crown |
| `AP_GraveKeepers_B03_2` | 14 | S | 49.9-87.4 | 25.0 | 26.7 | 4.0 | 16,443 | 4 | Crooked branch mass |
| `AP_GraveKeepers_B04` | 10 | S | 12.7-22.3 | 6.4 | 6.0 | 0.7 | 17,378 | 3 | Narrow snag |

**G. Ground & survivors** — target **300 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_Neutral_Foliage_A_Weed01_SM` | 28 | M | 3.2-5.2 | 3.2 | 1.5 | — | 72 | 336 | Dead grass floor |
| `AP_Neutral_Tree_A_PrickelGrass01_SM` | 24 | M | 1.9-3.1 | 1.9 | 1.2 | — | 1,087 | 288 | Pale scrub |
| `AP_Tree_Dry_N02` | 18 | M | 5.2-8.4 | 5.2 | 0.6 | — | 1,627 | 216 | Branch litter decal |
| `RF_Boulder4` | 12 | M | 3.0-4.9 | 3.0 | 1.7 | 2.1 | 62 | 144 | Dark cover stone |
| `RF_Log2` | 8 | S | 12.3-21.5 | 6.1 | 0.7 | 5.0 | 230 | 96 | Low ritual boundary |
| `AP_AlaskaCedar_001_2` | 6 | S | 10.4-18.2 | 5.2 | 10.9 | 0.4 | 963 | 72 | Rare surviving pine |
| `RF_Tree2` | 4 | S | 13.2-23.1 | 6.6 | 17.8 | 0.5 | 1,204 | 48 | Sickly survivor |

**Heretic Forest totals:** ~499 instances/ha · ~912,518 triangles/ha · ~1,995 instances across the biome.


### 6. Flak Tower — open enemy-spawn arena

Density target **3 / 5, continuous ground layer** · biome area ~**3.5 ha** (old survey, needs re-anchoring)

**A. Perimeter & scattered trees — keep the tower on the skyline** — target **22 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_FallTree_01_SM` | 18 | S | 17.8-31.2 | 8.9 | 25.1 | 0.8 | 2,890 | 14 | Primary autumn tree |
| `AP_FallTree_01_SM_1` | 14 | S | 22.2-38.9 | 11.1 | 27.6 | 0.8 | 2,890 | 11 | Secondary yellow tree |
| `AP_ENV_tree_SaGeeSukRim` | 14 | M | 8.6-13.8 | 8.6 | 10.0 | 0.6 | 288 | 11 | Small autumn crown, 288 tris |
| `AP_ENV_tree_ToeMunJean` | 12 | M | 7.9-12.6 | 7.9 | 10.4 | 0.8 | 309 | 9 | Broad low autumn cover |
| `AP_Norway_Spruce_01` | 12 | S | 8.0-14.0 | 4.0 | 12.7 | 0.4 | 1,514 | 9 | Thin sentinel spire |
| `AP_BC_PineTree_02` | 9 | S | 13.4-23.4 | 6.7 | 12.4 | 0.9 | 2,338 | 7 | Broad pine shadow |
| `RF_Tree3` | 8 | S | 16.0-28.1 | 8.0 | 19.8 | 0.6 | 1,378 | 6 | Sparse pine variation |
| `AP_AlaskaCedar_001_2` | 7 | S | 10.4-18.2 | 5.2 | 10.9 | 0.4 | 963 | 5 | Wind-beaten pine |
| `AP_Tree_04_PTree_05_SM` | 6 | S | 8.4-14.7 | 4.2 | 13.3 | 0.6 | 655 | 5 | Very sparse vertical |

**B. Autumn shrub band** — target **160 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_plant_001_13` | 26 | M | 4.1-6.5 | 4.1 | 1.7 | — | 282 | 146 | Autumn-red shrub — 4.1 m patch |
| `AP_plant_001_14` | 22 | D | 1.3-1.9 | 2.2 | 1.4 | — | 28 | 123 | Darker low shrub |
| `AP_Plant_003_10` | 18 | D | 1.1-1.7 | 1.9 | 1.1 | — | 208 | 101 | Low fern-like form |
| `AP_plant_001_18` | 16 | M | 3.9-6.3 | 3.9 | 1.4 | — | 545 | 90 | Coniferous ground spray, 3.9 m |
| `AP_Plant_003_07` | 10 | S | 15.2-26.7 | 7.6 | 10.1 | 0.2 | 504 | 56 | A 10.1 m TREE — GPT mislabelled it a low form |
| `AP_Plant_001_08` | 8 | D | 1.0-1.5 | 1.6 | 1.2 | — | 228 | 45 | Low green shrub |

**C. Flower carpet — no colliders, go heavy** — target **1500 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `Orange Aster_LOD` | 13 | D | 0.5-0.8 | 0.5 | 0.6 | — | 355 | 682 | Primary orange accent, 0.52 m |
| `YellowDaisy_LOD` | 12 | D | 0.5-0.8 | 0.4 | 0.1 | — | 446 | 630 | Low yellow drift, 0.41 m |
| `YellowAfricanDaisy_LOD` | 11 | D | 0.5-0.8 | 0.7 | 0.4 | — | 937 | 578 | Ochre-yellow field accent |
| `Tangerine Violet_LOD` | 10 | D | 0.5-0.8 | 0.8 | 0.3 | — | 156 | 525 | Acid-orange transition |
| `CaliforniaPoppy_01_LOD` | 10 | D | 0.5-0.8 | 0.8 | 0.2 | — | 432 | 525 | Warm orange-red accent |
| `AP_flower_001_10` | 9 | D | 0.5-0.8 | 0.7 | 0.7 | — | 52 | 472 | Green-yellow daisy clump |
| `AP_flower_001_11` | 9 | D | 0.5-0.8 | 0.7 | 0.8 | — | 52 | 472 | Rust-red daisy clump |
| `AP_Neutral_Foliage_A_WildFlower01_SM_JYI` | 8 | D | 0.9-1.4 | 1.5 | 1.2 | — | 52 | 420 | Flowered scrub |
| `AP_GR_003GR_002_D` | 7 | M | 3.1-4.9 | 3.1 | 1.1 | — | 1,121 | 368 | Mixed wildflower patch — 3.1 m, not one flower |
| `Purple Aster_LOD` | 6 | D | 0.5-0.8 | 0.5 | 0.6 | — | 351 | 315 | Purple accent |
| `CoralBells_green` | 5 | D | 0.6-0.9 | 1.0 | 0.6 | — | 142 | 262 | Low green/pink clump |

**D. Grass matrix — prefab layer on top of the terrain detail carpet** — target **500 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_Neutral_Foliage_A_Weed02_SM` | 40 | D | 0.9-1.4 | 1.5 | 0.8 | — | 148 | 700 | Primary 1.5 m grass tuft |
| `AP_Neutral_Foliage_A_Weed01_SM` | 32 | M | 3.2-5.2 | 3.2 | 1.5 | — | 72 | 560 | Taller 3.3 m clump — GPT's 0.6 m would wall the arena |
| `AP_Neutral_Tree_A_PrickelGrass01_SM` | 28 | D | 1.2-1.8 | 1.9 | 1.2 | — | 1,087 | 490 | Pale exposed grass |

**E. Cover rock & debris — the arena's only cover** — target **30 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `RF_Boulder2` | 22 | M | 2.1-3.3 | 2.1 | 0.9 | 1.9 | 54 | 23 | Scattered field rock |
| `RF_Boulder1` | 18 | M | 1.0-1.6 | 1.0 | 0.7 | 0.9 | 84 | 19 | Low mossy cover |
| `AP_TurtleLake_Rock_LakeRock02_SM` | 18 | M | 1.4-2.3 | 1.4 | 0.8 | 1.3 | 854 | 19 | Low drainage rock |
| `AP_M6_Rock_FieldStoneStone06_SM` | 16 | M | 1.6-2.5 | 1.6 | 1.2 | 1.6 | 1,008 | 17 | Larger cover boulder, 1.6 m |
| `RF_Boulder4` | 12 | S | 6.1-10.7 | 3.0 | 1.7 | 2.1 | 62 | 13 | Best crouch cover we own, 1.7 m proud |
| `RF_Log1` | 8 | S | 10.3-18.1 | 5.2 | 0.9 | 5.2 | 112 | 8 | Rare fallen horizontal |
| `RF_Stump1` | 6 | S | 3.9-6.8 | 1.9 | 1.6 | 1.2 | 112 | 6 | Rare stump |

**Flak Tower totals:** ~2,212 instances/ha · ~855,878 triangles/ha · ~7,742 instances across the biome.


### 7. Fountain — luminous sanctuary

Density target **4 / 5 at perimeter, open centre** · biome area ~**1.0 ha** (old survey, needs re-anchoring)

**A. Sheltering arch trees — just OUTSIDE the 10-16 m readable ring** — target **45 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_Tree_10_ArgassTree_02_SM` | 18 | M | 8.1-13.0 | 8.1 | 9.3 | 2.0 | 6,349 | 8 | First sculptural arch tree |
| `AP_Tree_10_ArgassTree_03_SM` | 16 | M | 9.6-15.4 | 9.6 | 11.2 | 2.3 | 9,965 | 7 | Second arch variation |
| `AP_Tree_10_ArgassTree_04_SM` | 14 | M | 12.8-20.5 | 12.8 | 11.6 | 1.7 | 6,652 | 6 | Broad mystical cover tree |
| `AP_Tree_10_ArgassTree_SM` | 12 | M | 7.6-12.1 | 7.6 | 8.1 | 1.4 | 8,199 | 5 | Large central-frame tree |
| `AP_Tree_Juniper02_SMIK` | 12 | M | 8.4-13.5 | 8.4 | 10.6 | 1.4 | 4,052 | 5 | Pale twisted canopy |
| `AP_Tree_Juniper03_SMIK` | 10 | M | 11.6-18.5 | 11.6 | 10.7 | 2.0 | 2,197 | 4 | Juniper variation |
| `AP_Tree_Lake_RoundTree_01_SM` | 10 | S | 25.5-44.7 | 12.8 | 15.3 | 2.7 | 7,253 | 4 | Curling witchlike silhouette |
| `AP_ENV_tree_Nokmyung` | 8 | M | 3.5-5.6 | 3.5 | 3.9 | 0.3 | 289 | 4 | Soft round green canopy, 3.5 m |

**B. Dark outer pine ring — seals the sanctuary** — target **55 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_Tree_Conifir_A_01_SM_2` | 26 | M | 12.8-20.5 | 12.8 | 30.9 | 1.5 | 3,082 | 14 | Dark outer ring |
| `AP_Tree_04_GTree01_01_SM_2` | 22 | M | 17.7-28.3 | 17.7 | 33.1 | 1.9 | 3,356 | 12 | Heavy pine frame |
| `AP_Norway_Spruce_01` | 20 | D | 2.4-3.6 | 4.0 | 12.7 | 0.4 | 1,514 | 11 | Tall perimeter spire, 4.0 m footprint |
| `RF_Tree3` | 18 | D | 4.8-7.2 | 8.0 | 19.8 | 0.6 | 1,378 | 10 | Thin perimeter filler |
| `RF_Tree4` | 14 | M | 8.6-13.8 | 8.6 | 23.9 | 1.1 | 976 | 8 | Tall cheap backbone |

**C. Blue flower basin — the signature layer, no colliders** — target **2200 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `CupcakeWhite_01_LOD` | 15 | D | 0.6-0.9 | 1.0 | 0.1 | — | 312 | 330 | Primary white luminous drift, 1.0 m |
| `VioletDaisy_LOD` | 13 | D | 0.5-0.8 | 0.4 | 0.1 | — | 302 | 286 | Pale daisy, 0.41 m — can go very dense |
| `Blue Aster_LOD` | 12 | D | 0.5-0.8 | 0.5 | 0.6 | — | 355 | 264 | Tall blue accent, 0.52 m |
| `Indigo Violet_LOD` | 11 | D | 0.5-0.8 | 0.8 | 0.3 | — | 156 | 242 | Deep cool accent |
| `Purple Violet_LOD` | 10 | D | 0.5-0.8 | 0.8 | 0.3 | — | 156 | 220 | Purple low drift |
| `BlueEyeGrass_01_LOD` | 9 | D | 0.6-0.8 | 0.9 | 0.2 | — | 288 | 198 | Blue-purple grass flower |
| `African_violet_blue_LOD` | 8 | M | 2.2-3.5 | 2.2 | 0.7 | — | 300 | 176 | Dense blue patch — 2.2 m, not one flower |
| `African_violet_LOD` | 7 | M | 2.1-3.3 | 2.1 | 0.6 | — | 300 | 154 | Purple-blue variation, 2.1 m |
| `AP_Samakyo_flower_003` | 6 | M | 2.3-3.6 | 2.3 | 1.3 | — | 288 | 132 | Thin mystical blue flower, 2.3 m |
| `AP_flower_001_12` | 5 | D | 0.5-0.8 | 0.7 | 0.7 | — | 52 | 110 | Blue hydrangea clump |
| `AP_Flower_001_08` | 4 | D | 0.6-1.0 | 1.1 | 0.6 | — | 146 | 88 | Second blue floral clump |

**D. Grass & fern matrix** — target **700 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_Neutral_Foliage_A_Weed02_SM` | 30 | D | 0.9-1.4 | 1.5 | 0.8 | — | 148 | 210 | Soft grass matrix |
| `AP_flower_001A` | 16 | M | 4.9-7.9 | 4.9 | 0.5 | — | 730 | 112 | Blue carpet — 4.9 m patch mesh, space it wide |
| `Fern_01A` | 16 | D | 1.1-1.7 | 1.8 | 0.5 | — | 156 | 112 | Fern at shaded roots |
| `RF_Fern2` | 14 | D | 0.8-1.2 | 1.3 | 0.5 | — | 33 | 98 | Small fern |
| `Fern_01B` | 12 | D | 1.1-1.7 | 1.9 | 0.7 | — | 252 | 84 | Fern variation |
| `AP_Neutral_Foliage_A_Weed01_SM` | 12 | M | 3.2-5.2 | 3.2 | 1.5 | — | 72 | 84 | Taller grass at the rim |

**E. Damp stone** — target **25 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_TurtleLake_Rock_LakeRock02_SM` | 34 | M | 1.4-2.3 | 1.4 | 0.8 | 1.3 | 854 | 8 | Low damp stone near the rill |
| `RF_Boulder2` | 24 | M | 2.1-3.3 | 2.1 | 0.9 | 1.9 | 54 | 6 | Mossy cover boulder |
| `RF_Boulder3` | 22 | M | 2.8-4.4 | 2.8 | 1.1 | 2.2 | 52 | 6 | Broad low cover |
| `AP_Tree_Break_Root_02_SM` | 20 | M | 3.0-4.8 | 3.0 | 1.3 | 1.3 | 509 | 5 | Exposed root shape |

**Fountain totals:** ~3,025 instances/ha · ~1,136,203 triangles/ha · ~3,025 instances across the biome.


### 8. Glade — treeless wind-beaten hill

Density target **3.5 / 5, all below knee height** · biome area ~**0.5 ha** (old survey, needs re-anchoring)

**A. Grass matrix — the biome's whole structure** — target **1400 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_Neutral_Foliage_A_Weed02_SM` | 34 | D | 0.9-1.4 | 1.5 | 0.8 | — | 148 | 238 | Primary short-grass matrix, 1.5 m |
| `AP_Neutral_Tree_A_PrickelGrass01_SM` | 26 | D | 1.2-1.8 | 1.9 | 1.2 | — | 1,087 | 182 | Pale exposed-hill grass, 1.95 m |
| `AP_Neutral_Foliage_A_Weed01_SM` | 22 | M | 3.2-5.2 | 3.2 | 1.5 | — | 72 | 154 | Taller clump — 3.3 m wide, 1.5 m tall, near eye level |
| `TSA_GrassDry_C` | 10 | D | 0.5-0.8 | 0.6 | 0.7 | — | 122 | 70 | Dry tuft — project asset GPT never used |
| `TSA_Heather_A` | 8 | D | 0.7-1.0 | 1.1 | 0.9 | — | 268 | 56 | Heather clump — project asset GPT never used |

**B. Flower drifts — cluster by colour, do not confetti** — target **1800 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `VioletDaisy_LOD` | 14 | D | 0.5-0.8 | 0.4 | 0.1 | — | 302 | 126 | Pale violet drift, 0.41 m |
| `YellowDaisy_LOD` | 13 | D | 0.5-0.8 | 0.4 | 0.1 | — | 446 | 117 | Muted yellow drift |
| `CupcakeWhite_01_LOD` | 12 | D | 0.6-0.9 | 1.0 | 0.1 | — | 312 | 108 | White windblown patch |
| `BlueEyeGrass_01_LOD` | 12 | D | 0.6-0.8 | 0.9 | 0.2 | — | 288 | 108 | Cool purple-blue accent |
| `Purple Aster_LOD` | 11 | D | 0.5-0.8 | 0.5 | 0.6 | — | 351 | 99 | Purple accent |
| `Blue Aster_LOD` | 10 | D | 0.5-0.8 | 0.5 | 0.6 | — | 355 | 90 | Blue accent |
| `CoralBells_green` | 9 | D | 0.6-0.9 | 1.0 | 0.6 | — | 142 | 81 | Low green/pink clump |
| `AP_flower_001A` | 7 | M | 4.9-7.9 | 4.9 | 0.5 | — | 730 | 63 | Blue carpet — 4.9 m patch mesh |
| `AP_GR_003GR_002_D` | 7 | M | 3.1-4.9 | 3.1 | 1.1 | — | 1,121 | 63 | Mixed wildflower drift, 3.1 m |
| `CaliforniaPoppy_01_LOD` | 5 | M | 0.8-1.3 | 0.8 | 0.2 | — | 432 | 45 | Rare warm interruption |

**C. Fern in sheltered dips only** — target **260 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `Fern_01A` | 24 | D | 1.1-1.7 | 1.8 | 0.5 | — | 156 | 31 | Sheltered dip cluster |
| `Fern_02A` | 21 | D | 1.1-1.7 | 1.8 | 0.5 | — | 156 | 27 | Yellowed wind-burned fern |
| `Fern_02B` | 19 | D | 1.1-1.7 | 1.9 | 0.7 | — | 252 | 25 | Second yellow variation |
| `Fern_01B` | 18 | D | 1.1-1.7 | 1.9 | 0.7 | — | 252 | 23 | Fern variation |
| `RF_Fern1` | 18 | D | 0.5-0.8 | 0.7 | 0.2 | — | 9 | 23 | Tiny fern, 9 tris |

**D. Bush islands — reverse slopes only, never the crown** — target **45 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `RF_Bush2` | 32 | M | 1.2-1.9 | 1.2 | 1.3 | — | 8 | 7 | Low bush island |
| `RF_Bush1` | 28 | M | 0.8-1.3 | 0.6 | 1.0 | — | 4 | 6 | Sparse upright island |
| `RF_Bush3` | 24 | M | 1.9-3.1 | 1.9 | 2.2 | — | 22 | 5 | Taller bush, reverse slope only |
| `TSA_BushDry_B` | 16 | M | 1.2-1.9 | 1.2 | 1.1 | — | 792 | 4 | Dry bush variation |

**E. Orientation rock — a handful, for navigation** — target **8 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `RF_Boulder3` | 30 | S | 5.6-9.7 | 2.8 | 1.1 | 2.2 | 52 | 1 | Broad rock at ridge break |
| `RF_Boulder2` | 26 | S | 4.1-7.2 | 2.1 | 0.9 | 1.9 | 54 | 1 | Low orientation rock |
| `RF_Boulder5` | 22 | S | 9.2-16.0 | 4.6 | 2.5 | 3.1 | 58 | 1 | Largest landmark rock, 4.6 m |
| `AP_M6_Rock_FieldStoneStone06_SM` | 22 | S | 3.1-5.5 | 1.6 | 1.2 | 1.6 | 1,008 | 1 | Single cover boulder |

**Glade totals:** ~3,513 instances/ha · ~1,325,098 triangles/ha · ~1,756 instances across the biome.


### 9. Mountain — rock-dominant ascent

Density target **2.5 / 5** · biome area ~**5.0 ha** (old survey, needs re-anchoring)

**A. Rock mass — SEE THE WARNING: we own no true granite masses** — target **130 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_TurtleLake_Rock_GoblinRock01_SM` | 14 | M | 4.5-7.2 | 4.5 | 1.6 | 3.5 | 1,638 | 91 | Largest rounded boulder — but 50% buried, 1.6 m proud |
| `AP_M6_Rock_CemeteryRock02_SM` | 13 | M | 4.8-7.6 | 4.8 | 2.5 | 4.7 | 3,394 | 84 | Angular ledge / entrance frame, 2.6 m proud |
| `AP_M6_Rock_SeashoreWallStone01_SM` | 13 | M | 4.8-7.7 | 4.8 | 4.0 | 3.9 | 1,600 | 84 | Upright cliff fragment — tallest rock we own, 4.0 m |
| `RF_Boulder5` | 11 | M | 4.6-7.3 | 4.6 | 2.5 | 3.1 | 58 | 72 | Pale lichen boulder, 2.5 m proud |
| `RF_Boulder4` | 10 | M | 3.0-4.9 | 3.0 | 1.7 | 2.1 | 62 | 65 | Broad rock, 1.7 m proud |
| `AP_TurtleLake_Rock_GoblinRock02_SM` | 9 | M | 3.9-6.2 | 3.9 | 1.8 | 2.1 | 1,370 | 58 | Second rounded boulder |
| `RF_Boulder3` | 8 | D | 1.7-2.5 | 2.8 | 1.1 | 2.2 | 52 | 52 | Low broad rock |
| `AP_M6_Rock_FieldStoneStone06_SM` | 7 | D | 0.9-1.4 | 1.6 | 1.2 | 1.6 | 1,008 | 46 | Scree stone, 1.3 m proud |
| `AP_M6_Rock_FieldStoneStone05_SM` | 6 | D | 0.8-1.2 | 1.3 | 0.7 | 1.3 | 480 | 39 | Small scatter, 0.7 m — NOT the primary mass |
| `RF_Boulder2` | 5 | D | 1.2-1.9 | 2.1 | 0.9 | 1.9 | 54 | 32 | Small scatter rock |
| `AP_TurtleLake_Rock_LakeRock02_SM` | 2 | D | 0.9-1.3 | 1.4 | 0.8 | 1.3 | 854 | 13 | Damp-channel rock |
| `AP_TurtleLake_Rock_TurtleRock04_SM` | 2 | H | hand | 11.0 | 1.1 | 7.8 | 1,686 | 1/node | 11 m slab — hand-place as ledges and rockfall |

**B. Sentinel pines — reduce with elevation** — target **28 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_AlaskaCedar_001_2` | 20 | M | 5.2-8.3 | 5.2 | 10.9 | 0.4 | 963 | 28 | Alaskan sentinel pine, 5.2 m |
| `AP_Norway_Spruce_01` | 16 | M | 4.0-6.4 | 4.0 | 12.7 | 0.4 | 1,514 | 22 | Tall narrow spire, 4.0 m |
| `AP_Tree_04_PTree_05_SM` | 13 | M | 4.2-6.7 | 4.2 | 13.3 | 0.6 | 655 | 18 | Tallest sparse pine, 4.2 m |
| `AP_Tree_04_PTree_04_SM` | 11 | S | 15.4-26.9 | 7.7 | 17.6 | 0.8 | 1,194 | 15 | Wind-shaped pine |
| `AP_Tree_04_PTree_02_SM` | 10 | S | 18.0-31.5 | 9.0 | 17.1 | 1.3 | 2,979 | 14 | Damaged pine |
| `AP_Tree_04_PTree_01_SM_2` | 8 | S | 21.7-38.0 | 10.9 | 20.4 | 1.4 | 3,067 | 11 | Lower-slope pine |
| `AP_Tree_04_PTree_03_SM` | 8 | S | 22.6-39.6 | 11.3 | 20.9 | 1.8 | 3,043 | 11 | Very sparse pine |
| `RF_Tree1` | 7 | M | 3.9-6.3 | 3.9 | 13.3 | 0.3 | 314 | 10 | Bare elevation-limit pine, 314 tris |
| `RF_Tree2` | 4 | S | 13.2-23.1 | 6.6 | 17.8 | 0.5 | 1,204 | 6 | Thin green pine |
| `AP_WhiteFir_MD_Dead_03` | 3 | S | 12.3-21.5 | 6.2 | 14.9 | 1.1 | 1,315 | 4 | Damaged high-elevation fir |

**C. Crack-grown scrub — concentrate below overhangs and by water** — target **190 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_Neutral_Tree_A_PrickelGrass01_SM` | 24 | D | 1.2-1.8 | 1.9 | 1.2 | — | 1,087 | 228 | Pale alpine scrub |
| `RF_Bush1` | 20 | D | 0.5-0.8 | 0.6 | 1.0 | — | 4 | 190 | Upright crack-grown shrub |
| `AP_plant_001_14` | 16 | D | 1.3-1.9 | 2.2 | 1.4 | — | 28 | 152 | Dark alpine shrub |
| `RF_Bush2` | 14 | D | 0.7-1.1 | 1.2 | 1.3 | — | 8 | 133 | Low sheltered bush |
| `AP_plant_001_18` | 10 | M | 3.9-6.3 | 3.9 | 1.4 | — | 545 | 95 | Coniferous ground spray, 3.9 m |
| `TSA_Heather_B` | 8 | D | 0.8-1.2 | 1.3 | 0.8 | — | 396 | 76 | Heather — project asset GPT never used |
| `AP_Plant_003_10` | 8 | D | 1.1-1.7 | 1.9 | 1.1 | — | 208 | 76 | Low fern-like variation |

**D. Damp pockets only** — target **120 instances/ha**

| Asset | % | Tier | Spacing (m) | Foot (m) | Vis H (m) | Blocks | Tris | ≈ Count | Intent |
|---|---:|:--:|---:|---:|---:|---:|---:|---:|---|
| `AP_Neutral_Foliage_A_Weed02_SM` | 38 | D | 0.9-1.4 | 1.5 | 0.8 | — | 148 | 228 | Sparse grass in cracks |
| `Fern_02B` | 24 | D | 1.1-1.7 | 1.9 | 0.7 | — | 252 | 144 | Yellow fern in a damp pocket |
| `RF_Fern1` | 22 | D | 0.5-0.8 | 0.7 | 0.2 | — | 9 | 132 | Tiny fern |
| `AP_Neutral_Foliage_A_Weed01_SM` | 16 | M | 3.2-5.2 | 3.2 | 1.5 | — | 72 | 96 | Larger clump at the treeline transition |

**Mountain totals:** ~468 instances/ha · ~281,566 triangles/ha · ~2,340 instances across the biome.


> Every row is density-limited: no spacing rule silently starves its stratum target.

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

---

## 10. Executing this in Gaia — the field-by-field mapping

Written so this document can be handed to a session that has not seen the analysis. Field names
below were read out of the installed `GaiaCore` assembly, not from documentation, so they are the
real ones.

### 10.1 Order of operations

1. **Rebuild terrain layers.** Nothing else can be masked until they exist. Recover the 8-layer set
   from `Island_Original_TerrainData_Backup.asset` (§4).
2. **Paint the biome map** from Carlos's definition (§6.3). This *is* the biome division.
3. **Rebuild the grass detail pass** — 27 detail prototypes, per-biome densities in §4. Biome
   independent, can be done in parallel with step 2.
4. **One Gaia Spawner per biome**, one **SpawnRule per species**, grouped by stratum (§7).
5. **Spawn, walk it, retune tiers** — not metres (§3).

### 10.2 Per-species rule settings

Every row of every §7 table becomes one `Gaia.SpawnRule`:

| What §7 gives you | Gaia field | Notes |
|---|---|---|
| Spacing low end | `m_locationIncrementMin` | metres |
| Spacing high end | `m_locationIncrementMax` | metres |
| % within stratum | spawn weight / `m_minRequiredFitness` | re-normalise within the stratum |
| Clustering (§3.6) | `m_noiseMask` = `Perlin`, `m_noiseZoom`, `m_noiseStrength`, `m_noiseMaskSeed` | **unique seed per species** |
| Detail-layer density (§4) | `m_terrainDetailDensity`, `m_terrainDetailMinFitness` | grass/detail rules only |

Slope and altitude (§3.5) go on an `ImageMask` in the rule's `m_imageMasks[]` array:

| Constraint | Gaia field |
|---|---|
| Slope filter | `m_slopeMin`, `m_slopeMax` (degrees), shaped by `m_slopeMaskCurve` |
| Altitude filter | `m_heightMaskType = RelativeToSeaLevel`, then `m_seaLevelRelativeHeightMin` / `m_seaLevelRelativeHeightMax` |
| Biome mask | `m_imageMaskLocation = SpawnRule`, plus the biome's terrain layer or poly/image mask |
| Mask blend | `m_strength` |

Use `RelativeToSeaLevel`, **not** `WorldSpace` — Crest's water sits at Y=8 and the rules stay correct
if it ever moves.

Tree scale variation is on `ResourceProtoTree`: `m_spawnScale` (`Fixed` / `Fitness` / `Random` /
`FitnessRandomized`), `m_minHeight`, `m_maxHeight`, `m_heightRandomPercentage`. Use
`FitnessRandomized` with ±15-20% so a stand does not read as clones.

### 10.3 Things that will go wrong if not set

- **Align to normal must be OFF for trees** and ON for rocks, logs and root formations (§3.5). This
  is the single most visible setting in the whole pass.
- **Unique `m_noiseMaskSeed` per species.** Sharing one makes every species clump in the same
  place, which looks worse than no clustering.
- **Terrain trees silently reject MeshColliders** (see `webgl_gles3_gotchas`). Anything needing a
  real collider must spawn as a **game object**, not a terrain tree. That applies to the whole
  "Blocks" column in §7.
- **A terrain tree ignores the prefab's root transform** (see `mrm70_terrain_tree_transform_bug`).
  Check `localRotation == identity` before trusting any species placed as a terrain tree.
- **Hero rows (`H` tier) are not spawn rules.** Six ritual trees plus `AP_S_Tree_01`, `RF_Log3` and
  `AP_TurtleLake_Rock_TurtleRock04_SM` are hand-placed, one per authored node.

### 10.4 Validation after each pass

- Re-run the doc's own check: `python biomes.py` must still print
  *"Every row is density-limited"* — if it reports spacing-limited rows, a tier and a stratum target
  have started contradicting each other and the spawner will silently underfill.
- Walk the primary route and confirm 2.4-3.0 m of clearance between collidable trunks (§3).
- Check the Appendix A trip-wall props are not sitting on walking lines.
- Compare achieved counts against the **≈ Count** column, per biome.


---

## Appendix A — trip-wall risk: props that block far wider than they look

Blocking diameter divided by visible height. Anything over ~3 reads to the player as an
invisible wall: a knee-high log that hard-stops you. These need either a lowered/removed
collider, a vault, or hand placement well off the walking line.

| Asset | Visible H (m) | Block dia (m) | Ratio | Verdict |
|---|---:|---:|---:|---|
| `AP_TurtleLake_Tree_BrokenTree01_SM` | 0.44 | 3.78 | **8.6×** | Remove collider or make it vaultable |
| `RF_Log2` | 0.68 | 5.02 | **7.4×** | Remove collider or make it vaultable |
| `AP_TurtleLake_Rock_TurtleRock04_SM` | 1.15 | 7.76 | **6.7×** | Remove collider or make it vaultable |
| `AP_Tree_Break_03_SM` | 1.29 | 8.50 | **6.6×** | Remove collider or make it vaultable |
| `RF_Log1` | 0.88 | 5.16 | **5.9×** | Remove collider or make it vaultable |
| `AP_Tree_Break_02_SM` | 1.28 | 5.98 | **4.7×** | Vault, or keep off traversal lines |
| `RF_Log3` | 1.90 | 8.56 | **4.5×** | Vault, or keep off traversal lines |
| `AP_Tree_Heretic_D03` | 2.34 | 6.34 | **2.7×** | Vault, or keep off traversal lines |

## Appendix B — burial: meshes sunk below the pivot

Only the ones where it changes a placement decision (>15%% of the mesh below Y=0).
Trees are all 1-8%% (normal root sink). The **rocks and stumps are 30-50%% buried**, which
is why none of them give standing cover.

| Asset | Bounds H (m) | min.Y (m) | Visible H (m) | % buried |
|---|---:|---:|---:|---:|
| `AP_TurtleLake_Rock_GoblinRock01_SM` | 3.23 | -1.62 | **1.62** | 50% |
| `AP_TurtleLake_Rock_TurtleRock04_SM` | 2.30 | -1.15 | **1.15** | 50% |
| `AP_TurtleLake_Rock_GoblinRock02_SM` | 3.02 | -1.27 | **1.75** | 42% |
| `AP_plant_001_18` | 2.32 | -0.91 | **1.41** | 39% |
| `RF_Log3` | 3.11 | -1.21 | **1.90** | 39% |
| `AP_TurtleLake_Rock_LakeRock02_SM` | 1.38 | -0.53 | **0.85** | 38% |
| `RF_Log2` | 1.03 | -0.35 | **0.68** | 34% |
| `RF_Stump1` | 2.39 | -0.80 | **1.59** | 33% |
| `AP_TurtleLake_Tree_Stump02_SM` | 2.18 | -0.70 | **1.48** | 32% |
| `AP_GraveKeepers_C01` | 3.12 | -1.00 | **2.12** | 32% |
| `AP_Mushroom_B01` | 1.09 | -0.32 | **0.77** | 29% |
| `AP_Mushroom_C01` | 1.02 | -0.29 | **0.73** | 28% |
| `AP_Mushroom_A01_2` | 0.56 | -0.15 | **0.42** | 27% |
| `RF_Stump2` | 4.38 | -0.98 | **3.40** | 22% |
| `AP_GraveKeepers_C02` | 5.06 | -1.06 | **4.00** | 21% |
| `AP_Neutral_Foliage_A_WildFlower01_SM_JYI` | 1.46 | -0.30 | **1.16** | 21% |
| `AP_plant_001_28` | 2.75 | -0.56 | **2.19** | 20% |
| `AP_Neutral_Tree_A_PrickelGrass01_SM` | 1.43 | -0.26 | **1.17** | 18% |
| `AP_flower_001A` | 0.63 | -0.11 | **0.52** | 17% |
| `TSA_Heather_B` | 0.99 | -0.16 | **0.83** | 16% |
| `AP_Plant_003_10` | 1.31 | -0.21 | **1.10** | 16% |
| `Indigo Violet_LOD` | 0.38 | -0.06 | **0.33** | 16% |
| `Purple Violet_LOD` | 0.38 | -0.06 | **0.33** | 16% |
| `Tangerine Violet_LOD` | 0.38 | -0.06 | **0.33** | 16% |
| `VioletDaisy_LOD` | 0.13 | -0.02 | **0.11** | 15% |
| `YellowDaisy_LOD` | 0.13 | -0.02 | **0.11** | 15% |
| `AP_Neutral_Foliage_A_Weed01_SM` | 1.79 | -0.27 | **1.51** | 15% |
| `AP_GR_003GR_002_D` | 1.33 | -0.20 | **1.14** | 15% |

## Appendix C — cover value against a 1.8 m player

Collidable props only, trees under 8 m included. This is the honest answer to
"what can the player actually hide behind".

| Asset | Visible H (m) | Block dia (m) | Cover class |
|---|---:|---:|---|
| `AP_GraveKeepers_B03_2` | 26.74 | 4.00 | **Standing cover** |
| `AP_GraveKeepers_B07` | 17.15 | 4.00 | **Standing cover** |
| `AP_GraveKeepers_B01` | 16.33 | 4.00 | **Standing cover** |
| `AP_WhiteFir_MD_Dead_03` | 14.91 | 1.08 | **Standing cover** |
| `AP_GraveKeepers_B02` | 14.66 | 2.88 | **Standing cover** |
| `AP_Norway_Spruce_01` | 12.68 | 0.40 | **Standing cover** |
| `AP_GraveKeepers_B06` | 12.29 | 3.20 | **Standing cover** |
| `AP_AlaskaCedar_001_2` | 10.93 | 0.38 | **Standing cover** |
| `AP_Plant_003_07` | 10.12 | 0.24 | **Standing cover** |
| `AP_GraveKeepers_B04` | 6.04 | 0.66 | **Standing cover** |
| `AP_M6_Tree_Bushtree02_SM` | 5.75 | 2.50 | **Standing cover** |
| `RF_Sapling2` | 5.40 | 0.36 | **Standing cover** |
| `AP_Tree_Heretic_B03` | 4.96 | 3.90 | **Standing cover** |
| `AP_sunghwangdang_Tree_pagoda_01` | 4.50 | 3.84 | **Standing cover** |
| `AP_Tree_Heretic_B05` | 4.31 | 1.20 | **Standing cover** |
| `AP_M6_Rock_SeashoreWallStone01_SM` | 4.03 | 3.94 | **Standing cover** |
| `AP_GraveKeepers_C02` | 4.00 | 2.46 | **Standing cover** |
| `AP_sunghwangdang_Tree_pagoda_01_1` | 3.91 | 3.24 | **Standing cover** |
| `AP_ENV_tree_Nokmyung` | 3.88 | 0.32 | **Standing cover** |
| `RF_Stump2` | 3.40 | 1.38 | **Standing cover** |
| `AP_M6_Rock_CemeteryRock02_SM` | 2.55 | 4.74 | **Standing cover** |
| `RF_Boulder5` | 2.46 | 3.08 | **Standing cover** |
| `RF_Sapling1` | 2.42 | 0.30 | **Standing cover** |
| `AP_Tree_Heretic_D03_02` | 2.40 | 2.76 | **Standing cover** |
| `AP_Tree_Heretic_D03` | 2.34 | 6.34 | **Standing cover** |
| `AP_GraveKeepers_C01` | 2.12 | 2.82 | **Standing cover** |
| `RF_Log3` | 1.90 | 8.56 | **Standing cover** |
| `AP_TurtleLake_Rock_GoblinRock02_SM` | 1.75 | 2.08 | Crouch cover |
| `RF_Boulder4` | 1.70 | 2.14 | Crouch cover |
| `AP_Tree_Heretic_D02_01` | 1.62 | 3.40 | Crouch cover |
| `AP_TurtleLake_Rock_GoblinRock01_SM` | 1.62 | 3.52 | Crouch cover |
| `RF_Stump1` | 1.59 | 1.24 | Crouch cover |
| `AP_TurtleLake_Tree_Stump02_SM` | 1.48 | 0.72 | Crouch cover |
| `AP_Tree_Break_Root_02_SM` | 1.34 | 1.28 | Crouch cover |
| `AP_Tree_Break_03_SM` | 1.29 | 8.50 | Crouch cover |
| `AP_Tree_Break_02_SM` | 1.28 | 5.98 | Crouch cover |
| `AP_M6_Rock_FieldStoneStone06_SM` | 1.25 | 1.56 | Crouch cover |
| `AP_TurtleLake_Rock_TurtleRock04_SM` | 1.15 | 7.76 | Crouch cover |
| `RF_Boulder3` | 1.09 | 2.16 | Crouch cover |
| `AP_Tree_Break_MushroomTrunk_01_SM` | 0.94 | 1.82 | Trip / ankle |
| `RF_Boulder2` | 0.90 | 1.86 | Trip / ankle |
| `RF_Log1` | 0.88 | 5.16 | Trip / ankle |
| `AP_TurtleLake_Rock_LakeRock02_SM` | 0.85 | 1.26 | Trip / ankle |
| `RF_Boulder1` | 0.70 | 0.94 | Trip / ankle |
| `AP_M6_Rock_FieldStoneStone05_SM` | 0.68 | 1.34 | Trip / ankle |
| `RF_Log2` | 0.68 | 5.02 | Trip / ankle |
| `AP_TurtleLake_Tree_BrokenTree01_SM` | 0.44 | 3.78 | Trip / ankle |

## Appendix D — triangle cost tiers

**Most expensive meshes** (these must stay rare — see the Eerie/Heretic notes):

| Asset | Tris | Visible H (m) | Tris per metre |
|---|---:|---:|---:|
| `AP_Tree_Curse_H01_2` | 27,975 | 9.9 | 2,834 |
| `AP_GraveKeepers_B02` | 27,036 | 14.7 | 1,844 |
| `AP_GraveKeepers_B01` | 20,824 | 16.3 | 1,275 |
| `AP_Tree_Curse_K01` | 18,270 | 9.5 | 1,915 |
| `AP_GraveKeepers_B06` | 17,899 | 12.3 | 1,456 |
| `AP_GraveKeepers_B04` | 17,378 | 6.0 | 2,877 |
| `AP_GraveKeepers_B03_2` | 16,443 | 26.7 | 615 |
| `AP_Tree_Curse_J07` | 13,461 | 13.8 | 974 |
| `AP_Tree_Curse_J08` | 10,366 | 11.4 | 912 |
| `AP_Tree_10_ArgassTree_03_SM` | 9,965 | 11.2 | 893 |
| `AP_GangshiTree_2` | 9,393 | 34.8 | 270 |
| `AP_M6_Tree_MonsterTreeBark_SM_PHJ_2` | 9,304 | 12.2 | 765 |
| `AP_Tree_10_ArgassTree_SM` | 8,199 | 8.1 | 1,012 |
| `AP_Tree_Heretic_A01_2` | 7,780 | 7.9 | 982 |
| `AP_Tree_Lake_RoundTree_01_SM` | 7,253 | 15.3 | 473 |
| `AP_Tree_Oak01_SM` | 6,948 | 18.8 | 370 |

**Cheapest large trees** — these are what carry base density:

| Asset | Tris | Visible H (m) | Tris per metre |
|---|---:|---:|---:|
| `AP_ENV_tree_SaGeeSukRim` | 288 | 10.0 | 29 |
| `AP_ENV_tree_ToeMunJean` | 309 | 10.4 | 30 |
| `RF_Tree1` | 314 | 13.3 | 24 |
| `AP_Plant_003_07` | 504 | 10.1 | 50 |
| `AP_Tree_04_PTree_05_SM` | 655 | 13.3 | 49 |
| `AP_DeadTree02` | 725 | 13.0 | 56 |
| `AP_AlaskaCedar_001_2` | 963 | 10.9 | 88 |
| `RF_Tree4` | 976 | 23.9 | 41 |
| `AP_Building_exorcist_tree2` | 1,141 | 14.8 | 77 |
| `AP_Tree_04_PTree_04_SM` | 1,194 | 17.6 | 68 |
| `RF_Tree2` | 1,204 | 17.8 | 68 |
| `AP_M6_Tree_ForestTree08_SM_JYI_2` | 1,305 | 25.0 | 52 |
| `AP_WhiteFir_MD_Dead_03` | 1,315 | 14.9 | 88 |
| `AP_BC_PineTree_03` | 1,360 | 11.6 | 117 |

## Appendix E — full measured inventory

Every prefab, measured from the `Visual` child's renderer bounds. **Foot** = max(X, Z),
the number every spacing value in this document is derived from.

| Asset | Folder | Foot (m) | Bounds H | Visible H | Block dia | Tris |
|---|---|---:|---:|---:|---:|---:|
| `GFF_Grass01` | GrassFlowers | 0.50 | 0.50 | 0.50 | — | 4 |
| `GFF_Grass02` | GrassFlowers | 0.50 | 0.50 | 0.50 | — | 4 |
| `GFF_GrassFlower01` | GrassFlowers | 0.50 | 0.50 | 0.50 | — | 4 |
| `GFF_GrassFlower02` | GrassFlowers | 0.50 | 0.50 | 0.50 | — | 4 |
| `GFF_GrassFlower03` | GrassFlowers | 0.50 | 0.50 | 0.50 | — | 4 |
| `GFF_GrassFlower04` | GrassFlowers | 0.50 | 0.50 | 0.50 | — | 4 |
| `GFF_GrassFlower05` | GrassFlowers | 0.50 | 0.50 | 0.50 | — | 4 |
| `GFF_GrassFlower06` | GrassFlowers | 0.50 | 0.50 | 0.50 | — | 4 |
| `GFF_GrassFlower07` | GrassFlowers | 0.50 | 0.50 | 0.50 | — | 4 |
| `GFF_GrassFlower08` | GrassFlowers | 0.50 | 0.50 | 0.50 | — | 4 |
| `GFF_GrassFlower09` | GrassFlowers | 0.50 | 0.50 | 0.50 | — | 4 |
| `GFF_GrassFlower10` | GrassFlowers | 0.50 | 0.50 | 0.50 | — | 4 |
| `RF_Tree4` | RetroRealism | 8.61 | 25.08 | 23.94 | 1.10 | 976 |
| `RF_Tree3` | RetroRealism | 8.02 | 20.67 | 19.84 | 0.58 | 1,378 |
| `RF_Tree2` | RetroRealism | 6.60 | 18.26 | 17.78 | 0.52 | 1,204 |
| `RF_Tree1` | RetroRealism | 3.93 | 13.59 | 13.26 | 0.28 | 314 |
| `RF_Sapling2` | RetroRealism | 2.82 | 5.40 | 5.40 | 0.36 | 684 |
| `RF_Stump2` | RetroRealism | 2.72 | 4.38 | 3.40 | 1.38 | 152 |
| `RF_Boulder5` | RetroRealism | 4.58 | 2.46 | 2.46 | 3.08 | 58 |
| `RF_Sapling1` | RetroRealism | 2.42 | 2.42 | 2.42 | 0.30 | 244 |
| `RF_Bush3` | RetroRealism | 1.91 | 2.18 | 2.18 | — | 22 |
| `RF_Log3` | RetroRealism | 10.08 | 3.11 | 1.90 | 8.56 | 404 |
| `RF_Boulder4` | RetroRealism | 3.05 | 1.70 | 1.70 | 2.14 | 62 |
| `RF_Stump1` | RetroRealism | 1.94 | 2.39 | 1.59 | 1.24 | 112 |
| `RF_Bush2` | RetroRealism | 1.19 | 1.26 | 1.26 | — | 8 |
| `RF_Boulder3` | RetroRealism | 2.78 | 1.09 | 1.09 | 2.16 | 52 |
| `RF_Bush1` | RetroRealism | 0.62 | 0.97 | 0.97 | — | 4 |
| `RF_Boulder2` | RetroRealism | 2.07 | 0.90 | 0.90 | 1.86 | 54 |
| `RF_Log1` | RetroRealism | 5.16 | 0.88 | 0.88 | 5.16 | 112 |
| `RF_Boulder1` | RetroRealism | 0.98 | 0.70 | 0.70 | 0.94 | 84 |
| `RF_Log2` | RetroRealism | 6.13 | 1.03 | 0.68 | 5.02 | 230 |
| `RF_Fern2` | RetroRealism | 1.34 | 0.53 | 0.53 | — | 33 |
| `RF_Fern1` | RetroRealism | 0.73 | 0.23 | 0.23 | — | 9 |
| `TSA_BushDry_B` | TerrainSampleAssets | 1.20 | 1.11 | 1.09 | — | 792 |
| `TSA_BushDry_A` | TerrainSampleAssets | 0.92 | 1.01 | 1.00 | — | 640 |
| `TSA_Heather_A` | TerrainSampleAssets | 1.10 | 1.00 | 0.93 | — | 268 |
| `TSA_Plant_B` | TerrainSampleAssets | 0.24 | 0.95 | 0.89 | — | 145 |
| `TSA_Plant_A` | TerrainSampleAssets | 0.49 | 0.95 | 0.88 | — | 260 |
| `TSA_Bush_A` | TerrainSampleAssets | 0.69 | 0.88 | 0.87 | — | 362 |
| `TSA_Heather_B` | TerrainSampleAssets | 1.31 | 0.99 | 0.83 | — | 396 |
| `TSA_Bush_B` | TerrainSampleAssets | 0.82 | 0.83 | 0.81 | — | 273 |
| `TSA_Plant_C` | TerrainSampleAssets | 0.31 | 0.83 | 0.78 | — | 128 |
| `TSA_Grass_D` | TerrainSampleAssets | 0.94 | 0.76 | 0.74 | — | 1,268 |
| `TSA_GrassDry_C` | TerrainSampleAssets | 0.58 | 0.71 | 0.68 | — | 122 |
| `TSA_Grass_C` | TerrainSampleAssets | 0.95 | 0.60 | 0.60 | — | 1,002 |
| `TSA_GrassDry_A` | TerrainSampleAssets | 0.41 | 0.55 | 0.53 | — | 173 |
| `TSA_Plant_D` | TerrainSampleAssets | 0.22 | 0.44 | 0.42 | — | 61 |
| `TSA_Fern_C` | TerrainSampleAssets | 1.00 | 0.41 | 0.38 | — | 384 |
| `TSA_GrassDry_B` | TerrainSampleAssets | 0.37 | 0.38 | 0.37 | — | 168 |
| `TSA_Grass_B` | TerrainSampleAssets | 1.04 | 0.39 | 0.35 | — | 1,136 |
| `TSA_Fern_A` | TerrainSampleAssets | 0.94 | 0.34 | 0.31 | — | 360 |
| `TSA_Grass_A` | TerrainSampleAssets | 0.32 | 0.30 | 0.26 | — | 128 |
| `TSA_Fern_B` | TerrainSampleAssets | 0.92 | 0.26 | 0.24 | — | 464 |
| `AP_Tree_04_M01_03_SM` | VegetationPrefabs | 20.46 | 42.58 | 41.93 | 2.80 | 3,801 |
| `AP_Tree_04_GTree01_04_SM` | VegetationPrefabs | 17.04 | 39.78 | 39.59 | 1.92 | 4,217 |
| `AP_Tree_04_GTree01_05_SM` | VegetationPrefabs | 11.89 | 39.08 | 38.49 | 1.96 | 4,146 |
| `AP_Tree_04_M01_02_SM` | VegetationPrefabs | 15.32 | 38.04 | 37.64 | 2.68 | 4,432 |
| `AP_Tree_04_GTree01_03_SM` | VegetationPrefabs | 12.27 | 37.88 | 37.55 | 2.02 | 4,532 |
| `AP_GangshiTree_2` | VegetationPrefabs | 29.21 | 36.72 | 34.77 | 5.10 | 9,393 |
| `AP_S_Tree_01` | VegetationPrefabs | 30.10 | 36.85 | 33.83 | 6.44 | 2,530 |
| `AP_Tree_04_GTree01_01_SM_2` | VegetationPrefabs | 17.68 | 34.02 | 33.14 | 1.92 | 3,356 |
| `AP_Tree_04_GTree01_02_SM` | VegetationPrefabs | 14.35 | 31.81 | 31.09 | 1.90 | 2,842 |
| `AP_Tree_Conifir_A_01_SM_2` | VegetationPrefabs | 12.84 | 31.28 | 30.88 | 1.50 | 3,082 |
| `AP_Tree_Conifir_A_02_SM` | VegetationPrefabs | 12.72 | 30.97 | 30.64 | 1.42 | 3,082 |
| `AP_Tree_04_M01_01_SM_2` | VegetationPrefabs | 17.75 | 28.99 | 28.51 | 2.38 | 3,846 |
| `AP_Tree_04_GTree01_06_SM` | VegetationPrefabs | 20.28 | 28.56 | 27.94 | 2.22 | 4,146 |
| `AP_FallTree_01_SM_1` | VegetationPrefabs | 11.11 | 28.22 | 27.60 | 0.80 | 2,890 |
| `AP_Tree_Burnt_04_SM` | VegetationPrefabs | 8.23 | 27.58 | 27.45 | 1.68 | 3,531 |
| `AP_GraveKeepers_B03_2` | VegetationPrefabs | 24.96 | 27.07 | 26.74 | 4.00 | 16,443 |
| `AP_FallTree_01_SM` | VegetationPrefabs | 8.90 | 25.68 | 25.14 | 0.80 | 2,890 |
| `AP_M6_Tree_ForestTree08_SM_JYI_2` | VegetationPrefabs | 19.33 | 25.37 | 24.99 | 2.04 | 1,305 |
| `AP_Tree_04_M01_04_SM` | VegetationPrefabs | 13.53 | 23.57 | 23.36 | 1.74 | 2,978 |
| `AP_Tree_04_M01_05_SM` | VegetationPrefabs | 16.58 | 23.12 | 23.04 | 2.44 | 2,710 |
| `AP_Tree_Blackpoplar01_SM` | VegetationPrefabs | 15.08 | 21.79 | 21.62 | 2.04 | 3,057 |
| `AP_Tree_04_PTree_03_SM` | VegetationPrefabs | 11.30 | 21.01 | 20.89 | 1.78 | 3,043 |
| `AP_Tree_04_PTree_01_SM_2` | VegetationPrefabs | 10.86 | 20.78 | 20.45 | 1.38 | 3,067 |
| `AP_Tree_WNT_M01_01_SM_3` | VegetationPrefabs | 16.60 | 20.32 | 19.74 | 1.76 | 2,949 |
| `AP_Tree_AUT_White_A_02_SM` | VegetationPrefabs | 10.47 | 19.36 | 19.16 | 0.62 | 3,505 |
| `AP_Tree_AUT_White_A_03_SM` | VegetationPrefabs | 9.68 | 19.26 | 18.91 | 0.62 | 2,823 |
| `AP_Tree_Oak01_SM` | VegetationPrefabs | 11.34 | 20.18 | 18.77 | 1.62 | 6,948 |
| `AP_Tree_04_PTree_04_SM` | VegetationPrefabs | 7.69 | 17.63 | 17.63 | 0.80 | 1,194 |
| `AP_Tree_WNT_03_Bark_01_SM_2` | VegetationPrefabs | 6.62 | 17.88 | 17.47 | 0.82 | 3,607 |
| `AP_GraveKeepers_B07` | VegetationPrefabs | 20.79 | 17.33 | 17.15 | 4.00 | 5,929 |
| `AP_Tree_04_PTree_02_SM` | VegetationPrefabs | 9.00 | 17.29 | 17.06 | 1.26 | 2,979 |
| `AP_Tree_WNT_M_03_SM` | VegetationPrefabs | 13.36 | 17.20 | 16.66 | 1.20 | 2,872 |
| `AP_GraveKeepers_B01` | VegetationPrefabs | 18.58 | 16.47 | 16.33 | 4.00 | 20,824 |
| `AP_FallTree_02_SM` | VegetationPrefabs | 8.13 | 17.05 | 15.88 | 0.40 | 2,535 |
| `AP_Tree_Deadtree06_SM` | VegetationPrefabs | 9.29 | 15.83 | 15.51 | 3.46 | 6,837 |
| `AP_Tree_Lake_RoundTree_01_SM` | VegetationPrefabs | 12.77 | 15.70 | 15.34 | 2.68 | 7,253 |
| `AP_WhiteFir_MD_Dead_03` | VegetationPrefabs | 6.15 | 15.70 | 14.91 | 1.08 | 1,315 |
| `AP_Building_exorcist_tree2` | VegetationPrefabs | 18.08 | 15.56 | 14.83 | 1.56 | 1,141 |
| `AP_GraveKeepers_B02` | VegetationPrefabs | 14.96 | 14.83 | 14.66 | 2.88 | 27,036 |
| `AP_Tree_Curse_J07` | VegetationPrefabs | 14.82 | 14.24 | 13.82 | 2.06 | 13,461 |
| `AP_Tree_DeadTree01_SM` | VegetationPrefabs | 18.33 | 14.10 | 13.59 | 1.70 | 1,962 |
| `AP_Tree_04_PTree_05_SM` | VegetationPrefabs | 4.20 | 13.33 | 13.33 | 0.62 | 655 |
| `AP_DeadTree02` | VegetationPrefabs | 16.15 | 12.78 | 13.02 | 1.48 | 725 |
| `AP_Norway_Spruce_01` | VegetationPrefabs | 4.00 | 12.93 | 12.68 | 0.40 | 1,514 |
| `AP_BC_PineTree_02` | VegetationPrefabs | 6.70 | 13.10 | 12.44 | 0.88 | 2,338 |
| `AP_DeadTree03` | VegetationPrefabs | 16.30 | 12.46 | 12.31 | 2.10 | 2,211 |
| `AP_GraveKeepers_B06` | VegetationPrefabs | 14.37 | 12.55 | 12.29 | 3.20 | 17,899 |
| `AP_M6_Tree_MonsterTreeBark_SM_PHJ_2` | VegetationPrefabs | 9.88 | 12.27 | 12.17 | 6.74 | 9,304 |
| `AP_Tree_10_ArgassTree_04_SM` | VegetationPrefabs | 12.79 | 11.87 | 11.60 | 1.72 | 6,652 |
| `AP_BC_PineTree_03` | VegetationPrefabs | 9.80 | 12.62 | 11.58 | 0.78 | 1,360 |
| `AP_DeadTree04` | VegetationPrefabs | 12.76 | 11.54 | 11.44 | 2.64 | 1,803 |
| `AP_Tree_Curse_J08` | VegetationPrefabs | 10.05 | 11.78 | 11.36 | 1.18 | 10,366 |
| `AP_Tree_color_001_01_2` | VegetationPrefabs | 11.59 | 11.63 | 11.26 | 2.16 | 1,428 |
| `AP_Tree_10_ArgassTree_03_SM` | VegetationPrefabs | 9.64 | 11.23 | 11.16 | 2.26 | 9,965 |
| `AP_AlaskaCedar_001_2` | VegetationPrefabs | 5.19 | 10.93 | 10.93 | 0.38 | 963 |
| `AP_Tree_Juniper03_SMIK` | VegetationPrefabs | 11.55 | 11.79 | 10.73 | 2.04 | 2,197 |
| `AP_Tree_Juniper02_SMIK` | VegetationPrefabs | 8.41 | 11.21 | 10.62 | 1.36 | 4,052 |
| `AP_ENV_tree_ToeMunJean` | VegetationPrefabs | 7.86 | 10.68 | 10.42 | 0.80 | 309 |
| `AP_Plant_003_07` | VegetationPrefabs | 7.62 | 10.26 | 10.12 | 0.24 | 504 |
| `AP_ENV_tree_SaGeeSukRim` | VegetationPrefabs | 8.62 | 10.26 | 9.99 | 0.58 | 288 |
| `AP_Tree_Curse_H01_2` | VegetationPrefabs | 15.63 | 10.49 | 9.87 | 3.68 | 27,975 |
| `AP_Tree_Curse_K01` | VegetationPrefabs | 9.79 | 10.49 | 9.54 | 3.14 | 18,270 |
| `AP_Tree_color_001_03` | VegetationPrefabs | 10.08 | 9.55 | 9.42 | 1.80 | 1,460 |
| `AP_Tree_10_ArgassTree_02_SM` | VegetationPrefabs | 8.10 | 9.45 | 9.32 | 2.00 | 6,349 |
| `AP_M6_Tree_Bushtree01_SM` | VegetationPrefabs | 9.66 | 8.28 | 8.13 | 3.24 | 966 |
| `AP_Tree_10_ArgassTree_SM` | VegetationPrefabs | 7.59 | 8.19 | 8.10 | 1.44 | 8,199 |
| `AP_Tree_Heretic_A01_2` | VegetationPrefabs | 8.00 | 8.22 | 7.92 | 2.72 | 7,780 |
| `AP_Tree_Dry_D01` | VegetationPrefabs | 7.80 | 8.23 | 7.67 | 2.78 | 2,243 |
| `AP_GraveKeepers_B04` | VegetationPrefabs | 6.37 | 6.07 | 6.04 | 0.66 | 17,378 |
| `AP_M6_Tree_Bushtree02_SM` | VegetationPrefabs | 12.57 | 5.95 | 5.75 | 2.50 | 731 |
| `AP_Tree_Heretic_B03` | VegetationPrefabs | 3.88 | 5.12 | 4.96 | 3.90 | 1,192 |
| `AP_sunghwangdang_Tree_pagoda_01` | VegetationPrefabs | 4.45 | 4.83 | 4.50 | 3.84 | 2,668 |
| `AP_Tree_Heretic_B05` | VegetationPrefabs | 1.66 | 4.43 | 4.31 | 1.20 | 544 |
| `AP_M6_Rock_SeashoreWallStone01_SM` | VegetationPrefabs | 4.80 | 4.50 | 4.03 | 3.94 | 1,600 |
| `AP_GraveKeepers_C02` | VegetationPrefabs | 2.49 | 5.06 | 4.00 | 2.46 | 2,620 |
| `AP_sunghwangdang_Tree_pagoda_01_1` | VegetationPrefabs | 3.57 | 4.22 | 3.91 | 3.24 | 3,645 |
| `AP_ENV_tree_Nokmyung` | VegetationPrefabs | 3.53 | 3.96 | 3.88 | 0.32 | 289 |
| `AP_M6_Rock_CemeteryRock02_SM` | VegetationPrefabs | 4.75 | 2.69 | 2.55 | 4.74 | 3,394 |
| `AP_Tree_Heretic_D03_02` | VegetationPrefabs | 12.18 | 2.60 | 2.40 | 2.76 | 4,170 |
| `AP_Tree_Heretic_D03` | VegetationPrefabs | 15.04 | 2.41 | 2.34 | 6.34 | 4,500 |
| `AP_plant_001_28` | VegetationPrefabs | 3.18 | 2.75 | 2.19 | — | 138 |
| `AP_GraveKeepers_C01` | VegetationPrefabs | 2.76 | 3.12 | 2.12 | 2.82 | 2,494 |
| `AP_Nest_B01` | VegetationPrefabs | 7.72 | 1.98 | 1.98 | — | 451 |
| `AP_TurtleLake_Rock_GoblinRock02_SM` | VegetationPrefabs | 3.90 | 3.02 | 1.75 | 2.08 | 1,370 |
| `AP_plant_001_13` | VegetationPrefabs | 4.08 | 1.85 | 1.71 | — | 282 |
| `AP_Tree_Heretic_D02_01` | VegetationPrefabs | 12.21 | 1.73 | 1.62 | 3.40 | 4,668 |
| `AP_TurtleLake_Rock_GoblinRock01_SM` | VegetationPrefabs | 4.52 | 3.23 | 1.62 | 3.52 | 1,638 |
| `AP_Neutral_Foliage_A_Weed01_SM` | VegetationPrefabs | 3.25 | 1.79 | 1.51 | — | 72 |
| `AP_TurtleLake_Tree_Stump02_SM` | VegetationPrefabs | 3.90 | 2.18 | 1.48 | 0.72 | 2,208 |
| `AP_plant_001_18` | VegetationPrefabs | 3.94 | 2.32 | 1.41 | — | 545 |
| `AP_plant_001_14` | VegetationPrefabs | 2.16 | 1.46 | 1.39 | — | 28 |
| `AP_Tree_Break_Root_02_SM` | VegetationPrefabs | 3.03 | 1.44 | 1.34 | 1.28 | 509 |
| `AP_Tree_Break_03_SM` | VegetationPrefabs | 9.30 | 1.37 | 1.29 | 8.50 | 1,238 |
| `AP_Tree_Break_02_SM` | VegetationPrefabs | 6.34 | 1.38 | 1.28 | 5.98 | 1,252 |
| `AP_Samakyo_flower_003` | VegetationPrefabs | 2.27 | 1.42 | 1.26 | — | 288 |
| `AP_M6_Rock_FieldStoneStone06_SM` | VegetationPrefabs | 1.56 | 1.37 | 1.25 | 1.56 | 1,008 |
| `AP_Plant_001_08` | VegetationPrefabs | 1.62 | 1.22 | 1.18 | — | 228 |
| `AP_Neutral_Tree_A_PrickelGrass01_SM` | VegetationPrefabs | 1.95 | 1.43 | 1.17 | — | 1,087 |
| `AP_Neutral_Foliage_A_WildFlower01_SM_JYI` | VegetationPrefabs | 1.52 | 1.46 | 1.16 | — | 52 |
| `AP_TurtleLake_Rock_TurtleRock04_SM` | VegetationPrefabs | 10.96 | 2.30 | 1.15 | 7.76 | 1,686 |
| `AP_GR_003GR_002_D` | VegetationPrefabs | 3.09 | 1.33 | 1.14 | — | 1,121 |
| `AP_Plant_003_10` | VegetationPrefabs | 1.89 | 1.31 | 1.10 | — | 208 |
| `AP_Tree_Break_MushroomTrunk_01_SM` | VegetationPrefabs | 2.31 | 1.03 | 0.94 | 1.82 | 801 |
| `AP_TurtleLake_Rock_LakeRock02_SM` | VegetationPrefabs | 1.43 | 1.38 | 0.85 | 1.26 | 854 |
| `AP_Mushroom_B01` | VegetationPrefabs | 4.55 | 1.09 | 0.77 | — | 1,036 |
| `AP_Neutral_Foliage_A_Weed02_SM` | VegetationPrefabs | 1.50 | 0.89 | 0.77 | — | 148 |
| `AP_flower_001_11` | VegetationPrefabs | 0.69 | 0.84 | 0.76 | — | 52 |
| `AP_Flower_001_09` | VegetationPrefabs | 1.37 | 0.77 | 0.74 | — | 148 |
| `AP_flower_001_10` | VegetationPrefabs | 0.68 | 0.82 | 0.73 | — | 52 |
| `AP_Mushroom_C01` | VegetationPrefabs | 4.33 | 1.02 | 0.73 | — | 1,114 |
| `AP_flower_001_12` | VegetationPrefabs | 0.68 | 0.77 | 0.69 | — | 52 |
| `AP_M6_Rock_FieldStoneStone05_SM` | VegetationPrefabs | 1.34 | 0.68 | 0.68 | 1.34 | 480 |
| `Fern_01B` | VegetationPrefabs | 1.91 | 0.71 | 0.67 | — | 252 |
| `Fern_02B` | VegetationPrefabs | 1.91 | 0.71 | 0.67 | — | 252 |
| `African_violet_blue_LOD` | VegetationPrefabs | 2.17 | 0.65 | 0.66 | — | 300 |
| `African_violet_LOD` | VegetationPrefabs | 2.09 | 0.63 | 0.64 | — | 300 |
| `Blue Aster_LOD` | VegetationPrefabs | 0.52 | 0.64 | 0.64 | — | 355 |
| `Orange Aster_LOD` | VegetationPrefabs | 0.52 | 0.64 | 0.64 | — | 355 |
| `Purple Aster_LOD` | VegetationPrefabs | 0.52 | 0.64 | 0.64 | — | 351 |
| `Fern_03A` | VegetationPrefabs | 2.27 | 0.67 | 0.61 | — | 224 |
| `AP_Flower_001_08` | VegetationPrefabs | 1.06 | 0.62 | 0.60 | — | 146 |
| `CoralBells_green` | VegetationPrefabs | 1.00 | 0.63 | 0.60 | — | 142 |
| `AP_Tree_Dry_N02` | VegetationPrefabs | 5.25 | 0.64 | 0.59 | — | 1,627 |
| `AP_flower_001A` | VegetationPrefabs | 4.94 | 0.63 | 0.52 | — | 730 |
| `Fern_01A` | VegetationPrefabs | 1.84 | 0.50 | 0.48 | — | 156 |
| `Fern_02A` | VegetationPrefabs | 1.84 | 0.50 | 0.48 | — | 156 |
| `AP_TurtleLake_Tree_BrokenTree01_SM` | VegetationPrefabs | 4.20 | 0.46 | 0.44 | 3.78 | 489 |
| `AP_Mushroom_A01_2` | VegetationPrefabs | 1.10 | 0.56 | 0.42 | — | 646 |
| `YellowAfricanDaisy_LOD` | VegetationPrefabs | 0.67 | 0.43 | 0.42 | — | 937 |
| `Indigo Violet_LOD` | VegetationPrefabs | 0.81 | 0.38 | 0.33 | — | 156 |
| `Purple Violet_LOD` | VegetationPrefabs | 0.81 | 0.38 | 0.33 | — | 156 |
| `Tangerine Violet_LOD` | VegetationPrefabs | 0.81 | 0.38 | 0.33 | — | 156 |
| `CaliforniaPoppy_01_LOD` | VegetationPrefabs | 0.81 | 0.23 | 0.21 | — | 432 |
| `BlueEyeGrass_01_LOD` | VegetationPrefabs | 0.93 | 0.21 | 0.20 | — | 288 |
| `CupcakeWhite_01_LOD` | VegetationPrefabs | 1.03 | 0.11 | 0.11 | — | 312 |
| `VioletDaisy_LOD` | VegetationPrefabs | 0.41 | 0.13 | 0.11 | — | 302 |
| `YellowDaisy_LOD` | VegetationPrefabs | 0.41 | 0.13 | 0.11 | — | 446 |
