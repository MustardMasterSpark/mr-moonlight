# Aanniarvik Island — collision-prefab distribution v2

This is the rebuilt distribution for the 95 large, collision-bearing prefabs in `vegetation-distribution-brief.md`. It is an art-directed starting point for Gaia, based on the measured prefab dimensions and the current biome screenshots. Grass, flowers, ferns, mushrooms used as terrain detail, and other small non-colliding foliage are intentionally absent.

The central correction is not simply “fewer objects.” Each biome now has a hierarchy: common structural forms, uncommon silhouette breakers, rare landmarks, and protected emptiness. The player should feel lost because the world is composed into walls, rooms, corridors, and distant screens—not because every available square metre contains a collider.

## How to use the numbers

- **Mix share** is the target percentage of that biome's automatically placed collidable instances. It is an art-direction target, not a second Gaia density control. Shares total 100% within each automatic table; rows marked **Manual** are excluded from that total.
- **Spacing** is per-prefab Poisson spacing. It is deliberately much wider than the old settings because Gaia places every prefab in an independent pass.
- **Max slope** has been opened enough to use the island's dominant 10–20° hillsides. Wide-rooted landmarks and ritual props retain lower caps so they sit cleanly.
- **Scale** is uniform XYZ scale. The largest 30–42 m trees use restrained ranges; small trees, rocks, and debris can vary more.
- **Structural coverage** is a final visual QA target: the approximate terrain share occupied by the projected footprint or exclusion halo of large collidable forms after overlap pruning. **Negative space** is the terrain that should remain free of these forms. Grass and detail foliage may still cover it.

For a quick density estimate, use the midpoint of the spacing range:

```text
prefab instances/ha ≈ 10,000 / spacingMidpoint²
biome instances/ha ≈ sum of all automatic prefab estimates
```

After a test spawn, correct a spacing range without knowing the island or biome area:

```text
newSpacing = oldSpacing × sqrt(actualDensity / targetDensity)
```

The same multiplier applies to both ends of the range. Because Gaia's passes do not avoid other prefab species, negative space must be protected by a shared exclusion mask, mutually exclusive palette masks, or a cross-species pruning pass; it cannot be created reliably by a “negative-space weight.”

### Global movement and composition guards

- Keep primary routes **5–7 m clear**, secondary passages **3.5–4.5 m clear**, and combat loops **6–10 m clear**.
- Measure route clearance from the outer collider surface, then add **1.25 m**: `route buffer = collider radius × max scale + 1.25 m`.
- Place a **12–25 m light well every 40–60 m** in the three dense forest biomes. Autumn openings can be wider and brighter.
- Divide large biome masks into **40–100 m palette regions**. A local region should normally activate only 4–7 upright-tree variants, 1–2 debris variants, and 1–2 rock variants, even when the global biome palette is larger.
- Never allow a long fallen trunk, broad Heretic root, or hero tree to spawn across a protected route.

## Biome targets at a glance

| Biome | Structural coverage | Protected negative space | Estimated automatic instances/ha |
|---|---:|---:|---:|
| Forest | 43% | 57% | 48–68 |
| Autumn Forest | 32% | 68% | 36–46 |
| Eerie Forest | 34% | 66% | 34–46 |
| Heretic Forest | 24% | 76% | 24–32, plus authored ritual props |
| Mountain | 12% | 88% | 12–22 |
| Beach | 5% | 95% | 4–8 |
| Glade | 2% | 98% | 1–3 |
| Fountain | 16% | 84% | 10–18, plus 0.2–0.4 hero trees/ha |
| Flak Tower | 9% | 91% | 8–15 |

---

## Forest

**Gameplay purpose:** a “First Blood” forest that compresses sightlines and disorients the player while retaining winding movement lanes and periodic light pockets.

**Composition:** This is a conifer cathedral, not a uniform tree carpet. Giant spruce/fir forms establish the ceiling; damaged pines and one restrained broadleaf family interrupt the repeated spear-like crown; low thickets tighten selected sides of a route; fallen wood makes individual spaces memorable. Compose 4–9-tree masses against 6–10 m serpentine corridors. Every 40–60 m, open the canopy into a 12–20 m breathing room so sunlight and landmarks can reach the player.

Use three rotating conifer palettes across the mask rather than exposing every giant-tree variant in every view. `AP_Tree_04_GTree01_06_SM` and the long fallen logs require route exclusion because their horizontal forms can become accidental walls.

