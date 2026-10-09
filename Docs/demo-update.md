# Demo update (MRM-88) — running doc

One continuous issue, done in stages. **Linear MRM-88**, branch `mrm-88` (created `--no-track` from `origin/main`).
Each stage gets a dated section below: what changed, where, how to test, traps. The task description and stage plan
live in `Docs/demo-update-opus-prompt.txt`; the cold-start handoff for the current stage is the newest
`Docs/demo-update-*-sonnet-prompt.txt`.

Scene being edited: **`07 LightingTestScene`** (`Assets/_Project/Scenes/07 LightingTestScene.unity`). Carlos places the new
props there by hand; Claude does the scripted edits through the UnityMCP bridge after asking.

---

## ENDING CHECKLIST — do NOT forget (Carlos, 2026-10-02)

While the demo update is being built, **the NavMesh and the enemies are deliberately switched off in scene 07** so props and
terrain can change without paying for a re-bake each time. When Carlos says he is done placing props / editing the terrain
(or says anything that sounds like "we're finished with this stage of placement"), **remind him of this step if he has not
raised it himself**:

1. Re-enable the root GameObject `NavMesh Surface` (Unity.AI.Navigation `NavMeshSurface`: volume 1024 x 67.5 x 1024, centre
   (0, 41.25, 0), CollectObjects = Volume, tile 256, voxel 0.2; new props must be inside the volume).
2. Re-enable the root GameObject `Enemies` (`DemoSpotterPopulationManager` + 10 pooled `Enemy_Spotter(Clone)` children).
3. **Re-bake the NavMesh** (ask Carlos first; he asked to run the bake himself or authorise it at the very end).
4. Verify: NavMesh data asset updated, Spotters still path, no errors in the console, `Event Director` behaves.
5. Save the scene (Ctrl+S) and note it in this doc and in a change-record row.
6. ~~Polish: re-run the Foliage Renormalizer (AST-301)~~ **CANCELLED 2026-10-08: Carlos rolled the renormalizer back and the 59 generated meshes were deleted
   (see "Side session - native Mesh LOD" below).** Do not re-run it unless Carlos asks again; if he does, run it BEFORE the Mesh LOD import settings matter (it
   regenerates meshes from the FBX and would need its own LOD handling).
