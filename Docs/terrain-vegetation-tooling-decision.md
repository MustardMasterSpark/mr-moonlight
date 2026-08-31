# Terrain & vegetation tooling — decision record

**Decided 2026-08-27 (Carlos + Claude).** Sits alongside `Docs/pc-build-target.md` §6-7 and
`Docs/mrm70-biome-vegetation-strategy.md`.

This document exists so these decisions are not re-litigated. If a Linear issue disagrees with it,
the issue wins — but check the date first.

> **Process note.** These are architecture-level decisions, not issue work, so they were written
> straight to `main` rather than on an issue branch — Carlos's explicit call, 2026-08-27. The
> normal one-issue-one-branch rule is unaffected.
>
> **Nothing here is linked to a Linear issue yet, deliberately.** Gaia is *not* attached to the
> vegetation story. A separate triage pass will map newly-acquired Asset Store packages onto issues
> and produce per-issue setup plans; until then these decisions stand on their own.

---

## The decisions, in one table

| Asset | Verdict | Why, in one line |
|---|---|---|
| **Gaia Pro VS** (Procedural Worlds, 4.8 GB) | ✅ **Adopt — editor-time tools only, installed temporarily** | Only tool evaluated that can spawn onto our *existing* terrain and drive biomes from an image mask (see the 2c gap list — our `biomes.png` needs re-projecting first) |
| **MicroWorld** (Star Twinkle, $140, 440 MB) | ❌ **Reject** | A procedural *level generator* — it builds its own terrain; cannot preserve our authored heightmap |
| **Nature Renderer 6 Pro** (Visual Design Cafe, $120, 377 MB) | ❌ **Reject** | Requires patching `RetroLit`, the shader that *is* our PSX look; Flora needed zero shader work |
| **Flora Renderer 6** (Magnetic Arcade) | ✅ **Keep — no change** | 38,980 → 535 draw calls, ~505 FPS, collision verified. Nothing left to solve |

> ### The art direction does not change
>
> **PSX / low-poly stays exactly as it is.** Gaia is a *placement* tool: it writes terrain data
> (heightmap, splatmap, tree instances, detail layers) and never touches materials or shaders.
> Everything it places is still rendered by our `RetroLit` / `RetroTerrainLit` materials, with the
> approved profile — Point Filtering, view-space Vertex Snapping, **Affine Texture Strength 1** —
> already baked into `PSXMaterialMigration.cs` as the tool's defaults, so new materials land there
> automatically. Declining Gaia Lighting / Water / skies (below) is precisely what protects this.

---

## 1. The build-size context these decisions sit inside

Measured 2026-08-27 against `E:\Builds\21 - PSX Materials - 2026-08-25`:

| | Size | % of itch.io's 1 GB limit |
|---|---|---|
| `Build.zip` | **54 MB** | ~5% |
| Raw build folder | **178 MB** | — |
| `Assets/` on disk | **2.2 GB** | *not shipped* |

1.9 GB of that `Assets/` figure is the Terrain Sample Asset Pack, most of which never enters a build.

> **Project-folder size is not build size.** Unity ships only referenced assets, and textures
> compress to BC/DXT on import. Do not reject a tool on download size — judge it on what it makes us
> *ship*. A 4.8 GB editor-time tool can cost ~0 MB in the build.

The 1 GB ceiling remains real (it is itch.io's upload limit, and the "playing within 2 minutes"
graded criterion still exists) but it is no longer the binding constraint on tool choice.

---

## 2. Gaia Pro — what we take and what we decline

### Why Gaia and not MicroWorld

The requirement that decided it: **preserve the existing island heightmap.** The terrain shape is
load-bearing for gameplay and for audio design already built against it.

MicroWorld generates connected terrain cells, procedural buildings, and automatic roads — its value
is *generating* a world you did not author. Neither its store listing nor its Unity forum thread
mentions existing-terrain or heightmap-import support, and it renders foliage through its own custom
foliage shader, which would collide with our `RetroLit` material stack. Wrong tool for this job.