| Prefab | Mix share | Spacing min–max (m) | Max slope (°) | Scale min–max | Role |
|---|---:|---:|---:|---:|---|
| `AP_Tree_04_M01_03_SM` | 5% | 50–70 | 34 | 0.82–1.06 | emergent canopy; skyline anchor |
| `AP_Tree_04_GTree01_04_SM` | 6% | 44–62 | 34 | 0.84–1.08 | mature canopy |
| `AP_Tree_04_GTree01_05_SM` | 7% | 40–56 | 36 | 0.82–1.10 | broken-crown canopy variation |
| `AP_Tree_04_M01_02_SM` | 5% | 48–68 | 35 | 0.84–1.08 | tall sparse canopy |
| `AP_Tree_04_GTree01_03_SM` | 6% | 44–60 | 36 | 0.84–1.10 | tall canopy variation |
| `AP_Tree_04_GTree01_01_SM_2` | 5% | 50–70 | 34 | 0.85–1.07 | dense mature canopy |
| `AP_Tree_04_GTree01_02_SM` | 6% | 44–62 | 35 | 0.84–1.10 | dense canopy alternate |
| `AP_Tree_Conifir_A_01_SM_2` | 7% | 40–56 | 36 | 0.82–1.12 | primary dark conifer |
| `AP_Tree_Conifir_A_02_SM` | 7% | 42–58 | 36 | 0.82–1.12 | primary dark conifer alternate |
| `AP_Tree_04_M01_01_SM_2` | 4% | 56–78 | 34 | 0.85–1.08 | large canopy anchor |
| `AP_Tree_04_GTree01_06_SM` | 2% | 85–120 | 24 | 0.88–1.08 | leaning/fallen canopy event; route exclusion |
| `AP_M6_Tree_ForestTree08_SM_JYI_2` | 5% | 52–72 | 30 | 0.82–1.15 | rare living broadleaf pocket |
| `AP_Tree_04_M01_04_SM` | 4% | 56–78 | 36 | 0.80–1.12 | mid-height sparse conifer |
| `AP_Tree_04_M01_05_SM` | 4% | 58–80 | 34 | 0.82–1.10 | asymmetrical conifer |
| `AP_Tree_04_PTree_03_SM` | 2% | 80–112 | 38 | 0.80–1.16 | damaged pine silhouette |
| `AP_Tree_04_PTree_01_SM_2` | 2% | 82–116 | 38 | 0.80–1.16 | broken-crown pine silhouette |
| `AP_Tree_04_PTree_04_SM` | 1% | 115–165 | 36 | 0.78–1.14 | rare high-crown landmark pine |
| `AP_Tree_04_PTree_02_SM` | 2% | 82–116 | 38 | 0.80–1.16 | sparse pine variation |
| `AP_WhiteFir_MD_Dead_03` | 1% | 115–160 | 38 | 0.82–1.18 | damaged fir transition accent |
| `AP_M6_Tree_Bushtree01_SM` | 4% | 55–76 | 28 | 0.80–1.15 | dense side-screen thicket |
| `AP_M6_Tree_Bushtree02_SM` | 3% | 68–94 | 26 | 0.80–1.15 | wide edge thicket; never on route centre |
| `AP_TurtleLake_Tree_Stump02_SM` | 2% | 82–118 | 26 | 0.80–1.25 | rooted stump landmark |
| `AP_Tree_Break_Root_02_SM` | 2% | 88–124 | 24 | 0.80–1.25 | low stump/root clutter |
| `AP_Tree_Break_03_SM` | 2% | 90–128 | 22 | 0.85–1.18 | long fallen barrier; route exclusion |
| `AP_Tree_Break_02_SM` | 2% | 88–124 | 24 | 0.85–1.20 | fallen log with living trace |
| `AP_Tree_Break_MushroomTrunk_01_SM` | 2% | 84–120 | 24 | 0.78–1.25 | memorable mossy stump |
| `AP_TurtleLake_Tree_BrokenTree01_SM` | 2% | 90–128 | 24 | 0.82–1.22 | pale fallen wood accent |

---

## Autumn Forest

**Gameplay purpose:** a calm, Halloween-like visual respite made of warm woodland rooms, clean sunlight, and darker conifer seams that keep the island identity intact.

**Composition:** The present version forms an almost continuous repeated-tree ceiling. Replace it with alternating orange groves, pale-trunk groves, open leaf-floor rooms, and narrow pine screens. Warm deciduous trees remain dominant, but roughly one quarter of the upright-tree mix is coniferous so the biome still belongs to the same Alaska-inspired island. Preserve 18–30 m sunlit openings; do not distribute every colour evenly. A grove should read as a deliberate colour statement, followed by a quieter transition.

Fallen wood is rare but necessary: it keeps the biome from reading like a manicured park while remaining calm and traversable.

