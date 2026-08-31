# MRM-70 — Vegetation we own but are NOT using

Written 2026-08-31, branch `mrm-70`. Answers one question: **what vegetation art exists in the
project that is not in `Assets/_Project/Art/VegetationPrefabs/`**, i.e. everything the current Gaia
spawn cannot reach.

Purpose: Carlos picks which of these to promote into the VegetationPrefabs set before the weighted
biome list goes to ChatGPT.

---

## 1. The short answer

| | |
|---|---|
| Prefabs Gaia currently spawns from | **137**, all in `Assets/_Project/Art/VegetationPrefabs/` |
| Source meshes behind them | 137 FBX in `LowPolyPlants/Meshes` (20) + `TopDownNature/Meshes` (117) — **1:1, nothing unbuilt** |
| **Prefabs we own and are NOT spawning** | **53**, all in `Assets/_Project/Prefabs/World/Vegetation/` |

So there is no orphan-mesh problem. The unused vegetation is **already prefabbed** — it just lives
in a different folder (`_Project/Prefabs/World/Vegetation/`) that the MRM-70 batch and the Gaia
spawner never looked at. Three packs, 53 prefabs:

| Pack | Prefabs | What it is |
|---|---:|---|
| **RetroRealism** (`RF_`) | 21 | The conifers, boulders, logs, stumps, bushes, ferns |
| **TerrainSampleAssets** (`TSA_`) | 20 | Unity's own grass/fern/bush/plant detail meshes |
| **GrassFlowers** (`GFF_`) | 12 | Flat billboard grass + wildflower cards |

**All 53 are already measured** — they are rows in `Tools/vegetation/veg_sizes.csv` alongside the
137 in use, so they can be dropped straight into the ChatGPT list with real dimensions.

---

## 2. What is in the screenshot

The 2026-08-30 screenshot (tall thin firs, dense green tufts carpeting the slope, 249 FPS) is
**entirely** this unused set:

- **The trees** — the tall bare-trunked firs — are `RF_Tree1`–`RF_Tree4` (RetroRealism),
  13.6 m to 25.1 m tall, materials `M_RF_Trees` / `M_RF_TreesDead` / `M_RF_BranchFir`.
- **The ground carpet** — the "little foliage" — is **not prefab instances**. It is Unity
  **terrain detail**, painted by a deleted editor tool called `BiomeGrassSetup`, from two kinds of
  prototype:
  - **mesh details** — the `TSA_Grass_*`, `TSA_Fern_*`, `TSA_Plant_*`, `TSA_Heather_*`,
    `TSA_Bush*` meshes plus `RF_Fern1/2` and `RF_Bush1-3`. The spiky vertical tufts and the small
    leafy rosettes are these.
  - **texture billboards** — the 12 `GFF_` grass/flower cards, fed in as *textures*
    (`GrassType.Texture`), not as prefabs. Unity builds the crossed quads itself and
    **wind-animates them for free** — that is the "moving grass" mechanism, and it is the cheap one.

That is why it ran at 249 FPS: terrain details are instanced by the terrain system and never get
colliders, so a dense carpet costs almost nothing next to spawning GameObjects.

---

## 3. Where everything lives

### 3.1 RetroRealism — 21 prefabs

| | Path |
|---|---|
| Prefabs | `Assets/_Project/Prefabs/World/Vegetation/RetroRealism/` |
| Meshes | `Assets/_Project/Art/Environment/Vegetation/RetroRealism/Meshes/` (plus `Meshes/Baked/`, plus `RF_Tree1-4_Collision.fbx` low-poly collision hulls) |
| Materials | `Assets/_Project/Art/Environment/Vegetation/RetroRealism/Materials/` — `M_RF_Boulders`, `M_RF_BranchFir`, `M_RF_BranchFirDead`, `M_RF_Bush`, `M_RF_Dirt`, `M_RF_Fern`, `M_RF_Trees`, `M_RF_TreesDead` |
| Textures | `Assets/_Project/Art/Environment/Vegetation/RetroRealism/Textures/` (BaseColor + Mask, `.tga`/`.png`) |

Measured — visible width × height in metres, triangles, collider:

