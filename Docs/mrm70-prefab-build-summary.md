# MRM-70 vegetation prefabs, materials & terrain layers - build summary

Built directly in the Mr. Moonlight Unity project (branch mrm-70) from everything under
`E:\Props\Environment\Prepared Props` / `Game Ready`. This is the step after asset-prep -
these are real, usable Unity assets now, not files sitting outside the project.

## What was built

| Type | Count | Location |
|---|---|---|
| Materials (vegetation) | 24 | `Assets/_Project/Art/Environment/Vegetation/<Pack>/Materials/` |
| Prefabs (vegetation) | 53 | `Assets/_Project/Prefabs/World/Vegetation/<Pack>/` |
| Terrain Layers | 51 | `Assets/_Project/Art/Environment/Terrain/<Pack>/Layers/` |

**Materials/Prefabs breakdown:**
- RetroRealism: 4 materials (`M_RF_Trees`, `M_RF_Boulders`, `M_RF_Bush`, `M_RF_Fern`) -> 21 prefabs
  (Trees/Saplings/Stumps/Logs all consolidated onto `M_RF_Trees`, since their 3 embedded submesh
  slots - `Trees1`/`Dirt`/`BranchFir` - carried no real texture references and all sample the
  same shared `Trees.tga` atlas per the earlier AO-bake finding). Tree1-4 additionally carry a
  `MeshCollider` built from their dedicated `_Collision.fbx`; everything else without one uses its
  render mesh directly as the collider.
- Terrain Sample Assets: 8 materials (one per shared material family, matching the source pack's
  own grouping) -> 20 prefabs, each built from the **existing native Unity mesh** at
  `Assets/ThirdParty/TerrainSampleAssets/Models/*.asset` with our pixelated material swapped in.
  The original TSA prefabs/materials (unpixelated, realistic PBR) are untouched at
  `Assets/ThirdParty/TerrainSampleAssets/Prefabs/` for comparison if ever wanted.
- Grass Flowers Free: 12 materials -> 12 prefabs. No source mesh existed for these (billboard
  texture cards only) - built as a **crossed double-quad** (two 0.5x0.5m quads at 90 deg to each
  other, feet-origin, shadows off) using Unity's built-in Quad primitive, no custom mesh asset.

**Terrain Layers breakdown:** Terrain Sample Assets ground (16), Terrain Textures Pack Free (5),
Yughues (29), Grass Flowers ground (1). Diffuse+Normal+Mask wired where the source had all three;
Terrain Textures Pack and Yughues have no Mask source (falls back to flat Metallic 0/Smoothness
0.1, matching their `analysis.md` notes from asset-prep). Tile size defaulted to 5x5m on all of
them - untuned, adjust per-layer once actually painted onto terrain.

## Excluded (per the 86 MB "game ready" cut)

`Poplar_Tree01` and its `Poplar_Bark`/`Poplar_Branches` texture sets - the only asset that got a
"do not use" verdict during asset-prep (8,198-20,248 tris, 10-65x the tree budget). Not built here
either.

## Optimization settings applied ("our optimization values")

- **Materials**: URP/Lit shader, `enableInstancing = true` on every material (required for
  GPU-instanced terrain scatter). Mask packed as R=Metallic/G=Occlusion/A=Smoothness, assigned to
  both `_MetallicGlossMap` and `_OcclusionMap` (URP samples the right channel from each slot
  automatically) - the established project convention, not something new here. Foliage materials
  (RF, TSA, GFF) use Alpha Clip (cutoff 0.5, Cull Off) - safe even for RF's fully-opaque bark
  regions, since alpha-clip only removes fully-transparent pixels.
- **Textures**: `_BaseColor` -> Point filter (the most commonly-missed setting per the pipeline
  doc - Bilinear silently undoes the whole pixelation pass), sRGB on, alpha-is-transparency.
  `_Normal` -> Normal Map type, Bilinear. `_Mask` -> linear (not sRGB, it's data not color),
  Bilinear.
- **Meshes** (RetroRealism FBX only - TSA reuses already-imported native meshes): Read/Write
  disabled (not needed for static props, saves runtime memory), no animation/camera/light import,
  medium mesh compression, polygon/vertex optimization on.
- **GameObjects**: `BatchingStatic` + `OccludeeStatic` (+ `ReflectionProbeStatic` on real meshes)
  static flags set on every prefab root, matching the project's static-batching approach for
  non-moving environment props.

## What this does NOT include (deliberately)

- **Nothing is placed in the Island scene or painted onto the actual Terrain.** These are
  ready-to-use assets sitting in the project, not yet wired into any GameObject hierarchy or
  Terrain's layer list - that's scene-view/Terrain-data work, a separate step per CLAUDE.md's
  Unity-touching rule.
- **LOD groups were not built.** Nothing here has an LODGroup component; the flagged oversized
  meshes (RF Tree2/Tree3, TSA's heavier Grass variants) are single-LOD prefabs, still carrying the
  polycount caveats from their `analysis.md`.
- **Tile sizes on Terrain Layers are an untuned default (5x5m).** No real terrain to preview
  against during this pass.