| Prefab | Mix share | Spacing min–max (m) | Max slope (°) | Scale min–max | Role |
|---|---:|---:|---:|---:|---|
| `AP_FallTree_01_SM_1` | 11% | 38–50 | 28 | 0.86–1.08 | primary orange/yellow canopy |
| `AP_FallTree_01_SM` | 8% | 44–60 | 30 | 0.84–1.12 | early-autumn green counterpoint |
| `AP_Tree_Blackpoplar01_SM` | 4% | 64–88 | 28 | 0.86–1.10 | tall cool-green contrast |
| `AP_Tree_AUT_White_A_02_SM` | 5% | 56–78 | 30 | 0.84–1.14 | pale autumn grove |
| `AP_Tree_AUT_White_A_03_SM` | 4% | 62–86 | 32 | 0.82–1.16 | sparse pale silhouette |
| `AP_Tree_Oak01_SM` | 3% | 76–104 | 28 | 0.88–1.10 | old park-like anchor |
| `AP_FallTree_02_SM` | 8% | 44–60 | 32 | 0.82–1.16 | bright yellow/orange accent |
| `AP_Tree_color_001_01_2` | 7% | 48–66 | 30 | 0.82–1.16 | low warm canopy mass |
| `AP_Tree_color_001_03` | 6% | 52–72 | 32 | 0.80–1.18 | olive/yellow transition tree |
| `AP_ENV_tree_ToeMunJean` | 4% | 62–86 | 30 | 0.80–1.18 | compact orange-brown mid-storey |
| `AP_ENV_tree_SaGeeSukRim` | 4% | 64–88 | 30 | 0.80–1.18 | compact yellow-brown mid-storey |
| `AP_Plant_003_07` | 3% | 76–104 | 32 | 0.78–1.20 | slender young-tree accent |
| `AP_Tree_Conifir_A_01_SM_2` | 6% | 52–72 | 34 | 0.84–1.12 | dark conifer screen |
| `AP_Tree_Conifir_A_02_SM` | 6% | 52–72 | 34 | 0.84–1.12 | dark conifer screen alternate |
| `AP_Norway_Spruce_01` | 4% | 64–88 | 36 | 0.80–1.18 | narrow Alaska-like spruce |
| `AP_BC_PineTree_02` | 3% | 76–104 | 34 | 0.80–1.18 | open asymmetrical pine |
| `AP_BC_PineTree_03` | 3% | 78–108 | 34 | 0.80–1.18 | exposed-root pine accent |
| `AP_AlaskaCedar_001_2` | 3% | 76–104 | 36 | 0.78–1.20 | wind-broken cedar accent |
| `AP_Tree_Break_Root_02_SM` | 2% | 90–126 | 24 | 0.82–1.20 | low root/stump detail |
| `AP_Tree_Break_02_SM` | 3% | 78–108 | 22 | 0.86–1.16 | rare fallen log |
| `AP_Tree_Break_MushroomTrunk_01_SM` | 3% | 76–104 | 24 | 0.80–1.24 | mossy stump punctuation |

---

## Eerie Forest

**Gameplay purpose:** oppressive, skeletal traversal with readable ambush lanes—frightening through silhouette and uncertainty rather than an impenetrable black wall.

**Composition:** Build asymmetrical dead-tree thickets around 12–20 m voids. Tall burnt snags and damaged firs provide vertical rhythm; wide horizontal dead trees close selected views; fallen wood creates occasional decisions at ground level. The GraveKeeper silhouettes are rare punctuation, not the background texture. Two living conifer variants appear at only 1% each so a surviving green tree feels lonely and wrong.

Keep eye-level exits readable. The current biome makes every tree equally monstrous and overlaps their branches into a single dark mass; the revised spacing makes the rare forms legible and lets thin daylight leak between them.

| Prefab | Mix share | Spacing min–max (m) | Max slope (°) | Scale min–max | Role |
|---|---:|---:|---:|---:|---|
| `AP_Tree_Burnt_04_SM` | 7% | 50–68 | 38 | 0.84–1.14 | tall burnt snag |
| `AP_Tree_WNT_M01_01_SM_3` | 7% | 52–72 | 32 | 0.86–1.12 | fine-branched dead canopy |
| `AP_Tree_WNT_03_Bark_01_SM_2` | 9% | 44–60 | 36 | 0.82–1.16 | narrow dead-tree rhythm |
| `AP_Tree_WNT_M_03_SM` | 7% | 52–72 | 34 | 0.84–1.14 | dense skeletal canopy |
| `AP_Tree_Deadtree06_SM` | 7% | 50–68 | 32 | 0.82–1.16 | pale twisted focal tree |
| `AP_WhiteFir_MD_Dead_03` | 9% | 44–60 | 38 | 0.80–1.18 | damaged fir transition |
| `AP_Tree_DeadTree01_SM` | 6% | 58–78 | 28 | 0.86–1.10 | wide skeletal side wall |
| `AP_DeadTree02` | 7% | 56–76 | 30 | 0.84–1.12 | upright forked dead tree |
| `AP_DeadTree03` | 6% | 58–80 | 28 | 0.84–1.12 | horizontal gnarled silhouette |
| `AP_DeadTree04` | 7% | 54–74 | 30 | 0.84–1.14 | horizontal dead-tree alternate |
| `AP_GraveKeepers_B07` | 2% | 96–132 | 26 | 0.90–1.08 | rare unnatural landmark |
| `AP_GraveKeepers_B06` | 2% | 96–132 | 24 | 0.90–1.08 | rare broad graveyard silhouette |
| `AP_Tree_Dry_D01` | 5% | 60–82 | 34 | 0.82–1.18 | broken dry snag |
| `AP_Tree_Dry_N02` | 4% | 68–94 | 24 | 0.82–1.18 | fallen bare branch; add simple box collider first |
| `AP_TurtleLake_Tree_Stump02_SM` | 3% | 78–108 | 26 | 0.80–1.24 | rooted dead stump |
| `AP_Tree_Break_Root_02_SM` | 2% | 92–126 | 24 | 0.80–1.24 | low dead root clutter |
| `AP_Tree_Break_03_SM` | 2% | 98–136 | 20 | 0.86–1.16 | long fallen barrier; route exclusion |
| `AP_Tree_Break_02_SM` | 2% | 94–130 | 22 | 0.86–1.18 | dark fallen log |
| `AP_Tree_Break_MushroomTrunk_01_SM` | 2% | 94–130 | 22 | 0.80–1.22 | decayed stump punctuation |
| `AP_TurtleLake_Tree_BrokenTree01_SM` | 2% | 96–132 | 22 | 0.82–1.22 | pale deadfall accent |
| `AP_AlaskaCedar_001_2` | 1% | 140–190 | 38 | 0.82–1.16 | very rare living survivor |
| `AP_BC_PineTree_03` | 1% | 140–190 | 36 | 0.82–1.16 | very rare living survivor |

