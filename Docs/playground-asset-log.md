# Playground asset log: what came in, what went out, what was tested

Playground (`E:\playground\Playground`, Unity MCP port 8081) is a **short-lived testing bench**, not an archive (Carlos, 2026-10-04,
disk was down to 32 GB). The cycle is: **need an asset -> stage it in Playground -> it fulfils its role (reviewed, moved to Mr. Moonlight,
or rejected) -> it is removed.** The master list of everything owned is the spreadsheet (`Docs/asset-index/ASSETS-Index.xlsx`); this file
is the history of what went through the bench, so we know what was already tested.

## RULE: nothing is deleted without Carlos's go-ahead

**Claude never removes an asset from Playground, Mr. Moonlight or `02_extracted` on its own, and never as an "automatic last step".**
When an asset has fulfilled its role Claude may *suggest* removal (and say what it would free), then waits. Only Carlos's explicit
instruction ("delete X", "keep only this list, remove the rest") authorises it. Before deleting:
1. list exactly what goes and what is kept, and check dependencies (GUID references from kept assets into the deleted ones; asmdef name
   references such as `PG.Shared`);
2. check what exists **only** in Playground (no file in `01_DOWNLOAD`, no Asset Store cache copy) and back up the small local-only files;
3. delete folders and their `.meta`, then refresh Playground and confirm the console is clean;
4. add the rows below. Never touch `01_DOWNLOAD` (the zips stay) or `Assets.zip`.

## How to log

One row per event, newest at the bottom of its table: date, AST id, name, event, notes (verdict, where it went, how to get it back).
Events: `staged`, `tested`, `moved to Mr. Moonlight`, `rejected`, `removed`. Keep the spreadsheet row's `notes` column in step.

## Currently in Playground (after the 2026-10-04 cleanup)

**Folder naming (same rule as `Docs/folder-map.md` for ThirdParty, applied to Playground 2026-10-05):** `AST-### (Short Name)`, the
code first then one or two words saying *what the asset is* (not how it looks). Rename through `AssetDatabase.MoveAsset` so GUIDs
survive, and fix any editor tool with a hardcoded folder (only `AST-162 (Swimming Pool)/Editor/HdrpToUrpMaterialConvert.cs` had one).
New staging: name the folder this way from the start.

| Folder | Why it is still here |
|---|---|
| `AST-070 (Wooden Church)` | built in Mr. Moonlight, awaiting Carlos's sign-off |
| `AST-108 (The Shed)` | next to move |
| `AST-131 (Weeper Ghosts)` | next to move (character path) |
| `AST-074 (Graveyard Tombstones)` | next to move |
| `AST-076 (Heavy Action Music)` | next to move (audio workflow) |
| `AST-162 (Swimming Pool)` | next to move (pick pieces; too big whole) |
| `AST-270 (Medieval Wells)` | moved to Mr. Moonlight 2026-10-03, awaiting review |
| `AST-292 (Witch Village)` | staged 2026-10-05 for Carlos's review; HDRP-only pack converted to URP in place |
| `AST-293 (Dead Bodies)` | staged 2026-10-05 for Carlos's review; Built-in materials converted to URP in place |
| `AST-294 (Barricades)` | staged 2026-10-05 for Carlos's review; raw FBX, materials built here |
| `AST-295 (Medieval Furniture)` | staged 2026-10-05 for Carlos's review; HDRP-only pack converted to URP in place |
| `AST-296 (PSX Hospital Church)` | staged 2026-10-05 for Carlos's review; raw FBX, textures assigned here |
| `AST-297 (PSX Wooden Fences)` | staged 2026-10-05 for Carlos's review; raw FBX, no changes needed |
| `AST-298 (Horror Sounds)` | staged 2026-10-07 (3,284 audio clips), import only, audio workflow not run |
| `AST-040 (Blood Factory)` | **dependency**: provides `PG.Shared` / `PGHybridInput`, which AST-131's asmdef and 11 demo scripts use. Remove only together with AST-131 |
| `_Local` | local tools and placeholder materials | **dependency**: the Shed and Weeper demo scenes point at its materials/fly-cam; also holds the URP conversion tools |

## History