| Prefab | W | H | tris | collider |
|---|---:|---:|---:|---|
| RF_Tree4 | 8.61 | **25.08** | 976 | capsule |
| RF_Tree3 | 7.49 | **20.67** | 1378 | capsule |
| RF_Tree2 | 6.60 | **18.26** | 1204 | capsule |
| RF_Tree1 | 3.67 | **13.59** | 314 | capsule |
| RF_Sapling2 | 2.82 | 5.40 | 684 | capsule |
| RF_Sapling1 | 2.15 | 2.42 | 244 | capsule |
| RF_Stump2 | 2.66 | 4.38 | 152 | capsule |
| RF_Stump1 | 1.75 | 2.39 | 112 | capsule |
| RF_Log3 | 10.08 | 3.11 | 404 | box |
| RF_Log2 | 6.13 | 1.03 | 230 | box |
| RF_Log1 | 5.16 | 0.88 | 112 | box |
| RF_Boulder5 | 4.58 | 2.46 | 58 | capsule |
| RF_Boulder4 | 3.05 | 1.70 | 62 | capsule |
| RF_Boulder3 | 2.78 | 1.09 | 52 | capsule |
| RF_Boulder2 | 1.93 | 0.90 | 54 | capsule |
| RF_Boulder1 | 0.98 | 0.70 | 84 | capsule |
| RF_Bush3 | 1.91 | 2.18 | 22 | none |
| RF_Bush2 | 1.12 | 1.26 | 8 | none |
| RF_Bush1 | 0.58 | 0.97 | 4 | none |
| RF_Fern2 | 1.25 | 0.53 | 33 | none |
| RF_Fern1 | 0.73 | 0.23 | 9 | none |

Worth noting how **cheap** these are. `RF_Tree4` is 25 m tall for **976 triangles**; the comparable
TopDownNature trees currently in use run 8,000–28,000. If frame budget ever bites, this pack is the
answer for background forest.

### 3.2 TerrainSampleAssets — 20 prefabs (the ground carpet)

| | Path |
|---|---|
| Prefabs | `Assets/_Project/Prefabs/World/Vegetation/TerrainSampleAssets/` |
| Source meshes | `Assets/ThirdParty/TerrainSampleAssets/Models/*.asset` (Unity mesh assets, not FBX) |
| Materials | `Assets/_Project/Art/Environment/Vegetation/TerrainSampleAssets/Materials/` — `M_TSA_Bush`, `M_TSA_BushDry`, `M_TSA_Fern`, `M_TSA_Grass`, `M_TSA_GrassC`, `M_TSA_GrassDry`, `M_TSA_Heather`, `M_TSA_Plant` |
| Textures | `Assets/_Project/Art/Environment/Vegetation/TerrainSampleAssets/Textures/` (BaseColor + Normal + Mask per family) |
| Wind shader | `Assets/ThirdParty/TerrainSampleAssets/ShaderGraphs/TerrainGrass.shadergraph` + `Subgraphs/AnimatedGrassPhase.shadersubgraph` — **this is the sway** |
| Ground layers (bonus) | `Assets/ThirdParty/TerrainSampleAssets/TerrainLayers/` — `Grass_A`, `Grass_B`, `Grass_Dry`, `Grass_Moss`, `Grass_Soil` |

Measured:

| Prefab | W | H | tris |
|---|---:|---:|---:|
| TSA_Grass_D | 0.94 | 0.76 | 1268 |
| TSA_Grass_C | 0.95 | 0.60 | 1002 |
| TSA_Grass_B | 0.96 | 0.39 | 1136 |
| TSA_Grass_A | 0.31 | 0.30 | 128 |
| TSA_GrassDry_C | 0.58 | 0.71 | 122 |
| TSA_GrassDry_A | 0.41 | 0.55 | 173 |
| TSA_GrassDry_B | 0.37 | 0.38 | 168 |
| TSA_Fern_C | 0.99 | 0.41 | 384 |
| TSA_Fern_A | 0.94 | 0.34 | 360 |
| TSA_Fern_B | 0.92 | 0.26 | 464 |
| TSA_Heather_B | 1.22 | 0.99 | 396 |
| TSA_Heather_A | 1.10 | 1.00 | 268 |
| TSA_BushDry_B | 1.20 | 1.11 | 792 |
| TSA_BushDry_A | 0.92 | 1.01 | 640 |
| TSA_Bush_B | 0.82 | 0.83 | 273 |
| TSA_Bush_A | 0.59 | 0.88 | 362 |
| TSA_Plant_A | 0.40 | 0.95 | 260 |
| TSA_Plant_B | 0.24 | 0.95 | 145 |
| TSA_Plant_C | 0.31 | 0.83 | 128 |
| TSA_Plant_D | 0.22 | 0.44 | 61 |

