# MRM-70 vegetation placement — kickoff prompt

Paste this to start a new session picking up vegetation placement. Written 2026-08-25, after the
asset-prep + in-engine prefab/material/TerrainLayer build finished, before any placement started.

---

## Prompt to paste

> Resuming MrMoonlight, branch `mrm-70`, Linear issue MRM-70 ("Island vegetation + terrain
> texturing pass", status **In Progress**). Read `CLAUDE.md` first, then
> `Docs/mrm70-prefab-build-summary.md` and `E:\Props\Environment\Prepared Props\_INVENTORY.md` for
> full context on everything already built — don't re-derive any of it. Also check the
> `mrm70_3d_pipeline` and `mrm70_vegetation_placement_kickoff` memory entries if this session has
> access to prior-session memory.
>
> **What already exists, ready to use:** 24 materials, 53 prefabs (`Assets/_Project/Prefabs/World/
> Vegetation/{RetroRealism,TerrainSampleAssets,GrassFlowers}/`), 51 TerrainLayer assets
> (`Assets/_Project/Art/Environment/Terrain/{TerrainSampleAssets,TerrainTexturesPack,Yughues,
> GrassFlowers}/Layers/`). All built and verified in-engine, zero console errors. Nothing is placed
> in the `Island` scene yet, nothing painted on the actual `Terrain` — that's this session's job.
>
> **The task:** place vegetation on the `Island` scene's `Terrain` (via Vegetation Spawner — Carlos
> owns the free version, NOT Flora Instancer, see `Docs/unity-conventions.md`/memory) and paint the
> TerrainLayers onto the terrain for the footstep sound system. Remaining MRM-70 acceptance
> criteria: vegetation placed + frame rate holds in a WebGL build; terrain layers assigned for
> footsteps (grass/leaves/wood/concrete); all 7 locations stay findable once vegetation is in.
>
> Ask Carlos for permission before touching the scene/Terrain, same as always — he's expecting to
> be asked per-piece, not a silent handoff. Verify every change by reading the actual scene/
> Terrain/component state back afterward, and document what changed.

---

## Facts worth knowing before starting

**Where things are:**
- Scene: `Assets/_Project/Scenes/Island.unity`. Terrain object is just named `Terrain`, layer
  `Ground`. Size 4103 × 260 × 7085 (X×Y-height×Z), heightmap resolution 1025 (~4-7m/cell — coarse
  block-out, Carlos hand-details on top).
- World orientation: **+X = east, +Z = north** in this scene.
  > **Corrected 2026-08-25.** This line previously said `-Z = north`, which is wrong. Chapel
  > (northernmost location on the map) is at Z 5668 and Camp/Dock (southernmost) at Z 4059-4273;
  > two further marker pairs at equal Z confirm it. See
  > `Docs/mrm70-biome-vegetation-strategy.md` §3 for the map-pixel → world transform derived from
  > the markers.
- 9 location blockout markers exist and are approved as-is (7 script locations + Flak Tower +
  Dock) — don't move them without asking, they're a settled decision (see changelog MRM-58 close).
- Player spawns at the campsite; `WalkSpeed` 3.0 m/s, `SprintSpeed` 5.5 m/s (both in
  `MoonlightTunables`).

**Prefab inventory (all in `Prefabs/World/Vegetation/`):**
- `RetroRealism/`: 21 prefabs (4 trees, 2 saplings, 2 stumps, 3 logs, 5 boulders, 3 bushes,
  2 ferns). All on one of 4 shared materials (`M_RF_Trees`/`Boulders`/`Bush`/`Fern`).
- `TerrainSampleAssets/`: 20 prefabs (bush/bushdry/fern/grass/grassdry/heather/plant families,
  A-D variants each). Native Unity meshes, 8 shared materials.
- `GrassFlowers/`: 12 crossed-quad card prefabs (grass/flower billboards, no real mesh).
- **Not built**: `Poplar_Tree01` — excluded, 8,198-20,248 tris, 10-65x the tree budget. Don't place
  it as terrain-instanced density; if Carlos wants it at all, it's a rare hand-placed hero tree,
  and it still needs to be built as a prefab first (was excluded from the game-ready cut).

**Polycount flags to respect when picking density** (full numbers in each source folder's
`analysis.md` under `E:\Props\Environment\Prepared Props\`):
- Cheap, safe for base density: RetroRealism's bushes/ferns (single digits-30 tris), most boulders
  (52-84 tris), TSA's `GrassDry`/`Plant_D`/`Heather_A` family (well under 200 tris).
- Moderate, use but don't oversaturate: RetroRealism Tree1/Tree4/Log1 (~300-800 tris, in-budget),
  TSA `Bush_A/B` (270-360 tris).
- Heavy, sparse/accent only: RetroRealism Tree2/Tree3 (976-1,378 tris), TSA `BushDry_A/B`
  (640-792), TSA `Grass_B/C/D` (1,000-1,268 tris each — these read as "grass" but are actually
  fairly detailed 3D clumps, not billboards, don't treat them like the cheap GFF cards).

**Budget constraints:**
- **1 GB total WebGL ceiling**, 960×540 embedded display target (not fullscreen).
- **Fog/draw distance is deliberately at 1500m** for stress-testing vegetation density — don't
  "fix" this by shortening it to hide a performance problem; that's covering the budget question,
  not answering it. If perf is bad at 1500m, that's real signal, bring it to Carlos rather than
  quietly reducing draw distance to compensate.
- GPU instancing is enabled on every vegetation material already (`enableInstancing = true`) —
  Vegetation Spawner's terrain-tree/detail instancing should pick this up automatically, no extra
  material work needed for that part.
- **Vegetation/staging numbers are deliberately NOT in `MoonlightTunables` yet** — Carlos's
  explicit call (place freely, see how a real WebGL build holds up, tunable-ize only once a real
  frame-rate/budget problem shows up). Don't add tunables preemptively for this pass.

**TerrainLayer notes:**
- None of the 51 layers are attached to the `Terrain`'s layer list yet — that's part of this
  session's job, along with actually painting them.
- Tile size on all of them is an untuned default (5×5m) — adjust once actually visible on the real
  terrain.
- For the footstep system specifically: check what's actually available for leaves/wood/concrete
  before assuming full coverage — the ground-texture packs prepared skew heavily toward natural
  ground types (grass/dirt/sand/rock/snow variants), a dedicated wood/concrete texture may not
  exist in what's been prepared and might need a new source.

**Standing project rules that apply here:**
- Ask Carlos before touching Unity scene/Terrain state, do it if approved, verify by reading actual
  state back, document what changed (`CLAUDE.md` hard rule).
- No hardcoded values in scripts — `MoonlightTunables`, except vegetation/staging numbers per the
  explicit exception above.
- Never commit/push — Carlos uses GitHub Desktop.
