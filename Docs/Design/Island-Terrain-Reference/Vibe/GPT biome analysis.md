# Mr. Moonlight - Biome Asset Distribution

## How to use the percentages

Every biome below has a normalized **100-point composition budget**. The percentage is the asset's share of the biome's visual/placement mix - it is not literal terrain coverage and should not be fed unchanged into one giant Unity scatter rule.

The **Spacing** column is the recommended center-to-center interval for that prefab or species, written as a minimum-maximum range in meters. It is intended as a direct starting value for Claude's `m_locationIncrementMin` and `m_locationIncrementMax`. It is not edge-to-edge clearance and does not replace collision, slope, water, landmark, or path-exclusion checks.

Example: `5.0-8.0` means `m_locationIncrementMin = 5.0f` and `m_locationIncrementMax = 8.0f`. Randomize within the range rather than placing assets on a visible fixed grid.

Continue to run separate scatter passes for structural trees, understory/ground cover, debris/rocks, and hand-placed hero assets. Re-normalize the percentages inside the appropriate scatter pass. For example, an asset with 4 points among 20 points of structural trees becomes a 20% structural-tree spawn weight. Hero and ritual assets remain hand-placed or tightly capped even when they have a listed visual weight.

### Player-movement envelope

- Paint navigation exclusion splines or masks before vegetation scattering. Spacing alone cannot guarantee a traversable forest.
- Keep primary progression routes **2.4-3.0 m clear** between collidable trunks, rocks, and logs. Secondary “lost in the woods” routes may narrow to **1.5-1.9 m**, but must not dead-end accidentally.
- For a collidable asset, keep its center at least `path half-width + collider radius + 0.25 m` from the route centerline. Low non-colliding grass, flowers, and ferns may enter the route visually.
- Provide a roughly **5-8 m combat/readability pocket every 25-40 m** in the densest forests. The vegetation can close behind and ahead of it so the player still feels buried.
- Never let rocks, deadfall, or hero trees block more than about **35% of a corridor width** unless the obstruction is an authored climb, vault, crouch, or deliberate detour.

All prefab names below reproduce the text shown at the top of the supplied screenshots. `RF_*` names reproduce the image filenames, as requested. Fixed landmark/addition lists are deliberately outside the 100-point supplied-asset budget.

## Global visual logic

The island should feel cold, wet, blocky, and silhouette-led, with PS1/N64-like simplicity and pixelated textures. Ordinary ecology stays mostly dark green, brown, gray, and muted autumn color. Acid/punk color is strongest around flowers, ritual nodes, hallucinations, blue-fire protection, and the nightly terrain shift.

| Biome | Vegetation density | Dominant silhouette | Normal-state acid-color ceiling |
|---|---:|---|---:|
| Autumn Forest | 4.5 / 5 | Pale autumn trunks broken by dark pine spires | 8% |
| Beach | 1 / 5 on sand; 5 / 5 at treeline | Driftwood diagonals and low wet rocks | 2% |
| Eerie Forest | 4.5 / 5 trunk density | Repeating dead verticals and claw branches | 8% |
| Heretic Forest | 4 / 5 | Dead-tree cathedral with rare ritual icons | 25% |
| Flak Tower | 3 / 5; ground layer is continuous | Low flower carpet beneath tower mass | 12% |
| Forest | 5 / 5 | Huge conifer columns, boulders, and deadfall | 5% |
| Fountain | 4 / 5 at perimeter; open center | Arched mystical trees around the well | 18% |
| Glade | 3.5 / 5 below knee height | Wind-shaped grass and enormous sky | 8% |
| Mountain | 2.5 / 5 | Fractured rock framing a black mine opening | 4% |

The acid-color ceiling is a separate art-direction metric, not part of the asset-weight totals. During hallucinations or the nightly terrain shift it can rise to roughly 1.5-2.5 times the listed value, with Heretic Forest and Fountain taking the strongest color treatment.

---

## 1. Autumn Forest

### Main composition and reasoning

This is the island's beautiful lie: a calm, Halloween-like orange corridor that initially feels safer than the surrounding woods. Warm deciduous trees make the dominant mass, but pines take roughly one-third of the tree budget so the biome still reads as part of the Alaskan-inspired island. Orange leaves and pale trunks should glow against a nearly black-green conifer wall; paths remain clean enough to navigate but bend out of sight quickly.

### Supplied asset mix - 100%