Gaia, by contrast:

- **Reads our existing terrain.** `Spawner → Advanced → Resource Management → Import Terrain
  Resources` turns each texture already on the terrain into a spawn rule. Our 8 painted TerrainLayers
  survive.
- **Drives biomes from an image mask.** Gaia biomes and spawn rules accept an **Image Mask**, and
  can stack height / slope / noise / distance / collision masks on top of it. A real upgrade over
  our `Painter`/`Composer` polygons.
  > **Corrected 2026-08-27.** An earlier draft of this doc said Gaia takes `biomes.png` *directly*.
  > It does not - **our PNG is not usable as-is.** Per `Docs/mrm70-biome-vegetation-strategy.md`,
  > `biomes.png` is a **scene-view screenshot, not a top-down map**, which is exactly why it had to
  > be hand-anchored against nine landmarks the first time. An Image Mask needs a PNG registered to
  > terrain coordinates - correct orthographic projection, correct aspect for a 4103 x 7085 m
  > terrain. **Producing that properly-projected mask is a prerequisite step, not free.** An
  > orthographic top-down render of the terrain gives the registration. Budget it into the pass.
- **Improves shape without replacing it.** Gaia's erosion and terrain-enhancer tools run on an
  existing heightmap as a *modifier* — thermal/hydraulic erosion, sediment, terracing. This is the
  "keep the silhouette, make the surface believable" pass, and it is the lowest-risk part of Gaia
  because it touches only the heightmap.
- **Takes our own props.** Gaia biomes are collections of spawn rules pointing at prefabs. Swapping
  in our Retro Realism trees is the documented workflow, not a workaround.
- **Composes with Flora.** Gaia's editor-time output is ordinary Unity terrain data, which is exactly
  what `FloraTerrainProvider` reads. **Gaia places, Flora draws.** No conflict.

### The scope boundary — take the left column, decline the right

| ✅ Take | ❌ Decline |
|---|---|
| Terraform / erosion / stamps | Gaia Runtime + Terrain Loader (world streaming) |
| Spawners, biomes, mask stacks | Gaia Water |
| Resource import from existing terrain | Gaia Lighting / skies / weather |
| | All sample biomes, demo scenes, sample art |

