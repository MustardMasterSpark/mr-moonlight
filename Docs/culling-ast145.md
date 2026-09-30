# AST-145 Advanced Culling System 2 (New Game Studio, v1.5.8): install, baseline and results

Linear: **MRM-85** (Performance reports), by Carlos's ruling 2026-09-30. Branch `mrm-86` (uncommitted work).
Change-record rows: `Docs/performance-sessions.md` section 8 (C-010 and C-011). Rollback: `Docs/culling-rollback.ps1`.

## 1. What the asset does and why DYNAMIC

The manual recommends Static Culling (baked per camera cell) for small/medium scenes and **Dynamic Culling
(no bake of visibility, Burst/Jobs raycasts from the camera) for large scenes**. The island is large, so Dynamic.

- Rays (default 1500/frame, R2 distribution) are cast from the camera against a hidden collider per source
  (layer `ACSCulling`). A source not hit for `ObjectsLifetime` seconds (default 2) is switched off.
- `KeepShadows` culls the main-pass draw but keeps the shadow caster; `FullDisable` turns the renderer off.
- `MergeInGroups` + `CellSize` (10) treat neighbours as one group (fewer rays needed, coarser decisions).
- It only touches what it is given (MeshRenderer / LODGroup / custom). **It cannot cull Flora-rendered vegetation**
  (instanced, not GameObjects), terrain, water or particles.

## 2. What was installed (2026-09-30)

- Files: `Assets/ThirdParty/AST-145/` = `Core` (76 scripts + 2 asmdefs) + `Docs/Manual.pdf`. Tutorials skipped (demo bloat).
  Git-ignored like all of ThirdParty: **a fresh clone will not have it** (see the ThirdParty policy). Source zip:
  `Documents\Asset Collection\01_DOWNLOAD\AST-145.zip`. Burst/Collections/Mathematics already present; no package change.
  Legacy `Input.*` check: none in the runtime scripts. Compile clean.
- **Scene: `Assets/_Project/Scenes/LightingTestSceneCullingTest.unity`**, a copy of `LightingTestScene` (original untouched,
  byte-identical). In the copy only:
  - `Dynamic Culling` GameObject with `DC_Controller` (ID 0, lifetime 2, MergeInGroups on, cell 10, default method KeepShadows);
  - `DC_Camera` (1500 rays) on `PlayerCamera` only (the overlay camera draws damage numbers only);
  - `DC_SourceSettings` (KeepShadows) on all **8,786** tree MeshRenderers under `Gaia Terrains` (88 unique meshes, all static,
    none readable) and a **baked** `DC_Collider` child on each (MeshCollider of the render mesh, layer 29 `ACSCulling`).
    Baking was mandatory: runtime-created colliders need readable meshes, and changing 88 model imports was not worth it.
- Layer `ACSCulling` = layer 29 created; collision ignored against every layer. Tracked files this changed:
  `ProjectSettings/TagManager.asset`, `DynamicsManager.asset`, `Physics2DSettings.asset`.
- Facts about our setup that matter: GPU Resident Drawer **off**, SRP Batcher on, dynamic batching off, **static batching ON**
  (Standalone), trees are static. Play Mode check: at the glade 775 of 8,786 trees were visible and 8,011 culled (KeepShadows).

## 3. Risks to watch in the culling build (what Carlos should look at)

1. **Pop-in / blinking trees** through gaps in canopies or when turning fast (2 s lifetime, group merge of 10 m cells).
2. **Canopy as occluder**: the collider is the full mesh including leaf cards, so foliage blocks rays.
3. **Terrain does not occlude** (no `DC_Occluder` added): hills hide nothing.
4. **Queries against layer 29**: `IgnoreLayerCollision` does not stop raycasts. Bullets, enemy line of sight, ground checks and
   footsteps with masks that include layer 29 could now hit the full-mesh hulls (bullets stopping at leaf cards?). Test by shooting.
5. **Static batching**: tree meshes may be combined in the build; disabling one renderer of a batch can leave artifacts.
6. **Tree fires** pick trees by distance and may sit on a culled tree (fire visible, tree hidden).
7. **KeepShadows** saves the main pass only; shadow draws remain. `FullDisable` saves more but shadows may pop.
8. Extra cost: 8,786 more GameObjects and colliders in the scene (scene file 37.8 MB -> 69.7 MB) and ray jobs every frame.

## 4. Baseline (build 38, no culling), 2026-09-30, Carlos's run