7. **Polish: try to get Flora Renderer working again (Carlos, 2026-10-08: "Flora was really giving us an edge but the phantom thing was kind of
   whack").** Flora is installed (`Packages/com.ma.flora`) but OFF: `Flora Scene Settings.EnableRendering = 0` in scenes 02, 06, 07, 08, and no
   `FloraInstanceRenderer` on any object. It was switched off 2026-08-31 for the **phantom tree shapes floating over the water** bug (confirmed by
   an on/off A-B test; notes in `Docs/mrm70-resume-2026-08-31.md`). Re-check only when everything else is set up: (a) re-add the renderers with
   `Tools > MrMoonlight > Vegetation > Add Flora Instance Renderers to Spawned Vegetation` on a COPY of the scene or on Island_Legion first,
   (b) turn `EnableRendering` on and look for the phantoms, (c) measure draw calls / FPS in a build against the same view with Flora off,
   (d) check it against AST-145 culling (culling cannot cull Flora-drawn instances) and the renormalized tree meshes, (e) keep it only if it is a
   clear win, otherwise leave it off and record the verdict here. Doc: `Docs/mrm70-flora-phase-kickoff.md`, `Docs/pc-build-target.md` section 6.

---

## Stage 1 — Wooden church placed on flattened terrain (2026-10-02)

**State at the end of the session (all saved to disk, scene 07):**

| Thing | Value |
|---|---|
| `Prop_WoodenChurch` (AST-070, prefab Variant of the FBX) | position (45.03, 50.03, 60.49), rotation 0, scale 1. Mesh world bounds: x 37.17..52.89, z 45.22..75.75 (door colliders reach z 76.22), y 49.67..77.96. 3 colliders (one MeshCollider, two door BoxColliders) |
| Player spawn (`Player_Tracey`) | (68.44, **47.80**, 74.01), Y rotation 148.4. About 1 m above the ground (terrain 46.8 there), so the player falls onto it on Play. The XZ and rotation come from a Play-mode Transform Carlos screenshotted. CharacterController was disabled while moving it |
| `Weather Circuit` (`SkyProximityCircuit`) | (70.02, 46.59, 71.45): 3 m in front of the spawn, base on the terrain. The spawn exists so the church can be checked in Play mode quickly and the weather changed next to it |
| `NavMesh Surface` and `Enemies` roots | **inactive** (see the ending checklist) |
| `Event Director` | left active; it has `EventDirector` + `ObjectiveTracker`. Not checked for enemy dependencies; the console had no errors from it |

**Terrain flattened under the church** (terrain `Terrain_0_0-20260829 - 035828`, origin (-512, 0, -512), size 1024 x 1024 x 1024,
heightmap 2049 = 0.5 m per sample, TerrainData asset `Assets/Gaia User Data/Sessions/GS-20260829 - 011148/Terrain Data/Terrain_0_0-20260829 - 035828.asset`, tracked in git, ~8 MB):

- Plateau level **Y 49.67273** = the lowest vertex of the church mesh (the pivot is 0.36 m above the mesh bottom; the first attempt used the pivot height 50.03 and buried the base). Heightmap precision is 1024/65535 = 1.6 cm, so the baked plateau reads **49.659**, about 1.3 cm below the church base (no clipping, hairline gap hidden by the base).
- Plateau rectangle = footprint + 1.5 m margin: x 35.67..54.39, z 43.72..77.73. Outside it, a **14 m smoothstep blend** back to the original terrain. Max change from the original 2.58 m. Edited region: heightmap samples x 1066..1162, z 1082..1209 (97 x 128 samples).
- Only heights were changed. Terrain textures (11 layers), grass details (72 prototypes), the TerrainCollider (enabled, same data, so walkable) are untouched. There are 0 terrain trees: vegetation is GameObjects.
- **Original heights backup** (normalised 0..1, header `ix0 iz0 w h sizeY`, then h rows of w values): `terrain_backup_church.txt` in the Claude session scratchpad, which is temporary. The permanent route back is `git checkout` of the TerrainData asset (loses any later terrain edit), so any later terrain edit should dump its own backup first.
- How it was done (reusable): `td.GetHeights` over the region, blend `lerp(old, target, smoothstep(1 - dist/fall))` where `dist` is the distance outside the plateau rectangle, `td.SetHeights`, `AssetDatabase.SaveAssets()`. Restoring from the backup before re-applying avoids blending twice.

**Traps found**
- **Play mode discards scene edits** (the `SetActive` calls and moves made while Carlos had Play mode on were lost on stop). Check `EditorApplication.isPlaying` before any scene edit; a previous `execute_code` also throws on `MarkSceneDirty` in Play mode, which is the tell.
- Vegetation GameObjects within ~16 m of the church were NOT repositioned after the terrain change; some may float or be buried. Next session: scan and fix (and Carlos wants more foliage around the church).
- The TerrainData asset is dirty until `AssetDatabase.SaveAssets()` / Ctrl+S; the scene being saved is not enough.
- `VP_LightingTest_Fog.asset` shows a diff `active: 1 -> 0` twice every session: confirmed harmless HAZE noise, leave it out of commits.
- Whenever a prop sits on the ground: terrain pivot Y vs mesh bottom differ (the church mesh bottom is 0.36 m under its pivot). Level to the mesh's lowest vertex, not the pivot.

**Still to do in this stage:** foliage is DONE (Stage 2). Remaining: other church extras and the other staged props (Shed, Weeper ghosts, Tombstones, Pool; see `Docs/asset-moves-to-moonlight-sonnet-prompt.txt`), then the ending checklist.

## Stage 3 - Prop placement (started 2026-10-05)

Scene 07, saved by Carlos on 2026-10-05.

- **Placed:** `Prop_MVillage_Well_01` at (52.944, 49.659, 73.761) and `Prop_MVillage_WellWall_01` at (54.26, 49.659, 78.95), yaw 29.2 deg,
  both from `Prefabs/Buildings/`, on the church plateau (Y 49.659 = plateau height). AST-145 Dynamic Culling registered their renderers
  automatically (scene diff in its object list).
- **`Scene Effects Toggle`: fog and CRT saved OFF** (`fogEnabled` and `crtEnabled` 1 -> 0). Builds 45/46 also had them off; a build made
  from this scene starts with fog and CRT off (F6/F7 still toggle them).
- **Order agreed with Carlos (2026-10-05):** (1) finish placing every prop (remaining Playground moves: Shed, Weeper ghosts, Tombstones,
  Heavy Action music, Pool); (2) then the embellishment pass, prop by prop: lights, lamps, small logic; the church interior fog is decided
  here; (3) then the ending checklist (enemies + NavMesh back on, re-bake); (4) then builds and optimisation testing again.
- **On hold:** build 47 (fog + CRT on) and the fog choice, HAZE (AST-078) vs Volumetric Fog & Mist 2 (AST-282), undecided (see
  `Docs/mrm85-fog-vf2-continue-prompt.txt`). Do not raise them until placement is done unless Carlos does.

### Session 2026-10-07: snap tool, church wall loop, church grounds cleared

- **New editor tool `PropTerrainSnap`** (`Assets/_Project/Code/Editor/PropTerrainSnap.cs`, namespace `MrMoonlight.EditorTools`). Select props,
  press **Ctrl+Shift+T** (menu `Tools > Moonlight > Snap Selection To Terrain`; window `Tools > Moonlight > Prop Terrain Snap`). Puts the
  bottom-centre of the renderer bounds on the terrain and tilts the prop up-axis toward the terrain normal, yaw kept. Settings (EditorPrefs):
  slope blend (default 1), sink (default 0.05 m), tilt on/off. Each selected object is snapped on its own, so do not select a parent and its
  child. Ctrl+Shift+G was the first choice and collided with AMD Adrenalin on Carlos's machine. Unity's own Ctrl+Shift surface drag does nothing on terrain.
- **Church wall loop.** Carlos drew a closed `Spline` (SplineContainer, root object `Spline`, 12 knots, at (61.46, 49.19, 66.89)). Claude filled it with
  **57 `Prop_MVillage_WellWall_01` pieces** under the new root `WellWall_Run_01` (`WellWall_00`..`WellWall_56`), placed by script, NOT with the Spline
  Instantiate component (it cannot follow terrain). Piece is 2.56 m long on local X, pivot at the base centre. Algorithm: sample the spline, drop
  every point on the terrain height, walk it with a fixed 3D chord length, bisect the chord so the loop closes exactly, pick the piece count whose
  chord is nearest 2.56 m (57 pieces, chord 2.546, X scale 0.995 x 1.03 overlap). Each piece: X along the chord, up = slerp(world up, terrain normal,
  0.5), sunk 8 cm, alternate pieces +4 mm to avoid z-fighting. Verified: every piece centre within 0.34 m of the spline, closing gap 0.
  **Not re-run if the spline moves.** The single `Prop_MVillage_WellWall_01` placed on 2026-10-05 is separate and untouched.
- **Trap found (cost one full wrong run):** `SplineContainer.Evaluate(...)` already returns WORLD-space positions. Calling
  `transform.TransformPoint` on them again shifts everything by the container's position (the first run landed ~60 m off). Use the values as they are.
- **Church grounds cleared of foliage** (inside the wall loop): the only foliage inside was terrain details (no terrain trees on this terrain; the
  only renderers inside were the church and the well). 398 detail cells (2 m) cleared on all 90 layers, detail density removed 1,595 (mostly
  BushDry A/B, Heather, RF_Bush); cells are those touching the loop polygon, so up to ~1 m past the wall on the outer side. Re-count inside the
  loop afterwards: 0. TerrainData asset saved (`Assets/Gaia User Data/Sessions/GS-20260829 - 011148/Terrain Data/...`). Recipe: same as stage 2.
- **Scene 07 was NOT saved by Claude** at the end of this session (57 pieces + `WellWall_Run_01` exist in the open scene only). Carlos must save.
- Still to do at the very end of placement (Carlos, 2026-10-07): group every placed prop under folder-style parent objects; clear foliage under
  each prop's footprint (Carlos approved doing it with the steps as the first test: the church loop was done instead).

## Side session - Foliage Renormalizer (AST-301) on the tree prefabs (2026-10-08)

Scene 05 `VegetationGallery_TechnieColliderTest` was the target; the prefabs are what changed, so scene 07 and Legion inherit it.

- **Done:** 59 tree prefabs under `Prefabs/Nature/Trees/` now use renormalized leaf-card meshes (`Art/Nature/Renormalized Trees/`, 17.4 MB);
  24 solid-material trees skipped; 0 failed. Batch script `Assets/_Project/Tools/Editor/FoliageRenormalizerBatch.cs`, manifest beside it.
  Rollback / Re-apply / Apply-all menu items under `Tools > Foliage Renormalizer`. Full detail and the **end-of-demo re-run** steps:
  `Docs/foliage-renormalizer-polish.md` (also ending checklist step 6 above). Change record C-025.
- **Carlos's verdict:** keep it ("not sure it improved, at least a placebo"). Compared against his recorded walk-through of scene 05.
- **Fog:** HAZE Global Fog and HAZE Explorable Area Fog were deactivated in the OPEN scene 05 for the comparison only. Scene 05 was not saved.
- **Not verified:** painted/instanced trees in scene 07 and Island_Legion render the new meshes (Flora may cache meshes). Check one of each.
- **Next (Carlos, 2026-10-08):** before returning to staging, try AST-147 Asset Optimizer Pro on the vegetation prefabs to reduce cost
  (handoff: `Docs/demo-update-ast147-sonnet-prompt.txt`). Then continue Stage 3 staging.
  **Superseded the same day:** AST-147 was assessed by reading its source, found unfit, and removed (see the next section).

## Side session - optimizer assets assessed: AST-147 removed, AST-068 and AST-146 staged (2026-10-08)

- **AST-147 Asset Optimizer Pro: REMOVED on Carlos's word.** Its simplifier rescans every vertex for each collapse, treats open leaf-card quads as
  borders (collapses almost nothing), flattens submeshes, and creates LOD objects inactive. Checked before deleting: 0 GUID or code references
  from outside its folder, no output folders or settings asset created, the folder was git-ignored. A full copy remains in
  `Asset Collection\02_extracted\AST-147` and the zip in `01_DOWNLOAD`.
- **Flora status established** (not changed): installed, OFF everywhere, 0 renderers; trees are ~7,900 real GameObjects in scene 07, grass is Unity terrain
  details. Re-check is now ending checklist step 7.
- **Measured (scene 07, from the scene file + prefab stats, NOT a build):** ~7,900 tree instances = ~25.4 M triangles if all drawn, avg ~3,200 tris per tree,
  2 submeshes each (bark + leaves), 65 distinct materials on ONE shader (Retro Lit, 512 px / 256 px base maps), 72 prefabs, 0 with LODGroup, all with colliders.
  This corrects the earlier focus on the 18-28k-triangle giants: the cost is hundreds of mid-size trees.
- **Staged into Mr. Moonlight, nothing run on any scene:** `Assets/ThirdParty/AST-146 (MeshFusion Pro)/` (Core + API/QuickStart PDFs, 2.0 MB; the 16 MB `Example/`
  left out) and `Assets/ThirdParty/AST-068 (Super Level Optimizer)/` (Core + manual + 4 tutorial PDFs, 2.5 MB; the 587 MB `Third-Party/` demo packs, the
  HDRP/URP/BuiltIn support packages and the tutorial scenes left out). 0 GUID collisions, compiles clean. MeshFusion has its own asmdefs (runtime code ships in a build);
  SLO2 has no asmdef. Both are by New Game Studio, the same author as AST-145 culling. Assessment is in the chat; verdict recorded below when Carlos decides.
- **Assessment (2026-10-08):** MeshFusion is the better fit IF trees are ever merged (no material changes, colliders untouched, LOD-aware via LODGroups), but merged cells duplicate
  geometry (~25 M tree vertices in scene 07 = roughly 0.8 GB) and per-tree culling (AST-145) is lost. SLO2's atlasing of 65 leaf-card materials is the riskiest part. Order agreed
  with Carlos: LODs first (scene 05 bench, then Island_Legion), builds between steps, MeshFusion pilot after. Unity 6.3 has NATIVE Mesh LOD (`ModelImporter.generateMeshLods`,
  `Mesh.SetLods`, `QualitySettings.meshLodThreshold`); the renormalized tree meshes are `.asset` files so the importer option cannot be used on them directly.
- **FPS counter + logging + baseline build (Carlos's plan, same session):** `FPS Counter` prefab added to scene 05 and the scene saved with the HAZE fog objects ACTIVE again
  (diff = the prefab instance only). SessionLog v6 (`Docs/performance-sessions.md` section 7). **Build 47** `E:\Builds\47 - Gallery Baseline - 2026-10-08` = scene 05 only,
  no LODs, 0 errors, 369.6 MB zipped. Scene 05 holds one of each prefab, so it is a visual/triangle baseline, not an island fps test. Carlos plays it and says so; analysis follows
  `performance-sessions.md` section 1. Change record C-026. Next session: `Docs/demo-update-lod-sonnet-prompt.txt`.

## Side session - renormalizer rolled back, native Mesh LOD applied to scene 05's vegetation (2026-10-08)

- **Renormalizer undone on Carlos's word:** `Tools > Foliage Renormalizer > ROLLBACK` put all 59 tree prefabs back on their original FBX meshes (status read back:
  usingOriginal=59); the folder `Art/Nature/Renormalized Trees/` (59 generated meshes, 17.4 MB) was then deleted with the MCP `manage_asset` delete after a 0-reference
  check (the first attempt via `execute_code` with safety checks off was refused by the permission layer and not retried that way). The batch tool
  `FoliageRenormalizerBatch.cs` and its manifest stay in `Tools/Editor` (the manifest now points at deleted meshes: do not use Re-apply). Why: not obviously better looking,
  and it would have blocked native LODs and complicated AST-068 / AST-146 (renormalized meshes were `.asset` files, not FBX imports).
- **Native Mesh LOD (Unity 6.3), importer route:** `Generate Mesh LODs` ticked on the FBX importer of every nature prefab with >= 1,000 triangles and no LODGroup.
  80 FBX meshes (`Prefabs/Nature/Trees` + `Rocks & Logs`), 5 to 11 levels each, valid `lodSelectionCurve` written by Unity (e.g. GTree01_05: 4146 -> 2109 -> 1088 -> 577 ... -> 64 tris;
  Curse_H01_2: 27,975 -> 64). Tool: `Assets/_Project/Tools/Editor/TreeMeshLodBatch.cs` (menu `Tools > Tree Mesh LOD`: apply, ROLLBACK), manifest `TreeMeshLodManifest.json`.
  A first attempt that built the lower levels itself (dropping leaf cards on the `.asset` meshes) was replaced; its two test meshes were rolled back and then deleted with the folder.
- **Colliders are unaffected:** every wood collider is a separate mesh in `Art/Nature/Tree Colliders/Wood Meshes/`; 110 mesh colliders in scene 05 read back with their meshes.
- **Not yet measured:** the visual quality of the lower levels and the numbers. Next: a scene 05 build with the standard run (`Docs/lod-experiment-log.md`), compared against build 47.
  Scene 05 reads as unsaved (dirty) in the editor after this session; it was NOT saved by Claude.
- **Next asset to try:** AST-146 MeshFusion Pro (see `Docs/demo-update-lod-sonnet-prompt.txt`). AST-068 Super Level Optimizer 2 is the riskier one: it atlases materials
  (clamp wrap, UVs rescaled, textures decompressed), which threatens the pixelated diffuse look and per-material cutoff / tint, and it combines with `Mesh.CombineMeshes`, which does not keep Mesh LODs.
  Change record C-027.

## Side session - MeshFusion Pro (AST-146) pilot on scene 05 (2026-10-08, night)

- **Asset:** AST-146 MeshFusion Pro 1.3.5 (Core only, git-ignored). At runtime `RuntimeMeshFusion` (controller) takes every `StaticMeshFusionSource` assigned to it, groups them into
  grid cells (`CellSize`, 80), copies vertices/triangles into combined meshes grouped by material and disables the originals' renderers. No file is created; combined meshes live in memory.
  Materials and textures are not touched (why it was chosen over AST-068).
- **Pilot, scene 05 only:** `Tools > MeshFusion Pilot` (`Assets/_Project/Tools/Editor/MeshFusionPilot.cs`, manifest `MeshFusionPilotManifest.json`, ROLLBACK tested by design, not yet run).
  89 gallery specimens at x <= 260 got a `StaticMeshFusionSource`; Batching Static cleared on their children; Read/Write ticked on 89 FBX importers (required by MeshFusion).
  Colliders stay on the originals. Scene saved by Carlos after the editor test.
- **Known trade-offs to measure:** merged copies use LOD0 geometry only (native Mesh LOD, C-027, is lost on merged objects); per-tree culling (AST-145) is replaced by per-cell culling;
  readable meshes cost a CPU copy; the gallery has one specimen per species so the draw saving is only what shares materials inside a cell.
- **Build 49:** a first build was made and DELETED on Carlos's word (2026-10-08): the commit had not been made, so it could not be traced to a hash. Build 49 is remade from the commit of change record C-028.
- **Next (planned for the next session):** apply the same to the island (scene 06 Island_Legion first, then 07) with the trees' native Mesh LOD already in place; see `Docs/demo-update-meshfusion-sonnet-prompt.txt`.
  Change record C-028.

## Side session - folder reorganisation (2026-10-03 to 05)

Not a placement stage: done so placement can find its prefabs. Every placeable prefab moved to `Assets/_Project/Prefabs/<semantic folder>/`
(Buildings, Props, Sets, Nature/{Trees, Rocks & Logs, Grass & Plants, Old (Rough Colliders)}, Characters, Sky & Lighting, VFX, UI,
Dev & Tests); art grouped by subject; vendor folders `AST-### (Short Name)`. The church is now `Prefabs/Buildings/Prop_WoodenChurch`,
the Medieval Wells pieces are split over `Buildings/`, `Props/`, `Sets/` and `Nature/Rocks & Logs/`. The original vegetation (rough
capsule/box collider) is `Old_*`; scene 07 still uses about 40 of them plus its Gaia objects, unchanged. **Place new trees and rocks
from `Nature/Trees` and `Nature/Rocks & Logs`** (exact wood colliders). No scene changed. Full map: `Docs/folder-map.md`; record C-021,
commit `e5f3941d`.

## Stage 2 - Foliage pass, church site clean, circuit raised (2026-10-02)

Scene 07 only. Source of truth for the distribution is Carlos's spreadsheet `Desktop\Foliage_Distribution_Sheet.xlsx` (not in the repo; 108 rows, 9 biome columns with dropdowns 0 None .. 5 Very dense, a "Current in scene" tab). Levels are instances per 2x2 m terrain-detail cell: <0.005 none, <0.05 sparse (target 0.03), <0.2 light (0.1), <0.6 medium (0.35), <1.2 dense (0.9), else very dense (1.8). Collider objects use objects per hectare of the biome (sparse 0.5, light 2, medium 5, dense 12, very dense 25).

- **Biomes** (dominant paint of 1024 m): Forest 35.8%, AutumnForest 5.4%, Mountain 4.9%, FlakTower 3.6%, Beach 3.4%, Glade 0.9%, EerieForest 0.9%, HereticForest 0.8%, Fountain 0.4%, Path 0.1%, Seafloor 43.7%. **The church stands in FlakTower**; the rock at (86, 45, 102) is Mountain.
- **How the foliage is rendered:** the 72 original details (all GRASS PREFABS) are mesh details, VertexLit, instanced, drawn by Unity's built-in terrain detail renderer (detail distance 150, density scale 0.5, instance-count mode, detail resolution 512). `Flora Scene Settings.EnableRendering` is False, so Flora does NOT accelerate them (Flora only takes terrain foliage when EnableRendering is on, FloraSystem.cs:746; that flag is tied to the phantom-tree bug, untested).
- **Pass 2 (build 46):** 18 new prototypes (72 -> 90): RF_Bush1-3, RF_Fern1-2, AP_Tree_Dry_N02 as mesh details; GFF_Grass01/02 and GFF_GrassFlower01-10 as GrassBillboard texture details (size 0.8-1.4 x 0.6-1.0 m is a guess). ~495k instances (Forest ~459k). ~290 collider objects under root `Foliage Pass 2 Objects` (land only, slope <30 deg, 22 m away from the church).
- **After build 46 (NOT in any build yet):** FlakTower BushDry A/B Very dense (~17k each), Heather A/B Light; Mountain Bush A/B Dense (~11.5k each), WildGrass_General Medium; church site cleared (x 33.2-56.9, z 41.2-80.2: 1,614 detail instances, no trees); `Weather Circuit` corners 0-2 raised to terrain + 1.5 m (Marker follows Corner 1).
- **Method:** `execute_code` writes via `td.detailPrototypes` + `SetDetailLayer`; painting replaces a biome's cells with random p = target/2.5 and value 2-3 (matches the original style). Backups of terrain data and scene before the pass sit in the session scratchpad only (lost with the session): `git diff` of the TerrainData asset is the real record. Biome masks come from the splat map at the detail cell.
- **Performance:** see `Docs/performance-sessions.md` section 5 (builds 45/46) and change record C-019. Foliage pass cost about +0.4 ms and +400 draws.
- **Open: distant tree pop-in** (H15) with fog and CRT off. Build 47 is meant to have fog (F6) and CRT (F7) on; test the culler on/off before changing it.
- Carlos's call: **stop foliage work here, continue with prop placement** (scene is a test scene, not the final one).

---

## Later stages (from the opus prompt, not started)

2. Enemy behaviour tweaks (Spotter). 3. New enemies. 4. Little stores. Order is Carlos's call.
