# Aanniarvik Island — vegetation distribution brief

**You are being asked to design the vegetation distribution for a first-person horror game's
island.** This document gives you the engine mechanics, the measured dimensions of every asset, and
the exact configuration that is live right now. Screenshots will be provided separately.

Everything in this brief was measured out of the project. Nothing is estimated.

---

## 1. What we need from you

A per-biome distribution for the **95 prefabs listed in §7**: which species belong in which biome,
at what spacing, at what scale range, and on what terrain.

**The previous version of this list was made without knowing how big any of these objects are.**
That is the single thing this document fixes. The trees in this project range from **0.44 m to
41.93 m tall** — a factor of 95 between the smallest and largest — and the earlier pass treated
many 20–35 m trees as though they were ordinary 8–12 m ones, and several multi-metre patch meshes
as though they were single small flowers. The result reads flat and repetitive in game.

Output format is specified in §8.

---

## 2. Scale reference — the player

Every size in this document is in **metres**, and every judgement about whether something reads as
big, blocks a path, or can be seen over should be made against the player capsule:

| | |
|---|---|
| **Player height** | **1.80 m** |
| **Player width** | **0.80 m** (capsule radius 0.40 m) |
| Eye height | ~1.65 m |
| View | first person |

So: a 1.0 m object comes to the player's waist. A 1.8 m object is exactly eye level. A 20 m tree is
**11× the player's height**. A 40 m tree is 22×.

**Two measurement conventions used throughout, both of which matter:**

- **Visible height** is the height of the mesh *above ground*, not the raw mesh height. 68 of these
  assets have pivots sunk below Y=0 — trees by 1–8%, rocks and stumps by **30–50%**. A rock whose
  mesh is 3.23 m tall may show only 1.62 m. Visible height is what the player sees, so it is the
  number given.
- **Footprint** is the widest horizontal dimension of the mesh — the canopy or patch width, not the
  trunk. This is the number that determines spacing, because two trees 6 m apart with 20 m canopies
  are one indistinguishable green mass.

**Collider radius is listed separately** and is a different thing: it is how much of the ground the
object physically blocks the player from walking through. It is much smaller than the footprint for
a tree (trunk, not canopy). Do not use it for spacing; use it to judge navigability.

---

## 3. The island

| | |
|---|---|
| Terrain | **1024 × 1024 m** (1.05 km²) |
| Land above sea level | **64.2 hectares** (0.642 km², ~61% of the terrain is sea) |
| Sea level | Y = 8 m |
| Max elevation | ~1016 m of range available; actual peaks are far lower |
| Biomes | 9, painted as masks (see §6) |

**Measured slope distribution of the land** (this drives everything, see §6.2):

| Slope | % of land | Cumulative |
|---|---:|---:|
| 0–9° | 29.4% | 29.4% |
| 10–19° | 42.3% | 71.7% |
| 20–29° | 18.3% | 89.9% |
| 30–39° | 6.1% | 96.1% |
| 40°+ | 3.9% | 100% |

The island is **mostly gentle-to-moderate hillside**. Only 9.9% of the land is flatter than 5°.
The bulk of it — 42% — sits between 10° and 20°.

---

## 4. How placement actually works

The engine is **Unity 6.3 with Gaia Pro**. Understanding these five mechanics will make your numbers
land correctly.

**4.1 — Species are placed independently, one at a time.**
Each species gets its own placement pass over the island using Poisson-disc sampling. It scatters
points no closer together than a per-species **spacing** value (a min–max range in metres, randomised
per point).

**4.2 — Spacing is per-species, NOT global.** This is the most important mechanic to understand.
If species A is spaced at 8 m and species B is spaced at 8 m, an A and a B can still land **0.5 m
apart**, because neither pass knows about the other. Spacing controls how far apart *two of the same
species* are. Density of the biome as a whole is the sum of all its species' passes.

Consequence: **the number of species in a biome multiplies its density.** Ten species at 10 m
spacing is a far denser forest than three species at 10 m spacing. Budget accordingly.

**4.3 — Biomes are painted masks, and they overlap the whole island.**
Each biome is a painted region. A species assigned to a biome spawns only inside that mask. Biome
regions do not have hard borders — they blend.

**4.4 — Every placement is also filtered by terrain criteria.**
Independently of the mask, each species carries a **max slope** (degrees, 0–90) and a height range.
A point that fails these is rejected. See §6.2 — the current values here are the main problem.

**4.5 — Scale range randomises each instance.**
Each species has a min and max uniform scale. Currently **almost every species is locked at
1.00–1.00**, i.e. every instance of a given tree is dimensionally identical to every other one. This
is a large part of why the forest reads as repetitive, and it is cheap to fix — a 0.85–1.20 range
costs nothing and breaks up the silhouette.

---

## 5. Scope — what is IN and what is OUT

### IN SCOPE: the 95 prefabs in §7

These are the objects that **have colliders and physically occupy the world**. Trees, dead trees,
rocks, boulders, stumps, logs, large shrubs, and the larger patch meshes. They are spawned as real
GameObjects. They block the player, cast shadows, and define the shape of every space.

