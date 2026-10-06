# Folder map: where things live

**Decided 2026-10-03 (Carlos). Committed on `mrm-88` in `e5f3941d` (2026-10-05), change record C-021.** Replaces the old "Folder structure" and "Art folder breakdown"
rules in `unity-conventions.md`. The aim: **one folder for level design** with semantic names, few folders, and no vendor or
renderer names.

## The two rules

1. **Every drag-and-drop prefab lives in `Assets/_Project/Prefabs/`**, in one semantic folder. If you place it in a scene, it is here.
2. **The art that makes the prefab (FBX, textures, materials) lives in `Assets/_Project/Art/`**, one folder per subject, grouped by what
   it is. You rarely need to open Art while placing things.

## Prefabs: the level-design folder

```
Prefabs/
  Buildings/                 church, wells, well walls, stone platforms, porch roof (19)
  Props/                     set dressing: lamp, buckets, jars, coins, rope, shovel, vases (16)
  Sets/                      ready-made groups: well compounds, jar and coin groups, tree sets (23)
  Nature/
    Trees/                   trees WITH exact wood colliders (83). The ones to drag
    Rocks & Logs/            boulders, field stones, stumps, logs, fallen trees, driftwood, trunks (35)
    Grass & Plants/          grass, flowers, ferns, bushes, saplings, mushrooms (111)
    Old (Rough Colliders)/   Old_* : older copies with ONE rough capsule/box collider (112). See below
  Weapons/                   Melee/ Pistol/ Rifle/ Shotgun/ Precision/ Throwable/ Item/ (17, unchanged)
  Characters/                Player_Tracey, Enemy_Spotter (+ future enemies)
  Sky & Lighting/            SUN, TimeManager, SkyboxSwitcher, Prop_Moon
  VFX/                       TreeFire_FX, VFX_Flare
  UI/                        FPS Counter, main-menu feathers
  Dev & Tests/               test prefabs, Scene Effects Toggle, Player_Legacy_PreMRM9, Technie Grass Copies/
```

### `Old_` prefabs (the rough-collider duplicates)

The vegetation existed twice with the same names: the originals, and a copy made in Sept 2026 to test **Technie Collider Creator
(AST-116)**. The copy got exact wood MeshColliders (`WoodColliderTool`, MRM-84) and is now the one to use (`Nature/Trees`,
`Nature/Rocks & Logs`). The originals carry a single rough capsule or box collider. **Carlos decided not to delete them**: they
were renamed `Old_<name>` and parked in `Nature/Old (Rough Colliders)/` so the duplication is visible. They are **still in use**:
`02 Island` (about 5,990 Gaia-spawned instances), `03 MainMenu` and `07 LightingTestScene` reference them by GUID. Repointing
those scenes to the collider versions is a possible follow-up; until then, do not delete them.

The 42 grass copies from the same test have no colliders (identical to the originals) and are used only by
`05 VegetationGallery_TechnieColliderTest`, so they sit in `Dev & Tests/Technie Grass Copies/`.

## Art

```
Art/
  Buildings & Props/   WoodenChurch, MedievalWells, Lamp, EagleFeather, SparrowFeather
  Nature/              vegetation source art: Trees & Rocks (was TopDownNature), Retro Forest (was RetroRealism),
                       Grass & Flowers (was GrassFlowers), Bushes & Grass (was TerrainSampleAssets),
                       Low Poly Plants, Grass Detail
    Tree Colliders/    Wood Meshes (WoodColliderTool output) + Technie Paint (Carlos's paint data, MUST be kept)
  Terrain/             terrain textures and layers, M_IslandTerrain, Backups
  Sky & Water/         Skies, Skyboxes, Moon, Water
  Characters/ Enemies/ Weapons/   one subfolder per subject, created when the asset is made (no empty placeholders)
  UI/                  menus, fonts, HUD (Veins), title cards
  VFX/                 Dissolve, Flare, Shared
  Materials/           four flat debug colours
```

## Where a new thing goes

| New thing | Prefab | Art |
|---|---|---|
| Static prop (`/prop`) | `Prefabs/Props/` (or `Buildings/` if you walk into or around it) | `Art/Buildings & Props/<Name>/` |
| Tree, rock, plant | `Prefabs/Nature/<Trees, Rocks & Logs or Grass & Plants>/` | `Art/Nature/<pack>/` |
| Weapon or held item | `Prefabs/Weapons/<Category>/` (see `hands-items-and-weapons-pipeline.md`) | `Art/Weapons/<Name>/` |
| Enemy or character | `Prefabs/Characters/` | `Art/Enemies/<Name>/` or `Art/Characters/<Name>/` |
| Light, sky, time system | `Prefabs/Sky & Lighting/` | `Art/Sky & Water/` |
| Particle / effect | `Prefabs/VFX/` | `Art/VFX/` |
| Test or debug prefab | `Prefabs/Dev & Tests/` | n/a |

Don't create a new top-level folder for one asset. Fold it into the nearest category; add a folder only once a category clearly
holds a different kind of thing.

## ThirdParty (git-ignored vendor content)

Folders are named **`AST-### (Short Name)`**: the code first (the spreadsheet and `asset-import-update-process.md` look it up by
code), then one or two words so you don't need the spreadsheet. Renamed 2026-10-03:

`AST-001 (Gore Simulator)` `AST-002 (InfiniCloud)` `AST-004 (Cartoon Rain)` `AST-009 (Damage Numbers)` `AST-015 (Fire Pack)`
`AST-033 (Flying Birds)` `AST-040 (Blood Factory)` `AST-043 (Northern Lights)` `AST-044 (Animation Collection)` `AST-046 (HQ FPS)`
`AST-078 (HAZE Fog)` `AST-079 (Retro Shaders)` `AST-083 (Water Shader)` `AST-084 (Terrain Pack)` `AST-145 (Culling System)`
`AST-282 (Volumetric Fog)`.

