# Assets moved into Mr. Moonlight (catalog)

**Read this first when a chat asks "do we already have X?" or "where is prop Y?".** One row per asset pack, written 2026-10-10 at the end of the
Playground -> Mr. Moonlight prop-move work (the prop moves are FINISHED; this is the reference, not a to-do list).
Drag-and-drop = open the folder, drag the prefab into a scene.
**Review status (Carlos, 2026-10-10):** reviewed superficially: he test-placed a couple of assets from each pack in the editor and found them fine. Real staging
in the demo scenes is Carlos's own ongoing work (a few test placements are in scene 07; do not assume a pack is "placed" or "unplaced" from this file, look in the scene).
**Colliders (Carlos, 2026-10-10): NOT part of this move.** They will be handled in a SEPARATE chat while he stages the scenes, per prop, following `Docs/collider-policy.md`.
Every collider mentioned below is the vendor's own (copied untouched), except the AST-108 shed and AST-270 wells, which were evaluated earlier.
History, per-step detail and restore notes for every pack: `Docs/playground-asset-log.md`. Path rules: `Docs/folder-map.md`.
Process and traps: `Docs/asset-import-update-process.md` (incl. "Crash countermeasures"), `Docs/3d-prop-pipeline-wizard.md` (G21, G22).

## Conventions that hold for every pack below

