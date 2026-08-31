# -*- coding: utf-8 -*-
exec(open('gen.py', encoding='utf-8').read())

B = {}

B['Forest'] = ("1. Forest", "the main lost biome", "5 / 5", 16, [
 ("A. Canopy", 55, [
  ("AP_Tree_04_GTree01_01_SM_2", 16, 'M', "Primary old-growth fir, cheap for its size"),
  ("AP_Tree_04_GTree01_02_SM", 14, 'M', "Fir variation"),
  ("AP_Tree_04_GTree01_03_SM", 11, 'M', "Narrow fir"),
  ("AP_Tree_Conifir_A_01_SM_2", 12, 'M', "Dense evergreen mass"),
  ("AP_Tree_Conifir_A_02_SM", 10, 'M', "Conifer variation"),
  ("AP_Tree_04_M01_01_SM_2", 10, 'M', "Secondary spruce family"),
  ("AP_Tree_04_M01_02_SM", 8, 'S', "Tall spruce"),
  ("AP_Tree_04_M01_03_SM", 5, 'S', "EMERGENT — 41.9 m, tallest in the set"),
  ("AP_Tree_04_GTree01_04_SM", 5, 'S', "EMERGENT — leaning 39.6 m fir"),
  ("AP_Tree_04_GTree01_05_SM", 4, 'S', "EMERGENT — 38.5 m narrow crown"),
  ("AP_Tree_04_GTree01_06_SM", 5, 'S', "Asymmetric 20 m crown, gap edge"),
 ]),
 ("B. Sub-canopy", 95, [
  ("RF_Tree4", 15, 'D', "Cheapest tall backbone — 976 tris for 24 m"),
  ("RF_Tree3", 14, 'D', "Thin pine filler"),
  ("RF_Tree2", 12, 'D', "Thin pine filler"),
  ("RF_Tree1", 12, 'D', "Cheapest tree in the project — 314 tris"),
  ("AP_Norway_Spruce_01", 10, 'D', "Narrow dark spire, 4.0 m footprint"),
  ("AP_Tree_04_PTree_01_SM_2", 8, 'M', "Sparse lower-slope pine"),
  ("AP_Tree_04_PTree_03_SM", 7, 'M', "Pine variation"),
  ("AP_Tree_04_M01_04_SM", 6, 'M', "Sparse spruce"),
  ("AP_Tree_04_M01_05_SM", 5, 'M', "Edge-of-gap spruce"),
  ("AP_BC_PineTree_02", 5, 'M', "Broad pine"),
  ("AP_BC_PineTree_03", 4, 'M', "Asymmetric pine"),
  ("AP_M6_Tree_ForestTree08_SM_JYI_2", 2, 'S', "Broadleaf break — cheap at 25 m"),
 ]),
 ("C. Shrub / small tree", 250, [
  ("RF_Bush2", 18, 'D', "Low leafy cluster, no collider"),
  ("RF_Bush3", 14, 'D', "Tall close-camera brush, no collider"),
  ("AP_Plant_001_08", 14, 'D', "Dense leafy shrub"),
  ("AP_plant_001_28", 12, 'D', "Brighter green shrub"),
  ("RF_Bush1", 10, 'D', "Small upright, 0.6 m"),
  ("RF_Sapling1", 10, 'M', "Young growth — GPT never used this"),
  ("RF_Sapling2", 8, 'M', "Taller sapling — GPT never used this"),
  ("AP_M6_Tree_Bushtree01_SM", 8, 'M', "Multi-stem cover bush, 9.7 m"),
  ("AP_M6_Tree_Bushtree02_SM", 6, 'M', "Lighter multi-stem, 12.6 m"),
 ]),
 ("D. Ground cover (no colliders — go heavy)", 900, [
  ("Fern_01A", 18, 'D', "Dominant fern bed"),
  ("Fern_01B", 16, 'D', "Fern variation"),
  ("Fern_02A", 13, 'D', "Yellow-green fern"),
  ("RF_Fern1", 12, 'D', "Tiny fern, 9 tris — GPT never used this"),
  ("Fern_02B", 11, 'D', "Fern variation"),
  ("Fern_03A", 10, 'D', "Long-frond fern, 2.3 m"),
  ("RF_Fern2", 10, 'D', "Small fern, 33 tris — GPT never used this"),
  ("AP_Mushroom_B01", 5, 'M', "Fungal patch — 4.6 m mesh, not one mushroom"),
  ("AP_Mushroom_C01", 5, 'M', "Second fungal patch — 4.3 m"),
 ]),
 ("E. Debris & rock", 45, [
  ("RF_Log1", 16, 'M', "Trail frame — WARNING 5.2 m block, 0.9 m visible"),
  ("RF_Stump1", 15, 'D', "Stump near clearings"),
  ("RF_Log2", 13, 'M', "Deadfall — WARNING 5.0 m block, 0.7 m visible"),
  ("RF_Boulder1", 12, 'D', "Small mossy rock, 1.0 m"),
  ("RF_Boulder3", 11, 'M', "Low broad boulder, crouch cover"),
  ("RF_Boulder4", 10, 'M', "Largest wet moss boulder, crouch cover"),
  ("AP_M6_Rock_FieldStoneStone05_SM", 9, 'D', "Ravine pebble, 1.3 m"),
  ("AP_M6_Rock_FieldStoneStone06_SM", 8, 'D', "Streamside rock, 1.6 m"),
  ("AP_Tree_Break_MushroomTrunk_01_SM", 6, 'S', "Fungal old-growth stump"),
 ]),
])

