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