| Date | AST | Asset | Event | Notes |
|---|---|---|---|---|
| 2026-10-03 | AST-060 | Poly Universal Pack v4.5 | staged, tested, **rejected**, removed | 11,904 files; converted 88+7 materials to URP, `PGHybridInput` on 2 scripts, first-person controller wired into DEMO_01. Carlos: "useful in general, not for Mr. Moonlight". Restore: `01_DOWNLOAD\AST-060.zip` |
| 2026-10-03 | AST-270 | Medieval Wells Props v2.0.0 | staged, **moved to Mr. Moonlight** | props only: 42 FBX, 42 `Prop_` + 23 `Set_` prefabs, atlas pixelated to 1024; legacy foliage, demo scenes, lightmaps, terrain layers not moved. Wizard G21 |
| 2026-10-04 | 60 assets | see next table | **removed** (cleanup) | Carlos: keep only AST-070/108/131/074/076/162/270, remove the rest. 59 GB freed (E: 32 -> 91 GB free). AST-040 and `_Local` kept as dependencies |
| 2026-10-05 | (all kept) | folder renames | **renamed** | the 8 kept AST folders now carry `(Short Name)`, see the naming rule above |
| 2026-10-05 | (project) | default render pipeline | **fixed after the cleanup** | everything in the AST-270 village scene rendered pink: Graphics Settings' default pipeline pointed at a URP asset (guid `d2dbbafc...`) that lived in one of the deleted folders, so Unity fell back to the built-in pipeline. Set the default to the project's own `Assets/Settings/PC_RPAsset` (best guess at the old one). Verified with a Game-view screenshot (no pink). **Lesson: before deleting, also check `ProjectSettings/GraphicsSettings.asset`, `QualitySettings.asset` and `Packages/manifest.json` for GUIDs/paths inside the folders to delete**, not only the kept assets' own references. Also: the editor then hung ~14 min on "Importing" with zero CPU; force-closing and reopening cured it |
| 2026-10-05 | AST-292 | Witch Village Environment v2.0 | staged, **converted HDRP -> URP**, rendered | 1,179 GUIDs / 2,224 files into `AST-292 (Witch Village)`. **HDRP-only pack** (everything under `LeartesStudios/WitchVillage/HDRP`). 265 materials: 23 HDRP/Lit (`HdrpToUrpMaterialConvert.Convert(folder)`), 140 Built-in Standard (`StandardToUrpConvert`), 95 HDRP Shader Graphs (`ShaderGraphToUrp`), 4 particle. Render audit of all 263 prefabs: 0 pink; 16 blank = 5 particle-only prefabs + 11 `SM_RopeSplineMesh_*` (spline meshes need the vendor's spline script, which is not in the package). Vendor leftovers, not fixed: `M_Terrain` (HDRP terrain material), 2 HDRP demo scenes (volumes/decals), 3 Tree-component warnings (need Nature/Soft Occlusion) |
| 2026-10-05 | AST-293 | P3D Dead Bodies v1.0 | staged, **converted Built-in -> URP**, rendered | 641 GUIDs / 1,209 files. 58 Standard converted, 6 Built-in particle materials converted, 6 Skybox/Cubemap (scene skies, left). Render audit of 161 prefabs: 0 pink; 2 blank (a blood splash decal and one body-parts-on-table prefab, to check by eye) |
| 2026-10-05 | AST-294 | Barricades Pack v1.0 | staged raw, **materials built**, rendered | no unitypackage: Sketchfab rip, 1 FBX (339 skinned meshes under bone root `szkielet-0`, 65 x 67 m laid out in a grid) + 51 PNG. The FBX carries 339 material slots named `000-0-0`.. with no textures; `materialInfo.txt` lists the 339 meshes in node order with a material name, so the index in the slot name picks one of **14 shared URP/Lit materials** (`AST-294/Materials/M_*`; base + normal only, flat metallic 0.5 on the metal ones). Remapped through the importer's external-object map (339/339). Normal PNGs set to Normal type, Roughness/Metallic/AO to linear. Render audit per child: 0 pink, 5 blank of the first 144 |
| 2026-10-05 | AST-295 | Medieval Furniture Props v1.0 | staged, **converted HDRP -> URP**, rendered | 359 GUIDs / 699 files; 1 GUID skipped (`M_FireSheet_01.mat`, also in AST-292, HDRP folder). **HDRP-only pack.** 52 HDRP/Lit + 38 HDRP Shader Graph (S_BasicTextured, S_Blend) converted, particles converted. Render audit of 89 prefabs: first pass 85 of 85 pink (Shader Graphs report `isSupported = True` yet draw as the error colour in URP), after conversion 0 pink; only `SM_Carpet_A` flat/blank and 4 particle-only prefabs |
| 2026-10-05 | AST-296 | PSX Abandoned Church Hospital (itch.io) | staged raw, **textures assigned**, rendered | `Hospital.fbx` (67 props) + 4 PNG into `AST-296 (PSX Hospital Church)`. The FBX materials had no textures; the 4 PNGs carry the material names, so 4 URP/Lit materials were built and remapped. Left in `02_extracted`: `Hospital.blend`, `.obj`/`.mtl` (duplicates) and `Hospital_horror.zip` (an **Unreal Engine project**, 160 `.uasset`, useless here). Render audit: 0 pink, 0 blank |
| 2026-10-05 | AST-297 | PSX Modular Wooden Fence and Debris (itch.io) | staged raw, rendered | 71 FBX + textures; Unity already built 71 URP/Lit materials. Left out: the `.blend` and the 71 `.glb` duplicates. Render audit: 0 pink, 0 blank |
| 2026-10-05 | AST-296, AST-297 | prefabs + orientation | **prefabs built** | `_Local/Editor/PsxPrefabBuilder.cs`. **AST-297**: every FBX node carries rotation (270,0,0) and scale 100 (Blender export without Apply Transform; only the pair looks upright). Unity's `Bake Axis Conversion` flipped a bench 180 deg about Y (tried on Bench_001, reverted), so the node transform is baked into **71 new mesh assets** (`AST-297/Meshes/*_Baked.asset`: vertices, normals, tangents, winding) and **71 prefabs** (`AST-297/Prefabs`) with identity rotation, scale 1, authored pivot kept; baked world bounds equal the FBX's upright bounds. **AST-296**: Y-up and base pivots already correct, but each prop sat at its row position (up to +-17 m on X): **67 prefabs** (`AST-296/Prefabs`) at the origin, mesh referenced from the FBX. No colliders (not requested). Verified from the saved prefabs (0 bad rotation/scale/position, 0 null mesh/material) and a render audit (0 pink, 0 blank); console 0 errors |
| 2026-10-07 | AST-247 | Gamer Girl v1.0 | staged, rendered | 476 GUIDs / 932 files into `AST-247 (Gamer Girl)` (`GamerGirl/` only). Ships **three render-pipeline sets** (`Render pipeline/Built-in`, `/HDRP`, `/URP`, each with its own scene): the URP set (63 URP/Lit materials, 15 prefabs = 3 outfits x 5 colour variants, skinned) is used as is; the Built-in (63 Standard / Standard Double Sided) and HDRP (63 error-shader) materials are the vendor's other variants and are **pink by design in URP: do not use those folders**. Render audit of the 15 URP prefabs: 0 pink, 0 blank. 0 scripts, console 0 errors. No conversion needed |
| 2026-10-07 | AST-298 | Horror Bundle - Sound Effects v8.0 (new) | staged | 3,690 GUIDs / 6,974 files into `AST-298 (Horror Sounds)`: 3,284 AudioClips, no prefabs/scenes/scripts/materials. Import only: the audio workflow (`Docs/audio-import-workflow.md`, clip-prefix routing) has **not** been run. Console 0 errors |
| 2026-10-07 | (audit tool) | `RenderAudit.cs` first-slot artifact | **fixed** | the first `PreviewRenderUtility` render of a session came out empty, so slot [0,0] of the first sheet read `BLANK`. A warm-up render is now thrown away first. Re-audit: AST-295 `SM_Carpet_A` and AST-293 `Blood_Splash_T01` (reported blank on 2026-10-05) are fine. **Real blank found in AST-293: `Severed_BodyParts_onTable_1.prefab` ships with every child at scale (0,0,0)**: a vendor defect, left as is |
| 2026-10-07 | AST-247 | Gamer Girl v1.0 | reviewed (Carlos: fine), **removed** | removed on Carlos's explicit word. Checked first: 476 GUIDs against 2,694 text assets in Assets/ProjectSettings/Packages, 0 external references. Folder and `.meta` deleted, refresh clean, console 0 errors. Nothing local-only. Restore: `01_DOWNLOAD\AST-247.unitypackage` (same staging method as before) |
| 2026-10-05 | (tools) | local tools for the six above | **added** | `_Local/Editor/ShaderGraphToUrp.cs` (+ `ShaderGraphToUrpFix.cs`, one-off), `_Local/Editor/RenderAudit.cs` (renders prefabs/models/children into contact-sheet PNGs and counts magenta pixels), `AST-162/Editor/HdrpToUrpMaterialConvert.cs` now takes any folder. All conversions are in place (same GUIDs) and keep the original properties in the saved material. Input check: no scripts and no legacy `StandaloneInputModule` in the four demo scenes. Console: 0 errors |

### 2026-10-04 cleanup: removed from Playground (60 folders)

All were staged earlier for testing; "In Mr. Moonlight" is the spreadsheet value at that date. Restore any of them from
`01_DOWNLOAD\AST-###.zip/.unitypackage` unless the third column says otherwise (see `Docs/asset-import-update-process.md`, staging method).

| AST | Asset | Restore note |
|---|---|---|
| 001 | Gore Simulator | in Mr. Moonlight (ThirdParty) |
| 002 | InfiniCLOUD URP | in Mr. Moonlight |
| 003 | Dynamic Radial Masks | in Mr. Moonlight |
| 004 | Cartoon Rain & Blood Rain | in Mr. Moonlight |
| 005 | Shiny SSR 2 | |
| 006 | PIDI Planar Reflections 6 | |
| 007 | URP Mirror Shaders | |
| 008 | Guns Sounds | |
| 009 | Damage Numbers Pro | in Mr. Moonlight |
| 010 | Outdoor Atmospheres SFX | |
| 011 | Industrial Shipping Container Pack | |
| 012 | Controller Overlays & Button Kits | |
| 013 | Retarget Pro V5 | |
| 014 | Aged Medieval PBR Tools | |
| 015 | Ian's Fire Pack | in Mr. Moonlight |
| 016 | HE Abandoned Hospital v.2 | |
| 017 | Realistic Gun VFX | |
| 018 | Procedural Lightning | |
| 019 | Cult Animations | |
| 020 | Fly Particles + SFX | |
| 021 | Artistic: Radial Blur | |
| 023 | Spice Up: Ghost Vision | |
| 024 | Spice Up: Stoned | |
| 025 | Spice Up: Rain | |
| 026 | Knife MocapAnimPack | |
| 027 | Spice Up: BodyCam | |
| 028 | Asset Cleaner PRO | |
| 029 | Sounds Good | |
| 030 | Ambient Sounds | |
| 031 | Low Poly Plant Collections | in Mr. Moonlight |
| 032 | Volumetric Light Beam | |
| 033 | Flying Birds VFX | in Mr. Moonlight |
| 034 | Insect VFX | |
| 035 | HQ Realistic explosions | |
| 036 | Bullet Impact VFX + Decals | |
| 037 | Shots VFX URP | |
| 038 | FPS Engine | |
| 039 | Body Poser | |
| 041 | Blaze AI Engine | in Mr. Moonlight |
| 042 | A* Pathfinding Project Pro | |
| 043 | Northern Lights Pack | in Mr. Moonlight |
| 044 | Ultimate Animation Collection | in Mr. Moonlight |
| 046 | HQ FPS Weapons 2.0 | in Mr. Moonlight |
| 047 | TopDown Nature Library | in Mr. Moonlight |
| 048 | Wendigo Forest Beast Collection | |
| 049 | Gaia Pro VS | in Mr. Moonlight |
| 050 | Crest Water 5 | in Mr. Moonlight |
| 051 | Log Cabin | |
| 052 | URP Wet Shaders | |
| 053 | Altos Volumetric Clouds | |
| 054 | Skybox Blender | local-only files backed up (below) |
| 055 | Highlight Plus 2 | |
| 057 | Screenspace VFX | |
| 063 | Dissolve FX Master Kit | burn dissolve already ported (MRM-85 / C-013) |
| 086 | AllSky 220+ | **no zip in `01_DOWNLOAD`**: re-download from the Asset Store cache package `AllSky - 220 Sky Skybox Set.unitypackage` (5.55 GB, `%APPDATA%\Unity\Asset Store-5.x`) or Package Manager. The 27 act skies are already in Mr. Moonlight (`Docs/sky-catalog.md`) |
| 093 | Drunk Color Pulse | **no zip**; Asset Store cache has `Drunk Color Pulse Post-Processing Effect.unitypackage` |
| 094 | Sewer/Underground Modular Pack v4.0 | **no zip and not in the cache**: only the Asset Store / Package Manager can bring it back |
| 147 | Asset Optimizer Pro | |
| 164 | Ether Skyboxes | the 218 in-place cubemap conversions (13.6 GB) are gone; the 27 picked skies are in Mr. Moonlight. Re-running the converter (backed up) rebuilds them |
| 232 | Beach Bundle | |

### Local-only files backed up before deleting

`C:\Users\calva\Documents\Asset Collection\04_playground-archive\` (14 MB, 60 files): the whole `AST-054` folder (Skybox Reviewer scene,
`SkyboxReviewer.cs`, `SkyboxOrientationTool.cs`, `SkyboxReviewStatus.json` with the approve/reject picks, `SkyboxOrientation.json`, the modified
`SkyboxBlender.shader`) and the `Editor` folders of AST-164 (`EtherToSkyboxBlender.cs`) and AST-086 (`AllSkyOrphanConvert.cs`).