B['AutumnForest'] = ("2. Autumn Forest", "the beautiful lie", "4.5 / 5", 18, [
 ("A. Warm canopy", 50, [
  ("AP_FallTree_01_SM", 18, 'M', "Primary warm vertical, 25.1 m"),
  ("AP_FallTree_01_SM_1", 16, 'M', "Secondary yellow-orange, 27.6 m"),
  ("AP_Tree_AUT_White_A_02_SM", 13, 'M', "Pale-bark cluster accent"),
  ("AP_Tree_color_001_01_2", 12, 'M', "Dense amber crown"),
  ("AP_Tree_AUT_White_A_03_SM", 11, 'M', "Pale-bark transition"),
  ("AP_FallTree_02_SM", 10, 'M', "Late-autumn transition tree"),
  ("AP_Tree_color_001_03", 10, 'M', "Olive/russet variation"),
  ("AP_Tree_Blackpoplar01_SM", 6, 'S', "Wet lowland broadleaf"),
  ("AP_Tree_Oak01_SM", 4, 'S', "Rare old broadleaf landmark"),
 ]),
 ("B. Dark conifer pierce — 1-2 per 5-12 tree grove", 30, [
  ("AP_Norway_Spruce_01", 24, 'M', "Tall dark spire, navigation rhythm"),
  ("AP_BC_PineTree_02", 20, 'M', "Broad evergreen mass"),
  ("AP_Tree_Conifir_A_01_SM_2", 16, 'S', "Dense evergreen background"),
  ("AP_Tree_04_GTree01_01_SM_2", 14, 'M', "Old heavy fir silhouette"),
  ("RF_Tree3", 14, 'M', "Thin pine variation, depth filler"),
  ("AP_BC_PineTree_03", 12, 'M', "Asymmetric pine"),
 ]),
 ("C. Small autumn crowns", 110, [
  ("AP_ENV_tree_SaGeeSukRim", 34, 'D', "Small orange crown — 288 tris, use freely"),
  ("AP_ENV_tree_ToeMunJean", 30, 'D', "Low broad autumn mass — 309 tris"),
  ("AP_ENV_tree_Nokmyung", 14, 'M', "Soft round green canopy, 3.5 m"),
  ("RF_Sapling2", 12, 'M', "Young pale trunk"),
  ("RF_Sapling1", 10, 'M', "Small sapling"),
 ]),
 ("D. Shrub", 240, [
  ("AP_plant_001_13", 24, 'M', "Rust-orange shrub — 4.1 m patch mesh"),
  ("AP_Plant_001_08", 20, 'D', "Dark green shrub counterpoint"),
  ("RF_Bush2", 18, 'D', "Low leafy blocker"),
  ("AP_plant_001_14", 14, 'D', "Darker low shrub"),
  ("RF_Bush3", 12, 'D', "Tall close-camera brush"),
  ("AP_Neutral_Foliage_A_Weed01_SM", 12, 'M', "Path-edge clump — 3.3 m wide, 1.5 m tall"),
 ]),
 ("E. Ground cover", 700, [
  ("Fern_01A", 26, 'D', "Dominant green fern bed"),
  ("Fern_01B", 22, 'D', "Fern variation"),
  ("RF_Fern2", 16, 'D', "Small fern"),
  ("AP_Mushroom_A01_2", 14, 'D', "Small saturated Halloween accent"),
  ("AP_Flower_001_09", 12, 'M', "Rare red floral spot"),
  ("AP_Neutral_Foliage_A_Weed02_SM", 10, 'D', "Dry grass tuft"),
 ]),
 ("F. Debris & rock", 35, [
  ("RF_Log1", 20, 'M', "Mossy trail frame"),
  ("RF_Stump1", 20, 'M', "Cut/broken rhythm near clearings"),
  ("RF_Boulder2", 16, 'M', "Small mossy rock cluster"),
  ("AP_TurtleLake_Rock_LakeRock02_SM", 16, 'M', "Damp rock at drainage lines"),
  ("AP_Tree_Break_MushroomTrunk_01_SM", 14, 'S', "Fungal hero stump"),
  ("RF_Log2", 14, 'S', "Secondary deadfall"),
 ]),
])