| Asset | Weight | Spacing (m) | Placement intent |
|---|---:|---:|---|
| `AP_ENV_tree_SaGeeSukRim` | 6% | 3.5-6.0 | Small orange crowns in irregular groups |
| `AP_ENV_tree_ToeMunJean` | 6% | 4.0-7.0 | Low, broad autumn masses near paths |
| `AP_FallTree_01_SM` | 6% | 4.5-7.5 | Primary warm vertical |
| `AP_FallTree_01_SM_1` | 5% | 4.5-7.5 | Secondary yellow-orange vertical |
| `AP_FallTree_02_SM` | 4% | 5.0-8.5 | Sparse late-autumn transition tree |
| `AP_Tree_color_001_01_2` | 5% | 5.0-8.0 | Dense amber crown |
| `AP_Tree_color_001_03` | 4% | 5.0-8.0 | Olive/russet variation |
| `AP_Tree_AUT_White_A_02_SM` | 4% | 3.5-6.0 | Pale-bark cluster accent |
| `AP_Tree_AUT_White_A_03_SM` | 3% | 4.0-7.0 | Sparse pale-bark transition |
| `AP_Norway_Spruce_01` | 5% | 5.5-9.0 | Tall dark spire and navigation rhythm |
| `AP_BC_PineTree_02` | 5% | 5.0-9.0 | Broad evergreen mass |
| `AP_Tree_04_GTree01_01_SM_2` | 4% | 6.0-10.0 | Old, heavy fir silhouette |
| `AP_Tree_Conifir_A_01_SM_2` | 4% | 5.5-9.5 | Dense evergreen background |
| `RF_Tree3` | 3% | 4.0-7.0 | Thin pine variation and depth filler |
| `Fern_01A` | 5% | 0.7-1.3 | Dominant green fern bed |
| `Fern_01B` | 4% | 0.8-1.5 | Fern variation |
| `AP_plant_001_13` | 4% | 1.5-3.0 | Rust-orange shrub patches |
| `AP_Plant_001_08` | 3% | 1.5-2.8 | Dark green shrub counterpoint |
| `AP_Neutral_Foliage_A_Weed01_SM` | 3% | 0.8-1.5 | Path-edge grass clumps |
| `AP_Mushroom_A01_2` | 2% | 1.2-2.5 | Small saturated Halloween accent |
| `AP_Flower_001_09` | 2% | 2.0-4.0 | Rare red floral spot |
| `RF_Bush2` | 3% | 2.0-4.0 | Low leafy blocker |
| `RF_Log1` | 2% | 8.0-14.0 | Mossy horizontal trail frame |
| `RF_Stump1` | 2% | 7.0-12.0 | Cut/broken rhythm near clearings |
| `AP_Tree_Break_MushroomTrunk_01_SM` | 2% | 12.0-20.0 | Fungal hero stump |
| `RF_Boulder2` | 2% | 6.0-10.0 | Small mossy rock cluster |
| `AP_TurtleLake_Rock_LakeRock02_SM` | 2% | 7.0-12.0 | Damp rock at bowls and drainage lines |
| **Total** | **100%** |  |  |

### Placement and additions

- Arrange orange trees in 5-12-tree groves, then pierce each grove with one or two dark pines. Do not alternate species evenly.
- Keep a continuous 2.4-3.0 m progression route and optional 1.5-1.9 m side paths, but use leaf banks, logs, and low branches to make every view turn within 20-35 m.
- Add a full orange leaf-litter terrain layer, damp black soil, leaf piles, moss decals, and occasional pale birch/aspen saplings. These surfaces are not included in the 100% prefab mix.

---

## 2. Beach

### Main composition and reasoning

The beach is a cold exposure zone: broad, empty tidal sand with isolated cover and a threatening black treeline. Low rocks and long driftwood establish horizontal bands, while one or two wind-stressed pines are enough to connect it to the island. The openness provides relief from forest claustrophobia but makes the player visible for an uncomfortable amount of time.

### Supplied asset mix - 100%

| Asset | Weight | Spacing (m) | Placement intent |
|---|---:|---:|---|
| `AP_M6_Rock_SeashoreWallStone01_SM` | 12% | 18.0-30.0 | Primary shore outcrop |
| `AP_M6_Rock_FieldStoneStone05_SM` | 8% | 10.0-18.0 | Rounded wet boulder |
| `AP_M6_Rock_FieldStoneStone06_SM` | 8% | 12.0-22.0 | Larger fieldstone variation |
| `AP_TurtleLake_Rock_LakeRock02_SM` | 8% | 8.0-16.0 | Low tide-pool edge rock |
| `AP_TurtleLake_Rock_TurtleRock04_SM` | 6% | 10.0-18.0 | Flat cover slab |
| `RF_Boulder1` | 4% | 6.0-12.0 | Mossy inland-edge rock |
| `RF_Boulder2` | 4% | 6.0-10.0 | Low scattered stone |
| `RF_Boulder5` | 3% | 8.0-14.0 | Wet rock color variation |
| `AP_TurtleLake_Rock_GoblinRock02_SM` | 2% | 20.0-35.0 | Occasional large landmark boulder |
| `RF_Log1` | 7% | 14.0-28.0 | Long bleached horizontal |
| `RF_Log2` | 6% | 12.0-24.0 | Broken tapered log |
| `RF_Log3` | 5% | 12.0-24.0 | Forked driftwood variation |
| `AP_TurtleLake_Tree_BrokenTree01_SM` | 6% | 18.0-32.0 | Large shore log |
| `AP_Tree_Break_02_SM` | 4% | 15.0-28.0 | Dark waterlogged trunk |
| `AP_Tree_Break_03_SM` | 3% | 18.0-30.0 | Secondary trunk variation |
| `RF_Stump2` | 2% | 14.0-24.0 | Root-like vertical accent |
| `AP_TurtleLake_Tree_Stump02_SM` | 2% | 16.0-28.0 | Root cluster near wrack line |
| `AP_AlaskaCedar_001_2` | 2% | 35.0-55.0 | Rare wind-beaten survivor |
| `RF_Tree1` | 1% | 50.0-80.0 | Nearly bare shore pine |
| `RF_Tree2` | 1% | 40.0-70.0 | Sparse second pine variant |
| `AP_Tree_04_PTree_05_SM` | 2% | 35.0-60.0 | Tall, thin transition pine |
| `RF_Bush1` | 1% | 10.0-18.0 | Treeline-edge shrub only |
| `AP_Neutral_Foliage_A_Weed01_SM` | 1% | 4.0-8.0 | Sparse grass above high-tide line |
| `AP_Neutral_Foliage_A_Weed02_SM` | 1% | 5.0-10.0 | Salt-beaten grass variation |
| `AP_Neutral_Tree_A_PrickelGrass01_SM` | 1% | 6.0-12.0 | Pale dune/wrack grass |
| **Total** | **100%** |  |  |

### Placement and additions