`AP_Tree_Dry_N02` is the one catalogue exception: the brief reports no collider. It is included to honour the full 95-prefab palette, but it should receive a simple low box collider before this rule is enabled.

---

## Heretic Forest

**Gameplay purpose:** a ritual escalation biome built around authored horror reveals: constriction, ceremonial clearing, singular grotesque landmark, then release.

**Composition:** The ordinary Eerie palette becomes a restrained procedural envelope. Inside it, ritual nodes are composed by hand from roots, stakes, grave markers, cairns, and one dominant tree. This preserves the distinction between “nature died here” and “someone did this.” Keep 12–25 m empty halos around the major silhouettes and never show more than two dominant hero trees at once.

The current rows of identical dead trees read like an orchard. The new foundation is sparse and asymmetric; every occult asset is authored. Across the whole Heretic biome, use every manual prefab at least once, but distribute them between different ritual nodes instead of building one catalogue display.

### Automatic dead-forest foundation

| Prefab | Mix share | Spacing min–max (m) | Max slope (°) | Scale min–max | Role |
|---|---:|---:|---:|---:|---|
| `AP_Tree_Burnt_04_SM` | 12% | 45–62 | 36 | 0.84–1.12 | burnt vertical frame |
| `AP_Tree_WNT_M01_01_SM_3` | 10% | 50–68 | 32 | 0.86–1.10 | skeletal canopy |
| `AP_Tree_WNT_03_Bark_01_SM_2` | 15% | 40–56 | 36 | 0.82–1.14 | common narrow dead-tree rhythm |
| `AP_Tree_WNT_M_03_SM` | 10% | 50–68 | 34 | 0.84–1.12 | dense skeletal screen |
| `AP_Tree_Deadtree06_SM` | 12% | 45–62 | 32 | 0.82–1.14 | pale twisted transition tree |
| `AP_Tree_DeadTree01_SM` | 9% | 52–72 | 28 | 0.86–1.08 | wide dead side wall |
| `AP_DeadTree02` | 10% | 50–68 | 30 | 0.84–1.10 | forked dead-tree frame |
| `AP_DeadTree03` | 8% | 56–76 | 28 | 0.84–1.10 | horizontal gnarled silhouette |
| `AP_DeadTree04` | 9% | 52–72 | 30 | 0.84–1.12 | horizontal dead-tree alternate |
| `AP_Tree_Dry_D01` | 5% | 70–96 | 34 | 0.82–1.16 | broken snag punctuation |

### Hand-placed ritual and hero kit

For these rows, **spacing is the recommended exclusion halo between repeated uses**, not a request to run a full-biome scatter pass. “Manual” items do not participate in the automatic mix-share total. The heavy GraveKeeper and Curse trees should usually appear only once or a few times across the complete biome.