B['Beach'] = ("3. Beach", "cold exposure zone", "1 / 5 on sand, 5 / 5 at treeline", 5, [
 ("A. Shore rock", 40, [
  ("AP_M6_Rock_SeashoreWallStone01_SM", 20, 'M', "Primary outcrop — the ONLY 4 m standing rock we own"),
  ("AP_TurtleLake_Rock_LakeRock02_SM", 16, 'M', "Low tide-pool edge rock"),
  ("AP_M6_Rock_FieldStoneStone06_SM", 14, 'M', "Rounded wet boulder, 1.6 m"),
  ("AP_M6_Rock_FieldStoneStone05_SM", 13, 'M', "Small fieldstone, 1.3 m"),
  ("RF_Boulder2", 11, 'M', "Low scattered stone"),
  ("RF_Boulder1", 9, 'M', "Mossy inland-edge pebble"),
  ("RF_Boulder5", 8, 'S', "Wet rock colour variation, 4.6 m"),
  ("AP_TurtleLake_Rock_GoblinRock02_SM", 5, 'S', "Occasional landmark boulder (42% buried)"),
  ("AP_TurtleLake_Rock_TurtleRock04_SM", 4, 'H', "11 m slab, only 1.15 m proud — hand-place as a rocky point"),
 ]),
 ("B. Driftwood — storm-deposit clusters, never a chain", 22, [
  ("RF_Log1", 20, 'S', "Long bleached horizontal"),
  ("RF_Log2", 18, 'S', "Broken tapered log"),
  ("AP_Tree_Break_02_SM", 14, 'S', "Dark waterlogged trunk, 6.3 m"),
  ("AP_Tree_Break_03_SM", 12, 'S', "Secondary trunk, 9.3 m"),
  ("AP_TurtleLake_Tree_BrokenTree01_SM", 12, 'S', "Shore log — WORST trip-wall in the set"),
  ("AP_TurtleLake_Tree_Stump02_SM", 10, 'S', "Root cluster near wrack line"),
  ("RF_Stump2", 8, 'S', "Root-like vertical accent"),
  ("RF_Log3", 6, 'H', "10 m x 1.9 m proud — hand-place, unvaultable"),
 ]),
 ("C. Treeline transition only — never a beach grove", 12, [
  ("AP_AlaskaCedar_001_2", 30, 'S', "Wind-beaten survivor, 5.2 m footprint"),
  ("AP_Tree_04_PTree_05_SM", 24, 'S', "Tall thin transition pine, 4.2 m"),
  ("RF_Tree2", 20, 'A', "Sparse shore pine"),
  ("RF_Tree1", 16, 'A', "Nearly bare shore pine"),
  ("AP_Tree_04_PTree_04_SM", 10, 'A', "Wind-shaped pine"),
 ]),
 ("D. Salt grass above the high-tide line", 120, [
  ("AP_Neutral_Foliage_A_Weed02_SM", 34, 'M', "Sparse grass"),
  ("AP_Neutral_Tree_A_PrickelGrass01_SM", 28, 'M', "Pale dune/wrack grass"),
  ("AP_Neutral_Foliage_A_Weed01_SM", 22, 'S', "Taller salt-beaten clump"),
  ("RF_Bush1", 16, 'S', "Treeline-edge shrub only"),
 ]),
])

