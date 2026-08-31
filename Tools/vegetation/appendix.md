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