**This is what we need you to distribute.**

### OUT OF SCOPE: grass, small flowers, foliage, ground cover

Do not plan for these and do not include them in your output.

They are handled by a **completely different system** — Unity's terrain detail layer, which is a
per-species density map painted over the terrain rather than a set of placed objects. It is
GPU-instanced, has **no colliders at all**, renders only within a radius of the player, and is tuned
by coverage percentage rather than by object spacing. None of your spacing or weight decisions would
apply to it.

We have already built 72 prefabs for that layer (grass tufts, ferns, small plants, wildflowers,
clover, weeds, mushrooms, moss) and we will set its density ourselves.

**So: assume the ground will be covered in grass and small foliage. You do not need to place any.**
Your job is everything above ankle height that the player can bump into.

Note this is why you will not find small flowers in the §7 list even though the project owns plenty
— they were moved out of scope deliberately, not lost.

---

## 6. The current setup — what is live right now

9 biome spawners, **78 active placement rules**, using **62 distinct prefabs**.

### 6.1 Live configuration, biome by biome

Spacing is the Poisson min–max in metres. Max slope is the terrain filter in degrees. Scale is
locked at 1.00 everywhere except one entry.

**AutumnForest** — 9 species

| Prefab | Spacing (m) | Max slope | Scale | Visible height (m) | Footprint (m) |
|---|---|---:|---|---:|---:|
| `AP_FallTree_01_SM_1` | 11.1–17.8 | 8.6° | 1.00–1.00 | 27.60 | 11.11 |
| `AP_FallTree_01_SM` | 8.9–14.2 | 7.5° | 1.00–1.00 | 25.14 | 8.90 |
| `AP_Tree_Blackpoplar01_SM` | 30.2–52.8 | 8.8° | 1.00–1.00 | 21.62 | 15.08 |
| `AP_Tree_AUT_White_A_02_SM` | 10.5–16.8 | 7.1° | 1.00–1.00 | 19.16 | 10.47 |
| `AP_Tree_AUT_White_A_03_SM` | 9.7–15.5 | 8.9° | 1.00–1.00 | 18.91 | 9.68 |
| `AP_Tree_Oak01_SM` | 22.7–39.7 | 6.6° | 1.00–1.00 | 18.77 | 11.34 |
| `AP_FallTree_02_SM` | 8.1–13.0 | 7.8° | 1.00–1.00 | 15.88 | 8.13 |
| `AP_Tree_color_001_01_2` | 11.6–18.5 | 6.9° | 1.00–1.00 | 11.26 | 11.59 |
| `AP_Tree_color_001_03` | 10.1–16.1 | 5.5° | 1.00–1.00 | 9.42 | 10.08 |

**Beach** — 8 species

| Prefab | Spacing (m) | Max slope | Scale | Visible height (m) | Footprint (m) |
|---|---|---:|---|---:|---:|
| `AP_M6_Rock_SeashoreWallStone01_SM` | 4.8–7.7 | 6.7° | 1.00–1.00 | 4.03 | 4.80 |
| `RF_Boulder5` | 9.2–16.0 | 8.3° | 1.00–1.00 | 2.46 | 4.58 |
| `AP_TurtleLake_Rock_GoblinRock02_SM` | 7.8–13.7 | 9.2° | 1.00–1.00 | 1.75 | 3.90 |
| `AP_M6_Rock_FieldStoneStone06_SM` | 6.4–10.0 | 10.0° | 1.00–1.00 | 1.25 | 1.56 |
| `RF_Boulder2` | 8.4–13.2 | 9.9° | 1.00–1.00 | 0.90 | 2.07 |
| `AP_TurtleLake_Rock_LakeRock02_SM` | 11.2–18.4 | 5.6° | 1.00–1.00 | 0.85 | 1.43 |
| `RF_Boulder1` | 8.0–12.8 | 6.4° | 1.00–1.00 | 0.70 | 0.98 |
| `AP_M6_Rock_FieldStoneStone05_SM` | 10.4–16.8 | 7.8° | 1.00–1.00 | 0.68 | 1.34 |

**EerieForest** — 9 species

| Prefab | Spacing (m) | Max slope | Scale | Visible height (m) | Footprint (m) |
|---|---|---:|---|---:|---:|
| `AP_Tree_Burnt_04_SM` | 8.2–13.2 | 8.5° | 1.00–1.00 | 27.45 | 8.23 |
| `AP_Tree_WNT_M01_01_SM_3` | 16.6–26.6 | 7.5° | 1.00–1.00 | 19.74 | 16.60 |
| `AP_Tree_WNT_03_Bark_01_SM_2` | 4.0–6.0 | 7.5° | 1.00–1.00 | 17.47 | 6.62 |
| `AP_Tree_WNT_M_03_SM` | 13.4–21.4 | 6.9° | 1.00–1.00 | 16.66 | 13.36 |
| `AP_WhiteFir_MD_Dead_03` | 6.2–9.8 | 9.6° | 1.00–1.00 | 14.91 | 6.15 |
| `AP_Tree_DeadTree01_SM` | 11.0–16.5 | 7.8° | 1.00–1.00 | 13.59 | 18.33 |
| `AP_DeadTree02` | 9.7–14.5 | 8.8° | 1.00–1.00 | 13.02 | 16.15 |
| `AP_DeadTree03` | 9.8–14.7 | 8.4° | 1.00–1.00 | 12.31 | 16.30 |
| `AP_DeadTree04` | 7.7–11.5 | 6.9° | 1.00–1.00 | 11.44 | 12.76 |

