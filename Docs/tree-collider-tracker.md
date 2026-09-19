# Tree collider tracker (MRM-84)

Which test-copy prefabs in `Assets/_Project/Art/VegetationPrefabs/AST116_ColliderTest/` have had
collider work, so nothing finished gets overwritten. **Check this before touching any prefab;
update it after every tree.** Process: `Docs/technie-vegetation-collider-process.md`.

Rules:
- **Done** prefabs are off-limits unless Carlos asks for a redo by name. Never pass them to
  `TreeColliderReset` (its `keep` list must contain every Done prefab).
- **Since 2026-09-18 the tool is `WoodColliderTool`** (one non-convex MeshCollider of the painted
  wood). A tree moves to Done after a real `Run` -> `VERIFY OK` -> `RayTest` -> `RAYTEST OK` ->
  `<prefab>_wood.png` looked at (no thick grey limbs) -> Carlos's OK.

## Wood colliders (WoodColliderTool) — 9 trees, re-run 2026-09-18 on Carlos's clean one-hull repaint, awaiting his live check

Carlos cleared all 9 and repainted each as ONE hull holding just the wood, then the tool was re-run.
All 9: 1 MeshCollider on `Visual/WoodCollider`, `VERIFY OK` from disk, `RAYTEST OK` (3000 rays each,
0 hits in air, 0 shots through wood, every hit on the exact bark point).

| Prefab | Wood tris | Collider tris | RayTest hits | Notes |
|---|---|---|---|---|
| `AP_Tree_Deadtree06_SM` | 5,932 | 11,864 | 1215/1215 | Two-sided. Now includes the thicker twigs (586 from the second material). |
| `AP_Tree_DeadTree01_SM` | 1,248 | 2,496 | 533/533 | Two-sided. |
| `AP_Tree_Lake_RoundTree_01_SM` | 6,945 | 6,945 | 703/703 | Culled material, one-sided. The mesh is almost all wood. |
| `AP_Tree_Curse_H01_2` | 22,858 | 45,716 | 1200/1200 | Two-sided. Thick lower roots now painted; moss slabs left out. **Heaviest collider so far**: check it in the perf pass. |
| `AP_M6_Tree_MonsterTreeBark_SM_PHJ_2` | 9,304 | 9,304 | 1129/1129 | Culled material, one-sided. The whole mesh is wood. |
| `AP_Tree_Juniper02_SMIK` | 2,299 | 4,598 | 584/584 | Two-sided. |
| `AP_ENV_tree_Nokmyung` | 77 | 154 | 671/671 | Two-sided. |
| `AP_ENV_tree_SaGeeSukRim` | 108 | 216 | 458/458 | Two-sided. |
| `AP_AlaskaCedar_001_2` | 392 | 784 | 1426/1426 | Two-sided. |

As of the morning of 2026-09-18 these 9 were the only prefabs in the folder with any collider.

## Wood colliders, bulk run 2026-09-18 (evening) — 86 more, 95 total, awaiting Carlos's live check

Carlos painted every tree and rock as one hull each. `PaintedPrefabs()` found 95 (the 9 above plus 86).
Dry run of all 95 (18 s), then the front-view image of each of the 86 checked on contact sheets
(grey = leaf cards, cloth ribbons on the GraveKeepers, moss slabs; no thick grey limb anywhere).
Real run on the 86: **86/86 `VERIFY OK`**. `RayTest(3000)` on the 86: **86/86 `RAYTEST OK`**,
0 hits in air, 0 shots through wood, worst error 0.000 m. The 9 above were not re-run (already current).
Gallery scene `VegetationGallery_TechnieColliderTest`: all 95 instances carry `Visual/WoodCollider`
with its mesh (read back live); scene needed no save.

Rocks, stumps, logs and fallen trees were run with the same wood tool (no Technie Auto), because
Carlos painted them: `AP_M6_Rock_*` x4, `AP_TurtleLake_Rock_*` x4, `AP_GraveKeepers_C01/C02`,
`AP_TurtleLake_Tree_Stump02_SM`, `AP_TurtleLake_Tree_BrokenTree01_SM`, `AP_FallTree_01_SM`,
`AP_FallTree_01_SM_1`, `AP_FallTree_02_SM`, `AP_Tree_Break_02/03_SM`, `AP_Tree_Break_MushroomTrunk_01_SM`,
`AP_Tree_Break_Root_02_SM`. Rocks are one-sided and solid (`_Cull` non-zero or single submesh).

Heaviest new colliders (collider tris): `AP_GraveKeepers_B02` (~24k), `AP_Tree_Curse_K01` (~18k),
`AP_GraveKeepers_B01/B04/B06` (~15–18k), `AP_Tree_Curse_J07` (~13k), `AP_Tree_10_ArgassTree_03_SM`
(~17k, two-sided). Fold into the perf pass (`Docs/performance-log.md`).

