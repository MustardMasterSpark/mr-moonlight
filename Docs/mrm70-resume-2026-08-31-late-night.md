# MRM-70 — resume point, 2026-08-31 late night

> **Everything in this file is a Sept 1 deliverable pass, not the final vegetation system.**
> Carlos, end of session: the entire vegetation spawn (the 174 Gaia GameObject rules below, and the
> terrain-detail foliage layer) will very likely be redone from scratch later, **probably alongside
> a reshape of the island itself.** Don't over-invest in fine-tuning current spacing/density
> numbers — they're good enough to look right tomorrow, not a baseline worth preserving carefully.

Branch `mrm-70`. Supersedes `Docs/mrm70-resume-2026-08-31-night.md` for vegetation spawn state.
That doc's §2.1 slope-cap proposal (32°/30°/50° blanket fix) is now moot — see below.

---

## What happened this pass

ChatGPT returned `Docs/Design/Island-Terrain-Reference/Vibe/Biome_Collision_Distribution_v2.md`
(the "v2" distribution) with a deliberately sparser, "protected negative space" redesign, per-prefab
spacing/slope/scale instead of flat values, and an explicit "Manual" tier for hand-placed hero
props. Carlos: eliminate the old vegetation, rebuild rules from GPT's v2 tables, spawn.

**All 9 Gaia biome spawners were rebuilt from scratch** (Forest, AutumnForest, EerieForest,
HereticForest, Mountain, Beach, Glade, Fountain, FlakTower — **Glade included this time**,
reversing the earlier "Glade permanently excluded from GameObject spawning" rule, because v2 gives
Glade 5 real automatic tree rows, not just grass).

### Data source
- `Tools/vegetation/gpt_distribution_v2_auto.csv` — 150 automatic rows actually spawned this pass
  (biome, prefab, spacing min/max, max slope, scale min/max). Transcribed directly from the GPT doc.
- `Tools/vegetation/gpt_distribution_v2_manual.csv` — 24 "Manual"/hero rows **NOT auto-spawned**:
  23 Heretic Forest ritual/hero props (`AP_GangshiTree_2`, `AP_S_Tree_01`, the GraveKeepers/Curse/
  Heretic kit) + 1 Fountain guardian (`AP_Tree_Lake_RoundTree_01_SM`). GPT's doc is explicit these
  must be hand-composed per ritual node/sightline, never scatter-spawned — automating them would
  contradict the whole point of the redesign. **This is scene-placement work and needs Carlos's
  go-ahead** per the CLAUDE.md hard rule, same as any other hands-on Unity placement.