**FlakTower** — 9 species

| Prefab | Spacing (m) | Max slope | Scale | Visible height (m) | Footprint (m) |
|---|---|---:|---|---:|---:|
| `AP_FallTree_01_SM_1` | 22.2–38.9 | 9.3° | 1.00–1.00 | 27.60 | 11.11 |
| `AP_FallTree_01_SM` | 17.8–31.2 | 8.9° | 1.00–1.00 | 25.14 | 8.90 |
| `RF_Tree3` | 16.0–28.1 | 6.4° | 1.00–1.00 | 19.84 | 8.02 |
| `AP_Tree_04_PTree_05_SM` | 8.4–14.7 | 7.9° | 1.00–1.00 | 13.33 | 4.20 |
| `AP_Norway_Spruce_01` | 8.0–14.0 | 5.5° | 1.00–1.00 | 12.68 | 4.00 |
| `AP_BC_PineTree_02` | 13.4–23.4 | 9.2° | 1.00–1.00 | 12.44 | 6.70 |
| `AP_AlaskaCedar_001_2` | 10.4–18.2 | 9.9° | 1.00–1.00 | 10.93 | 5.19 |
| `AP_ENV_tree_ToeMunJean` | 7.9–12.6 | 9.5° | 1.00–1.00 | 10.42 | 7.86 |
| `AP_ENV_tree_SaGeeSukRim` | 8.6–13.8 | 8.8° | 1.00–1.00 | 9.99 | 8.62 |

**Forest** — 11 species

| Prefab | Spacing (m) | Max slope | Scale | Visible height (m) | Footprint (m) |
|---|---|---:|---|---:|---:|
| `AP_Tree_04_M01_03_SM` | 40.9–71.6 | 9.0° | 1.00–1.00 | 41.93 | 20.46 |
| `AP_Tree_04_GTree01_04_SM` | 34.1–59.6 | 6.8° | 1.00–1.00 | 39.59 | 17.04 |
| `AP_Tree_04_GTree01_05_SM` | 23.8–41.6 | 5.5° | 1.00–1.00 | 38.49 | 11.89 |
| `AP_Tree_04_M01_02_SM` | 30.6–53.6 | 6.8° | 1.00–1.00 | 37.64 | 15.32 |
| `AP_Tree_04_GTree01_03_SM` | 12.3–19.6 | 7.4° | 1.00–1.00 | 37.55 | 12.27 |
| `AP_Tree_04_GTree01_01_SM_2` | 17.7–28.3 | 5.4° | 1.45–1.45 | 33.14 | 17.68 |
| `AP_Tree_04_GTree01_02_SM` | 14.3–23.0 | 9.6° | 1.00–1.00 | 31.09 | 14.35 |
| `AP_Tree_Conifir_A_01_SM_2` | 12.8–20.5 | 5.3° | 1.00–1.00 | 30.88 | 12.84 |
| `AP_Tree_Conifir_A_02_SM` | 12.7–20.4 | 8.9° | 1.00–1.00 | 30.64 | 12.72 |
| `AP_Tree_04_M01_01_SM_2` | 17.8–28.4 | 7.6° | 1.00–1.00 | 28.51 | 17.75 |
| `AP_Tree_04_GTree01_06_SM` | 40.6–71.0 | 9.6° | 1.00–1.00 | 27.94 | 20.28 |

**Fountain** — 8 species

| Prefab | Spacing (m) | Max slope | Scale | Visible height (m) | Footprint (m) |
|---|---|---:|---|---:|---:|
| `AP_Tree_Lake_RoundTree_01_SM` | 25.5–44.7 | 6.6° | 1.00–1.00 | 15.34 | 12.77 |
| `AP_Tree_10_ArgassTree_04_SM` | 12.8–20.5 | 8.7° | 1.00–1.00 | 11.60 | 12.79 |
| `AP_Tree_10_ArgassTree_03_SM` | 9.6–15.4 | 8.0° | 1.00–1.00 | 11.16 | 9.64 |
| `AP_Tree_Juniper03_SMIK` | 11.6–18.5 | 7.5° | 1.00–1.00 | 10.73 | 11.55 |
| `AP_Tree_Juniper02_SMIK` | 8.4–13.5 | 7.1° | 1.00–1.00 | 10.62 | 8.41 |
| `AP_Tree_10_ArgassTree_02_SM` | 8.1–13.0 | 7.1° | 1.00–1.00 | 9.32 | 8.10 |
| `AP_Tree_10_ArgassTree_SM` | 7.6–12.1 | 7.6° | 1.00–1.00 | 8.10 | 7.59 |
| `AP_ENV_tree_Nokmyung` | 14.0–22.4 | 6.3° | 1.00–1.00 | 3.88 | 3.53 |

