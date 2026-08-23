# Change log — Mr. Moonlight

Newest first. One entry per merged issue.
Structure is **BUILT / DECISIONS / FAILED / NEXT** — see `Claude Code Context MDs/kickstart.md` §B.2.

---

## MRM-68 (in progress) — Stylized, animated sea shader replacing the flat placeholder

**BUILT (2026-08-23)**

- New hand-written URP shader, `Assets/_Project/Art/Environment/Water/Water.shader`
  (`MrMoonlight/StylizedWater`), applied to the existing `M_Sea.mat`/`Sea` GameObject from
  MRM-58. Built from Simon Swartout's "Simple Water Shader" Medium article (Voronoi ripples,
  Radial Shear, Power-sharpened edges, vertex displacement) — fully procedural, **zero texture
  maps**. A second, older Gerstner-wave/normal-map article was read for inspiration on the
  calm-vs-aggressive split only; not implemented (see DECISIONS). Full technique writeup,
  parameter table, and a **Shader Graph reproduction spec** (Carlos wants the option to convert
  this to a node graph later) live in `Docs/water-shader.md` — kept out of this changelog entry
  to avoid duplicating a large reference doc.
- **Replaced `Sea`'s mesh.** The MRM-58 quad was 2 triangles by design (near-zero render cost) —
  far too few vertices for any vertex-displacement technique to show. Generated `SeaGrid.mesh`
  (64×64 grid, ~4,225 verts, still trivial GPU cost) via `execute_code` and swapped it onto the
  `Sea` GameObject's `MeshFilter`.
- **Distance-based calm→aggressive blend**: cell density, animation speed, edge sharpness, and
  swell amplitude all `lerp` between Near/Far values on a single `smoothstep` of
  distance-from-camera (XZ). One shader, one mesh — no seam between two separate materials.
- **Found and fixed two real bugs while tuning it**, both worth remembering for future
  procedural-noise shader work:
  1. A screen-space-derivative (`fwidth`) anti-aliasing pass that widened the *entire* brightness
     falloff instead of only softening the threshold crossing — washed the whole pattern out to
     solid bright at grazing angles. Fixed by clamping the AA band to a small fraction of the
     artistic edge width, applied only around the actual threshold.
  2. Swell wavelength (90m) far smaller than the new grid's own cell size (~470m at this plane's
     scale) — aliased into chaotic warped craters instead of a smooth roll. Fixed by using a much
     larger wavelength (1200m) for the geometric swell; fine ripple detail is unaffected since it
     lives entirely in the fragment shader, where mesh resolution isn't a constraint.
- Verified via a temporary camera + `manage_camera` screenshots (near-shore calm cells vs.
  distant chaotic multi-octave veins, both correctly distinct) and zero console errors after
  each shader recompile.

**DECISIONS**

- **Hand-written HLSL, not Shader Graph.** The available tooling creates shader scripts, not
  Shader Graph node graphs — hand-crafting a `.shadergraph`'s JSON directly is fragile enough to
  likely produce a broken/uneditable graph. All parameters are still exposed as material
  properties (Inspector sliders), so day-to-day tuning doesn't need code edits. Full reasoning
  and the node-by-node spec for rebuilding this as an actual Shader Graph are in
  `Docs/water-shader.md`.
- **Gerstner waves, paired normal/height maps, and depth-texture fog/refraction/foam** (all from
  the second article) deliberately deferred — heavier than a "not photorealistic" stylized target
  needs, and the depth-texture sampling in particular has a real WebGL cost. Documented as
  optional future polish in `Docs/water-shader.md`, not lost.
- **This landed on the `mrm-58` branch** (merged to `main` as a checkpoint commit, not a real
  finish — see below) — Carlos asked for it mid-session while that branch was still checked out.
  Given its own issue, **MRM-68**, now that the work exists — per the project's
  one-issue-one-branch rule this should move to its own branch before its own commit, Carlos's
  call via GitHub Desktop.
- **Paused here, deliberately.** Carlos wants to get back to MRM-58's blockout/vegetation pass
  first; this issue holds the water shader as a self-contained unit to resume whenever the next
  polish pass comes around.

**FAILED**