| Prefab | Mix share | Spacing / exclusion halo (m) | Max slope (°) | Scale min–max | Role |
|---|---:|---:|---:|---:|---|
| `AP_GangshiTree_2` | Manual | 180–260 | 18 | 0.90–1.04 | primary talisman-tree hero |
| `AP_S_Tree_01` | Manual | 180–260 | 18 | 0.90–1.05 | giant horror landmark |
| `AP_GraveKeepers_B03_2` | Manual | 130–190 | 22 | 0.90–1.06 | tall graveyard guardian |
| `AP_GraveKeepers_B01` | Manual | 130–190 | 22 | 0.90–1.06 | wide graveyard guardian |
| `AP_Building_exorcist_tree2` | Manual | 140–220 | 18 | 0.92–1.08 | primary exorcist-tree reveal |
| `AP_GraveKeepers_B02` | Manual | 140–220 | 20 | 0.92–1.06 | expensive upright guardian |
| `AP_Tree_Curse_J07` | Manual | 110–170 | 26 | 0.88–1.10 | thin cursed silhouette |
| `AP_GraveKeepers_B06` | Manual | 130–190 | 24 | 0.90–1.08 | broad graveyard silhouette |
| `AP_M6_Tree_MonsterTreeBark_SM_PHJ_2` | Manual | 110–170 | 24 | 0.90–1.10 | intertwined monster-tree tower |
| `AP_Tree_Curse_J08` | Manual | 105–160 | 26 | 0.88–1.12 | broken cursed fork |
| `AP_Tree_Curse_H01_2` | Manual | 170–250 | 18 | 0.94–1.04 | primary root-wall horror hero |
| `AP_Tree_Curse_K01` | Manual | 130–200 | 22 | 0.92–1.08 | thick secondary cursed hero |
| `AP_Tree_Heretic_A01_2` | Manual | 100–150 | 24 | 0.88–1.12 | fleshy ritual-tree hero |
| `AP_GraveKeepers_B04` | Manual | 90–140 | 26 | 0.90–1.10 | narrow foreground ritual accent |
| `AP_Tree_Heretic_B03` | Manual | 60–100 | 14 | 0.90–1.10 | stake barrier; authored choke only |
| `AP_sunghwangdang_Tree_pagoda_01` | Manual | 80–120 | 14 | 0.90–1.10 | tagged shrine/cairn node |
| `AP_Tree_Heretic_B05` | Manual | 50–85 | 14 | 0.88–1.12 | skull-stake punctuation |
| `AP_GraveKeepers_C02` | Manual | 65–100 | 18 | 0.85–1.15 | standing grave-rock marker |
| `AP_sunghwangdang_Tree_pagoda_01_1` | Manual | 75–110 | 14 | 0.90–1.10 | secondary plain shrine/cairn |
| `AP_Tree_Heretic_D03_02` | Manual | 90–140 | 12 | 0.86–1.14 | side root barrier; route exclusion |
| `AP_Tree_Heretic_D03` | Manual | 100–160 | 12 | 0.88–1.12 | major root barrier; route exclusion |
| `AP_GraveKeepers_C01` | Manual | 60–95 | 18 | 0.85–1.15 | low grave-rock marker |
| `AP_Tree_Heretic_D02_01` | Manual | 90–140 | 12 | 0.86–1.14 | root-arch threshold; route exclusion |

Budget **1–3 authored ritual props/ha**, but only **0.2–0.5 dominant hero trees/ha**. Small props may cluster at a node; the major trees may not. Leave the ceremonial centre empty enough for combat, reading the ritual, and seeing the hero silhouette from a distance.

---

## Mountain

**Gameplay purpose:** exposed vertical traversal and a lower-tension mine approach, with clear terrain reading and occasional sheltered pines.

**Composition:** The current mountain is a near-continuous pile of repeated boulders. The revised version keeps soil and scree visible, then groups rocks into intermittent slide fans, ledges, and separated outcrops. Each 60–100 m macro-region should select one large-rock type and one or two small-rock types; do not enable all eight rock passes in the same local patch. Sparse pines belong on benches, cracks, and sheltered shoulders—not uniformly across every slope.

Protect a **15–25 m rock-free apron** in front of the mine entrance, and keep the main uphill route visibly traceable from below.

| Prefab | Mix share | Spacing min–max (m) | Max slope (°) | Scale min–max | Role |
|---|---:|---:|---:|---:|---|
| `AP_M6_Rock_SeashoreWallStone01_SM` | 6% | 86–120 | 38 | 0.78–1.18 | rare vertical outcrop anchor |
| `AP_M6_Rock_CemeteryRock02_SM` | 8% | 74–100 | 42 | 0.80–1.22 | stepped outcrop anchor |
| `AP_TurtleLake_Rock_GoblinRock02_SM` | 10% | 66–90 | 44 | 0.76–1.24 | rounded medium boulder |
| `AP_TurtleLake_Rock_GoblinRock01_SM` | 8% | 74–100 | 44 | 0.76–1.24 | character boulder accent |
| `AP_M6_Rock_FieldStoneStone06_SM` | 10% | 66–90 | 46 | 0.70–1.32 | small blocky rock rhythm |
| `AP_TurtleLake_Rock_TurtleRock04_SM` | 6% | 88–124 | 38 | 0.80–1.12 | rare flat landmark shelf |
| `AP_TurtleLake_Rock_LakeRock02_SM` | 9% | 70–94 | 46 | 0.70–1.34 | small irregular scree accent |
| `AP_M6_Rock_FieldStoneStone05_SM` | 8% | 74–100 | 46 | 0.70–1.34 | low fieldstone accent |
| `AP_Tree_04_PTree_03_SM` | 4% | 105–145 | 38 | 0.78–1.16 | sparse shoulder pine |
| `AP_Tree_04_PTree_01_SM_2` | 4% | 105–145 | 38 | 0.78–1.16 | broken-crown shoulder pine |
| `AP_Tree_04_PTree_04_SM` | 2% | 145–205 | 38 | 0.80–1.14 | solitary high-crown pine |
| `AP_Tree_04_PTree_02_SM` | 4% | 105–145 | 38 | 0.78–1.16 | sparse pine alternate |
| `AP_Tree_04_PTree_05_SM` | 3% | 120–165 | 38 | 0.78–1.18 | thin skyline pine |
| `AP_Norway_Spruce_01` | 3% | 120–165 | 36 | 0.78–1.16 | narrow sheltered spruce |
| `AP_BC_PineTree_02` | 3% | 120–165 | 36 | 0.78–1.16 | asymmetrical sheltered pine |
| `AP_BC_PineTree_03` | 3% | 120–165 | 36 | 0.78–1.16 | exposed-root pine |
| `AP_AlaskaCedar_001_2` | 2% | 145–205 | 38 | 0.76–1.18 | wind-shaped cedar landmark |
| `AP_TurtleLake_Tree_Stump02_SM` | 2% | 145–205 | 32 | 0.78–1.24 | storm-killed stump |
| `AP_Tree_Break_Root_02_SM` | 1% | 205–285 | 30 | 0.78–1.24 | very rare exposed root base |
| `AP_TurtleLake_Tree_BrokenTree01_SM` | 4% | 105–145 | 28 | 0.80–1.24 | pale fallen timber accent |