**Glade** — 5 species

| Prefab | Spacing (m) | Max slope | Scale | Visible height (m) | Footprint (m) |
|---|---|---:|---|---:|---:|
| `AP_Neutral_Foliage_A_Weed01_SM` | 12.8–20.8 | 9.2° | 1.00–1.00 | 1.51 | 3.25 |
| `AP_Neutral_Tree_A_PrickelGrass01_SM` | 9.6–14.4 | 9.6° | 1.00–1.00 | 1.17 | 1.95 |
| `TSA_Heather_A` | 5.6–8.0 | 8.8° | 1.00–1.00 | 0.93 | 1.10 |
| `AP_Neutral_Foliage_A_Weed02_SM` | 7.2–11.2 | 8.0° | 1.00–1.00 | 0.77 | 1.50 |
| `TSA_GrassDry_C` | 4.0–6.4 | 7.2° | 1.00–1.00 | 0.68 | 0.58 |

**HereticForest** — 8 species

| Prefab | Spacing (m) | Max slope | Scale | Visible height (m) | Footprint (m) |
|---|---|---:|---|---:|---:|
| `AP_Tree_Burnt_04_SM` | 8.2–13.2 | 8.4° | 1.00–1.00 | 27.45 | 8.23 |
| `AP_Tree_WNT_03_Bark_01_SM_2` | 4.0–6.0 | 6.7° | 1.00–1.00 | 17.47 | 6.62 |
| `AP_Tree_WNT_M_03_SM` | 13.4–21.4 | 8.1° | 1.00–1.00 | 16.66 | 13.36 |
| `AP_Tree_Deadtree06_SM` | 18.6–32.5 | 5.9° | 1.00–1.00 | 15.51 | 9.29 |
| `AP_Tree_DeadTree01_SM` | 11.0–16.5 | 6.7° | 1.00–1.00 | 13.59 | 18.33 |
| `AP_DeadTree02` | 9.7–14.5 | 9.2° | 1.00–1.00 | 13.02 | 16.15 |
| `AP_DeadTree03` | 16.3–26.1 | 5.0° | 1.00–1.00 | 12.31 | 16.30 |
| `AP_DeadTree04` | 12.8–20.4 | 9.5° | 1.00–1.00 | 11.44 | 12.76 |

**Mountain** — 11 species

| Prefab | Spacing (m) | Max slope | Scale | Visible height (m) | Footprint (m) |
|---|---|---:|---|---:|---:|
| `AP_M6_Rock_SeashoreWallStone01_SM` | 4.8–7.7 | 5.8° | 1.00–1.00 | 4.03 | 4.80 |
| `AP_M6_Rock_CemeteryRock02_SM` | 4.8–7.6 | 5.3° | 1.00–1.00 | 2.55 | 4.75 |
| `RF_Boulder5` | 4.6–7.3 | 5.5° | 1.00–1.00 | 2.46 | 4.58 |
| `AP_TurtleLake_Rock_GoblinRock02_SM` | 3.9–6.2 | 8.1° | 1.00–1.00 | 1.75 | 3.90 |
| `RF_Boulder4` | 12.0–19.6 | 6.0° | 1.00–1.00 | 1.70 | 3.05 |
| `AP_TurtleLake_Rock_GoblinRock01_SM` | 4.5–7.2 | 7.7° | 1.00–1.00 | 1.62 | 4.52 |
| `AP_M6_Rock_FieldStoneStone06_SM` | 7.2–11.2 | 9.5° | 1.00–1.00 | 1.25 | 1.56 |
| `RF_Boulder3` | 6.8–10.0 | 9.5° | 1.00–1.00 | 1.09 | 2.78 |
| `RF_Boulder2` | 9.6–15.2 | 9.1° | 1.00–1.00 | 0.90 | 2.07 |
| `AP_TurtleLake_Rock_LakeRock02_SM` | 7.2–10.4 | 8.8° | 1.00–1.00 | 0.85 | 1.43 |
| `AP_M6_Rock_FieldStoneStone05_SM` | 6.4–9.6 | 9.5° | 1.00–1.00 | 0.68 | 1.34 |

### 6.2 Three things wrong with the current setup

You do not have to preserve any of it. These are stated so you know what you are correcting.

**(a) The slope filter is choking the island.**
Every species currently carries a max slope between **5.0° and 10.0°**. Against the measured
distribution in §3, that means each species is allowed to spawn on only **10% to 29% of the land** —
and the fitness curve pushes most placements toward the flat end of even that. The island is 42%
hillside between 10° and 20°, and almost nothing is permitted to grow there.

This is very likely the single largest cause of the world reading as sparse and dull. Treat the
slope caps as free to change. Real forest grows on 25–35° slopes without difficulty; the limit that
matters is for rocks and boulders, which should sit where they look plausible.