First two tuning passes on the ripple edge math (see DECISIONS/BUILT bug #1) — not a dead end,
just iteration, recorded above since the *reasoning* (don't let AA width scale the whole falloff)
is the reusable lesson, not just the fix.

**NEXT**

- **Carlos: confirm the ripples actually animate in a normal, focused Play Mode session.**
  `Time.time` was confirmed advancing during the automated build session, but two screenshots
  taken several seconds apart via the MCP camera tool came back pixel-identical — most likely
  because the Game View doesn't necessarily redraw every tick while the Editor window isn't
  OS-focused in a remote session, not a fault in the shader's `_Time.y` usage, but not proven
  either way by automation. First acceptance criterion on MRM-68.
- Frame cost not yet profiled in an actual WebGL build (should be cheap — no texture samples at
  all — but not measured).
- Visual pass once Carlos can look at it hands-on: does the near/far transition read as smooth,
  is the aggressive-far pattern actually aggressive enough, does the near pattern read as calm.
- Decide Shader Graph conversion — spec is ready in `Docs/water-shader.md` whenever wanted.

---

## MRM-58 (in progress) — Programmatic terrain block-out from Carlos's map: both islands shaped, water carved, chapel hill raised, sea horizon added

**BUILT — terrain block-out (2026-08-23)**

Carlos supplied a topographic map, a grayscale heightmap, a real-world distance calibration
image, a location map, and a gameplay-area perimeter (now at
`Docs/Design/Island-Terrain-Reference/`, see that folder's README). Executed the plan from the
prior session: read the grayscale pixel-by-pixel, resampled to the terrain's heightmap grid,
pushed in with `TerrainData.SetHeights()`. Scene-view work, done via UnityMCP per standing
permission (Carlos directed this task explicitly this session — "Model in Unity using the
Terrain Shaper... you decide the dimensions"), verified by reading terrain/player state back
and by Scene View screenshots (below), documented here.

- **Scale derivation.** Measured Carlos's two calibration lines in `AANNIARVIK-scale-
  calibration-lines.png` (yellow 1137.8px = 1.72km, red 1225.9px = 1.82km) → real-world
  **~1.498 m/px**, cross-validated by both lines agreeing to within 2%. Carlos's own suggestion
  ("a map with around double those dimensions would be cool") became the final call: **3.0 m/px
  in-game** (~2.003× real scale — landed almost exactly on 2× by rounding to a clean number).
  Verified this against pacing (see DECISIONS) rather than taking the 2× suggestion on faith.
- **Elevation kept at 1:1 real meters**, not doubled with the footprint. `AANNIARVIK-height-
  scale-legend.png` gives a linear white(0m)→black(170m) grayscale mapping, confirmed by
  sampling the legend image's gradient bar directly (row 0 = 255 = 0m, row 1622 = 0 = 170m,
  linear between). Doubling the footprint while holding height constant **halves the average
  grade** — deliberately, since this is a walking/exploration game and gentler slopes read as
  more traversable at the larger scale.
- **Crop + buffer.** Source image is 1490×2258px, mostly empty sea. Cropped to the combined
  land bounding box (both islands + the small decorative islet south of the second island) plus
  a 20-40px margin: source px `x[160,1361] y[30,2225]` (1201×2195px). Added a **250m flat-sea
  buffer ring** on all four sides so the terrain doesn't cut off right at the coastline.
  Content: 3603×6585m. **Final `Terrain.size` = 4103 × 260 × 7085** (X × Y-height × Z),
  `heightmapResolution` **1025** (~4.0m/cell in X, ~6.9m/cell in Z — coarse by design, this is
  a first-pass block-out for Carlos to hand-detail, not final geometry).
- **Water carving, via two BFS passes** over the 1025×1025 grid (source pixel → grid-cell
  classified water via the same blue-channel test used for the land mask: `B > R+40 && B >
  G+20`): (1) flood-fill from every water cell on the grid's outer border classifies "open sea"
  vs. an enclosed body reachable from nowhere ("lake") — the north island's interior water
  system (the lake + narrow channel connecting the two named location clusters) came out
  correctly separated from the surrounding sea, no manual per-body tuning needed; (2)
  multi-source BFS from every water cell adjacent to land gives distance-from-shore in grid
  cells for every water cell. Depth = `min(cap, distance × rate)`, `seaLevel = 40m` (terrain-
  local): **sea** rate 3.5m/cell, cap 35m (floor at 5m); **lake** rate 2.0m/cell, cap 22m. A
  narrow strait/river only a few cells wide never reaches its cap and stays shallow; a wide lake
  does — "larger bodies of water go deeper" fell out of the distance model instead of needing
  per-body hand tuning. Also directly satisfies "stop digging past a certain distance, keep
  flat" — once distance-from-shore clears the cap threshold (~2-3 cells past the buffer's inner
  edge), the seafloor is already perfectly flat.
- **Chapel hill.** Chapel marker (`AANNIARVIK-locations.png`, black box, centroid px
  (407,422)) → world (991, 1426). Added a `Mathf.SmoothStep` boost, +25m at the chapel's
  position tapering to 0 at 150m radius, on top of whatever the source map's own grayscale
  already gave that spot (which sampled at ~90m elevation before the boost — the map already
  placed the chapel partway up high ground, matching "chapel is on a hill" in the task
  description; the boost pushes it the rest of the way to read clearly on the skyline).
- **Small-scale roughness.** Two-octave `Mathf.PerlinNoise` (±2.5m at ~50m wavelength, ±1m at
  ~12.5m), land cells only, added after elevation + chapel boost, before normalization.
- **Sea/water visualization**: `M_Sea.mat` (`Assets/_Project/Art/Environment/Water/`),
  `Universal Render Pipeline/Lit`, Transparent surface, base color `(0.04, 0.22, 0.38, 0.62)`,
  smoothness 0.65, shadows off both ways (doesn't cast, doesn't receive) — a stock URP shader,
  no custom shader code, itch.io-proven surface type. A single large flat quad (`Sea`
  GameObject, no collider — purely visual, never satisfies the player's `Ground`-layer
  ground-check, layer left at `Default`) at Y=40 (sea level), 30,000m across, centered on the
  terrain — one quad (2 tris) handles both "water fills the carved coastline/lake basins" (since
  the terrain's dug-out areas sit below Y=40) and "distant sea horizon" at effectively zero
  extra render cost. `Main Camera.farClipPlane` raised **1000 → 4000** so that horizon is
  actually visible instead of clipped — flagged in DECISIONS as a first pass, pairs well with
  distance fog later.
- **Player repositioned** to the campsite's world position (1006, ~79.8, 2704 — sampled from
  the baked terrain, not guessed), facing roughly toward the glade (next script location).
  Old position (0,0,0) was a corner of the old 1000×600×1000 terrain and would have spawned in
  open water/void on the new one. **Not** the same thing as placing the location markers
  themselves — Carlos still does that by hand, per his own note on `AANNIARVIK-locations.png`.
- **Live-verified**: entered Play mode, player settled at Y≈79.79 (matches the sampled terrain
  height, didn't fall through), zero console errors/warnings. `cc.isGrounded` read `false` at
  rest, same known-quirk already documented on `GroundCheckDistance` in `MoonlightTunables` —
  not a regression, `PlayerController`'s own SphereCast ground check is what actually matters
  and the terrain collider clearly held the player up.
- Three Scene View screenshots taken confirming shape fidelity against the source map (both
  islands + islet, correct silhouette), the carved lake basins (visible depression with banked
  walls), and the chapel hill (reads as a distinct raised dome). Saved under
  `Assets/Screenshots/` (Editor default location, not attached here).

**BUILT — orientation fix, beach smoothing, corrected player spawn (2026-08-23)**

Carlos added `Docs/Design/Island-Terrain-Reference/Map/AANNIARVIK orientation.png` (a compass
rose + "P" player-start marker overlaid on the same source map) and flagged two problems with
the block-out above: the island was mirrored north/south, and every land/water edge was a
vertical "cannon wall" instead of a beach. Full terrain regenerated from scratch via UnityMCP
`execute_code`, same permission basis as the prior pass, verified live (top-down and oblique
Scene View captures, Play Mode spawn check) rather than assumed.

- **Root cause of the flip, confirmed not guessed.** The chapel marker's centroid (found by
  scanning `AANNIARVIK-locations.png` for its black-square cluster: bbox x[403,411] y[414,429],
  centroid (407, 421.5) — matches the prior session's own (407,422) almost exactly, cross-
  validating the pixel-scan method itself) recomputes to world Z=5660.5 under a corrected
  north-up mapping. `7085 − 1426 = 5659`, matching the *old* (flipped) Z=1426 to within a
  meter. That is the signature of a classic Unity gotcha: `Texture2D.LoadImage` stores row 0 as
  the *bottom* of the image (OpenGL V=0 convention), but the old sampling code evidently read
  "row N from the top of the PNG" directly as the texture's row index without flipping it,
  mirroring the whole island north/south. Fixed by sampling
  `pixels[(srcHeight - 1 - rowFromTop) * srcWidth + col]` explicitly. Re-verified against the
  reference image with a temporary top-down orthographic camera (`TempTopDownCam`, deleted after
  use) — silhouette, compass orientation, and the lake lobe's position (west, matching the
  reference) all line up; screenshots in `Assets/Screenshots/`.
- **A second, unrelated bug found and fixed while rebuilding: a missing sea-level offset.** The
  height-scale legend's 0–170m is real-world elevation *above* sea level, but the regenerated
  land-height formula was writing that value directly as the terrain-local height instead of
  `seaLevel + elevation`. Silent effect: any pixel with real elevation under ~47m (a large
  fraction of a real coastal island) baked in *below* the local sea level (Y=40) regardless of
  the beach work below — confirmed by an interim run where 54% of all land cells came out
  underwater. Fixed by anchoring land height at `seaLevel + elevAboveSea` before boosts/
  roughness; re-verified (0.07% of land cells sit below sea level afterward, all inside the
  intentional beach taper's roughness noise, not a residual bug).
- **Beach/shoreline smoothing**, applied uniformly to every land/water boundary (sea, lake, and
  the inter-island channel alike — no per-body-type logic needed, same as the existing water
  BFS): a third multi-source BFS (mirroring the existing water-distance BFS) computes each land
  cell's distance-in-grid-cells from the nearest water cell. Within a 14-cell band (~55–95m,
  anisotropic since the grid cells aren't square at this heightmap resolution — coarse by
  design, same block-out caveat as before), height is `Lerp(seaLevel + 1m, naturalHeight,
  smoothstep(dist/14))`: a 1m beach lip at the waterline easing up to full natural terrain,
  replacing the old hard cliff. Beyond the band, terrain is untouched.
- **"Lower the island as a whole"**, per Carlos's direction, rather than just re-grading the
  coastline and having to compensate the interior afterward: land elevation *above* sea level is
  scaled **0.75×** before boosts/roughness (was 1.0× / unscaled in the original block-out).
  Combined with the sea-level-offset fix above, this gives gentler overall grades without
  sinking low-lying land — max terrain height came out at 138m (vs. budget of 260m), comfortable
  headroom.
- **Player spawn moved to the "P" marker**, not reused from the old campsite guess. Pixel-scanned
  the orientation image the same way as the chapel (single largest near-black cluster, isolated
  from the compass rose's 8 letter-clusters by size: 359px vs. 50–102px each): centroid (407.1,
  862.5) → world (991.3, 4337.5). That exact point sampled at ~26m — a real, natural low point on
  the narrow neck of land between the lake and the strait, not a bug — well under the 40m sea
  level. Rather than fight the surrounding beach grading (the nearest naturally-dry point was
  170m+ away — this whole neck is legitimately low), gave it the same treatment as the chapel: a
  small local landmark boost (`+16m within a 40m falloff radius, no beach-taper dilution inside a
  22m core pad`), the same pattern already established for the chapel hill. Final spawn height
  79.5m. **Flagged as a deliberate, explicit exception** to the plain distance-based beach
  grading — worth Carlos's eyes when he does his own location-placement pass, in case he'd rather
  move the campsite itself than rely on a boosted pad.
- Facing set to world +X (east, roughly toward the lake/interior) since the old rotation (Y=80°)
  was tuned for the flipped orientation and no longer means anything meaningful — a placeholder
  for Carlos to redo once he's placed the actual campsite dressing.
- **Live-verified**: Play Mode, player settled at Y≈79.62 (matches the sampled terrain height),
  zero console errors/warnings, same ground-check quirk noted previously (not a regression).
- **Build #10** — `E:\Builds\10 - Terrain Reshape - 2026-08-23\Build.zip`, WebGL, 11.84 MB,
  0 build errors (1 informational "1 URP assets included in build" log, not an issue). Verified
  the Unity MCP bridge was actually connected (`mcpforunity://instances`) before triggering,
  per the note below from the interrupted build last session. Zip built entry-by-entry with
  forward-slash paths and verified via `ZipFile` entry-name inspection (all 6 entries clean, no
  `\` separators) — `Compress-Archive`/`ZipFile.CreateFromDirectory` still avoided per the MRM-8
  zip-separator bug.

**BUILT (prior session)**

- `Assets/_Project/Scenes/Island.unity` — new scene, replaces the deleted `SampleScene`. Carlos
  copied over the HUD Canvas and Player prefab from prior work and added a `Terrain` object.
  Registered in Build Settings as scene 0 (Sandbox kept as scene 1).
- **Ground layer fix (scene-view, permission granted, done via UnityMCP, verified by reading the
  component back):** the new `Terrain` defaulted to layer `Default`, not `Ground` — jump silently
  never worked because `PlayerController.CheckGrounded()`'s SphereCast only tests the `Ground`
  layer (see MRM-9's own note on this requirement). Fixed: `Terrain` → layer `Ground`, scene saved.
- **Cross-issue fix, MRM-9 scope, done on this branch (flagged, see comment on MRM-9 too):**
  jump could "ram" up any slope regardless of `SlopeLimit`, since the ground-check only tested
  "is there ground below," never the surface angle — `CharacterController.slopeLimit` only
  governs the walking `Move()` resolution, so it never got a chance to reject repeated jump+land
  hops climbing a steep slope a step at a time. Fixed with sliding instead of a jump block:
  standing on ground steeper than `SlopeLimit` now slides Tracey back down it at a constant
  speed, jump itself stays available on any slope (including jumping off one back to flat
  ground). `PlayerController.CheckGrounded()` now also returns the ground hit's normal so
  `UpdateMove()` can derive the slope angle and downhill direction.
- **New tunable:** `SlideSpeed` (4 m/s default) on `MoonlightTunables`, next to `SlopeLimit`.
- **New debug-only tool, not tied to any acceptance criteria:** `DebugFlyController.cs` — an
  inspector-checkbox-toggleable free-fly/noclip mode for Carlos to visually inspect terrain shape
  and inter-location distances from the player's own POV, no collision/gravity/stamina/slope.
  Full 3D fly in the look direction, keyboard+mouse and gamepad both live simultaneously (same
  "whichever device fires" philosophy as MRM-8's `InputMapController`). Reads `Keyboard`/`Mouse`/
  `Gamepad` directly, not the shared Gameplay action map, and only ever flips
  `PlayerController`'s and `CharacterController`'s `enabled` flags — never reaches into
  `PlayerController`'s own fields, so normal movement is untouched. Same "debug tool, not
  shipped" category as `InputDebugOverlay`. Not attached to the Player prefab yet.
- Build `9 - Island Blockout - 2026-08-22` — Island + Sandbox both in Build Settings, uploaded
  for Carlos to test on itch.io.

**DECISIONS (terrain block-out, 2026-08-23)**

- **Pacing math behind the 2× scale call.** Location markers on `AANNIARVIK-locations.png`,
  converted to world meters at 3.0 m/px, give a straight-line surface route (campsite → glade →
  cabin → Flak Tower → mine entrance, then mine exit → well → chapel; the mine itself is a
  teleport, not surface distance) of **~1.96km straight-line**. Real walking paths around lake
  barriers and elevation typically run 1.3-1.6× straight-line, so **~2.5-3.1km actual overland
  route**. At `WalkSpeed` 3.0 m/s that's ~14-17 min of pure walking; blended with some
  `SprintSpeed` (5.5 m/s) use, closer to ~12-13 min. Carlos's target is a 40-50 min full
  playthrough with the mine (his own "~10 min section") carved out separately, leaving ~30-40
  min for the overland leg — pure movement at ~12-17 min of that leaves comfortable room (over
  half the budget) for the Flak Tower combat gauntlet, other encounters, backtracking, and
  "wasting a little time," per the brief. **This is what confirmed 2× rather than something
  larger** — a 4× scale would have pushed pure walking alone past 25-30 min, leaving too little
  room for combat inside the 30-40 min overland budget.
- **World orientation convention, since the source map is oriented (north = top of image) but
  Unity has no inherent compass:** in `Island.unity`, **+X = east, -Z = north** (i.e. image row
  0/north maps to terrain Z=0 at the buffer's inner edge, image column increasing = world +X).
  Worth remembering before placing anything with the source map open side-by-side.
- **Heightmap resolution 1025, not 513 (the placeholder default) or higher.** 513 was too coarse
  for a 4.1×7.1km footprint (~8m/cell); 2049 would be 4× the memory for detail this pass doesn't
  need, since Carlos hand-sculpts on top of this anyway. 1025 (~4-7m/cell) is the middle ground.
- **Terrain dimensions and the sea/lake depth constants are NOT routed through
  `MoonlightTunables`** — same reasoning as the vegetation/staging numbers below: this is
  one-time level-design geometry set by an editor bake, not a runtime-tunable gameplay constant
  scripts read every frame. `MoonlightTunables` stays reserved for values code actually consumes
  live.
- **Reference images moved to `Docs/Design/Island-Terrain-Reference/`, not `Assets/`.** They're
  inputs to a one-time editor bake (read via `File.ReadAllBytes` from an absolute path, not a
  Unity-imported asset), not runtime textures — same category as the existing screenplay/pitch/
  style docs already living under `Docs/Design/`, not the `Art/Environment/` folder
  `Docs/unity-conventions.md` reserves for actual in-game terrain textures. Renamed the 10 files
  from cryptic/spaced names (`AANNIARVIK y1,72 r1,82.png`, `LrNUdu.jpg`) to descriptive
  kebab-case/prefixed ones — see that folder's own README for the mapping.
- **Mine geometry is explicitly out of scope for this pass** — "the area of the mine is not
  represented here" per Carlos's brief, and it's still an open question in `00-INDEX.md` whether
  it's carved into the island or a teleport to a separate space. Its own ~10-minute pacing target
  is noted above for whenever that issue lands, but nothing about the mine was modeled.
- **No terrain texture layers assigned.** Bare/default terrain material, matching "we're not
  going to fill it with trees, just do the shape" — texturing and vegetation are explicitly
  Carlos's own pass (Vegetation Spawner, per the prior session's decision), not this one's job.

**DECISIONS**

- **This issue got auto-closed by Linear's GitHub integration** when its linked PR (#8) merged
  to `main`, then reopened same-session — Carlos merges to `main` as a checkpoint/safety commit
  (his own stated habit, likely to recur on other branches too), not necessarily as "this issue
  is finished." Worth remembering: a merged PR or a `Done` status in Linear doesn't reliably mean
  an issue's acceptance criteria are actually met for this project — check with Carlos rather
  than assuming.
- **Flora Instancer dropped from this issue's scope** — Carlos only owns the free Vegetation
  Spawner (Staggart Creations), not Flora Instancer/Renderer (a separate paid BRG-based rendering
  engine with unconfirmed WebGL support). Vegetation Spawner drives Unity's built-in terrain
  tree/detail instancing, which should be sufficient for this pass; revisit only if profiling
  later shows a rendering bottleneck that combination can't solve. Noted inline in the issue
  description.
- **Vegetation/staging numbers deliberately NOT routed through `MoonlightTunables` yet, at
  Carlos's explicit call.** He wants to place trees freely with the Vegetation Spawner first and
  see how a real WebGL build actually holds up, rather than have every count/radius decision go
  through a tunable up front. Revisit once a build shows a real frame-rate/budget problem — at
  that point tunable-izing the numbers becomes part of the actual fix, not a process step ahead
  of one.
- **Slope handling: slide, not a jump block.** First pass blocked jump outright on steep ground;
  Carlos found that felt wrong (still wanted to jump off a slope back to flat ground) and asked
  for sliding instead — jump-climbing steep terrain is prevented by the slide itself (you can't
  net gain height jumping onto ground that immediately pushes you back down), not by refusing the
  jump input.

**FAILED**

Nothing to record.

**NEXT**

- **Carlos:** hand-detail the block-out — the eight location placements (campsite, glade,
  cabin, Flak Tower, mine entrance, mine exit, well, chapel; final positions are his call per his
  own note on `AANNIARVIK-locations.png`), soften/vary the lake-basin walls if the carved banks
  read too geometric up close, add rocks/noise breakup Perlin alone won't give, and the actual
  vegetation/texture pass (Vegetation Spawner, still deliberately un-tunable-ized — see
  DECISIONS above). Walk the route before detailing, per the issue's own instruction — the
  Player now spawns at the campsite specifically so that's possible immediately.
- **Sanity-check the current numbers** now that they're chosen rather than requested: **map
  footprint 4103×7085m** (unchanged), land elevation now **0.75× scaled + a seaLevel(40m)
  offset** (was 1.0×/unscaled), **peak height ~138m** (was ~171m + chapel boost), beach taper
  **14 grid cells (~55–95m)** from every shoreline, player spawn-pad boost **+16m within 40m** of
  the "P" marker. If any of these feel wrong hands-on — too flat, beach band too wide/narrow,
  spawn pad too obviously a bump — all re-bakeable in one script run, not hand sculpting to redo.
- **Carlos: sanity-check the player spawn-pad boost specifically.** The "P" marker sits on a
  genuinely low neck of land between the lake and the strait (nearest naturally-dry ground was
  170m+ away), so it got a small deliberate raised-clearing boost rather than being moved. Worth
  a look during the location-placement pass in case a moved campsite reads better than a bump.
- **Sea plane and camera far-clip are a first pass**, not final. `M_Sea`'s color/alpha and the
  4000m far clip are guesses at "simple yet pretty" — pairing the far clip with distance fog for
  a softer horizon fade-out is a natural next step whenever lighting/atmosphere work starts.
- **Mine section not modeled** — still needs its own space (carved-in vs. teleport, per the open
  question in `00-INDEX.md`) and its own ~10-minute pacing pass once that issue is picked up.
- `DebugFlyController` needs adding to the Player prefab/GameObject in the Inspector (or ask
  Claude to do it via UnityMCP) before it's usable — not wired into any scene yet. Genuinely
  useful now for walking/flying the new terrain at scale.
- Watch the WebGL budget once vegetation starts going down — this is the acceptance criterion
  most likely to need a build-and-check cycle before it's done. The terrain itself (1025²
  heightmap, no texture layers yet) is cheap; the vegetation pass is where the real budget risk
  is.
- `SlideSpeed`'s constant-speed slide is flagged as a polish-pass candidate (friction/acceleration
  curve) now that real terrain shapes (including the lake-basin banks, which are steep enough to
  act as the "natural barrier" the brief asked for) exist to tune it against.

---

## MRM-17 (wrap-up) — 5 of 6 acceptance criteria confirmed, ready to commit

Carlos confirmed hands-on with screenshots (2026-08-22), on top of everything already verified
live during the build below.

**BUILT** — see the full build/decisions detail in the entry directly below; not repeated here.

**NEXT**

- **Confirmed by Carlos:** crouch stance preserved through the fall (capsule size and Speed
  1.50 m/s hold, camera moves from the crouched position) · capsule falls in a varied direction
  every time.
- **One caveat on the direction criterion, accepted as-is for now:** only the camera tilts — the
  `CharacterController` capsule and Tracey's placeholder body stay upright, since there's no
  ragdoll/animation to drive a real physical fall. Logged as a follow-up on MRM-67 (Polishing
  Details) for once a real model/rig exists.
- **Left deliberately open, not failing:** the inventory-cleanup criterion. No inventory exists
  yet (MRM-42/MRM-16 both Backlog) — Carlos's call is to leave this as an explicit open point on
  the issue rather than tick it or mark it N/A, and revisit once the inventory is built.
- 5 of 6 acceptance criteria now checked in Linear. Ready for Carlos to commit via GitHub
  Desktop.

---

## MRM-17 (in progress) — Death sequence built, verified live in Sandbox, handed off for testing

**BUILT**

- `Assets/_Project/Code/Runtime/Player/PlayerStats.cs` — edge-triggered `event Action OnDeath`,
  same pattern as `OnStaminaTired`, fired once when `Health.Value` reaches 0.
- `Assets/_Project/Code/Runtime/Player/PlayerController.cs` — `DisableControl()` (permanently
  stops `Update`, freezing crouch/camera state exactly as it was), `ResetCameraPitch()`, and a
  public `CameraPivot` accessor for MRM-17 to drive directly.
- `Assets/_Project/Code/Runtime/Player/DeathSequence.cs` — orchestrates the full sequence:
  disable control, force-close HUD, camera-tilt-and-shake fall while the red tint rises, scream,
  hold, cut to black, scream tail, game over.
- `Assets/_Project/Code/Runtime/VFX/ScreenTint.cs` + `ScreenTintRenderer.cs` — the shared
  additive red-tint mechanism. A static contribution registry (`SetRed`/`ClearRed` by source
  name), agnostic of who calls it, plus the one renderer that sums contributions, clamps to the
  new shared `MoonlightTunables.RedTintCeiling`, and draws them as a full-screen UI Image alpha.
  Built ahead of MRM-53 specifically so its future health tint can plug into the same mechanism
  instead of a parallel one — see the note left on that issue.
- `Assets/_Project/Code/Runtime/UI/HudCloseRequest.cs` — narrow no-op stub event for force-closing
  the inventory/map on death; no subscribers yet since MRM-42/MRM-16 are both Backlog.
- `Assets/_Project/Code/Runtime/UI/GameOverPanel.cs` — minimal unstyled landing stub (`Show()`
  only, no buttons). MRM-19 owns the real game over screen — see the note left there.
- `Assets/_Project/Code/Runtime/Audio/SoundPool.cs` + the empty `DeathScreamPool.asset` —
  matches the sound-pool shape already described in `Docs/unity-conventions.md`.
- Scene: `DeathSequence` + a scream `AudioSource` added to `Player.prefab`; a `HUD Canvas`
  (960×540 scaler) added to Sandbox with the red tint image, black-screen image and game-over
  stub, all wired and saved.
- `Assets/_Project/Code/Runtime/VFX/HealthRedTintSource.cs` — **MRM-53 scope, built ahead of
  schedule at Carlos's request the same day.** Sits on the Player prefab, continuously feeds
  current health into `ScreenTint` as a second contributor (`"HealthDamage"`) alongside the death
  tint, using the new `HealthRedTintCurve` tunable. See the follow-up note left on MRM-53.
- Carlos supplied `Assets/_Project/Art/HUD Textures/Veins_1.png` (a transparent veins overlay,
  CoD-damage-vignette style) for the tint's actual art. Imported under the existing `Tex_UI`
  preset from MRM-6 (Default texture type, no Sprite), so `ScreenTintRenderer` renders it via a
  `RawImage` rather than `Image` — both `Red Tint` in Sandbox and the renderer code were swapped
  accordingly.
- Death yell audio: Carlos recorded and exported 5 clips (WAV, mono, 22050 Hz, per the export
  guidance given this session). Renamed/reorganized from his working names
  (`Aud_PSFX_DeathN.wav` in `Audio/PLAYER_SOUNDS/`) to `PLR_Death_01.wav`…`PLR_Death_05.wav` in
  `Assets/_Project/Audio/Player/` — sibling to the project's existing (previously empty)
  `Audio/SFX/` and `Audio/VO/` category folders. Added a new **`PLR_` prefix** (Tracey's own
  non-dialogue vocalizations — death, pain, effort — as opposed to `VO_`'s spoken dialogue
  lines) and a matching **`Aud_PlayerVox` preset** (`Aud_VO_Dialogue`'s settings under its own
  name, so the two can diverge later without a rename), wired into the Preset Manager's
  filename-filter list via the `Preset`/`DefaultPreset` API — not a hand-edit of
  `ProjectSettings/PresetManager.asset`, which the running Editor won't pick up live. Documented
  in `Docs/unity-conventions.md` and `Docs/audio-import-workflow.md` (which also had a stale
  "ENM_/UI_ missing from the naming table" note removed — that gap had already been fixed
  separately and the note wasn't). All 5 clips assigned to `DeathScreamPool.asset`'s `Clips`
  array and live-verified (`TryGetRandomClip` returned a real clip and pitch in Play mode).

Tunables added: DeathFallDuration, DeathHoldBeforeBlackDuration, DeathRedTintCurve,
DeathCameraShakeAmplitude, DeathCameraShakeFrequency, DeathScreamTailDuration, RedTintCeiling
(shared with MRM-53), HealthRedTintCurve (MRM-53 scope, see above).

**DECISIONS**

- Fall is a camera-pivot tilt + Perlin-noise shake, not a physically simulated capsule tip-over —
  the player has no Rigidbody/ragdoll and building one felt out of scope. Flagged for Carlos; the
  amplitude/frequency/duration are already tunable without code, but a hand-authored fall
  (Cinemachine impulse/dolly, or a real Animation Clip) would replace `DeathSequence.FallAndTint()`
  — a legitimate polish-phase follow-up, not a rewrite of the sequencing. Cinemachine isn't
  installed in the project yet.
- Sound mute uses `AudioListener.pause` + `AudioSource.ignoreListenerPause` on the scream source,
  not an `AudioMixer` — none exists in the project yet, and this is a clean built-in fit.
- Red tint renders via a full-screen `RawImage` rather than a URP Volume override, since no
  post-processing profile exists in the project yet. `ScreenTintRenderer` is the only class that
  would need to change if a Volume-based approach lands later.
- `HealthRedTintCurve` defaults to the same shape as `DeathRedTintCurve` for now, per Carlos's
  request — MRM-53 should retune it independently rather than assume the two must stay identical.

**FAILED**

- `HealthRedTintCurve` ramping to 1.0 (matching `DeathRedTintCurve`'s shape) let the health tint
  alone saturate `RedTintCeiling` by the time health hit 0 — the death tint's own rise then had
  zero headroom to add anything, so death was visually indistinguishable from "already low
  health." Confirmed live: alpha pinned at the 0.85 ceiling through the whole death sequence
  regardless of the death curve's value. Fixed by capping `HealthRedTintCurve` at 0.4 instead —
  see NEXT.
- Discovered mid-fix: `MoonlightTunables.asset` hadn't been re-serialized since MRM-12, so
  several fields (including `HealthRedTintCurve`, moments after I'd just changed its C# default)
  were silently running on stale in-memory values rather than picking up the new default — a
  missing-from-YAML field only gets its C# default applied the *first* time it's ever
  instantiated in a session; after that, Unity's domain-reload snapshot carries the live value
  forward regardless of source changes. Fixed by explicitly setting the field on the loaded
  asset and calling `AssetDatabase.SaveAssets()`, which re-serialized every field's current value
  into the `.asset` file — worth remembering next time a tunable's default needs to change after
  it's already been touched once in a running Editor session.

**NEXT**

- Live-verified in Play mode (Health drained to 0 via code): control disabled, tint reached
  ceiling, instant cut to black, `AudioListener.pause` engaged, game over panel activated, no way
  to regain control. Compiles clean, zero console errors.
- `HealthRedTintSource`'s math verified directly (0.784 contribution at 30/100 health, matching
  the curve) — the live on-screen alpha update in Play mode couldn't be watched frame-by-frame in
  this session because the Editor runs heavily throttled while unfocused during MCP-driven
  testing (2 engine frames in ~2 minutes of wall time). Not a code issue; worth a normal
  hands-on look once Carlos is driving it directly.
- **Not yet verified by Carlos hands-on** — acceptance criteria intentionally left unticked in
  Linear pending his test pass and his call on the fall/game-over decisions above.
- ~~Death scream pool is empty~~ — filled, and **Carlos confirmed the yells play correctly**
  in-game. (An earlier note here claimed the clips were silent, based on `AudioClip.GetData()`
  reading peak/RMS 0.0 on all 5 — that reading was a false negative, not a real problem;
  correcting the record rather than leaving it stand. Likely `GetData()` behaving oddly on a
  Compressed-In-Memory Vorbis clip in-editor rather than anything wrong with the files
  themselves. Trust Carlos's hands-on ear over that specific diagnostic next time.)
- **Death tint still wasn't visible after the HealthRedTintCurve fix above — second, deeper bug,
  now actually fixed.** Carlos confirmed live: the veins texture intensified correctly, but the
  centre of the screen stayed sky-blue right through death — no red wash at all. Root cause:
  `Veins_1.png` is an edge-concentrated damage-vignette texture with a **transparent centre** —
  no amount of alpha on that texture alone can ever redden the middle of the screen, so the
  "extremely red before the blackout" the issue calls for was structurally impossible with a
  single textured layer, no matter how the curves were tuned. Fixed by giving
  `ScreenTintRenderer` a second layer: a flat, full-screen solid-red `Image` (`Red Tint Flat`,
  Sandbox) sitting *behind* the veins `RawImage`, both driven by the same computed alpha. Verified
  with an actual in-game screenshot at death (Health 0, Death tint at 1) — the screen genuinely
  floods red edge-to-edge, veins visible as detail on top. This is the fix that should have
  shipped the first time; the single-texture approach couldn't have worked as specified.
- **Third round: Carlos caught a visible border/corner where the tint didn't reach**, screenshot
  from the actual Game view at 16:9. Measured it precisely with `RectTransform.GetWorldCorners`
  rather than guessing: `Red Tint`, `Red Tint Flat`, `Black Screen` and `Game Over Panel` all had
  a **leftover non-1.0 `localScale`** (0.96–1.10 depending on the object) baked in from however
  each was created via the MCP bridge, before their stretch anchors were applied - the anchors
  and offsets were correct, but the scale was silently shrinking/growing the rect around the
  screen centre on top of that, leaving (or overshooting) a border. Also confirmed in the process
  that `offsetMin`/`offsetMax` set via the MCP `manage_components` tool's SerializedProperty path
  don't reliably take (those are computed C# properties on `RectTransform`, not raw serialized
  fields) - the actual fix had to go through real C# (`execute_code`) setting `anchorMin`,
  `anchorMax`, `offsetMin`, `offsetMax` **and `localScale = Vector3.one`** together. Also re-learned
  the hard way that Play Mode edits don't persist - the first pass of this exact fix was made
  while in Play Mode and silently reverted the moment Play Mode stopped; redone in Edit Mode and
  confirmed via a fresh Play session afterward. All four overlays now measure exactly
  `(0,0)`-`(Screen.width,Screen.height)`, verified both numerically and with an in-game
  screenshot showing full edge-to-edge red with no border.

---

## MRM-12 (wrap-up) — All acceptance criteria confirmed, ready to commit

**BUILT**

- Confirmed live in Sandbox: Health drops on contact with `HealthTest`; Melee/Defense modifiers
  visibly stack on the debug HUD (F2) walking into `MeleeTest`/`DefenseTest`; Audio Pitch glides
  audibly rather than snapping walking into `PitchTest`; stamina drains on the curve while
  sprinting and regenerates after the delay; jump/sprint both refuse at 0 stamina.
  `StatModifierStackingTests` — 5/5 passing. Fresh recompile, zero console warnings.
- All six MRM-12 acceptance criteria ticked in Linear.

**NEXT**

- Ready to commit and merge to `main`. See commit proposal in this session's conversation.
- **Recommended next issue: MRM-11** (Event Director) — the project's own stated biggest
  blocker, unblocks MRM-62 and the narrative critical path. Opus-scoped for the format/
  architecture decision; start it in a fresh session rather than carrying this one's context
  over, per the model-discipline rule in `kickstart.md` §B.5.
- **If staying in Sonnet right now instead: MRM-17** (Death sequence) — small, unblocked,
  consumes the `Health` stat this issue just built.

---

## MRM-12 (in progress) — Core stat framework built, handed off for scene wiring

**BUILT**

- `Assets/_Project/Code/Runtime/Player/StatModifier.cs` — `StatModifierType` (Additive /
  Multiplicative) and the `StatModifier` struct (source, type, value).
- `Assets/_Project/Code/Runtime/Player/Stat.cs` — the generic modifier-stacked stat. One class
  covers all six stats, per the issue's Model note. Formula: `Value = (BaseValue + sum of
  additive) * (product of multiplicative)`, documented on the class with a worked example.
  Also owns `Deplete`/`Restore` (direct pool mutation, for damage/regen) and `Lock`/`Unlock`
  (§11.6 — pins `Value`, bypasses the modifier stack, until unlocked).
- `Assets/_Project/Code/Runtime/Player/PlayerStats.cs` — owns the six `Stat` instances (Health,
  Stamina, Speed, MeleeDamage, Defense, AudioPitch). Subscribes to `PlayerController.OnJumped`
  and `OnSprinting` (MRM-9's hooks) for jump/sprint stamina costs. Stamina drains on
  `StaminaDrainCurve` (curve input: fraction of stamina remaining) while sprinting, regenerates
  at a flat rate after `StaminaRegenDelayAfterSprint` seconds of not sprinting. `OnStaminaTired`
  / `OnStaminaHyperventilating` fire edge-triggered at the 50%/20% thresholds — empty
  subscriber lists, ready for Carlos's breathing sound pools. `CurrentAudioPitch` chases the
  modifier-stack target via `Mathf.MoveTowards` at `AudioPitchTransitionSpeed`, so pitch never
  jumps instantly. `ConsumeSwingStamina()` is public and unused, ready for the Pickaxe issue.
- `Assets/_Project/Code/Runtime/Player/PlayerStatsDebugOverlay.cs` — same on-screen-overlay
  shape as MRM-8's `InputDebugOverlay`, hidden by default (`visible = false`), toggled with F2 /
  gamepad Start. Positioned below `InputDebugOverlay`'s rect so both can be on at once in
  Sandbox.
- `MoonlightTunables.cs` — new `Player Stats — MRM-12` header: `MaxHealth`, `MaxStamina`,
  `StaminaDrainCurve`, `StaminaRegenRate`, `StaminaRegenDelayAfterSprint`, `JumpStaminaCost`,
  `SwingStaminaCost`, `StaminaTiredThreshold` (50), `StaminaHyperventilateThreshold` (20),
  `BaseMeleeMultiplier` (1.0), `BaseDefenseMultiplier` (1.0), `AudioPitchTransitionSpeed`.
- `Assets/_Project/Code/Tests/EditMode/StatModifierStackingTests.cs` (new
  `MrMoonlight.Tests.EditMode` asmdef, EditMode/NUnit) — 5 tests proving the stacking rule: an
  additive + multiplicative modifier applied simultaneously produce the documented result,
  removing one source's modifier leaves the other intact, two multiplicative modifiers multiply
  their factors (not sum their percentages), values clamp to `MaxValue` even when modifiers would
  exceed it, and `Lock` bypasses modifiers until `Unlock`. All 5 pass (`MrMoonlight.Tests.EditMode`
  run via the Unity Test Runner).

**DECISIONS**

- **Speed unified with `PlayerController`, per Carlos's explicit call after reviewing the
  tradeoff.** `PlayerController` now optionally links to a sibling `PlayerStats`: it writes its
  computed base speed (walk/sprint/crouch) into `Speed.BaseValue` every frame and moves at
  `Speed.Value` (the modifier-stacked result) instead of its own raw number, so boots/weapon/
  substance modifiers actually affect movement. The link is optional (null-checked, falls back to
  MRM-9's original behaviour) so prefabs without `PlayerStats` are unaffected.
- **Same pass also gates jump and sprint on stamina**, at Carlos's request: at exactly 0 stamina,
  jump is refused and sprint silently falls back to a walk, instead of firing for free. Melee is
  not gated — no attack action exists yet to gate (Pickaxe issue's job).
- Modifier stacking rule documented on `Stat`'s class doc comment (canonical, single source) per
  the issue's "document the stacking rule" requirement, rather than a separate docs file — same
  reasoning as the tunables override pattern being documented once on `MoonlightTunables.cs`.
- Health/Stamina reuse the same `Stat` class as Melee/Defense/Speed/Pitch rather than a separate
  "pool" type — `Deplete`/`Restore` mutate `BaseValue` directly, while the modifier list stays
  available for future capacity/rate modifiers (a "difficulty" max-health boost, say), keeping
  one mechanism for all six per the Model note instead of two.
- Event director's `stat` verb (`op=lock`/`op=unlock`, MRM-11) isn't built yet, so there's no
  string-keyed dispatch — `Lock`/`Unlock` live directly on each `Stat` instance
  (`playerStats.Stamina.Lock(...)`). MRM-11 calls these directly when it exists; no premature
  abstraction added ahead of that consumer.

**FAILED**

- `create_script` rejected the first version of `Stat.cs` with a false-positive "duplicate
  method signature" on an expression-bodied `Value` property that called a private helper method
  of the same name pattern. Fixed by inlining the computation directly into `Value`'s getter
  instead of delegating to a separate method — not a real duplicate, just something about that
  shape the script validator didn't like.

**DECISIONS (cont.)**

- **Scene-view exception, at Carlos's explicit request:** he'd already created `HealthTest`,
  `MeleeTest`, `DefenseTest`, `PitchTest` GameObjects (each with a visualization Cube child) and
  asked Claude to attach the debug scripts directly rather than hand off instructions — normally
  `CLAUDE.md`'s "stop at the scene view" rule. Done via the UnityMCP bridge, verified by reading
  the components back afterward:
  - `HealthTest/Cube` — its existing `BoxCollider` set to `isTrigger = true`, plus
    `StatDebugPoolZone` (Target Pool: Health, Restores: false, Amount Per Hit: 10, Re-Trigger
    Delay: 1s).
  - `MeleeTest` — `StatDebugModifierToggle` (Target Stat: Melee Damage, Additive, +0.5, key F3).
  - `DefenseTest` — `StatDebugModifierToggle` (Target Stat: Defense, Additive, +0.5, key F4).
  - `PitchTest` — new `AudioSource` (Loop + Play On Awake on, **no clip assigned — none exists in
    the project yet**), `StatDebugAudioPitchTest`, and `StatDebugModifierToggle` (Target Stat:
    Audio Pitch, Multiplicative, ×1.3, key F5).
  - This surfaced a real bug fixed alongside it: `StatDebugModifierToggle` and
    `StatDebugAudioPitchTest` originally had `[RequireComponent(typeof(PlayerStats))]`, assuming
    they'd live on the `Player` GameObject. On a standalone object that would have cascaded into
    Unity auto-adding a duplicate `PlayerStats` → `PlayerController` → `CharacterController`
    stack. Fixed by dropping that attribute and having both find the scene's one `PlayerStats`
    via `FindFirstObjectByType` at `Awake` (Awake-time lookup, not per-frame, per
    `Docs/csharp-conventions.md`) when the serialized field is left blank.
  - Left the scene **unsaved** — Carlos is repositioning the Cubes next and will save when done.

**DECISIONS (cont. 2)**

- **Two more scene-view fixes, at Carlos's explicit request** (per the updated §B.3 policy —
  offer, get permission, act via UnityMCP, verify by reading state back):
  - **`PitchTest`'s `AudioSource` was inaudibly flat regardless of distance** — its `Spatial
    Blend` defaulted to `0` (2D) when added, which ignores listener distance entirely; the
    clip's importer being "3D-capable" doesn't set that. Fixed: `spatialBlend = 1`,
    `minDistance = 1.5`, `maxDistance = 8` — calibrated against the actual Sandbox `Plane`
    (20×20, scale 2 on the default 10×10 primitive) and `PitchTest`'s position (9 units from
    the Player's spawn), so it's inaudible at spawn and clear on approach rather than an
    arbitrary guess.
  - **`StatDebugModifierToggle` (keypress-based) replaced with `StatDebugModifierZone`
    (trigger-based),** at Carlos's request for consistency with `HealthTest`'s walk-in
    interaction model — deleted the old script (after removing its components from the scene
    first, to avoid a missing-script reference) and added the new one, which applies its
    modifier `OnTriggerEnter` and removes it `OnTriggerExit`, same shape as
    `StatDebugPoolZone`. Moved from living on the `MeleeTest`/`DefenseTest`/`PitchTest` parent
    objects onto their `Cube` children (set to `Is Trigger`), since that's where the collider
    actually is. Settings preserved from the keypress version: Melee +0.5 additive, Defense
    +0.5 additive, Pitch ×1.3 multiplicative.

**NEXT**

- **Carlos:** `PlayerStats`/`PlayerStatsDebugOverlay` are already on `Player`, and the four debug
  test objects are wired (see DECISIONS above). Remaining: reposition the Cubes, assign a looping
  test clip to `PitchTest`'s `AudioSource`, save the scene, then confirm live against the
  acceptance criteria — Tracey's movement speed comes from `PlayerStats.Speed`, jump/sprint refuse
  at 0 stamina, sprint falls back to a walk, stamina drains/regenerates correctly, jumping costs
  stamina, F2 toggles the debug HUD, pitch transitions are smooth (F5 on `PitchTest`), and F3/F4
  visibly stack on Melee/Defense via the HUD.
- Unblocks: any future item that reads `PlayerStats.MeleeDamage`/`.Defense` in a damage
  calculation, MRM-11's `stat` verb (lock/unlock), the Pickaxe issue (`ConsumeSwingStamina`),
  Carlos's breathing sound pools on `OnStaminaTired`/`OnStaminaHyperventilating`.

---

## MRM-8 (follow-up) — Look X-axis was silently inverted

**BUILT**

- `InputSystem_Actions.inputactions`: the `Look` action's processor changed from
  `invertVector2(invertY=false)` to `invertVector2(invertX=false,invertY=false)`.

**DECISIONS**

- **Root cause, confirmed not guessed:** `UnityEngine.InputSystem.Processors.InvertVector2Processor`
  defaults **both** `invertX` and `invertY` to `true` (verified live by instantiating one via
  `execute_code`). MRM-8's binding only explicitly set `invertY=false` — to make Y toggleable via
  the `InvertYAxis` tunable — never touching `invertX`, which silently stayed at its inverted
  default. Symptom: moving the stick or mouse right turned the camera left. Caught by Carlos
  during MRM-9 look testing.
- Done inline on MRM-9's branch, same deliberate exception as the mouse-scroll and sprint/jump
  fixes earlier this session — not re-confirmed individually since the pattern was already agreed.

**FAILED**

Nothing to record.

**NEXT**

- **Carlos:** confirm stick/mouse right now turns the camera right in a live test.

---

## MRM-9 (wrap-up) — All acceptance criteria confirmed, ready to commit

**BUILT**

- Confirmed live on itch.io (build `7 - Player Controller`): move, look, jump, crouch (toggle,
  smooth transition), sprint, jump-blocked-while-crouched, look-down shows placeholder body, all
  working in an actual WebGL build. Every tunable reads `Tunables.I` live each frame by
  construction (no caching), covering the "changes take effect in play mode" criterion by design.
- `Player.prefab` and `Controller UI Test.prefab` — Carlos saved both so they're reusable across
  scenes, not just live in `Sandbox`.
- All six MRM-9 acceptance criteria ticked in Linear.

**DECISIONS**

- **Incidental, unexplained diff worth flagging for the commit:** `ProjectSettings.asset`'s
  `preloadedAssets` list dropped its one entry (the Input System actions asset) to empty at some
  point during this session's build/testing cycle. Not an intentional change, cause not
  identified — likely a Unity-internal side effect of the build or a settings save. Everything
  tested fine regardless (WebGL build works, input capture works), but flagged rather than
  silently included in the commit.

**NEXT**

- Ready to commit and merge to `main`. See commit proposal in this session's conversation.
- **Recommended next issue: MRM-12** (core stat framework) — fully unblocked, Sonnet-scoped,
  doesn't need terrain/environment, and plugs directly into the `OnJumped`/`OnSprinting` hooks
  this issue already exposed.

---

## MRM-9 (in progress) — Sprint-backward and stationary-jump bugs found in testing

**BUILT**

- **Sprint now requires a forward move component** (`moveInput.y > 0f`), not just any movement.
  Holding Sprint while backing up or pure-strafing now walks instead. Flagged by Carlos: sprint
  backwards felt wrong.
- **Jump/landing no longer trusts `CharacterController.isGrounded`.** Confirmed live via the
  Unity MCP bridge: dropped the player onto the flat `Sandbox` plane, let it settle, and
  `isGrounded` read `false` continuously while completely at rest (not a one-frame flicker —
  sampled twice, ~11 seconds apart, both `false`). That's why jump only worked while moving:
  continuous horizontal collision resolution was masking the same underlying flakiness. Replaced
  with `PlayerController.CheckGrounded()` — a short downward `SphereCast` against a new `Ground`
  physics layer — used for both the vertical-velocity-reset check and jump eligibility. Confirmed
  fixed the same way: same drop-and-settle test, `CheckGrounded()` now reads `true` at rest.
- New tunable `GroundCheckDistance` (0.2, not in MRM-9's original list — added because the
  acceptance criterion "jump works" can't be met without a reliable grounded check).
- **New `Ground` physics layer created** (was documented in `Docs/unity-conventions.md` but never
  actually created in the project). Assigned to the `Sandbox` scene's test `Plane`. **This is now
  a hard requirement for every floor/terrain surface**, including MRM-58's terrain blockout — see
  the conventions doc's updated layers table.

**DECISIONS**

- **Fixed the ground check at the engine-reliability level (SphereCast), not by adding a fudge
  factor to the existing `isGrounded` reads.** `CharacterController.isGrounded` is a documented
  Unity flakiness point, not something a tunable epsilon can paper over reliably.

**FAILED**

First attempt at verifying the fix via `EditorApplication.update` polling registered from within
a single `execute_code` call showed frozen position across 114 logged "frames" — misleading, not
an actual repro. The callback fired in a rapid synchronous burst rather than at real per-frame
intervals, so the real Play Mode loop (and `PlayerController.Update()`) never got a chance to
interleave. Switched to plain sequential position/state checks with real wall-clock waits between
separate tool calls instead, which reflected genuine elapsed game time.

**NEXT**

- **Carlos:** re-test sprint (forward only), jump-while-stationary, and jump-while-crouched-blocked
  in the Sandbox scene to confirm before the next WebGL build.
- **Whoever builds MRM-58's terrain blockout must assign it to the `Ground` layer** or the player
  won't be able to jump on it. Flagged in `Docs/unity-conventions.md` and as a comment on MRM-58
  itself.
- Rest of MRM-9's acceptance criteria (WebGL build test, live-tunable spot check) still open —
  see MRM-9's own comments.

---

## MRM-8 (follow-up) — Mouse scroll wheel added to the input debug overlay

**BUILT**

- `InputDebugOverlay.cs` now also reports mouse scroll. `Mouse.scroll` is a `Vector2Control`,
  not a `ButtonControl`, so it never fired the existing `InputSystem.onAnyButtonPress` listener
  — that's why only key/button presses showed before. Added a `CheckMouseScroll()` poll in
  `Update()`: `Mouse.current.scroll.y` reports the frame's scroll delta directly (0 when idle,
  no `wasPressedThisFrame`-equivalent needed), reported through the same `FindBoundActions`
  lookup and `_lastPress` display path as button presses.

**DECISIONS**

- **Done inline on MRM-9's branch, not a new issue or MRM-8's own branch.** This is MRM-8-owned
  code (already `Done` in Linear) touched while testing MRM-9 — a deliberate, explicit exception
  to the project's one-issue-one-branch rule, confirmed with Carlos rather than assumed. Recorded
  here and as comments on both MRM-8 and MRM-9 for traceability.

**FAILED**

Nothing to record.

**NEXT**

Nothing outstanding — this was a small, complete addition.

---

## MRM-8 (wrap-up) — Two real bugs found chasing a debug overlay that "wasn't showing"

**BUILT**

- Confirmed, live on itch.io: the 960×540 embed (see below), the `InputDebugOverlay` (MRM-8),
  keyboard and gamepad capture, and action-name lookup all work correctly together. Console:
  `[InputDebugOverlay] Started`, `Gamepad added 1`, zero errors.
- **MRM-66** — a new issue capturing the full checklist of everywhere the target resolution
  number lives (Player Settings, itch.io embed, docs, every future UI Canvas Scaler, the build
  and zip steps), so a future swap to 1280×720 or anything else doesn't require re-discovering
  any of what follows.

**DECISIONS / FAILED — the two real bugs, for the record**

What looked like one mystery ("the debug overlay never shows up in the browser, no matter what
Unity setting we change") was actually two unrelated bugs stacked on top of each other. Neither
was a Unity or WebGL Template problem, despite that being the leading theory for most of the
session:

1. **A stale itch.io upload flag.** The very first build ever uploaded to this project — from
   before any of today's work — stayed flagged "This file will be played in the browser" through
   every subsequent re-upload. Every test that session was silently re-running that original
   build; newer uploads just sat there unused. Confirmed by noticing the served file's content
   hash never changed across builds with genuinely different settings. **Fix:** delete stale
   uploads rather than leaving them, and always confirm the *newest* file is the one flagged —
   checking that flag on an already-uploaded file after the fact doesn't reliably force itch to
   re-process it as HTML; a fresh upload with the flag set from the start is safer.
2. **A zip tool writing invalid path separators.** PowerShell's `Compress-Archive` — and, on
   this machine, even .NET's `ZipFile.CreateFromDirectory` — wrote literal backslash (`\`) path
   separators into nested zip entries (e.g. `Build\...loader.js`) instead of the forward slash
   (`/`) the ZIP spec requires. Windows tools don't care; itch's Linux servers extract a single
   garbled flat filename instead of a real subfolder, so `Build/...loader.js` 404'd while
   top-level files like `index.html` loaded fine. **Diagnosed by inspecting `.FullName` on the
   zip's entries — which was itself misleading (it can normalize for display); the real proof
   came from reading the zip's raw bytes and searching for the literal separator character.**
   **Fix:** never use `Compress-Archive`. Build the zip entry-by-entry, explicitly replacing `\`
   with `/` in each relative path before calling `CreateEntry`. Verify with the same raw-byte
   check before trusting a zip, going forward — recorded in the build-process memory.

**NEXT**

- **MRM-66** exists for the next resolution change; nothing else to carry forward from this
  detour — MRM-8 and MRM-10 are both otherwise complete.

---

## MRM-10 (in progress) — Display target changed to 960×540 embedded, not fullscreen

**BUILT**

- `PlayerSettings.defaultWebScreenWidth/Height` → **960×540** (was 1920×1080).
- `Docs/webgl-constraints.md` — target line updated; decision recorded inline with rationale.
- `Docs/webgl-budget.md` — original canvas-resolution audit row annotated as superseded, left
  in place rather than rewritten (it's a point-in-time record of MRM-6's spike).
- MRM-10's own Scope bullet ("itch.io embed configured: 1920x1080, fullscreen button enabled")
  rewritten to the new 960×540/embedded spec, plus a comment documenting the full decision.

**DECISIONS**

- **Embedded at a fixed 960×540, not launched fullscreen.** While testing MRM-8's input debug
  overlay in a real itch.io browser build, the persistent branding/fullscreen-button bar from
  Unity's `Default` WebGL template only fully disappeared in true fullscreen — and true
  fullscreen has its own letterboxing quirks across monitor aspect ratios (see the WebGL
  Template decision below). Carlos chose to sidestep the whole problem: embed the game at a
  fixed size in the itch.io page instead of chasing fullscreen behavior.
- **960×540, specifically, because it's an exact 2× divisor of 1920×1080** — the resolution
  everything was originally authored against — so nothing scales blurrily. It also quarters the
  fill-rate cost of every full-screen post-processing pass (fear vignette, chromatic aberration,
  etc. — the project's biggest per-pixel cost per `Docs/webgl-budget.md`), and a slightly softer
  canvas suits a 1979-set game better than a crisp full-HD window.
- **Superseded, not deleted:** MRM-6's original "1920×1080 fullscreen" target predates this
  decision by one day and was a reasonable call at the time — WebGL's fullscreen/embed quirks
  only became visible once an actual build went up against actual browsers, which is the whole
  point of MRM-10 running early (see that issue's own "why issue four, not forty" framing).

**FAILED**

Nothing to record.

**NEXT**

- ~~Carlos, itch.io account access required: switch the embed mode...~~ **Done** — itch.io embed
  mode is now "Embed in page", manually-set viewport 960×540, fullscreen button left disabled
  (Carlos wants fullscreen unavailable entirely, not just unused, so the game's own itch.io page
  — title, branding — always stays visible around the embed as a recall/marketing device).
- **A fresh build is still needed to actually ship this** — build folder `3` (`WebGL Template`
  fix, made minutes before this decision) still has the old 1920×1080 canvas baked in.
- **960×540 is now this project's UI reference resolution, not just a display setting.** Flagged
  on every not-yet-built UI/HUD issue: MRM-18 (main menu), MRM-19 (pause/game over), MRM-65 (UI
  polish — the issue that actually builds the title logo and button styling, most affected),
  MRM-46 (difficulty modes' health/stamina bars), MRM-53 (damage feedback HUD wounds + full-
  screen VFX, which also get cheaper at this resolution).
- **Added a prominent line to `CLAUDE.md` itself** (not just `Docs/webgl-constraints.md`) so the
  960×540 target is impossible to miss at the start of any future session, with an explicit note
  that any `1920×1080` reference found elsewhere is stale.
- **No camera or scene changes needed for the resolution itself** — 960×540 is exactly the same
  16:9 aspect ratio as 1920×1080 (half-scale, not a different shape), and Unity cameras frame by
  aspect ratio, not absolute pixel count. Only pixel-space UI (Canvas Scaler reference resolution,
  hardcoded layout) needed flagging, which is what the issue comments above do.
- Confirmed live in build 4: renders at 960×540 embedded, black page background applied, gamepad
  detected via console (`Gamepad added 1`). Re-verify the `InputDebugOverlay` (MRM-8) is visibly
  legible next time — it wasn't confirmed on-screen in the latest test screenshot (a DevTools
  panel was open taking half the browser width, which may just be cropping it out of view).

---

## Incidental — WebGL Template switched to Minimal

Found while diagnosing "the debug overlay isn't visible in the itch.io build" (MRM-8 testing).
Not a bug: the canvas was already scaling correctly to fill the browser window (confirmed live
against the actual itch.io page, both the direct HTML page and a fresh automated load — no
letterboxing, no fixed-size box). The "thin bar" Carlos saw at the bottom was Unity's `Default`
WebGL Template's own footer (branding + its built-in fullscreen button), confirmed via
`PlayerSettings.WebGL.template == "APPLICATION:Default"`.

**Changed:** `PlayerSettings.WebGL.template` → `APPLICATION:Minimal` (Carlos confirmed). Canvas
now goes edge-to-edge with no Unity branding strip. itch.io's own "Click to launch in
fullscreen" page setting already triggers fullscreen independently of Unity's in-canvas button,
so nothing else needed to change. Aspect-ratio letterboxing on non-16:9 monitors is expected
and unaffected — that's Unity preserving the 1920×1080 image rather than distorting it, not a
bug to fix.

Not logged against a specific issue — this affects whichever issue eventually owns "first WebGL
build on itch.io" (MRM-10). No code change, no tunable, no scene touched.

---

## MRM-8 — Input System — Xbox and keyboard/mouse control schemes

**BUILT**

- `Assets/InputSystem_Actions.inputactions` — extended in place, not replaced. `Player` map
  renamed **`Gameplay`** to match the issue's named maps. All fifteen table actions exist:
  `Move`, `Look`, `Fire`, `Interact`, `Crouch`, `Jump`, `Sprint`, `AimDownSights`, `Reload`,
  `SwitchWeapon`, `EquipMelee`, `FlashlightToggle`, `BootsToggle`, `InventoryScroll`, `Pause` —
  each bound exactly to its Xbox control from the issue's table (left/right stick, both
  triggers, both bumpers, all four face buttons, full d-pad, Start). `Crouch` moved off
  `buttonEast`(B) onto **right-stick press** and `Interact` off `buttonNorth`(Y) onto
  **`buttonWest`**(X) — the stock template had them on the wrong buttons relative to this
  issue's table. Added three new, currently-empty action maps — `Turret`, `Stretcher`,
  `Cutscene` — as switch targets for their own future issues. `UI` map kept, trimmed of
  Touch/Joystick/XR bindings and actions (`TrackedDevicePosition`/`TrackedDeviceOrientation`)
  not relevant to this project's WebGL + Xbox + KB/M target. Control schemes trimmed to just
  `Keyboard&Mouse` and `Gamepad` for the same reason.
- **C# wrapper class generation turned on** for the asset (`generateWrapperCode` in the
  importer, was off) — the wrapper lands at
  `Assets/_Project/Code/Runtime/Input/InputSystem_Actions.cs`, namespace `MrMoonlight.Input`,
  auto-regenerated on every reimport. Required adding `Unity.InputSystem` to
  `MrMoonlight.Runtime.asmdef`'s `references` (it had none before this issue).
- `Assets/_Project/Code/Runtime/Input/InputMapController.cs` — the load-bearing piece. Owns
  one `InputSystem_Actions` instance; `SetMode(InputMode)` disables every map and enables only
  the target one (verified live: Gameplay → UI → Cutscene, each transition fully exclusive).
  Not a MonoBehaviour and not a singleton — whatever needs input constructs one in `Awake` and
  calls `Dispose()` in `OnDestroy`, per this project's "no singletons but `Tunables`" rule.
- Two new tunables on `MoonlightTunables`, header **Input System — MRM-8**: `StickDeadzone`
  (0.125 default) and `InvertYAxis` (false default). Verified live via `execute_code`: the
  constructor writes `StickDeadzone` into `InputSystem.settings.defaultDeadzoneMin` (project-wide,
  since `Gamepad`'s stick controls already fall back to that default — confirmed in
  `Gamepad.cs`) and applies `InvertYAxis` as a runtime parameter override on the `Look` action's
  `invertVector2` processor.

**DECISIONS**

- **No `PlayerInput` component, no `InputUser` pairing, no explicit control-scheme-switch
  code.** Both control schemes stay bound and enabled simultaneously — nothing restricts the
  asset to one device. A newly-connected gamepad therefore "just works" the instant its stick
  or button fires, with no restart and no scheme-change plumbing needed. This satisfies the
  issue's hot-plug acceptance criterion for free; a later HUD/prompts issue can still add
  explicit current-scheme detection (e.g. via `InputUser`) if button-icon prompts need it —
  that wasn't asked for here and would have been scope creep.
- **Keyboard bindings for the nine new actions are placeholders**, not Carlos's prepared
  template — it wasn't in the repo when this issue was picked up. Chosen conventionally (R
  reload, Q switch weapon, V equip melee, F flashlight, B boots, Escape pause,
  `[`/`]` + mouse wheel for inventory scroll). **Flagging this: swap these for Carlos's real
  keyboard template as soon as he supplies it** — only the keyboard column needs touching: all
  Xbox bindings are final, straight from the issue's table.
- **Stick deadzone applied via `InputSystem.settings.defaultDeadzoneMin`, not a per-binding
  processor.** `Gamepad.cs` shows the stick controls already default to `stickDeadzone` with no
  explicit min/max, which reads project-wide settings — adding a second explicit processor on
  top would have clamped twice for no benefit.
- **`InventoryScroll` (d-pad left/right, `[`/`]`, mouse wheel) does double duty as both the
  open trigger and the navigate control, confirmed with Carlos and matching MRM-42's existing
  spec** ("Opens on D-pad left/right or mouse wheel"). No separate "open inventory" binding
  exists or is needed — a single `InventoryScroll` read, interpreted differently depending on
  whether the inventory panel is currently open, is MRM-42's job. Corrected from this entry's
  first draft, which had wrongly flagged this as a missing binding.
- **Added `InputDebugOverlay.cs`**, a throwaway `OnGUI` readout of the last button/key pressed
  on any device, via `InputSystem.onAnyButtonPress` — the same pattern shown in Unity's own API
  docs for this exact purpose. Requested by Carlos so he can visually confirm input capture
  before MRM-9 lands a player to react to it. Toggle: F1 (keyboard), Select/View (gamepad), or
  the inspector checkbox. Not wired into any scene — see NEXT.

**FAILED**

Nothing to record.

**NEXT**

- **Unblocks MRM-9** (player controller) — `Gameplay.Move`/`Look`/`Jump`/`Crouch`/`Sprint` are
  ready to read from.
- **Carlos:** confirm the Xbox scheme in an actual browser build (editor gamepad support
  differs from WebGL's Gamepad API per `Docs/webgl-constraints.md` §7) with your Logitech
  controller, and hand over the keyboard binding template so the nine placeholder keys above
  can be swapped for real ones. **Also: drop `InputDebugOverlay` onto any empty GameObject** in
  a test scene (e.g. `SampleScene`, until Sandbox exists) to try it — that wiring is yours per
  the usual rule.
- **Deferred:** `Turret`, `Stretcher`, `Cutscene` maps exist but are empty — each fills in when
  its own issue lands. Current-control-scheme detection/exposure (for button-icon HUD prompts)
  also deferred, not required by this issue's acceptance criteria.
- **MRM-42 comment added**: which underlying actions its "open/navigate" (`InventoryScroll`),
  "use" (`Jump`/A), and "close" (`EquipMelee`/B) mechanics read from, and a flagged open
  question — whether the inventory stays in the `Gameplay` map or borrows the `UI` map while
  open — left for whoever picks that issue up, since MRM-42's own "no pause, Tracey stays
  vulnerable" design means it isn't a clean full mode-switch like Turret/Stretcher/Cutscene.

---

## MRM-7 — MoonlightTunables — central constants asset + inspector pattern

**BUILT**

- `Assets/_Project/Code/Runtime/Data/MoonlightTunables.cs` — the `MoonlightTunables`
  `ScriptableObject`. Sixteen fields across three `[Header]` groups, each with an XML doc
  comment naming its owning issue: **Player Movement — MRM-9** (walk/sprint/crouch speed,
  mouse and stick look speed, look acceleration, jump height and speed, crouch height delta,
  crouch transition duration, slope limit, gravity — defaults per MRM-9's proposed starting
  values where it gave one, sensible placeholders otherwise), **Pathfinding — MRM-27** (the
  three tunables MRM-6 obliged this asset to carry: ms-per-frame budget, max concurrent
  agents, repath interval), **Mine Lighting — MRM-60** (max real-time lights).
- `Assets/_Project/Code/Runtime/Data/Tunables.cs` — the single access point, `Tunables.I`,
  a lazy `Resources.Load<MoonlightTunables>("MoonlightTunables")`. The project's only
  sanctioned singleton and only sanctioned `Resources.Load` call, per
  `Docs/csharp-conventions.md` and `Docs/unity-conventions.md`.
- Per-instance override pattern documented on the `MoonlightTunables` class doc comment
  (mirrors the example already in `Docs/unity-conventions.md`): a tunables value is the
  default, a component may carry `[SerializeField] bool overrideX` + `[SerializeField] float
  xOverride`, and a computed property picks between them. Both fields show in the inspector,
  not just a checkbox. Reuse this shape everywhere a value needs a shared default plus a
  per-instance override (per-enemy cone distance, per-weapon spread, etc.) rather than
  inventing a new one per system.

**DECISIONS**

- **Player-movement values seeded from MRM-9's own issue text**, not left empty. MRM-9 is
  blocked by this issue and hasn't started, but its description already proposes
  walk/sprint/crouch/crouch-transition defaults — using those (and sensible placeholders for
  the rest of its listed tunables: look speed/acceleration, jump height/speed, crouch height
  delta, slope limit, gravity) means MRM-9 lands with values to tune rather than a still-empty
  asset. This is the one exception to "populating it is not in scope" that the issue itself
  calls out.
- **`JumpHeight` and `JumpSpeed` are both fields**, not one derived from the other, even
  though a physics controller could compute takeoff velocity from height and gravity. MRM-9's
  tunables list names them separately; keeping both lets Carlos tune the felt takeoff speed
  without back-solving the math.
- **Script location follows the repo's actual `Code/Runtime` / `Code/Editor` split** (from
  MRM-6), not the `Scripts/Player`, `Scripts/Weapons`, etc. subfolders shown in
  `Docs/unity-conventions.md` — that split doesn't exist yet in this repo. Used a `Data/`
  subfolder under `Code/Runtime` to match the ScriptableObject-definitions intent from the
  conventions doc.

**FAILED**

Nothing to record.

**NEXT**

- **Carlos:** create the `MoonlightTunables` asset instance — `Create > MrMoonlight >
  Tunables` — and place it inside a folder literally named `Resources` so `Tunables.I`
  resolves it (e.g. `Assets/_Project/Data/Resources/MoonlightTunables.asset`), named exactly
  `MoonlightTunables`. Confirm a value change in the inspector takes effect in play mode
  without a recompile, then this issue's acceptance criteria are done.
- **Unblocks MRM-9** (player controller), which now has a tunables asset and seeded defaults
  to build against.
- **Feeds MRM-27** and **MRM-60** with the four tunables MRM-6's comment obliged.

---

## MRM-6 — [SPIKE] WebGL viability decision + build budget

**BUILT**

- `Docs/webgl-budget.md` — the viability decision, the MB budget table, 17 WebGL traps with
  mitigations, texture and audio import preset specs, the project setup sequence, and the
  results of the first live test.
- `Docs/changelog.md` — this file.
- `Assets/_Project/Settings/Web_RPAsset.asset` + `Web_Renderer.asset` — a dedicated WebGL
  render tier. Forward+, depth and opaque textures on, render scale 0.8, MSAA off, main-light
  shadows at 1024 with 2 cascades, additional-light shadows off, soft shadows off.
- `Assets/_Project/Settings/Presets/` — **17 import presets** (9 texture, 8 audio), wired into
  the Preset Manager so imports self-sort by filename prefix.
- Project settings: web canvas 960×600 → **1920×1080**; WebGL initial memory 32 → **512 MB**;
  `nameFilesAsHashes` on; managed stripping **Medium**; audio real voices 32 → **24**, DSP
  buffer → Best Performance.
- New `Web` quality level, with WebGL pointed at it and the availability matrix locked down.
- `Packages/manifest.json` — **9 dependencies removed** (46 → 37).

No runtime code. No tunables — this issue produced a document. The four tunables it *implies*
are logged against MRM-7.

**DECISIONS**

- **GO on WebGL.** The governing number is a **~300 MB build, not 1 GB.** The Assignment #10
  gate is wall-clock: after ~26 s of fixed overhead only ~94 s of download remains, which is
  ~294 MB at 25 Mbps. A 1 GB build only passes above ~100 Mbps.
- **Overage is not a stop-work** (Carlos). Above target we ship a loading notice on the itch.io
  page rather than cutting content. 450 MB is a review line, not a hard stop. Build size is
  explicitly **not** a no-go trigger; only "does not run in a browser" is.
- **All cutscenes are in-engine runtime. No pre-rendered video ships.** Video budget fixed at
  0 MB and `com.unity.modules.video` removed.
- **One custom full-screen pass, not four.** URP already folds chromatic aberration, vignette,
  colour grading, film grain, lens distortion and tonemapping into a single `UberPost` pass, so
  those stack free. Radial blur, double vision and tunnel vision become one weighted
  `MoonlightScreenFX` feature — assigned to MRM-53, which makes MRM-54/55/56/57 "add a weight."
- **Audio is not the biggest risk — wrong import settings are.** 250 dialogue lines are 264 MB
  as stereo WAV and 20 MB as mono Vorbis. Mitigation is project-wide presets, not content cuts.
  Textures are the real pressure at 143 MB.
- **IL2CPP, not the CoreCLR backend.** Unity's manual labels CoreCLR experimental; the whole
  §8 settings chain and the 25 MB code budget assume IL2CPP.
- **WebGL 2.0, not WebGPU.** Not gambling a graded deadline on a browser feature the grader may
  not have.
- **Rejected:** giving WebGL the existing `Mobile` tier (no depth or opaque texture — silent
  VFX breakage in the browser only); hand-editing MCP for Unity's asmdef (third-party, lost on
  update, and Medium stripping should handle it).

**FAILED**

Three claims in the first draft of `webgl-budget.md` were wrong. Corrected in place, recorded
here so they are not retried:

- **"Turn on lightmap/fog/instancing shader stripping — all three are currently off."** Wrong.
  All three read `0`, which the Editor's own enums confirm is `Automatic` / `StripUnused`,
  i.e. stripping already enabled. Setting them to `Custom` requires hand-listing modes to keep
  and a wrong list breaks lighting or fog **in the build only**.
- **"Remove `com.unity.modules.screencapture`."** Wrong. MCP for Unity's `ScreenshotUtility.cs`
  calls `ScreenCapture.CaptureScreenshot`; removing it breaks the Editor bridge.
- **"Remove `com.unity.modules.umbra` — URP does not use it."** Wrong. Umbra *is* Unity's
  occlusion culling and is pipeline-independent. A forest island wants it.

Also downgraded: pruning `DefaultVolumeProfile` is **not** a build-size win — post-processing
shaders ship via the renderer's `PostProcessData` regardless. Moved to MRM-47 as tidiness.

**NEXT**

- **Validated live.** Empty build **~10 MB**, uploaded to itch.io as project kind `HTML`, runs
  fullscreen. **Brotli is served correctly** — no Decompression Fallback, no Gzip. Confirmed on
  the real platform: WebGL 2.0, DXT via `s3tc`, BPTC, `KHR_parallel_shader_compile`, PhysX
  single-threaded, and the audio context resuming on the fullscreen click.
- **Unblocks MRM-10**, which is now mostly done — what remains is the build report, the page
  loading notice, log stripping, and cold-cache timing from a machine that has never seen it.
- **Constrains** MRM-7 (4 tunables), MRM-15 (no video), MRM-18 (**no percentage on the loading
  bar** — itch.io sends no `Content-Length`), MRM-27 (single-threaded A\*), MRM-47 (4 skyboxes),
  MRM-53 (`MoonlightScreenFX`), MRM-58 (terrain tier values), MRM-63 (preset filename prefixes),
  MRM-64 (10 MB baseline).
- **Deferred:** three URP internal shaders fail under GLES 3.0 — `CoreSRP/CoreCopy`,
  `StencilDitherMaskSeed`, `HDRDebugView`. Nothing visibly broke; re-check at MRM-58 (LOD
  cross-fade) and MRM-53 (copy paths).
- **Not created:** `Docs/optimization.md`. It belongs to MRM-64; the baseline and first two
  entries are waiting in that issue's comments.
- **Open questions:** on-screen character count (MRM-63), surface-world SSAO (currently off),
  hero skybox resolution (MRM-47).