---

## Beach

**Gameplay purpose:** exposed navigation where the player feels visible, framed by long empty sand runs and a few believable tide-line deposits.

**Composition:** The current repeated small rocks read as evenly spaced dots. Replace them with short, irregular rock pockets and driftwood strand lines separated by **40–80 m stretches of untouched sand**. A large rock should lead a pocket; one or two smaller shapes may support it. Trees occur only near the inland transition or as a deliberately lonely exception—never as a beach woodland.

Tree variants are globally available but must be mutually exclusive by shoreline segment. A connected beach should normally show zero to two pines in total, not one instance of every rare-pine rule.

| Prefab | Mix share | Spacing min–max (m) | Max slope (°) | Scale min–max | Role |
|---|---:|---:|---:|---:|---|
| `AP_M6_Rock_SeashoreWallStone01_SM` | 8% | 125–165 | 20 | 0.82–1.18 | vertical rock-pocket anchor |
| `AP_TurtleLake_Rock_GoblinRock02_SM` | 10% | 110–150 | 18 | 0.78–1.24 | rounded shore boulder |
| `AP_TurtleLake_Rock_GoblinRock01_SM` | 8% | 125–165 | 18 | 0.78–1.24 | character shore boulder |
| `AP_M6_Rock_FieldStoneStone06_SM` | 10% | 110–150 | 18 | 0.70–1.32 | blocky pocket support rock |
| `AP_TurtleLake_Rock_TurtleRock04_SM` | 7% | 132–180 | 16 | 0.82–1.12 | rare flat shelf landmark |
| `AP_TurtleLake_Rock_LakeRock02_SM` | 9% | 115–158 | 18 | 0.70–1.34 | wet-edge stone accent |
| `AP_M6_Rock_FieldStoneStone05_SM` | 8% | 125–165 | 18 | 0.70–1.34 | low fieldstone support |
| `AP_TurtleLake_Tree_Stump02_SM` | 6% | 145–195 | 12 | 0.82–1.20 | rooted drift stump |
| `AP_Tree_Break_03_SM` | 8% | 125–170 | 12 | 0.86–1.18 | long storm-cast log |
| `AP_Tree_Break_02_SM` | 8% | 125–170 | 12 | 0.86–1.20 | drift log with living trace |
| `AP_TurtleLake_Tree_BrokenTree01_SM` | 8% | 125–170 | 14 | 0.80–1.25 | pale driftwood |
| `AP_Tree_Dry_N02` | 4% | 175–235 | 12 | 0.82–1.18 | small dead branch strand; collider required |
| `AP_Tree_04_PTree_01_SM_2` | 2% | 250–340 | 18 | 0.82–1.12 | exceptionally rare damaged pine |
| `AP_BC_PineTree_02` | 2% | 250–340 | 18 | 0.82–1.14 | exceptionally rare shore pine |
| `AP_BC_PineTree_03` | 1% | 340–480 | 18 | 0.82–1.14 | singular exposed-root pine |
| `AP_AlaskaCedar_001_2` | 1% | 340–480 | 20 | 0.80–1.16 | singular wind-bent cedar |

---

## Glade

**Gameplay purpose:** a vulnerable, desolate horizon and pacing reset where the player can finally see—and be seen.

**Composition:** Keep the hill almost entirely open. The continuous dark perimeter in the current screenshot makes the space feel like a bowl, so break that rim into distant masses and preserve two or three broad entrances plus a generous sky silhouette. Inside the biome, use one wind-bent tree on a crest or an offset pair near an edge. The ground-detail system will provide the grass and flowers; this collision layer should barely announce itself.

Because five independent rare passes can still land near one another, divide the glade into large mutually exclusive cells and select **one tree variant per cell**. Do not allow every row to run across the same small connected glade.

| Prefab | Mix share | Spacing min–max (m) | Max slope (°) | Scale min–max | Role |
|---|---:|---:|---:|---:|---|
| `AP_Tree_04_PTree_04_SM` | 25% | 130–190 | 30 | 0.82–1.12 | lone high-crown silhouette |
| `AP_Tree_04_PTree_05_SM` | 20% | 140–205 | 30 | 0.80–1.16 | sparse ridge pine |
| `AP_Norway_Spruce_01` | 20% | 135–195 | 32 | 0.82–1.16 | narrow isolated spruce |
| `AP_AlaskaCedar_001_2` | 20% | 135–195 | 34 | 0.80–1.18 | wind-broken edge cedar |
| `AP_Plant_003_07` | 15% | 155–220 | 28 | 0.78–1.20 | rare young-tree accent |