Build 38 = commit `8ecee2b4` + SessionLog v5 + `DisplayBootSettings` (vSync off). Scene LightingTestScene, Windows build,
1920x1080 exclusive fullscreen, Ryzen 7 9800X3D, RX 9070 XT, vSync 0, cap -1 (verified in the log header). 285 s session,
log `session-20260930-152234.log`. (Build 37 was locked to 75 fps by vSync, see section 6.)

**Same spot, standing still at "Story 6 - Fountain" (the clean comparison):**

| State | fps avg | 1% low | frame ms | GPU ms | draws | SetPass | tris |
|---|---|---|---|---|---|---|---|
| Fires OFF (t 54-79 s, 6 windows) | **118.1** | 116 | 8.5 | 8.2 | 19,570 | 335 | 89.2 M |
| Fires ON, 60 burning / 10 real lights (t 89-114 s) | **89.8** | 88 | 11.1 | 10.9 | 19,800 | 486 | 89.2 M |

- **Tree fires cost 28 fps (-24%), +2.6 ms/frame, at this spot.** Draw calls and triangles are unchanged; SetPass +150 and GPU time
  +2.7 ms, so the cost is the fires' particles, overdraw and 10 real lights, not geometry. (First measurement of fire size 1.2,
  light x3, medium base fire; the prompt called this the "before" for the culling work.)
- Frame is GPU-bound (CPU ms ~= GPU ms in every window).

**Walking the circuit (first 50 s, fires off, no enemies):** 85 to 208 fps. **Draw calls swing with view direction, not position:**
2.9k (looking away from forest) to 31.6k (looking into the forest), tris 34 M to 123 M. The Story 3/4 views (31k draws) ran 85-89 fps.
This is the frustum-culling-only cost, and it is exactly what occlusion culling targets.

**Enemies + invincibility + walking and killing (t 119-285 s, fires ON):** fps 58 to 122, settling ~60 fps. 100 corpses by t 280,
SetPass 490 -> 1,780, alive 10-25. Matches H1 (corpses pile up, SetPass climbs). Worst frames: 151.8 ms when the enemies were
switched on (t 119), 96 ms at t 134, 48 ms when the fires were switched on.

The log flooded with 675 `[WORLD] weather` lines during the circuit blend: a SessionLog v5 bug (fixed, see C-011 / section 7).

## 5. Culling build (build 39) and results (Carlos's run, 2026-09-30, log `session-20260930-153715.log`)

Build 39 `E:\Builds\39 - Culling Test - 2026-09-30`: scene `LightingTestSceneCullingTest` only, same code as build 38 plus the log flood fix.
Same vsync 0 / cap -1, same hardware. 280 s, 27,126 frames, avg 97.5 fps, 1% low 35.5 fps, 100 kills (AKM), worst frame 51.5 ms.
No culling-related warnings or errors; the warning set equals build 38's (38 vs 40 convex-mesh warnings); logger cost unchanged (1-2 us/frame).

**A. Same spot, standing still at the Fountain** (build 38 at 420,25,-61; build 39 at 423,24,-58, about 3 m apart; same weather, still):

| State | build 38 (no culling) | build 39 (Dynamic, KeepShadows) | change |
|---|---|---|---|
| Fires OFF | 118.1 fps, 8.5 ms, 19,570 draws, 335 SetPass, 89.2 M tris | **136.3 fps, 7.3 ms, 5,070 draws, 284 SetPass, 78.1 M tris** | **+15% fps, -1.2 ms, draws -74%, tris -12%** |
| Fires ON (60) | 89.8 fps, 11.1 ms, 19,800 draws, 486 SetPass | **99.6 fps, 10.0 ms, 5,150 draws, 453 SetPass, 77.9 M tris** | **+11% fps, -1.1 ms, draws -74%** |

- Culling saves about **1.1-1.2 ms in both states**; the fires still cost the same 2.7 ms (136.3 -> 99.6 fps here vs 118.1 -> 89.8).
- Draws fell 74% but frame time only 14%: the frame is still GPU-bound by something other than the trees. 78 M triangles remain
  (trees are ~3-4k tris each), so most of the triangle load is not these 8,786 trees. Next measurement: where do the 78 M come from (Frame Debugger / per-object tris).

**B. The same 6-window circuit at the start (fires off, no enemies)** (positions within about 6-20 m of each other, timing not identical):

| Story | build 38 fps (draws) | build 39 fps (draws) |
|---|---|---|
| 1 Campsite | 123.7 (12.3k) | 132.5 (6.6k) |
| 2 Moving to glade | 208.0 (2.9k) | 201.8 (3.0k) |
| 3 Glade | 144.3 (11.6k) | 175.6 (3.5k) |
| 3-4 (worst forest view) | 89.4 (31.6k) | **136.4 (5.2k)** |
| 4 Moving to cabin | 85.6 (31.4k) | **166.9 (5.1k)** |
| 6 Fountain arriving | 198.3 (4.8k) | 186.5 (2.9k) |

