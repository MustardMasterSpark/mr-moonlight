# Tree collider tracker (MRM-84)

Which test-copy prefabs in `Assets/_Project/Art/VegetationPrefabs/AST116_ColliderTest/` have had
collider work, so nothing finished gets overwritten. **Check this before touching any prefab;
update it after every tree.** Process: `Docs/technie-vegetation-collider-process.md`.

Rules:
- **Done** prefabs are off-limits unless Carlos asks for a redo by name. Never pass them to
  `TreeColliderReset` (its `keep` list must contain every Done prefab).
- A tree moves to Done only after `Apply` -> `VERIFY OK` -> `Inspect` image looked at -> Carlos's OK.

## Done

| Prefab | Tool | Colliders | Date | Notes |
|---|---|---|---|---|
| `AP_Tree_Juniper02_SMIK` | TreeColliderTool | 104 | 2026-09-17 | Carlos approved. Data: `Visual 344` |
| `AP_ENV_tree_Nokmyung` | old ring-seam splitter | 8 | 2026-09-17 | Not redone with the new tool. Data: `Visual 383` |
| `AP_ENV_tree_SaGeeSukRim` | old ring-seam splitter | 15 | 2026-09-17 | Not redone. Data: `Visual 273` |
| `AP_AlaskaCedar_001_2` | old ring-seam splitter | 18 | 2026-09-17 | Not redone. Data: `Visual 381` |

## In progress

(none)

## Reset 2026-09-17 — clean, waiting for paint

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