B['EerieForest'] = ("4. Eerie Forest", "ecological corruption", "4.5 / 5 trunk density", 5, [
 ("A. Dead backbone — CHEAP meshes carry the density", 130, [
  ("AP_DeadTree02", 18, 'D', "Primary leafless tree — 725 tris, the workhorse"),
  ("AP_Tree_DeadTree01_SM", 14, 'D', "Dead tree, 1,962 tris"),
  ("AP_DeadTree03", 12, 'D', "Y-shaped negative space"),
  ("AP_Tree_WNT_03_Bark_01_SM_2", 12, 'D', "Thin winter snag, 6.6 m footprint"),
  ("AP_DeadTree04", 11, 'D', "Leaning claw silhouette"),
  ("AP_Tree_WNT_M01_01_SM_3", 10, 'M', "Bare winter crown"),
  ("AP_Tree_WNT_M_03_SM", 10, 'M', "Dense black branch lattice"),
  ("AP_Tree_Burnt_04_SM", 8, 'M', "Charred 27.6 m vertical punctuation"),
  ("AP_WhiteFir_MD_Dead_03", 5, 'M', "Damaged fir, 1,315 tris"),
 ]),
 ("B. Expensive claw silhouettes — ACCENT ONLY", 14, [
  ("AP_GraveKeepers_B04", 18, 'S', "Narrow snag — 17,378 tris for 6 m, worst ratio in the set"),
  ("AP_GraveKeepers_B02", 15, 'S', "Upright tangled dead tree — 27,036 tris"),
  ("AP_GraveKeepers_B06", 14, 'S', "Forked corridor blocker — 17,899 tris"),
  ("AP_GraveKeepers_B01", 13, 'S', "Hanging lateral branch — 20,824 tris"),
  ("AP_GraveKeepers_B07", 12, 'S', "Dense claw crown"),
  ("AP_GraveKeepers_B03_2", 10, 'S', "Broad crooked mass, 25 m wide"),
  ("AP_Tree_Deadtree06_SM", 10, 'S', "Large twisted dead tree"),
  ("AP_Tree_Dry_D01", 8, 'S', "Root-heavy decayed trunk"),
 ]),
 ("C. Hero", 0.3, [
  ("AP_S_Tree_01", 100, 'H', "30 m x 33.8 m rooted giant — 1 per district, NEVER scattered"),
 ]),
 ("D. Rare living survivors — proof of corruption", 6, [
  ("AP_AlaskaCedar_001_2", 34, 'A', "Rare surviving pine"),
  ("RF_Tree2", 26, 'A', "Sickly green survivor"),
  ("AP_BC_PineTree_03", 22, 'A', "Distant normal-tree reminder"),
  ("AP_Norway_Spruce_01", 18, 'A', "Single dark vertical landmark"),
 ]),
 ("E. Dead ground layer", 420, [
  ("AP_Neutral_Foliage_A_Weed01_SM", 32, 'M', "Dark dead-grass floor — 3.3 m wide"),
  ("AP_Neutral_Tree_A_PrickelGrass01_SM", 28, 'M', "Pale ghostlike scrub"),
  ("AP_Tree_Dry_N02", 20, 'M', "Branch litter — 5.3 m flat decal, no collider"),
  ("AP_Neutral_Foliage_A_Weed02_SM", 18, 'D', "Short dead tuft"),
  ("AP_Nest_B01", 2, 'S', "Stick nests — 7.7 m wide, NOT a small prop"),
 ]),
 ("F. Debris & rock", 40, [
  ("RF_Stump2", 26, 'M', "Narrow broken stump, 3.4 m proud"),
  ("RF_Boulder4", 22, 'M', "Moss-dark cover rock"),
  ("AP_Tree_Break_Root_02_SM", 20, 'M', "Root trip hazard"),
  ("RF_Log2", 18, 'S', "Fallen corridor obstruction"),
  ("RF_Boulder3", 14, 'M', "Low broad rock"),
 ]),
])