**Not renamed:** `AST-049` (Gaia: it locates its own install, so it stays as is), `AST-085` (empty, Burntwax was removed),
embedded packages under `Packages/` (SmartBuilder, Flora, Crest, Febucci: UPM or hardcoded paths), and `Code/Vendor/` (script
logic, out of scope).

New ThirdParty imports: name the folder `AST-### (Short Name)` from the start.

## Not moved (they mean something to Unity or Gaia)

`Assets/Resources`, `Assets/Plugins`, `Assets/Gaia User Data`, `Assets/Procedural Worlds`, `Assets/TextMesh Pro`,
`Assets/Technie`, `Assets/InputSystem_Actions.inputactions`, every `Resources/` folder under `Code/` and `Data/`.

## Old path -> new path (for reading older docs)

Docs written before 2026-10-03 quote the old paths. They were not rewritten (history); use this table.

| Old | New |
|---|---|
| `Art/VegetationPrefabs/AST116_ColliderTest/<tree>.prefab` | `Prefabs/Nature/Trees/` or `Prefabs/Nature/Rocks & Logs/` |
| `Art/VegetationPrefabs/AST116_ColliderTest/RetroRealism/` | `Prefabs/Nature/Trees`, `Rocks & Logs`, `Grass & Plants` (bushes) |
| `Art/VegetationPrefabs/AST116_ColliderTest/GRASS PREFABS/` | `Prefabs/Dev & Tests/Technie Grass Copies/` |
| `Art/VegetationPrefabs/AST116_ColliderTest/WoodColliders/` | `Art/Nature/Tree Colliders/Wood Meshes/` |
| `Art/VegetationPrefabs/AST116_ColliderTest/Physics Hulls/` | `Art/Nature/Tree Colliders/Technie Paint/` |
| `Art/VegetationPrefabs/<tree>.prefab` | `Prefabs/Nature/Old (Rough Colliders)/Old_<tree>.prefab` |
| `Art/VegetationPrefabs/GRASS PREFABS/` | `Prefabs/Nature/Grass & Plants/` |
| `Prefabs/World/Vegetation/RetroRealism/RF_*` | `Prefabs/Nature/Old (Rough Colliders)/Old_RF_*` (Fern, Sapling: `Grass & Plants/`) |
| `Prefabs/World/Vegetation/GrassFlowers/`, `.../TerrainSampleAssets/` | `Prefabs/Nature/Grass & Plants/` |
| `Prefabs/World/MedievalWells/` | `Prefabs/Buildings/`, `Prefabs/Props/`, `Prefabs/Nature/Rocks & Logs/` (driftwood, trunks) |
| `Prefabs/World/MedievalWells/Sets/` | `Prefabs/Sets/` |
| `Prefabs/World/Prop_WoodenChurch`, `Prop_Lamp` | `Prefabs/Buildings/`, `Prefabs/Props/` |
| `Prefabs/World/SUN`, `TimeManager`, `SkyboxSwitcher`, `Prop_Moon` | `Prefabs/Sky & Lighting/` |
| `Prefabs/Player/`, `Prefabs/Enemies/` | `Prefabs/Characters/` |
| `Prefabs/MainMenu/` (feathers) | `Prefabs/UI/` |
| `Prefabs/DevTools/`, `Prefabs/Legacy/`, loose test prefabs | `Prefabs/Dev & Tests/` |
| `Art/Environment/Terrain/` | `Art/Terrain/` |
| `Art/Environment/Vegetation/<pack>/` | `Art/Nature/<new pack name>/` (see Art above) |
| `Art/Environment/Skies`, `Skyboxes`, `Moon`, `Water` | `Art/Sky & Water/...` |
| `Art/Environment/WoodenChurch`, `MedievalWells`, `Art/Props/*` | `Art/Buildings & Props/...` |
| `Art/HUD Textures/` | `Art/UI/HUD/` |
| `ThirdParty/AST-###/` | `ThirdParty/AST-### (Short Name)/` (except 049, 085) |

## Cleanup (2026-10-05)

The 10 emptied old folders were deleted (Art/Environment, Art/Props, Art/HUD Textures, Art/VegetationPrefabs, Prefabs/World, Player,
Enemies, MainMenu, DevTools, Legacy), plus 7 empty placeholder folders from the August scaffolding (Art/Enemies/Furman, Wolf, Zealot,
Art/Items, Art/Weapons/Pickaxe, Pistol, Turret). Each was re-checked for files first; nothing referenced them. `Audio/VO` is kept (the
dialogue category). Rule from now on: no empty placeholder folders; the `/prop` wizard creates a subject folder when it makes the asset.

## How it was done (so it can be repeated safely)

Every move went through Unity (`AssetDatabase.MoveAsset`), so GUIDs survive and scenes, prefabs and materials keep their links.
Verified: all 6,551 GUIDs under `_Project` and `ThirdParty` present before and after (only the 16 new folders added); console clean
after a recompile; `WoodColliderTool.PaintedPrefabs()` = 109 with every wood mesh attached. Editor tools with hardcoded folders were
updated in the same change (`WoodColliderTool`, `TreeColliderTool`, `TreeColliderReset`, `AST116TechnieBatchCollider`,
`VegetationMaterialFix`, `VegetationTerrainPrep`, `PSXMaterialMigration`, `CorpseDissolveTool`, `PolymindPlayerBuild`,
`MoonlightWeaponSetBuild`, the AST-046 paths in the weapon/arms migration scripts, `Tools/vegetation/measure_prefabs.cs`).