**(b) 44 of the 95 prefabs are never spawned at all.**
Roughly half the library is unused, including some of the most distinctive assets in it — the entire
`GraveKeepers` family, all six `Curse`/`Heretic` trees, `AP_S_Tree_01` (33.8 m), `AP_GangshiTree_2`
(34.8 m), the `PTree` family, and every broken-trunk and stump piece. They are marked
`— not spawned —` in §7. Several are strong candidates for landmark or hero placement.

**(c) 11 objects currently spawning are not in the §7 list.**
Five small ground plants (`TSA_GrassDry_C`, `TSA_Heather_A`, two `Weed` meshes, `PrickelGrass`) are
being spawned as full GameObjects in the Glade biome. They are ground cover and are moving to the
terrain detail layer described in §5, so **do not plan for them**. Six others (`RF_Boulder1–5`,
`RF_Tree3`) live in a different folder; they are listed here for completeness but are also outside
your list.

### 6.3 Scale spread is unused

Every species except one is locked at scale 1.00–1.00. Every instance of a given tree is therefore
the exact same size as every other instance of it. Giving each species a scale range is one of the
cheapest available improvements and we would like you to specify one per species.

---

## 7. The asset catalogue — all 95 in-scope prefabs

Sorted tallest first, so the scale hierarchy is visible at a glance. Remember the player is
**1.80 m**.

- **Visible height** — metres above ground, pivot sink already subtracted.
- **Footprint** — widest horizontal extent (canopy/patch width). Use this for spacing.
- **Blocks walking** — collider radius in metres. `0.00` with a collider type means a thin or flat
  collider. This is navigation, not spacing.
- **Tris** — triangle count, one instance. Relevant because a 28,000-triangle tree placed at 4 m
  spacing across a biome is not affordable; a 900-triangle one is.
- **Currently spawned in** — the live configuration from §6.1, or `— not spawned —`.

