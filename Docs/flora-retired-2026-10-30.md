# Flora retired, replaced by BRG Instanced Renderer (2026-10-30)

Carlos's decision, 2026-10-30, after build 61: **AST-314 BRG Instanced Renderer replaces Flora in every island
scene (02, 06, 07, 08, plus the 09 trial).** This file records what Flora was and did, why it was replaced, and how
to bring it back if we ever need to. The BRG side is in `Docs/brg-options.md`; the decision is D8 in
`Docs/optimization-decisions-2026-10.md`; change records C-039 (trial), C-041 (profile + rollout) in
`Docs/performance-sessions.md` §8.

## 1. What Flora was in this project

- **Package:** `Packages/com.ma.flora` (MA Flora, namespace `MA.Flora`). Still installed. Nothing was deleted
  (standing rule), so it can come back without a reinstall.
- **Adopted** under MRM-70 (vegetation, 2026-08-31 "standard vegetation pass") to draw Gaia's spawns fast:
  - Collidable species (trees, rocks, logs) were real GameObjects under `<Terrain>/Gaia Game Object Spawns/<species>/_Sp_*/Visual`.
    Each `Visual` carried a **`FloraInstanceRenderer`**, which hid the MeshRenderer (`forceRenderingOff`, edit and play mode)
    and drew it instanced. The colliders (`WoodCollider`, MRM-84) stayed on the GameObjects.
  - Decorative species (grass, ferns, flowers) went through Unity's terrain **detail** system, which Flora accelerated via the
    **`FloraTerrainProvider`** on the terrain and the **`Flora Scene Settings`** object (`FloraSceneSettings`,
    `EnableTerrainFoliage` + `AutoRegisterTerrains`).
  - It needed **BatchRendererGroup Variants = Keep All** (`m_BrgStripping: 2`, commit `21240544`) or the trees vanished in builds.
- **Per scene before the switch:** 07, 06, 08: 8,786 `FloraInstanceRenderer` each; 02: 5,990. One provider and one settings object per scene.
- **Editor tooling built for it:** `Code/Editor/FloraInstanceRendererPass.cs` adds `FloraInstanceRenderer` to every Gaia spawn.
  It was the last step of the "standard vegetation pass" / "generate new Gaia" procedure. **Do not run it any more:** after a
  new Gaia spawn, run `Mr. Moonlight/BRG/Convert Open Scene From Flora` instead (it works on a scene with no Flora too: it adds
  the BRG components and profile). The file stays (never delete without Carlos's go).
- **Known Flora bug, never fixed:** phantom tree shapes floating over the water with `EnableRendering` on (memory `mrm70_flora_rendering_bug`).

## 2. Flora's best numbers (for comparison)

| Build | Scene | fps first 50 s | 1% low | CPU / GPU ms | Notes |
|---|---|---|---|---|---|
| 55 / 56 (Flora Island / Flora Keepall) | 07 | **160.9** (build 56) | 126.4 | 6.21 / 4.51 | fog and CRT off; Keep All fixed invisible trees (GPU -31% vs no Flora) |

## 3. Why it was replaced

| | Flora (build 56) | BRG trial (build 60) | **BRG profile v1 (build 61)** |
|---|---|---|---|
| fps, first 50 s | 160.9 | 200.6 | **251.0** |
| 1% low | 126.4 | 141.8 | **203.0** |
| CPU / GPU ms | 6.21 / 4.51 | 4.98 / 4.73 | **~3.9 / ~3.5** |

Builds 60 and 61 ran with HAZE fog and the CRT on; build 56 had both off. BRG also gave controls Flora lacks: GPU (HZB)
occlusion, shadow-caster occlusion, per-group live density / LOD bias / shadow distance, distance thinning, per-camera
exclusion and LOD bias. Carlos saw no visual difference in build 61 ("to the human eye it looks kind of the same").

**Trap that matters if Flora ever returns:** 48 of the island's 90 detail layers use prefabs whose renderer sits on a
**child** object. Unity's detail system, and so Flora, silently drew **none** of them. BRG draws them unless
`TerrainBRGRegisterer.useUnityTransforms` is on (it is, so BRG draws exactly Flora's set). Switching back to Flora
keeps those 48 layers invisible, as they always were.

## 4. How the switch was made

`Mr. Moonlight/BRG/Convert Open Scene From Flora` (`Code/Editor/BRG/BRGTrialSetup.cs`), run once per scene on 2026-10-30:
1. every `MA.Flora` component removed (instance renderers, terrain provider, the `Flora Scene Settings` object); `forceRenderingOff` cleared;
2. `BRGMeshLodData` on every tree/rock renderer; `BRGGameObjectGroup` on `Gaia Game Object Spawns` (Static Multi Chunk 256 m,
   detect prefab overrides), converted; `TerrainBRGRegisterer` (useUnityTransforms on) on the terrain;
3. profile v1 (`brg-options.md` §5): groups and baked options, overlay camera excluded, player camera LOD bias 0.6; F11 bench object added.

## 5. How to go back to Flora

1. **Scene files:** the four scenes as they were just before the switch were backed up to the session scratchpad
   (`scene-backups-before-brg/`), which is temporary. **The durable copy is git:** commit `11a1ba2c` (and every earlier
   commit) has 02, 06, 07, 08 with Flora. `git checkout 11a1ba2c -- "Assets/_Project/Scenes/07 LightingTestScene.unity"` (etc.)
   restores one, but loses any later edits to that scene.
2. **Or re-add Flora by hand:** remove `BRGGameObjectGroup` (Revert first), `TerrainBRGRegisterer`, the `BRGCameraSettings`;
   add `FloraInstanceRenderer` to each `Visual`, `FloraTerrainProvider` to the terrain and a `Flora Scene Settings` object
   (copy the settings from git history). Leaving `BRGMeshLodData` / `BRGPrototypeExtraData` is harmless.
3. Keep **BatchRendererGroup Variants = Keep All** either way (both need it).
4. The 78 detail prefabs carry a `BRGPrototypeExtraData`. It is inert for Flora.
