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

