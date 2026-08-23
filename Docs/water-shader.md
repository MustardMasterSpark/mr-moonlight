# Water Shader — MRM-68

`Assets/_Project/Art/Environment/Water/Water.shader` (`MrMoonlight/StylizedWater`), applied to
`M_Sea.mat` on the `Sea` GameObject. Replaces the flat placeholder from MRM-58 (stock URP/Lit,
no motion) with a stylized, animated ripple shader: calm and detailed near the player, more
chaotic toward the horizon, blended smoothly by distance from camera.

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

This work happened on the `mrm-58` branch (already merged to `main` and closed) rather than its
own branch — MRM-58 was still checked out when Carlos asked for this mid-session. It has its own
issue now, **MRM-68**, suggested branch `mrm-68`. Per the project's one-issue-one-branch rule,
this should move to its own branch before commit — Carlos's call via GitHub Desktop, not done
here.
