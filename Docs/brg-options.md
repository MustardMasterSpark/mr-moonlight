# BRG options (AST-314 BRG Instanced Renderer) — what it offers, how we test it

MRM-88. Started 2026-10-30 (Opus session after build 60) in `09 BRG Trial`; **profile v1 decided and rolled out to every island scene the same day (§6).**
Handoff that started it: `Docs/brg-options-opus-prompt.txt`. Results go in §4 and in
`Docs/performance-sessions.md` (build entry + change record).

## 1. What was built (2026-10-30)

| Piece | Where | What it does |
|---|---|---|
| F11 bench | `Code/Runtime/DevTools/BRG/BRGOptionsBench.cs` (own asmdef `MrMoonlight.DevTools.BRG`), GameObject **BRG Options Bench** in scene 09 | Panel listing every option that can change while the game runs (33 rows). Switch one row at a time, teleport to 3 fixed spots, auto A/B sweep. Works in the editor **and in builds** |
| Setup menu | `Code/Editor/BRG/BRGTrialSetup.cs`, menu **Mr. Moonlight/BRG/Apply Groups And Baked Options** | Creates 4 Prototype Groups, puts a `BRGPrototypeExtraData` on every tree/rock instance (by species folder) and on every detail prefab root, then Revert → Convert, refreshes the terrain and saves scene 09 |
| Baked options | `Settings/BRG/BRGBakedOptions.asset` (+ `BRGGroup_Trees/DeadWood/Rocks/Details.asset`) | The options that need a re-convert. Edit the asset, re-run the menu. Defaults change nothing |

State after the first run: 7,731 trees → Trees, 854 → DeadWood (dead/break/fall/burnt/stump/broken/root/dry
folders), 201 → Rocks; 88 prototypes (unchanged count) = Trees 63, DeadWood 17, Rocks 8; 78 detail
prototypes → Details; 12 texture-grass layers have no prefab, so no group.
Scene 09 backup from before: session scratchpad `09 BRG Trial.before-groups.unity`.

**Changes outside scene 09:** the 78 terrain detail prefabs got a `BRGPrototypeExtraData` component on
their root. It does nothing outside BRG, so the Flora scenes (07/02/06/08) are not affected, but these are
shared prefab files (some are in git-ignored vendor folders, so git will not show them).

## 2. Using the bench

Play scene 09, press **F11**. All keys work only while the panel is open; none of them is bound by the game.

| Key | Action |
|---|---|
| PgUp / PgDn | select row |
| Home / End | previous / next value (applied live, logged `[BRGBENCH] SET`) |
| Delete | measure the current settings (1.5 s settle + 4 s) |
| Insert | **quick sweep** at the current spot: each row at its most aggressive value, baseline re-measured every 6 tests (~3 min) |
| Shift+Insert | full sweep (every value of every row, ~8 min) |
| Ctrl+Insert | quick sweep at all 3 spots, teleporting between them |
| Shift+PgUp / PgDn | teleport to previous / next spot |
| Shift+Home | save where you stand over the current spot (this session only, printed in the log) |
| Backspace | everything back to baseline · Esc cancels a sweep |

**Don't touch the mouse during a sweep.** Results: `[BRGBENCH]` lines in the SessionLog and
`%USERPROFILE%\AppData\LocalLow\Mustard Master Spark\MrMoonlight\Logs\brgbench-*.csv` (column `mode` = editor/build).
Compare editor with editor and build with build. Unity must be **focused**: an unfocused editor runs at ~11-20 fps.
On exit the bench restores the BRG config, the URP asset and the groups. These are assets, and play mode does not roll assets back.

