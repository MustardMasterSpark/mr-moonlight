# MRM-70 — Biome painting + vegetation spawning strategy

> ## ⛔ SUPERSEDED FOR ALL NUMBERS — 2026-08-30
>
> **Species palettes, densities and spacing:** use
> **`Docs/mrm70-biome-distribution-measured.md`** instead. It is built from measured prefab
> geometry; this document's §4 numbers were estimates made before the assets were sized.
>
> **§3 "Biome regions in world coordinates" is dead, not just stale.** It describes a
> 4103 × 7085 m terrain. The live terrain is **1024 × 1024 m at origin (−512, 0, −512)**, a
> different shape — the coordinates cannot be scaled across. Landmarks and the player spawn need
> re-anchoring from scratch.
>
> **Still valid and still the plan:** §2 (terrain layers *are* the biome masks), §5 (material
> variants), and the overall run order. Read those; ignore every number.

> ## ⚠️ Platform note — banner added 2026-08-27
>
> This document predates the **2026-08-25 platform change**: WebGL was dropped for a **Windows
> 64-bit standalone** build at **1920×1080**. See `Docs/pc-build-target.md`.
>
> **The strategy is unaffected** — biomes, species, masks, densities and run order are all
> placement decisions, and placement did not change. What *is* stale:
>
> - Any mention of **960×540** — the target is 1920×1080.
> - **§6d "WebGL reality check"** is a **historical session record** of builds 12–14. Kept as
>   history; do not action it. The GLES3 sampler limit and the browser console it describes no
>   longer exist.
> - Density and LOD numbers described as *"to be tuned against a real WebGL build"* should be tuned
>   against a real **Windows** build — and per `pc-build-target.md` §6, editor `UnityStats` and
>   editor screenshots have both given false readings on this project. **Launch the .exe.**
> - The vegetation is now drawn by **Flora Renderer 6**, not Unity's native tree/detail path
>   (**38,980 → 535 draw calls, ~505 FPS**). The perf risk this doc worries about was real and has
>   been addressed.
>
> **Still binding, and still the two easiest things to get wrong:** terrain smoothness comes from
> the diffuse texture's **alpha channel**, and **terrain trees silently reject MeshColliders** —
> use a CapsuleCollider at the trunk.

> ## ⚠️ Terrain regenerated twice since this was written — banner added 2026-08-29
>
> The terrain this document was written against **no longer exists**. Between then and now: a full
> landmass regeneration via Gaia Stamps (2026-08-28, see `mrm70-gaia-kickoff.md` "MAJOR PIVOT"),
> then a second pivot to Gaia **World Designer** (2026-08-29, see that doc's "SECOND PIVOT"
> section) where Carlos hand-shaped a fresh island and explicitly settled on it. **The new terrain
> is a different footprint, size, and shape** — currently centered on world origin, roughly
> 1024×1024m (World Size "Medium"), not the old 4103×7085 non-square terrain this doc's coordinates
> assume.
>
> **What that breaks:**
> - **§3 "Biome regions in world coordinates" is entirely invalid.** Every X/Z range in that table
>   is measured against blockout markers on the old terrain footprint. The 9 MRM-58 location
>   blockouts (Camp/Dock/Glade/Cabin/Mine Entrance/Flak Tower/Mine Exit/Well/Chapel) were already
>   flagged orphaned after the *first* regen and are now doubly stale — not repositioned for either
>   of the two terrains since. Biome regions need re-anchoring from scratch against the current
>   shape, not adjusted from these old numbers.
> - **The custom tools §6b describes building — `BiomeGrassSetup.cs` and `BiomeVegetationSetup.cs`
>   under `Assets/_Project/Code/Editor/`, menu `Tools/Mr. Moonlight/Vegetation/` — no longer exist
>   in the repo** (confirmed via `git status`, both show deleted). Don't assume they're available;
>   confirm before referencing them, and if vegetation work resumes, decide whether to rebuild them
>   or use a different path (see below).
>
> **What's still genuinely reusable:** §2 (terrain layers *are* the biome masks — a strategy
> decision, not a coordinate), §4 (per-biome content plan / species palette / colour-coding), §5
> (the technical fixes needed — no LODGroups, GFF grass can't be detail meshes, MeshCollider vs
> CapsuleCollider). These don't depend on where the terrain's edges are.
>
> **A path not evaluated when this doc was written, worth considering now:** Gaia itself has a
> native rule-based Spawner system (`Spawner`/`SpawnRule`) that can paint textures, spawn terrain
> trees, terrain details, *and* arbitrary GameObjects/props from one rule set — masked by painted
> terrain texture layer, slope, height, noise, or a hand-drawn `PolyMask` region — with rule
> resolution modes (`Fittest`/`WeightedFittest`) that directly address the old Vegetation Spawner's
> hard-cutoff/overlap limitations. Not proven out hands-on yet (no cross-species spacing guarantee
> confirmed). Full research write-up in `Docs/mrm70-biome-vegetation-kickoff.md`.
>
> **Current open gaps and the actual next-session starting point live in
> `Docs/mrm70-biome-vegetation-kickoff.md`** (2026-08-29) — supersedes `mrm70-pause-2026-08-26.md`
> for this purpose, which is now historical only (also written against the pre-regen terrain).


Written 2026-08-25, branch `mrm-70`. Inputs: `Docs/Design/Island-Terrain-Reference/Map/biomes.png`,
the 20 `Vibe/` reference images, `Vegetation Spawner Documentation.pdf`, and the live `Island`
scene / `Terrain` state read back through UnityMCP.

Supersedes nothing; sits on top of `Docs/mrm70-prefab-build-summary.md` (what exists) and
`Docs/mrm70-vegetation-placement-kickoff.md` (where we left off).

---

## 0. Feasibility verdict

