# External assets — what this project depends on and how to restore it

**Nothing third-party is tracked in git.** Project policy (Carlos, 2026-08-25): the repo holds only
our own game content. This document is the record of everything external, so a clean machine can be
brought back to a working state.

> **Why the version numbers matter.** A `.unitypackage` embeds its own `.meta` files, so re-importing
> **the same version** restores **the same GUIDs** — and every reference from our tracked assets
> (scenes, prefabs, renderer assets) survives. Import a *different* version and Unity may assign new
> GUIDs, silently breaking those references. Treat the versions below as load-bearing.

## Restore procedure on a fresh clone

1. Open Package Manager → **My Assets**, download each package below (**Download**, not Import).
2. Import them to the paths in the table. Extract **lean** — strip `Demo`/`Samples~`/`Documentation~`
   folders, which are the bulk of the download and are never referenced.
3. Flora is a special case: its `.unitypackage` is a *bootstrapper*. The real package is nested at
   `Editor/Packages/com.ma.flora@<version>.unitypackage` — extract that and place it at
   `Packages/com.ma.flora/` as an embedded package.
4. If `Packages/packages-lock.json` still pins `"com.ma.flora": "file:com.ma.flora"` and the folder
   is missing, either reinstall Flora before opening the project or delete that entry and let Unity
   regenerate the lock.
5. Open the project and check the console for missing script references on `Island.unity` and
   `Assets/_Project/Settings/PC_Renderer.asset` — those are the two assets that reference third-party
   types by GUID.

## Load-bearing packages

These are referenced by GUID from tracked assets. **Without them the project does not work.**

| Package | Publisher | Version | Installs to | Referenced by |
|---|---|---|---|---|
| **Flora Renderer 6** | Magnetic Arcade | 6.3.35 | `Packages/com.ma.flora/` | `Island.unity` (Flora Scene Settings, Terrain Provider) |
| **HAZE – Volumetric Fog & Lighting for URP** | Harry Alisavakis | (as of 2026-08-25) | `Assets/ThirdParty/HAZE - Volumetric Fog & Lighting for URP/` | `PC_Renderer.asset` — guid `8cfd8a276368c7140bdbd86e806898a8` |
| **Retro Shaders Pro for URP** | Daniel Ilett | (as of 2026-08-25) | `Assets/ThirdParty/Retro Shaders Pro/` | `PC_Renderer.asset` — guid `790bcd5ee75b9fb4c997ed0938750856` |
| **Vegetation Spawner FREE** | Staggart Creations | (free) | `Assets/ThirdParty/VegetationSpawner/` | `Island.unity` — guid `1f710250abab6f24a954bdf3c3c1ac64` |
| **Simple Water Shader URP** | IgniteCoders | (free) | `Assets/ThirdParty/SimpleWaterShaderURP/` | `Island.unity` (Sea) |
| **Terrain Sample Asset Pack** | Unity Technologies | (free) | `Assets/ThirdParty/TerrainSampleAssets/` | MRM-70 TSA vegetation prefabs reference meshes in `Models/` |

### Extraction notes

- **Flora** — 232 MB full, **4.9 MB** installed. Drop `Samples~` (224.8 MB), `Documentation~`,
  `Tests`. Do **not** run the in-editor installer window; it imports everything.
- **HAZE** — ~9 MB full, **0.95 MB** installed. Drop `Demo`, user manual PDF.
- **Retro Shaders Pro** — ~95 MB full, **2.1 MB** installed. Drop `Demo` (92.8 MB) and README.
  **Never extract its bundled `Packages/manifest.json`** — it clobbers the project's.
  Decline the "install the additional Shader Graph package" popup unless authoring custom PSX shaders.
- **Vegetation Spawner FREE** — 3.5 MB full, **212 KB** installed. Keep `Runtime/`, `Editor/`,
  asmdefs; drop `_Demo/`.
- **Terrain Sample Asset Pack** — **1.9 GB**. This is the one genuinely large dependency.
  ⚠️ The MRM-70 TSA prefabs reference meshes *inside* it, which is fragile. Anything from it that
  actually ships should be copied into `Assets/_Project/Art/` with attribution.

## Art source packs (not in the Unity project)

