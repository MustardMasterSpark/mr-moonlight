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