**Yes — the 8 biomes are buildable with Vegetation Spawner, but only if terrain painting comes
first and drives the vegetation, not the other way round.** See §2. Four things need fixing before
a single tree is spawned (§5); none of them is hard, but none of them is optional.

One blocker is outside my control: **the package is not on this machine yet** (§1).

---

## 1. Blocker — the asset has not been downloaded

`Vegetation Spawner Free` is on Carlos's account but is **not** in the Asset Store download cache:

```
C:\Users\calva\AppData\Roaming\Unity\Asset Store-5.x\
  ALP\  AsAlex\  HATOGAME\  IgniteCoders\  Nobiax Yughues\
  Pedro Verpha\  Queen\  rpgwhitelock\  Unity Technologies\
```

No `Staggart Creations` folder, no matching `.unitypackage`. "Added to account" is not the same as
"downloaded" — same situation as AllSky (see the `asset_store_acquisition_workflow` memory).

**Carlos needs to do one click:** Package Manager → **My Assets** → *Vegetation Spawner Free* →
**Download** (Download only — do *not* press Import, that pulls in the demo scene and samples).

Once the `.unitypackage` lands in the cache I extract only what's needed with the `tar` technique
already used for AllSky, so nothing unnecessary enters `Assets/`. Expected keep-list, based on the
documentation:

| Keep | Why |
|---|---|
| `Runtime/` — `VegetationSpawner`, `SpawnerBase`, `TerrainSampler`, `VegetationExtensions` | the actual system + the terrain helper functions the docs point at |
| `Editor/` — inspector/UI scripts | the tool is editor-time only; without these there is no UI |
| any `.asmdef` files | dropping these silently moves the scripts into Assembly-CSharp |
| `Shaders/` *(only if referenced)* | verify after extraction — grass uses Unity's built-in terrain detail shaders, so this may be droppable |
| **Drop** demo scenes, demo terrains, sample prefabs/textures, README/PDF | pure bloat against the 1 GB WebGL ceiling |

> **RESOLVED 2026-08-25.** Package downloaded and extracted: `Assets/ThirdParty/VegetationSpawner/`,
> **212 KB** of the ~4.5 MB package (Editor + Runtime + asmdefs; `_Demo/` dropped). Compiles clean,
> both assemblies resolve.
>
> **There is no free-version cap.** `treeTypes` and `grassPrefabs` are plain unbounded `List<>`s and
> a grep for licence/PRO/limit gating finds nothing. The biome plan is limited only by the perf
> budget. Confirmed additionally from source: `GrassType.Texture` is the *default* grass mode
> (§5.2 is the intended path, not a workaround), grass exposes `alignToGround`, and grass
> prototypes carry `mainColor`/`secondaryColor` tints — which means **grass needs no material
> variants at all**, only trees do (§5.4).

---

## 2. The core strategy — terrain layers *are* the biome masks

This is the whole plan in one idea, so it is worth being explicit.

Vegetation Spawner does **not** place vegetation inside hand-drawn regions. It spawns by *rules*:
height range, slope range, terrain-layer mask, spawn chance, noise. From the documentation:

> "**Terrain layer masks** — an option available for both trees and grass, specifies on which
> terrain material the item should spawn. An item will only spawn on the materials added to this
> list, so is automatically excluded from other materials."

That single feature is how we get 8 distinct biomes out of a rule-based spawner. So:

1. **Paint the ground first.** Each biome gets its own TerrainLayer. Painting the splatmap *is*
   drawing the biome map.
2. **Mask every species to its biome's layer.** Autumn birches spawn only on the autumn-leaf layer,
   boulders only on the rock layer, oasis flowers only on the moss layer, and so on.
3. Biome boundaries become **splatmap blends**, which gives the smooth transitions asked for
   *for free* — and a hard-painted edge gives the beach its abrupt cut, also for free.
4. The same painted layers feed the **footstep sound system**, which is a separate MRM-70
   acceptance criterion. One pass, two requirements.

**Consequence: the task order in the kickoff doc is inverted.** Paint, then spawn. Spawning first
would have to be redone.

### The 8-layer budget (hard constraint)

Unity terrain blends **4 layers per splatmap pass**. Layers 5-8 cost a second full terrain render
pass; 9+ costs a third. On WebGL at 960×540 with a 4103×7085 m terrain, **8 is the practical
ceiling and 8 is exactly what we need** — one per biome. This maps 1:1, which is lucky, and it
means *no biome may claim a second layer* without taking one from another biome.

| # | Biome (marker colour on `biomes.png`) | TerrainLayer | Footstep surface |
|---|---|---|---|
| 1 | Forest (blue) | `TL_TSA_Ground_Grass_A` | grass |
| 2 | Glade (green) | `TL_YFGM_Grass05` | grass |
| 3 | Autumn forest + campsite (pink) | `TL_YFGM_GrassLeafs01` | leaves |
| 4 | Flak tower (red) | `TL_YFGM_Dry02` | dry grass |
| 5 | Mountain / mine entrance (orange) | `TL_TSA_Ground_Rock` | stone |
| 6 | Beach (yellow perimeter) | `TL_TSA_Ground_Sand` | sand |
| 7 | Fountain oasis (turquoise) | `TL_TSA_Ground_Grass_Moss` | grass/soft |
| 8 | Eerie forest (black) | `TL_TTP_GroundDryLeaves01` | leaves |

Note #1 vs #2 and #3 vs #8 use *different* grass/leaf textures on purpose — not for looks, but
because two biomes sharing a layer cannot be told apart by a spawn mask. The glade must stay
treeless while the forest next to it is dense; that only works if they are different layers.

**Wood and concrete are deliberately absent.** Nothing in the 51 prepared layers is a wood or
concrete ground texture, and painting one would waste a slot on a surface that only exists where a
*mesh* is (dock planks, cabin floor, flak tower apron). Those footsteps should come from the prop's
own collider, not from the terrain — which is the correct architecture anyway. Flagging it because
the acceptance criterion names them.