---

## Fountain

**Gameplay purpose:** a mystical sanctuary and decompression space whose calm central field remains readable while strange tree curtains conceal the outer world.

**Composition:** The current giant overlapping crowns overwhelm the location. Keep a **20–30 m hard no-spawn radius** around the fountain or central basin and a **6–8 m circular movement loop** outside it. Place the automatic palette only in an irregular perimeter-grove mask, arranged as three to five unequal curtain groups. Preserve two or three sightline wedges that carry light and guide the player toward exits.

One `AP_Tree_Lake_RoundTree_01_SM` becomes a deliberately isolated guardian silhouette. The smaller Argass, Juniper, and Nokmyung forms provide cover at the perimeter rather than consuming the centre.

### Automatic perimeter palette

| Prefab | Mix share | Spacing min–max (m) | Max slope (°) | Scale min–max | Role |
|---|---:|---:|---:|---:|---|
| `AP_Tree_10_ArgassTree_04_SM` | 9% | 76–102 | 24 | 0.86–1.12 | low lopsided curtain tree |
| `AP_Tree_10_ArgassTree_03_SM` | 9% | 76–102 | 26 | 0.84–1.14 | twisted perimeter cover |
| `AP_Tree_Juniper03_SMIK` | 11% | 68–92 | 28 | 0.84–1.14 | pale-trunk mystical cover |
| `AP_Tree_Juniper02_SMIK` | 11% | 68–92 | 28 | 0.82–1.16 | gnarled green cover |
| `AP_Tree_10_ArgassTree_02_SM` | 9% | 76–102 | 28 | 0.82–1.16 | medium twisted screen |
| `AP_Tree_10_ArgassTree_SM` | 7% | 86–116 | 28 | 0.82–1.16 | low gnarled cover tree |
| `AP_ENV_tree_Nokmyung` | 11% | 68–92 | 30 | 0.78–1.20 | lush umbrella-canopy cover |
| `AP_Plant_003_07` | 9% | 76–102 | 28 | 0.78–1.20 | luminous young-tree accent |
| `AP_Tree_Oak01_SM` | 7% | 86–116 | 24 | 0.88–1.10 | calm old-tree counterpoint |
| `AP_M6_Tree_Bushtree01_SM` | 4% | 115–155 | 24 | 0.82–1.16 | dense outer-rim thicket |
| `AP_M6_Tree_Bushtree02_SM` | 3% | 130–180 | 22 | 0.84–1.14 | wide outer-rim thicket |
| `AP_TurtleLake_Rock_GoblinRock02_SM` | 2% | 160–220 | 24 | 0.80–1.20 | natural basin-edge boulder |
| `AP_TurtleLake_Rock_GoblinRock01_SM` | 2% | 160–220 | 24 | 0.80–1.20 | character basin-edge boulder |
| `AP_TurtleLake_Rock_TurtleRock04_SM` | 2% | 170–230 | 18 | 0.84–1.10 | rare flat seating/altar rock |
| `AP_TurtleLake_Rock_LakeRock02_SM` | 2% | 160–220 | 24 | 0.74–1.28 | small wet-edge stone |
| `AP_Tree_Break_MushroomTrunk_01_SM` | 2% | 160–220 | 22 | 0.82–1.22 | mossy perimeter stump |

### Fountain hero

| Prefab | Mix share | Spacing / exclusion halo (m) | Max slope (°) | Scale min–max | Role |
|---|---:|---:|---:|---:|---|
| `AP_Tree_Lake_RoundTree_01_SM` | Manual | 100–160 | 20 | 0.90–1.08 | isolated curled guardian landmark |

Use approximately one guardian for every two to five hectares of Fountain mask, or simply one per major fountain space. It must sit outside the central loop and receive its own shaft of sky.

---

## Flak Tower

**Gameplay purpose:** an open flower meadow and combat-readable landmark space where the tower remains visible and isolated trees cast long directional shadows.

**Composition:** The current biome reads as another crowded woodland. Move most collidable trees into broken edge islands and preserve **20–40 m open approach bands** toward the tower. Dark pines form roughly half the sparse upright mix; autumn trees supply warm colour without closing the ceiling. Keep a 25–40 m no-tree halo around the tower and cut several gaps through the perimeter so the meadow connects visually to the larger island.

The flowers and low foliage will come from the detail layer. These collidable trees should be read as individuals or small groups, never a continuous stand.

