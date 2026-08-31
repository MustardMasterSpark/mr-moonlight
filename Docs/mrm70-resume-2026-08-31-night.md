# MRM-70 — resume point, 2026-08-31 night

Branch `mrm-70`. Read this before touching vegetation. It supersedes
`Docs/mrm70-resume-2026-08-31-evening.md` for anything about grass or spawn configuration.

---

## Where things stand in one paragraph

The vegetation library is now split into two tiers. **95 collidable prefabs** sit in
`Assets/_Project/Art/VegetationPrefabs/` and are spawned as GameObjects by 9 Gaia biome spawners.
**72 no-collider prefabs** sit in `Assets/_Project/Art/VegetationPrefabs/GRASS PREFABS/` and are
destined for Unity's terrain detail layer, which nothing has been registered into yet. A brief
describing the 95 has been handed to ChatGPT for a distribution pass, and **we are waiting on that
answer.** While building it, an audit turned up three problems in the live spawn config — one of
them serious — and none are fixed.

---

## 1. The blocking item

**Waiting on ChatGPT.** Carlos gave it `Docs/vegetation-distribution-brief.md` plus screenshots and
asked for a per-biome distribution: species, spacing, max slope, scale range, role.

When the answer arrives, apply it. Do not start retuning weights independently in the meantime —
that is the whole point of the brief.

---

## 2. The three problems found, none fixed

### 2.1 🔴 Slope caps — the serious one

Every one of the **78 active spawn rules** caps max slope between **5° and 10°**. Gaia's default is
90°, and the check is a hard reject:

```csharp
// SpawnCriteria.cs — GetSlopeFitness()
if (slope < m_minSlope || slope > m_maxSlope) { return 0f; }
```

Measured against the live terrain (256×256 sample, 64.2 ha of land above Y=8):

| Slope | % of land | Cumulative |
|---|---:|---:|
| 0–9° | 29.4% | 29.4% |
| 10–19° | 42.3% | 71.7% |
| 20–29° | 18.3% | 89.9% |
| 30°+ | 10.0% | 100% |

Only **9.9% of land is under 5°**, **29.4% under 10°**. So each species is confined to a tenth to a
quarter of the island, and the 42% that is 10–20° hillside grows nothing. This is very likely the
largest single cause of the island reading dull — larger than any weight choice.

**Proposed and explained to Carlos, awaiting his go:**

| Type | Cap | Reason |
|---|---:|---|
| Trees (4 forests, Fountain, FlakTower) | **32°** | ~91% of land; trunk-float not yet visible |
| Dead / thin trees | **30°** | Thin trunks show the ground gap sooner |
| Rocks and boulders (Beach, Mountain) | **50°** | Already `rotateToSlope = true`, so they tilt to match |

Do **not** reshape the terrain to fix this — it was explicitly considered and rejected. The hills
do real work for a horror game, and this is one number per rule. Original values are preserved in
`Tools/vegetation/current_spawn_setup.csv`.

### 2.2 44 of 95 prefabs never spawn

Half the library is idle, including the entire `GraveKeepers` family, all six `Curse`/`Heretic`
trees, `AP_S_Tree_01` (33.8 m), `AP_GangshiTree_2` (34.8 m), the `PTree` family, and every
broken-trunk and stump piece. Listed as `— not spawned —` in the brief's §7. ChatGPT has been asked
to decide deliberately which stay unused.

### 2.3 Every species is locked at scale 1.00–1.00

One exception (`AP_Tree_04_GTree01_01_SM_2` at 1.45). Every instance of a given tree is therefore
dimensionally identical to every other one. The brief asks for real ranges.

---

## 3. What was built today

**30 `GRASS_*` prefabs**, verified 1 MeshFilter / 1 MeshRenderer / 0 LODGroup / 0 Collider:

- 20 `GRASS_TSA_*` — Unity TerrainSampleAssets, the carpet from Carlos's screenshot
- 10 `GRASS_Gaia_*` — 5 LawnGrass (DeadPatch, Spiky, Weeds, Wheat_01/02), 5 WildGrass (General,
  TallWhiteFlower, Understory_Clover/Foliage/Sticks)

