# Water Shader — MRM-68

> ## ⚠️ Superseded direction — banner added 2026-08-27
>
> **The water is moving to Crest Water 5** under **MRM-71** (sub-issue of MRM-67, M2 milestone).
> This document remains the record of everything that came before, and the **fallback options are
> deliberately kept, not deleted** — `MrMoonlight/StylizedWater` (hand-written) and the Procedural
> Water Shader analysis are both still valid if Crest is ever backed out.
>
> **What is stale below:**
> - Every WebGL cost argument. WebGL was dropped 2026-08-25; see `Docs/pc-build-target.md`.
>   Specifically, the **WaterWorks** rejection leaned on WebGL full-screen-pass budget — its *real*
>   remaining objection is the Unity 6 RenderGraph compile patch, which still stands.
> - The `_CameraDepthTexture` cost note. `PC_RPAsset` already has `m_RequireDepthTexture: 1` and
>   `m_RequireOpaqueTexture: 1`, so **that cost is already being paid** and Crest inherits it rather
>   than adding it.
>
> **What is still live:** the **near-calm / far-aggressive distance blend was descoped** from MRM-68
> because the IgniteCoders shader has no such mechanic — **Crest brings it back for free**, plus
> shoreline foam. **Do not delete `SeaGrid.mesh` or `M_Sea.mat`**; the old path stays as a
> documented fallback.
>
> **Look decision, Carlos 2026-08-27: the water gets the CRT pass and nothing else.** No PSX
> treatment, no `RetroLit` migration, no editing Crest's shader graph. See
> `Docs/terrain-vegetation-tooling-decision.md` §6.


> **SUPERSEDED 2026-08-27 — see MRM-71.** Carlos decided to replace this shader with **Crest Water 5**. Everything below still accurately describes what is *live today* and stays current until
> MRM-71 ships; treat it as the fallback record afterwards, not as the plan. Rationale, the
> CRT-only look decision, the disabled Underwater Renderer, and six open gaps are in
> `Docs/terrain-vegetation-tooling-decision.md` section 6.

**Current state (2026-08-24): `M_Sea.mat` uses a purchased Asset Store shader (IgniteCoders'
"Simple Water Shader URP"), not the hand-written one below.** Carlos found the hand-written
shader's look was hurting visual harmony during blockout and asked for a free Asset Store
alternative instead — see **Asset Store options (2026-08-24)** further down for the full
evaluation, what got imported, and the two other shaders (hand-written + Procedural Water Shader)
kept intact as fallbacks. The rest of this section, below the options writeup, is the original
hand-written shader's documentation — still accurate for that shader, just not what's live today.

**Status: MRM-68 closed 2026-08-24.** Animation confirmed hands-on by Carlos; frame cost confirmed
live on itch.io via `E:\Builds\11 - Water Shader - 2026-08-24\Build.zip` (~18.5 MB, acceptable
performance); the original near-calm/far-aggressive distance-blend requirement was explicitly
descoped to future polish (the current shader doesn't have that mechanic — see Linear MRM-68 for
the full reasoning, recorded in the issue itself, not just a comment).

---

## Asset Store options (2026-08-24)

Carlos gave four free Asset Store candidates, ranked by his own visual preference, and asked for
a technical read against the project's WebGL/budget constraints before implementing. All four
confirmed FREE and URP-compatible before evaluating further.