B['HereticForest'] = ("5. Heretic Forest", "authored ritual landscape, the final section", "4 / 5", 4, [
 ("A. Dead base — same cheap backbone as Eerie, thinned to open ritual clearings", 90, [
  ("AP_DeadTree02", 20, 'D', "Cheap leafless base"),
  ("AP_Tree_WNT_03_Bark_01_SM_2", 16, 'D', "Thin background bars"),
  ("AP_Tree_DeadTree01_SM", 14, 'D', "Dead-tree base"),
  ("AP_DeadTree03", 12, 'M', "Leaning branch frame"),
  ("AP_DeadTree04", 11, 'M', "Second leaning variation"),
  ("AP_Tree_WNT_M_03_SM", 10, 'M', "Black branch lattice"),
  ("AP_Tree_Burnt_04_SM", 9, 'M', "Charred marker"),
  ("AP_Tree_Deadtree06_SM", 8, 'S', "Large twisted base tree"),
 ]),
 ("B. Curse trees — ritual mid-layer, expensive, keep sparse", 12, [
  ("AP_Tree_Curse_J07", 26, 'S', "Ritual-adjacent crooked tree — 13,461 tris"),
  ("AP_Tree_Curse_J08", 22, 'S', "Taller curse-tree — 10,366 tris"),
  ("AP_Tree_Curse_K01", 20, 'S', "Thick clawed trunk — 18,270 tris"),
  ("AP_Tree_Dry_D01", 18, 'S', "Root-heavy decayed tree"),
  ("AP_Tree_Curse_H01_2", 14, 'S', "Hanging curse tree — 27,975 tris, heaviest mesh we own"),
 ]),
 ("C. Ritual heroes — CAP AT ONE PER AUTHORED NODE", 0.8, [
  ("AP_Building_exorcist_tree2", 22, 'H', "Primary ritual landmark, 18 m — and only 1,141 tris"),
  ("AP_GangshiTree_2", 18, 'H', "Altar tree — 29 m x 34.8 m, biggest thing in the set"),
  ("AP_M6_Tree_MonsterTreeBark_SM_PHJ_2", 16, 'H', "Monstrous root — see collider audit, 6.7 m block"),
  ("AP_Tree_Heretic_A01_2", 16, 'H', "Sculptural heretic tree — only 8 m, smaller than GPT assumed"),
  ("AP_sunghwangdang_Tree_pagoda_01", 14, 'H', "Bound stone totem"),
  ("AP_sunghwangdang_Tree_pagoda_01_1", 14, 'H', "Alternate cairn/totem"),
 ]),
 ("D. Ritual ground formations — large flat meshes, NOT small props", 18, [
  ("AP_Tree_Heretic_D03", 22, 'S', "Root altar frame — 15.0 m across, 6.3 m block"),
  ("AP_Tree_Heretic_D03_02", 20, 'S', "Second root formation, 12.2 m"),
  ("AP_Tree_Heretic_D02_01", 20, 'S', "Crawling root formation, 12.2 m"),
  ("AP_Tree_Break_Root_02_SM", 20, 'M', "Root ring / barrier"),
  ("AP_Nest_B01", 18, 'S', "Twig altar filler — 7.7 m wide"),
 ]),
 ("E. Stakes & bound stones — small props, use them DENSE around nodes", 70, [
  ("AP_Tree_Heretic_B05", 26, 'D', "Single stake, 1.7 m — GPT over-spaced this 12x"),
  ("AP_Tree_Heretic_B03", 22, 'D', "Stake cluster, 3.9 m"),
  ("AP_GraveKeepers_C01", 20, 'M', "Bound ritual boulder, 2.8 m"),
  ("AP_GraveKeepers_C02", 18, 'M', "Narrow bound stone, 4.0 m proud"),
  ("RF_Stump2", 14, 'M', "Stake-like broken trunk"),
 ]),
 ("F. Expensive claw accents", 8, [
  ("AP_GraveKeepers_B01", 22, 'S', "Hanging branch frame"),
  ("AP_GraveKeepers_B02", 20, 'S', "Tangled upright"),
  ("AP_GraveKeepers_B06", 18, 'S', "Forked funnel tree"),
  ("AP_GraveKeepers_B07", 16, 'S', "Dense claw crown"),
  ("AP_GraveKeepers_B03_2", 14, 'S', "Crooked branch mass"),
  ("AP_GraveKeepers_B04", 10, 'S', "Narrow snag"),
 ]),
 ("G. Ground & survivors", 300, [
  ("AP_Neutral_Foliage_A_Weed01_SM", 28, 'M', "Dead grass floor"),
  ("AP_Neutral_Tree_A_PrickelGrass01_SM", 24, 'M', "Pale scrub"),
  ("AP_Tree_Dry_N02", 18, 'M', "Branch litter decal"),
  ("RF_Boulder4", 12, 'M', "Dark cover stone"),
  ("RF_Log2", 8, 'S', "Low ritual boundary"),
  ("AP_AlaskaCedar_001_2", 6, 'S', "Rare surviving pine"),
  ("RF_Tree2", 4, 'S', "Sickly survivor"),
 ]),
])

B['FlakTower'] = ("6. Flak Tower", "open enemy-spawn arena", "3 / 5, continuous ground layer", 3.5, [
 ("A. Perimeter & scattered trees — keep the tower on the skyline", 22, [
  ("AP_FallTree_01_SM", 18, 'S', "Primary autumn tree"),
  ("AP_FallTree_01_SM_1", 14, 'S', "Secondary yellow tree"),
  ("AP_ENV_tree_SaGeeSukRim", 14, 'M', "Small autumn crown, 288 tris"),
  ("AP_ENV_tree_ToeMunJean", 12, 'M', "Broad low autumn cover"),
  ("AP_Norway_Spruce_01", 12, 'S', "Thin sentinel spire"),
  ("AP_BC_PineTree_02", 9, 'S', "Broad pine shadow"),
  ("RF_Tree3", 8, 'S', "Sparse pine variation"),
  ("AP_AlaskaCedar_001_2", 7, 'S', "Wind-beaten pine"),
  ("AP_Tree_04_PTree_05_SM", 6, 'S', "Very sparse vertical"),
 ]),
 ("B. Autumn shrub band", 160, [
  ("AP_plant_001_13", 26, 'M', "Autumn-red shrub — 4.1 m patch"),
  ("AP_plant_001_14", 22, 'D', "Darker low shrub"),
  ("AP_Plant_003_10", 18, 'D', "Low fern-like form"),
  ("AP_plant_001_18", 16, 'M', "Coniferous ground spray, 3.9 m"),
  ("AP_Plant_003_07", 10, 'S', "A 10.1 m TREE — GPT mislabelled it a low form"),
  ("AP_Plant_001_08", 8, 'D', "Low green shrub"),
 ]),
 ("C. Flower carpet — no colliders, go heavy", 1500, [
  ("Orange Aster_LOD", 13, 'D', "Primary orange accent, 0.52 m"),
  ("YellowDaisy_LOD", 12, 'D', "Low yellow drift, 0.41 m"),
  ("YellowAfricanDaisy_LOD", 11, 'D', "Ochre-yellow field accent"),
  ("Tangerine Violet_LOD", 10, 'D', "Acid-orange transition"),
  ("CaliforniaPoppy_01_LOD", 10, 'D', "Warm orange-red accent"),
  ("AP_flower_001_10", 9, 'D', "Green-yellow daisy clump"),
  ("AP_flower_001_11", 9, 'D', "Rust-red daisy clump"),
  ("AP_Neutral_Foliage_A_WildFlower01_SM_JYI", 8, 'D', "Flowered scrub"),
  ("AP_GR_003GR_002_D", 7, 'M', "Mixed wildflower patch — 3.1 m, not one flower"),
  ("Purple Aster_LOD", 6, 'D', "Purple accent"),
  ("CoralBells_green", 5, 'D', "Low green/pink clump"),
 ]),
 ("D. Grass matrix — prefab layer on top of the terrain detail carpet", 500, [
  ("AP_Neutral_Foliage_A_Weed02_SM", 40, 'D', "Primary 1.5 m grass tuft"),
  ("AP_Neutral_Foliage_A_Weed01_SM", 32, 'M', "Taller 3.3 m clump — GPT's 0.6 m would wall the arena"),
  ("AP_Neutral_Tree_A_PrickelGrass01_SM", 28, 'D', "Pale exposed grass"),
 ]),
 ("E. Cover rock & debris — the arena's only cover", 30, [
  ("RF_Boulder2", 22, 'M', "Scattered field rock"),
  ("RF_Boulder1", 18, 'M', "Low mossy cover"),
  ("AP_TurtleLake_Rock_LakeRock02_SM", 18, 'M', "Low drainage rock"),
  ("AP_M6_Rock_FieldStoneStone06_SM", 16, 'M', "Larger cover boulder, 1.6 m"),
  ("RF_Boulder4", 12, 'S', "Best crouch cover we own, 1.7 m proud"),
  ("RF_Log1", 8, 'S', "Rare fallen horizontal"),
  ("RF_Stump1", 6, 'S', "Rare stump"),
 ]),
])

