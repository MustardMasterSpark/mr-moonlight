# PC build target — Windows 64-bit standalone

**Supersedes `Docs/webgl-constraints.md`, which is historical as of 2026-08-25.**

Target: **Windows 64-bit standalone, 1920×1080 borderless fullscreen**, distributed as a
downloadable zip on itch.io. Unity 6.3 LTS, URP, `PC` quality level + `PC_RPAsset`.

---

## 1. Why the platform changed

Carlos's call on 2026-08-25, after the vegetation pass made the ceiling measurable rather than
theoretical.

The island profiled at **21,946 draw calls** — roughly **10× what WebGL sustains** (~1,000-3,000) —
running at **19 FPS**. The cause was structural, not a tuning mistake: Unity terrain trees do not
batch (`instancedBatchedDrawCalls = 0`), and each tree costs **3 draw calls** because its mesh has
three submeshes (bark / dirt / needles) on three materials. ~7,300 visible trees × 3 ≈ 22,000 calls.

Three WebGL-only defects had already cost most of a day, none of which can occur on DX11:

1. **Invisible terrain** — the URP terrain shader exceeded the GLES3 **16 fragment sampler**
   guarantee (8 layers × diffuse+normal+mask = 13 samplers before URP's own shadow/probe
   textures), failing to compile with `GLSL compilation failed, no infolog provided`.
2. **Mirror-finish ground** — Unity terrain reads smoothness from the diffuse texture's **alpha
   channel**; fully-opaque ground textures meant smoothness 1.0. Not WebGL-specific, but only
   visible once the terrain rendered at all.
3. **Build failing instantly** — an editor-only asmdef with `"includePlatforms": []` compiled into
   the player.

A Windows build removes the ceiling and all three failure modes at once.

## 2. What still applies from the WebGL era

Not everything was browser paranoia. These stay:

- **The 1 GB ceiling stays** — it is *itch.io's upload limit*, not a browser limit. But it is now
  only **download size**: textures compress (BC/DXT), assets stream from disk, and there is no
  wasm/JS overhead. A 1080p build of this scope lands well under it.
- **Ship 4 skyboxes, not 220.** Still the single largest size win available.
- **Bake spreadsheet data into ScriptableObjects at build time.** Still correct — runtime CSV
  parsing is fragile regardless of platform.
- **Cap simultaneous audio voices.** Still good practice.
- **Strip demo/sample content from imported packages.** Vegetation Spawner went 4.5 MB → 212 KB
  this way.

## 3. What is no longer true

- `Application.Quit()` **works now.** The main menu's Quit option is real (MRM-18).
- **Compute shaders are available.** This is what makes Flora Renderer viable (see §6).
- **No shader sampler ceiling** in practice. Terrain normal maps and mask maps were restored on
  2026-08-25 after being stripped to survive GLES3.
- **The browser console is gone.** Diagnostics now come from `Player.log`
  (`%USERPROFILE%\AppData\LocalLow\Mustard Master Spark\MrMoonlight\Player.log`);
  `PlayerSettings.usePlayerLog` is on.
- **960×540 is dead.** Everything targets 1920×1080.

## 4. Player settings (as configured 2026-08-25)

| Setting | Value | Why |
|---|---|---|
| Build target | StandaloneWindows64 | |
| Scripting backend | **Mono2x** | Builds far faster than IL2CPP, and our bottleneck is draw calls (GPU/driver), not C#. Switch to IL2CPP for the final release build, where the ~10-30% CPU win is worth the long build. |
| Resolution | 1920×1080 | |
| Fullscreen mode | FullScreenWindow (borderless) | Opens straight into the game at desktop resolution, no mode-switch flicker. |
| Splash screen | **off** | Costs seconds against the "playing within 2 minutes" gate. |
| Colour space | Linear | unchanged |
| Graphics API | Direct3D12, Direct3D11 (auto) | |

## 5. Quality / render settings

`PC` quality level, `PC_RPAsset`: render scale 1.0, **shadow distance 90 m** (raised from 50 — trees
are 13-25 m tall and their shadows were clipping close to the player), 4 cascades, soft shadows,
2048 shadowmap, HDR on, MSAA off.