- Leave 60-75% of walkable sand visually empty. Place assets in bands and small storm-deposit clusters, not uniform noise.
- Rotate driftwood to frame movement or provide cover, but never allow random logs to chain into a full barrier between the waterline and treeline.
- Keep trees at the forest transition or on a rocky point; never build a beach grove.
- Add wet charcoal sand, pebble bands, kelp/seaweed, tide pools, foam/wrack decals, one large root-ball hero, and ruined dock fragments near the island entry. These are outside the 100% mix.

---

## 3. Eerie Forest

### Main composition and reasoning

This is a dense maze of dead verticals rather than a fantasy wasteland. Repeated trunks, hanging branch webs, ash-dark ground, and fog create short tunnels; a few surviving pines prove that the normal forest has been corrupted. Human-made ritual objects stay minimal here so the Heretic Forest can escalate meaningfully.

### Supplied asset mix - 100%

| Asset | Weight | Spacing (m) | Placement intent |
|---|---:|---:|---|
| `AP_GraveKeepers_B01` | 6% | 3.5-6.0 | Hanging, lateral branch silhouette |
| `AP_GraveKeepers_B02` | 6% | 3.0-5.5 | Upright tangled dead tree |
| `AP_GraveKeepers_B03_2` | 5% | 4.0-7.0 | Broad crooked branch mass |
| `AP_GraveKeepers_B04` | 5% | 3.0-5.0 | Narrow snag variation |
| `AP_GraveKeepers_B06` | 5% | 4.0-6.5 | Forked corridor blocker |
| `AP_GraveKeepers_B07` | 5% | 4.0-7.0 | Dense claw-shaped crown |
| `AP_Tree_DeadTree01_SM` | 5% | 3.5-6.0 | Primary leafless tree |
| `AP_Tree_Deadtree06_SM` | 5% | 4.5-7.5 | Large twisted dead tree |
| `AP_Tree_Dry_D01` | 4% | 4.0-7.0 | Root-heavy decayed trunk |
| `AP_Tree_Dry_N02` | 3% | 7.0-12.0 | Fallen branch / low obstruction |
| `AP_Tree_WNT_03_Bark_01_SM_2` | 4% | 2.5-4.5 | Thin winter snag |
| `AP_Tree_WNT_M01_01_SM_3` | 4% | 3.5-6.0 | Bare winter crown |
| `AP_Tree_WNT_M_03_SM` | 4% | 3.5-6.0 | Dense black branch lattice |
| `AP_DeadTree02` | 3% | 3.5-6.0 | Strong Y-shaped negative space |
| `AP_DeadTree04` | 3% | 4.0-7.0 | Leaning claw silhouette |
| `AP_S_Tree_01` | 3% | 4.0-7.0 | Rooted dead-tree variation |
| `AP_Neutral_Foliage_A_Weed01_SM` | 5% | 0.9-1.6 | Dark dead-grass floor |
| `AP_Neutral_Tree_A_PrickelGrass01_SM` | 4% | 1.2-2.2 | Pale ghostlike scrub |
| `AP_Nest_B01` | 3% | 5.0-9.0 | Stick and twig nests |
| `AP_Tree_Burnt_04_SM` | 3% | 5.0-9.0 | Charred vertical punctuation |
| `AP_Tree_Break_Root_02_SM` | 3% | 6.0-10.0 | Root trip hazard / low silhouette |
| `RF_Stump2` | 2% | 5.0-9.0 | Narrow broken stump |
| `RF_Log2` | 2% | 7.0-12.0 | Fallen corridor obstruction |
| `RF_Boulder4` | 3% | 7.0-12.0 | Moss-dark cover rock |
| `AP_AlaskaCedar_001_2` | 2% | 14.0-24.0 | Rare surviving pine |
| `RF_Tree2` | 1% | 16.0-28.0 | Sickly green survivor |
| `AP_BC_PineTree_03` | 1% | 18.0-30.0 | Distant normal-tree reminder |
| `AP_Norway_Spruce_01` | 1% | 18.0-30.0 | Single dark vertical landmark |
| **Total** | **100%** |  |  |

### Placement and additions

- Use 65-80% trunk occlusion in the mid-distance, but reserve one continuous 2.4-3.0 m progression route plus occasional 1.5-1.9 m secondary channels.
- Form arches, inward leans, and dense snag junctions; do not rotate every tree randomly without silhouette checks.
- Add black leaf litter/ash, dead grass, thorn scrub, lightning scars, root pits, local cyan fog, and tightly controlled red backlight. Keep crosses, altars, and effigies out of most of this biome.

---

## 4. Heretic Forest

### Main composition and reasoning

Heretic Forest is Eerie Forest turned into an authored ritual landscape. The dead-tree base remains dominant, but selected trunks lean inward like cathedral ribs and funnel the player toward ceremonial clearings. Hero trees and pagan/punk-Christian props are rare, deliberate icons - one strong revelation per ritual node is more disturbing than evenly scattered occult clutter.

### Supplied asset mix - 100%