All 20 have **no collider** — correct for understory, and required if they go in as terrain details.

Duplicate copies of these prefabs also sit at `Assets/ThirdParty/TerrainSampleAssets/Prefabs/`
(unprefixed: `Grass_A.prefab` etc.). The `TSA_` copies under `_Project` are the ones we adopted;
ignore the ThirdParty ones.

### 3.3 GrassFlowers — 12 billboard cards

| | Path |
|---|---|
| Prefabs | `Assets/_Project/Prefabs/World/Vegetation/GrassFlowers/` — `GFF_Grass01/02`, `GFF_GrassFlower01`–`10` |
| Meshes | **None.** There is no source mesh — each prefab is two crossed quads built in-engine |
| Materials | `Assets/_Project/Art/Environment/Vegetation/GrassFlowers/Materials/` (12 × `M_GFF_*`) |
| Textures | `Assets/_Project/Art/Environment/Vegetation/GrassFlowers/Textures/` (12 × `T_GFF_*_BaseColor.png`) |
| Matching ground texture | `Assets/_Project/Art/Environment/Terrain/GrassFlowers/Textures/T_GFF_GroundGrass01_BaseColor.png` + `Layers/TL_GFF_GroundGrass01.terrainlayer` |

All 12 measure 0.50 × 0.50 m at 4 triangles — but that number is meaningless for the intended use,
because as terrain **detail textures** Unity rebuilds the quads and the size comes from the
prototype's `minMaxHeight` / `minMaxWidth`, not from the prefab.

**Known limitation:** the 12 GFF *prefabs* are crossed double-quads = 2 MeshRenderers, and Unity's
terrain detail system rejects any prefab with more than one MeshRenderer (or with an LODGroup). So
they can only be used as **detail textures** — feeding `T_GFF_*_BaseColor.png` directly — or as
hand-placed set dressing. See `Docs/mrm70-biome-vegetation-strategy.md` §5.2.

---

## 4. The mechanism, and what has to be rebuilt

The moving grass in the screenshot was **not** Gaia and **not** GameObject spawning. It was:

1. `DetailPrototype`s registered on the terrain — 27 of them — mixing texture billboards (GFF) with
   detail meshes (TSA, RF ferns/bushes).
2. A detail density map painted per biome by an editor tool, `BiomeGrassSetup` (~13 s to spawn).
3. Unity's terrain detail renderer doing the instancing and the wind sway.

Two things stand in the way of simply turning it back on:

**The tool is deleted.** `Assets/_Project/Code/Editor/BiomeGrassSetup.cs` was removed in commit
`f306acc` ("MRM-70/71 — Gaia terrain pivot…"). It is recoverable in full with
`git show f306acc^:Assets/_Project/Code/Editor/BiomeGrassSetup.cs`.

**The live terrain has zero detail prototypes.** The current terrain is a 1024 × 1024 m Gaia session
asset with 0 layers / 0 details / 0 trees, and every coordinate and density map from the first
island is dead. Prototypes have to be re-registered and density re-painted against the new terrain,
whichever tool does it.

Two hard rules carried over from the first build, both learned the expensive way:

- `DetailPrototype.density` **must be set explicitly**. The terrain runs in CoverageMode, where
  `density` *is* the coverage amount; it defaults to 0, which silently creates prototypes and zero
  blades.
- Billboards and meshes need **different** coverage. A shared value of 6 fused the 1–2 m ferns into
  a continuous chest-height hedge that walled the player in. The first island shipped
  `MaxCoverageBillboard = 6` / `MaxCoverageMesh = 0.7`.

