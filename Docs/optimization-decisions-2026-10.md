# Island optimization track, 2026-10-08 to 2026-10-09: what was tried, what was decided, how to undo it

Branch `mrm-88` (Linear MRM-88; results also in MRM-85 "Performance reports"). Raw numbers per build: `Docs/performance-sessions.md` section 5 (builds 47-54).
Change records: C-026 to C-033 (section 8 of the same doc). Test PC: Ryzen 9800X3D / RX 9070 XT (high-end, so keep headroom for typical PCs and for enemies).
Standard route (builds 50-54): spawn near the church, straight line through the dense forest, ~60 s, scene 07 only, release build, fog off.

## 1. Decisions (final unless Carlos reopens them)

| # | Decision | Date | Why (numbers) | Undo |
|---|---|---|---|---|
| D1 | **Native Mesh LOD stays** (80 FBX, `lodBias 2`, `meshLodThreshold 1`, `maxLOD 0`) | 2026-10-08 | Build 48 (gallery): triangles -9% (up to -23% in dense stretches), fps unchanged, no visible cost. Costs nothing at runtime; a real forest should save more than the gallery. Not isolated on the island yet (every island build had it). | Reimport the FBX with Generate Mesh LODs off |
| D2 | **MeshFusion Pro (AST-146) dropped** from the island | 2026-10-08 | Build 51: fused 8,786 trees = ~2% fps for +45 MB memory (the island was not draw-bound by trees once culled). `MeshFusionIsland.cs` stays as a harmless editor tool; the asset stays staged (git-ignored). | Tools > MeshFusion Island > Apply |
| D3 | **AST-145 (Advanced Culling System 2) REMOVED, Unity's own culling is the default** | 2026-10-09 | AST-145 was worth ~20 fps over plain frustum (131 vs 110) but made distant trees pop in (H15; 4000 rays / 5 s only reduced it). Build 54 with baked Umbra occlusion: **154.0 fps avg, 1% low 132.2, worst frame 9.7 ms, no pop-in of any kind** (AST-145 builds 128-131 fps). Carlos: "best-performing run so far". | See section 4 |
| D4 | **Umbra occlusion is baked per scene** (07 done; 06 and 02 see section 3) | 2026-10-09 | The bake is what made build 54 fast. A scene without the bake only gets frustum culling (~110 fps, build 52). | Window > Rendering > Occlusion Culling > Clear |
| D5 | Leaf cards stay Occluder Static | 2026-10-09 | The feared artifact (trees hidden behind canopy gaps) did NOT show in build 54. If it appears: clear Occluder Static on the tree renderers and keep Occludee Static. | n/a |
| D6 | Proxy colliders (`DC_Collider`, layer 29 `ACSCulling`, 8,786 per scene) deleted with AST-145 | 2026-10-09 | They existed only for the culler (full tree mesh, matched the renderers exactly). Removing them also removes 8,786 mesh colliders per scene from physics and from the build. The WoodColliders (MRM-84) are untouched. | Not restorable without AST-145 |

| **D7** | **Flora Renderer ON for the island trees** (scenes 07, 02, 06, 08; 8,786 / 5,990 / 8,786 / 8,786 `FloraInstanceRenderer`s, `EnableRendering` on) | 2026-10-09 | Build 56 (scene 07): **162.1 fps avg** vs build 54's 154.0 (+5%), GPU 6.59 -> 4.56 ms (-31%), CPU 6.79 -> 6.17 ms; Carlos saw ~170 fps in the dense forest, "not happening in any other setup". No phantom trees (the 2026-08-31 bug did not reproduce), no pop-in, wind and shadows looked normal, no flicker. **Requirements that cost a day: Retro Lit must not read `unity_ObjectToWorld` directly (use `GetObjectToWorldMatrix()`), and Project Settings > Graphics > BatchRendererGroup Variants must be `Keep All`** (`m_BrgStripping: 2`). Without the second one the build shows NO trees (build 55) while the editor looks perfect. | Scene file `git checkout`, or `EnableRendering` off + remove the `FloraInstanceRenderer`s (`FloraInstanceRendererPass` adds them); the shader patch and Keep All can stay |

## 2. Timeline (so the story is not lost)

| Build | What | avg fps | Verdict |
|---|---|---|---|
| 47 / 48 | Gallery (scene 05) baseline, then native Mesh LOD | 960 (gallery) | LOD: -9% triangles, fps same (D1) |
| 49 | MeshFusion pilot on the gallery | n/a | Promising on the gallery, led to build 51 |
| 50 | Island baseline, AST-145 on (1500 rays, 2 s) | 131.0 | Baseline |
| 51 | MeshFusion on 8,786 trees, AST-145 off on them | worse than 50 | Fusion dropped (D2) |
| 52 | Culler off, no fusion (plain frustum + Mesh LOD) | 110.0, no pop-in | Culler worth ~20 fps; pop-in is the culler |
| 53 | Culler on, 4000 rays / 5 s | 127.7 | Pop-in reduced, not gone (H15) |
| **54** | **AST-145 off, Umbra occlusion baked** | **154.0** | **Winner (D3)** |
| 55 | Flora on (8,786 renderers) + shader patch, Keep All NOT set | n/a (all trees missing) | BROKEN: BRG shader variants stripped, colliders stay, ~300 draws, 262 fps meaningless |
| **56** | **Flora on + BatchRendererGroup Variants = Keep All** | **162.1** | **New best (D7); GPU -31%, CPU-bound at ~6 ms now** |

## 3. Scene state after the removal (2026-10-09, change record C-033)