| Asset | Weight | Spacing (m) | Placement intent |
|---|---:|---:|---|
| `AP_GraveKeepers_B01` | 4% | 4.0-7.0 | Hanging branch frame |
| `AP_GraveKeepers_B02` | 4% | 3.5-6.0 | Tangled upright base tree |
| `AP_GraveKeepers_B03_2` | 3% | 4.5-7.5 | Crooked branch mass |
| `AP_GraveKeepers_B04` | 3% | 3.0-5.5 | Narrow snag |
| `AP_GraveKeepers_B06` | 3% | 4.0-7.0 | Forked funnel tree |
| `AP_GraveKeepers_B07` | 3% | 4.5-7.5 | Dense claw crown |
| `AP_Tree_WNT_03_Bark_01_SM_2` | 4% | 2.8-5.0 | Thin background bars |
| `AP_Tree_DeadTree01_SM` | 3% | 3.5-6.0 | Normal dead-tree base |
| `AP_Tree_Deadtree06_SM` | 3% | 4.5-8.0 | Large twisted base tree |
| `AP_Tree_Curse_J07` | 4% | 5.0-8.0 | Ritual-adjacent crooked tree |
| `AP_Tree_Curse_J08` | 4% | 4.5-7.5 | Taller curse-tree variation |
| `AP_Tree_Curse_K01` | 4% | 5.0-8.5 | Thick clawed trunk |
| `AP_Tree_Dry_D01` | 3% | 4.5-7.5 | Root-heavy decayed tree |
| `AP_Tree_Dry_N02` | 3% | 8.0-14.0 | Low dead branch |
| `AP_DeadTree02` | 3% | 3.5-6.0 | Y-shaped silhouette |
| `AP_DeadTree03` | 2% | 4.0-7.0 | Leaning branch frame |
| `AP_DeadTree04` | 2% | 4.0-7.0 | Second leaning variation |
| `AP_Building_exorcist_tree2` | 4% | 30.0-50.0 | One primary ritual-node landmark |
| `AP_GangshiTree_2` | 3% | 25.0-40.0 | Ancient gnarled altar tree |
| `AP_M6_Tree_MonsterTreeBark_SM_PHJ_2` | 3% | 30.0-50.0 | Monstrous root silhouette |
| `AP_Tree_Heretic_A01_2` | 3% | 25.0-40.0 | Sculptural heretic tree |
| `AP_Tree_Heretic_B03` | 2% | 18.0-30.0 | Tall stake-like cluster |
| `AP_Tree_Heretic_B05` | 2% | 20.0-35.0 | Spear/tree variation |
| `AP_Tree_Heretic_D02_01` | 2% | 12.0-24.0 | Crawling root formation |
| `AP_Tree_Heretic_D03` | 1.5% | 12.0-22.0 | Low root altar frame |
| `AP_Tree_Heretic_D03_02` | 1.5% | 12.0-22.0 | Second root formation |
| `AP_Tree_Curse_H01_2` | 1% | 24.0-40.0 | Cloth/hanging curse-tree accent |
| `AP_sunghwangdang_Tree_pagoda_01` | 1% | 18.0-30.0 | Bound stone totem |
| `AP_sunghwangdang_Tree_pagoda_01_1` | 1% | 18.0-30.0 | Alternate cairn/totem |
| `AP_GraveKeepers_C01` | 2% | 10.0-18.0 | Bound ritual boulder |
| `AP_GraveKeepers_C02` | 2% | 10.0-18.0 | Narrow bound stone |
| `AP_Nest_B01` | 2% | 5.0-9.0 | Twig altar filler |
| `AP_Tree_Burnt_04_SM` | 2% | 6.0-10.0 | Charred marker |
| `AP_Tree_Break_Root_02_SM` | 2% | 7.0-12.0 | Root ring / barrier |
| `RF_Stump2` | 2% | 6.0-10.0 | Stake-like broken trunk |
| `RF_Log2` | 1% | 8.0-14.0 | Low ritual boundary |
| `RF_Boulder4` | 2% | 8.0-14.0 | Dark cover stone |
| `AP_AlaskaCedar_001_2` | 2% | 18.0-30.0 | Rare surviving pine |
| `AP_BC_PineTree_03` | 1% | 20.0-35.0 | Living silhouette contrast |
| `RF_Tree2` | 1% | 18.0-30.0 | Sickly survivor |
| `AP_Norway_Spruce_01` | 1% | 20.0-35.0 | Distant normal-tree anchor |
| **Total** | **100%** |  |  |

### Placement and additions

- Treat hero weights as visual importance, not unrestricted random spawn chance. Cap the Exorcist Tree, Monster Tree, Gangshi Tree, and each Heretic Tree to one dominant specimen per authored node.
- Use dead-tree density gradients to funnel the player through a 2.4-3.0 m protected route into 12-25 m ritual clearings, then reveal detail only at close range.
- Add altars, skulls, crosses, Orthodox/Catholic fragments, cords, bone, candles, dirty cloth, wet crimson paint, and small fire sources. Keep Inuit, missionary, Catholic, and fictional-cult motifs distinct; avoid generic “tribal” decoration.

---

## 5. Flak Tower

### Main composition and reasoning

This is a low autumn/tundra plain with flowers almost everywhere and the WWII tower always winning the skyline. Sparse pines and small autumn trees cast long moving shadows and create intermittent combat cover without hiding the landmark. Cheerful flower color against brutal concrete is the biome's coolest tonal contrast.

### Supplied asset mix - 100%