| # | Prefab | Visible height (m) | Footprint (m) | Blocks walking (m) | Collider | Tris | Currently spawned in |
|---:|---|---:|---:|---:|---|---:|---|
| 1 | `AP_Tree_04_M01_03_SM` | **41.93** | 20.46 | 1.40 | capsule | 3801 | Forest @ 40.9–71.6m |
| 2 | `AP_Tree_04_GTree01_04_SM` | **39.59** | 17.04 | 0.96 | capsule | 4217 | Forest @ 34.1–59.6m |
| 3 | `AP_Tree_04_GTree01_05_SM` | **38.49** | 11.89 | 0.98 | capsule | 4146 | Forest @ 23.8–41.6m |
| 4 | `AP_Tree_04_M01_02_SM` | **37.64** | 15.32 | 1.34 | capsule | 4432 | Forest @ 30.6–53.6m |
| 5 | `AP_Tree_04_GTree01_03_SM` | **37.55** | 12.27 | 1.01 | capsule | 4532 | Forest @ 12.3–19.6m |
| 6 | `AP_GangshiTree_2` | **34.77** | 29.21 | 2.55 | capsule | 9393 | — not spawned — |
| 7 | `AP_S_Tree_01` | **33.83** | 30.10 | 3.22 | capsule | 2530 | — not spawned — |
| 8 | `AP_Tree_04_GTree01_01_SM_2` | **33.14** | 17.68 | 0.96 | capsule | 3356 | Forest @ 17.7–28.3m |
| 9 | `AP_Tree_04_GTree01_02_SM` | **31.09** | 14.35 | 0.95 | capsule | 2842 | Forest @ 14.3–23.0m |
| 10 | `AP_Tree_Conifir_A_01_SM_2` | **30.88** | 12.84 | 0.75 | capsule | 3082 | Forest @ 12.8–20.5m |
| 11 | `AP_Tree_Conifir_A_02_SM` | **30.64** | 12.72 | 0.71 | capsule | 3082 | Forest @ 12.7–20.4m |
| 12 | `AP_Tree_04_M01_01_SM_2` | **28.51** | 17.75 | 1.19 | capsule | 3846 | Forest @ 17.8–28.4m |
| 13 | `AP_Tree_04_GTree01_06_SM` | **27.94** | 20.28 | 1.11 | capsule | 4146 | Forest @ 40.6–71.0m |
| 14 | `AP_FallTree_01_SM_1` | **27.60** | 11.11 | 0.40 | capsule | 2890 | AutumnForest @ 11.1–17.8m; FlakTower @ 22.2–38.9m |
| 15 | `AP_Tree_Burnt_04_SM` | **27.45** | 8.23 | 0.84 | capsule | 3531 | EerieForest @ 8.2–13.2m; HereticForest @ 8.2–13.2m |
| 16 | `AP_GraveKeepers_B03_2` | **26.74** | 24.96 | 2.00 | capsule | 16443 | — not spawned — |
| 17 | `AP_FallTree_01_SM` | **25.14** | 8.90 | 0.40 | capsule | 2890 | AutumnForest @ 8.9–14.2m; FlakTower @ 17.8–31.2m |
| 18 | `AP_M6_Tree_ForestTree08_SM_JYI_2` | **24.99** | 19.33 | 1.02 | capsule | 1305 | — not spawned — |
| 19 | `AP_Tree_04_M01_04_SM` | **23.36** | 13.53 | 0.87 | capsule | 2978 | — not spawned — |
| 20 | `AP_Tree_04_M01_05_SM` | **23.04** | 16.58 | 1.22 | capsule | 2710 | — not spawned — |
| 21 | `AP_Tree_Blackpoplar01_SM` | **21.62** | 15.08 | 1.02 | capsule | 3057 | AutumnForest @ 30.2–52.8m |
| 22 | `AP_Tree_04_PTree_03_SM` | **20.89** | 11.30 | 0.89 | capsule | 3043 | — not spawned — |
| 23 | `AP_Tree_04_PTree_01_SM_2` | **20.45** | 10.86 | 0.69 | capsule | 3067 | — not spawned — |
| 24 | `AP_Tree_WNT_M01_01_SM_3` | **19.74** | 16.60 | 0.88 | capsule | 2949 | EerieForest @ 16.6–26.6m |
| 25 | `AP_Tree_AUT_White_A_02_SM` | **19.16** | 10.47 | 0.31 | capsule | 3505 | AutumnForest @ 10.5–16.8m |
| 26 | `AP_Tree_AUT_White_A_03_SM` | **18.91** | 9.68 | 0.31 | capsule | 2823 | AutumnForest @ 9.7–15.5m |
| 27 | `AP_Tree_Oak01_SM` | **18.77** | 11.34 | 0.81 | capsule | 6948 | AutumnForest @ 22.7–39.7m |
| 28 | `AP_Tree_04_PTree_04_SM` | **17.63** | 7.69 | 0.40 | capsule | 1194 | — not spawned — |
| 29 | `AP_Tree_WNT_03_Bark_01_SM_2` | **17.47** | 6.62 | 0.41 | capsule | 3607 | EerieForest @ 4.0–6.0m; HereticForest @ 4.0–6.0m |
| 30 | `AP_GraveKeepers_B07` | **17.15** | 20.79 | 2.00 | capsule | 5929 | — not spawned — |
| 31 | `AP_Tree_04_PTree_02_SM` | **17.06** | 9.00 | 0.63 | capsule | 2979 | — not spawned — |
| 32 | `AP_Tree_WNT_M_03_SM` | **16.66** | 13.36 | 0.60 | capsule | 2872 | EerieForest @ 13.4–21.4m; HereticForest @ 13.4–21.4m |
| 33 | `AP_GraveKeepers_B01` | **16.33** | 18.58 | 2.00 | capsule | 20824 | — not spawned — |
| 34 | `AP_FallTree_02_SM` | **15.88** | 8.13 | 0.20 | capsule | 2535 | AutumnForest @ 8.1–13.0m |
| 35 | `AP_Tree_Deadtree06_SM` | **15.51** | 9.29 | 1.73 | capsule | 6837 | HereticForest @ 18.6–32.5m |
| 36 | `AP_Tree_Lake_RoundTree_01_SM` | **15.34** | 12.77 | 1.34 | capsule | 7253 | Fountain @ 25.5–44.7m |
| 37 | `AP_WhiteFir_MD_Dead_03` | **14.91** | 6.15 | 0.54 | capsule | 1315 | EerieForest @ 6.2–9.8m |
| 38 | `AP_Building_exorcist_tree2` | **14.83** | 18.08 | 0.78 | capsule | 1141 | — not spawned — |
| 39 | `AP_GraveKeepers_B02` | **14.66** | 14.96 | 1.44 | capsule | 27036 | — not spawned — |
| 40 | `AP_Tree_Curse_J07` | **13.82** | 14.82 | 1.03 | capsule | 13461 | — not spawned — |
| 41 | `AP_Tree_DeadTree01_SM` | **13.59** | 18.33 | 0.85 | capsule | 1962 | EerieForest @ 11.0–16.5m; HereticForest @ 11.0–16.5m |
| 42 | `AP_Tree_04_PTree_05_SM` | **13.33** | 4.20 | 0.31 | capsule | 655 | FlakTower @ 8.4–14.7m |
| 43 | `AP_DeadTree02` | **13.02** | 16.15 | 0.74 | capsule | 725 | EerieForest @ 9.7–14.5m; HereticForest @ 9.7–14.5m |
| 44 | `AP_Norway_Spruce_01` | **12.68** | 4.00 | 0.20 | capsule | 1514 | FlakTower @ 8.0–14.0m |
| 45 | `AP_BC_PineTree_02` | **12.44** | 6.70 | 0.44 | capsule | 2338 | FlakTower @ 13.4–23.4m |
| 46 | `AP_DeadTree03` | **12.31** | 16.30 | 1.05 | capsule | 2211 | EerieForest @ 9.8–14.7m; HereticForest @ 16.3–26.1m |
| 47 | `AP_GraveKeepers_B06` | **12.29** | 14.37 | 1.60 | capsule | 17899 | — not spawned — |
| 48 | `AP_M6_Tree_MonsterTreeBark_SM_PHJ_2` | **12.17** | 9.88 | 3.37 | capsule | 9304 | — not spawned — |
| 49 | `AP_Tree_10_ArgassTree_04_SM` | **11.60** | 12.79 | 0.86 | capsule | 6652 | Fountain @ 12.8–20.5m |
| 50 | `AP_BC_PineTree_03` | **11.58** | 9.80 | 0.39 | capsule | 1360 | — not spawned — |
| 51 | `AP_DeadTree04` | **11.44** | 12.76 | 1.32 | capsule | 1803 | EerieForest @ 7.7–11.5m; HereticForest @ 12.8–20.4m |
| 52 | `AP_Tree_Curse_J08` | **11.36** | 10.05 | 0.59 | capsule | 10366 | — not spawned — |
| 53 | `AP_Tree_color_001_01_2` | **11.26** | 11.59 | 1.08 | capsule | 1428 | AutumnForest @ 11.6–18.5m |
| 54 | `AP_Tree_10_ArgassTree_03_SM` | **11.16** | 9.64 | 1.13 | capsule | 9965 | Fountain @ 9.6–15.4m |
| 55 | `AP_AlaskaCedar_001_2` | **10.93** | 5.19 | 0.19 | capsule | 963 | FlakTower @ 10.4–18.2m |
| 56 | `AP_Tree_Juniper03_SMIK` | **10.73** | 11.55 | 1.02 | capsule | 2197 | Fountain @ 11.6–18.5m |
| 57 | `AP_Tree_Juniper02_SMIK` | **10.62** | 8.41 | 0.68 | capsule | 4052 | Fountain @ 8.4–13.5m |
| 58 | `AP_ENV_tree_ToeMunJean` | **10.42** | 7.86 | 0.40 | capsule | 309 | FlakTower @ 7.9–12.6m |
| 59 | `AP_Plant_003_07` | **10.12** | 7.62 | 0.12 | capsule | 504 | — not spawned — |
| 60 | `AP_ENV_tree_SaGeeSukRim` | **9.99** | 8.62 | 0.29 | capsule | 288 | FlakTower @ 8.6–13.8m |
| 61 | `AP_Tree_Curse_H01_2` | **9.87** | 15.63 | 1.84 | capsule | 27975 | — not spawned — |
| 62 | `AP_Tree_Curse_K01` | **9.54** | 9.79 | 1.57 | capsule | 18270 | — not spawned — |
| 63 | `AP_Tree_color_001_03` | **9.42** | 10.08 | 0.90 | capsule | 1460 | AutumnForest @ 10.1–16.1m |
| 64 | `AP_Tree_10_ArgassTree_02_SM` | **9.32** | 8.10 | 1.00 | capsule | 6349 | Fountain @ 8.1–13.0m |
| 65 | `AP_M6_Tree_Bushtree01_SM` | **8.13** | 9.66 | 1.62 | capsule | 966 | — not spawned — |
| 66 | `AP_Tree_10_ArgassTree_SM` | **8.10** | 7.59 | 0.72 | capsule | 8199 | Fountain @ 7.6–12.1m |
| 67 | `AP_Tree_Heretic_A01_2` | **7.92** | 8.00 | 1.36 | capsule | 7780 | — not spawned — |
| 68 | `AP_Tree_Dry_D01` | **7.67** | 7.80 | 1.39 | capsule | 2243 | — not spawned — |
| 69 | `AP_GraveKeepers_B04` | **6.04** | 6.37 | 0.33 | capsule | 17378 | — not spawned — |
| 70 | `AP_M6_Tree_Bushtree02_SM` | **5.75** | 12.57 | 1.25 | capsule | 731 | — not spawned — |
| 71 | `AP_Tree_Heretic_B03` | **4.96** | 3.88 | 1.95 | box | 1192 | — not spawned — |
| 72 | `AP_sunghwangdang_Tree_pagoda_01` | **4.50** | 4.45 | 1.92 | capsule | 2668 | — not spawned — |
| 73 | `AP_Tree_Heretic_B05` | **4.31** | 1.66 | 0.60 | capsule | 544 | — not spawned — |
| 74 | `AP_M6_Rock_SeashoreWallStone01_SM` | **4.03** | 4.80 | 1.97 | capsule | 1600 | Beach @ 4.8–7.7m; Mountain @ 4.8–7.7m |
| 75 | `AP_GraveKeepers_C02` | **4.00** | 2.49 | 1.23 | capsule | 2620 | — not spawned — |
| 76 | `AP_sunghwangdang_Tree_pagoda_01_1` | **3.91** | 3.57 | 1.62 | capsule | 3645 | — not spawned — |
| 77 | `AP_ENV_tree_Nokmyung` | **3.88** | 3.53 | 0.16 | box | 289 | Fountain @ 14.0–22.4m |
| 78 | `AP_M6_Rock_CemeteryRock02_SM` | **2.55** | 4.75 | 2.37 | box | 3394 | Mountain @ 4.8–7.6m |
| 79 | `AP_Tree_Heretic_D03_02` | **2.40** | 12.18 | 1.38 | box | 4170 | — not spawned — |
| 80 | `AP_Tree_Heretic_D03` | **2.34** | 15.04 | 3.17 | box | 4500 | — not spawned — |
| 81 | `AP_GraveKeepers_C01` | **2.12** | 2.76 | 1.41 | capsule | 2494 | — not spawned — |
| 82 | `AP_TurtleLake_Rock_GoblinRock02_SM` | **1.75** | 3.90 | 1.04 | box | 1370 | Beach @ 7.8–13.7m; Mountain @ 3.9–6.2m |
| 83 | `AP_Tree_Heretic_D02_01` | **1.62** | 12.21 | 1.70 | box | 4668 | — not spawned — |
| 84 | `AP_TurtleLake_Rock_GoblinRock01_SM` | **1.62** | 4.52 | 1.76 | box | 1638 | Mountain @ 4.5–7.2m |
| 85 | `AP_TurtleLake_Tree_Stump02_SM` | **1.48** | 3.90 | 0.36 | box | 2208 | — not spawned — |
| 86 | `AP_Tree_Break_Root_02_SM` | **1.34** | 3.03 | 0.64 | box | 509 | — not spawned — |
| 87 | `AP_Tree_Break_03_SM` | **1.29** | 9.30 | 4.25 | box | 1238 | — not spawned — |
| 88 | `AP_Tree_Break_02_SM` | **1.28** | 6.34 | 2.99 | box | 1252 | — not spawned — |
| 89 | `AP_M6_Rock_FieldStoneStone06_SM` | **1.25** | 1.56 | 0.78 | box | 1008 | Beach @ 6.4–10.0m; Mountain @ 7.2–11.2m |
| 90 | `AP_TurtleLake_Rock_TurtleRock04_SM` | **1.15** | 10.96 | 3.88 | box | 1686 | — not spawned — |
| 91 | `AP_Tree_Break_MushroomTrunk_01_SM` | **0.94** | 2.31 | 0.91 | box | 801 | — not spawned — |
| 92 | `AP_TurtleLake_Rock_LakeRock02_SM` | **0.85** | 1.43 | 0.63 | box | 854 | Beach @ 11.2–18.4m; Mountain @ 7.2–10.4m |
| 93 | `AP_M6_Rock_FieldStoneStone05_SM` | **0.68** | 1.34 | 0.67 | box | 480 | Beach @ 10.4–16.8m; Mountain @ 6.4–9.6m |
| 94 | `AP_Tree_Dry_N02` | **0.59** | 5.25 | 0.00 | none | 1627 | — not spawned — |
| 95 | `AP_TurtleLake_Tree_BrokenTree01_SM` | **0.44** | 4.20 | 1.89 | box | 489 | — not spawned — |