> MSAA stays off deliberately: the foliage is alpha-clipped, which MSAA does not antialias without
> alpha-to-coverage, so it would cost fill rate for almost no benefit.

Terrain: `treeDistance` 1500 m, `detailObjectDistance` **80 m** (doubled from WebGL's 40, *not*
quadrupled — grass area scales with the square of this, so 160 m would have been 16× the ground
cover), `heightmapPixelError` 3, `drawInstanced` **false** (Unity's default; it was briefly enabled
and wrongly blamed for the invisible terrain — do not re-enable without a measurement).

Ambient 0.4 / reflection 0.25 — a proposal, not a settled look; revisit with the real skybox.

> **2026-09-04 — shadow distance had regressed to 40m, found and fixed.** Carlos reported a dark
> "shadow circle" moving with the camera in-build — the well-known symptom of `shadowDistance`
> being tighter than a scene's visible geometry: shadows render only inside that radius, so a
> heavily-forested scene looks abruptly different just past it. Live inspection found both
> `PC_RPAsset.asset` and `QualitySettings.asset` reading **40m**, not the **90m** documented above
> — almost certainly a Gaia vegetation-regen silently reapplying its own preset at some point
> between when 90m was set and now (Gaia ships a `GWS_URPShadowDistance` wizard-check asset for
> exactly this kind of setting). Restored to 90m in both files — this is a regression fix, not a
> new decision; the reasoning above (13-25m trees clipping shadows close to the player) still
> applies unchanged. If this drifts again after a future Gaia regen, check those two files first.

## 6. Known, still open

- **Trees cost 3 draw calls each.** The fix is either an atlas merge (3 materials → 1) or
  **Flora Renderer 6** ($60, BRG/GPU-resident, reads terrain tree+detail data directly). Flora
  requires compute shaders, so it was never possible on WebGL — it only became an option with this
  switch. Deferred until after the Sept 1 gate; collision behaviour with `TerrainCollider` is
  unverified and is the thing to test first.
- **Editor `UnityStats` is not a reliable profiler here** — it includes Scene View rendering, and it
  only updates when a frame actually draws, so A/B toggling from a blocking script returns the same
  numbers every time. Measure in a real build with the FPS counter
  (`Assets/_Project/Prefabs/UI/FPS Counter.prefab`).

## 7. Third-party rendering stack (added 2026-08-25)

Three paid assets, all extracted **lean** — demo/sample content stripped on import, same discipline as
Vegetation Spawner (4.5 MB → 212 KB).

| Asset | Full | Installed | Location | Dropped |
|---|---|---|---|---|
| **Flora Renderer 6** | 232 MB | **4.9 MB** | `Packages/com.ma.flora` | `Samples~` (224.8 MB), `Documentation~`, `Tests` |
| **HAZE – Volumetric Fog** | ~9 MB | **0.95 MB** | `Assets/ThirdParty/HAZE …` | `Demo`, user manual |
| **Retro Shaders Pro** | ~95 MB | **2.1 MB** | `Assets/ThirdParty/Retro Shaders Pro` | `Demo` (92.8 MB), README, its bundled `manifest.json` |

Flora ships as a *bootstrapper* whose real 140 MB package is nested inside at
`Editor/Packages/com.ma.flora@6.3.35.unitypackage`. Extract that and install it as an embedded
package; do not run the in-editor installer window, which imports everything.

Retro's package contains its own `Packages/manifest.json` — **never extract it**, it clobbers the
project's.

### Flora

`Flora Scene Settings` in the Island scene auto-registers a `FloraTerrainProvider` on the Terrain
and sets `drawTreesAndFoliage = false` so Unity does not double-draw underneath. Reads existing
terrain data, so **no respawn is needed**.

Measured, same methodology before and after: **38,980 → 535 draw calls**, **70.4 M → 126 K triangles**.
Build ran at **505 FPS / 2.0 ms**.

**Tree collision survives** (28/31 chest-height raycasts blocked) — the one thing Flora's docs never
mention, and the main risk in adopting it. Verified, not assumed.