| Asset | Weight | Spacing (m) | Placement intent |
|---|---:|---:|---|
| `AP_Neutral_Foliage_A_Weed01_SM` | 8% | 0.6-1.2 | Primary grass carpet |
| `AP_Neutral_Foliage_A_Weed02_SM` | 8% | 0.7-1.4 | Taller grass variation |
| `AP_Neutral_Foliage_A_WildFlower01_SM_JYI` | 6% | 1.0-2.0 | Sparse flowered scrub |
| `AP_GR_003GR_002_D` | 5% | 1.2-2.4 | Natural mixed wildflower patch |
| `AP_flower_001_10` | 5% | 1.5-3.0 | Green-yellow daisy clump |
| `AP_flower_001_11` | 4% | 1.5-3.0 | Rust-red daisy clump |
| `Orange Aster_LOD` | 5% | 1.8-3.5 | Primary orange accent |
| `YellowAfricanDaisy_LOD` | 4% | 1.8-3.5 | Ochre-yellow field accent |
| `YellowDaisy_LOD` | 4% | 1.5-3.0 | Low yellow drift |
| `CaliforniaPoppy_01_LOD` | 3% | 2.0-4.0 | Warm orange-red accent |
| `Tangerine Violet_LOD` | 3% | 2.0-4.0 | Acid-orange transition accent |
| `AP_plant_001_13` | 4% | 2.5-5.0 | Autumn-red shrub band |
| `AP_plant_001_14` | 3% | 2.5-5.0 | Darker low shrub |
| `AP_Plant_003_07` | 3% | 4.0-7.0 | Isolated low autumn form |
| `AP_FallTree_01_SM` | 4% | 10.0-18.0 | Primary autumn tree |
| `AP_FallTree_01_SM_1` | 3% | 12.0-20.0 | Secondary yellow tree |
| `AP_ENV_tree_SaGeeSukRim` | 3% | 8.0-14.0 | Small autumn crown |
| `AP_ENV_tree_ToeMunJean` | 3% | 9.0-16.0 | Broad low autumn cover |
| `AP_Norway_Spruce_01` | 3% | 14.0-24.0 | Thin sentinel spire |
| `AP_BC_PineTree_02` | 3% | 16.0-28.0 | Occasional broad pine shadow |
| `RF_Tree3` | 2% | 16.0-26.0 | Sparse pine variation |
| `AP_AlaskaCedar_001_2` | 2% | 18.0-30.0 | Wind-beaten pine |
| `AP_Tree_04_PTree_05_SM` | 2% | 16.0-28.0 | Very sparse vertical |
| `RF_Boulder1` | 2% | 12.0-20.0 | Low mossy cover |
| `RF_Boulder2` | 2% | 10.0-18.0 | Scattered field rock |
| `AP_M6_Rock_FieldStoneStone05_SM` | 2% | 15.0-25.0 | Larger cover boulder |
| `RF_Log1` | 1% | 18.0-30.0 | Rare fallen horizontal |
| `RF_Stump1` | 1% | 16.0-28.0 | Rare stump |
| `AP_TurtleLake_Rock_LakeRock02_SM` | 2% | 12.0-22.0 | Low drainage rock |
| **Total** | **100%** |  |  |

### Placement and additions

- Keep the tower visible from most traversal points. Flower and shrub bands can curve toward it as subtle navigation lines, while 3-4 m collision-free lanes remain open through the field.
- Use broad swales and reverse slopes so enemies disappear without requiring a dense tree wall.
- Add the Flak Tower landmark, cracked concrete, sandbags, shallow trenches, rusted metal, wire, and restrained ammunition debris. These are fixed dressing outside the 100% mix.

---

## 6. Forest

### Main composition and reasoning

This is the main lost biome: wet, gray, enormous, and overwhelmingly coniferous in the spirit of *First Blood*. It has the highest total density and occupies every vertical layer, but the player still receives authored movement corridors and occasional micro-landmarks. Folded terrain, deadfall, boulders, and fog keep sightlines short even when tree spacing opens slightly for combat.

### Supplied asset mix - 100%

| Asset | Weight | Spacing (m) | Placement intent |
|---|---:|---:|---|
| `AP_Tree_04_GTree01_01_SM_2` | 5% | 4.5-7.0 | Primary old-growth fir |
| `AP_Tree_04_GTree01_02_SM` | 5% | 4.5-7.5 | Dense fir variation |
| `AP_Tree_04_GTree01_03_SM` | 4% | 4.0-6.5 | Narrow fir variation |
| `AP_Tree_04_GTree01_04_SM` | 4% | 5.0-8.0 | Leaning/irregular fir |
| `AP_Tree_04_GTree01_05_SM` | 3% | 4.5-7.0 | Sparse crown transition |
| `AP_Tree_04_GTree01_06_SM` | 3% | 5.0-8.0 | Sixth old-growth variation |
| `AP_Tree_04_M01_01_SM_2` | 4% | 4.0-6.5 | Secondary spruce family |
| `AP_Tree_04_M01_02_SM` | 3% | 4.5-7.0 | Tall spruce variation |
| `AP_Tree_04_M01_03_SM` | 3% | 4.0-6.5 | Thin spruce variation |
| `AP_Tree_04_M01_04_SM` | 2% | 5.0-8.0 | Sparse spruce |
| `AP_Tree_04_M01_05_SM` | 2% | 5.0-8.0 | Edge-of-gap spruce |
| `AP_Tree_Conifir_A_01_SM_2` | 4% | 5.0-8.0 | Dense evergreen mass |
| `AP_Tree_Conifir_A_02_SM` | 3% | 5.5-9.0 | Conifer variation |
| `AP_Norway_Spruce_01` | 3% | 4.5-7.0 | Tall dark spire |
| `AP_BC_PineTree_02` | 2% | 5.0-8.0 | Broad pine |
| `AP_BC_PineTree_03` | 2% | 5.0-8.0 | Asymmetric pine |
| `RF_Tree3` | 1% | 3.5-6.0 | Thin pine filler |
| `RF_Tree4` | 1% | 4.0-7.0 | Tall sparse filler |
| `AP_M6_Tree_ForestTree08_SM_JYI_2` | 2% | 6.0-10.0 | Rare broadleaf break in pine wall |
| `AP_Tree_Blackpoplar01_SM` | 1% | 7.0-12.0 | Wet lowland tree |
| `AP_Tree_Oak01_SM` | 1% | 10.0-16.0 | Rare old broadleaf landmark |
| `Fern_01A` | 4% | 0.55-1.10 | Dominant fern bed |
| `Fern_01B` | 4% | 0.60-1.20 | Fern variation |
| `Fern_02A` | 3% | 0.65-1.30 | Yellow-green fern |
| `Fern_03A` | 3% | 0.70-1.40 | Long-frond fern variation |
| `AP_Plant_001_08` | 3% | 1.2-2.5 | Dense leafy shrub |
| `AP_plant_001_28` | 3% | 1.2-2.5 | Brighter green shrub |
| `AP_M6_Tree_Bushtree01_SM` | 2% | 2.5-4.5 | Multi-stem cover bush |
| `AP_M6_Tree_Bushtree02_SM` | 2% | 2.8-5.0 | Lighter multi-stem variation |
| `RF_Bush2` | 1% | 1.2-2.4 | Low leafy cluster |
| `RF_Bush3` | 1% | 1.5-3.0 | Tall close-camera brush |
| `AP_Mushroom_B01` | 1% | 1.5-3.0 | Damp fungal patch |
| `AP_Mushroom_C01` | 1% | 1.5-3.0 | Second fungal patch |
| `RF_Boulder1` | 2% | 6.0-10.0 | Mossy cover boulder |
| `RF_Boulder3` | 2% | 7.0-12.0 | Low broad boulder |
| `RF_Boulder4` | 2% | 8.0-14.0 | Large wet moss boulder |
| `AP_M6_Rock_FieldStoneStone05_SM` | 2% | 8.0-14.0 | Ravine/stream rock |
| `AP_M6_Rock_FieldStoneStone06_SM` | 2% | 10.0-16.0 | Large streamside rock |
| `RF_Log1` | 1% | 7.0-12.0 | Fallen-log corridor frame |
| `RF_Log2` | 1% | 8.0-14.0 | Broken deadfall |
| `RF_Stump1` | 1% | 6.0-10.0 | Stump near small clearings |
| `AP_Tree_Break_MushroomTrunk_01_SM` | 1% | 12.0-20.0 | Fungal old-growth stump |
| **Total** | **100%** |  |  |