B['Fountain'] = ("7. Fountain", "luminous sanctuary", "4 / 5 at perimeter, open centre", 1, [
 ("A. Sheltering arch trees — just OUTSIDE the 10-16 m readable ring", 45, [
  ("AP_Tree_10_ArgassTree_02_SM", 18, 'M', "First sculptural arch tree"),
  ("AP_Tree_10_ArgassTree_03_SM", 16, 'M', "Second arch variation"),
  ("AP_Tree_10_ArgassTree_04_SM", 14, 'M', "Broad mystical cover tree"),
  ("AP_Tree_10_ArgassTree_SM", 12, 'M', "Large central-frame tree"),
  ("AP_Tree_Juniper02_SMIK", 12, 'M', "Pale twisted canopy"),
  ("AP_Tree_Juniper03_SMIK", 10, 'M', "Juniper variation"),
  ("AP_Tree_Lake_RoundTree_01_SM", 10, 'S', "Curling witchlike silhouette"),
  ("AP_ENV_tree_Nokmyung", 8, 'M', "Soft round green canopy, 3.5 m"),
 ]),
 ("B. Dark outer pine ring — seals the sanctuary", 55, [
  ("AP_Tree_Conifir_A_01_SM_2", 26, 'M', "Dark outer ring"),
  ("AP_Tree_04_GTree01_01_SM_2", 22, 'M', "Heavy pine frame"),
  ("AP_Norway_Spruce_01", 20, 'D', "Tall perimeter spire, 4.0 m footprint"),
  ("RF_Tree3", 18, 'D', "Thin perimeter filler"),
  ("RF_Tree4", 14, 'M', "Tall cheap backbone"),
 ]),
 ("C. Blue flower basin — the signature layer, no colliders", 2200, [
  ("CupcakeWhite_01_LOD", 15, 'D', "Primary white luminous drift, 1.0 m"),
  ("VioletDaisy_LOD", 13, 'D', "Pale daisy, 0.41 m — can go very dense"),
  ("Blue Aster_LOD", 12, 'D', "Tall blue accent, 0.52 m"),
  ("Indigo Violet_LOD", 11, 'D', "Deep cool accent"),
  ("Purple Violet_LOD", 10, 'D', "Purple low drift"),
  ("BlueEyeGrass_01_LOD", 9, 'D', "Blue-purple grass flower"),
  ("African_violet_blue_LOD", 8, 'M', "Dense blue patch — 2.2 m, not one flower"),
  ("African_violet_LOD", 7, 'M', "Purple-blue variation, 2.1 m"),
  ("AP_Samakyo_flower_003", 6, 'M', "Thin mystical blue flower, 2.3 m"),
  ("AP_flower_001_12", 5, 'D', "Blue hydrangea clump"),
  ("AP_Flower_001_08", 4, 'D', "Second blue floral clump"),
 ]),
 ("D. Grass & fern matrix", 700, [
  ("AP_Neutral_Foliage_A_Weed02_SM", 30, 'D', "Soft grass matrix"),
  ("AP_flower_001A", 16, 'M', "Blue carpet — 4.9 m patch mesh, space it wide"),
  ("Fern_01A", 16, 'D', "Fern at shaded roots"),
  ("RF_Fern2", 14, 'D', "Small fern"),
  ("Fern_01B", 12, 'D', "Fern variation"),
  ("AP_Neutral_Foliage_A_Weed01_SM", 12, 'M', "Taller grass at the rim"),
 ]),
 ("E. Damp stone", 25, [
  ("AP_TurtleLake_Rock_LakeRock02_SM", 34, 'M', "Low damp stone near the rill"),
  ("RF_Boulder2", 24, 'M', "Mossy cover boulder"),
  ("RF_Boulder3", 22, 'M', "Broad low cover"),
  ("AP_Tree_Break_Root_02_SM", 20, 'M', "Exposed root shape"),
 ]),
])