---

## 8. What to return

For **each of the 9 biomes**, give a table of the species it should contain:

| Prefab | Spacing min–max (m) | Max slope (°) | Scale min–max | Role |
|---|---|---|---|---|

Where:

- **Prefab** — exact name from §7. Every name must come from that list.
- **Spacing** — Poisson min–max in metres for that species alone. Remember §4.2: this is per-species,
  and the biome's total density is the sum of all its species. Sanity-check it against the species'
  own footprint — spacing well below the footprint means guaranteed overlap.
- **Max slope** — degrees, 0–90. Use §3's distribution to know what a number actually buys you.
- **Scale min–max** — the randomisation range. Give a real range, not 1.00–1.00.
- **Role** — one short phrase: canopy, mid-storey, understory, landmark, ground clutter, barrier.

Then add, for each biome:

- **A one-line statement of what the biome is for**, in gameplay terms — open and readable, dense and
  disorienting, a corridor, a landmark space.
- **Estimated instances per hectare** for the biome as a whole, so we can sanity-check the budget
  before spawning. For reference, a previous pass produced roughly 150 trees/ha in the densest
  forest and 10/ha in the sparsest, across 64.2 ha of land.

Finally:

- **Say which prefabs you would leave unused**, and why. 44 are currently idle; it is entirely
  reasonable for some to stay that way, but we want that to be a decision rather than an oversight.
- **Flag any species you would use as a hero or landmark** — placed a handful of times by hand
  rather than scattered. Several assets in §7 are large and distinctive enough that scattering them
  wastes them.

### Constraints to respect

1. **Only the 95 prefabs in §7.** No new assets.
2. **No grass, flowers, or ground cover** — §5. Assume it is already there.
3. **The island is 64.2 ha of land**, mostly 10–20° slope. Totals must be plausible against that.
4. **Triangle cost is real.** The heaviest assets here are ~28,000 triangles each. Dense placement of
   those is not affordable; dense placement of the 900-triangle ones is.
5. The nine biomes are: **Forest, AutumnForest, EerieForest, HereticForest, Mountain, Beach, Glade,
   Fountain, FlakTower.** Their names are the only guidance given about them deliberately — the
   visual character of each is your call, and screenshots are provided separately.

The aesthetic direction is yours. This document is the physical facts only.