Source art at `Art/Environment/Vegetation/GrassDetail/{TSA,Gaia}/`. Carlos then moved **42 further
no-collider prefabs** into the same folder himself — flowers, ferns, mushrooms, small plants — so
git shows those as deletions from the root. **They are moves, not losses.**

---

## 4. Rules established today — do not relitigate

**Grass is a Gaia spawner rule, not a separate tool.** `SpawnerResourceType.TerrainDetail` is
native; `ResourceProtoDetail` carries mesh-or-texture prototype, min/max width and height, density,
target coverage, healthy/dry colour, and the same `SpawnCritera[]` the tree rules use. The deleted
`BiomeGrassSetup` (commit `f306acc`) is **not** being rebuilt. Rules survive "generate new Gaia";
hand-painted detail does not, so Unity's Paint Details brush is a last-step touch-up only.

**The grass tier is out of scope for ChatGPT.** It is a coverage/density problem, not per-species
spawn weights, and it has no colliders. We set it ourselves.

**`Gaia Pro Assets and Biomes` stays declined as a package, but is a cherry-pick source.** 3.7 GB in
Playground, never installed. 14 files extracted by GUID today. Still available there: 11 more
`PW_LawnGrass` meshes and **16 legacy billboard cards** (`PW_Grass_Patch_01/02`, Dactylis, Phleum,
the flower cards) — the natural source if we want the cheap `GrassType.Texture` billboard tier.

**Gaia atlases go to 1024, not 512.** `PW_LawnGrass_00_D` packs 12 meshes' UV islands. Single-object
textures stay at 512.

**Pixelation is baked into the texture**, not the shader — so the PSX look survives when the terrain
substitutes its own waving-grass shader for RetroLit.

---

## 5. Next actions, in order

1. **Wait for ChatGPT.** Apply the returned distribution.
2. **Apply the slope-cap fix** at the same time (§2.1), once Carlos says go.
3. **Register the 72 grass prefabs as `TerrainDetail` rules** in the 9 biome spawners. Two rules
   learned the expensive way on the first island: `DetailPrototype.density` must be set explicitly
   (defaults to 0, silently spawns nothing), and billboards and meshes need very different coverage
   (the first island shipped billboard 6 / mesh 0.7).
4. **Remove the 5 ground plants still spawning as GameObjects in `Spawner_Glade`** —
   `TSA_GrassDry_C`, `TSA_Heather_A`, `AP_Neutral_Foliage_A_Weed01_SM`, `..._Weed02_SM`,
   `AP_Neutral_Tree_A_PrickelGrass01_SM`. They moved to the detail tier; the spawner does not know.
5. `detailObjectDistance` is the "only render grass near the player" lever — max 250 m, stock 40.
   Tune it against a real build, not the editor.

---

## 6. Known-stale elsewhere

- **MRM-70's Linear description** still names Vegetation Spawner (Staggart) as the placement tool
  and says Flora is not owned. Both wrong; noted in the 2026-08-31 comment but the description
  itself is unedited.
- **Flora is off** pending the phantom-tree rendering bug — unchanged today.
- **Terrain layers**: 10 exist and are wired, only Beach is painted.
- `Docs/mrm70-biome-vegetation-strategy.md` §5.2 now carries a superseded banner for tooling; its
  facts about the GFF single-MeshRenderer limit are still binding.

---

## 7. Files to read, in order

| File | Why |
|---|---|
| `Docs/vegetation-distribution-brief.md` | What ChatGPT was given. The full 95-prefab catalogue with measured sizes |
| `Docs/mrm70-unused-vegetation-inventory.md` | The grass tier: what was built, where the art lives, §7 on Gaia rules vs hand-painting |
| `Tools/vegetation/current_spawn_setup.csv` | The live 78-rule config as it stood before any change |
| `Docs/mrm70-biome-distribution-measured.md` | The measured size reference behind everything |