B['Glade'] = ("8. Glade", "treeless wind-beaten hill", "3.5 / 5, all below knee height", 0.5, [
 ("A. Grass matrix — the biome's whole structure", 1400, [
  ("AP_Neutral_Foliage_A_Weed02_SM", 34, 'D', "Primary short-grass matrix, 1.5 m"),
  ("AP_Neutral_Tree_A_PrickelGrass01_SM", 26, 'D', "Pale exposed-hill grass, 1.95 m"),
  ("AP_Neutral_Foliage_A_Weed01_SM", 22, 'M', "Taller clump — 3.3 m wide, 1.5 m tall, near eye level"),
  ("TSA_GrassDry_C", 10, 'D', "Dry tuft — project asset GPT never used"),
  ("TSA_Heather_A", 8, 'D', "Heather clump — project asset GPT never used"),
 ]),
 ("B. Flower drifts — cluster by colour, do not confetti", 1800, [
  ("VioletDaisy_LOD", 14, 'D', "Pale violet drift, 0.41 m"),
  ("YellowDaisy_LOD", 13, 'D', "Muted yellow drift"),
  ("CupcakeWhite_01_LOD", 12, 'D', "White windblown patch"),
  ("BlueEyeGrass_01_LOD", 12, 'D', "Cool purple-blue accent"),
  ("Purple Aster_LOD", 11, 'D', "Purple accent"),
  ("Blue Aster_LOD", 10, 'D', "Blue accent"),
  ("CoralBells_green", 9, 'D', "Low green/pink clump"),
  ("AP_flower_001A", 7, 'M', "Blue carpet — 4.9 m patch mesh"),
  ("AP_GR_003GR_002_D", 7, 'M', "Mixed wildflower drift, 3.1 m"),
  ("CaliforniaPoppy_01_LOD", 5, 'M', "Rare warm interruption"),
 ]),
 ("C. Fern in sheltered dips only", 260, [
  ("Fern_01A", 24, 'D', "Sheltered dip cluster"),
  ("Fern_02A", 21, 'D', "Yellowed wind-burned fern"),
  ("Fern_02B", 19, 'D', "Second yellow variation"),
  ("Fern_01B", 18, 'D', "Fern variation"),
  ("RF_Fern1", 18, 'D', "Tiny fern, 9 tris"),
 ]),
 ("D. Bush islands — reverse slopes only, never the crown", 45, [
  ("RF_Bush2", 32, 'M', "Low bush island"),
  ("RF_Bush1", 28, 'M', "Sparse upright island"),
  ("RF_Bush3", 24, 'M', "Taller bush, reverse slope only"),
  ("TSA_BushDry_B", 16, 'M', "Dry bush variation"),
 ]),
 ("E. Orientation rock — a handful, for navigation", 8, [
  ("RF_Boulder3", 30, 'S', "Broad rock at ridge break"),
  ("RF_Boulder2", 26, 'S', "Low orientation rock"),
  ("RF_Boulder5", 22, 'S', "Largest landmark rock, 4.6 m"),
  ("AP_M6_Rock_FieldStoneStone06_SM", 22, 'S', "Single cover boulder"),
 ]),
])