| # | Asset | Verdict |
|---|---|---|
| 1 | [BitGem — URP Stylized Water Shader, Proto Series](https://assetstore.unity.com/packages/vfx/shaders/urp-stylized-water-shader-proto-series-187485) | **Rejected.** Store screenshots show a bright cartoon toy-pool look — thick white foam borders, checkered tile walls, built for a low-poly "Cube World" prop pack. Technically harmless (732 KB, no depth texture, no extra passes) but a hard tone mismatch for 1979 Alaska horror no matter the recolor. |
| 2 | [IgniteCoders — Simple Water Shader URP](https://assetstore.unity.com/packages/2d/textures-materials/water/simple-water-shader-urp-191449) | **Implemented — currently live.** See below. |
| 3 | [GapperGames — WaterWorks](https://assetstore.unity.com/packages/3d/environments/waterworks-simple-water-ocean-river-system-for-urp-reflection-re-206909) | **Rejected.** Nicest "realistic" look of the four (rocky coastline, real waves) but real screen-space reflections plus a custom underwater-fog `ScriptableRendererFeature` (full-screen `Blit` passes) — exactly the stacked full-screen-pass cost `webgl-constraints.md` §4 warns is tightly budgeted on WebGL. The store page itself carries a community-pasted "UNITY 6 WaterVolume.cs fix," meaning the shipped package doesn't compile against Unity 6's RenderGraph pipeline without manual patching — too much unbudgeted integration risk this close to Sept 1. |
| 4 | [Pedro Verpha — Procedural Water Shader](https://assetstore.unity.com/packages/vfx/shaders/procedural-water-shader-353486) | **Implemented, then rolled back at Carlos's request** (he wanted #2 instead once he saw both). Technical case was strong — see below — kept as a documented fallback. |

### #2 — IgniteCoders "Simple Water Shader URP" (currently live)

Files pulled directly from the purchased `.unitypackage` via the same `tar` GUID-index extraction
technique as the AllSky pack (`allsky_asset_extraction.md` memory) rather than a full package
import — avoids the package's demo scene, Editor readme scripts, and its `WaterBlock_50m`
tiling-prefab system (the project already has its own single-huge-plane `Sea`/`SeaGrid.mesh`
approach from MRM-58/this issue, so the vendor's own mesh/prefab weren't needed):

- `Assets/ThirdParty/SimpleWaterShaderURP/WaterShader.shadergraph` — the actual shader (Shader
  Graph asset, not hand-written HLSL this time — imported as-is, unedited, per the project's
  "third-party assets are never edited" rule). `Assets/ThirdParty/` is gitignored project-wide, so
  none of this reaches the repo regardless of "bloat."
- `WaterSurface_atlas.tif` + `WaterSurface_single.tif` — the two normal-map textures the graph
  actually samples ("Main Normal" / "Second Normal").
- **Two material presets kept as unedited vendor references, per Carlos ("keep both"):**
  `Water_mat_01_Dark.mat` (dark navy — the one now live) and `Water_mat_03_Clear.mat` (bright
  teal/clear). The package actually ships a third, `Water_mat_02` (a medium blue) — **not** kept,
  Carlos only asked for two.
- `M_Sea.mat` now uses shader `Shader Graphs/WaterShader` with `Water_mat_01_Dark`'s exact tuned
  values (Deep Color `{0.152, 0.209, 0.340}`, Shallow Color `{0, 0.510, 0.420}`, Smoothness `1`,
  Normal Tiling `10×10`, full float list in the material). No scene/prefab edits needed — `Sea`'s
  `MeshRenderer` already pointed at `M_Sea.mat`, so the shader swap alone took effect;
  `SeaGrid.mesh` (64×64 grid) is reused as-is.
- **The package's "Use Reflection (Experimental)" planar-reflection system was left off** — it's a
  `Boolean` toggle in the graph, `0` in both kept materials, so the reflection camera
  prefab/script/render-texture were never imported; the material's reflection texture slot is
  cleanly nulled (not a dangling reference).
- Verified: shader + material import with zero console errors. Live focused-camera Play Mode
  screenshot over open water shows correct dark-navy color, working fresnel + sun-glint specular,
  visible surface ripple.
- **Known tuning issue:** at a low grazing viewing angle under this scene's current overcast/dusk
  sky, the water reads washed-out/pale — `Smoothness = 1` (full mirror) reflects the pale sky
  almost uniformly rather than showing the tuned deep-navy body color. From a more front-on angle
  over open water it reads correctly. Likely fix: lower Smoothness slightly. Not touched yet —
  flagging for Carlos's own pass rather than guessing at a value.
- **Wave size, 2026-08-24:** Carlos confirmed the shader animates correctly (first hands-on
  confirmation of either shader, closing that long-standing acceptance-criterion caveat) but found
  the ripple pattern read too large/uniform — "like a giant pool." **`Normal Tiling`
  (`Vector2_4351ac2be1d74054986ec5378db9d578`) is the knob** — it's the UV tiling fed into both
  normal-map samples, so higher values pack more (smaller, more varied) ripple repetitions across
  the same physical water area. It's a normal Material Inspector field on `M_Sea.mat`, already
  exposed by the shader graph — no code or tunables asset needed, Carlos can drag it himself
  anytime. Bumped from the vendor default `(10, 10)` to `(40, 40)` as a first pass, verified in
  Play Mode (visibly finer/more varied ripple texture, no visible tiling seams at this value,
  animation and sun-glint specular unaffected). Carlos said he expects to keep tweaking this
  himself.
- **Animation not confirmed by automation**, same limitation as the original hand-written shader
  below — see that section's caveat, which applies identically here.

### #4 — Pedro Verpha "Procedural Water Shader" (rolled back, documented as fallback)

Implemented first, as the independent technical pick (no custom render feature, no reflection
camera, no SSR — needs only URP's standard Depth Texture + Opaque Texture, both of which
`Assets/_Project/Settings/Web_RPAsset.asset` already requires project-wide, so no new per-frame
cost category). Verified working correctly with real Gerstner-wave motion in a focused Play Mode
screenshot over deep water (the first grazing-angle test looked broken — a checkerboard artifact —
but that turned out to be the water's own high transparency at shallow depth-difference letting
MRM-58's still-unpainted terrain checker show through, not a shader bug; a deeper/more front-on
shot confirmed correct rendering).

Rolled back in full once Carlos asked for IgniteCoders' shader instead:
`Assets/ThirdParty/ProceduralWaterShader/` deleted, `M_Sea.mat` reverted via `git checkout` to its
pre-swap state. If this ever gets revisited, the URP-variant shader source (hand-extracted from
the purchased package's nested `URP.unitypackage`, since the top-level shader in that package is
actually the **Built-in RP** version — same filename, different `CGPROGRAM`/`HLSLPROGRAM` — worth
remembering if re-attempting this) is no longer in the project, but the same extraction technique
gets it back in a few minutes: the package's a free re-download from Carlos's Asset Store account.

---

Built from Simon Swartout's "Simple Water Shader" (Medium, URP Shader Graph series) — Voronoi
ripples, Radial Shear UV warp, Power-sharpened edges, vertex displacement. Fully procedural, no
texture maps of any kind. A second, older article (Gerstner-wave/normal-map ocean shader) was
read for inspiration on the calm-vs-aggressive split but not implemented directly — see
**Deferred** below.

## Why hand-written HLSL, not Shader Graph

The available tooling this was built with creates shader *scripts* (`.shader` text files), not
Shader Graph node graphs — a `.shadergraph` asset's JSON is complex, GUID-heavy, and versioned;
hand-crafting one by writing its serialized JSON directly is fragile enough that it would likely
produce a graph that's broken or won't open. The HLSL version below implements the exact same
technique from the article, node-for-node — same math, just written as code instead of wired as
boxes. Every tunable value is still exposed as a Material Inspector property (sliders, color
pickers), so day-to-day tweaking doesn't require touching the code.

**If Carlos wants an actual click-and-drag Shader Graph later**, the table below is the spec to
rebuild it that way — Unity's built-in Shader Graph nodes referenced by name, with all custom
math left in a Custom Function node.

## What it does

- **Fine ripple pattern (fragment shader):** a Worley/Voronoi F1-F2 edge pattern — bright thin
  "veins" at cell borders, dark cell interiors — matches the cracked-glass/circuit-vein look
  Carlos referenced from the TikTok clip. Two octaves layered (different density/rotation/speed)
  for visual richness. Animated by rotating each cell's jitter point over time (equivalent to
  the article's "Time → Voronoi AngleOffset").
- **Radial Shear:** UVs warped around each repeating tile's center before sampling, so the
  pattern doesn't read as an obvious grid — same node the article uses.
- **Distance blend:** every parameter that matters for "calm vs. aggressive" (cell density,
  animation speed, edge sharpness/width, and — see below — swell amplitude) is a `lerp` between
  a *Near* and *Far* value, driven by `smoothstep` on distance-from-camera (XZ only). No hard
  seam between two materials/objects — it's one shader, one mesh, blending continuously.
- **Swell (vertex shader):** `Sea` was a single flat 2-triangle quad (deliberately, near-zero
  render cost) — nowhere near enough vertices for the article's vertex-displacement technique to
  show anything. Replaced with `SeaGrid.mesh`, a 64×64 grid (~4,225 verts, still trivial cost)
  generated via `execute_code`, specifically so there's real geometry for a slow large-scale
  swell to displace. The swell's wavelength (1200m default) is deliberately far larger than the
  grid's own cell size (~470m at 30,000m plane size / 64 cells) — a wavelength anywhere near the
  cell size aliases into chaotic warped craters instead of a smooth roll (found and fixed during
  this session; the fine ripple detail above is unaffected since it lives entirely in the
  fragment shader, where mesh resolution isn't a constraint).
- **Fresnel + alpha:** a small view-angle-dependent brightening, plus alpha rising with ripple
  intensity, so the veins read as slightly more opaque/reflective than open water.

## Shader Graph reproduction spec

For someone rebuilding this as an actual `.shadergraph` asset. Node names are Shader Graph's
built-in nodes unless marked **[Custom Function]**.

| Stage | Nodes | Notes |
|---|---|---|
| Tiling | `Position (World)` → `Divide` by `_TileSize` (200) → `Fraction` | Keeps the Voronoi/hash math working on small numbers regardless of world position — large world coordinates fed directly into a sine-based hash lose precision. |
| Shear | `Radial Shear` (built-in node) | Center (0.5, 0.5), Strength = `_ShearStrength`. |
| Ripple | **[Custom Function]** `VoronoiEdges` | Shader Graph's built-in `Voronoi` node does not expose F1/F2 separately — this needs a custom HLSL function (body is in `Water.shader`'s `VoronoiEdges`) taking UV/density/time-angle/edge-width, returning the F1-F2 edge mask. Everything else (Power, Lerp, Multiply for the two octaves) is standard nodes. |
| Distance blend | `Position (World)` + `Camera (World Space Position)` → `Distance` (XZ only — zero the Y component first) → `Subtract`/`Divide`/`Saturate`/`Smoothstep` → drives every Near/Far `Lerp` | One blend factor, reused for density, speed, edge width, power, and swell amplitude. |
| Swell | **[Custom Function]** `Swell` (two `Sine` waves, summed) in the **Vertex** stage, offsetting `Position.y`; normal recomputed via finite difference (two extra `Swell` evaluations at small XZ offsets, then `Cross Product`) | Keep wavelength large relative to mesh vertex spacing — see caveat above. |
| Output | `Alpha`, `Emission`, `Base Color` | Base Color = Lerp(deep color, shallow/ripple tint, ripple mask). Emission = shallow tint × ripple mask × strength. Alpha = base + ripple×boost + fresnel×strength. |

All the `_Near`/`_Far` pairs, `_ShearStrength`, `_DetailStrength`, `_SwellWavelength`,
`_NearDistance`/`_FarDistance`, and the two colors should become Shader Graph **Properties**
with the same names, so the material keeps its existing Inspector layout either way.

## Deferred (from the second, older article)

Read for inspiration, not implemented — flagged as optional future polish:

- **Gerstner wave vertex animation** (5 combinable waves) — heavier than the sine-swell used
  here, and the swell already gets the "rolling ocean" read at a fraction of the complexity.
- **Paired normal + height maps** for fine surface chaos — would need actual texture assets
  (this shader intentionally uses none) and the article's own author flags the setup as fragile
  (normal/height maps must match exactly, multi-wave steepness isn't normalized).
- **Depth-texture-based fog, refraction, and intersection foam** — requires sampling
  `_CameraDepthTexture`, a real cost on WebGL and a meaningful chunk of added shader complexity
  for a "not photorealistic" stylized target. Worth revisiting only if the flat alpha-blend
  water reads as too flat once textures/lighting are further along.
- **LOD mesh swap for distant water** — the source article uses a separate simplified shader for
  far tiles. Not needed here since there's only one mesh and the distance blend already handles
  near/far behavior inside a single shader.

## Known caveat — animation not visually confirmed by automation

The ripple pattern is driven by `_Time.y`, standard and should animate correctly in normal play.
`Time.time` was confirmed advancing during an automated Play Mode session (21.1s → 28.3s across
screenshot calls), but two screenshots taken several seconds apart via the MCP camera-screenshot
tool came back pixel-identical — most likely because the Game View doesn't necessarily redraw
every simulation tick while the Editor window isn't OS-focused in a remote/automated session, not
a fault in the shader logic itself. **Needs a real, focused Play Mode look to confirm the ripples
are actually crawling** — flagged as the first acceptance criterion on MRM-68.

## Branch note

The original hand-written shader was first built on the `mrm-58` branch (already merged to `main`)
rather than its own branch — MRM-58 was still checked out when Carlos asked for this mid-session.
**Resolved 2026-08-24:** now on its own `mrm-68` branch, created off that `mrm-58` HEAD (so it
carries the shader work forward), per the project's one-issue-one-branch rule. The Asset Store
shader swap above also happened on this branch.