### Placement and additions

- Build with 3-7-tree same-species clusters, not checkerboard randomness. Offset clusters vertically on banks so the canopy interlocks.
- Aim for 8-25 m combat sightlines, but preserve a 2.4-3.0 m primary movement spline and 1.5-1.9 m optional side routes; allow low non-colliding foliage to brush into both. Use rare 40 m glimpses toward cabins, waterfalls, flashes, or marked trees.
- Add needle litter, mud, moss decals, root steps, branch piles, creek beds, waterfall/stream modules, fog volumes, and occasional concealed bear traps or punji-pit dressing. Terrain folding is as important as foliage density.

---

## 7. Fountain

### Main composition and reasoning

Fountain is a mystical sanctuary: a luminous flower basin around the canonical stone well and Our Lady of Guadalupe, protected by huge sheltering trees and a dark pine ring. The center stays readable for combat and for the blue-fire event, while massive roots and trunks provide cover. It should feel genuinely calm, but unnaturally perfect and slightly sealed off from the island.

### Supplied asset mix - 100%

| Asset | Weight | Spacing (m) | Placement intent |
|---|---:|---:|---|
| `CupcakeWhite_01_LOD` | 8% | 0.8-1.6 | Primary white luminous drift |
| `Blue Aster_LOD` | 7% | 1.2-2.4 | Tall blue accent |
| `African_violet_blue_LOD` | 7% | 0.7-1.4 | Dense blue low patch |
| `African_violet_LOD` | 5% | 0.8-1.5 | Purple-blue variation |
| `Indigo Violet_LOD` | 5% | 1.0-2.0 | Deep cool accent |
| `Purple Violet_LOD` | 4% | 1.0-2.0 | Purple low drift |
| `VioletDaisy_LOD` | 4% | 1.0-2.0 | Pale daisy patch |
| `AP_Samakyo_flower_003` | 4% | 1.2-2.5 | Thin mystical blue flower |
| `AP_flower_001_12` | 4% | 2.0-4.0 | Blue hydrangea clump |
| `AP_Flower_001_08` | 3% | 2.0-4.0 | Second blue floral clump |
| `AP_Neutral_Foliage_A_Weed01_SM` | 4% | 0.6-1.2 | Soft grass matrix |
| `AP_Neutral_Foliage_A_Weed02_SM` | 3% | 0.7-1.4 | Taller grass variation |
| `Fern_01A` | 2% | 1.0-2.0 | Fern at shaded roots |
| `AP_Tree_10_ArgassTree_02_SM` | 4% | 10.0-18.0 | First sculptural arch tree |
| `AP_Tree_10_ArgassTree_03_SM` | 4% | 10.0-18.0 | Second arch variation |
| `AP_Tree_10_ArgassTree_04_SM` | 3% | 12.0-20.0 | Broad mystical cover tree |
| `AP_Tree_10_ArgassTree_SM` | 3% | 12.0-22.0 | Large central-frame tree |
| `AP_Tree_Lake_RoundTree_01_SM` | 4% | 18.0-30.0 | Curling witchlike silhouette |
| `AP_Tree_Juniper02_SMIK` | 3% | 10.0-18.0 | Pale twisted canopy |
| `AP_Tree_Juniper03_SMIK` | 2% | 12.0-20.0 | Juniper variation |
| `AP_ENV_tree_Nokmyung` | 2% | 9.0-16.0 | Soft round green canopy |
| `AP_Tree_Conifir_A_01_SM_2` | 3% | 5.0-9.0 | Dark outer ring |
| `AP_Tree_04_GTree01_01_SM_2` | 3% | 6.0-10.0 | Heavy pine frame |
| `AP_Norway_Spruce_01` | 2% | 5.0-9.0 | Tall perimeter spire |
| `RF_Tree3` | 2% | 4.0-7.0 | Thin perimeter filler |
| `AP_TurtleLake_Rock_LakeRock02_SM` | 2% | 8.0-14.0 | Low damp stone near rill |
| `RF_Boulder2` | 1% | 7.0-12.0 | Mossy cover pebble/boulder |
| `RF_Boulder3` | 1% | 8.0-14.0 | Broad low cover |
| `AP_Tree_Break_Root_02_SM` | 1% | 10.0-16.0 | Exposed root shape |
| **Total** | **100%** |  |  |