**Correction, same evening:** the `RF_*` prefabs (Retro Realism, in `AST116_ColliderTest/RetroRealism/`)
WERE painted; `WoodColliderTool` only scanned the top-level folder, so it never saw them (an earlier
version of this note wrongly called them unpainted). The tool now resolves prefabs in subfolders too
(`PrefabPath()`, and `PaintedPrefabs()` skips only `WoodColliders/`). Ran the 14: 14/14 `VERIFY OK`,
14/14 `RAYTEST OK` (3000 rays each, 0 in air, 0 through wood). Boulders, logs, stumps fully painted;
trees = trunk only, foliage cards left grey. Collider tris 52-404 for boulders/logs/stumps, 144-160
(x2, two-sided) for trees. Each `RF_*` `Visual` keeps its single-LOD `LODGroup` intact (1 LOD, 1 renderer).
Total now **109 prefabs** with wood colliders.

**No collider by design:** `RF_Bush1..3` (no Technie component, 4-22 tris), mushrooms
(`AP_Mushroom_*`), `AP_Nest_B01`, and all grass/flower/fern/weed prefabs.

**LOD facts (surveyed 2026-09-18):** only the 14 `RF_*` prefabs have a `LODGroup`, each with ONE LOD
(cull at 1.5% screen height). All 95 `AP_*` trees/rocks and the grass prefabs have none. The wood
collider is built from LOD 0 and lives outside the LOD Group, so it stays active while the renderer is culled.
`QualitySettings.lodBias` = 2.

## Done with convex hulls — ALL CONVERTED to wood colliders 2026-09-18 (kept for the record)

| Prefab | Tool | Colliders | Date | Notes |
|---|---|---|---|---|
| `AP_Tree_Juniper02_SMIK` | TreeColliderTool | 104 | 2026-09-17 | Carlos approved. Data: `Visual 344` |
| `AP_ENV_tree_Nokmyung` | old ring-seam splitter | 8 | 2026-09-17 | Not redone with the new tool. Data: `Visual 383` |
| `AP_ENV_tree_SaGeeSukRim` | old ring-seam splitter | 15 | 2026-09-17 | Not redone. Data: `Visual 273` |
| `AP_AlaskaCedar_001_2` | old ring-seam splitter | 18 | 2026-09-17 | Not redone. Data: `Visual 381` |

## Batch 1 with convex hulls — SUPERSEDED the same day by the wood colliders above (kept for the record)

Details and what was tried: `Docs/technie-vegetation-collider-process.md` "Batch 1".

| Prefab | Colliders | Notes |
|---|---|---|
| `AP_Tree_Deadtree06_SM` | **0** | Split undone while testing. **Carlos repainted it 2026-09-18 as one hull (`Hull 1`)** — untouched since. Base (flat roots fused into the trunk at ground) is the open problem. |
| `AP_Tree_DeadTree01_SM` | 69 | Outline-fit cutter, level rings. **Carlos flagged: upper-left fork crotch still filled** — fix next session. |
| `AP_Tree_Lake_RoundTree_01_SM` | 128 | Outline-fit cutter, trunk 5 -> 39 ring slices. One tight crown curl filled (out of reach). Awaiting Carlos's OK. |
| `AP_Tree_Curse_H01_2` | 442 | OLD cutter. Braided trunk strands wedge. Carlos: fine for what they are. Could be re-run with the new cutter. |
| `AP_M6_Tree_MonsterTreeBark_SM_PHJ_2` | 321 | OLD cutter. Looping-root wedge. Same verdict. |

**Carlos started painting batch 2 during this session** (many other prefabs in the folder show as
modified in git). Don't assume the "Reset — waiting for paint" list below is still paint-free;
check each prefab's Technie hull count before planning.

## Reset 2026-09-17 — clean, waiting for paint (STALE: everything below was painted and converted by 2026-09-18, see the wood-collider sections above)

Every prefab below had all Technie paint, Technie data assets and colliders removed
(`TreeColliderReset`, verified from disk: 0 colliders, 0 Technie components). Git has the previous
state (commit `4454569` on `mrm-84`) if anything ever needs to come back.

"Solid?" = name suggests a rock, boulder, stump or log; those probably want Technie's Auto mode
(one collider, `AST116TechnieBatchCollider.RunOnNames`) rather than painting. Unconfirmed — ask
Carlos.

