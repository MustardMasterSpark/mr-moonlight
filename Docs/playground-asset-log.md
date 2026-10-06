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
