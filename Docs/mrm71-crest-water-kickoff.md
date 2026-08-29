# MRM-71 — Crest water kickoff

**Written 2026-08-28, end of the MRM-70 island-regeneration session.** Read this, then wait for
Carlos's go-ahead — standing rule, same as `mrm70-gaia-kickoff.md`.

---

## What this session is

**Two things, in this order, in the same session:**

1. Implement **Crest Water 5** (already decided, already owned — see
   `Docs/terrain-vegetation-tooling-decision.md` §6 for the full decision record and
   `Docs/water-shader.md` for the superseded-shader history) at the new island's sea level.
2. **Then continue shaping the island** (the erosion + manual adjustment pass flagged as open item
   #1 in `mrm70-gaia-kickoff.md`'s pivot section) — **Carlos's explicit call**: he wants real water
   in and visible *first*, so the shoreline is actually judgeable, before doing more terrain work.
   Don't treat water and terrain as separate sessions — water unblocks the terrain judgment call,
   it isn't the whole scope.

**This is a new/separate context from the MRM-70 terrain-regeneration session** — read
`Docs/mrm70-gaia-kickoff.md`'s **"MAJOR PIVOT, 2026-08-28"** section first for what the island
terrain actually looks like right now (new 4km×4km square terrain, replaced the old one, still
being eroded/adjusted in parallel).

## Do not re-derive — already decided

- **Crest Water 5**, not Crest 4, not KWS2. Owned, $240, 60.6 MB UPM package.
- **Look: CRT pass and nothing else.** No PSX/RetroLit treatment, no editing Crest's shader graph,
  no migrating it to `RetroLit`. Leave Crest 5's extra realism features off by default.
- Full rationale, alternatives considered, and the six open gaps (Packages/ gitignore question,
  renderer feature ordering on PC_Renderer, exact version recording, no-prefab-mode limitation,
  etc.) are already written up in `terrain-vegetation-tooling-decision.md` §6 — **read the gaps
  table before starting**, several are unverified and flagged "confirm during MRM-71" (i.e. now).

## What's different this time: the sea level target

Carlos's explicit spec for the new island (from the MRM-70 session): **sea level = world Y 0**,
island peak = 200m ceiling. The old placeholder water (`SeaGrid.mesh` + `M_Sea.mat`) was
deleted from the scene during the MRM-70 regeneration — **the mesh/material assets themselves were
deliberately kept on disk** (`Assets/_Project/Art/Environment/Water/`), per the standing
"do not delete" rule in `water-shader.md`, but there is currently **no live water object in
`Island.unity` at all.** Crest gets built fresh, not by reviving the old GameObject.

Open question carried over from MRM-70: whether Y=0 is the final call or whether the shoreline
looks better at a different height once Crest is actually in and visible — the MRM-70 session
never fully settled this because the Stamper's "Sea Level" preview field (a Gaia-only preview
value, see `Docs/gaia-stamper-lessons-learned.md`) doesn't correspond 1:1 to where real water
would sit. Confirm the real number with Carlos once Crest is in and visible.

## Working rules

Same as every other session on this project — `CLAUDE.md` hard rules apply: ask permission before
Unity work, then do it, verify by reading state back, document. Never commit or push.

## First water pass built, 2026-08-29

Landed at the same time as the [[mrm70_world_designer_pivot|second MRM-70 pivot]] (Stamper →
World Designer, so Carlos can shape the island in the Inspector). While he works the island shape,
he asked for a placeholder sea now — default settings, fixed at **Y=18** (World Designer's current
*preview* Sea Level number, not necessarily final — expect to move this once the real island is
generated and the shoreline is judgeable).