### Placement and additions

- Keep a 10-16 m readable ring around the fountain/well plus at least two 2.5-3.0 m clear approach spokes; put the largest trees just outside it so their trunks frame rather than obscure the statue.
- Cluster flowers by color in soft drifts. Do not evenly confetti every flower prefab across the field.
- Add the stone well/fountain, Our Lady of Guadalupe statue, clear-water rills, damp stone materials, mist, and a blue-emissive fire-ring state. Those fixed assets are outside the 100% mix.

---

## 8. Glade

### Main composition and reasoning

Glade is a treeless, wind-beaten hill with a very large sky. Ground vegetation is dense everywhere but stays below eye level; flowers form irregular drifts and low bushes appear only as islands. The openness supports the meteor-shower observation point while reverse slopes and violent synchronized wind preserve tension.

### Supplied asset mix - 100%

| Asset | Weight | Spacing (m) | Placement intent |
|---|---:|---:|---|
| `AP_Neutral_Foliage_A_Weed01_SM` | 15% | 0.45-0.90 | Primary short-grass matrix |
| `AP_Neutral_Foliage_A_Weed02_SM` | 15% | 0.55-1.10 | Taller wind-driven matrix |
| `AP_Neutral_Tree_A_PrickelGrass01_SM` | 8% | 0.8-1.5 | Pale exposed-hill grass |
| `Fern_01A` | 5% | 1.0-2.0 | Sheltered dip clusters |
| `Fern_01B` | 4% | 1.1-2.2 | Fern variation |
| `Fern_02A` | 4% | 1.2-2.4 | Yellowed wind-burned fern |
| `Fern_02B` | 4% | 1.2-2.4 | Second yellow fern variation |
| `RF_Bush1` | 4% | 4.0-8.0 | Sparse upright island |
| `RF_Bush2` | 3% | 5.0-10.0 | Low bush island |
| `RF_Bush3` | 3% | 6.0-12.0 | Taller bush in reverse slope only |
| `AP_GR_003GR_002_D` | 5% | 1.2-2.5 | Natural mixed wildflower drift |
| `AP_flower_001A` | 4% | 1.0-2.0 | Low blue flower carpet |
| `BlueEyeGrass_01_LOD` | 4% | 1.5-3.0 | Cool purple-blue accent |
| `VioletDaisy_LOD` | 3% | 1.8-3.5 | Pale violet drift |
| `YellowDaisy_LOD` | 3% | 1.8-3.5 | Muted yellow drift |
| `CupcakeWhite_01_LOD` | 3% | 1.5-3.0 | White windblown patch |
| `Purple Aster_LOD` | 2% | 2.0-4.0 | Purple accent |
| `CaliforniaPoppy_01_LOD` | 1% | 3.0-6.0 | Rare warm interruption |
| `CoralBells_green` | 2% | 2.0-4.0 | Low green/pink clump |
| `RF_Boulder2` | 2% | 18.0-30.0 | Low orientation rock |
| `RF_Boulder3` | 2% | 20.0-35.0 | Broad rock at ridge break |
| `AP_TurtleLake_Rock_TurtleRock04_SM` | 2% | 20.0-35.0 | Flat isolated landmark |
| `AP_M6_Rock_FieldStoneStone05_SM` | 2% | 25.0-40.0 | Single cover boulder |
| **Total** | **100%** |  |  |

### Placement and additions

- No trees inside the Glade biome volume. A dense forest wall can sit beyond its boundary but must not leak into the hill scatter.
- Animate grasses in large coherent wind waves. Keep grass and flowers non-colliding, then reserve 3-4 m flattened paths and trampled circles to keep movement and the telescope approach readable.
- Add the mounted telescope, extinguished bonfire, sleeping bags, scattered drinks and belongings, broken guitar, photograph, boots, and pickaxe embedded in the telescope frame. These fixed story props are outside the 100% mix.

---

## 9. Mountain

### Main composition and reasoning

Mountain is a rock-dominant, lower-tension ascent that gives the player scale and breathing room before the mine. Fractured granite, scree, and ledges do most of the visual work; sparse pines and low shrubs cling to cracks and damp pockets. The mine entrance must read from a distance as a simple black wound framed by rockfall and two or three sentinel pines.

### Supplied asset mix - 100%