**Known issue: 14 of 27 detail prototypes do not render.** Those are `GrassType.Texture` billboards
using Unity's built-in grass shader, which is not BRG-compatible. The 13 mesh prototypes are fine.
Fix is to rebuild the GrassFlowers cards as single-mesh crossed quads on a URP/Lit material — folded
into the next vegetation pass. (A separate real misconfiguration was also found and fixed on the
way: `DetailStreamingMode` was *Streamed* with `DetailLoadBudgetPerFrame = 0`, so details could
never load at all.)

### HAZE

`HazeRendererFeature` on `PC_Renderer`. Global fog Volume (`VP_HazeGlobalFog`) + a
`HazeDensityVolume` box covering the playable slice (X 650–1700, Y 20–200, Z 3800–5900).

> **`HazeRendererFeature.cs:546` bails out entirely when `!cameraData.postProcessEnabled`.**
> The Main Camera had post-processing off, so fog rendered in Scene view (on by default there) and
> not in Game view. Fixed on **`Player.prefab`**, not the scene instance — otherwise `Sandbox` and
> every future scene keeps the bug. This is the first thing to check if a full-screen effect ever
> "works in Scene view but not in game".

Which parameters actually matter, in order:

1. **`_globalDensityMultiplier`** — the master dial. Effectively unbounded (0–999). Was `0.06`,
   i.e. barely on.
2. **`_heightFogFactor` + `_maxFogHeight`** — *where* the fog is. These silently cancel the density:
   at ~80 m on terrain reaching 130 m with `maxFogHeight` 170 and a steep falloff, you sit at the top
   of the gradient and see nothing however high you push density.
3. `_globalDensityThreshold` — a floor below which fog is culled entirely. Nonzero suppresses thin fog.

Current Silent-Hill-dense values: global 3.0, box 1.0, `heightFogFactor` 0.10, `maxFogHeight` 400,
ambient pale grey. **Placeholder, not art direction** — and worth judging only against the real
skybox, since thick fog under a bright daytime sky reads as white haze rather than dread.

`_additionalLightContribution = 1` on both, so the flashlight and Spotter lamps light the fog. That
works because `PC_Renderer` is already **Forward+** — HAZE only supports punctual light contribution
in Forward+/Deferred+.

### Retro Shaders Pro

`CRTEffect` renderer feature on `PC_Renderer`, `CRTSettings` override on the global volume.
Enabled: **RGB subpixels** (0.55) and **scanlines** (0.35, size 4). Off: CRT point filtering,
pixelation, interlacing, barrel distortion, tracking, dithering.

The RGB mask darkens the image noticeably — inherent to multiplying by a subpixel mask. `brightness`
(default 1.0, range 0–5) compensates; left at 1.0 pending an art call.

> **CRT and PSX are two different mechanisms.** CRT effects are post-processing and drag-and-drop.
> **PSX Affine Textures / Vertex Snapping / Colour Depth / Resolution Limit are per-material
> features of `RetroLit.shader`** — no renderer feature can enable them. They require migrating
> materials from `URP/Lit` → `RetroLit`. Verified safe for Flora: `RetroLit.shader` includes
> `DOTS.hlsl`, so it is BRG-compatible. `RetroTerrainLit.shader` is the matching terrain shader.
> PSX Point Filtering is largely already achieved at import — `MoonlightTextureImporter` sets
> `_BaseColor` textures to Point filter.

### Source control caveat

`.gitignore` excludes `/[Aa]ssets/ThirdParty/**` by deliberate project policy, so **HAZE and Retro
Shaders Pro are NOT in the repo** — but **Flora IS**, because it installs to `Packages/`. That
inconsistency has two consequences worth knowing:

- A fresh clone gets Flora but not HAZE/Retro, so `Island.unity` and `PC_Renderer` will show
  **missing script references** for the fog and CRT features until those are re-downloaded.
- Flora (5 MB of paid Asset Store code) would be committed to a GitHub remote. Decide deliberately
  whether that is wanted.

## 8. The graded gate still exists

Assignment #10's *"playing within 2 minutes"* criterion did not go away — it just moved from
"download + decompress + run in browser" to "download + extract + launch". **Build size is still
graded, indirectly.** Keep the zip small and test the cold path on a machine that has never seen it.