**Acquisition:** Crest wasn't installed anywhere Claude could reach — not in Mr. Moonlight, not on
the public Unity registry (expected, paid closed-source UPM). Carlos bought/downloaded it into
**Playground** (`Assets/PLAYGROUND/Crest Water 5/`, a raw UPM package layout — has `package.json`,
`Runtime/`, `Editor/`, `Samples~/`), since that download step needs his own Asset Store account
click. From there it was a plain recursive file+meta copy (1300 files, verified count matched) into
`Packages/com.waveharmonic.crest/` in Mr. Moonlight — Unity auto-discovers any `Packages/` folder
with a `package.json` as an embedded package, same mechanism as Flora, no `manifest.json` edit
needed. Confirmed installed via `manage_packages`: `com.waveharmonic.crest` **v5.9.2**, source
Embedded. Zero compile errors after refresh.

**Built in `Island.unity`:**
- `Water` GameObject, layer `Water` (built-in layer 4), position `(0, 18, 0)` — **in Crest 5, sea
  level *is* the GameObject's Y position**, there's no separate hidden sea-level field on
  `WaterRenderer`. This is a real, useful finding: unlike Gaia's Stamper (whose "Sea Level" was
  preview-only and never touched the real heightmap, see `gaia-stamper-lessons-learned.md`),
  Crest's number is exactly what it looks like. Raising/lowering the sea later is just moving this
  Transform.
  - `WaterRenderer` component: `_Material` → `Runtime/Materials/Water.mat` (the package's own
    default, not a sample material — `Samples~` content isn't in the AssetDatabase at all, Unity
    hides `~` folders). `_Resources` → `Runtime/Settings/Resources.asset` (a required
    `WaterResources` singleton reference; its GUID matched the official sample's reference exactly,
    confirming the file+meta copy preserved GUIDs correctly). `_Underwater._Enabled` explicitly set
    **false** — Crest ships this **on** by default, so this had to be set, not just left alone, to
    honor the "Tracey doesn't enter water" call in `terrain-vegetation-tooling-decision.md` §6.
- `Water/Waves` child, `ShapeFFT` component (Crest 5's wave-shape input), `_Spectrum` →
  `Runtime/Data/WaveSpectra/WavesModerate.asset` — matches Wave Harmonic's own official sample
  scene's choice (same GUID appears in their `Main_Scene.prefab`), not a guess.

**Why this should already lean toward "calm near shore, rougher far out" without extra work:**
Crest's `AnimatedWavesLod` has shallow-water wave attenuation on by default (waves shrink as
depth shallows near the coast — reads directly from the terrain, no manual depth cache needed
since our island is a real Unity `Terrain`, not a mesh prop like Wave Harmonic's own sample uses),
*and* its LOD cascade system inherently renders coarser/larger waves at distance. Both push toward
what Carlos asked for by default. **Not yet tuned or verified in a real view** — the screenshot
taken was the Scene view during World Designer's stamp-preview mode, confirming the water renders
and sits at the right height, not a judgment of how calm/violent it actually reads.

**Updated 2026-08-29 — sea level moved from the Y=18 placeholder to Y=8**, once Carlos could
actually judge the shoreline against the settled island shape (`mrm70-gaia-kickoff.md`'s "TERRAIN
SETTLED" section). Just a Transform position change on `Water` — no other Crest settings touched.
Still not necessarily final — Carlos flagged possible further terrain deformation, so re-check this
number if the shoreline shape changes again.

**Left open, deliberately, until the island shape is settled:**
- ~~The Y=18 height itself~~ — resolved, see above. Re-confirm if the terrain changes again.
- Whether `WavesModerate` is the right spectrum once there's a real coastline to look at — swap is
  a one-field change (`Water/Waves`'s `ShapeFFT._Spectrum`), other built-in options already on disk
  at `Packages/com.waveharmonic.crest/Runtime/Data/WaveSpectra/` (Calm, Dead, ModerateSmooth,
  Shoreline, Swell).
- CRT-only look pass (scanlines/RGB mask on top, no shader edits) — not touched yet, water renders
  with Crest's own material as-is.