### Painting method

Not by hand in the terrain inspector — 29 km² of terrain, and hand-painting is not reproducible.
Instead a **local editor script** that takes a biome definition (polygon + layer + falloff width)
and writes the splatmap procedurally, with a hard-edge flag for the beach. Carlos runs it himself
and re-runs it when a boundary moves. Rationale: `feedback_build_local_tools` memory — shipping a
tested script beats hundreds of conversational round-trips, and the boundaries *will* be iterated.

---

## 3. Biome regions in world coordinates

`biomes.png` is a scene-view screenshot, not the raw heightmap, so I anchored it against the nine
real blockout-marker positions read out of the live scene rather than guessing from the image.

**Correction to an existing note:** `Docs/mrm70-vegetation-placement-kickoff.md` says
"+X = east, **-Z = north**". That is wrong. Chapel (northernmost on the map) sits at Z 5668 and
Camp/Dock (southernmost) at Z 4059-4273, so **+Z = north**. Two independent marker pairs confirm it
(Mine Entrance X 841 / Flak Tower X 1350 at equal Z; Camp X 1003 / Glade X 1405 at equal Z).

Derived transform (~2.6 m per map pixel, uniform):

```
world_X ≈ 1003 + (px_x - 210) * 2.6
world_Z ≈ 4273 + (604 - px_y) * 2.6
```

Verified against markers not used to fit it — Chapel, Well and Mine Exit all land inside their
expected coloured regions, and the Well lands in the middle of the turquoise oasis blob, which is
exactly what the brief says should be there.

| Biome | X range | Z range | Approx size | Anchor |
|---|---|---|---|---|
| Eerie forest (black) | 885 – 1070 | 5320 – 5790 | 185 × 470 m | Chapel 992/5668, Mine Exit 1003/5339 |
| └ Fountain oasis (turquoise) | 940 – 1030 | 5425 – 5570 | 90 × 145 m | Well 1000/5484 |
| Flak tower (red) | 1250 – 1470 | 4855 – 5040 | 220 × 185 m | Flak Tower 1350/4952 |
| Mountain (orange) | 755 – 990 | 4930 – 5115 | 235 × 185 m | Mine Entrance 841/4949 |
| Forest (blue) | 730 – 1550 | 4550 – 4920 | 820 × 370 m | Cabin 1154/4652 |
| Autumn forest (pink) | 730 – 1550 | 3920 – 4560 | 820 × 640 m | Camp 1003/4273, Dock 1105/4059 |
| └ Glade (green) | 1367 – 1445 | 4232 – 4310 | ~78 m circle | Glade 1405/4273 |
| Beach (yellow) | coast perimeter | — | ~20-30 m band | — |

Player spawns at **(883, 80.6, 4489)** — the campsite end, on the autumn/forest boundary.

**These are estimates read off a screenshot and need eyeballing in-scene before painting.** They
are good enough to build the first pass from and to iterate against; they are not survey data.

---

## 4. Per-biome content plan

Read against the reference images, the polycount tiers from asset-prep, and what each biome has to
do for gameplay.

Density tiers used below: **dense** ≈ 0.20-0.30 trees/m², **medium** ≈ 0.08-0.12, **sparse** ≈
0.02-0.04, **accent** = hand/rare. Starting numbers only — they get tuned against a real WebGL
build, and per Carlos's standing call they do **not** go into `MoonlightTunables` until a real
frame-rate problem shows up (`feedback_tunables_during_prototyping`).

### Forest (blue) — the classic dense one
*Ref: `Vibe_FoggyForestPath`, `Vibe_SnowyRockForest`, `Vibe_WaterfallStream`, `beach 2`*
`Vibe_FoggyForestPath` is the single best style anchor in the whole folder — it is already the
dithered/pixelated retro-realism look our materials target, so match it directly.
- Trees **dense**, green: `RF_Tree1` + `RF_Tree4` as the backbone (~300-800 tris, in budget),
  `RF_Tree2`/`RF_Tree3` (976-1,378 tris) as **sparse** accents only — never as base density.
- Understory: `RF_Fern1/2`, `TSA_Fern_A/B/C`, `RF_Bush1-3` medium; `RF_Log1-3`, `RF_Stump1/2`
  sparse; `RF_Sapling1/2` scattered.
- Grass: `TSA_Grass_A` + GFF green cards, dense. **Avoid `TSA_Grass_B/C/D` at density** — they read
  as "grass" but are 1,000-1,268 tri 3D clumps, not billboards.

### Autumn forest + campsite (pink)
*Ref: `autumn forest 1` (orange canopy, thick leaf litter, warm light), `autumn forest 2` (pale
birch trunks, low red/orange scrub)*
- Brief says **prefer birch over pine**. We have no birch mesh. Closest available read is a
  pale-trunk variant of `RF_Tree1`/`RF_Sapling` with an autumn-tinted atlas — see §5.4. Flagging
  rather than silently substituting pines.
- Trees medium-dense, autumn material variant. Ground: leaf-litter layer, `TSA_GrassDry_A/B/C` and
  `TSA_Heather_A/B` for the red-brown scrub in `autumn forest 2`.
- **Campsite sub-area is deliberately thinner** — leave a clear ~40-50 m pad around
  (1003, 4273) for tents and props. Enforced by the collision cache (§5.3), not by hand.

### Beach (yellow)
*Ref: `beach 1` (grey-brown sand, scattered pebbles, big bleached driftwood logs), `beach 2` (dense
conifer wall cutting straight to bare sand — the transition reference)*
- Almost empty. `RF_Log1-3` and `RF_Boulder1-5` **accent** only, sparse enough to read as debris.
- No grass, no trees. **Hard-edged splatmap boundary** — the one place we do *not* blend.