- **Location:** `Assets/_Project/Art/Buildings & Props/AST-### (Short Name)/{Prefabs, Meshes, Materials, Textures}`, the prefabs live INSIDE the asset's own folder
  (Carlos's rule, 2026-10-09), named with the asset ID so a prefab can always be traced back to its pack. Exceptions: AST-070, AST-270 and the custom assets predate this rule
  (see their rows).
- **Material:** every converted material is `Retro Shaders Pro/Retro Lit` (samples ONLY BaseColor + Normal; AO is multiplied into the BaseColor; no emission, metallic or
  smoothness map). Exceptions are named in the rows (glass, decals and particles stay URP).
- **Textures:** BaseColor goes through the pixelation filter (`Tools/pipeline/texture_pass.py`), imported Point-filtered. Normals are never pixelated (bilinear, half size).
  The size was chosen per material from how big the objects using it are. **`MoonlightTextureImporter` clamps every BaseColor to 512 (normal 256) unless the folder is listed
  in `FolderCeilings` / `HeroEnvironmentFolders`**; if you add a texture folder bigger than 512, add it there and force-reimport, then read the IMPORTED width back.
- **GUIDs:** vendor prefab / FBX / material GUIDs were kept so nested prefabs keep resolving. Do not re-import a pack over these folders.
- **Colliders:** vendor colliders were copied UNTOUCHED unless a row says otherwise. `Docs/collider-policy.md` applies to any collider request (evaluate each prop first).
- **Playground copies** of every pack are still staged in `E:\playground\Playground\Assets\PLAYGROUND\AST-###` (removal only on Carlos's word).
- **Builders** (re-runnable, `dry` first): `Tools/pipeline/move_pack_to_moonlight_*.py`; check new folders with `Tools/pipeline/preflight_import.py`.

## The packs

| ID | Pack | Prefabs (where) | What it is | Textures (BaseColor) | Colliders / extras | Caveats |
|---|---|---|---|---|---|---|
| AST-070 | Old Wooden Church | `Prefabs/Buildings/Prop_WoodenChurch.prefab` (art: `Art/Buildings & Props/WoodenChurch`) | the demo's church, 16,160 tris, 15.7 x 28.3 x 30.5 m | 9 materials at 512 | vendor MeshCollider on the building + a box per door, doors ajar | awaiting Carlos's sign-off; interior furniture = AST-295 |
| AST-270 | Medieval Wells (props only) | `Prefabs/Buildings`, `Props`, `Sets` (42 `Prop_MVillage_*` + 23 `Set_MVillage_*`; art `Art/Buildings & Props/MedievalWells`) | wells, walls, stone blocks, coins, jars, driftwood | one 4096 atlas -> 1024 | exact LOD0 MeshCollider per prop (26,078 tris) | small clutter (coins, cups, rope) probably needs cheaper colliders: Carlos has not answered |
| custom | Custom assets (no pack) | `Prefabs/Custom Assets/Prop_PersianRug`, `Prop_Broom`, `Prop_Dustpan` | rug 1.5 x 2.6 m, broom, dustpan | rug 1024, others 512 | box colliders on broom and dustpan only | prop-log entry not written (waits for Carlos's sign-off) |
| AST-292 | Witch Village (Leartes) | `AST-292 (Witch Village)/Prefabs` (257 `SM_*`) and `/Particles` (5 `P_*`) | village kit: houses, walls, roofs, props, plants, rope-less | 132 textures at 512; 79 RetroLit + 4 URP particle materials + glass | vendor box colliders, not evaluated; 5 particle prefabs (candle flame, dust, fire, 2 smoke) | no moss/wind/water, plank graphs reduced to a trim-sheet colour, `M_WoodBowls_Red/yellow` tints invented; 11 `SM_RopeSplineMesh_*` need the vendor's spline script (not shipped) |
| AST-074 | Graveyard Tombstones (Lyrebird) | `AST-074 (Graveyard Tombstones)/Prefabs/{Bricks,Crosses,Headstones/*,Pillars,Sarcophagy}` (63, 9 are nested "combi" prefabs) | headstones, crosses, sarcophagi + lids, pillars, shrines, bricks | 9 materials: 512 (shared atlases), normals 256 | vendor 9 MeshCollider + 71 BoxCollider, not evaluated | the 17 headstones share ONE 512 atlas (looks coarse); vendor smoothness dropped |
| AST-108 | The Shed (Blackant) | `AST-108 (The Shed)/Prefabs/The Shed.prefab` (ONLY that prefab) | shed building + door (animated), tools, objects, ivy | 2048 (building, tools atlas), 1024 (objects); normals 512/512/256 | ADDED: shell + objects LOD0 MeshCollider, 5 boxes on the tool pieces; doorway verified open | the 11 flat-grey decal quads are deactivated (vendor shipped no decal texture); ivy flat green; 61k tris (tools mesh 38k); not static |
| AST-162 | Abandoned Swimming Pool (Leartes, URP) | `AST-162 (Swimming Pool)/Prefabs` (148) + **`Assembled/AbandonedPool_Assembled.prefab`** (the scene's whole Meshes object: 3,190 pieces) | an abandoned sports complex: walls, stands, pool, stairs, trees, grass, vehicles | 2048 for large structures, 1024 props, 512 where the source is 512 (trees/leaf cards) | vendor colliders untouched (assembled prefab: 3,148 mesh colliders, ~13.4k renderers, ~8.3M tris, 356 x 62 x 456 m = heavy, slow NavMesh bake) | not included: terrain, lights, probes, sky/fog, camera; dirt/blend layers lost; `M_Emissive` flat; glass + edge decal are URP/Lit transparent; `Cables` renders blank in a preview (thin) |
| AST-293 | Dead Bodies (Phoenix3D) | `AST-293 (Dead Bodies)/Prefabs` (156) | skeletons, bodies, body bags, mummies, cages, cells, severed parts, morgue table, skull walls | 1024 (body-sized, bone atlases), 512 (limbs, skulls), 256 (hands, feet) | none (vendor had none), 240 LOD groups | `Mega_Blood_Pack` NOT moved; `Severed_BodyParts_onTable_1` renders blank (vendor: children at scale 0); hair flat (no texture in the pack); DirectX normals were flipped; meshes heavy (`Severed_Bodies` 1.8M tris over its LODs) |
| AST-294 | Barricades (Sketchfab rip) | `AST-294 (Barricades)/Prefabs/Pieces` (124) and `/Structures` (26) | ONE skinned FBX of 339 meshes **split by us**: pieces = single objects, structures = pre-built barricades (hedgehogs, barbed-wire walls, sandbag stacks, tyre piles), numbered in lineup order | 2048 (blocks, ground, metal, pipes, wood), 1024 (rest), 512 (grid); AO baked in for blocks/bricks/sandbags | none; pivot on the ground at bounds centre | the original skinned FBX is NOT in Mr. Moonlight; meshes are 339 baked text `.asset` files (55 MB, not LFS); `M_Grid` is alpha-clip |
| AST-295 | Medieval Furniture (Hivemind, URP) | `AST-295 (Medieval Furniture)/Prefabs` (89 = 85 props + 4 `PS_*` particle prefabs) | church interior: pews, cabinets, tables, chairs, books, candle stands, chandelier, chalices, banners, paintings, carpet | 1024 (large pieces), 512 (small); normals 512/256 | 133 vendor colliders untouched; **6 realtime point lights, 3 with shadows** (see `lighting.md`) | candle emission dropped (RetroLit); the 4 `PS_*` particle prefabs + 3 materials + 3 textures are UNCHANGED vendor data (built-in particle shaders, still render); `Meshes/Materials` holds 33 Unity auto-generated `MI_*` materials (unused, harmless clutter) |
| AST-296 | PSX Hospital Church (itch.io) | `AST-296 (PSX Hospital Church)/Prefabs` (67) | low-poly props: first-aid box, drawers, tubs, tables, keys, shovel, doors, windows, wall pieces | 1024 (architecture atlas), 512 (3 props atlases); no normal maps | none | meshes are sub-assets of `Hospital.fbx`; the images are detailed (not low-res), the PSX look is the meshes |
| AST-297 | PSX Wooden Fences (itch.io) | `AST-297 (PSX Wooden Fences)/Prefabs` (71) | fences (whole + broken), gates, planks, debris, lamps, bird houses, bench, sign post | 512 (27), 256 (4), capped at each texture's effective resolution; no normal maps | none | prefabs use 71 baked mesh `.asset` files; their materials had to be extracted from the FBX in Playground first |

## Not moved / still in Playground only

- AST-295 `Meshes/Materials`: 51 unused pink HDRP leftovers (deletion suggested, no go given). AST-295 shaders and demo scene.
- AST-293 `Mega_Blood_Pack` (5 blood decal/stroke prefabs). AST-162 terrain, lights, probes. AST-294 original FBX.
- Still waiting for Carlos to name them: AST-076 Heavy Action Music (audio workflow), AST-131 Weeper Ghosts (character path), AST-298 Horror Sounds (audio import).
- No asset in this catalog has a `Docs/prop-log.md` entry yet (the wizard writes one only after Carlos signs a prop off; he has only reviewed them superficially).

## Facts other chats keep needing

- **Whole-island performance:** these are (almost) not staged in a scene yet, so none of the numbers in `performance-sessions.md` include them. When they are placed expect
  `[SCENE] CENSUS` renderers/colliders to jump and the occlusion + Flora bakes to need a re-bake (C-036, C-037 in `performance-sessions.md` section 8).
- **Import safety:** a heavy import with an unsaved scene is how work gets lost; the Mr. Moonlight editor froze 3 times on 2026-10-10 (all caused by us, see the crash
  countermeasures). Run `preflight_import.py` first, never import script-written YAML first in Mr. Moonlight.
- **Lighting:** any prefab that carries a Light must be listed in `Docs/lighting.md` (AST-295's six are).