- `AP_Tree_Dry_N02` (Eerie deadfall + Beach driftwood in GPT's table) was **excluded** — GPT's own
  doc flags it as having no collider in the measured catalogue and says it needs one before
  enabling. Not spawned. Give it a collider first if it's wanted.

### Rebuild mechanics (for the next person touching this)
Each `Gaia.SpawnRule` (species) references its biome's `Gaia.ImageMask` by GUID
(`m_textureMaskSpawnRuleGUID`), not by object reference — that GUID points at the biome's
"Mask: <Biome>" rule (index 0 in each spawner, itself `m_isActive = False`, purely a mask
definition). Rebuilding species rules means: capture one existing rule as a template, `Gaia.
ImageMask.Clone()` its mask for every new rule, `Gaia.GaiaUtils.CopyFields()` + explicit overrides
for the rest (spacing, `SpawnCritera.m_maxSlope`, `ResourceProtoGameObjectInstance.m_minScale/
m_maxScale` with `m_commonScale=true, m_spawnScale=Random`, `m_rotateToSlope`). Same convention as
[[mrm70_tree_rotate_to_slope_fix]]: name contains "Rock"/"Boulder" → `rotateToSlope=true`, else
false. Scale is now genuinely randomized per-instance (was locked 1.00–1.00 before).

Prefabs resolved by exact filename match under `Assets/_Project/Art/VegetationPrefabs/` via
`AssetDatabase.FindAssets` — all 71 unique prefab names in the auto CSV resolved with zero misses.

### Density correction — one pass, per GPT's own formula
First spawn attempt (raw v2 spacing values) produced **926 instances**, badly skewed: Forest-family
canopy species landed fine, but Fountain/Glade/Beach/Heretic/Eerie came back near-zero. Root cause:
GPT's spacing assumed biome mask sizes it had no way to measure. Measured against the live terrain
(1024×1024m = 104.86 ha total, `TerrainData.GetAlphamaps`):

| Biome | Painted % of terrain | Actual ha |
|---|---:|---:|
| Forest | 35.84% | 37.58 |
| AutumnForest | 5.42% | 5.68 |
| Mountain | 4.92% | 5.16 |
| FlakTower | 3.62% | 3.80 |
| Beach | 3.37% | 3.53 |
| EerieForest | 0.94% | 0.99 |
| Glade | 0.88% | 0.92 |
| HereticForest | 0.76% | 0.80 |
| Fountain | 0.41% | 0.43 |
| (Path) | 0.14% | 0.15 |
| (Seafloor/no-spawn) | 43.71% | — |

Fountain's real painted area is 0.43 ha — GPT's 68–116m spacing assumed something much bigger.
Applied GPT's own documented correction (`newSpacing = oldSpacing × sqrt(actualDensity /
targetDensity)`), one multiplier per biome computed from actual-spawned vs. (target instances/ha ×
actual ha): Forest ×0.798, AutumnForest ×0.95, EerieForest ×0.929, HereticForest ×0.923,
Mountain ×0.893, Beach ×0.813, Glade ×1.0 (already on target), Fountain ×0.408, FlakTower ×0.659.
Re-ran clean + spawn once more.

### Result
**1,511 instances** (`Gaia Game Object Spawns` child count, the ground-truth source per
[[mrm70_generate_new_gaia_trigger]] — NOT the per-rule `m_spawnedInstances` sum, which read 2,683
and is over-counting, consistent with the prior note that field is unreliable). Down from
**21,683** before this pass — a ~93% reduction, which is exactly what "eliminate the crowding,
protect the negative space" was asking for. Console clean, no errors/warnings. Scene saved.

Full per-species breakdown is in the session transcript; broad shape: Forest's 11 canopy species
carry the bulk (81–173 instances each, ~37.6 ha of forest mask), everything else — sub-canopy,
fallen wood, rocks, the 8 smaller biomes — sits in the low single digits to low dozens, matching
GPT's "rare punctuation, not wallpaper" intent.

---

## Update — same session, after Carlos's feedback

Carlos: (1) wanted density much higher than the correction pass left it — settled on **+300%
(×4)** as a flat multiplier over the whole island; (2) asked whether GPT gave weight data for the
24 "Manual"/hero props, since he doesn't have budget for hand-authored ritual placement right now.

**Answer to (2):** yes — GPT gave every one of the 95 assets a spacing/exclusion-halo, max slope,
and scale range, including the 24 manual ones. The only thing missing was a numeric mix-share %
(deliberately excluded from GPT's automatic density math because it intended these as rare,
hand-composed landmarks). The exclusion-halo column works as Poisson spacing directly, so no need
to re-prompt ChatGPT. **All 24 were added as ordinary Gaia GameObject rules** (23 into
HereticForest, 1 into Fountain) using that halo as spacing — same mechanism as the automatic 150.
This means **all 95 catalogued assets are now spawning in-scene**, at the cost of GPT's intended
"one dominant silhouette per ritual clearing" hand-composed staging for Heretic Forest — it now
scatters like everything else. Documented as a deliberate scope trade-off, not an oversight.

**Applying ×4 density surfaced a real Gaia bug, now fixed.** After halving all 174 rules' spacing
and respawning, the result was 63,744 instances — not the expected ~6,044. Root cause: every
species rule's `ImageMask.m_active` had silently flipped to `false` (the same gotcha documented in
[[mrm70_gaia_spawn_attempt1_failed]] for Play Mode transitions — this confirms it *also* fires from
a scripted `AreaSpawn` call itself, not just Play Mode). With masks inactive, every species ignored
its biome's painted boundary and spawned across effectively the whole terrain. Fixed by forcing
`m_active = true` on all 174 masks immediately before every clean+respawn cycle — **this needs to
happen every single time from now on, not just once**, since it can reoccur after any spawn call.
Re-ran clean+respawn with the fix in place: **5,990 total instances** (ground truth = `Gaia Game
Object Spawns` hierarchy child count), ≈3.96× the pre-increase 1,511 — matches the requested ×4.
Per-biome truth (from each rule's own `m_spawnedInstances`, which is internally consistent for
relative comparison even though its absolute sum over-counts ~1.8x vs. the hierarchy ground truth):
Forest 8696, AutumnForest 909, EerieForest 148, HereticForest 136, Mountain 331, Beach 93,
Glade 8, Fountain 82, FlakTower 208 (these are the inflated per-rule figures, not final counts —
use them only for relative biome-to-biome proportion, not as the reported total).

Scene saved. Console clean (the "non-existent texture spawn rule" warnings seen mid-session were
from the broken 63,744-instance run and cleared once the fix was applied and re-verified — GUID
references were confirmed structurally correct throughout, matching each species rule's mask to
its biome's own "Mask: `<Biome>`" rule GUID with zero mismatches).

## Update — same session, terrain-detail foliage pass

Separately from the 174 GameObject rules above, Carlos asked for ground-cover foliage (grass/
flowers/ferns/mushrooms) for tomorrow's presentation. Full detail, exact parameters, and the
reversal snippet are in `Tools/vegetation/terrain_detail_demo_pass.md` — short version:

- All 72 prefabs in `Assets/_Project/Art/VegetationPrefabs/GRASS PREFABS/` registered as raw Unity
  `TerrainData.detailPrototypes` (mesh mode) and painted via `TerrainData.SetDetailLayer` —
  **this deliberately bypasses the Gaia SpawnRule system**, so it will NOT survive a future
  "generate new Gaia" clean+respawn cycle, and does not need to (see reversal note below).
- Scattered across every biome except Beach (and automatically excluded underwater).
- Forest + AutumnForest boosted much higher ("go crazy... cover all the terrain") — ~22%
  per-cell chance vs. ~2% elsewhere, since they're also the largest chunk of usable land.
- 23 flower-named prefabs get extra density specifically in Fountain + Glade.
- Final: 1,768,029 non-zero cells / 4,384,290 total instance-units across 72 layers.

**This is explicitly a one-step-reversible placeholder**, per Carlos: "we will improve this in the
future." The exact undo snippet (zero every detail layer, clear the prototype array) is in
`Tools/vegetation/terrain_detail_demo_pass.md` — running it returns the terrain to its actual
pre-pass state (confirmed 0 detail prototypes before this session).

## Still open

1. **24 manual/hero props are undeployed.** Needs Carlos's go-ahead for hands-on placement (per
   CLAUDE.md's Blender/Unity scene-work rule) — these are ritual-node composition, not a script job.
2. **AP_Tree_Dry_N02** needs a box collider added before it can join Eerie/Beach rules.
3. **Grass/detail tier has a placeholder, not the real thing.** The terrain-detail foliage pass
   above is raw scripted `TerrainData` painting for tomorrow's presentation only — it is NOT the
   `TerrainDetail` Gaia spawn rules [[mrm70_grass_detail_tier]] establishes as the actual target
   architecture (rules that survive "generate new Gaia"; this placeholder doesn't and isn't meant
   to). One-step reversible per `Tools/vegetation/terrain_detail_demo_pass.md`.
4. **Further density tuning is expected to continue** — this was one correction pass, not a final
   tune. GPT's doc's own "recommended first test pass" workflow (walk it in first person, adjust
   spacing per-species if one repeats too visibly) still applies. Do this with a real build per
   [[verification_requires_a_build]], not the editor.
5. **Route/clearance guards from GPT's doc are not enforced** — "keep primary routes 5-7m clear",
   the 20-30m fountain no-spawn radius, the 25-40m FlakTower halo, the mine apron, etc. are all
   art-direction guidance GPT gave, not something Gaia's spawner rules encode. If a route reads as
   blocked in a playtest, that's the next manual placement issue.
6. **The whole vegetation pass may be replaced wholesale, not just tuned.** Carlos, end of session:
   both layers here (174 Gaia GameObject rules + the foliage placeholder) will likely be redone
   from scratch, probably alongside a reshape of the island itself. Treat everything in this file
   as disposable groundwork for tomorrow's deliverable, not a system to carefully preserve.

## Superseded by this pass
- `Docs/mrm70-resume-2026-08-31-night.md` §2.1 (the flat 5-10° slope-cap problem and the proposed
  32°/30°/50° blanket fix) — moot, replaced by GPT's per-species slope table (12°-46° depending on
  role).
- §2.3 (scale locked 1.00-1.00) — fixed, every species now spawns with `SpawnScale.Random` across
  GPT's per-species range.
- The old "Glade permanently excluded from GameObject spawning" rule in
  [[mrm70_generate_new_gaia_trigger]] — Glade now has 5 real automatic tree rules and was spawned
  this pass. Update that memory's algorithm to include Glade going forward.