- Scenes 02, 06, 07, 08: all AST-145 components removed (DC_SourceSettings on the trees, DC_Controller, DC_Camera, CorpseCullingBridge) and all `DC_Collider` objects deleted. Verified: 0 missing scripts and 0 objects on layer 29, in all four.
- Scene 07: baked occlusion (`07 LightingTestScene/OcclusionCullingData.asset`, 114 MB YAML, 56.9 MB Umbra data, LFS-tracked via `.gitattributes`).
- Scenes 06 (Legion, the fight scene) and 02 (Island): see the status at the end of section 5. **Every scene needs its own bake; re-bake after moving static props, buildings or terrain.**
- **Re-bake rule for the demo:** props placed in scene 07 during staging (church, wells, walls, sheds) are static: give big solid buildings Occluder Static, small props Occludee Static only, then re-bake at the END of placement (Window > Rendering > Occlusion Culling > Bake, or `StaticOcclusionCulling.GenerateInBackground()` from execute_code).
- Unity must be FOCUSED for the background bake to progress (unfocused it sits at 0%).
- Code edited: `MeshFusionIsland.cs` (no longer needs the DC types), comments in `MoonlightTunables.cs` and `CorpseOptimizer.cs`. The `CorpseCullingHooks` event stays (nothing subscribes).
- Deleted on Carlos's explicit word ("Yes remove 145. Remove all the files"): `Assets/ThirdParty/AST-145 (Culling System)` (2.1 MB, git-ignored). The layer name `ACSCulling` (layer 29) is still in `TagManager.asset`; harmless, can be renamed later. NOT touched: copies outside the project (`01_DOWNLOAD` is never touched; ask Carlos before removing any extracted copy).

## 4. How to get AST-145 back (only if Unity culling disappoints)

1. Re-extract it from the owned asset (see `Docs/asset-import-update-process.md`; `Docs/culling-ast145.md` has the install and settings).
2. It needs `DC_SourceSettings` on every tree Visual (8,786), a `DC_Controller` and a `DC_Camera` on PlayerCamera, and proxy colliders on layer 29 (`ACSCulling`). The old scene versions are in git history (commit `fa7086fc` and earlier have all of it; `Docs/culling-rollback.ps1` rolls a scene back).
3. The corpse bridge (`CorpseCullingBridge.cs`, our code) was deleted with the folder. It lived in the git-ignored folder, so it may NOT be in git history. Its job: subscribe to `CorpseCullingHooks.CorpseSettled` and register the corpse renderer with the culler.

## 5. Open items and suggestions (what else can be optimized, in the order I would do it)

1. **Occlusion bake for scenes 06 (Legion) and 02 (Island)** and a fight test in 06. Corpses are dynamic, so Umbra does not hide them; the settle pass and burn dissolve (`Docs/corpse-optimization.md`) still remove them. Step 7 (cull while out of sight) no longer exists.
2. **Re-bake scene 07 at the end of staging** (D4 rule) and re-measure with the same route once the church, wells and walls are in.
3. **GPU Resident Drawer + GPU occlusion culling** (URP, Unity 6): `PC_RPAsset` has it off; the Windows graphics API is Direct3D11 only and the Resident Drawer very likely needs D3D12 or Vulkan; custom shaders (RetroLit, HAZE fog, Crest) may not support BatchRendererGroup. Draws are still 8.6k and CPU is about equal to GPU (6-9 ms), so this is the next big lever, but it is an Opus job: render-audit first (RenderAudit.cs), screenshot check for pink.
4. **Shadows**: 1.7-2.8k shadow casters per window; check shadow distance, cascades and tree shadow LOD against the GPU ms in `[PERF]`.
5. **Typical-PC headroom**: the test PC is top-end. Before the Kickstarter, run a build on a weaker GPU; settings tiers (lodBias, shadow distance, far clip 1000) are the knobs.
6. **Fog + CRT build** (on hold): fog hides far trees and allows a shorter far clip. The HAZE vs Volumetric Fog 2 decision is also on hold.
7. **Flora is now ON (D7, 2026-10-09).** Open: it replaces the `[PERF]` draw / setpass / tris counters for the trees (build 56 reported 3.2k draws, 0.37M tris against 8.6k / 68.6M in build 54, which is NOT real work: trust fps and GPU ms), whether it uses the baked Umbra occlusion or its own GPU culling, and the CPU still sits at ~6 ms with only 3.2k draws (not the trees any more; look at shadows, terrain, Flora's own update). Lights (H13: ~0.3 ms per light) are unchanged.

**Scene state, Flora (2026-10-09 night, C-035):** Flora renderers + `EnableRendering` on in 07 (8,786), 02 (5,990), 06 (8,786) and 08 (8,786), all saved. Scenes 02, 06, 08 are NOT measured in a build yet. Scene 08's occlusion bake was started the same night (see the result in `Docs/performance-sessions.md` C-035). **Any new tree painted into a scene needs `FloraInstanceRendererPass` run again, or it draws through Unity's normal path (it still works, it is just not accelerated).**

**Bake status (2026-10-09, end of session):** scene 07 = 56.9 MB Umbra data (build 54 measured), scene 06 Island_Legion = 56.9 MB (baked, saved), scene 02 Island = 50.0 MB (baked, saved). Scene 08 (Fog Experiment) is NOT baked. Scenes 06 and 02 have not been measured in a build yet. Each `OcclusionCullingData.asset` is 100-114 MB of YAML and goes to Git LFS. Do not run a long `Thread.Sleep` loop in `execute_code` while a background bake runs: it blocks the editor thread and stalls Unity.