Raw/prepared source art lives outside the project at `E:\Props\Environment\`, processed through
`Docs/3d-asset-pipeline.md`. Finished assets are copied into `Assets/_Project/Art/`, which **is**
tracked.

| Pack | Used for |
|---|---|
| Retro Realism – Lonely Forest | trees, boulders, logs, stumps, bushes, ferns; `BranchFir` / `TreesDead` textures |
| Grass Flowers Pack Free (ALP) | grass + flower billboard textures |
| Terrain Textures Pack Free (ALP) | ground textures |
| Yughues Free Ground Materials | ground textures |
| AllSky – 220 Sky Skybox Set (rpgwhitelock) | skyboxes — **ship 4 only** |
| Big Poplar Tree FREE (ALP) | excluded — 8,198–20,248 tris, over budget |

## Also acquired, not yet integrated

| Package | Publisher | Intended use |
|---|---|---|
| Advanced Horror FPS Kit | Queen | reference |
| Wolfenemu / 01 Monster Wolf Boss | AsAlex / HATOGAME | wolf enemy + boss — **staged in Playground 2026-08-28** at `Assets/Wolf_enemu/` and `Assets/Hatogame_new/BossMonsterPack1/Wolfboss/`; not yet evaluated or moved into Mr. Moonlight. See `Docs/dual-project-workflow.md`. |
| Procedural Water Shader | Pedro Verpha | evaluated, not used (MRM-68 chose Simple Water Shader) |
| **Gaia Pro VS** | Procedural Worlds | **Adopted 2026-08-27, editor-time tools only.** Terraform/erosion + Spawner/biome mask stacks, replacing our hand-rolled Painter/Composer. Not yet downloaded. See `Docs/terrain-vegetation-tooling-decision.md` §2 for the mandatory keep/drop boundary — **importing its Runtime, Water, Lighting or sample art is a mistake**, they collide with HAZE, Retro, Simple Water Shader and TimeManager. |
| **Crest Water 5** | Wave Harmonic | **Adopted 2026-08-27 for MRM-71** ($240, 60.6 MB, owned). Replaces Simple Water Shader URP on `M_Sea.mat`. **Installs to `Packages/` as a UPM package** — so unlike `Assets/ThirdParty/` packages it is **not gitignored and will be committed**, same as Flora; settle that deliberately (MRM-71 risk 1). `Samples~` do not import unless requested, so no lean-extraction needed. Underwater Renderer ships **disabled** (Tracey cannot enter water in the demo). Record the exact version on install. Not yet installed. |

## Evaluated and rejected — do not re-litigate

Recorded so these are not re-evaluated from scratch. Full reasoning in
`Docs/terrain-vegetation-tooling-decision.md`.

| Package | Publisher | Date | Why rejected |
|---|---|---|---|
| **MicroWorld – Procedural Terrain Generator** | Star Twinkle | 2026-08-27 | A procedural *level generator* — builds its own terrain cells; no existing-terrain or heightmap-import path. Cannot preserve the authored island shape, which is load-bearing for the already-designed gameplay audio. Its custom foliage shader would also collide with `RetroLit`. |
| **Nature Renderer 6・Pro** | Visual Design Cafe | 2026-08-27 | Requires shaders to support *its* procedural instancing and ships a shader patcher. `RetroLit` — the PSX look — is already BRG/DOTS-compatible, which is why Flora needed zero shader work. Adopting it means patching a paid third-party shader and re-patching on every Retro update, for a measured gain of zero over Flora. |
| **Crest Water 4 URP** | Wave Harmonic | 2026-08-27 | Superseded by **Crest 5**, which is the same lineage but 60.6 MB instead of 1.5 GB, ships as a UPM package, covers all three pipelines, and is actively maintained (5.10.0 Aug 2026 vs 4.23.1 Jul 2026). **Not an aesthetic call** — neither version is more PSX-friendly; Crest 5's extra realism is opt-in switches. |
| **KWS2 Dynamic Water System** | kripto289 | 2026-08-27 | Owned, and the stronger simulation (FFT ocean, flow sim, SSR, caustics, volumetric sunshafts, Snell's window) — but every one of those is a photoreal flourish that fights the PSX look, and is where its GPU cost lives. Right buy when water is a *character*; ours is scenery. Crest chosen instead. |
| **Big Poplar Tree FREE** | ALP | (earlier) | 8,198–20,248 tris, over budget. |

> **The test that decides any future vegetation renderer: does it work with `RetroLit` unmodified?**
> If it needs the shader patched, the answer is no.

## Keeping this current

Update this file whenever a package is added, removed, or upgraded — it is the only record. If you
upgrade a package version, expect to re-check the GUID references listed above.