> **Correction, 2026-08-28.** Carlos confirmed the goal precisely: *"add a better realistic
> terrain without changing the shape."* That descopes **`Gaia/Stamps.unitypackage`** (393 MB) —
> its own package description says it's only for adding new landmass shapes ("without
> stamps you cannot create mountains or valleys, etc."). We aren't creating new landmass, we're
> refining our existing one, so **only `Gaia/Gaia.unitypackage` (5.4 MB core) was moved into Mr.
> Moonlight**, not Stamps. The Stamper tool itself (in the core package) still works on our
> existing terrain via its **Shape Input = existing terrain** mode; it just doesn't need the
> pre-made mountain/valley image library to do erosion/effects passes.
>
> **Also confirmed 2026-08-28: our own textures, our own props.** Gaia's auto-texturing (its
> biome "Textures" spawner) and its sample/Gaia Pro prefab content (`Asset Samples`, `Asset
> Samples - Synty Studios`, `Gaia Pro Assets and Biomes`) were already declined above for style
> reasons — this confirms that stands. Gaia's *Spawner* is still the tool for Phase 2 (it places
> **our** Retro Realism prefabs by rule; it doesn't ship them). Worth a look purely for variety,
> not for adoption as-is: some of Gaia's stock stamp/sample trees (pines, etc.) *could* be
> cherry-picked later if a specific species is missing from our own asset list — but the sample
> packs are explicitly "wrong style" (low-poly PSX vs. Gaia's more realistic defaults), so the
> default is **no**, don't pull them in without a specific gap to fill.
>
> **First invocation of that clause, 2026-08-31.** The specific gap was ground-detail grass: Mr.
> Moonlight's Gaia install ships zero grass art, and `Gaia Pro Assets and Biomes` holds 21 detail
> meshes and 16 billboard cards. 14 files were extracted by GUID (never installed), pixelated
> through our own texture pass, and rebuilt as `GRASS_Gaia_*` prefabs. The package stays declined;
> the cherry-pick clause is what was used. See `Docs/mrm70-unused-vegetation-inventory.md` §6.4.

Everything on the right would fight something already built and tuned: **HAZE** for fog, **Retro
Shaders Pro** for the CRT/PSX look, **Simple Water Shader** for the sea, our own `TimeManager` +
AllSky setup for lighting.

### How the declining actually works — two different mechanisms

This matters, because "decline it at import" is only half true.

**A. Art and demo content — declined at import time.** Straightforward. The `.unitypackage` import
dialog has per-folder checkboxes, and sample biomes / demo scenes / sample art are self-contained art
folders. Safe to drop — the same discipline that took Flora 232 MB → 4.9 MB and Retro 95 MB → 2.1 MB.

**B. Gaia Water / Lighting / Runtime / Terrain Loader — NOT an import-time choice.** These are scene
systems the **Gaia Manager** creates when you run its "Create World" / advanced setup workflow.
Importing Gaia does not force them on you. Declining them means:

1. Use the **Spawner** and **Terraform** windows directly.
2. Never run the Gaia Manager's world-creation flow.
3. Delete any `Gaia Runtime` / `Gaia Lighting` / `Gaia Water` GameObjects it creates in the scene.

> ⚠️ **Do not deselect Gaia's *code* folders at import.** Gaia's runtime and editor assemblies are
> interdependent; dropping code folders risks compile errors that are tedious to diagnose. The rule
> is: **drop art freely, import all the tool code, decline the runtime systems at the scene level.**

### The next concrete step

Same as the AllSky workflow (see the `asset_store_acquisition_workflow` memory):

1. **Carlos:** Package Manager → **My Assets** → *Gaia Pro VS* → **Download** *(Download only — not
   Import)*.
2. **Claude:** list the cached `.unitypackage` contents with the `tar` technique and produce an exact
   keep/drop list **before anything touches `Assets/`**. Gaia's precise folder split cannot be
   verified until the package is on disk — everything above is strategy, and the file-level list
   comes from the actual archive.

### Timing — after Sept 1, not before

Adopting Gaia means re-spawning 34,816 instances and then re-verifying, in order: Flora still reads
the new tree data → the PSX material migration survived → tree collision still blocks (the raycast
sweep) → FPS still ~500. That is a full day, and it fixes **none** of the seven open gaps in
`Docs/mrm70-pause-2026-08-26.md`.

**Do the Gaia spawn pass in the same session as the new-tree respawn.** Gap #2 of the pause doc
already calls for "clear + respawn in one motion" once Carlos's new tree models land; that respawn is
the natural moment to switch spawners, and doing both at once turns two days into one.

The one piece worth pulling forward if desired: **the erosion-only pass on the existing heightmap.**
Isolated, does not touch vegetation, low risk.

---

## 2b. Gaia as a temporary tool - import, use, remove, re-import

**Carlos's preferred model, 2026-08-27, and the recommended one.** Gaia does not have to live in the
project permanently.

**Why it works:** everything Gaia produces is baked into Unity's native `TerrainData` - heightmap,
splatmap, tree instances, detail layers. Gaia is the chisel, not the statue. Delete it afterwards
and the island is unchanged; Flora keeps drawing it and nothing in the scene notices. This also caps
the clutter cost at "however long Gaia is installed".

**Three conditions make the cycle clean. All three are easy to skip and annoying to discover late:**

1. **Save Gaia's recipe outside the Gaia folder.** Spawn rules, biomes and session settings are
   ScriptableObject assets. If they sit in `Assets/Procedural Worlds/` and that folder is deleted,
   the *terrain* survives but the *recipe* is gone, and later adjustments mean rebuilding the setup
   from scratch. **Save them into `Assets/_Project/`.**
2. **Pin the version.** Those recipe assets reference Gaia's scripts by GUID. With Gaia removed they
   become broken/missing-script assets - expected and harmless - and they come back on re-import of
   **the same version**. A different version can shift GUIDs and break the recipe permanently. This
   is the standing GUID rule in `Docs/external-assets.md`; the remove/re-import cycle makes it
   load-bearing rather than theoretical. **Record the exact version number at download time.**
3. **Strip Gaia components off the Terrain GameObject before removing the package.** Otherwise
   `Island.unity` - which *is* tracked in git - carries missing-script components. One check, not a
   real problem, but skipping it produces console noise that is expensive to trace later.

**The cycle:** import -> shape -> spawn -> save recipe to `_Project` -> strip components -> remove
-> re-import the same version whenever changes are wanted.

---

## 2c. Known gaps in this document

Everything below is **unverified** and must be confirmed before or during install. Recorded so none
of it is mistaken for settled fact. Nothing here changes the decision; all of it changes the plan.

| # | Gap | Why it matters | How to close it |
|---|---|---|---|
| 1 | **Gaia's actual folder layout, asset names, and what its first-run wizard really does** | The whole keep/drop list in section 2 is strategy, not a file list | List the cached `.unitypackage` with `tar` before importing |
| 2 | **Whether Gaia attaches components to the Terrain GameObject** | Decides whether removal leaves missing scripts on tracked `Island.unity` | Inspect the Terrain after the first spawn |
| 3 | **Whether Gaia's spawner/biome/session assets can be relocated outside its own folder** | Condition 1 of 2b depends on it. Probable, not confirmed | Move one and re-open the Spawner |
| 4 | **Exact Gaia Pro VS version number** | Condition 2 of 2b - the whole remove/re-import cycle rests on it | Record at download; add to `external-assets.md` |
| 5 | **Whether Gaia reorders our 8 existing TerrainLayers** | **Highest-risk unknown.** Layer order drives *both* the vegetation spawn masks *and* the footstep surface mapping. A silent reorder breaks two systems at once, and the footstep break stays invisible until someone listens | Snapshot layer order before/after the first Gaia operation and diff it |
| 6 | **Whether Gaia handles a single non-square 4103 x 7085 m terrain** | Its defaults assume Gaia-created, often square, often multi-tile terrains | Verify on the erosion-only pass first, which is reversible |
| 7 | **A correctly-projected biome mask PNG does not exist yet** | See the correction in section 2 - `biomes.png` is a scene-view screenshot | Orthographic top-down render of the terrain |

> **Do the erosion-only pass first.** It touches only the heightmap, is independently useful, and
> closes gaps 1, 2, 4 and 6 at low risk before any vegetation is at stake.

---

## 3. Why Nature Renderer 6 Pro was rejected

Not a close call, and the reason is specific rather than a matter of taste.

**Nature Renderer requires shaders to support its own procedural instancing.** Its documentation is
explicit: custom shaders must be modified, and it ships an automatic shader *patcher* to do it.

Our vegetation runs on **`RetroLit`** from Retro Shaders Pro — the shader that *is* the PSX look.
It is already BRG/DOTS-instancing compatible out of the box:

```
Assets/ThirdParty/Retro Shaders Pro/Shaders/RetroLit.shader:143
    #include_with_pragmas ".../ShaderLibrary/DOTS.hlsl"
Assets/ThirdParty/Retro Shaders Pro/Shaders/RetroSurfaceInput.hlsl:42
    #ifdef UNITY_DOTS_INSTANCING_ENABLED
```

That is exactly Flora's requirement, and exactly why migrating all 28 vegetation materials to
`RetroLit` needed zero shader work. Nature Renderer would need that same third-party paid shader
patched with *its* pragma — and re-patched on every Retro Shaders Pro update. **A permanent
maintenance tax on the game's signature look.**

There is also no problem left to solve. Flora already delivers 38,980 → 535 draw calls, 70.4 M →
126 K triangles, ~505 FPS in build, tree collision verified by raycast. Every headline Nature
Renderer feature — procedural instancing, distance density falloff, LOD, per-camera settings,
vegetation shadows, floating origin — Flora already has, **plus GPU occlusion culling**
(`OcclusionCuller.GPUDriven.cs`, `IndirectCullingChunks/Draws/Instances.compute`), which Nature
Renderer does not advertise.

Cost of switching: $120 + 377 MB + re-verifying the entire PSX / collision / performance chain, for a
measured gain of zero.

---

## 4. The telescope sequence — what Flora does and does not do

Recorded because it was the feature that made Nature Renderer tempting, and the answer is precise.

**Flora does not perform the zoom.** Narrowing the camera's field of view is plain Unity, and would
be our own code either way. No vegetation renderer provides that.

**What Flora provides is the half that would otherwise break under zoom.** Its distance culling is
**screen-coverage based, not distance based.** Verified live values in
`Assets/_Project/Settings/DefaultVolumeProfile.asset`:

| Parameter | Live value | Meaning |
|---|---|---|
| `MaxRenderDistance` | **0** | **Unlimited** — no hard distance clamp to fight |
| `MaxShadowDistance` | 0 | unlimited (URP's own 90 m shadow distance still applies) |
| `MinScreenSize` | **0.005** | cull below 0.5% of screen height |
| `MinScreenSizeMode` | `RenderersOnly` | |
| `CrossFadeDuration` | 0.3 s | LOD cross-fade |
| `GlobalDensityEnabled` / `RangeDensityEnabled` | **0 / 0** | density culling currently **off** — only `MinScreenSize` governs |

Because there is no distance clamp and culling keys off screen coverage, **narrowing the FOV makes
distant trees occupy proportionally more of the screen** — so vegetation that sat below the 0.005
threshold rises above it and appears, and LOD levels step up at the same time, since LOD is also
screen-height driven. **This happens automatically, with no code.** That is the honest answer to "can
we see further when we zoom": yes, and it is a property of how Flora culls rather than a feature
anyone implemented.

Three things to design around when the sequence is built:

1. **Raising the scope will spike instance count and LOD tier simultaneously** — a potential frame
   hitch at the moment of zoom. This is what the controls are *for*, not a reason to avoid it:
   `FloraAdditionalCameraSettings.LODBiasScale` (0.001–3.0, per camera) plus a Volume override on
   `MinScreenSize`, blended with the scope raise, turn a pop into a ramp. `Teleported` exists for
   cinematic cuts.
2. **Distant trees will be unshadowed when zoomed** — URP shadow distance is 90 m
   (`Docs/pc-build-target.md` §5). Cosmetic; decide at art-direction time.
3. **Terrain `heightmapPixelError = 3`** still governs terrain LOD and may show visible terrain
   popping under zoom. Unrelated to Flora, but it will surface in the same sequence.

Per-camera settings and density are `VolumeComponent`s (`FloraRenderSettings`,
`FloraDensitySettings`), so all of this blends through URP's volume system like any post-process.
**A scoped-zoom override is a small component driving a Volume weight, not an asset purchase** — and
its numbers belong in `MoonlightTunables`, since this is gameplay, not vegetation staging.

---

## 5. Open, deliberately not decided here

- ~~Water rendering will move off Simple Water Shader.~~ **RESOLVED same day — see section 6.**
- **The external-asset texture pipeline.** Every imported 3D asset must route through a defined
  texture-reduction + pixelation pass, matching what was done for vegetation — this is a build-size
  lever as well as an art-direction one. To be specified in its own pass;
  `Assets/_Project/Code/Editor/MoonlightTextureImporter.cs` already does part of it (prefix-routed
  sRGB / Point filter / 512 cap / compression) and is the hook to **extend, not replace**. See
  `Docs/3d-asset-pipeline.md`.
- **A large batch of newly-acquired Asset Store packages** is to be triaged against existing Linear
  issues in a later pass.

---

## 6. Water — Crest Water 5

**Decided 2026-08-27 (Carlos).** Tracked as **MRM-71**, sub-issue of MRM-67 (Polishing Details),
M2 milestone, branch `mrm-71`. Replaces IgniteCoders "Simple Water Shader URP" on `M_Sea.mat`.
**Owned as of 2026-08-27; not yet installed.**

| Asset | Verdict | Why |
|---|---|---|
| **Crest Water 5** (Wave Harmonic, $240, 60.6 MB, owned) | ✅ **Adopt** | Underwater pass runs *before* post-processing; LOD-cascade architecture; 60 MB UPM package; actively maintained |
| **Crest Water 4 URP** (Wave Harmonic, $100, 1.5 GB) | ❌ Superseded | Previous generation. 25x larger, last updated Jul 2026 |
| **KWS2 Dynamic Water System** (kripto289, $159, 560 MB, owned) | ❌ **Reject** | Stronger simulation, but its value and its cost both live in photoreal flourishes that fight a PSX look |

### Why Crest over KWS2

- **Crest's Underwater Renderer executes between the transparent pass and post-processing.** HAZE fog
  and the Retro CRT effect therefore land *on top of* it. Any water system rendering *after*
  post-processing punches an unfiltered modern hole in a frame whose whole identity is a full-screen
  CRT filter. **This is the fact that decided it.**
- **Cascaded LOD textures** (displacement / foam / shadow / depth, multi-resolution, centred on the
  viewer) suit one ocean around one island seen from shore. Cost is bounded and tunable rather than
  scaling with scene complexity.
- **Closes a descoped MRM-68 requirement for free.** The *near-calm / far-aggressive distance blend*
  was cut because Simple Water Shader has no such mechanic; Crest varies wave scale with viewer
  distance by design. Shoreline foam against the beach biome comes free too. Both are **surface**
  features visible from land, so they land even with the underwater renderer off.
- KWS2's FFT ocean, flow simulation, SSR, caustics, volumetric sunshafts and Snell's window are
  genuinely stronger technology — and every item on that list is a photoreal flourish that fights a
  PSX horror look, and is where its GPU cost lives. Right buy when water is a *character* (sailing,
  buoyancy-driven gameplay). Ours is scenery you walk to the edge of.

### Why Crest 5 over Crest 4 — logistical, NOT aesthetic

| | Crest 4 URP | **Crest 5** |
|---|---|---|
| Size | 1.5 GB | **60.6 MB** |
| Price | $100 | $240 |
| Latest | 4.23.1 (Jul 2026) | **5.10.0 (Aug 2026)** |
| Pipelines | URP only | all three, one package |
| Installs to | `Assets/` | **`Packages/` (UPM)** |
| Minimum Unity | — | 2022.3.62 |
| Unity 6 | required for the URP build | full support since 5.1.0, fixes through 5.7.0 |

Crest 5 was re-architected as a UPM package, which is why it is 25x smaller — `Samples~` do not
import unless requested, so **no lean-extraction dance is needed** (unlike Flora 232 MB → 4.9 MB and
Retro 95 MB → 2.1 MB). It is also the actively maintained line; Crest 4 is the previous generation.

> ### Neither Crest version is more PSX-friendly than the other
>
> Recorded because this was explicitly misread once and the mistake is an easy one.
>
> **Both are realistic water systems.** Crest 5's extra realism — out-scattering in water volumes,
> "pinch" subsurface scattering, TIR planar reflections, absorption-colour-to-depth-fog, anisotropy
> — is **opt-in switches, not forced behaviour.** Left off, Crest 5 looks like Crest 4.
>
> More features means **more dials**, not "more realistic whether you like it or not." If anything
> Crest 5 is marginally *better* for a stylised look, because it exposes more dials to turn **down**
> and its water shader is a Shader Graph wrapper.
>
> What actually makes the water fit our look is identical for both: **the full-screen CRT pass on
> top** (which does most of the work), plus turning Crest's own settings down — low smoothness,
> fewer wave layers, reflections off.
>
> The Crest 5 price premium buys size, maintenance and Unity 6 maturity. It does **not** buy
> better-looking water *for this demo*, because the features it adds are ones we have decided not to
> lean on.

### Look decision — CRT only, no PSX treatment on water

**Carlos's explicit call, 2026-08-27.** The water gets the CRT pass (scanlines + RGB subpixel mask,
full-screen, applies to everything) and **nothing else**. Specifically:

- **Do NOT** enable full-screen CRT pixelation to "unify" the water with the island.
- **Do NOT** migrate Crest's shader to `RetroLit`.
- **Do NOT** edit Crest's shader graph to add vertex snapping or affine warping — third-party assets
  are imported unedited, the standing project rule and the reason the current water shader went in
  as-is.
- **Leave Crest 5's extra realism features off** by default.

**Accepted consequence:** the water will be the smoothest, most modern-looking thing on screen, seen
through a CRT. That is the intended result, not a defect. Affine warping and vertex snapping are
per-material features of `RetroLit` and simply will not apply to Crest's own shader.

### Underwater renderer — disabled for the demo

Carlos is **blocking Tracey from entering the water** in the demo. So the Underwater Renderer earns
nothing and costs a fullscreen pass, the renderer-feature ordering risk, and the largest
art-direction risk. **Disable the component; do not delete it** — it is a toggle for post-demo work.

Crest still earns its place without it: the distance-blended wave scale and shoreline foam are
surface features, visible from land, and neither exists today.

### Performance context

Baseline before this work: **535 draw calls, ~505 FPS / 2.0 ms** (build 21).

`PC_RPAsset` already has `m_RequireDepthTexture: 1`, `m_RequireOpaqueTexture: 1`,
`m_OpaqueDownsampling: 1`. **The usual biggest hidden cost of adopting a water system — forcing the
camera opaque and depth textures on — is already being paid** by the current shader. Crest inherits
it rather than adding it. Expected additions are a small number of LOD-ring draw calls plus
fixed-cost cascade update passes that scale with cascade resolution, not scene complexity.

### Gaps — unverified, confirm during MRM-71

| # | Gap | Why it matters |
|---|---|---|
| 1 | **`Packages/` is NOT gitignored — Crest 5 will be committed to the repo** | `.gitignore` excludes `Assets/ThirdParty/**` but not `Packages/`, which is why Flora is in the repo and HAZE/Retro are not. Crest makes it ~65 MB of paid Asset Store code on the GitHub remote. *Upside:* a fresh clone works with no re-download. *Downside:* a licensing question. Already open in `pc-build-target.md` §7 — this is when it stops being theoretical. **Carlos's call** |
| 2 | **Renderer feature ordering on `PC_Renderer`** | HAZE + CRT + Crest all inject passes. HAZE also bails out entirely when `!cameraData.postProcessEnabled` (`HazeRendererFeature.cs:546`). The step most likely to cost real time |
| 3 | **Unity 6.3 specifically is unverified** | Minimum is 2022.3.62 and Unity 6 support landed in 5.1.0 with fixes through 5.7.0, so low risk — but 6.3 is newer than anything stated as tested. Verify on install |
| 4 | **Exact Crest version not yet recorded** | Less critical than for `Assets/ThirdParty/` packages if it ends up committed, but still required in `external-assets.md` |
| 5 | **Crest does not support prefab mode** (its own Known Issues, current as of the 5.10.0 docs) | Dirty state in prefab mode is not reflected in the scene view |
| 6 | **Actual measured cost is unknown** | The estimates above are reasoning, not measurement. Measure in a real build — editor `UnityStats` has given false readings on this project |
| 7 | **Whether the existing `SeaGrid.mesh` / `M_Sea.mat` path is retired or kept** | Keep as a documented fallback, do not delete — the same treatment the hand-written shader got in MRM-68 |