The forest views that cost 31k draws and 85-89 fps now cost 5k draws and 136-167 fps (**+53% to +95%**). Open views (Story 2, arrival at the
fountain) are unchanged: nothing to cull. **This is where the asset pays off.** The draw-call swing with view direction (2.9k-31.6k) is gone (max 6.6k).

**C. Roaming and killing: NOT comparable.** Build 38's fight was on a plain; build 39's was in a tight, dense forest (biomes Forest / Path /
AutumnForest). Build 39: 33-110 fps, 18.5k draws and 100 M tris at the worst windows (33-44 fps with 60 fires, 20 alive, 37-100 corpses, SetPass 1.7-2.3k),
but also 103-110 fps at 5-6k draws in the same forest. Build 38 (plain): 58-122 fps, ~60 at 100 corpses. H2 (dense forest costs a lot) and H1 (corpses) both
show here and cannot be separated from culling without a fixed-spot A/B. Combat stayed GPU-bound (CPU ms ~= GPU ms).

**D. Visual / gameplay checks (pop-in, shots into canopies, enemy sight, fires on culled trees): pending Carlos's answers** (recorded in section 8 when given).

### Verdict so far
Dynamic Culling with KeepShadows is a clear win for forest views from open ground (up to +95% fps, -80% draws), a modest win at a fixed
open spot (+11-15%), and unproven in a dense forest under combat. No errors, no logger cost. Keep it in the test scene; decide on Island/Island_Legion
only after the visual checks and a fixed-spot forest A/B.

### Next measurements
1. **Fixed dense-forest benchmark spot**, no combat, 30 s, fires off, in build 38 vs build 39 (same coordinates and view). Decides the dense-forest question.
2. `FullDisable` variant (more savings, shadow pop risk) vs `KeepShadows`.
3. Where the remaining ~78 M tris come from.
4. Terrain as a `DC_Occluder` (hills hiding trees), ObjectsLifetime 2 -> 1 s, rays 1500 -> lower: CPU vs pop-in trade.

## 6. Side finding: 75 fps was vSync, not a cap

Polymind `GraphicsOptions.Apply()` forces `vSyncCount = 1` at every boot; our override lived only in MainMenu's `SettingsPanel`
(MRM-78), so any other start scene ran at the monitor's 75 Hz. Fixed with `Assets/_Project/Code/Runtime/Data/DisplayBootSettings.cs`
(BeforeSceneLoad: saved vSync choice, default off, and `targetFrameRate = -1`). No scene or prefab uses Gaia `FrameRateManager` (60)
or AST-079 `FramerateLimiter` (30). `[PERF]` lines now carry `vsync N cap N`. Touches MRM-78's area, flagged in the MRM-85 record.

## 7. Alternative worth testing later

GPU Resident Drawer with GPU occlusion culling (Unity 6, URP asset setting, currently off) targets the same draw-call cost without
colliders or rays. Not tested. It interacts with Flora, HAZE and custom shaders (RetroLit), so it is a separate experiment.

## 9. Dense-forest A/B/C test (builds 40, 41, 42), prepared 2026-09-30 night

Carlos's answers to the visual checks on build 39: no blinking trees, nothing odd, enemies behaved the same, fires only seen normally.
Bullets against leaves could not be verified by eye (still open, test item 4 in section 3).

**New start for all three:** Carlos's paused Play Mode spot. Player (218.06, 33.37, -5.23), yaw 311.3. Dense forest: 8-9 colliders within 6 m,
terrain Y ~33, NavMesh present, inside the HAZE explorable area. The weather circuit moved there too (same 22 m square, same order; corner 0 is 2 m behind
the player, as in the old layout): Corner 0 (218.06, 33.56, -7.23), Corner 1 (218.06, 30.94, 14.77), Corner 2 (240.06, 29.00, 14.77), Corner 3 (240.06, 32.14, -7.23),
each on the terrain. Old values (builds 37-39): player (403.60, 23.10, -74.20), circuit root (414.60, 22.91, -65.20). Verified by reading every saved scene back.