And the one that limits ambition: **terrain detail instances never get colliders.** Nothing in the
grass tier can block the player. Fine for grass — it means the `RF_` trees, boulders, logs and
stumps must be spawned as GameObjects or tree instances, not as details.

An open note from the first pass: grass tuning was never finished. The detail map verified at full
coverage (255) in the forest, so the data was right, but the GFF cards are thin stalks on a mostly
empty 512×512 texture at real-world scale, so under dusk lighting the ground read sparser than the
numbers suggested. The remaining levers are **card size and tint saturation, not coverage**.

---

## 5. Decision needed

Three buckets, and they do not have to be answered the same way:

1. **The 21 `RF_` props** — trees, boulders, logs, stumps, saplings. Drop-in candidates for the
   VegetationPrefabs folder and the ChatGPT weight list. Cheapest trees we own by a wide margin.
2. **The 20 `TSA_` plus 5 `RF_` understory meshes** — ferns, bushes, grass clumps. Best as terrain
   detail meshes (nearly free, no colliders), not as Gaia GameObjects.
3. **The 12 `GFF_` cards** — the dense moving carpet. Detail *textures* only; the lever for "lots of
   moving grass" is card size and coverage, not species count.

Buckets 2 and 3 arguably do not belong in the ChatGPT weight list at all if they go the
terrain-detail route — that is a separate density pass, not a per-species spawn weight. Bucket 1
does.

---

## 6. BUILT — 30 grass detail prefabs, 2026-08-31

Carlos approved the recommendation (1024 for the Gaia atlases, ~30 prefabs). Done and verified.

### 6.1 What shipped

`Assets/_Project/Art/VegetationPrefabs/GRASS PREFABS/` — **30 prefabs**, all `GRASS_` prefixed.

| Set | Count | Prefabs |
|---|---:|---|
| `GRASS_TSA_*` | 20 | Bush_A/B, BushDry_A/B, Fern_A/B/C, Grass_A/B/C/D, GrassDry_A/B/C, Heather_A/B, Plant_A/B/C/D |
| `GRASS_Gaia_LawnGrass_*` | 5 | DeadPatch, Spiky, Weeds, Wheat_01, Wheat_02 |
| `GRASS_Gaia_WildGrass_*` | 5 | General, TallWhiteFlower, Understory_Clover, Understory_Foliage, Understory_Sticks |

Verified by reading component state back off all 30: **1 MeshFilter, 1 MeshRenderer, 0 LODGroup,
0 Collider, material present, base texture bound** — the exact shape Unity's terrain detail system
requires. 24 to 1,268 triangles each; visible sizes 0.06 m to 1.31 m.

Shadow casting is **off** on every one (grass casting real shadows is pure cost at this density),
reflection probes off, motion vectors forced off.

### 6.2 Where the source art went

| | Path |
|---|---|
| TSA textures (pixelated) | `Art/Environment/Vegetation/GrassDetail/TSA/Textures/` — 8 BaseColor @ 512, 8 Normal @ 256 |
| TSA materials | `.../GrassDetail/TSA/Materials/` — 8 × `M_GRASS_TSA_*` |
| Gaia meshes | `.../GrassDetail/Gaia/Meshes/` — 10 FBX |
| Gaia textures (pixelated) | `.../GrassDetail/Gaia/Textures/` — 2 BaseColor @ 1024, 2 Normal @ 1024 |
| Gaia materials | `.../GrassDetail/Gaia/Materials/` — `M_GRASS_Gaia_LawnGrass`, `M_GRASS_Gaia_WildGrass` |

Originals under `Prefabs/World/Vegetation/` and `Vegetation/TerrainSampleAssets/Textures/` are
untouched.

### 6.3 Texture treatment

Everything went through `Tools/pipeline/texture_pass.py` — the same pixelation the props use
(10 levels per channel, Bayer 4×4 ordered dither, nearest-neighbour resize). All import as
**Point filter, mipmapped, clamped, alpha-is-transparency**.

The two Gaia sheets went to **1024, not 512**, and that was the one real judgement call. They are
**atlases** — `PW_LawnGrass_00_D` carries 12 meshes' UV islands and `PW_WildGrass_00_D` carries 6.
At 512 each island lands near 128 px and the blades dissolve. 1024 is the honest per-asset
equivalent of the 512 a single-object texture gets. Everything else was already 512 and only
needed the pixel pass.