| Asset | Weight | Spacing (m) | Placement intent |
|---|---:|---:|---|
| `AP_M6_Rock_FieldStoneStone05_SM` | 10% | 4.0-8.0 | Primary granite mass |
| `AP_M6_Rock_FieldStoneStone06_SM` | 10% | 5.0-9.0 | Large block variation |
| `AP_M6_Rock_SeashoreWallStone01_SM` | 8% | 8.0-14.0 | Upright cliff fragment |
| `AP_M6_Rock_CemeteryRock02_SM` | 7% | 10.0-16.0 | Angular ledge/entrance frame |
| `AP_TurtleLake_Rock_GoblinRock01_SM` | 6% | 12.0-20.0 | Large rounded boulder |
| `AP_TurtleLake_Rock_GoblinRock02_SM` | 6% | 12.0-20.0 | Second rounded boulder |
| `AP_TurtleLake_Rock_LakeRock02_SM` | 5% | 6.0-12.0 | Damp-channel rock |
| `AP_TurtleLake_Rock_TurtleRock04_SM` | 5% | 7.0-14.0 | Flat scree/cover slab |
| `RF_Boulder1` | 2% | 5.0-9.0 | Mossy lower-slope rock |
| `RF_Boulder2` | 2% | 4.0-8.0 | Small scatter rock |
| `RF_Boulder3` | 2% | 5.0-10.0 | Broad low rock |
| `RF_Boulder5` | 2% | 6.0-11.0 | Pale lichen variation |
| `AP_AlaskaCedar_001_2` | 3% | 10.0-18.0 | Alaskan-inspired sentinel pine |
| `AP_Norway_Spruce_01` | 3% | 12.0-20.0 | Tall narrow spire |
| `AP_Tree_04_PTree_01_SM_2` | 2% | 8.0-14.0 | Sparse lower-slope pine |
| `AP_Tree_04_PTree_02_SM` | 2% | 9.0-15.0 | Damaged pine variation |
| `AP_Tree_04_PTree_03_SM` | 2% | 10.0-17.0 | Very sparse pine |
| `AP_Tree_04_PTree_04_SM` | 2% | 11.0-18.0 | Wind-shaped pine |
| `AP_Tree_04_PTree_05_SM` | 2% | 12.0-20.0 | Tallest sparse pine |
| `RF_Tree1` | 1% | 14.0-24.0 | Bare elevation-limit pine |
| `RF_Tree2` | 1% | 12.0-22.0 | Thin green pine |
| `RF_Tree3` | 1% | 12.0-22.0 | Pine variation |
| `AP_WhiteFir_MD_Dead_03` | 1% | 14.0-24.0 | Damaged high-elevation fir |
| `AP_Neutral_Tree_A_PrickelGrass01_SM` | 3% | 2.0-4.0 | Pale alpine scrub |
| `RF_Bush1` | 3% | 3.0-6.0 | Upright crack-grown shrub |
| `RF_Bush2` | 2% | 3.0-6.0 | Low sheltered bush |
| `AP_plant_001_14` | 2% | 2.5-5.0 | Dark alpine shrub |
| `AP_plant_001_18` | 2% | 2.5-5.0 | Coniferous ground spray |
| `Fern_02B` | 1% | 2.0-4.0 | Yellow fern in damp pocket |
| `AP_Neutral_Foliage_A_Weed01_SM` | 1% | 1.5-3.0 | Sparse grass in cracks |
| `AP_Plant_003_10` | 1% | 2.0-4.0 | Low fern-like variation |
| **Total** | **100%** |  |  |

### Placement and additions

- Reduce vegetation with elevation. Concentrate bushes and moss below rock overhangs, beside water, and at the lower forest transition.
- Use switchbacks, blind ledges, boulder chutes, and narrow gullies, but protect a 2.2-2.8 m walkable strip on every required route and allow occasional broad views back over the island. Any rock that forces climbing or squeezing must be authored, never a random scatter result.
- Add the mine entrance, scree and gravel materials, a major rockfall, abandoned mine equipment, a small water channel/cascade, and - only near the story entrance - the torch and skull totem. Keep the military/Japanese signage question unresolved until the history is clarified.

---

## Implementation rules that keep the dense world playable

1. **Cluster by species.** Real-looking density comes from same-species pockets with imperfect edges, not from evenly mixing every prefab.
2. **Keep procedural landmarks readable.** Because locations move, each reusable biome chunk needs one unmistakable silhouette and at least two approach angles.
3. **Author first-person corridors.** Dense forest can still have readable combat lanes; use trunks and brush to curve or shorten them rather than blocking movement randomly.
4. **Cap heroes aggressively.** Exorcist, Heretic, Monster, Gangshi, fountain, tower, telescope, and mine assets should be fixed or rule-capped.
5. **Validate after every scatter pass.** Rebuild or query the navigation data, test required start-to-goal connections, and reject/resample any generated colliders that close a protected corridor. The spacing ranges reduce bad placements but cannot prove connectivity.
6. **Use collision tiers.** Trees, major rocks, and deliberate deadfall are solid; small bushes should use simplified or soft collision; grass, flowers, mushrooms, and most ferns should not block the player.
7. **Use sound as a placement layer.** Attach localized wind, branch creak, water, distant yelling, or deceptive stereo cues to a few visual landmarks instead of broadcasting them uniformly.
8. **Preserve escalation.** Eerie Forest is ecological corruption; Heretic Forest adds authored human ritual; unmistakably supernatural treatment peaks during night shifts, hallucinations, and the fountain's protection event.
9. **Break texture repetition.** Apply small hue/value variation, wetness, scale variation, 90-degree-safe rotations, and rare damaged variants while protecting each biome's dominant palette.

## Source-informed fixed story elements

- **Beach:** the ruined dock is the island entry point.
- **Flak Tower:** WWII tower/base fragments remain a long-distance navigation landmark.
- **Fountain:** a stone well below the chapel, topped by Our Lady of Guadalupe; later protected by blue fire.
- **Glade:** telescope party site with bonfire, sleeping bags, scattered belongings, broken guitar, photograph, boots, and pickaxe.
- **Mountain:** mine entrance near/below the tower, with localized torch, skull totem, and discarded equipment.

These fixed story elements are not represented in the supplied screenshot/RF weights and therefore sit outside each biome's 100-point procedural composition budget.