### Eerie forest (black)
*Ref: `eerie forest 1/2/4/5` — bare trunks, no canopy, black ground, red sky tinting everything*
- **Sparse by design** so the well reads at distance. Dead-material variants of `RF_Tree1/2` with
  branches only, `RF_Stump1/2`, `RF_Log1-3`. No bushes, no ferns, minimal grass.
- The red tint is **lighting, not materials** — it has to switch at the well, so it must come from
  `TimeManager`/`SunController` + skybox, not from baked-red textures. Confirms the existing
  MRM-47/69 work is the right hook.

### Fountain oasis (turquoise)
*Ref: `fountain 1` — deep blue-lit clearing, glowing white/blue flowers carpeting mossy grass*
- **No trees inside** (they'd hide the statue); ring the edge instead. Dense GFF flower cards +
  `TSA_Grass_A` + `TSA_Plant_A-D` on moss.
- Same lighting note as above: blue → red is a light/skybox change.

### Flak tower (red)
*Ref: `flaktower 1` — open golden dry-grass meadow ringed by autumn trees, **no leaf litter***
- Deliberately **open** — it is an enemy spawn arena. Trees sparse and pushed to the perimeter.
- Dry grass dense, GFF flowers scattered, a few pines. Brief explicitly says no ground leaves, so
  this is the one autumn-adjacent biome on a dry-grass layer instead of a leaf layer.

### Mountain (orange)
*Ref: `mountain 1` — grey scree and boulder fields, sparse conifers clinging on, low scrub in the
gaps*
- **Rocks lead, vegetation follows.** `RF_Boulder1-5` dense, trees sparse, `TSA_Heather_A/B` and
  `TSA_Plant_D` in the pockets.
- Boulders here are **walkable and path-blocking**, so they need real collision — see §5.3.

### Glade (green)
*Ref: `glade 1` — misty conifer ring around an open grass clearing; `fountain 4` for the flower
scatter*
- **Circular, treeless centre** (the telescope + wolf fight), dense pine ring on the boundary.
- Grass dense, flowers sparse. The treeless centre is what forces glade onto its own layer (§2).

### Barriers (magenta lines)
Rock walls with collision. **Do not spawn these with the spawner** — a random scatter will not
reliably block a player, and Vegetation Spawner explicitly guarantees only that points never
overlap, not that they form a wall. Place them as real GameObjects along the magenta polylines
(scripted from a coordinate list, same tool as the painter), then let the spawner's collision cache
keep vegetation out of them. Exceptions per the brief: flak tower and eerie forest use water/other
mechanisms instead.

Outside the walkable perimeter: sparse silhouette-only cover, enough to read from the shore. Empty
interior spaces stay empty for now, per the brief.

---

## 5. What must be fixed before spawning anything

All four are consequences of how Unity's terrain system actually works, verified against the live
prefabs.

### 5.1 No prefab has an LODGroup — trees will all face the same way
Documentation, Troubleshooting: *"Random tree rotation and sink amount isn't working — Tree prefabs
require a LOD Group setup for this functionality to work."*

All 53 prefabs report `LODGroup=False`. Without one, every tree in a dense forest spawns at
**identical Y rotation**, which is immediately obvious and ugly. Fix: add a single-level LODGroup to
the tree/rock/log prefabs.

This is also the lever for the real WebGL perf risk: our trees are plain meshes, **not** SpeedTree
or Tree Creator, so Unity's tree **billboard system cannot apply to them** — at `treeDistance` 1500
every tree in range renders as a full mesh. An LODGroup cull threshold is the only way to drop
distant trees.

To be explicit, because the kickoff doc warns against hiding perf problems: **LOD culling is not
the same as shortening the fog distance.** Fog stays at 1500 m as the stress test. But I am not
setting a cull threshold silently — it needs a decision (§7).

### 5.2 The GFF grass prefabs cannot be used as detail meshes
Documentation: *"The terrain system does not support grass prefabs with a LOD Group component. You
must use a prefab that consists out of a single Mesh Renderer."*

All 12 GFF prefabs are crossed double-quads = **2 MeshRenderers**. Invalid as detail meshes.

Fix, and it is the better option anyway: GFF are billboard *textures* with no source mesh, and the
spawner supports `GrassType.Texture`. **Feed the source textures in as detail textures**, not the
prefabs. Unity's built-in grass billboard cross-quads and wind-animates them for free, cheaper than
our hand-built prefab. The 12 prefabs stay useful for hand-placed set dressing.

### 5.3 Collision — and one consequence for the enemy vision mechanic
Answering Carlos's question directly:

- **Terrain detail/grass instances never get colliders.** Anything that must block the player cannot
  be grass. Not a problem — nothing in the grass tier should block anyway.
- **Terrain tree instances *do* get colliders** from the prefab, so trees and boulders spawned as
  "tree" species block the player as required. Boulders as a barrier just means registering them as
  a tree species.
- **But every RF prefab currently uses a non-convex `MeshCollider`.** Fine for one hand-placed hero
  rock; at forest scale it is thousands of mesh colliders baked into the TerrainCollider — bad for
  WebGL memory and bake time. Recommend swapping to `CapsuleCollider` on trees/saplings and
  `BoxCollider`/convex `MeshCollider` on boulders and logs, keeping non-convex mesh colliders only
  for hand-placed hero rocks and the barrier walls.
- **Tree collider layers — earlier concern WITHDRAWN.** I originally flagged that terrain tree
  colliders inherit the Terrain's layer (`Ground`), which would stop an enemy sightline raycast
  distinguishing a trunk from the ground. That is Unity's *default*, but it is overridden by
  `Terrain.preserveTreePrototypeLayers`, which Vegetation Spawner sets to `true`
  (`SpawnerBase.CopySettingsToTerrains`). Verified on the live terrain: `preserveTreePrototypeLayers
  = True`, so **tree colliders keep the prefab's own layer** and the vision-block mechanic can put
  trees on a dedicated layer whenever the AI work needs one. No architectural change required.
  (Related: the `unity_layers_state` memory — the `Player`/`EnemyMovement` layers the docs describe
  were never actually created, so that layer still has to be made.)
- **Collision verified working**: 22 of 31 horizontal chest-height raycasts fired through the
  forest biome were blocked inside 60 m. Trees stop the player and will block sightlines.

### 5.4 Per-biome material variants
Carlos is right that one material per mesh will not carry 8 biomes: the same tree has to read green
in the forest, orange in autumn, and grey-dead in the eerie forest. Currently all RF trees, saplings,
stumps and logs share a single `M_RF_Trees` sampling one `Trees.tga` atlas.

Two ways, and I would mix them:

- **Tint-only variants** (`_BaseColor` on a material variant, same texture): free — zero extra
  texture memory. Good enough for subtle shifts, but a global tint hits bark and foliage together,
  so it cannot produce "grey dead trunk + no leaves".
- **Hue-shifted atlas copies** (recolour only the foliage regions of `Trees.tga` offline, produce
  `Trees_Autumn` / `Trees_Dead`): costs one small atlas per variant, looks far better, and is a
  scripted offline job rather than hand work.

Recommend hue-shifted atlases for **autumn** and **dead**, tint-only for everything else. Budget
impact is small — the RF atlas is one texture — but it must be counted against the 1 GB ceiling.

**This interacts with the §1 unknown:** every material variant is a separate prefab, and every
prefab is a separate species in the spawner. If the free version caps species count, the variant
plan has to shrink to fit. That number decides how ambitious §4 can be.

### 5.5 Terrain settings to change
| Setting | Now | Proposed | Why |
|---|---|---|---|
| `Terrain.drawInstanced` | `False` | `True` | GPU-instanced terrain rendering; needed for the instanced tree path. Every vegetation material already has `enableInstancing = true`. |
| `detailObjectDistance` | 40 m | 80-120 m | 40 m is very short; grass pops in at the player's feet. Costs fill rate — tune against the build. |
| `detailResolution` | 1024 | decide | Over a 4103×7085 m terrain that is ~4.0 × 6.9 m per detail cell — coarse. Raising it to 2048 quadruples detail-map memory. Density-per-cell may cover it instead; measure before changing. |
| `treeDistance` | 1500 | **leave** | Matches the deliberate 1500 m fog stress test. |

---

## 6. Recommended order of work

1. Carlos downloads the package (§1) → I extract selectively and verify the free version's limits.
2. Fix prefabs: LODGroups, colliders, material variants (§5.1, 5.3, 5.4).
3. Build the splatmap painter script; assign the 8 layers to the Terrain (§2).
4. Paint biomes from the §3 coordinates; eyeball and correct boundaries.
5. Configure the spawner per biome with layer masks (§4).
6. Place barrier rock walls (§4) and rebuild the collision cache.
7. WebGL build as the stress test; tune density and LOD cull from real numbers.
8. Wire footstep surfaces to the 8 layers; wood/concrete from prop colliders.

---

## 6b. What was actually built — 2026-08-25

Steps 1-6 of the plan above are done. Grass/details (§5.2) and material variants (§5.4) are not.

### The bug this pass caught

**Every RetroRealism prefab was unusable as a terrain tree**, and nothing before this pass would
have revealed it. The source FBXs are authored Z-up at 1/100 scale, and the prefabs built in the
earlier MRM-70 pass compensated on the prefab *root* — rotation `(270.02, 0, 0)`, scale
`(100, 100, 100)`. That is invisible for an ordinary GameObject in a scene, which is why it went
unnoticed.

Unity's terrain builds each tree instance's matrix from the `TreeInstance` alone —
`TRS(position, AngleAxis(rotation, up), (widthScale, heightScale, widthScale))` — and **ignores the
prototype prefab's root transform**. Placed as a terrain tree, every tree rendered flat on the
ground at 1/100 size. Confirmed visually in the editor before writing any fix, and confirmed that
neither `bakeAxisConversion` nor `globalScale` addresses it (the FBX declares itself Y-up while its
geometry is Z-up, so Unity has nothing to detect).

Fixed by baking the root transform into new mesh assets under `Meshes/Baked/`, feet-origin and
centred, and resetting all 21 prefab roots to identity.

### Tools added (all under `Assets/_Project/Code/Editor/`, menu `Tools/Mr. Moonlight/Vegetation/`)

| Tool | Does |
|---|---|
| `VegetationTerrainPrep` | Bakes the root transform into mesh assets; assigns tier-appropriate colliders and LODGroups. Has a **Report** mode that changes nothing. |
| `TerrainComposer` | Flattens the four staging pads. **Run before the painter** — the painter's beach rule reads height. |
| `BiomePainter` | Writes the 8-biome splatmap. |
| `BiomeVegetationSetup` | Configures all 16 tree/prop species with their layer masks, then spawns. |
| `TreeOverlapCull` | Removes overlapping trees (see below). |

Run order: **Prep → Composer → Painter → Setup → Cull.**

### Prefab tiers and colliders

Trunk capsules are sized from each tree's dedicated `*_Collision`/`UCX_` hull, then capped at
`height/30`. The cap matters: `RF_Tree4`'s hull measures 2.66 m across at the root flare, which
would have stopped the player 1.3 m short of the trunk; it becomes 0.84 m. Saplings have no hull
mesh at all, so they fall back to 15% of canopy width — without that, `RF_Sapling1` got a 1.08 m
capsule on a 2.42 m shrub.

Final radii: Sapling1 0.15, Sapling2 0.18, Tree1 0.29, Tree2 0.50, Tree3 0.69, Tree4 0.84 m.

Props (boulders/logs/stumps) get convex `MeshCollider`s — shape matters there, since the brief wants
boulders both walkable and path-blocking. Bushes and ferns are Detail tier: no LODGroup (the terrain
detail system rejects prefabs that have one) and no collider.

### Proportion audit

Checked against a 1.8 m player, since nothing was scaled in Blender. **Fundamentally sound.**
Trees 13.6 / 18.3 / 20.7 / 25.1 m; boulders 0.7-4.3 m; logs 5.2-10.1 m; bushes 1.0-2.2 m; ferns
0.2-0.5 m. Three assets are misnamed rather than mis-scaled, and are better used as what they
actually are:

- `RF_Sapling1` is 2.15 m wide × 2.42 m tall — bush-proportioned, not a sapling.
- `RF_Stump2` is a 4.38 m broken trunk — a snag. Now used as eerie-forest silhouette.
- `RF_Log3` is 10 m long and 3.1 m thick — a fallen giant. Accent only, never at density.

### Terrain sculpting

Four pads flattened, each blending out through a smoothstep ring rather than stamping a mesa:

| Pad | Radius | Height spread before → after |
|---|---|---|
| Flak tower | 60 m | 29.1 m → 0 m |
| Oasis | 40 m | 11.8 m → 0 m |
| Campsite | 30 m | 9.0 m → 0 m |
| Glade | 38 m | 7.5 m → 0 m |

The flak tower arena previously spanned 54.5 m of fall across 100 m with part of it *below* the
sea plane — not somewhere a fight could happen.

**These four pads then keep themselves clear of trees for free.** Every tree species carries
`slopeRange.x = 1.5°`, and a flattened pad has ~0 slope, so no tree spawns on one — while the
sloped falloff ring around it still plants. That is exactly "circular open plain surrounded by
pines", with no per-biome exclusion zones needed. Result: 1 tree in the whole glade, 0 in the oasis,
7 across 4 ha of flak tower.

### The hinterland threshold problem

Painting the non-biome hinterland with the same pure forest-grass layer as the forest biome would
have made a layer-0 mask spawn trees across all 7.9 km² of it — roughly **90,000 trees** instead of
the ~3,500 the forest wants. Fixed by giving the hinterland a *blended* signature (62% grass / 38%
rock) so the spawner's per-mask `threshold` separates them: forest species use 0.75 and fire only
inside the real forest (which paints 1.0), while a sparse `Hinterland tree` species at threshold
0.3 covers the rest for distant silhouette. Verified: forest biome reads L0 = 1.00, hinterland
reads L0 = 0.62.

Worth knowing for future tuning: the mask test is a **hard cutoff at `threshold`, not a gradient**.
`VegetationSpawner.Trees.cs` compares a 0-1 `Random.value` against a 0-100 spawn chance, so any
splat weight above the threshold passes essentially always.

### Overlap

Carlos's rule is that trees must never overlap. The spawner cannot deliver this alone: it samples a
Poisson disc **per species** (`item.seed + seed`), so "spawn points for a species will never
overlap" says nothing about two *different* species landing together — and with 16 species it
happens constantly. The collision cache is no help either, since it explicitly skips
`TerrainCollider`, which is where tree colliders live.

`TreeOverlapCull` handles it afterwards: canopy circles from the baked mesh bounds × `widthScale` ×
a 0.8 canopy factor, largest-first so mature trunks survive and crowding saplings are thinned.
**668 of 10,823 trees culled (6.2%), in 11 ms.** Rocks, logs and stumps are exempt on purpose —
piled rock is what makes a believable scree slope or barrier wall.

### Result

**17,350 instances, 35 prototypes, spawned in 18 s.** Density per biome, against the brief:

| Biome | trees/ha | Brief asked for |
|---|---|---|
| Forest | 76.1 | dense ✓ |
| Autumn | 39.9 | medium, denser away from camp ✓ |
| Eerie | 10.6 | sparse, so the well reads at distance ✓ |
| Mountain | 7.0 *(248 instances, only 31 trees)* | rocks lead, vegetation secondary ✓ |
| Flak tower | 1.8 | open arena ✓ |
| Glade | 1.4 *(1 tree total)* | treeless centre ✓ |
| Oasis | 0.0 | open, statue unobstructed ✓ |

Terrain settings now: `drawInstanced` on, `treeDistance` 1500 (unchanged — the deliberate stress
test), `treeBillboardDistance` 1500 and `treeMaximumFullLODCount` 5000 (our trees have no billboard
LOD, and the stock 25 would have capped full-mesh trees at 25 on screen), `detailObjectDistance`
110, alphamap resolution raised 512 → 1024 (4.0 × 6.9 m per texel; at 512 the 78 m glade was only
10 texels across).

---

## 6c. Second pass — 2026-08-25, same day

### The tree texture bug (the reason the trees looked wrong)

Carlos flagged that the trees "don't look at all like the RetroRealism trees". They didn't, and the
cause was in the earlier prefab pass, not in the spawner.

Every tree FBX has **three** material slots — `Trees1` (bark), `Dirt`, `BranchFir` (needles) — and
the earlier pass collapsed all three onto a single `M_RF_Trees`, reasoning that the FBX's embedded
materials carry no texture references and therefore all sample the same shared atlas.

Half of that was right. The embedded materials really are textureless, and the per-tree
`T_RF_Tree1..4_BaseColor.tga` files really are byte-identical hard links of the shared
`T_RF_Trees_BaseColor.tga` — so dropping them lost nothing. But the conclusion didn't follow:
**`Trees.tga` is the BARK atlas and is RGB with no alpha channel at all.** The needles live in a
separate 256×256 RGBA `BranchFir.png` that was never imported.

So the branch submesh — **1,060 of RF_Tree2's 1,204 triangles**, i.e. most of every tree — was
alpha-clipping against a texture with no alpha. Alpha came back as 1 everywhere, so every needle
card rendered as a solid opaque quad sampling bark pixels. That is exactly the "flat grey slabs"
look.

Imported from `E:\Props\Environment\Raw\LonelyForest\Texture\`:

| New texture | Purpose |
|---|---|
| `T_RF_BranchFir_BaseColor.png` | the needles — the actual fix |
| `T_RF_BranchFirDead_BaseColor.png` | dead variant, for the eerie forest |
| `T_RF_Dirt_BaseColor.tga` | the ground patch at each trunk base |
| `T_RF_TreesDead_BaseColor.tga` | dead bark, for the eerie forest |

New tool `VegetationMaterialFix` rebinds every submesh to its correct material. It reads slot order
from the **raw FBX renderer's `sharedMaterials`**, because `AssetDatabase.LoadAllAssetsAtPath`
returns embedded materials *alphabetically* — which is not submesh order and would have bound bark
to the needles on every tree.

`alphaIsTransparency` also had to be set by hand on the two branch textures; the project's
`MoonlightTextureImporter` does not set it, and without it cutout edges get dark halos.

**Lesson worth keeping:** "these materials have no texture references, so they must all share one
atlas" is not a safe inference. Check whether the source pack has a texture per material slot, and
check whether the atlas even has an alpha channel before pointing an alpha-clipped material at it.

### Density doubled

`BiomeVegetationSetup.DensityMultiplier = 2.0`. Poisson point count goes as 1/distance², so
distance is divided by √2 — 2.0 genuinely means twice as many trees rather than twice as close.
`TreeOverlapCull.CanopyFactor` dropped 0.8 → 0.6 at the same time, because at 0.8 the cull was
eating most of the increase instead of letting the forest thicken. Trunks stay well clear either
way: the widest trunk capsule is 0.84 m and 0.6 still holds two RF_Tree4s 5.2 m apart.

| Biome | before | after |
|---|---|---|
| Forest | 76.1 trees/ha | **150.9** |
| Autumn | 39.9 | **79.3** |
| Eerie | 10.6 | 18.6 |
| Mountain | 7.0 | 10.2 |
| Flak tower | 1.8 | 2.8 |
| Glade | 1.4 | 9.9 |

**34,816 tree/prop instances** (from 17,350). Cull removed 1,423 of 21,737 trees (6.5%) in 11 ms.
Respawn takes ~35 s and will time out a single MCP call — it does finish, so re-query rather than
re-running it.

### Grass and ground detail added

New tool `BiomeGrassSetup`, **27 detail prototypes**, ~13 s to spawn.

- **Billboards** (`GrassType.Texture`) for the GrassFlowers cards — no source mesh exists for them,
  and this is the spawner's own default mode. Also the fix for the 12 GFF *prefabs* being unusable
  as detail meshes (crossed double-quads = 2 MeshRenderers; the terrain requires one).
- **Meshes** for the real understory — TSA and RetroRealism ferns, bushes, grass clumps.
- **Per-biome colour is free.** Detail prototypes carry `mainColor`/`secondaryColor`, so autumn gets
  orange grass and the eerie forest drained grey from the *same* textures. Confirms §5.4: only
  trees need real material variants.

Two things that had to be discovered the hard way:

1. **`density` must be set explicitly or nothing spawns.** The terrain runs in `CoverageMode`
   (Unity 2022.2+) where `DetailPrototype.density` *is* the coverage amount, and the spawner copies
   it straight across (`d.density = item.density`). `GrassPrefab` defaults it to **0** — which
   happily creates 27 prototypes and zero blades. First spawn produced exactly that.
2. **Billboards and meshes need very different coverage.** At a shared coverage of 6 the 1-2 m
   ferns and bushes fused into a continuous chest-height hedge that walled the player in. Split to
   `MaxCoverageBillboard = 6` / `MaxCoverageMesh = 0.7`.

**Grass tuning is not finished.** The detail map is verified at full coverage (255) in the forest,
so the data is right, but the GrassFlowers cards are thin stalks on a mostly-empty 512×512 at
real-world scale, so under dusk lighting the ground still reads sparser than the numbers suggest.
The remaining levers are card size (`minMaxHeight`/`minMaxWidth`) and tint saturation, not coverage
— both judgement calls better made looking at it live than tuned blind.

---

## 6d. WebGL reality check — 2026-08-25, builds 12-14

Three builds and two wrong diagnoses. Recorded in full because the failure modes are all invisible
in the Editor and will recur.

### Symptom 1: terrain invisible in-game (build 12)

Trees, grass, water and sky rendered; the terrain surface did not, so the sea showed through.

**First wrong guess:** `Terrain.drawInstanced`, which I had switched on this pass. Plausible (Unity's
default is `false`, and it's a separate draw path), but I never checked it against evidence before
shipping build 13. It was not the cause.

**Actual cause,** from the browser console: the first error is
`Shader Hidden/Universal Render Pipeline/Terrain/Lit (Base Pass): GLSL compilation failed, no
infolog provided` — the terrain shader never compiles on GLES3.

"No infolog provided" is the signature of exceeding a **hardware limit**; the driver rejects the
program without a message. Each of the 8 terrain layers carried a diffuse **and a normal map**, and
four also had mask maps, so a single 4-layer pass needed
`1 control + 4 diffuse + 4 normal + 4 mask = 13` fragment samplers before URP adds its shadowmap,
cascades and reflection probes. **GLES3 only guarantees 16 fragment texture units.**

**Fix:** cleared `normalMapTexture` and `maskMapTexture` on all 8 layers — **13 samplers → 5**, with
all 8 biomes preserved. At 960×540 with point-filtered pixel-art ground, terrain normal maps
contribute essentially nothing, so this is close to a free trade.

The 208 cascading failures (URP/Lit ×128, WaterShader, CopyDepth, CoreBlit, Skybox) are the same
ceiling being hit by other variants once the budget is gone.

**`drawInstanced` was left at `false` anyway** — it is Unity's default, it was this project's state
before this pass, and nothing measured ever justified turning it on.

### Symptom 2: rocks and logs had no collision at all

`TerrainCollider: MeshCollider is not supported on terrain at the moment.` ×18 in the WebGL console.

`VegetationTerrainPrep` gave the Prop tier convex `MeshCollider`s. **Unity's TerrainCollider only
accepts Capsule/Box/Sphere on tree prototypes** and silently discards mesh colliders, so every
boulder, log and stump was walk-through. The warning count (18) matches the number of Prop
prototypes exactly.

**Fix:** Prop tier now gets a `BoxCollider` from the baked mesh bounds. Verified across all 35 tree
prototypes: 18 box, 17 capsule, **0 mesh**. Tool updated so it cannot regress.

### The lesson under both

Neither failure is visible in the Editor. Under DX11 the terrain shader compiles fine, and prefab
assets are not terrain instances so no collider warning is ever raised. **These only exist as
terrain instances in a GLES3 player build** — which is precisely what `Docs/webgl-constraints.md`
warns about. Check the browser console against the *first* error, not the loudest one, and do not
ship a fix for an unverified theory.

Also: the last line of a browser log is usually `CONTEXT_LOST_WEBGL` from closing the tab. It is a
consequence of the tab closing, not a cause — do not read it as a crash.

---

## 6e. The mirror terrain — 2026-08-25, build 15

Once the terrain became visible (§6d) it looked wet: a blown-white sheen with a hard specular
streak, on sand *and* grass — i.e. all terrain, not one layer.

**Two independent causes.**

**1. Over-lighting.** `RenderSettings.ambientIntensity = 1` **and** `reflectionIntensity = 1`
against a bright placeholder skybox, with no tonemapper in the scene (there are no post-processing
Volumes at all). Everything washed toward white. Set to **0.4 / 0.25**; sand immediately read as
khaki and the distant island returned to green.

**2. The terrain really was a mirror — and the cause is not where you would look.** Four things were
tried and eliminated, in order:

| Tried | Result |
|---|---|
| `TerrainLayer.smoothness` → 0 on all 8 layers | no change |
| Material `_Smoothness0..3` → 0 (own material asset) | no change |
| Stray `_TERRAIN_INSTANCED_PERPIXEL_NORMAL` keyword cleared | no change |
| `drawTreesAndFoliage = false` (isolation) | isolated it to one blown-white ellipse on bare terrain |

The actual smoothness input is the **diffuse texture's ALPHA channel** — Unity terrain uses splat
alpha as smoothness. Every ground texture shipped fully opaque (alpha 255), i.e. **smoothness 1.0,
a perfect mirror**, which is why none of the exposed smoothness controls did anything.

**Fix:** zeroed the alpha channel on all 8 terrain diffuse textures. Specular vanished entirely.

Because of this, a terrain diffuse texture is not just colour: **its alpha is a material property.**
Any new ground texture needs its alpha authored to the roughness you want (0 = matte) before it goes
on the terrain.

### Terrain now owns its material

`Assets/_Project/Art/Environment/Terrain/M_IslandTerrain.mat`, replacing the URP package's shared
`TerrainLit.mat`. During diagnosis I edited that package material and Unity warned that assets in
immutable packages "can be lost without any warning during Package Manager operations" — the edit
was reverted, and the terrain now points at a material we own. Do not edit anything under
`Packages/` again.

### FPS counter

`Assets/_Project/Prefabs/UI/FPS Counter.prefab` + `Code/Runtime/UI/FpsCounter.cs`.

Self-contained: its own Canvas at the 960×540 reference resolution, `sortingOrder` 32000, raycaster
disabled, so it can be dropped into any scene alone with no HUD dependency. Bottom-left, shows both
FPS and frame time in ms, colour-coded green/amber/red at 50/30.

Written allocation-free (`TMP_Text.SetText` with format args, not string building) because a
performance readout must not add GC pressure to the frames it is measuring — which matters more on
WebGL than anywhere else. Uses `unscaledDeltaTime` so it stays honest through pauses and the MRM-17
death sequence.

**Lighting values are a proposal, not a settled look.** 0.4 / 0.25 were picked by eye against a
skybox that is still the unapproved placeholder from MRM-47/69. Revisit both together.

### Still open

- **Grass/details not spawned yet** — §5.2's texture-billboard path.
- **Material variants not built** (§5.4). Autumn and eerie currently reuse the green RetroRealism
  material, so those biomes read green rather than orange/dead. This is the single biggest visual
  gap right now.
- **Barrier rock walls not placed** — still needs the magenta polylines confirmed.
- **No WebGL build measured yet.** 17,350 instances at 1500 m draw distance is the stress test;
  the LOD cull threshold in `VegetationTerrainPrep` (`LodCullScreenHeight = 0.015`, ~780 m for a
  13.6 m tree) is a first guess and should be re-tuned from real frame times, not taste.

---

## 7. Open questions for Carlos

1. **Birch.** The autumn brief asks for birch over pine and we have no birch mesh. Retint an
   existing tree to a pale trunk, source a birch model, or accept pines?
2. **Tree LOD cull distance.** Needed for WebGL (§5.1), and adjacent to the "don't hide perf
   problems behind draw distance" rule — so: pick a number now, or spawn at full density first,
   measure, and decide from the real frame time?
3. **Vision-block mechanic.** Terrain trees all report as layer `Ground` (§5.3). Fine, or do enemy
   sightlines need trees on their own layer — which would mean real GameObjects instead of terrain
   trees for at least some of them?
4. **Barrier polylines.** The magenta lines are the least legible thing on `biomes.png`. Worth me
   drawing my read of them into the scene as gizmos for confirmation before building rock walls?