Spots (from build 60's log): **a** dense forest (24,51,81) yaw 252 · **b** far view, the slowest
(-204,55,37) yaw 12, 141 fps in b60 · **c** open ground (50,51,87) yaw 271.

## 3. Inventory: everything BRG offers, and where each option is covered

**Live (bench rows):**
- Config: GPU occlusion (HZB) on/off, HZB tightness, HZB retest pass, HZB pixel expansion, shadow caster
  occlusion, HZB shadow segments, baked Umbra occlusion (has no effect until scene 09 is baked), skip intermediate
  LODs, shadow crossfade cascades.
- Camera: BRG LOD bias (`BRGCameraSettings.lodBiasMultiplier`, every prototype at once); exclude non-main
  cameras from BRG (`DamageNumbersOverlayCamera` is a second camera BRG may cull for).
- URP: shadow distance 90/60/45/30, cascades 4/2/1. *If one wins, it changes lighting: update `Docs/lighting.md` and the quality tunables.*
- **Prototype Groups (not in the original handoff list):** per group (Trees / DeadWood / Rocks / Details):
  shadows on/off, density (uniform random thinning), LOD bias, shadow caster distance cull, off-screen shadow cull.

**Baked (BRGBakedOptions asset → menu):** max shadow cascade, min shadow LOD, distance density
(RawDistance or ScreenSize, start/end/min fraction/fade), min screen size + crossfade. Group density on
the bench multiplies distance density.

**Not covered, and why:**
- `defaultSmallMeshSize`: applies only to prototypes **without** LODs. The trees use mesh LOD, so it never
  touched them; for details, use the Details `minScreenSize`.
- Mesh LOD screen sizes on 8,786 `BRGMeshLodData`: camera/group LOD bias scales the same thresholds live.
- Terrain streaming (`terrainMaxUploadsPerFrame`, async loading, detail chunk size): affects loading and
  hitches, not steady fps. Look at it only if the 1% low points to streaming.
- `useUnityTransforms` / `BRGTerrainDetailOverride`: an override can point a detail layer at a different
  prefab for BRG only. That is a cleaner fix for the 48 child-renderer detail prefabs (trap 1) than editing
  them. Look at it only if the 1% low shows GC from the slow path.
- `enablePerInstanceColor`: needs shader support (RetroLit doesn't read it). SpeedTree wind: our trees are not SpeedTree.
- `supportLegacyLightProbes`: lighting only, +128 B/instance; only matters if the island gets baked probes.
- `enableAutoCompaction`, pooling sizes: memory only.

## 4. Results

### 4.1 Editor sweep, 2026-10-30 (quick sweep, all 3 spots, `brgbench-20261030-175336.csv`)

Baselines held steady at each spot: forest 122-124, far view 100-101, open 108-110 fps, so noise is about ±2%.
**Caveats:** (1) in the editor the CPU column ≈ frame time and the GPU column mostly reads ~0: the editor is
CPU-bound and its GPU timing is not valid, so GPU-side savings (shadows, density) are **understated** here and
must be re-measured in a build. (2) The forest's first baseline (109, right after Play) was a warm-up reading, so
the forest's first 6 tests are compared against too low a baseline (shadow segments 16 "+11.5%" is that artefact;
against 123 it is nothing). Teleport settle raised from 4 s to 8 s.

| Option (most aggressive value) | Forest | Far view | Open | Visual cost | Verdict |
|---|---|---|---|---|---|
| **Exclude non-main cameras from BRG** | **+16%** | **+30%** | **+17%** | **none**: the only other camera, `DamageNumbersOverlayCamera` (overlay, child of PlayerCamera), renders only the `WorldOverlayFX` layer, so BRG was running a full cull for a camera that never draws foliage | **WINNER, applied 2026-10-30**: `BRGCameraSettings` (Exclude From Rendering) on scene 09's overlay camera. The camera is a scene-level object in 02/06/07/08 too, so add it there when those scenes switch to BRG |
| URP shadow cascades 1 | +29% | +7% | +6% | blurry, short-range shadows | no; maybe 2 (untested) |
| [Trees] shadows off | +32% | +9% | +8% | trees cast no shadows | no |
| [Trees] shadow caster distance 25 m | +13% | +8% | +7% | far trees lose shadows | **candidate**: try 40/60 m in a build |
| URP shadow distance 30 m | +6% | +4% | +6% | shorter shadows overall | weak |
| [DeadWood] shadows off | +7% | 0 | 0 | | weak |
| BRG LOD bias (camera) 0.35 | -1% | +14% | +2% | coarser LODs at distance | candidate (far view only) |
| [Details] density 0.25 | +1% | +17% | +2% | 1/4 of the grass/flowers | candidate: try 0.5-0.75 |
| [Details] LOD bias 0.5 | +1% | +11% | +2% | | candidate |
| [Trees] density / LOD bias | ~0 | +7 / +8% | +2 / +3% | | weak |
| HZB tightness Aggressive / pixel expansion 1 | (warm-up) | +8% / +8% | +2% / +2% | risk of things wrongly culled (flicker on fast turns) | candidate, check by eye |
| GPU occlusion (HZB) OFF | -23% | -14% | -22% | | keep ON |
| HZB retest pass ON | -4% | +1% | -4% | | keep OFF |
| Umbra off (no bake), skip intermediate LODs, shadow crossfade, off-screen shadow cull, shadow caster occlusion, all Rocks rows, DeadWood except shadows | within noise | | | | no effect |

## 5. Performance profile v1 (2026-10-30, applied to scene 09, build 61)

Chosen from the §4.1 sweep, performance first, stopping short of changes that are obviously visible. Carlos judges
the look in build 61 and says what to undo. The bench now reads its baselines from the scene, so the profile is
"baseline" (white [brackets]) and Backspace returns to it. To A/B an item in game, move its row back to the old value.

| Setting | Was | Profile v1 | Why |
|---|---|---|---|
| Overlay camera `BRGCameraSettings` Exclude From Rendering | off | **on** | +16/+30/+17%, invisible |
| BRGIRConfig `hzbTightness` | Tight | **Aggressive** | +8% far view; watch for objects flickering at screen edges on fast turns |
| PlayerCamera `BRGCameraSettings.lodBiasMultiplier` | (none = 1) | **0.6** | 0.35 gave +14% far view; 0.6 = coarser LODs a bit nearer |
| Groups Trees / DeadWood / Rocks: shadow caster distance | off | **40 m** | 25 m gave +7-13%; 40 m keeps shadows around the player |
| Baked Trees + DeadWood `minShadowLOD` | 0 | **1** | shadows drawn from LOD1 (cheaper mesh). Not measured alone |
| Baked Details distance density (RawDistance) | off | **100% to 30 m → 35% at 100 m** | Details density 0.25 gave +17% far view; this thins only the far grass/flowers |
| Baked Details `minScreenSize` | 0 (config 0.5%) | **1%** | like Details LOD bias 0.5 (+11% far view); small far details drop sooner |
| Not changed | | | URP shadows (90 m / 4 cascades: lighting + quality presets own them; 1 cascade = +29% in forest, test 2 on the bench), Trees density (weak), HZB pixel expansion, retest, Umbra |

Rollback: set the values in the "Was" column (baked ones in `Settings/BRG/BRGBakedOptions.asset`, then the Apply menu).
The detail `maxShadowDistance` on BRGGroup_Details was reset to 80 (the sweep had left 25 while off; no effect).

Panel cost fix: the panel text is cached and drawn only on Repaint; during a sweep it shows only the progress.
The panel opening/closing is logged (`[BRGBENCH] PANEL open/closed`) so [PERF] windows with it on screen can be spotted.

**Build 61** `E:\Builds\61 - BRG Profile - 2026-10-30` (scene 09 only, 486 MB zip). Compare with build 60: 200.6 fps first
50 s, 1% low 141.8, far view 141. Also open question: whether the setup itself (extra data on every tree,
`_detectPrefabOverrides` on) costs anything — editor spawn readings dropped (208-254 → ~177) but at different angles; build 61
vs build 60 at the same spots settles it.

## 6. DECIDED and rolled out (2026-10-30, after build 61)

Carlos: "lock this decision… replace Flora with these settings… this is the way to go." Profile v1 is now the setup in
**every island scene: 02 Island, 06 Island_Legion, 07 LightingTestScene, 08 LightingTestScene Fog Experiment** (and 09).
Done with the new menu **Mr. Moonlight/BRG/Convert Open Scene From Flora** (same file as the setup menu), read back per scene:

| Scene | Flora removed | Trees/rocks | Prototypes (Trees / DeadWood / Rocks) | Umbra bake kept |
|---|---|---|---|---|
| 07 | 8,786 + provider + settings | 8,786 | 88 (63 / 17 / 8) | yes |
| 02 | 5,990 + provider + settings | 5,990 | 89 (65 / 17 / 7) | yes |
| 06 | 8,786 + provider + settings | 8,786 | 88 | yes |
| 08 | 8,786 + provider + settings | 8,786 | 88 | yes |

All four: detail prototypes in the Details group (78, plus 12 texture-grass layers with no group), `useUnityTransforms` on,
overlay camera excluded, player camera LOD bias 0.6, F11 bench object present, no console errors. Screenshots: 07 renders
under BRG; 08 renders under BRG with its VF2 fog off. **With VF2 on, scene 08 shows a full-screen weave.** That is VF2's own
known artifact (the runtime profile `VF2_Legion_Fog` has `jittering` 0.5; `fog-experiment-ast282.md`: weave = Jittering, set 0),
not BRG. Left for the fog session.

**Not yet verified in a build:** 02, 06, 08 (same recipe as 09/07; scenes 02 and 06 are in Build Settings, so the next game build
is the check). Scenes 02 and 06 have enemies/fights: BRG has not been played with combat yet.

**Things that live outside git** (AST-314 is in git-ignored `Assets/ThirdParty/`): `BRGIRConfig.asset` holds
`hzbTightness Aggressive` (and defaultSmallMeshSize 0.5, allowTextureGrass on). A fresh clone needs AST-314 reinstalled and those set.

**New Gaia spawn from now on:** run the convert menu (not the old `FloraInstanceRendererPass`), then re-bake Umbra.
