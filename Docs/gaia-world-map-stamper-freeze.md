# Gaia World Map Stamper: pink terrain preview, big box, editor freeze (2026-10-01)

**Symptom (Carlos, scene `02 Island`, zoomed far out in the Scene view):** a pink terrain-shaped surface and a huge
bounding box over the island. Clicking it **froze Unity**. Disabling `Gaia Runtime` in the Hierarchy **crashed** the editor.
Nothing in the visible Hierarchy was the source.

## Cause
Gaia's **World Designer** keeps a hidden child, **`World Map Stamper`** (a Gaia `Stamper` + `WorldMap`), saved *in the scene*:

```
Gaia Tools            (tag EditorOnly, visible)
  World Designer      (HideFlags 2 = HideInHierarchy)   WorldDesigner + WorldMap(heightmap 2049) + TerrainLoader
    World Map Stamper (HideFlags 1 on Transform)         Stamper, scale 100 x 7 x 100,
                                                         m_drawPreview 1, m_showBoundingBox 1, m_showSeaLevelPlane 1
```

- It draws a stamp-preview terrain (pink: Gaia's preview material has no URP-correct shader here) plus its bounding box, over a
  world-map area sized from `TerrainSceneStorage` (`m_worldMapPreviewHeightmapResolution 2049`, range/height 1024).
- It is **hidden from the Hierarchy**, so "disable it in the inspector" can't find it. Zoom out and click = Scene-view picking
  and selection of a ~4M-vertex preview mesh, which Gaia's Stamper editor tries to build a gizmo/inspector for. That is the freeze.
- `Gaia Runtime` holds the **Terrain Loader Manager**. Disabling it makes Gaia's editor tear down and rebuild its loaders while the
  preview objects above still exist. That is the crash. Not root-caused further; the rule is: **don't toggle `Gaia Runtime`**.
- The terrain itself is **not** affected. `Terrain_0_0-...` under `Gaia Terrains` is a normal `Terrain`.

## Where it exists
Identical stack in **`02 Island`, `06 Island_Legion`, `07 LightingTestScene`, `08 LightingTestScene Fog Experiment`**
(the lighting/Legion scenes were duplicated from the island). Scenes `01, 03, 04, 05` have no Gaia objects.
**Any future scene duplicated from these inherits it.**

## Fix applied (offline, Unity closed)
Script `strip_worldmap.py` (kept in the session scratchpad; logic below) edited the scene YAML directly, so no editor code touched
the objects. Per scene it deleted **40 objects**: GameObjects `World Designer` and `World Map Stamper` with their Transforms and
components (`WorldDesigner`, `WorldMap`, `TerrainLoader`, `Stamper`), the loose Gaia settings objects only they referenced
(26 `StamperSettings`, `WorldCreationSettings`, `WorldMapStampSettings`, `BaseTerrainSettings`, `NoiseSettings`,
`Default Gaia World Designer Settings(Clone)`) and the cached preview RenderTexture. The parent `Gaia Tools` child list was patched.
Checks: a dry run found **zero dangling references**; afterwards every parent/child/component fileID resolves; the git diff is
deletions only (~14.9k lines per scene, ~380 KB smaller).
Backups of all four scenes before the edit: scratchpad `backup/` (not in the repo, so use `git checkout -- <scene>` to revert 02/06/08;
07 had uncommitted changes of Carlos's, so a backup copy was kept for it).

**Left in place on purpose:** `Gaia Tools` (EditorOnly) with Session Manager, Stamper, Scanner and Custom Biome; `Gaia Runtime`
(Terrain Loader Manager, no terrain scenes registered); `Gaia Terrains`; `Gaia Game Object Spawns`. The regular `Stamper` also has
`m_drawPreview 1` / `m_showBoundingBox 1` (small, not the freeze). If it shows up, remove it the same way (offline).

## Rules going forward
1. Don't click or disable Gaia preview/loader objects in a scene with a 1024 m terrain. Remove Gaia tool objects **offline** (scene closed).
2. New scenes: don't duplicate from an island scene without removing `World Designer` / `World Map Stamper` first (or start from a scene already stripped).
3. If the pink box returns, look for hidden Gaia objects (`HideFlags` 1/2) in the scene YAML: `grep -n "m_Name: World" scene.unity`.

## Follow-up 2026-10-01: pink plane survived in scene 07, whole `Gaia Tools` removed there
After the first pass, scene 07 still showed a pink plane and a wireframe box. My earlier note that the regular `Stamper` was "small, not the freeze"
was **wrong**: it was never measured. Remaining culprits in `Gaia Tools`: `Stamper` (`m_drawPreview 1`, `m_showBoundingBox 1`, `m_showSeaLevelPlane 1`, scale ~15.9),
`Scanner` (at y=512, with its own MeshRenderer + mesh, very likely the pink plane) and `Custom Biome` (draws a box).
Removed **the whole `Gaia Tools` group from scene 07 only** (Stamper, Scanner, Custom Biome, Session Manager; 23 objects, the SceneRoots entry patched), offline with Unity closed.
Backup before this step: scratchpad `backup/07 LightingTestScene.before-gaiatools.unity`.
**Scenes 02, 06, 08 still have Gaia Tools** (without World Designer) and can show the same pink plane; clean them the same way when wanted.
Lesson: when hunting editor-preview junk, measure *every* Gaia tool object (scale, `m_drawPreview`, MeshRenderer) before ruling any out.

## Correction 2026-10-01 (later): the crash is a Scene-view GIZMO OVERFLOW, not Gaia's loader
**The first section above blames the World Map Stamper for the freeze. That was a hypothesis and it was wrong as the crash cause.** The pink plane (`Scanner` mesh at y=512)
and the preview objects were real noise and are removed from scene 07, but Carlos still crashed afterwards by clicking the white wire box.

Evidence, from `%LOCALAPPDATA%\Temp\Unity\Editor\Crashes\Crash_2026-10-01_*` (4 crashes in one hour; 52-58 repeats each):
```
Gizmo vertex count exceeds the maximum size: 38348667 > 38347922. Please draw fewer gizmos.
 ... UniversalRenderPipeline:RenderSingleCamera -> ScriptableRenderContext:Submit  -> native crash
 (called from SceneView:DoDrawCamera, i.e. the Scene view repaint)
```
Something in scene 07 draws ~38 MILLION gizmo vertices; every Scene-view repaint beyond Unity's cap crashes in `Submit`. Clicking/hovering in the Scene view just forces a repaint,
which is why it "freezes when I click the box". It is **not** a Gaia-specific crash, and disabling `Gaia Runtime` crashing is probably the same thing (a repaint).

What the wire box is (not yet pinned down). Scene 07 candidates with a 1024 m footprint: `NavMesh Surface` (volume 1024 x 67.5 x 1024, centre y 41.25),
`HAZE Explorable Area Fog` (HazeDensityVolume, scale 1050 x 180 x 2100), `Dynamic Culling` (DC_Controller, `DC_ControllerEditor` draws selection gizmos for NonSelected too),
and the cyan tile outlines (likely NavMesh/culling tiles). None is proven. Not caused by Gaia: the Terrain Loader Manager only draws when selected.
Also: Gaia **re-creates an empty `Gaia Tools` group in memory every time the scene opens** (it is not saved to the file, so it reappears in the Hierarchy; harmless).

Immediate mitigation (no scene change): Scene view toolbar -> **Gizmos toggle OFF**, or in its dropdown untick the offending component types, before zooming out.
Next step when Carlos agrees: bisect gizmo types through `GizmoUtility.SetGizmoEnabled` (editor prefs only) to find the offender.

## Result 2026-10-01: Dynamic Culling gizmos turned off; the box is the NavMesh Surface volume
Carlos clicked the wire box: it is the **`NavMesh Surface`** (volume 1024 x 67.5 x 1024). It was a victim, not the cause. Through the UnityMCP bridge I switched OFF the editor gizmo drawing of six
AST-145 types (`GizmoUtility.SetGizmoEnabled(type, false)`): `DC_Controller`, `DC_SourceSettings`, `DC_ActivateNearObjects`, `StaticCullingCamera`, `StaticCullingController`, `StaticCullingSource`.
After that Carlos could click the box without a crash. Likely mechanism (not proven by measurement): Dynamic Culling puts a `DC_SourceSettings` on every culled object (thousands of trees), so its per-object
bounds gizmos plus `DC_ControllerEditor`'s `[DrawGizmo(Selected | NonSelected)]` add up to tens of millions of vertices over the 1024 m island. **Only drawing was switched off; the components and culling still run.**
- Stored in `Library/AnnotationManager` (per project, git-ignored, **editor only, never in builds or scenes**). If `Library/` is deleted or the project is re-cloned, the gizmos come back and the crash returns.
- This applies to **every scene with a Dynamic Culling controller** (02, 06, 07, 08 and any new island scene), and any scene with Flora/Dynamic Culling at island scale.
- A crash at 08:55 the same day still carried the overflow message; whether the gizmos were off at that moment is unverified (bridge down). Re-check `Crash_*/Editor.log` for "Gizmo vertex count" if it recurs.
- Gaia also writes a new untracked `Assets/Gaia User Data/Sessions/GS-<date>.asset` every time the project opens (junk; leave out of commits).
