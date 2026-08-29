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
| **Burntwax FPS Engine** | Burntwax Collective | (as of 2026-08-29) | `Assets/ThirdParty/Burntwax Collective/` | `Player.prefab` — the whole player controller, weapon, pickup and pause stack (MRM-9) |

### Extraction notes

- **Flora** — 232 MB full, **4.9 MB** installed. Drop `Samples~` (224.8 MB), `Documentation~`,
  `Tests`. Do **not** run the in-editor installer window; it imports everything.
- **HAZE** — ~9 MB full, **0.95 MB** installed. Drop `Demo`, user manual PDF.
- **Retro Shaders Pro** — ~95 MB full, **2.1 MB** installed. Drop `Demo` (92.8 MB) and README.
  **Never extract its bundled `Packages/manifest.json`** — it clobbers the project's.
  Decline the "install the additional Shader Graph package" popup unless authoring custom PSX shaders.
- **Vegetation Spawner FREE** — 3.5 MB full, **212 KB** installed. Keep `Runtime/`, `Editor/`,
  asmdefs; drop `_Demo/`.
- **Burntwax FPS Engine** — 103 MB download, **73 MB** installed. ⚠️ **This is a Complete Project
  export, not a systems package: it ships all 19 `ProjectSettings/*.asset` files plus
  `Packages/manifest.json`.** Importing it through the normal dialog would clobber the URP renderer
  assignment, physics, and the tag/layer table. **Extract only the `Assets/` subtree** — read the
  `.unitypackage` out of the Asset Store cache and pull the paths you want, rather than using the
  import dialog at all (same technique as AllSky). It also requires **Cinemachine 3.1.7** and
  **Animation Rigging 1.4.1**, both installed via Package Manager. Wall-running, the save system and
  its menus were stripped; see `Docs/mrm9-burntwax-integration.md`.
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
| **Crest Water 5** | Wave Harmonic | **Installed 2026-08-29 for MRM-71** ($240, 60.6 MB, owned), package `com.waveharmonic.crest` **v5.9.2**. Replaces Simple Water Shader URP on `M_Sea.mat`. Installed **embedded** at `Packages/com.waveharmonic.crest/` (same mechanism as Flora — files copied in directly, no `manifest.json` entry needed, auto-discovered) — **not gitignored, will be committed**, same as Flora. Acquired via Playground (Carlos's Asset Store account) then file-copied over, since the paid UPM package can't be pulled by an automated agent. `Samples~` were not imported, per plan. Underwater Renderer explicitly set **disabled** (Crest ships it enabled by default — Tracey cannot enter water in the demo). **No renderer feature needed on `PC_Renderer`** — Crest 5 has no `ScriptableRendererFeature` class at all, it injects via `RenderPipelineManager` callbacks, so MRM-71 risk/gap #2 (renderer feature ordering) doesn't apply. |

## Adopted in the 2026-08-27 triage — owned, not yet installed

Full reasoning and a per-asset integration brief: **`Docs/new-asset-list.md`**. Listed here so this
file stays the single register of what the project depends on. **None is installed yet.** Record the
exact version in the tables above when each one lands.

| Package | Publisher | For | Milestone |
|---|---|---|---|
| **A* Pathfinding Project Pro** | Aron Granberg | MRM-27 — resolves the NavMesh-vs-A* decision. Bound the Recast graph to the walkable area | **M1** |
| **HQ FPS Weapons 2.0** | — | MRM-22/23/24/25/34/52 — models + weapon/hand animations. **Art only; import no scripts** | **M1** |
| **Sounds Good** | Melenitas Dev | MRM-38 — playback/pool backend. Our layer + distance gating stays on top | **M1** |
| **Spice Up: Bodycam** | Fronkon Games | MRM-49 — telescope aperture | **M1** |
| **Gaia Pro VS** | Procedural Worlds | MRM-70 — editor-time only, temporary, **after Sept 1**. Core `Gaia.unitypackage` only (no Stamps — no new landmass needed) moved into `Assets/ThirdParty/Gaia Pro VS/` 2026-08-28, not yet imported. Exact version TBD — check Package Manager → My Assets and record here. See `Docs/mrm70-gaia-kickoff.md` | M2 |
| **Crest Water 5** | Wave Harmonic | MRM-71 — multiple Water Bodies confirmed (sea/shore/rivers/lake) | M2 |
| **Wendigo Forest Beast Collection** | — | MRM-36 — the boss model. Name stays "Furman" | M2 |
| **Ultimate Animation Collection** | — | MRM-31/29/35/37 — humanoid mocap. ⚠️ import selectively | M2 |
| **Cult Animations · Knife MocapAnimPack · AnimSet 2-Handed Melee** | — | MRM-59 · MRM-35 · MRM-23 | M2 |
| **Gore Simulator + Blood Factory** | — | MRM-32, MRM-36 — dismemberment + spatter | M2 |
| **Spice Up: Rain / Stoned / Ghost Vision** | Fronkon Games | MRM-53 · MRM-55 (weed) · MRM-55 (morphine) | M2 |
| **Artistic Radial Blur** | Fronkon Games | MRM-54 fear + MRM-53 low health — **one shared channel** | M2 |
| **Drunk Color Pulse** | — | MRM-55 (drunk) | M2 |
| **Procedural Lightning** | Digital Ruby | MRM-57, MRM-36 — bolt only | M2 |
| **HQ Realistic Explosions · Ian's Fire Pack · Northern Lights · Insect VFX · Fly Particle System · Birds (Fab)** | — | MRM-57 · MRM-61 · MRM-47 · MRM-60/61 · MRM-60 · MRM-59 | M2 |
| **Bullet Impact VFX + Decals** | — | MRM-22/24 — impacts + decals. Shares a surface lookup with MRM-39 | M2 |
| **Realistic Gun VFX** *(or Shots VFX URP)* | — | Muzzle flashes only. **Both owned — pick one visually, ship one** | M2 |
| **Sewer Underground Modular · Abandoned Hospital v2 · Shipping Containers** | — | MRM-60 (the infirmary is inside the mine) | M2 |
| **Aged Medieval PBR Tools** | — | MRM-35 — the sickle only | M2 |
| **Outdoor / Underground Atmospheres SFX** | — | MRM-38 sound layers. ⚠️ import compressed, never WAV | M2 |
| **URP Wet Shaders** | — | MRM-18 main-menu staging | M2 |
| **Body Poser** | — | MRM-59 staging. **Editor-only, zero runtime footprint** | M2 |
| **Asset Cleaner Pro** | — | MRM-64. ⚠️ **find-references ONLY, never bulk-delete** | M2 |

### Parked — owned, conditional, do not install yet

| Package | Blocked on |
|---|---|
| **Blaze AI Engine** | NavMesh-based, so mutually exclusive with A* PP. Only reopens if its manual documents an A* PP integration |
| **Altos Volumetric Clouds** | Does it render *over* a skybox, or *is* it the skybox? If the latter it collides with AllSky + `SkyboxSwitcher` + `TimeManager` at once. ✅ Usable on the main menu either way |
| **Volumetric Light Beam** | Test HAZE alone on the flashlight first — two volumetric systems may overlap |
| **Skybox Blender** | MRM-47 deliberately chose instant, story-hidden skybox swaps. Solves a problem the design removed |
| **Shots VFX URP** | The alternative to Realistic Gun VFX. Keep whichever looks better; ship one |

### Rejected in the 2026-08-27 triage

| Package | Why |
|---|---|
| **FPS Engine** (cowsins) | Would reopen MRM-8/9/12/16/17/41/42 (five already Done) and, decisively, **fights the "Tracey must see her own feet" requirement** — every FPS template is built around an arms-only viewmodel. Staged in Playground as a **read-only reference and parts donor**; none of its code enters the project. **FPS Animation Baker Toolkit therefore stays.** |
| **Crystal Save** | The demo needs in-session respawn checkpoints, not serialization. Revisit post-demo |
| **Evo Localization** / **I2 Localization** | Our text is already spreadsheet + ID keyed with `text_en`/`text_es`/`text_ru` columns — the data layer already *is* a localization system. Adopting either adds a second data model. (I2 is the better of the two if ever needed) |
| **Event Manager** / **Game Event Hub** | A string-keyed bus would *reduce* readability in a 55-file codebase whose call graph is currently greppable, and neither helps MRM-11, which is a sequencer not a pub/sub bus |
| **Ambient Sounds** (Procedural Worlds) | MRM-38's sound-layer system already is a zone system. **The design idea was adopted; the asset was not** |

---

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