| Prefab | Mix share | Spacing min–max (m) | Max slope (°) | Scale min–max | Role |
|---|---:|---:|---:|---:|---|
| `AP_FallTree_01_SM_1` | 8% | 88–118 | 26 | 0.86–1.08 | isolated orange shadow caster |
| `AP_FallTree_01_SM` | 7% | 92–126 | 26 | 0.86–1.10 | early-autumn shadow caster |
| `AP_Tree_AUT_White_A_02_SM` | 5% | 110–150 | 28 | 0.86–1.10 | pale edge-tree accent |
| `AP_Tree_AUT_White_A_03_SM` | 4% | 120–168 | 28 | 0.84–1.12 | sparse pale silhouette |
| `AP_FallTree_02_SM` | 6% | 100–138 | 28 | 0.84–1.12 | bright yellow edge accent |
| `AP_Tree_color_001_01_2` | 5% | 110–150 | 28 | 0.84–1.14 | warm low canopy accent |
| `AP_Tree_color_001_03` | 4% | 120–168 | 28 | 0.84–1.14 | olive autumn transition |
| `AP_Norway_Spruce_01` | 12% | 70–96 | 32 | 0.82–1.16 | primary narrow pine shadow caster |
| `AP_BC_PineTree_02` | 10% | 78–104 | 32 | 0.82–1.16 | asymmetrical pine anchor |
| `AP_BC_PineTree_03` | 8% | 88–118 | 32 | 0.82–1.16 | exposed-root pine accent |
| `AP_AlaskaCedar_001_2` | 10% | 78–104 | 34 | 0.80–1.18 | wind-broken pine/cedar anchor |
| `AP_Tree_04_PTree_05_SM` | 8% | 88–118 | 32 | 0.80–1.16 | high sparse pine silhouette |
| `AP_ENV_tree_ToeMunJean` | 4% | 120–168 | 28 | 0.80–1.18 | compact autumn edge tree |
| `AP_ENV_tree_SaGeeSukRim` | 4% | 120–168 | 28 | 0.80–1.18 | compact brown/yellow accent |
| `AP_Plant_003_07` | 5% | 110–150 | 30 | 0.78–1.20 | occasional young tree |

---

## Prefab-use audit

**Unused prefabs: none.** Every one of the 95 prefab names in the supplied MD catalogue appears in at least one biome above.

There is one technical prerequisite: `AP_Tree_Dry_N02` has no collider according to the measured catalogue. It is intentionally assigned as Eerie deadfall and Beach driftwood, but it should receive a simple low box collider before enabling those rules. That keeps the artistic commitment to use the full palette without silently violating the collision-layer purpose.

### Hero and landmark prefabs

These assets should be used only a handful of times or as carefully isolated procedural landmarks:

| Biome | Prefabs | Treatment |
|---|---|---|
| Heretic Forest | `AP_GangshiTree_2`, `AP_S_Tree_01`, `AP_GraveKeepers_B03_2`, `AP_GraveKeepers_B01`, `AP_Building_exorcist_tree2`, `AP_GraveKeepers_B02`, `AP_Tree_Curse_J07`, `AP_GraveKeepers_B06`, `AP_M6_Tree_MonsterTreeBark_SM_PHJ_2`, `AP_Tree_Curse_J08`, `AP_Tree_Curse_H01_2`, `AP_Tree_Curse_K01`, `AP_Tree_Heretic_A01_2`, `AP_GraveKeepers_B04` | hand-place; one dominant silhouette per ritual clearing |
| Heretic Forest set pieces | `AP_Tree_Heretic_B03`, `AP_sunghwangdang_Tree_pagoda_01`, `AP_Tree_Heretic_B05`, `AP_GraveKeepers_C02`, `AP_sunghwangdang_Tree_pagoda_01_1`, `AP_Tree_Heretic_D03_02`, `AP_Tree_Heretic_D03`, `AP_GraveKeepers_C01`, `AP_Tree_Heretic_D02_01` | compose as authored thresholds, shrines, and side barriers |
| Fountain | `AP_Tree_Lake_RoundTree_01_SM` | hand-place outside the central loop with a clear sky silhouette |
| Forest | `AP_Tree_04_M01_03_SM`, `AP_Tree_04_GTree01_06_SM`, `AP_Tree_04_PTree_04_SM` | rare emergent, leaning event, and high-crown navigation silhouettes |
| Eerie Forest | `AP_GraveKeepers_B07`, `AP_GraveKeepers_B06` | procedural only at very low frequency; never overlap their crowns |
| Mountain | `AP_M6_Rock_SeashoreWallStone01_SM`, `AP_TurtleLake_Rock_TurtleRock04_SM` | isolate as outcrop or shelf anchors; do not tile |
| Beach | `AP_TurtleLake_Rock_TurtleRock04_SM`, any assigned pine | use as a singular shore event, preferably near the inland transition |

## Recommended first test pass

1. Paint the shared route, clearing, tower, fountain, mine-apron, and shoreline negative-space masks before spawning anything.
2. Hand-place the Heretic and Fountain hero kits first; their sightlines define the spaces around them.
3. Spawn one biome at a time and record actual automatic instances per hectare plus the per-prefab counts.
4. Apply `newSpacing = oldSpacing × sqrt(actualDensity / targetDensity)` to the complete range when a biome misses its target.
5. Check the mix shares. If one tree repeats too visibly, adjust only that prefab's spacing using the same formula rather than adding another species pass.
6. Walk every biome at eye height with collision enabled. An overhead image can approve canopy rhythm, but only the first-person pass can approve movement, threat readability, and whether sunlight reaches the ground.

The desired final rhythm is **compression → glimpse → release**. Dense vegetation should conceal what lies twenty metres ahead, while the protected openings ensure the player can still move, fight, orient, and occasionally see the sky.