**Trees / branchy (paint with TreeColliderTool):**
`AP_BC_PineTree_02`, `AP_BC_PineTree_03`, `AP_Building_exorcist_tree2`, `AP_DeadTree02`,
`AP_DeadTree03`, `AP_DeadTree04`, `AP_ENV_tree_ToeMunJean`, `AP_GangshiTree_2`,
`AP_GraveKeepers_B01`, `AP_GraveKeepers_B02`, `AP_GraveKeepers_B03_2`, `AP_GraveKeepers_B04`,
`AP_GraveKeepers_B06`, `AP_GraveKeepers_B07`, `AP_M6_Tree_Bushtree01_SM`, `AP_M6_Tree_Bushtree02_SM`,
`AP_M6_Tree_ForestTree08_SM_JYI_2`, `AP_M6_Tree_MonsterTreeBark_SM_PHJ_2`, `AP_Norway_Spruce_01`,
`AP_Plant_003_07`, `AP_S_Tree_01`, `AP_sunghwangdang_Tree_pagoda_01`,
`AP_sunghwangdang_Tree_pagoda_01_1`, `AP_Tree_04_GTree01_01_SM_2`, `AP_Tree_04_GTree01_02_SM`,
`AP_Tree_04_GTree01_03_SM`, `AP_Tree_04_GTree01_04_SM`, `AP_Tree_04_GTree01_05_SM`,
`AP_Tree_04_GTree01_06_SM`, `AP_Tree_04_M01_01_SM_2`, `AP_Tree_04_M01_02_SM`, `AP_Tree_04_M01_03_SM`,
`AP_Tree_04_M01_04_SM`, `AP_Tree_04_M01_05_SM`, `AP_Tree_04_PTree_01_SM_2`, `AP_Tree_04_PTree_02_SM`,
`AP_Tree_04_PTree_03_SM`, `AP_Tree_04_PTree_04_SM`, `AP_Tree_04_PTree_05_SM`,
`AP_Tree_10_ArgassTree_02_SM`, `AP_Tree_10_ArgassTree_03_SM`, `AP_Tree_10_ArgassTree_04_SM`,
`AP_Tree_10_ArgassTree_SM`, `AP_Tree_AUT_White_A_02_SM`, `AP_Tree_AUT_White_A_03_SM`,
`AP_Tree_Blackpoplar01_SM`, `AP_Tree_Burnt_04_SM`, `AP_Tree_color_001_01_2`, `AP_Tree_color_001_03`,
`AP_Tree_Conifir_A_01_SM_2`, `AP_Tree_Conifir_A_02_SM`, `AP_Tree_Curse_H01_2`, `AP_Tree_Curse_J07`,
`AP_Tree_Curse_J08`, `AP_Tree_Curse_K01`, `AP_Tree_DeadTree01_SM`, `AP_Tree_Deadtree06_SM`,
`AP_Tree_Dry_D01`, `AP_Tree_Dry_N02` (had no colliders even before), `AP_Tree_Heretic_A01_2`,
`AP_Tree_Heretic_B03`, `AP_Tree_Heretic_B05`, `AP_Tree_Heretic_D02_01`, `AP_Tree_Heretic_D03`,
`AP_Tree_Heretic_D03_02`, `AP_Tree_Juniper03_SMIK`, `AP_Tree_Lake_RoundTree_01_SM`,
`AP_Tree_Oak01_SM`, `AP_Tree_WNT_03_Bark_01_SM_2`, `AP_Tree_WNT_M01_01_SM_3`, `AP_Tree_WNT_M_03_SM`,
`AP_WhiteFir_MD_Dead_03`, `RF_Tree1`, `RF_Tree2`, `RF_Tree3`, `RF_Tree4`

**Solid? (probably Auto, confirm):**
`AP_M6_Rock_CemeteryRock02_SM`, `AP_M6_Rock_FieldStoneStone05_SM`, `AP_M6_Rock_FieldStoneStone06_SM`,
`AP_M6_Rock_SeashoreWallStone01_SM`, `AP_TurtleLake_Rock_GoblinRock01_SM`,
`AP_TurtleLake_Rock_GoblinRock02_SM`, `AP_TurtleLake_Rock_LakeRock02_SM`,
`AP_TurtleLake_Rock_TurtleRock04_SM`, `RF_Boulder1`..`RF_Boulder5`, `RF_Log1`..`RF_Log3`,
`RF_Stump1`, `RF_Stump2`, `AP_TurtleLake_Tree_Stump02_SM`, `AP_TurtleLake_Tree_BrokenTree01_SM`,
`AP_GraveKeepers_C01`, `AP_GraveKeepers_C02`, `AP_FallTree_01_SM`, `AP_FallTree_01_SM_1`,
`AP_FallTree_02_SM`, `AP_Tree_Break_02_SM`, `AP_Tree_Break_03_SM`,
`AP_Tree_Break_MushroomTrunk_01_SM`, `AP_Tree_Break_Root_02_SM`

**Never had colliders, left alone:** the 47 prefabs in `GRASS PREFABS/` and `RF_Bush1`..`RF_Bush3`.