| Build | Folder | Scene | Culling |
|---|---|---|---|
| A (40) | `E:\Builds\40 - Forest A - 2026-09-30` | `LightingTestScene` | none (build 38's parameters) |
| B (41) | `E:\Builds\41 - Forest B - 2026-09-30` | `LightingTestSceneCullingTest` | Dynamic, KeepShadows (build 39's parameters) |
| C (42) | `E:\Builds\42 - Forest C - 2026-09-30` | `LightingTestSceneCullingTestFull` (copy of B) | Dynamic, FullDisable, all 8,786 sources and the controller default |

Each zip is 473 MB, verified (218 entries, no backslashes, no zero-byte files, exe at root). Same code in all three (SessionLog v5.1, DisplayBootSettings).

**Test plan (same for A, B and C, so the logs compare):** spawn (do not move for ~10 s), walk the circuit through the weathers, stand still at the Fountain weather
30 s with fires off, then 30 s with fires on, then AKM + infinite ammo + vulnerability and fight. Comparable numbers: the spawn still window, the Fountain still
windows (fires off / on), and the fight (same area now). Watch for shadows popping in C (FullDisable).

## 10. Dense-forest A/B/C results (Carlos's runs, 2026-09-30 night)

Logs: A `session-20260930-160202.log` (the later of two A runs), B `session-20260930-160731.log`, C `session-20260930-161241.log`. Same new start
(218, 35, -5), vsync 0 cap -1, same machine. Each: spawn still, circuit, Fountain still (corner 2, 240,31,15) fires off then on, AKM fight to 100 kills.
Averages are mean frame time per phase; "still" phases use windows at an unchanged position.

| Phase | A no culling | B Dynamic KeepShadows | C Dynamic FullDisable |
|---|---|---|---|
| **Spawn still** (same view) | 111.7 fps, 8.95 ms, 13,752 draws, 364 SetPass, 89.8 M tris | **126.1 fps (+13%)**, 7.93 ms, 4,724 draws, 230 SP, 74.7 M | **132.9 fps (+19%)**, 7.53 ms, 3,155 draws, 227 SP, 72.1 M |
| **Circuit** (walking, fires off, no enemies) | 113.9 fps, 13,990 draws, 91.3 M | 129.1 fps (+13%), 5,010 draws, 77.2 M | 132.7 fps (+17%), 4,136 draws, 75.5 M |
| **Fountain still, fires off** | 107.3 fps, 9.32 ms, 23,685 draws, 352 SP, 106.3 M | **140.6 fps (+31%)**, 7.11 ms, 3,318 draws, 242 SP, 78.4 M | **145.1 fps (+35%)**, 6.89 ms, 3,123 draws, 249 SP, 72.9 M |
| **Fountain still, fires on** | 80.5 fps, 12.42 ms, 23,796 draws, 529 SP, 105.2 M | not reliable (view changed while standing: tris 39-73 M) | **109.7 fps (+36%)**, 9.11 ms, 2,295 draws, 412 SP, 48.6 M |
| Whole session avg / 1% low / worst frame | 81.0 fps / 32.1 / 130.6 ms | 95.4 / 29.3 / 52.2 ms | 96.8 / 33.0 / 327 ms (Alt-Tab, see below) |

- **Culling gains are larger in the dense forest than in build 39's glade (+15%)**: +13-19% at the spawn, +31-35% at the Fountain, draws -66% to -87%.
- **FullDisable (C) is slightly better than KeepShadows (B)**: +3-5% fps and ~5% fewer triangles at the same spots. Fires cost 3.1 ms in A (9.32 -> 12.42) and **2.2 ms in C** (6.89 -> 9.11):
  fewer drawn trees also means less geometry lit by the 10 fire lights. Nobody reported shadow pop-in in C yet (question open).
- C's two 300+ ms "worst frames" (t=113 and t=118) are **Alt-Tab**: the log shows `window LOST focus` at t=112.4 and `display changed ... Windowed`. Not a culling problem.
  (A's 130 ms at t=48 is a weather transition; B's 52 ms is the fire toggle.)
- B's fires-on windows are unusable: the player stood still but looked around (triangles 39 M to 73 M at one position). Yaw is not in the log (idea: add it).

### The fight: corpses dominate, and culling cannot fix that
Fight windows grouped by corpse count (enemies alive, all builds; the three fights happened in **different spots**: A near the spawn, B to the south-west (175-187, 37-47), C to the north (217-230, z -16 to -40)):

| Corpses | A fps (SetPass) | B fps (SetPass) | C fps (SetPass) |
|---|---|---|---|
| 0-9 | 93.6 (605) | 102.6 (590) | 103.4 (591) |
| 10-29 | 76.9 (877) | 92.9 (679) | 90.6 (768) |
| 30-59 | 43.9 (1,723) | 39.3 (1,872) | 55.1 (1,604) |
| 60-100 | 37.9 (2,034) | 34.2 (2,115) | 41.6 (2,006) |

- **In every build the frame falls from ~9.7 ms at 0 corpses to 24-29 ms at 60-100 corpses, and SetPass rises from ~600 to ~2,000.** SetPass is the same in A, B and C at a
  given corpse count, so the corpses' cost is independent of culling. This is H1 again and it is **the biggest cost in the game**: at 100 corpses the culling gain (+10-20% at
  low corpse counts) is swamped and all three builds sit at 34-42 fps.
- With corpses 30+, A/B/C differ by spot, not by culling (B's spot was the worst). No reliable culling effect can be read from the fight.

### Verdict
Dynamic Culling is a clear, safe win for the dense forest (no pop-in seen in build 39, no errors, no extra logger cost): **C (FullDisable) > B (KeepShadows) > A**.
Adopt C if the shadows look right; otherwise B. The next big win is **not culling but the corpses** (H1/H7): no-shadow or simplified corpse renderers, a cap or cleanup,
fewer materials per corpse. Then the fires (2.2-3.1 ms) and the remaining ~70 M triangles that are not these trees.

## 11. Decisions and cautions at the end of the 2026-09-30 session

**Decision (Carlos):** he saw nothing wrong in build C (FullDisable); prefer C, keep **B (KeepShadows) as the documented fallback** if shadow problems ever show up
(Island/Island_Legion have different light and a different forest). Switching back is one edit: `DC_Controller.DefaultCullingMethod` plus every `DC_SourceSettings`
strategy's `CullingMethod` (`GetStrategy<DC_RendererSourceSettingsStrategy>().CullingMethod`), or start from the B scene.

**B vs C in one paragraph.** Both are the same Dynamic Culling system: rays from the camera are cast against a hidden collider on every tree, and a tree that no ray hits for
`ObjectsLifetime` (2 s) counts as hidden. They differ only in what "hidden" does to the tree. **B (KeepShadows):** the tree is set to *shadows only*: not drawn in colour, but it
still casts its shadow. **C (FullDisable):** the renderer is switched off completely: no colour and no shadow, which also removes the shadow-pass draw. C is faster (+3-5%);
its one risk is a hidden tree whose shadow falls on visible ground (that shadow would vanish or pop).

**Cautions about committing.** `Assets/ThirdParty/**` is git-ignored, so the AST-145 scripts are not in a clone: the two culling scenes would show missing scripts there.
The two scenes are ~66.5 MB each (GitHub warns at 50 MB per file, blocks at 100 MB). The rebuild recipe (layer, controller, camera, sources, bake) is in
`Docs/mrm85-culling-corpses-sonnet-prompt.txt` section 2, so the scenes can stay local if Carlos prefers.

**Next sessions:** (1) wire the chosen method into the real scenes, finish the open tests (bullets vs leaves), tune, and optimize the corpses (what stays active after death:
see the prompt, section 4); (2) swap HAZE for Volumetric Fog & Mist 2; (3) find the best combination.

## 12. LOCKED 2026-09-30: C (FullDisable) in every forest scene, and corpses get culled too

**Decision (Carlos, 2026-09-30): build C is the way forward.** Applied to every scene that has a Gaia forest, using the rebuild recipe
(controller, camera, sources, bake) and read back from disk afterwards:

| Scene | Tree sources | Read-back |
|---|---|---|
| `Island` | 5,990 | FullDisable 5,990, baked 5,990, layer-29 colliders 5,990, controller default FullDisable, lifetime 2, camera PlayerCamera |
| `Island_Legion` | 8,786 | same, all 8,786 |
| `LightingTestScene` | 8,786 | same, all 8,786 (the start spot and circuit are unchanged) |

`Sandbox`, `MainMenu`, `VegetationGallery` and `VegetationGallery_TechnieColliderTest` have no Gaia Terrains (0 tree renderers): nothing to cull.
The two test scenes `LightingTestSceneCullingTest` (B) and `...Full` (C) are now redundant copies; keep or delete, Carlos decides.
**Any future scene with a forest gets the same recipe.** B (KeepShadows) stays the documented fallback (section 11).
New scenes are +30 MB and +N GameObjects each (N = tree count). Cost of the ray jobs in the CPU number: still not isolated.

**Corpses are culled by the same system (step 7 of `corpse-optimization.md`).** A second controller, `Dynamic Culling (Corpses)` (ID 1,
`MergeInGroups` off), is created at runtime by `Assets/ThirdParty/AST-145/MrMoonlightBridge/CorpseCullingBridge.cs` (git-ignored, like the asset)
when the first corpse settles in a scene that already has the tree controller. Verified in Play Mode: looking away for 4 s switched the body off,
looking back switched it on. **Still open:** bullets against the layer-29 tree hulls and the corpse proxy boxes (player weapon masks not read yet).