Materials are **RetroLit**, matching the existing vegetation batch exactly: `_AlphaClip 1`,
`_AlphaToMask 1`, `_Cutoff 0.5`, `_Cull 0` (double-sided), TexelLit + screen dither + point filter
keywords.

Note the pixelation is baked **into the texture**, not applied by the shader. That matters here:
when the terrain renders these in `DetailRenderMode.Grass` it substitutes its own waving-grass
shader and RetroLit does not run — but the PSX look survives, because it is in the pixels.

### 6.4 Gaia Pro asset adoption

The Gaia meshes came out of `Gaia Pro Assets and Biomes.unitypackage` (3.7 GB) in the Playground
project, which was **never extracted anywhere** — Mr. Moonlight's Gaia install ships zero grass art.
Extracted via the GUID/`pathname` tar technique; only 14 files were pulled in, not the package.

Still available there and deliberately left behind for now: 11 more `PW_LawnGrass` meshes,
`PW_Clover`, both `_Lod0` variants, and **16 legacy billboard cards** (`PW_Grass_Patch_01/02`,
`PW_Grass_Dactylis_Glomerata`, `PW_Grass_Phleum_Pratense`, the flower cards) which are the natural
source if we want the `GrassType.Texture` billboard tier later.

### 6.5 Not done, and deliberately

**Nothing has been spawned.** These are prefabs on disk. Registering them as `DetailPrototype`s and
painting density is the next step, and it belongs inside the Gaia spawners (§7), not a separate
tool.

---

## 7. Gaia rules vs. hand-painting — how the grass gets placed

Both routes exist. They write to the same place and the last one to run wins, so the order matters.

### 7.1 Gaia spawner rules — the route we use

Grass is a **first-class Gaia resource type**, not a bolt-on:

```
Assets/Procedural Worlds/.../Core/Utils/GaiaConstants.cs:431
public enum SpawnerResourceType { TerrainTexture=0, TerrainDetail=1, TerrainModifierStamp=8,
                                  TerrainTree=2, GameObject=3, SpawnExtension=4, Probe=7, ... }
```

`ResourceProtoDetail` carries everything the deleted `BiomeGrassSetup` tool did, and more:
`m_detailPrototype` (mesh) **or** `m_detailTexture` (billboard), `m_minWidth`/`m_maxWidth`,
`m_minHeight`/`m_maxHeight`, `m_density`, `m_targetCoverage`, `m_healthyColour`/`m_dryColour`,
`m_alignToGround`, `m_useInstancing`, `m_noiseSpread`, plus the same `SpawnCritera[]` array the
tree rules use — so grass obeys the identical height/slope/mask logic.

This is the route to take, for one reason above all: **it survives regeneration.** The grass becomes
more rules inside the same biome spawners, so "generate new Gaia" reproduces the whole island —
textures, trees, props and grass — in one pass, from one rule set, against the same masks.

### 7.2 Unity's own Paint Details brush — touch-up only

Unity's terrain toolbar has a **Paint Details** brush that edits the density maps by hand. It is a
legitimate tool and worth using for spot work — thinning grass off a path, thickening a clearing
where the player will stand.

The catch is unforgiving: **a Gaia respawn wipes hand-painted detail.** Gaia clears and rewrites the
detail layers it owns. So hand-painting is strictly a **last** step, after the terrain is final and
no further regeneration is planned — never a base layer.

There is also no separate Gaia paint panel. That toolbar is Unity's own; Gaia does not add one.

### 7.3 Consequence for the ChatGPT weight list

The grass tier is placed by **coverage and density per biome**, not by per-species spawn weights,
and it is tuned by looking at it — card size, tint, coverage — not by a table.

So the ChatGPT brief covers **only the spawned props**: the trees, rocks, logs, stumps, bushes and
flowers that spawn as tree instances or GameObjects and have real footprints, spacings and
collision. The 30 `GRASS_*` detail prefabs, and any billboard cards added later, stay **out** of
that list entirely — we set those ourselves.