B['Mountain'] = ("9. Mountain", "rock-dominant ascent", "2.5 / 5", 5, [
 ("A. Rock mass — SEE THE WARNING: we own no true granite masses", 130, [
  ("AP_TurtleLake_Rock_GoblinRock01_SM", 14, 'M', "Largest rounded boulder — but 50% buried, 1.6 m proud"),
  ("AP_M6_Rock_CemeteryRock02_SM", 13, 'M', "Angular ledge / entrance frame, 2.6 m proud"),
  ("AP_M6_Rock_SeashoreWallStone01_SM", 13, 'M', "Upright cliff fragment — tallest rock we own, 4.0 m"),
  ("RF_Boulder5", 11, 'M', "Pale lichen boulder, 2.5 m proud"),
  ("RF_Boulder4", 10, 'M', "Broad rock, 1.7 m proud"),
  ("AP_TurtleLake_Rock_GoblinRock02_SM", 9, 'M', "Second rounded boulder"),
  ("RF_Boulder3", 8, 'D', "Low broad rock"),
  ("AP_M6_Rock_FieldStoneStone06_SM", 7, 'D', "Scree stone, 1.3 m proud"),
  ("AP_M6_Rock_FieldStoneStone05_SM", 6, 'D', "Small scatter, 0.7 m — NOT the primary mass"),
  ("RF_Boulder2", 5, 'D', "Small scatter rock"),
  ("AP_TurtleLake_Rock_LakeRock02_SM", 2, 'D', "Damp-channel rock"),
  ("AP_TurtleLake_Rock_TurtleRock04_SM", 2, 'H', "11 m slab — hand-place as ledges and rockfall"),
 ]),
 ("B. Sentinel pines — reduce with elevation", 28, [
  ("AP_AlaskaCedar_001_2", 20, 'M', "Alaskan sentinel pine, 5.2 m"),
  ("AP_Norway_Spruce_01", 16, 'M', "Tall narrow spire, 4.0 m"),
  ("AP_Tree_04_PTree_05_SM", 13, 'M', "Tallest sparse pine, 4.2 m"),
  ("AP_Tree_04_PTree_04_SM", 11, 'S', "Wind-shaped pine"),
  ("AP_Tree_04_PTree_02_SM", 10, 'S', "Damaged pine"),
  ("AP_Tree_04_PTree_01_SM_2", 8, 'S', "Lower-slope pine"),
  ("AP_Tree_04_PTree_03_SM", 8, 'S', "Very sparse pine"),
  ("RF_Tree1", 7, 'M', "Bare elevation-limit pine, 314 tris"),
  ("RF_Tree2", 4, 'S', "Thin green pine"),
  ("AP_WhiteFir_MD_Dead_03", 3, 'S', "Damaged high-elevation fir"),
 ]),
 ("C. Crack-grown scrub — concentrate below overhangs and by water", 190, [
  ("AP_Neutral_Tree_A_PrickelGrass01_SM", 24, 'D', "Pale alpine scrub"),
  ("RF_Bush1", 20, 'D', "Upright crack-grown shrub"),
  ("AP_plant_001_14", 16, 'D', "Dark alpine shrub"),
  ("RF_Bush2", 14, 'D', "Low sheltered bush"),
  ("AP_plant_001_18", 10, 'M', "Coniferous ground spray, 3.9 m"),
  ("TSA_Heather_B", 8, 'D', "Heather — project asset GPT never used"),
  ("AP_Plant_003_10", 8, 'D', "Low fern-like variation"),
 ]),
 ("D. Damp pockets only", 120, [
  ("AP_Neutral_Foliage_A_Weed02_SM", 38, 'D', "Sparse grass in cracks"),
  ("Fern_02B", 24, 'D', "Yellow fern in a damp pocket"),
  ("RF_Fern1", 22, 'D', "Tiny fern"),
  ("AP_Neutral_Foliage_A_Weed01_SM", 16, 'M', "Larger clump at the treeline transition"),
 ]),
])

order = ['Forest', 'AutumnForest', 'Beach', 'EerieForest', 'HereticForest',
         'FlakTower', 'Fountain', 'Glade', 'Mountain']

for k in order:
    num_title, subtitle, dens, ha, strata = B[k]
    print("\n### %s — %s\n" % (num_title, subtitle))
    print("Density target **%s** · biome area ~**%.1f ha** (old survey, needs re-anchoring)\n" % (dens, ha))
    tot_i = 0.0; tot_t = 0.0
    for sname, d, items in strata:
        s = sum(x[1] for x in items)
        assert abs(s - 100) < 0.01, (k, sname, s)
        print(table(sname, d, items, ha, num_title))
        tot_i += d
        tot_t += sum(d * (x[1] / 100.0) * tr(x[0]) for x in items)
    print("**%s totals:** ~%s instances/ha · ~%s triangles/ha · ~%s instances across the biome.\n"
          % (num_title.split('. ')[1], "{:,.0f}".format(tot_i),
             "{:,.0f}".format(tot_t), "{:,.0f}".format(tot_i * ha)))

if CONFLICTS:
    print()
    print("> **Spacing-limited rows (marked WARN above).** For these the minimum-distance rule")
    print("> binds before the stratum target is reached, so the spawner places fewer than the")
    print("> weight asks for. The count column already shows the achievable number.")
    print()
    print("| Biome | Stratum | Asset | Tier | Wanted /ha | Achievable /ha |")
    print("|---|---|---|:--:|---:|---:|")
    for b, s_, n, t, w, c in CONFLICTS:
        print("| %s | %s | `%s` | %s | %.1f | %.1f |" % (b, s_.split(".")[0], n, t, w, c))
else:
    print()
    print("> Every row is density-limited: no spacing rule silently starves its stratum target.")
    print()
